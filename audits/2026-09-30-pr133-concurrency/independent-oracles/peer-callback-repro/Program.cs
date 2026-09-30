using System.Reflection;
using LiteDB;
using LiteDB.Engine;
var file = Path.GetFullPath(args[0]);
var password = args[1] == "encrypted" ? "secret" : null;
var mode = args[2];
if (mode == "verify") { using var cold = new LiteDatabase(new ConnectionString { Filename = file, Password = password }); var ids = cold.GetCollection("rows").FindAll().Select(x => x["_id"].AsInt32).OrderBy(x => x).ToArray(); if (!ids.SequenceEqual(new[] { 1 }) || cold.GetCollection("sentinel").FindById(42) == null) throw new Exception("cold corruption"); Console.WriteLine("cold pre-transaction state and sentinel verified"); return; }
BsonDocument Row(int id) => new() { ["_id"] = id, ["value"] = id };
if (!File.Exists(file)) using (var seed = new LiteDatabase(new ConnectionString { Filename = file, Password = password }))
{ seed.GetCollection("rows").Insert(Row(1)); seed.GetCollection("rows").EnsureIndex("value"); seed.GetCollection("sentinel").Insert(Row(42)); }
var settings = new EngineSettings { Filename = file, Password = password, ReadTransform = (_, value) => value };
var outer = new SharedEngine(settings);
var peer = mode == "same" ? outer : new SharedEngine(new EngineSettings { Filename = mode == "other" ? file + ".other" : file, Password = password });
var peerDb = new LiteDatabase(peer);
object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
using var reached = new ManualResetEventSlim();
Exception failure = null;
var worker = new Thread(() => {
 try {
  using var anchor = outer.Query("rows", new Query());
  if (!anchor.Read()) throw new Exception("missing anchor");
  if (Field(outer, "_pin") != null) throw new Exception("anchor not leased");
  IEnumerable<BsonDocument> Input() {
   yield return Row(2);
   var pin = Field(outer, "_pin");
   if (pin == null || (int)Field(pin, "_operations") < 1 || (int)Field(pin, "_holds") != 0) throw new Exception("wrong pin state");
   Console.WriteLine("callback-owned-pin operations>0 holds=0");
   reached.Set();
   peerDb.GetCollection("rows").Insert(Row(9));
   yield return Row(3);
  }
  outer.Insert("rows", Input(), BsonAutoId.Int32);
 } catch (Exception error) { failure = error; }
}) { IsBackground = true };
worker.Start();
if (!reached.Wait(TimeSpan.FromSeconds(5))) throw new Exception("callback missing: " + failure);
if (!SpinWait.SpinUntil(() => (int)Field(peer, "_mutexWaiters") > 0 || !worker.IsAlive, 5000)) throw new Exception("wait not reached");
if (worker.Join(1000)) { if (failure != null) throw failure; peerDb.Dispose(); if (mode != "same") outer.Dispose(); Console.WriteLine("CONTROL_COMPLETED"); Environment.Exit(0); }
if ((int)Field(peer, "_mutexWaiters") < 1) throw new Exception("wait was transient");
if (mode == "cancel") { peerDb.Dispose(); if (!worker.Join(5000)) throw new Exception("cancel did not release worker"); if (failure is not OperationCanceledException) throw new Exception("unexpected cancellation " + failure); outer.Dispose(); Console.WriteLine("CANCELLED_PEER_RELEASED_OUTER " + failure.GetType().Name); return; }
Console.WriteLine("PROVEN_BLOCK: callback cannot return until peer obtains native mutex retained by active outer pin; worker remains alive, peer mutexWaiters>0");
Environment.Exit(20);
