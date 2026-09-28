import struct, sys
d=open(sys.argv[1],"rb").read(); l=open(sys.argv[2],"rb").read()
P=8192
off = P if d[0]==1 else 0
print("data LastPageID", struct.unpack_from("<I",d,64)[0] if off==0 else "(encrypted)", "created", d[68:76].hex() if off==0 else "-", "data pages", len(d)//P, "log pages", len(l)//P)
if off: sys.exit()
hdr=0
for i in range(len(l)//P):
    pg=l[i*P:(i+1)*P]
    pid,=struct.unpack_from("<I",pg,0); typ=pg[4]; conf=pg[18]
    if typ==1 and conf: hdr+=1; print("  committed header at", i, "LastPageID", struct.unpack_from("<I",pg,64)[0], "created", pg[68:76].hex())
print("  committed headers:", hdr)
