using var mutex = new Mutex(false, "pr133-waitany-" + Guid.NewGuid());
using var cancel = new CancellationTokenSource();
try { var result = WaitHandle.WaitAny(new WaitHandle[] { mutex, cancel.Token.WaitHandle }); Console.WriteLine("WaitAny result=" + result); if (result == 0) mutex.ReleaseMutex(); }
catch (Exception error) { Console.WriteLine(error); Environment.ExitCode = 1; }
