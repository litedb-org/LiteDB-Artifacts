import sys
path, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
s = open(path).read()
n = s.count(old)
if n != 1:
    print(f"MUTATION FAILED: {n} matches in {path}"); sys.exit(1)
open(path, 'w').write(s.replace(old, new))
print("mutated", path)
