using System;
using System.Diagnostics;
using System.IO;
using Xunit.Abstractions;

namespace LiteDB.Tests
{
    internal static class RetainedTestFixture
    {
        internal static void PublishNativeCrash(string directory, string phase, int childPid,
            bool childExited, Exception primary, ITestOutputHelper output)
        {
            try
            {
                PublishManifest(new BsonDocument
                {
                    ["fixtureKind"] = "native-crash-directory",
                    ["directory"] = Path.GetFullPath(directory),
                    ["phase"] = phase,
                    ["childPid"] = childPid,
                    ["childExitObserved"] = childExited
                }, primary, output);
            }
            catch (Exception diagnostic) { ReportFailure(output, diagnostic); }
        }

        internal static void PublishSharedFollowup(string directory, string phase, Exception primary, ITestOutputHelper output,
            int[] childPids = null)
        {
            try
            {
                PublishManifest(new BsonDocument
                {
                    ["fixtureKind"] = "shared-followup-directory",
                    ["childPids"] = new BsonArray(Array.ConvertAll(childPids ?? new int[0], pid => new BsonValue(pid))),
                    ["directory"] = Path.GetFullPath(directory),
                    ["phase"] = phase
                }, primary, output);
            }
            catch (Exception diagnostic) { ReportFailure(output, diagnostic); }
        }

        // Publish only a manifest in the test host. Copying must wait until the
        // host exits; a failing finalizer assertion can leave live native handles.
        internal static void Publish(string filename, Exception primary, ITestOutputHelper output)
        {
            try
            {
                output.WriteLine("Retained graph fixture: {0}\n{1}", filename, primary);
                PublishManifest(new BsonDocument { ["database"] = Path.GetFullPath(filename) }, primary, output);
            }
            catch (Exception diagnostic) { ReportFailure(output, diagnostic); }
        }

        private static void ReportFailure(ITestOutputHelper output, Exception diagnostic)
        {
            try { output.WriteLine("Fixture manifest publication failed: {0}", diagnostic); }
            catch { }
        }

        private static void PublishManifest(BsonDocument manifest, Exception primary, ITestOutputHelper output)
        {
            try
            {
                var directory = Environment.GetEnvironmentVariable("LITEDB_RETAINED_FIXTURES");
                if (string.IsNullOrEmpty(directory)) return;
                Directory.CreateDirectory(directory);
                manifest["processId"] = Process.GetCurrentProcess().Id;
                manifest["failure"] = primary.ToString();
                var target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(target + ".tmp", JsonSerializer.Serialize(manifest));
                File.Move(target + ".tmp", target);
            }
            catch (Exception diagnostic)
            {
                // Diagnostics must never replace the original test failure.
                ReportFailure(output, diagnostic);
            }
        }
    }
}
