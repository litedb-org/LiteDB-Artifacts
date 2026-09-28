using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using LiteDB.Engine;
using LiteDB.Internals;
using LiteDB.Tests.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe3
{
    public class TornTailSweep_Probe
    {
        private readonly ITestOutputHelper _out;
        public TornTailSweep_Probe(ITestOutputHelper output) { _out = output; }
        internal const string Dump = "$SCRATCH/r3a/images/";

        // mode: g = garbage bytes, c = prefix of the 3rd-last page restamped as confirming tx 22,
        // d = prefix of a verbatim copy of the last physical WAL page (for encrypted: its ciphertext)
        [Theory]
        [InlineData(false, 'g', 100)]
        [InlineData(false, 'c', 512)]
        [InlineData(false, 'c', 4096)]
        [InlineData(false, 'c', 8191)]
        [InlineData(false, 'd', 4096)]
        [InlineData(false, 'd', 16)]
        [InlineData(true, 'g', 100)]
        [InlineData(true, 'g', 16)]
        [InlineData(true, 'd', 512)]
        [InlineData(true, 'd', 4096)]
        [InlineData(true, 'd', 8176)]
        public void Sweep(bool encrypted, char mode, int tail)
        {
            var fixture = encrypted ? "EncryptedWalCrash_5_0_21.zip" : "WalCrash_5_0_21.zip";
            var password = encrypted ? "wal-secret" : null;
            var expected = encrypted ? "65/15" : "101/21";
            var data = Entry(fixture, "crash.db");
            var log = Entry(fixture, "crash-log.db");
            byte[] torn;
            if (mode == 'g') torn = Enumerable.Repeat((byte)0xCD, tail).ToArray();
            else if (mode == 'c')
            {
                torn = log.Skip(log.Length - 3 * Constants.PAGE_SIZE).Take(tail).ToArray();
                BitConverter.GetBytes(22u).CopyTo(torn, BasePage.P_TRANSACTION_ID);
                torn[BasePage.P_IS_CONFIRMED] = 1;
            }
            else torn = log.Skip(log.Length - Constants.PAGE_SIZE).Take(tail).ToArray();
            log = log.Concat(torn).ToArray();

            var name = $"{(encrypted ? "enc" : "plain")}-{mode}{tail}";
            var dir = Dump + name + "/";
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            File.WriteAllText(dir + "expected.txt", expected + "\n" + (password ?? ""));

            using var device = new IndexMigrationCrashDevice(data, log);
            string first;
            try
            {
                using var db = new LiteDatabase(new LiteEngine(Settings(device.Data, device.Log, password)));
                first = Check(db);
            }
            catch (Exception ex) { first = "THROW " + ex.GetType().Name + ": " + ex.Message; }
            device.Armed = false;

            var bad = new List<string>();
            var seconds = new HashSet<string>();
            var i = 0;
            foreach (var image in device.Images)
            {
                File.WriteAllBytes(dir + $"{i:D3}.db", image.Data);
                File.WriteAllBytes(dir + $"{i:D3}-log.db", image.Log);
                File.WriteAllText(dir + $"{i:D3}.txt", image.Event);
                i++;
                // repeated interruption: crash the recovery of this image at every first boundary
                using var recovery = new IndexMigrationCrashDevice(image.Data, image.Log) { FirstEventOnly = true };
                string r;
                try
                {
                    using (var db = new LiteDatabase(new LiteEngine(Settings(recovery.Data, recovery.Log, password)))) r = Check(db);
                }
                catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message; }
                recovery.Armed = false;
                if (r != expected) bad.Add(image.Event + " first-recovery=" + r);
                foreach (var second in recovery.Images)
                {
                    if (!seconds.Add(Sig(second))) continue;
                    string s;
                    try
                    {
                        using var d = ChecksumTestFiles.Copy(second.Data);
                        using var l = ChecksumTestFiles.Copy(second.Log);
                        using (var db = new LiteDatabase(new LiteEngine(Settings(d, l, password)))) s = Check(db);
                        using (var db = new LiteDatabase(new LiteEngine(Settings(d, l, password)))) s += "|" + Check(db);
                    }
                    catch (Exception ex) { s = "THROW " + ex.GetType().Name + ": " + ex.Message; }
                    if (s != expected + "|" + expected) bad.Add(image.Event + " / " + second.Event + " => " + s);
                }
            }
            _out.WriteLine($"{name}: first={first} images={device.Images.Count} secondImages={seconds.Count} bad={bad.Count}");
            foreach (var b in bad.Take(10)) _out.WriteLine("  BAD " + b);
            Assert.Equal(expected, first);
            Assert.Empty(bad);
        }

        private static EngineSettings Settings(Stream data, Stream log, string password) =>
            new EngineSettings { DataStream = data, LogStream = log, Password = password };

        private static string Sig(IndexMigrationCrashDevice.Image image)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(image.Data)) + ":" + Convert.ToBase64String(sha.ComputeHash(image.Log));
        }

        internal static string Check(LiteDatabase db)
        {
            var docs = db.GetCollection("docs").FindAll().ToList();
            return $"{docs.Count}/{docs.Count(x => x["value"].AsInt32 == 7)}";
        }

        internal static byte[] Entry(string fixture, string name)
        {
            using var resource = typeof(TornTailSweep_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources." + fixture);
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
