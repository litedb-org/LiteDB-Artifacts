import struct, random
def entry(slot,name,expr,unique,hp,hi,tp,ti,res=0,free=0xFFFFFFFF):
    return bytes([slot,0])+name.encode()+b'\0'+expr.encode()+b'\0'+bytes([1 if unique else 0])+struct.pack('<IB',hp,hi)+struct.pack('<IB',tp,ti)+bytes([res])+struct.pack('<I',free)
def write(area,entries):
    b=bytes([len(entries)])+b''.join(entries); area[:len(b)]=b
class R:
    def __init__(s,a): s.a=a; s.p=0
    def b(s): v=s.a[s.p]; s.p+=1; return v
    def skip(s,n): s.p+=n
    def cs(s):
        e=s.a.index(0,s.p); v=bytes(s.a[s.p:e]); s.p=e+1; return v.decode('utf-8')
def head_parse(area):
    r=R(area); n=r.b(); live=[]
    for i in range(n):
        r.b(); r.b(); live.append(r.cs()); r.cs(); r.skip(16)
    vc=r.b(); vec={}
    for i in range(vc):
        nm=r.cs(); r.skip(10); vec[nm]=1
    return live, vc, vec
random.seed(1)
outcomes={'none':0,'garbage':0,'throw':0,'livename':0}
examples={}
names=[("Name","$.Name"),("Age","$.Age"),("Email","$.Email"),("CreatedAt","$.CreatedAt"),("CustomerId","$.CustomerId"),("Tags","$.Tags[*]"),("Status","$.Status"),("x","LOWER($.x)")]
for trial in range(20000):
    k=random.randint(2,5)
    chosen=random.sample(names,k)
    ents=[("_id","$._id",True)]+[(n,e,random.random()<0.2) for n,e in chosen]
    pids={}
    def mk(i,n,e,u):
        hp=random.randint(2,5000); tp=hp if random.random()<0.7 else random.randint(2,5000)
        return entry(i,n,e,u,hp,random.randint(0,40),tp,random.randint(0,40),0,0xFFFFFFFF if random.random()<FFP else random.randint(2,5000))
    built=[mk(i,*x) for i,x in enumerate(ents)]
    area=bytearray(8192-96); write(area,built)
    d=random.randint(1,len(built)-1)
    rest=built[:d]+built[d+1:]
    write(area,rest)
    try:
        live,vc,vec=head_parse(area)
        if vc==0: o='none'
        elif any(n in live for n in vec): o='livename'
        else: o='garbage'
    except UnicodeDecodeError:
        o='throw'
    except ValueError:
        o='throw'
    outcomes[o]+=1
    examples.setdefault(o,( [x[0] for x in ents], ents[d][0]))
print(outcomes); print(examples)
print("---deterministic")
def run(ents, drop):
    built=[entry(i,n,e,u,hp,0,hp,1,0,0xFFFFFFFF) for i,(n,e,u,hp) in enumerate(ents)]
    area=bytearray(8192-96); write(area,built)
    rest=[b for i,b in enumerate(built) if ents[i][0]!=drop]
    write(area,rest)
    try:
        live,vc,vec=head_parse(area); print([x[0] for x in ents],"drop",drop,"-> vc",vc,"vec",list(vec))
    except Exception as ex: print([x[0] for x in ents],"drop",drop,"-> THROW",type(ex).__name__, ex)
run([("_id","$._id",True,2),("Name","$.Name",False,4),("Age","$.Age",False,5)],"Name")
run([("_id","$._id",True,2),("Name","$.Name",False,4),("Age","$.Age",False,5)],"Age")
run([("_id","$._id",True,2),("Email","$.Email",True,4),("Age","$.Age",False,5)],"Email")
run([("_id","$._id",True,2),("CustomerId","$.CustomerId",False,4),("Age","$.Age",False,5)],"CustomerId")
run([("_id","$._id",True,2),("Age","$.Age",False,4),("CustomerId","$.CustomerId",False,5)],"Age")
