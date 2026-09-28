using LiteDB;
using LiteDB.Engine;
static MemoryStream W(byte[] b){var s=new MemoryStream();s.Write(b,0,b.Length);s.Position=0;return s;}
var d=new MemoryStream();
using(var db=new LiteDatabase(new LiteEngine(new EngineSettings{DataStream=d}))){
  db.GetCollection("rows").Insert(Enumerable.Range(1,20).Select(i=>new BsonDocument{["_id"]=i,["p"]=new string('x',100)}));
  db.Checkpoint();
}
var orig=d.ToArray();
Console.WriteLine("pages "+orig.Length/8192);
// page types at offset 4 of each page
for(int p=0;p<orig.Length/8192;p++) Console.Write($"{p}:{orig[p*8192+4]} ");
Console.WriteLine();
// corrupt data page(s): type 4 = DataPage in v5 -> set to 3 (IndexPage)
var bad=(byte[])orig.Clone();
for(int p=1;p<bad.Length/8192;p++) if(bad[p*8192+4]==4) { bad[p*8192+4]=3; Console.WriteLine("corrupted page "+p); break; }
foreach(var ro in new[]{true,false}){
 var data=W(bad); var log=new MemoryStream();
 using(var e=new LiteEngine(new EngineSettings{DataStream=data,LogStream=log,ReadOnly=ro}))
 using(var db=new LiteDatabase(e,disposeOnClose:false)){
  try{ db.GetCollection("rows").FindAll().ToList(); Console.WriteLine("scan ok"); }catch(Exception ex){Console.WriteLine($"scan ex {ex.GetType().Name} {(ex as LiteException)?.ErrorCode}: {ex.Message}");}
 }
 var b=data.ToArray();
 Console.WriteLine($"ro={ro} mark byte={b[123]} changed idx: "+string.Join(",",Enumerable.Range(0,bad.Length).Where(i=>b[i]!=bad[i]).Take(5))+$" log={log.Length}");
}
