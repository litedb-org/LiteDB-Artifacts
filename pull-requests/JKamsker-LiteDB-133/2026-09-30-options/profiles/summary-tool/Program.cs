using Microsoft.Diagnostics.Tracing;
using System.Text.Json;
foreach(var path in args) {
 var counts = new Dictionary<string,long>();
 var alloc = new Dictionary<string,long>();
 double last = 0;
 using var source = new EventPipeEventSource(path);
 source.Clr.All += data => {
  last = Math.Max(last, data.TimeStampRelativeMSec);
  counts[data.EventName] = counts.GetValueOrDefault(data.EventName) + 1;
  if(data.EventName.Contains("AllocationTick")) {
   var name = data.PayloadByName("TypeName")?.ToString() ?? "unknown";
   var bytes = Convert.ToInt64(data.PayloadByName("AllocationAmount64"));
   alloc[name] = alloc.GetValueOrDefault(name) + bytes;
  }
 };
 source.Process();
 Console.WriteLine(JsonSerializer.Serialize(new { path, durationMS = last,
   events = counts.OrderByDescending(x=>x.Value),
   sampledAllocationBytesByType = alloc.OrderByDescending(x=>x.Value).Take(30) }));
}
