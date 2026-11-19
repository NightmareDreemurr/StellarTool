using StellarTool;
using System.Text.Json.Nodes;
int passed=0;void Check(bool b,string name){if(!b)throw new Exception(name);passed++;}void Reject(Action a,string name){try{a();}catch{passed++;return;}throw new Exception("accepted "+name);}
Check(ConfigRules.Color("#00000080")=="00000080","alpha");Check(ConfigRules.Color("abcdef")=="ABCDEF","case");Reject(()=>ConfigRules.Color("red"),"color");
var c=ConfigRules.Defaults();ConfigRules.Validate(c);passed++;c["server"]!["displayPort"]=7856;Reject(()=>ConfigRules.Validate(c),"collision");c=ConfigRules.Defaults();c["taiko"]!["width"]=-1;Reject(()=>ConfigRules.Validate(c),"size");c=ConfigRules.Defaults();c["corner"]!["order"]=new JsonArray("time","time","bpm","pos");Reject(()=>ConfigRules.Validate(c),"order");
var s=new SnapshotStore();Check(s.Get()["connected"]!.GetValue<bool>()==false,"disconnected");s.Put(new(){["state"]="play",["time"]=10,["sessionId"]="a"});Check(s.Get()["connected"]!.GetValue<bool>(),"connected");var v=s.Get();v["time"]=999;Check(s.Get()["time"]!.GetValue<int>()==10,"immutable");s.Put(new(){["state"]="select",["time"]=0,["song"]=null});Check(s.Get()["song"]==null,"clear");Console.WriteLine($"{passed} checks passed");

s.Put(new(){["state"]="playing",["time"]=0,["sessionId"]="timing",["hitOffsets"]=new JsonObject{["available"]=true}});
Check(!s.Get(false).ContainsKey("hitOffsets"),"ordinary light poll skips requested-only timing samples");
Check(s.Get(false,true)["hitOffsets"]?["available"]?.GetValue<bool>()==true,"judgement opt-in returns independent timing");
StellarTool.Tests.JsonTests.Run();
var events=new JsonArray();for(int i=0;i<3000;i++)events.Add(new JsonObject{["time"]=i*.1,["tick"]=i*24,["lane"]=i%8,["type"]="Tap",["break"]=false,["each"]=false});var sample=new JsonObject{["state"]="playing",["time"]=1,["sessionId"]="bench",["chart"]=new JsonObject{["events"]=events}};
var before=GC.GetAllocatedBytesForCurrentThread();var sw=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<20;i++)s.Put(sample);for(int i=0;i<70;i++)s.Get().ToJsonString();sw.Stop();Console.WriteLine($"BASELINE 3000 notes, 20 ingest+70 full polls: {sw.Elapsed.TotalMilliseconds:F1} ms, {(GC.GetAllocatedBytesForCurrentThread()-before)/1048576.0:F1} MiB allocated; payload={sample.ToJsonString().Length} bytes");

s.Put(sample);var lite=(JsonObject)sample.DeepClone();lite.Remove("chart");before=GC.GetAllocatedBytesForCurrentThread();sw.Restart();for(int i=0;i<20;i++)s.Put(lite);for(int i=0;i<70;i++)s.Get(false).ToJsonString();sw.Stop();Console.WriteLine($"OPTIMIZED 3000 notes, 20 lightweight ingest+70 polls: {sw.Elapsed.TotalMilliseconds:F1} ms, {(GC.GetAllocatedBytesForCurrentThread()-before)/1048576.0:F3} MiB allocated; payload={s.Get(false).ToJsonString().Length} bytes");
Check(s.Chart("bench")!=null,"chart survives lightweight updates");Check(s.Get(false)["chart"]==null,"lightweight excludes chart");Check(s.Chart("wrong-session")==null,"chart session isolation");s.Put(new(){["state"]="playing",["time"]=0,["sessionId"]="new"});Check(s.Chart("new")==null,"new session clears old chart");Check(s.Put(new(){["state"]="playing",["time"]=1,["sessionId"]="new"}),"missing chart requests retry");s.Put(sample);s.Put(new(){["state"]="idle",["time"]=0,["sessionId"]="bench",["chart"]=null});Check(s.Chart("bench")==null,"explicit null clears chart");Console.WriteLine($"{passed} store/config checks passed");

Check(!ConfigRules.Defaults()["taiko"]!["showTiming"]!.GetValue<bool>(),"timing default off");var timing=ConfigRules.Defaults();timing["taiko"]!["showTiming"]=true;Check(ConfigRules.Validate(timing)["taiko"]!["showTiming"]!.GetValue<bool>(),"timing saved");timing["taiko"]!["showTiming"]="true";Reject(()=>ConfigRules.Validate(timing),"timing type");

StellarTool.Tests.InputTests.Run();
