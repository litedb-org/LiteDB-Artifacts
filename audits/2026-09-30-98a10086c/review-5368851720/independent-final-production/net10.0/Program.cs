using LiteDB;
using System.Reflection;
using System.Diagnostics;
using System.Collections;
static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);

var loadedAssembly=typeof(LiteDatabase).Assembly;
Console.WriteLine("assembly="+loadedAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
Console.WriteLine("runtime="+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
Console.WriteLine("os="+System.Runtime.InteropServices.RuntimeInformation.OSDescription);
Console.WriteLine("arch="+System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
var admission=loadedAssembly.GetType("LiteDB.TransactionAdmission",true);
if(admission.GetField("Observe",BindingFlags.Static|BindingFlags.NonPublic)!=null)throw new Exception("TESTING hook unexpectedly present");
Console.WriteLine("testing-hook-absent=True");

foreach (var password in new string[]{null,"secret"})
foreach (var peerFacade in new[]{false,true}) {
 var path=Path.Combine(Path.GetTempPath(),"pin-review-"+Guid.NewGuid()+".db");
 using(var seed=new LiteDatabase(new ConnectionString {Filename=path,Password=password})) {seed.GetCollection("rows").Insert(new[]{new BsonDocument{["_id"]=1},new BsonDocument{["_id"]=2}});}
 using(var shared=new SharedEngine(new LiteDB.Engine.EngineSettings {Filename=path,Password=password,ReadTransform=(_,v)=>v}))
 using(var db=new LiteDatabase(shared))
 using(var peer=new LiteDatabase(new SharedEngine(new LiteDB.Engine.EngineSettings {Filename=path,Password=password})))
 using(var anchor=shared.Query("rows",new Query())) {
 anchor.Read();
 Console.WriteLine($"setup encrypted={password!=null} peer={peerFacade} localReaders={((IDictionary)Field(shared,"_localReaders")).Count} noPin={Field(shared,"_pin")==null}");
 IEnumerable<BsonDocument> Input() {
 var pin=Field(shared,"_pin"); Console.WriteLine($"callback operations={Field(pin,"_operations")} holds={Field(pin,"_holds")}");
 var timer=Stopwatch.StartNew();
 try {using var tx=(peerFacade?peer:db).BeginTransaction(TimeSpan.FromMilliseconds(500));Console.WriteLine("unexpected-success");}
 catch(Exception e){Console.WriteLine($"result={e.GetType().Name} elapsedMs={timer.ElapsedMilliseconds}");}
 yield return new BsonDocument{["_id"]=3};
 }
 shared.Insert("ordinary",Input(),BsonAutoId.Int32);
 }
 using(var cold=new LiteDatabase(new ConnectionString{Filename=path,Password=password})){Console.WriteLine($"cold ordinary={cold.GetCollection("ordinary").Count()} rows={cold.GetCollection("rows").Count()}");}
 File.Delete(path);
}
foreach (var password in new string[]{null,"secret"})
foreach (var peerFacade in new[]{false,true}) {
 var path=Path.Combine(Path.GetTempPath(),"reader-review-"+Guid.NewGuid()+".db");
 using(var seed=new LiteDatabase(new ConnectionString {Filename=path,Password=password})) {seed.GetCollection("rows").Insert(new[]{new BsonDocument{["_id"]=1},new BsonDocument{["_id"]=2}});}
 LiteDatabase target=null; bool armed=false; int callbackCount=0;
 using(var shared=new SharedEngine(new LiteDB.Engine.EngineSettings {Filename=path,Password=password,ReadTransform=(_,v)=> {
 if(armed){callbackCount++; var timer=Stopwatch.StartNew(); try {using var tx=target.BeginTransaction(TimeSpan.FromMilliseconds(500));Console.WriteLine("unexpected-reader-success");}catch(Exception e){Console.WriteLine($"reader result={e.GetType().Name} elapsedMs={timer.ElapsedMilliseconds}");}}
 return v;
 }}))
 using(var db=new LiteDatabase(shared))
 using(var peer=new LiteDatabase(new SharedEngine(new LiteDB.Engine.EngineSettings {Filename=path,Password=password})))
 using(var reader=shared.Query("rows",new Query{ForUpdate=true})) {
 target=peerFacade?peer:db; armed=true;
 Console.WriteLine($"reader setup encrypted={password!=null} peer={peerFacade}");
 var worker=new Thread(()=>{ Console.WriteLine($"reader-first={reader.Read()}"); Console.WriteLine($"reader-second={reader.Read()}"); });worker.Start(); if(!worker.Join(5000))throw new Exception("worker stuck");
 armed=false; Console.WriteLine($"reader-callback-count={callbackCount}");
 }
 using(var cold=new LiteDatabase(new ConnectionString{Filename=path,Password=password})){Console.WriteLine($"reader-cold rows={cold.GetCollection("rows").Count()}");}
 File.Delete(path);
}
foreach (var password in new string[]{null,"secret"}) {
 var path=Path.Combine(Path.GetTempPath(),"prefetch-review-"+Guid.NewGuid()+".db");
 using(var seed=new LiteDatabase(new ConnectionString {Filename=path,Password=password})) {seed.GetCollection("rows").Insert(new[]{new BsonDocument{["_id"]=1},new BsonDocument{["_id"]=2}});}
 using(var peer=new LiteDatabase(new SharedEngine(new LiteDB.Engine.EngineSettings{Filename=path,Password=password})))
 using(var shared=new SharedEngine(new LiteDB.Engine.EngineSettings {Filename=path,Password=password,ReadTransform=(_,v)=> {
 var timer=Stopwatch.StartNew(); try {using var tx=peer.BeginTransaction(TimeSpan.FromMilliseconds(500));Console.WriteLine("unexpected-prefetch-success");}catch(Exception e){Console.WriteLine($"prefetch encrypted={password!=null} result={e.GetType().Name} elapsedMs={timer.ElapsedMilliseconds}");} return v;
 }}))
 using(var reader=shared.Query("rows",new Query())) {
 Console.WriteLine($"prefetch read={reader.Read()} leased={Field(reader,"_ownedSnapshot")!=null}");
 }
 File.Delete(path);
}
