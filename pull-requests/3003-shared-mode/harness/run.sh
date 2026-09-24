#!/usr/bin/env bash
# Like-for-like matrix: 3 rounds; within each round and scenario, round-robin over build x mode.
SP="${WORK_DIR:?set WORK_DIR to a scratch directory containing fbench-bin/<build>/}"
# export DOTNET_ROOT=<dotnet install dir> if needed
out="$SP/fbench-raw.txt"
: > "$out"
stamp=$(mktemp)
combos="pre:shared dev:shared imp:shared imp:coord pre:direct dev:direct imp:direct"
scenarios="upd:2000 ins:2000 qry:4000 mixed:2000 scan:200 held:500 iter:2000"
for round in 1 2 3; do
  for sc in $scenarios; do
    s=${sc%%:*}; n=${sc#*:}
    for c in $combos; do
      v=${c%%:*}; m=${c#*:}
      [ "$m" = direct ] && [ "$s" = held ] && continue
      line=$(timeout 300 dotnet "$SP/fbench-bin/$v/fbench.dll" "$SP/fb-db" "$m" "$s" "$n" 2>&1 | tail -1)
      echo "r$round $v $line" | tee -a "$out"
      rm -rf "$SP/fb-db"
      find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'litedb*' -newer "$stamp" -exec sh -c '[ -e "$1/.git" ] || rm -rf "$1"' _ {} \; 2>/dev/null
    done
  done
done
echo done
