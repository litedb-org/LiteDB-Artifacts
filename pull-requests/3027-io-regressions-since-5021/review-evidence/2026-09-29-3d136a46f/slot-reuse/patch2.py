p='LiteDB.Tests/Internals/SharedUnsyncableLog_Tests.cs'
s=open(p).read()
def rep(old, new):
    global s
    assert old in s, old
    s = s.replace(old, new)

rep('''    /// #2242 in shared mode: every operation opens a fresh engine whose recovery registers
    /// the WAL slots an earlier engine cleared without a durable sync. Those slots must
    /// never be reused, a live reader keeps its snapshot, and the connection keeps
    /// reporting the weaker guarantee. Every retiring checkpoint first proves its syncs, so a
    /// log known unable to sync makes a checkpoint retire nothing. A log that stops syncing
    /// during the checkpoint keeps the retired frames, unless it stops only at the sync of the
    /// clears themselves; either way no later engine reuses those slots. A second connection,
    /// which does not share the first one's diagnostic, relies on its own proof.''',
'''    /// #2242 in shared mode: every operation opens a fresh engine whose recovery registers
    /// the WAL slots an earlier engine cleared without a durable sync. The connection's later
    /// engines never reuse them, as they know the log cannot sync; a live reader keeps its
    /// snapshot, and the connection keeps reporting the weaker guarantee. Every retiring checkpoint
    /// first proves its syncs, so a log known unable to sync makes a checkpoint retire nothing. A
    /// log that stops syncing during the checkpoint keeps the retired frames, unless it stops only at
    /// the sync of the clears themselves. A second connection does not share the first one's
    /// diagnostic: opted out of durable commits it never syncs, and it reuses such cleared slots,
    /// whose witness root is durable (implementation note 15); a power loss keeps every commit synced.''')

rep('''        public void Fresh_shared_engines_never_reuse_slots_retired_on_a_log_that_cannot_sync_without_durable_commits(string stopsAt, bool secondConnection)''',
'''        public void Fresh_shared_engines_never_reuse_slots_on_a_log_their_connection_knows_cannot_sync_without_durable_commits(string stopsAt, bool secondConnection)''')

rep('''            var written = ReadAll(log);
            ChangedFrames(retired, written, frames).Should().Be(0,
                "slots retired without durable syncs must not be reused by later engines");
            // An opted-out commit never asks for a device sync. Only an engine that has not learned of
            // the rejection (the second connection's first) asks, once, to prove the log before it
            // would reuse a cleared slot; the answer makes its connection append instead.
            (log.RejectedSyncs - rejectedBefore).Should().Be(secondConnection && stopsAt == "clear" ? 1 : 0);''',
'''            var written = ReadAll(log);
            // Implementation note 15 of docs/decisions/durability-policy.md: reusing a slot needs no
            // sync of its own, as the root that witnesses it is durable (the checkpoint synced it before
            // its clears). The connection that found the log cannot sync reuses none; the second one
            // never learns it, as opted-out commits never sync, and reuses the cleared slots. (Before
            // that note an engine synced the log before its first reuse: the second connection asked
            // once, was refused, and appended.)
            if (stopsAt == "clear" && secondConnection)
                ChangedFrames(retired, written, frames).Should().BeGreaterThan(0, "the second connection reuses the witnessed slots");
            else ChangedFrames(retired, written, frames).Should().Be(0, "the connection knows the log cannot sync");
            (log.RejectedSyncs - rejectedBefore).Should().Be(0, "an opted-out commit never asks for a device sync, nor does a reused slot");''')

rep('''            recovered.GetCollection("docs").FindAll().Should().OnlyContain(doc => doc["value"].AsInt32 == 20);
        }
''','''            recovered.GetCollection("docs").FindAll().Should().OnlyContain(doc => doc["value"].AsInt32 == 20);

            if (stopsAt != "clear") return;
            // A power loss keeps each file as of its last successful sync: the checkpoint synced both
            // files before its clears, the later opted-out commits never synced. Also with every reused
            // slot's new frame written back while the confirmations, which append, were not: the
            // witnessed slots hide the old frames and the unconfirmed new ones alike.
            var powerLoss = log.Durable;
            var reusedWrittenBack = (byte[])powerLoss.Clone();
            foreach (var frame in Enumerable.Range(0, frames).Where(frame => (frame + 1) * WalChecksum.FrameSize <= powerLoss.Length &&
                !retired.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize).SequenceEqual(written.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize))))
                Buffer.BlockCopy(written, frame * WalChecksum.FrameSize, reusedWrittenBack, frame * WalChecksum.FrameSize, WalChecksum.FrameSize);
            foreach (var image in new[] { powerLoss, reusedWrittenBack })
            {
                using var dataImage = Expandable(data.Durable);
                using var logImage = Expandable(image);
                using var afterPowerLoss = new LiteDatabase(new LiteEngine(new EngineSettings
                {
                    DataStream = dataImage, LogStream = logImage, CompactStorage = CompactStorageMode.Legacy
                }));
                afterPowerLoss.GetCollection("cold").FindAll().Should().HaveCount(DocumentCount).And.OnlyContain(doc => doc["value"].AsInt32 == 0);
                afterPowerLoss.GetCollection("docs").FindAll().Should().HaveCount(DocumentCount).And.OnlyContain(doc => doc["value"].AsInt32 == 20);
            }
        }

        private static MemoryStream Expandable(byte[] bytes)
        {
            var stream = new MemoryStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;
            return stream;
        }
''')

rep('''        private sealed class SyncFile : FileStream
        {
            internal Exception Failure;
            internal int RejectedSyncs;
            internal int AllowedSyncs;

            internal SyncFile(string filename)
                : base(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite,
                    4096, FileOptions.DeleteOnClose) { }

            public override void Flush(bool flushToDisk)
            {
                if (flushToDisk && Failure != null && AllowedSyncs > 0) AllowedSyncs--;
                else if (flushToDisk && Failure != null)
                {
                    base.Flush(false);
                    RejectedSyncs++;
                    throw Failure;
                }
                base.Flush(flushToDisk);
            }
        }''','''        /// <summary>
        /// A file whose device syncs answer <see cref="Failure"/> once set, unless allowed.
        /// <see cref="Durable"/> is what a power loss keeps: the file as of its last successful sync.
        /// </summary>
        private sealed class SyncFile : FileStream
        {
            internal Exception Failure;
            internal int RejectedSyncs;
            internal int AllowedSyncs;
            internal byte[] Durable = new byte[0];

            internal SyncFile(string filename)
                : base(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite,
                    4096, FileOptions.DeleteOnClose) { }

            public override void Flush(bool flushToDisk)
            {
                if (flushToDisk && Failure != null && AllowedSyncs > 0) AllowedSyncs--;
                else if (flushToDisk && Failure != null)
                {
                    base.Flush(false);
                    RejectedSyncs++;
                    throw Failure;
                }
                base.Flush(flushToDisk);
                if (flushToDisk) Durable = LiteDB.Tests.Regressions.SyncPowerLossModel.ReadShared(this.Name);
            }
        }''')
open(p,'w').write(s)
