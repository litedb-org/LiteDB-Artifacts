using LiteDB;
var dir=args[0];
var encrypted=args[1]=="encrypted";
var file=Path.Combine(dir,"data.db");
var expected=new[]{1}.Concat(Enumerable.Range(100,60)).ToArray();
for(var n=0;n<2;n++)
{
 using var db=new LiteDatabase(new ConnectionString{Filename=file,Password=encrypted?"secret":null});
 var rows=db.GetCollection("rows");
 if(!rows.FindAll().Select(x=>x["_id"].AsInt32).OrderBy(x=>x).SequenceEqual(expected))throw new Exception("row mismatch");
 foreach(var id in expected){var q=rows.Query().Where(Query.EQ("value",id));if(q.GetPlan()["index"]["name"].AsString!="value"||q.Single()["_id"].AsInt32!=id)throw new Exception("index mismatch");}
 if(db.GetCollection("sentinel").FindById(42)["value"].AsInt32!=42)throw new Exception("sentinel mismatch");
}
Console.WriteLine("KILLED_GETTER_RECOVERY_EXACT_INDEX_SENTINEL_VERIFIED");
