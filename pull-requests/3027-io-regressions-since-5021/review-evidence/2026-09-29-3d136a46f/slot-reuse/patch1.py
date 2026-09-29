p='LiteDB.Tests/Regressions/SlotReuseWithoutProof_Tests.cs'
s=open(p).read()
old=s[s.index('            var images = new Dictionary<string, ((byte[] Data, byte[] Log) Files, HashSet<int> Allowed)>();'):s.index('        /// <summary>\n        /// Commit values 1 to 9 under a live reader')]
new='''            var images = new Dictionary<string, ((byte[] Data, byte[] Log) Files, HashSet<int> Allowed, List<string> Taken)>();
            var points = new List<string>();
            void Add(string taken, (byte[], byte[]) files, int allowed)
            {
                var key = Key(files);
                if (!images.TryGetValue(key, out var image)) images.Add(key, image = (files, new HashSet<int> { allowed }, new List<string>()));
                image.Allowed.IntersectWith(new[] { allowed });
                image.Taken.Add(taken + " expects " + allowed);
            }
            var confirmed = false;
            EngineState.SimulateProcessCrash = point =>
            {
                if (!point.StartsWith("wal-", StringComparison.Ordinal)) return;
                points.Add(point);
                confirmed |= point == "wal-confirmation-after-write";
                var synced = point == "wal-after-durable-flush";
                foreach (var kind in Kinds)
                {
                    // Before its sync the commit is on the device only where its confirmation was written
                    // back whole; torn, the confirmation is invalid and so is the commit.
                    var expected = synced || (kind == ImageKind.WrittenBack && confirmed) ? value : previous;
                    Add($"{points.Count}:{point}:{kind}", (data.Image(kind), log.Image(kind)), expected);
                }
            };
            try { Update(db, value); }
            finally { EngineState.SimulateProcessCrash = null; }
            DurableLogFlush(db).Should().BeTrue("the commit is acknowledged durable");
            points.Should().Contain(new[] { "wal-page-after-write", "wal-confirmation-after-write", "wal-after-durable-flush" });
            Add("acknowledged", (data.Durable, log.Durable), value);
            foreach (var image in images.Values)
            {
                image.Allowed.Should().HaveCount(1, "an image is either before or after the commit: {0}", string.Join(", ", image.Taken));
                Recover(image.Files, password, image.Allowed.Single());
            }
        }

'''
s=s.replace(old,new)
open(p,'w').write(s)
