using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;
using LiteDB.Engine;

class Program
{
    const string Expression = "IIF($.number = DOUBLE($.number), 1, 0)";
    static BsonDocument Doc(int id) => new BsonDocument {
        ["_id"] = id, ["oid"] = new ObjectId((id % 2 == 0 ? "80000000" : "7fffffff") + "1122334455" + id.ToString("x6")),
        ["score"] = id % 100, ["number"] = 0.1m, ["payload"] = new string((char)('a' + id % 26),128) + id };
    static void Require(bool value, string why) { if (!value) throw new Exception(why); }
    static void Main(string[] args)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            engine=typeof(LiteDatabase).Assembly.FullName,engineSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(LiteDatabase).Assembly.Location))) }));
        var path=args[0]; var count=int.Parse(args[1]); var password=args[2]=="plain"?null:"cost-fixture";
#if !CURRENT
        using (var db=new LiteDatabase(new ConnectionString {Filename=path, Password=password, Collation=Collation.Binary})) {
            db.CheckpointSize=0;
            var rows=db.GetCollection("rows"); rows.Insert(Enumerable.Range(1,count).Select(Doc));
            rows.EnsureIndex("oid"); rows.EnsureIndex("score"); rows.EnsureIndex("computed",Expression);
            db.GetCollection("cold").Insert(new BsonDocument { ["_id"]=1, ["payload"]="preserved" });
            Require(rows.Count(Expression+" = 1")==count,"released comparator control"); db.Checkpoint();
        }
        Console.WriteLine(JsonSerializer.Serialize(new {created=path,count,dataBytes=new FileInfo(path).Length}));
#else
        var original=Inspect(path,password); var logPath=Path.Combine(Path.GetDirectoryName(path),Path.GetFileNameWithoutExtension(path)+"-log"+Path.GetExtension(path));
        var dataBefore=new FileInfo(path).Length; var logBefore=File.Exists(logPath)?new FileInfo(logPath).Length:0;
        using var data=new CountFile(path); using var log=new CountFile(logPath);
        var settings=new EngineSettings {DataStream=data,LogStream=log,Password=password,CompactStorage=CompactStorageMode.Auto};
        var watch=Stopwatch.StartNew(); var engine=new LiteEngine(settings); var openMs=watch.Elapsed.TotalMilliseconds;
        var atOpen=new {dataWrites=data.Written,logWrites=log.Written,dataCalls=data.Writes,logCalls=log.Writes,dataSyncs=data.Syncs,logSyncs=log.Syncs,dataLength=data.Length,logLength=log.Length,walPeak=log.Peak};
        using (var db=new LiteDatabase(engine)) {
            Verify(db,count); watch.Restart(); db.Checkpoint(); var checkpointMs=watch.Elapsed.TotalMilliseconds;
            Console.WriteLine(JsonSerializer.Serialize(new {count,password=password==null?"plain":"encrypted",openMs,checkpointMs,dataBefore,logBefore,atOpen,
                afterCheckpoint=new {dataWrites=data.Written,logWrites=log.Written,dataSyncs=data.Syncs,logSyncs=log.Syncs,dataLength=data.Length,logLength=log.Length,walPeak=log.Peak}}));
        }
        data.Flush(true);log.Flush(true);
        var after=Inspect(path,password); Require(original.Data.Count==after.Data.Count,"data page count changed");
        Require(original.Data.All(kv=>after.Data.TryGetValue(kv.Key,out var hash)&&hash==kv.Value),"legacy data pages rewritten");
        Console.WriteLine(JsonSerializer.Serialize(new {before=original.Summary,after=after.Summary,unchangedDataPages=original.Data.Count}));
        for(var run=0;run<3;run++) {data.Reset();log.Reset();watch.Restart();using(var db=new LiteDatabase(new LiteEngine(settings))) {var repeatMs=watch.Elapsed.TotalMilliseconds;Verify(db,count);
            Console.WriteLine(JsonSerializer.Serialize(new {repeat=run,openMs=repeatMs,dataWrites=data.Written,logWrites=log.Written,dataSyncs=data.Syncs,logSyncs=log.Syncs}));}}
#endif
    }
#if CURRENT
    static void Verify(LiteDatabase db,int count) {
        var rows=db.GetCollection("rows");var docs=rows.FindAll().OrderBy(x=>x["_id"].AsInt32).ToArray();Require(docs.Length==count,"count");
        for(var i=0;i<count;i++) Require(BsonSerializer.Serialize(docs[i]).SequenceEqual(BsonSerializer.Serialize(Doc(i+1))),"payload "+i);
        Require(rows.Count(Expression+" = 0")==count && rows.Count(Expression+" = 1")==0,"computed index");
        Require(rows.Query().Where(Expression+" = 0").GetPlan()["index"]["name"].AsString=="computed","computed index plan");
        for(var score=0;score<100;score++) Require(rows.Find(Query.EQ("score",score)).Select(x=>x["_id"].AsInt32).OrderBy(x=>x).SequenceEqual(Enumerable.Range(1,count).Where(id=>id%100==score)),"scalar index");
        var expected=Enumerable.Range(1,count).OrderBy(id=>Doc(id)["oid"].AsObjectId.ToString(),StringComparer.Ordinal);
        Require(rows.Query().OrderBy("$.oid").ToArray().Select(x=>x["_id"].AsInt32).SequenceEqual(expected),"oid ordering");
        Require(db.GetCollection("cold").FindById(1)["payload"].AsString=="preserved","unrelated collection");
    }
    class PageImage { public object Summary;public Dictionary<uint,string> Data=new Dictionary<uint,string>(); }
    static PageImage Inspect(string path,string password) {
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var stream=password==null?(Stream)file:new AesStream(password,file,allowRecovery:false);
        var result=new PageImage();var page=new byte[8192];var types=new Dictionary<byte,int>();byte version=0;uint last=0;
        for(long position=0;position<stream.Length;position+=8192) {stream.ReadExactly(page);if(position==0){version=page[59];last=BitConverter.ToUInt32(page,64);}types.TryGetValue(page[4],out var n);types[page[4]]=n+1;
            if(page[4]==4)result.Data[BitConverter.ToUInt32(page,0)]=Convert.ToHexString(SHA256.HashData(page));}
        result.Summary=new {version,lastPageId=last,logicalPages=stream.Length/8192,pageTypes=types};return result;
    }
    sealed class CountFile:FileStream {
        public long Written,Writes,Syncs,Peak;
        public CountFile(string path):base(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite){Peak=Length;}
        public void Reset(){Written=Writes=Syncs=0;Peak=Length;}
        public override void Write(byte[] bytes,int offset,int count){base.Write(bytes,offset,count);Written+=count;Writes++;Peak=Math.Max(Peak,Length);}
        public override void WriteByte(byte value){Write(new[]{value},0,1);}
        public override void SetLength(long length){base.SetLength(length);Peak=Math.Max(Peak,length);}
        public override void Flush(bool durable){base.Flush(durable);if(durable)Syncs++;}
    }
#endif
}
