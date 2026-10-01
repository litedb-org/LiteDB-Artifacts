using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Engine
{
    /// <summary>
    /// A caller-owned stream can attempt a peer transaction handle while closing an engine: its close
    /// checkpoint writes the data file. Several closes run while the connection still holds
    /// the native mutex but outside any public call: disposing a result streamed under the
    /// mutex, a pin ending on its holder thread, the checkpoint after the last leased reader,
    /// and disposing the connection. A stream callback there must not wait for the mutex
    /// through another connection to the same database either.
    /// </summary>
    [Collection(nameof(SharedPeerCallbackCollection))]
    public class TransactionHandleCloseFrame_Tests : SharedPeerCallbackFixture
    {
        private readonly ITestOutputHelper _output;
        public TransactionHandleCloseFrame_Tests(ITestOutputHelper output) { _output = output; }

        // Past the 50-page close threshold, so the closing engine checkpoints.
        private const int BigRows = 60;

        public enum Close { RetainingReader, PinHolder, LastLeasedReader, ConnectionWithEngine, ConnectionCheckpoint }

        public static IEnumerable<object[]> Cases() =>
            from close in new[] { Close.RetainingReader, Close.PinHolder, Close.LastLeasedReader, Close.ConnectionWithEngine, Close.ConnectionCheckpoint }
            from encrypted in new[] { false, true }
            from otherDatabase in new[] { false, true }
            select new object[] { close, encrypted, otherDatabase };

        private static BsonDocument Big(int id)
        {
            var row = Row(id);
            row["payload"] = new string('p', 4000);
            return row;
        }

        private static IEnumerable<BsonDocument> BigBatch(int first) => Enumerable.Range(first, BigRows).Select(Big);

        [Theory]
        [MemberData(nameof(Cases))]
        public void Peer_handle_from_stream_callback_during_close_refuses_only_same_database(Close close, bool encrypted, bool otherDatabase)
        {
            try { this.VerifyClose(close, encrypted, otherDatabase); }
            catch
            {
                this.RetainFailure();
                _output.WriteLine("Failed close-frame fixture retained: " + this.Filename);
                throw;
            }
        }

        private void VerifyClose(Close close, bool encrypted, bool otherDatabase)
        {
            this.Seed(this.Filename, encrypted);
            var data = this.Track(new CallbackFile(this.Filename));
            var log = this.Track(new CallbackFile(FileHelper.GetLogFile(this.Filename)));
            var outer = this.Track(new SharedEngine(new EngineSettings
            {
                Filename = this.Filename,
                Password = Password(encrypted),
                DataStream = data,
                LogStream = log,
                // A user callback: even a one-row read then streams under a lease instead of buffering.
                ReadTransform = (_, value) => value
            }));
            if (otherDatabase) this.Seed(this.OtherFilename, encrypted);
            var peer = this.OpenPeer(otherDatabase ? this.OtherFilename : this.Filename, encrypted);
            var checkpointWriter = this.OpenPeer(this.Filename, encrypted, out var checkpointEngine);
            var called = 0;
            var admissions = 0;
            Exception refusal = null;
            Action callback = () =>
            {
                Interlocked.Increment(ref called);
                var observer = TransactionAdmission.Observe;
                TransactionAdmission.Observe = stage => Interlocked.Increment(ref admissions);
                try
                {
                    using var transaction = peer.BeginTransaction(TimeSpan.FromSeconds(1));
                    transaction.GetCollection("rows").Insert(Row(9));
                    transaction.Commit();
                }
                catch (Exception ex) { refusal = ex; }
                finally { TransactionAdmission.Observe = observer; }
            };
            var expected = new List<int> { 1 };

            var error = this.RunBounded(() =>
            {
                switch (close)
                {
                    case Close.RetainingReader:
                    {
                        // The write query retains native ownership and the same engine. Its
                        // transaction is independent, so hold a different collection
                        // while writing rows for the eventual close checkpoint.
                        var reader = outer.Query("sentinel", new Query { ForUpdate = true });
                        outer.MutexOwner.IsHeld.Should().BeTrue("the result must retain native ownership until close");
                        outer.Insert("rows", BigBatch(100).ToArray(), BsonAutoId.Int32);
                        data.Arm(callback);
                        reader.Dispose();
                        break;
                    }
                    case Close.PinHolder:
                    {
                        // The pin's holder thread closes its engine once the last leased reader ends.
                        var anchor = outer.Query("rows", new Query());
                        anchor.Read().Should().BeTrue();
                        outer.Insert("rows", BigBatch(100).ToArray(), BsonAutoId.Int32);
                        data.Arm(callback);
                        anchor.Dispose();
                        break;
                    }
                    case Close.LastLeasedReader:
                    {
                        // The last leased reader's disposal checkpoints what another connection left.
                        var reader = outer.Query("rows", new Query());
                        reader.Read().Should().BeTrue();
                        // Keep a changed page in the WAL below the writer close threshold.
                        checkpointWriter.GetCollection("rows").Update(Big(1)).Should().BeTrue();
                        checkpointEngine.MutexOwner.WaitForRelease();
                        File.Exists(FileHelper.GetLogFile(this.Filename)).Should().BeTrue("the lease prevents a full checkpoint");
                        data.Arm(callback);
                        reader.Dispose();
                        break;
                    }
                    case Close.ConnectionWithEngine:
                    {
                        // Disposing the connection closes the engine a still-open result keeps.
                        var reader = outer.Query("sentinel", new Query { ForUpdate = true });
                        outer.MutexOwner.IsHeld.Should().BeTrue("the result must retain native ownership until close");
                        outer.Insert("rows", BigBatch(100).ToArray(), BsonAutoId.Int32);
                        data.Arm(callback);
                        outer.Dispose();
                        reader.Dispose();
                        break;
                    }
                    case Close.ConnectionCheckpoint:
                    {
                        // Disposing the connection checkpoints a WAL below the close threshold.
                        outer.Insert("rows", new[] { Row(100) }, BsonAutoId.Int32);
                        data.Arm(callback);
                        outer.Dispose();
                        break;
                    }
                }
            });

            error.Should().BeNull();
            called.Should().Be(1, "the close must write through the caller's data stream");
            if (otherDatabase)
            {
                refusal.Should().BeNull("another namespace has no dependency on the closing owner");
                admissions.Should().BeGreaterThan(0, "the positive control must actually acquire a handle");
            }
            else
            {
                refusal.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be(
                    "Cannot open a transaction handle from inside an operation retaining its shared writer ownership.");
                admissions.Should().Be(0, "reject before publishing local or native admission");
            }
            if (close == Close.ConnectionCheckpoint) expected.Add(100);
            else if (close != Close.LastLeasedReader) expected.AddRange(Enumerable.Range(100, BigRows));

            using (var after = peer.BeginTransaction(TimeSpan.FromSeconds(5)))
            {
                after.GetCollection("rows").Insert(Row(11));
                after.Commit();
            }
            this.CloseAll();
            for (var reopen = 0; reopen < 2; reopen++)
            {
                VerifyCold(this.Filename, encrypted, expected.Concat(otherDatabase ? new int[0] : new[] { 11 }).OrderBy(x => x).ToArray());
                if (otherDatabase) VerifyCold(this.OtherFilename, encrypted, 1, 9, 11);
                if (close == Close.LastLeasedReader)
                {
                    using var cold = new LiteDatabase(new ConnectionString { Filename = this.Filename, Password = Password(encrypted) });
                    cold.GetCollection("rows").FindById(1)["payload"].AsString.Should().Be(new string('p', 4000));
                }
            }
        }

        /// <summary>The database's own file, shared with the peer connection, calling back on its first write: a checkpoint's, since read-only snapshots only read and flush.</summary>
        private sealed class CallbackFile : FileStream
        {
            private Action _onWrite;

            internal CallbackFile(string path)
                : base(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)
            {
            }

            internal void Arm(Action action) => Volatile.Write(ref _onWrite, action);

            private void Fire() => Interlocked.Exchange(ref _onWrite, null)?.Invoke();

            public override void Write(byte[] array, int offset, int count)
            {
                this.Fire();
                base.Write(array, offset, count);
            }

#if NETCOREAPP
            public override void Write(ReadOnlySpan<byte> buffer)
            {
                this.Fire();
                base.Write(buffer);
            }
#endif
        }
    }
}
