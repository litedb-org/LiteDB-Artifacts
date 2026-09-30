#if !NETFRAMEWORK
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;

namespace LiteDB.Internals
{
    /// <summary>Progress means a completed write; diagnostic messages never extend its deadline.</summary>
    internal sealed class SharedPinProgressProbe : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan OverallLimit = TimeSpan.FromSeconds(60);
        private readonly Stopwatch _elapsed = Stopwatch.StartNew();
        private readonly MvccProcess _process;
        private readonly int _start;
        private TimeSpan _lastCommit;
        private bool _admissionObserved, _disposed;

        internal SharedPinProgressProbe(string filename, string password = null, int start = 1000)
        {
            _start = start;
            _process = new MvccProcess("pin-insert-progress", filename, password, start.ToString());
        }

        internal Task Ready() => Expect("ready", Limit);

        internal void Start()
        {
            _lastCommit = _elapsed.Elapsed;
            _process.Send("go");
        }

        internal async Task ObserveAdmission()
        {
            await Expect("native-wait", Limit);
            _admissionObserved = true;
        }

        internal async Task Complete(TimeSpan? progressLimit = null)
        {
            var progress = progressLimit ?? Limit;
            if (!_admissionObserved) await ObserveAdmission();
            for (var id = _start; id < _start + 20; id++)
            {
                await Expect("committed:" + id, progress);
                _lastCommit = _elapsed.Elapsed;
            }
            await Expect("done", progress);
            await _process.Finish().WaitAsync(Remaining(OverallLimit - _elapsed.Elapsed));
            Remaining(OverallLimit - _elapsed.Elapsed);
        }

        private async Task Expect(string expected, TimeSpan progress)
        {
            string line;
            try
            {
                var remaining = Remaining(Min(progress - (_elapsed.Elapsed - _lastCommit), OverallLimit - _elapsed.Elapsed));
                line = await _process.ReadLine(remaining);
                Remaining(Min(progress - (_elapsed.Elapsed - _lastCommit), OverallLimit - _elapsed.Elapsed));
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException($"Pin child stopped before '{expected}'; elapsed={_elapsed.Elapsed}, " +
                    $"last completed-write offset={_lastCommit}. Diagnostic markers do not renew the progress deadline.", error);
            }
            line.Should().Be(expected);
        }

        private static TimeSpan Remaining(TimeSpan value)
        {
            if (value <= TimeSpan.Zero) throw new TimeoutException("Pin child exceeded its bounded progress or total deadline.");
            return value;
        }
        private static TimeSpan Min(TimeSpan first, TimeSpan second) => first < second ? first : second;

        internal static void VerifyCold(string filename, int seedCount, string password = null, bool inserted = true)
        {
            using var cold = new LiteDatabase(new ConnectionString { Filename = filename, Password = password });
            var collection = cold.GetCollection("docs");
            var rows = collection.Query().OrderBy("_id").ToArray();
            var expected = Enumerable.Range(1, seedCount).Concat(inserted ? Enumerable.Range(1000, 20) : Array.Empty<int>());
            rows.Select(row => row["_id"].AsInt32).Should().Equal(expected);
            foreach (var row in rows.Take(seedCount))
            {
                var id = row["_id"].AsInt32;
                row["value"].AsInt32.Should().Be(id == 1 || (inserted && id == 2) ? 1 : 0);
                row["payload"].AsString.Should().Be(new string('p', 200));
            }
            foreach (var row in rows.Skip(seedCount)) row.Count.Should().Be(1);
            collection.Query().Where("value = 1").GetPlan()["index"].AsDocument["mode"].AsString.Should().StartWith("INDEX SEEK");
            collection.Find("value = 1").Select(row => row["_id"].AsInt32).OrderBy(id => id)
                .Should().Equal(inserted ? new[] { 1, 2 } : new[] { 1 });
            cold.GetCollection("untouched").FindById(1)["value"].AsString.Should().Be("preserved");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _process.Dispose();
        }
    }
}
#endif
