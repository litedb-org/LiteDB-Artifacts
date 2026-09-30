using System.Diagnostics;
using System.Reflection;
using LiteDB;

namespace Issue_3067_SharedCallbackNativeWait;
internal static class Processes
{
    internal static int Run(string mode, string path, string? password, string scenario)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        foreach (var arg in new[] { mode, path, password ?? "", scenario }) start.ArgumentList.Add(arg);
        Console.WriteLine($"CHILD_START mode={mode} scenario={scenario} path={path} encrypted={password != null}");
        using var child = Process.Start(start) ?? throw new Exception("Child failed to start.");
        var stderr = child.StandardError.ReadToEndAsync();
        var ready = child.StandardOutput.ReadLineAsync();
        Task<string>? stdout = null; var printed = false;
        try
        {
            if (!ready.Wait(TimeSpan.FromSeconds(20)) || ready.Result != "CHILD_READY") throw new Exception("Child startup handshake failed.");
            child.StandardInput.WriteLine("go"); child.StandardInput.Flush();
            stdout = child.StandardOutput.ReadToEndAsync();
            if (!child.WaitForExit(45000)) throw new Exception("Post-start child deadline expired.");
            Task.WaitAll(stdout, stderr);
            Console.WriteLine($"CHILD_RESULT mode={mode} scenario={scenario} encrypted={password != null} exit={child.ExitCode}");
            Console.Write(stdout.Result); Console.Error.Write(stderr.Result); printed = true;
            if (child.ExitCode != 10 && child.ExitCode != 20) throw new Exception("Unexpected child failure.");
            if (!stdout.Result.Contains(mode == "--peer" ? "PEER_COMMITTED" : child.ExitCode == 20 ? "NATIVE_SELF_WAIT_VERIFIED" : "CASE_FIXED")) throw new Exception("Child result marker missing.");
            return child.ExitCode;
        }
        catch (Exception failure)
        {
            // Preserve the original handshake/deadline error even if diagnostics fail.
            Console.Error.WriteLine("CHILD_FAILURE " + failure);
            try
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                if (!child.WaitForExit(10000)) throw new Exception("Child did not stop for diagnostics.");
                if (!printed)
                {
                    if (!ready.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Startup output did not drain.");
                    Console.WriteLine("CHILD_STARTUP_OUTPUT " + ready.Result);
                    stdout ??= child.StandardOutput.ReadToEndAsync();
                    if (!Task.WaitAll(new Task[] { stdout, stderr }, TimeSpan.FromSeconds(5))) throw new Exception("Child output did not drain.");
                    Console.Write(stdout.Result); Console.Error.Write(stderr.Result);
                }
            }
            catch (Exception diagnostic) { Console.Error.WriteLine("CHILD_DIAGNOSTIC_FAILURE " + diagnostic); }
            throw;
        }
        finally
        {
            try { if (!child.HasExited) { child.Kill(entireProcessTree: true); child.WaitForExit(10000); } }
            catch (Exception cleanup) { Console.Error.WriteLine("CHILD_CLEANUP_FAILURE " + cleanup); }
        }
    }

    internal static bool WaitNativeBoundary(SharedEngine shared, Thread caller, ManualResetEventSlim finished)
    {
        // Read-only observation of existing production fields. No injected hook,
        // mutation, replacement binary or startup timeout forms the failure oracle.
        var waiters = typeof(SharedEngine).GetField("_mutexWaiters", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Production waiter field unavailable.");
        var turnstile = typeof(SharedEngine).GetField("_turnstile", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(shared)
            ?? throw new Exception("Production turnstile unavailable.");
        var hasWaiter = turnstile.GetType().GetMethod("HasWaiter") ?? throw new Exception("Production native queue probe unavailable.");
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (finished.IsSet) return false;
            // This monitor is a different thread from the callback and native holder.
            // Only this scenario's parent can hold this fresh file's turnstile.
            if ((int)waiters.GetValue(shared)! > 0 && (bool)hasWaiter.Invoke(turnstile, null)! &&
                (caller.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0)
            {
                Console.WriteLine("NATIVE_BOUNDARY parentWaiters>0 occupiedTurnstile callerWaiting");
                return true;
            }
            Thread.Sleep(1);
        }
        throw new Exception("Neither refusal nor actual native admission boundary was observed.");
    }

    internal static void Close(LiteDatabase db)
    {
        Exception? error = null;
        var close = new Thread(() => { try { db.Dispose(); } catch (Exception failure) { error = failure; } }) { IsBackground = true };
        close.Start();
        if (!close.Join(TimeSpan.FromSeconds(10)) || error != null) throw new Exception("Session cancellation/close did not drain.", error);
    }
}
