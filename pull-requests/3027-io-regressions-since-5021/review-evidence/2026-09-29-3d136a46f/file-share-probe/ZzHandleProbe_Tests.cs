#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using LiteDB.Engine;
using LiteDB.Tests.Issues;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Regressions
{
    [Collection(NativeFileSyncCollection.Name)]
    public class ZzHandleProbe_Tests
    {
        private readonly ITestOutputHelper _out;
        public ZzHandleProbe_Tests(ITestOutputHelper output) => _out = output;

        private void Handles(string label, string path)
        {
            var full = Path.GetFullPath(path);
            foreach (var fd in Directory.GetFiles("/proc/self/fd"))
            {
                string target;
                try { target = new FileInfo(fd).LinkTarget; } catch { continue; }
                if (target != full) continue;
                var flags = File.ReadAllLines("/proc/self/fdinfo/" + Path.GetFileName(fd)).First(l => l.StartsWith("flags")).Split('\t').Last();
                var mode = Convert.ToInt32(flags, 8) & 3;
                _out.WriteLine($"PROBE {label}: fd {Path.GetFileName(fd)} mode={(mode == 0 ? "RDONLY" : mode == 1 ? "WRONLY" : "RDWR")}");
            }
            _out.WriteLine($"PROBE {label}: end");
        }

        [Theory]
        [InlineData(22, false)]
        [InlineData(0, false)]
        public void Probe(int errno, bool shared)
        {
            using var file = new TempFile();
            NativeFileSync.SimulateErrno = _ => errno;
            try
            {
                using var db = new LiteDatabase(shared ? $"Filename={file.Filename};Connection=shared" : file.Filename);
                Handles("after open", file.Filename);
                db.GetCollection("rows").Count();
                Handles("after count", file.Filename);
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }
    }
}
#endif
