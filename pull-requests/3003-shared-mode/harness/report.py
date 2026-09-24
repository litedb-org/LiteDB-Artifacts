import re, statistics, io, collections
import os
SP = os.environ["WORK_DIR"]
rows = []
for line in io.open(SP + "/fbench-raw.txt", encoding="utf-8"):
    line = line.strip().replace(",", ".")
    m = re.match(r"r(\d) (\w+) (\w+) (\w+) n=(\d+) (.*)", line)
    if not m:
        continue
    rnd, build, mode, scen, n, rest = m.groups()
    rec = dict(round=int(rnd), build=build, mode=mode, scenario=scen, n=int(n), blocked=rest.startswith("blocked"))
    for k in ("total_ms", "ms_per_op", "p50", "p99"):
        mm = re.search(k + r"=([\d.]+)", rest)
        rec[k] = float(mm.group(1)) if mm else None
    rows.append(rec)

with io.open(SP + "/benchmark-final.csv", "w", encoding="utf-8", newline="\n") as f:
    f.write("round,build,mode,scenario,n,blocked,total_ms,ms_per_op,p50_ms,p99_ms\n")
    for r in rows:
        f.write(",".join(str(x) for x in (r["round"], r["build"], r["mode"], r["scenario"], r["n"], r["blocked"],
                                          r["total_ms"] or "", r["ms_per_op"] or "", r["p50"] or "", r["p99"] or "")) + "\n")

groups = collections.defaultdict(list)
for r in rows:
    groups[(r["build"], r["mode"], r["scenario"])].append(r)

cols = [("pre", "shared", "Shared, pre-stack dev"), ("dev", "shared", "Shared, dev now"),
        ("imp", "shared", "Shared, improved"), ("imp", "coord", "Coordinator, improved"),
        ("pre", "direct", "Direct, pre-stack"), ("dev", "direct", "Direct, dev now"), ("imp", "direct", "Direct, improved")]
scen = [("upd", "Update loop (2000), ms/op"), ("ins", "Insert loop (2000), ms/op"), ("qry", "Point read FindById (4000), ms/op"),
        ("mixed", "1 update : 9 reads (2000), ms/op"), ("scan", "Full scan 2000 rows (200), ms/op"),
        ("held", "Update while another connection holds a reader (500), ms/op"),
        ("iter", "2000 updates while iterating a cursor, total s")]

def cell(key, field="ms_per_op"):
    g = groups.get(key)
    if not g:
        return "n/a"
    if all(r["blocked"] for r in g):
        return "blocked (writer waits for the reader)"
    vals = [r["total_ms"] / 1000.0 if key[2] == "iter" and field == "ms_per_op" else r[field] for r in g if not r["blocked"]]
    fmt = "{:.2f}" if key[2] == "iter" or max(vals) >= 1 else "{:.3f}"
    return "**" + fmt.format(statistics.median(vals)) + "** (" + fmt.format(min(vals)) + "–" + fmt.format(max(vals)) + ")"

out = ["| Scenario | " + " | ".join(c[2] for c in cols) + " |", "|---|" + "---|" * len(cols)]
for s, label in scen:
    out.append("| " + label + " | " + " | ".join(cell((b, m, s)) if not (m == "direct" and s == "held") else "not possible" for b, m, _ in cols) + " |")
p99 = ["| Scenario (p99 ms, median of 3 runs) | " + " | ".join(c[2] for c in cols) + " |", "|---|" + "---|" * len(cols)]
for s, label in scen:
    vals = []
    for b, m, _ in cols:
        g = [r for r in groups.get((b, m, s), []) if not r["blocked"] and r["p99"] is not None]
        vals.append("{:.2f}".format(statistics.median(r["p99"] for r in g)) if g else ("blocked" if groups.get((b, m, s)) else "n/a"))
    p99.append("| " + label.split(",")[0] + " | " + " | ".join(vals) + " |")
print("\n".join(out))
print()
print("\n".join(p99))
io.open(SP + "/benchmark-table.md", "w", encoding="utf-8", newline="\n").write("\n".join(out) + "\n\n" + "\n".join(p99) + "\n")
