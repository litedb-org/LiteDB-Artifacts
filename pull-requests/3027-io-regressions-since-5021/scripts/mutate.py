import sys, pathlib
path, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
p = pathlib.Path(path); t = p.read_text()
n = t.count(old)
if n != 1: sys.exit(f"pattern count {n} in {path}")
p.write_text(t.replace(old, new))
print("mutated", path)
