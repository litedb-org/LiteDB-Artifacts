using LiteDB;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

namespace SharedPinCallbackProof;
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0) return Child(args);
        var host = ReproHostClient.CreateDefault(); ReproConfigurationReporter.SendConfiguration(host);
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "p133-pin-proof-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); Console.WriteLine("DATABASE_ROOT " + root);
            var results = new List<int>();
#if PIN_WRITE_PROOF
            var scenarios = new[] { "write-commit", "write-rollback" };
            var controls = new[] { "normal-pin", "other-database", "idle-handoff", "no-anchor" };
#else
            var scenarios = new[] { "pin-begin-same", "pin-begin-peer", "reader-begin-same", "reader-begin-peer" };
            var controls = new[] { "leased-begin-same", "leased-begin-peer", "idle-handoff" };
#endif
            foreach (var password in new string?[] { null, "secret" })
            {
                foreach (var scenario in scenarios)
                {
                    var path = Path.Combine(root, Guid.NewGuid() + ".db"); Fixture.Seed(path, password);
                    var result = Processes.Run("--case", path, password, scenario); results.Add(result);
                    if (Processes.Run("--peer", path, password, "") != 10) throw new Exception("Peer could not commit after case exit.");
                    var committed = result == 10 && scenario == "write-commit";
                    var ordinary = result == 10 && scenario.StartsWith("pin-begin", StringComparison.Ordinal);
                    Fixture.Verify(path, password, committed ? new[] { 1, 2, 3, 20, 21, 30 } : new[] { 1, 2, 3, 30 }, ordinary);
                }
                foreach (var control in controls)
                {
                    var path = Path.Combine(root, Guid.NewGuid() + ".db"); Fixture.Seed(path, password);
                    if (Processes.Run("--control", path, password, control) != 10) throw new Exception("Control failed.");
                }
            }
            if (results.Distinct().Count() != 1) throw new Exception("Plain/encrypted scenario outcomes disagree.");
            var reproduced = results[0] == 20;
            Console.WriteLine(reproduced ? "BUG_REPRODUCED" : "VERIFIED_FIXED");
            host.SendResult(reproduced, "Native dependency cases, independent controls and cold models verified."); return reproduced ? 0 : 10;
        }
        catch (Exception error) { Console.Error.WriteLine(error); host.SendResult(false, "Unexpected failure", new { Error = error.ToString() }); return 2; }
    }
    private static int Child(string[] args)
    {
        try
        {
            Console.WriteLine("CHILD_READY"); if (Console.ReadLine() != "go") throw new Exception("Missing parent handshake.");
            var password = args[2].Length == 0 ? null : args[2];
            if (args[0] == "--peer")
            {
                using (var db = new LiteDatabase(new ConnectionString { Filename = args[1], Password = password, Connection = ConnectionType.Shared }))
                    db.GetCollection("rows").Insert(Fixture.Row(30));
                Console.WriteLine("PEER_COMMITTED"); return 10;
            }
            var reproduced = args[0] == "--case" ? Cases.Run(args[1], password, args[3]) : Controls.Run(args[1], password, args[3]);
            Console.WriteLine(reproduced ? "CALLBACK_NATIVE_DEPENDENCY_VERIFIED" : "CASE_FIXED");
            // Known-bad cases have established the native dependency in background
            // threads. Exiting this isolated process terminates them; the parent
            // must then prove real peer progress and recovered cold data.
            return reproduced ? 20 : 10;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }
}
