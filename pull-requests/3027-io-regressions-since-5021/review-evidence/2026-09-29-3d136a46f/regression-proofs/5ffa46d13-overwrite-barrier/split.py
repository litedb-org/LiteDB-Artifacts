import os

os.chdir('$REPO/.claude/worktrees/agent-ae670113747d5f100/LiteDB.ReproRunner/Repros/Issue_3027_OverwriteBehindUnsyncedLog')
s = open('Program.cs').read()
start = s.index('    /// <summary>\n    /// A file on a modeled device.')
end = s.rindex('}\n')  # the closing brace of class Program is the last line
device = s[start:end]
program = s[:start].rstrip() + '\n}\n'
# Dedent the device class to top level.
lines = [l[4:] if l.startswith('    ') else l for l in device.rstrip().split('\n')]
device_src = ('namespace Issue_3027_OverwriteBehindUnsyncedLog;\n\n' + '\n'.join(lines) + '\n').replace(
    'private sealed class DeviceFile', 'internal sealed class DeviceFile')
open('DeviceFile.cs', 'w').write(device_src)

cap_start = program.index('    /// <summary>\n    /// The devices when the data file was about to be synced')
old_capture = program[cap_start:program.rindex('}\n')]
new_capture = '''    /// <summary>
    /// The devices when the data file was about to be synced with the checkpoint's writes pending: every data
    /// image a power loss before that sync may leave (a prefix of the pending writes, the last whole or torn),
    /// and the log as of its last successful sync.
    /// </summary>
    private sealed record Capture(List<(string Name, byte[] Image)> Images, byte[] Log, int LogPending, int Writes, int Overwrites)
    {
        internal static Capture Take(DeviceFile data, DeviceFile log, long deviceLength)
        {
            var (writes, overwrites) = data.PendingWrites(deviceLength);
            return new Capture(data.PowerLossImages(), log.Device, log.Pending, writes, overwrites);
        }
    }
'''
program = program.replace(old_capture, new_capture)
program = program.replace('capture = new Capture(data, log, dataBefore.Device.Length)', 'capture = Capture.Take(data, log, dataBefore.Device.Length)')
assert 'new Capture(data, log' not in program

old_inspect_body = '''            using var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log });
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            var rows = db.GetCollection("rows");
            var all = rows.FindAll().OrderBy(x => x["_id"].AsInt32).ToList();
            ids = all.Select(x => x["_id"].AsInt32).ToList();
            if (ids.Count < Synced || !ids.SequenceEqual(Enumerable.Range(1, ids.Count))) return new Outcome(false, ids, $"rows [{Ids(ids)}]");
            var changed = all.FirstOrDefault(x => !BsonSerializer.Serialize(x).SequenceEqual(BsonSerializer.Serialize(Row(x["_id"].AsInt32))));
            if (changed != null) return new Outcome(false, ids, $"rows [{Ids(ids)}], _id {changed["_id"]} changed");
            for (var value = 0; value < 5; value++)
            {
                var found = rows.Find(Query.EQ("value", value)).Select(x => x["_id"].AsInt32).OrderBy(x => x).ToList();
                if (!found.SequenceEqual(ids.Where(id => id % 5 == value)))
                    return new Outcome(false, ids, $"rows [{Ids(ids)}], the value index finds [{Ids(found)}] for value {value}");
            }
            var count = rows.Count();
            return count == ids.Count ? new Outcome(true, ids, $"rows [{Ids(ids)}]") : new Outcome(false, ids, $"rows [{Ids(ids)}], counted {count}");
        }
        catch (Exception error)
        {
            return new Outcome(false, ids, $"{error.GetType().Name}: {error.Message}");
        }
    }

    private static void RequireRows(LiteDatabase db, int count, string name)
    {
        var rows = db.GetCollection("rows");
        var all = rows.FindAll().OrderBy(x => x["_id"].AsInt32).ToList();
        Require(all.Select(x => x["_id"].AsInt32).SequenceEqual(Enumerable.Range(1, count)) &&
            all.All(x => BsonSerializer.Serialize(x).SequenceEqual(BsonSerializer.Serialize(Row(x["_id"].AsInt32)))),
            $"{name} reads rows [{Ids(all.Select(x => x["_id"].AsInt32))}], expected 1-{count}");
        for (var value = 0; value < 5; value++)
            Require(rows.Find(Query.EQ("value", value)).Select(x => x["_id"].AsInt32).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, count).Where(id => id % 5 == value)),
                $"{name}: the value index does not find every row of value {value}");
    }
'''
new_inspect_body = '''            using var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log });
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            return Check(db);
        }
        catch (Exception error)
        {
            return new Outcome(false, new List<int>(), $"{error.GetType().Name}: {error.Message}");
        }
    }

    /// <summary>Read "rows" as <see cref="Inspect"/> does; a read that throws propagates.</summary>
    private static Outcome Check(LiteDatabase db)
    {
        var rows = db.GetCollection("rows");
        var all = rows.FindAll().OrderBy(x => x["_id"].AsInt32).ToList();
        var ids = all.Select(x => x["_id"].AsInt32).ToList();
        if (ids.Count < Synced || !ids.SequenceEqual(Enumerable.Range(1, ids.Count))) return new Outcome(false, ids, $"rows [{Ids(ids)}]");
        var changed = all.FirstOrDefault(x => !BsonSerializer.Serialize(x).SequenceEqual(BsonSerializer.Serialize(Row(x["_id"].AsInt32))));
        if (changed != null) return new Outcome(false, ids, $"rows [{Ids(ids)}], _id {changed["_id"]} changed");
        for (var value = 0; value < 5; value++)
        {
            var found = rows.Find(Query.EQ("value", value)).Select(x => x["_id"].AsInt32).OrderBy(x => x).ToList();
            if (!found.SequenceEqual(ids.Where(id => id % 5 == value)))
                return new Outcome(false, ids, $"rows [{Ids(ids)}], the value index finds [{Ids(found)}] for value {value}");
        }
        var count = rows.Count();
        return count == ids.Count ? new Outcome(true, ids, $"rows [{Ids(ids)}]") : new Outcome(false, ids, $"rows [{Ids(ids)}], counted {count}");
    }

    private static void RequireRows(LiteDatabase db, int count, string name)
    {
        var outcome = Check(db);
        Require(outcome.Intact && outcome.Ids.Count == count, $"{name} reads {outcome.Detail}, expected rows 1-{count}");
    }
'''
assert program.count(old_inspect_body) == 1
program = program.replace(old_inspect_body, new_inspect_body)
old_head = '''    private static Outcome Inspect((byte[] Data, byte[] Log) image)
    {
        var ids = new List<int>();
        try'''
assert program.count(old_head) == 1
program = program.replace(old_head, '''    private static Outcome Inspect((byte[] Data, byte[] Log) image)
    {
        try''')
open('Program.cs', 'w').write(program)
print("done")
