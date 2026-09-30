#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LiteDB.Client.Shared;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;
using LiteDB.Tests;

namespace LiteDB.Internals
{
    public class SharedFollowupProcess_Tests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "litedb-followup-" + Guid.NewGuid().ToString("N"));
        private readonly ITestOutputHelper _output;
        private bool _retain;
        private string Filename => Path.Combine(_directory, "test.db");

        public SharedFollowupProcess_Tests(ITestOutputHelper output)
        { _output = output; Directory.CreateDirectory(_directory); }

        [Fact]
        public async Task Killing_a_waiter_while_it_owns_the_turnstile_does_not_abandon_the_database()
        {
            await MvccProcess.Run("seed", Filename, null);
            var name = SharedMutexNameFactory.Create(Filename, SharedMutexNameStrategy.Default);
            using var main = SharedMutexFactory.Create(name);
            using var held = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var holder = new Thread(() => { main.WaitOne(); held.Set(); release.Wait(); main.ReleaseMutex(); });
            holder.Start();
            held.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
            try
            {
                using var waiter = new MvccProcess("followup-wait", Filename, null);
                await waiter.Expect("ready"); // owns Turn, about to wait on main
                await waiter.Kill();
            }
            finally { release.Set(); holder.Join(); }
            await MvccProcess.Run("write", Filename, null, "1");
            using var verify = new MvccProcess("read", Filename, null);
            await verify.Expect("value:1");
            await verify.Finish();
        }

        [Fact]
        public async Task Killing_a_scoped_owner_recovers_acknowledged_data_and_admits_a_queued_process()
        {
            using var owner = new MvccProcess("followup-owner", Filename, null);
            await owner.Expect("ready");
            using var waiter = new MvccProcess("followup-wait", Filename, null);
            await waiter.Expect("ready");
            await owner.Kill();
            await waiter.Expect("done");
            await waiter.Finish();
            using var db = new LiteDatabase(Filename);
            db.GetCollection("ack").FindById(1)["value"].AsInt32.Should().Be(42);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public Task A_pinned_write_loop_yields_to_another_process_before_its_hold_limit(string password) =>
            RunPinnedWriter(password, held: false);

        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public Task Yield_oracle_rejects_native_wait_without_a_completed_write(string password) =>
            RunPinnedWriter(password, held: true);

        private async Task RunPinnedWriter(string password, bool held)
        {
            MvccProcess owner = null, waiter = null;
            var childPids = new List<int>();
            Exception primary = null;
            var phase = "seed";
            try
            {
                await MvccProcess.Run("seed", Filename, password, started: childPids.Add);
                await MvccProcess.Run("followup-seed-pin", Filename, password, started: childPids.Add);
                // Load and initialize the writer before establishing the pin. Its
                // startup deadline never spends the existing ten-second yield budget.
                phase = "writer startup (before pin)";
                waiter = new MvccProcess("pin-insert-progress", Filename, password, "100");
                childPids.Add(waiter.Id);
                await waiter.Expect("ready");
                phase = "owner pin establishment";
                var ownerElapsed = Stopwatch.StartNew();
                owner = new MvccProcess(held ? "followup-held-pin" : "followup-pin", Filename, password);
                childPids.Add(owner.Id);
                await owner.Expect("ready");
                phase = "writer native admission";
                var elapsed = Stopwatch.StartNew();
                waiter.Send("go");
                await ExpectBeforeDeadline(waiter, "native-wait", elapsed, TimeSpan.FromSeconds(10));
                phase = "writer committed progress";
                if (held)
                {
                    // The exact same progress oracle must reject an observed native
                    // waiter while a real legacy-transaction pin hold remains live.
                    var error = await Assert.ThrowsAsync<TimeoutException>(() =>
                        CompleteWrites(waiter, Stopwatch.StartNew(), TimeSpan.FromSeconds(1)));
                    error.Message.Should().Contain("committed:100");
                    await waiter.Kill(); // no writes may slip through when the hold ends
                }
                else
                {
                    // Keep all twenty separately committed writes within the original
                    // ten-second post-go budget, well before the one-minute hold limit.
                    await CompleteWrites(waiter, elapsed, TimeSpan.FromSeconds(10));
                    await waiter.Finish();
                    ownerElapsed.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(1), "all twenty writes must finish before even the earliest possible pin hold limit");
                }
                owner.HasExited.Should().BeFalse("writer progress must occur while the pin owner remains alive: " + owner.DiagnosticSummary());
                phase = "owner death and native release";
                await owner.Kill();
                phase = "cold indexed recovery";
                await MvccProcess.Run(held ? "followup-verify-blocked" : "followup-verify-pin", Filename, password, started: childPids.Add);
            }
            catch (Exception error)
            {
                primary = error;
                _retain = true;
                // Stop children before publishing the post-host copy manifest. Preserve
                // both the original failure and the last observed semantic boundary.
                foreach (var process in new[] { waiter, owner })
                {
                    if (process == null) continue;
                    try { await process.Kill(); } catch (InvalidOperationException) { }
                    catch (Exception cleanup) { error.Data[ReferenceEquals(process, waiter) ? "writer-stop" : "owner-stop"] = cleanup; }
                }
                try
                {
                    var diagnostic = $"phase={phase}; encrypted={password != null}; held={held}\n" +
                        $"writer:\n{waiter?.DiagnosticSummary()}\nowner:\n{owner?.DiagnosticSummary()}\n{error}";
                    _output.WriteLine(diagnostic);
                    File.WriteAllText(Path.Combine(_directory, "failure-diagnostics.txt"), diagnostic);
                }
                catch (Exception diagnostic) { error.Data["failure-diagnostics"] = diagnostic; }
                try { RetainedTestFixture.PublishSharedFollowup(_directory, phase, error, _output, childPids.ToArray()); }
                catch (Exception publication) { error.Data["failure-manifest"] = publication; }
                throw;
            }
            finally
            {
                var cleanupErrors = new List<Exception>();
                foreach (var process in new[] { waiter, owner })
                {
                    try { process?.Dispose(); }
                    catch (Exception cleanup) { cleanupErrors.Add(cleanup); }
                }
                if (cleanupErrors.Count != 0)
                {
                    var cleanup = new AggregateException("Pinned-writer child disposal failed", cleanupErrors);
                    if (primary == null) throw cleanup;
                    primary.Data["child-final-disposal"] = cleanup;
                }
            }
        }

        private static async Task CompleteWrites(MvccProcess waiter, Stopwatch elapsed, TimeSpan limit)
        {
            for (var id = 100; id < 120; id++)
                await ExpectBeforeDeadline(waiter, "committed:" + id, elapsed, limit);
            await ExpectBeforeDeadline(waiter, "done", elapsed, limit);
        }

        private static async Task ExpectBeforeDeadline(MvccProcess process, string expected, Stopwatch elapsed, TimeSpan limit)
        {
            try
            {
                var remaining = limit - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero) throw new TimeoutException();
                var actual = await process.ReadLine(remaining);
                actual.Should().Be(expected, process.DiagnosticSummary());
                if (elapsed.Elapsed >= limit) throw new TimeoutException();
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException($"Pinned writer stopped before '{expected}', elapsed={elapsed.Elapsed}, limit={limit}. " +
                    "Startup and native-wait markers do not count as completed writes.\n" + process.DiagnosticSummary(), error);
            }
        }

        public void Dispose()
        {
            if (!_retain && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
#endif
