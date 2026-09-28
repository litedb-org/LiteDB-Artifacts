using System;
using System.IO;
using System.Linq;
using Xunit;

namespace LiteDB.Tests.Review
{
    public class ReviewImages_Tests
    {
        [Theory]
        [InlineData("pragma", "")]
        [InlineData("rebuild", "")]
        [InlineData("rebuild-collation", "")]
        [InlineData("encrypted-rebuild", ";Password=pw")]
        [InlineData("checkpoint0", "")]
        [InlineData("upgrade", "")]
        public void Real_5_0_21_crash_image_opens(string name, string extra)
        {
            foreach (var readOnly in new[] { true, false })
            {
                using var file = new TempFile();
                var logName = FileHelper.GetLogFile(file.Filename);
                File.Copy("$SCRATCH/img/" + name + "/d.db", file.Filename, true);
                File.Copy("$SCRATCH/img/" + name + "/d-log.db", logName, true);
                try
                {
                    using var db = new LiteDatabase($"Filename={file.Filename}" + extra + (readOnly ? ";readonly=true;legacy index scan=true" : ""));
                    Console.WriteLine("image " + name + (readOnly ? " ro " : " rw ") + string.Join(" ", db.GetCollectionNames().OrderBy(x => x).Select(c => c + "=" + db.GetCollection(c).Count())) + " userVersion=" + db.UserVersion);
                }
                finally { File.Delete(logName); }
            }
        }
    }
}
