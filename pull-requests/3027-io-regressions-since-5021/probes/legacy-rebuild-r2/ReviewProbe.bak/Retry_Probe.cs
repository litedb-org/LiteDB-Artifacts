using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class Retry_Probe
    {
        private readonly ITestOutputHelper _out;
        public Retry_Probe(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void Writable_stream_auto_rebuild()
        {
            var original = Fixture();
            var data = new MemoryStream();
            data.Write(original, 0, original.Length);
            data.Position = 0;
            Exception error = null;
            try { new LiteEngine(new EngineSettings { DataStream = data, AutoRebuild = true }).Dispose(); }
            catch (Exception ex) { error = ex; }
            _out.WriteLine(error?.GetType() + " " + (error as LiteException)?.ErrorCode + ": " + error?.Message);
            _out.WriteLine("inner: " + error?.InnerException?.GetType() + " " + string.Join(" | ", (error?.InnerException as AggregateException)?.InnerExceptions.Select(x => x.GetType().Name + ":" + x.Message) ?? Array.Empty<string>()));
            var after = data.ToArray();
            _out.WriteLine($"len {original.Length} -> {after.Length}; mark={after[HeaderPage.P_INVALID_DATAFILE_STATE]}");
            var diffs = Enumerable.Range(0, Math.Min(after.Length, original.Length)).Where(i => after[i] != original[i]).ToArray();
            _out.WriteLine("diff offsets: " + string.Join(",", diffs.Take(20)));
        }

        private static byte[] Fixture(string name = "DamagedDocument_5_0_21.zip")
        {
            using var resource = typeof(Retry_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources." + name);
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry("damaged.db").Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
