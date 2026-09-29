#!/bin/bash
# stress.sh <bin-dir-name> <count> <parallel>: run a built repro variant many times in parallel
export PATH=$DOTNET_ROOT:$PATH DOTNET_ROOT=$DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
O=$SCRATCH/promo-proof
one() {
  out=$($DOTNET_ROOT/dotnet $O/$1/Issue_3027_PromotionAfterFailedCheckpoint.dll 2>&1); code=$?
  after=$(echo "$out" | grep -o 'After the failed data sync: [^"]*' | head -1)
  result=$(echo "$out" | grep -o '"type":"result"[^}]*"text":"[A-Z]*' | grep -o '[A-Z]*$' | head -1)
  echo "exit=$code result=$result | $after"
}
export -f one; export O
seq 1 $2 | xargs -P $3 -I{} bash -c "one $1" | sort | uniq -c
