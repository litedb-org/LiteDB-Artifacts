import struct, sys
W=sys.argv[1]
d=open(W+"/c.db","rb").read(); l=open(W+"/c-log.db","rb").read()
P=8192
print("data pages",len(d)//P,"data LastPageID",struct.unpack_from("<I",d,64)[0],"log pages",len(l)//P, "log bytes", len(l))
for i in range(len(l)//P):
    pg=l[i*P:(i+1)*P]
    pid,=struct.unpack_from("<I",pg,0); typ=pg[4]; tid,=struct.unpack_from("<I",pg,14); conf=pg[18]
    extra = (" LastPageID=%d" % struct.unpack_from("<I",pg,64)[0]) if typ==1 else ""
    print(i,"pageID",pid,"type",typ,"tx",tid,"confirmed",conf,extra)
