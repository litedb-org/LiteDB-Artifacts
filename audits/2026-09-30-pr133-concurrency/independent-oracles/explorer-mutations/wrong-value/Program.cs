using LiteDB.ConcurrencyTesting;
try {
 if (args[0] == "worker-starvation") {
  var schedule = new ExplorerSchedule(args[1]+".history", "mutant stalled A; completing B");
  var a=schedule.NewActor("A"); var b=schedule.NewActor("B");
  a.Invoke("stalled",()=>Thread.Sleep(Timeout.Infinite));
  for (int i=0;i<1000;i++) { b.Run("peer-completed-"+i,()=>Thread.Sleep(20)); schedule.CheckActors(); }
  throw new Exception("MUTANT_ESCAPED");
 }
 TransactionInterleavingExplorer.Run(args[1],false,false,0,3034134);
 Console.WriteLine("SCENARIO_PASSED"); return 0;
} catch(Exception e) { Console.Error.WriteLine(e); return 2; }
