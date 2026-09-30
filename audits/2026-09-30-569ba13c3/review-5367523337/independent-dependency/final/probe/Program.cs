using LiteDB;
using LiteDB.Engine;
using System.Reflection;
using System.Diagnostics;
using LiteDB.Client.Shared;

static void Require(bool condition, string message) { if(!condition) throw new Exception(message); }
var mode = args[0];
var fixedExpected=Environment.GetEnvironmentVariable("PR133_EXPECT_CALLBACK_REFUSAL")=="1";
var path = Path.Combine(Path.GetTempPath(), "pr133-independent-" + Guid.NewGuid().ToString("N") + ".db");
var password = args.Length > 1 ? "secret" : null;
Console.WriteLine($"mode={mode} file={path} runtime={Environment.Version} encrypted={password!=null}");
using (var engine = new SharedEngine(new EngineSettings { Filename=path,Password=password }))
using (var db = new LiteDatabase(engine)) {
 db.GetCollection("rows").Insert(new BsonDocument{["_id"]=1,["value"]=10});
 db.GetCollection("rows").EnsureIndex("value");
 for(int i=0;i<5;i++) Require(db.GetCollection("rows").Count()==1,"warm read");
 using var tx = db.BeginTransaction();
 tx.GetCollection("rows").Insert(new BsonDocument{["_id"]=2,["value"]=20});
 Exception observed=null;
 int localWaits=0;
 var turn=(SharedMutexTurnstile)typeof(SharedEngine).GetField("_turnstile",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(engine);
 turn.BeforeContendedWait=_=>throw new Exception("unexpected native wait");
 IEnumerable<BsonDocument> Input() {
  if(mode=="nested-begin" || mode=="nested-begin-timeout") {
   using var cancel = new CancellationTokenSource();
   TransactionAdmission.Observe=stage=>{if(stage=="local-wait") { localWaits++; if(mode=="nested-begin") cancel.Cancel(); }};
   try { using var nested=db.BeginTransaction(mode=="nested-begin" ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(200),cancel.Token); }
   catch(Exception ex) { observed=ex; }
   finally { TransactionAdmission.Observe=null; }
  } else if(mode=="coordinated") {
   var before=engine.CoordinatedReadHits;
   var result=db.GetCollection("rows").Query().Where(Query.GTE("value",0)).ToArray();
   Require(result.Length==1 && result[0]["_id"]==1,"ordinary query implicitly enlisted");
   Require(engine.CoordinatedReadHits>before,"did not exercise coordinated query");
  } else if(mode=="nested-exception") {
   var otherPath=path+".other";
   using(var other=new LiteDatabase(new ConnectionString{Filename=otherPath,Password=password})) {
    other.GetCollection("rows").Insert(new BsonDocument{["_id"]=1});
    using(var nested=other.BeginTransaction()) {
     IEnumerable<BsonDocument> Bad(){ yield return new BsonDocument{["_id"]=2}; throw new FormatException("nested callback"); }
     try { nested.GetCollection("rows").Insert(Bad()); } catch(FormatException) { }
     Require(nested.State==LiteTransactionState.Failed,"nested exception must fail only nested handle");
    }
    Require(other.UserVersion==0,"stale nested dependency");
    try { var value=db.UserVersion; throw new Exception("lost outer dependency"); } catch(InvalidOperationException) { }
    Require(tx.State==LiteTransactionState.Active,"nested failure aborted enclosing handle");
   }
   using(var coldOther=new LiteDatabase(new ConnectionString{Filename=otherPath,Password=password}))
    Require(coldOther.GetCollection("rows").Count()==1 && coldOther.GetCollection("rows").FindById(2)==null,"nested rollback lost");
  } else if(mode=="exception") {
   throw new FormatException("callback sentinel");
  }
  yield return new BsonDocument{["_id"]=3,["value"]=30};
 }
 try { tx.GetCollection("rows").Insert(Input()); }
 catch(FormatException ex) when(ex.Message=="callback sentinel") { observed=ex; }
 Console.WriteLine($"observed={observed?.GetType().Name??"none"} localWaits={localWaits} txState={tx.State}");
 if(mode=="nested-begin" && !fixedExpected) { Require(localWaits==1,"nested begin did not reach local wait"); Require(observed is OperationCanceledException,"nested begin unexpectedly refused before wait"); }
 if(mode=="nested-begin-timeout" && !fixedExpected) { Require(localWaits==1,"nested begin did not reach local wait"); Require(observed is TimeoutException,"nested begin unexpectedly refused before wait"); }
 if(fixedExpected && mode.StartsWith("nested-begin")) { Require(localWaits==0,"fixed begin reached local wait"); Require(observed is InvalidOperationException,"fixed begin was not refused"); }
 if(mode=="exception") Require(tx.State==LiteTransactionState.Failed,"uncaught callback error must fail transaction");
 else { Require(tx.State==LiteTransactionState.Active,"caught callback changed state"); tx.Rollback(); }
 Require(typeof(TransactionContext).GetField("_dependency",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null,"ambient dependency retained");
 Require(typeof(TransactionContext).GetField("_executing",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)==null,"ambient binding retained");
 turn.BeforeContendedWait=null;
 Require(db.UserVersion==0,"stale dependency after unwind");
 db.GetCollection("rows").Insert(new BsonDocument{["_id"]=4,["value"]=40});
}
for(int i=0;i<2;i++) {
 using var cold=new LiteDatabase(new ConnectionString {Filename=path,Password=password});
 var query=cold.GetCollection("rows").Query().Where(Query.GTE("value",0));
 Require(query.GetPlan()["index"]["name"]=="value","index unused");
 Require(string.Join(",",query.ToArray().Select(d=>d["_id"].AsInt32).OrderBy(x=>x))=="1,4","cold data mismatch");
}
Console.WriteLine("PASS");
