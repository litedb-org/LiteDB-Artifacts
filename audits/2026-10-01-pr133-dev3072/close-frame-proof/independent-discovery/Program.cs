using LiteDB;
using LiteDB.Engine;
using System.Diagnostics;
using System.Reflection;
var path = Path.GetFullPath(args[0]);
var password = args[1] == "encrypted" ? "secret" : null;
using (var seed = new LiteDatabase(new ConnectionString { Filename=path, Password=password }))
{
 seed.GetCollection("rows").Insert(new BsonDocument { ["_id"]=1, ["value"]=1 });
 seed.GetCollection("rows").EnsureIndex("value");
 seed.GetCollection("sentinel").Insert(new BsonDocument { ["_id"]=42, ["value"]=42 });
}
using var data = new CallbackFile(path);
using var log = new CallbackFile(Path.Combine(Path.GetDirectoryName(path)!,Path.GetFileNameWithoutExtension(path)+"-log"+Path.GetExtension(path)));
var outer = new SharedEngine(new EngineSettings { Filename=path,Password=password,DataStream=data,LogStream=log,ReadTransform=(_,v)=>v });
using var peer = new LiteDatabase(new ConnectionString { Filename=path,Password=password,Connection=ConnectionType.Shared });
var observer = typeof(LiteDatabase).Assembly.GetType("LiteDB.TransactionAdmission")!.GetField("Observe",BindingFlags.Static|BindingFlags.NonPublic)!;
var native=0;var called=0;Exception refusal=null;double seconds=0;
observer.SetValue(null,(Action<string>)(stage=> { Console.WriteLine("admission:"+stage);if(stage=="native-wait")Interlocked.Increment(ref native); }));
outer.Insert("rows",new[]{new BsonDocument{["_id"]=100,["value"]=100}},BsonAutoId.Int32);
data.Arm(()=> {
 Interlocked.Increment(ref called);
 var sw=Stopwatch.StartNew();
 try { using var tx=peer.BeginTransaction(TimeSpan.FromSeconds(1));tx.Rollback(); }
 catch(Exception error) {refusal=error;}
 seconds=sw.Elapsed.TotalSeconds;
 Console.WriteLine("callback-result:"+refusal?.GetType().FullName+":"+refusal?.Message+";seconds="+seconds);
});
outer.Dispose();
observer.SetValue(null,null);
using(var control=peer.BeginTransaction(TimeSpan.FromSeconds(5))) control.Rollback();
peer.Dispose();
using(var cold=new LiteDatabase(new ConnectionString{Filename=path,Password=password}))
{
 if(!cold.GetCollection("rows").FindAll().Select(x=>x["_id"].AsInt32).OrderBy(x=>x).SequenceEqual(new[]{1,100}) || cold.GetCollection("sentinel").FindById(42)["value"]!=42)throw new Exception("cold integrity failed");
 Console.WriteLine("cold committed rows/sentinel preserved; post-close handle succeeds");
}
Console.WriteLine("called="+called+";native-wait="+native);
return called==1 && native>0 && refusal is TimeoutException ? 20 : refusal is InvalidOperationException && native==0 ? 0 : 2;
sealed class CallbackFile:FileStream
{
 private Action callback;
 public CallbackFile(string path):base(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite|FileShare.Delete){}
 public void Arm(Action value)=>callback=value;
 private void Fire()=>Interlocked.Exchange(ref callback,null)?.Invoke();
 public override void Write(byte[] a,int o,int c){Fire();base.Write(a,o,c);}
 public override void Write(ReadOnlySpan<byte> b){Fire();base.Write(b);}
}
