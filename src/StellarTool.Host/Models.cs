using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace StellarTool;
public static class ConfigRules {
 public static JsonObject Defaults() => JsonNode.Parse("""{"server":{"host":"127.0.0.1","managementPort":7856,"displayPort":7857},"taiko":{"showTiming":false,"mode":"taiko","width":800,"height":100,"bpmcolor":"EC2127","barcolor":"000000","bgcolor":"FFFFFFFF","linecolor":"000000","idle":"songinfo"},"score":{"mode":"score","width":800,"height":100,"bpmcolor":"EC2127","barcolor":"000000","bgcolor":"FFFFFFFF","linecolor":"000000","idle":"songinfo"},"corner":{"anchor":"top-left","bgcolor":"00000000","textcolor":"000000","time":true,"bpm":true,"pos":true,"metronome":"point","order":["time","bpm","pos","metronome"],"colors":{}}}""")!.AsObject();
 public static string Color(string s) { s=s.TrimStart('#'); if(!Regex.IsMatch(s,"^[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) throw new ArgumentException("颜色须为 RRGGBB 或 RRGGBBAA"); return s.ToUpperInvariant(); }
 public static JsonObject Validate(JsonObject input) {
  var result=Defaults(); foreach(var group in input) { if(result[group.Key] is not JsonObject dest || group.Value is not JsonObject source) throw new ArgumentException("未知配置组: "+group.Key); foreach(var prop in source) {if(!dest.ContainsKey(prop.Key)) throw new ArgumentException("未知参数: "+prop.Key);dest[prop.Key]=prop.Value?.DeepClone();} }
  var server=result["server"]!; var host=server["host"]!.GetValue<string>(); if(!System.Net.IPAddress.TryParse(host,out _)) throw new ArgumentException("监听地址必须为 IP 地址");
  foreach(var key in new[]{"managementPort","displayPort"}) {var n=server[key]!.GetValue<int>();if(n<1024||n>65535)throw new ArgumentException("端口范围 1024–65535");} if(server["managementPort"]!.GetValue<int>()==server["displayPort"]!.GetValue<int>())throw new ArgumentException("两个端口不能相同");
  foreach(var g in new[]{"taiko","score"}) {var o=result[g]!;if(o["mode"]!.GetValue<string>()!=g)throw new ArgumentException("模式配置不可串用");foreach(var k in new[]{"width","height"}) {var n=o[k]!.GetValue<int>();if(n<16||n>8192)throw new ArgumentException("尺寸范围 16–8192");} foreach(var k in new[]{"bpmcolor","barcolor","bgcolor","linecolor"}) o[k]=Color(o[k]!.GetValue<string>()); if(!new[]{"songinfo","empty"}.Contains(o["idle"]!.GetValue<string>()))throw new ArgumentException("idle 无效");}
  _ = result["taiko"]!["showTiming"]!.GetValue<bool>();
  var c=result["corner"]!; if(!new[]{"top-left","top-right","bottom-left","bottom-right"}.Contains(c["anchor"]!.GetValue<string>()))throw new ArgumentException("anchor 无效");if(!new[]{"point","bar","square","none"}.Contains(c["metronome"]!.GetValue<string>()))throw new ArgumentException("metronome 无效");foreach(var k in new[]{"bgcolor","textcolor"})c[k]=Color(c[k]!.GetValue<string>());foreach(var k in new[]{"time","bpm","pos"})_ = c[k]!.GetValue<bool>();
  var keys=new[]{"time","bpm","pos","metronome"};var order=c["order"]!.AsArray().Select(n=>n!.GetValue<string>()).ToArray();if(order.Length!=4||order.Distinct().Count()!=4||order.Any(k=>!keys.Contains(k)))throw new ArgumentException("行序须包含四个显示项各一次");foreach(var p in c["colors"]!.AsObject().ToArray()){if(!keys.Contains(p.Key))throw new ArgumentException("未知行颜色");c["colors"]![p.Key]=Color(p.Value!.GetValue<string>());}return result;
 }
}
public sealed class SnapshotStore {
 private readonly object gate=new(); private JsonObject latest=new(){["state"]="disconnected",["time"]=0,["paused"]=true};private long received;private long sequence;private string? chartSession;private byte[]? chartBytes;
 public bool Put(JsonObject next) {
  if(next["state"] is null||next["time"] is null)throw new ArgumentException("state/time 必填");
  var time=double.Parse(next["time"]!.ToJsonString(),System.Globalization.CultureInfo.InvariantCulture);if(!double.IsFinite(time))throw new ArgumentException("time 非有限值");
  var session=next["sessionId"]?.GetValue<string>();
  // Serialize immutable chart once, outside the short state lock. Never clone it per poll.
  byte[]? incomingChart=next["chart"] is {} ch?System.Text.Encoding.UTF8.GetBytes(ch.ToJsonString()):null;
  var stateCopy=new JsonObject();foreach(var p in next)if(p.Key!="chart")stateCopy[p.Key]=p.Value?.DeepClone();
  lock(gate){
   if(chartSession!=session){chartSession=session;chartBytes=null;}
   if(next.ContainsKey("chart"))chartBytes=incomingChart;
   latest=stateCopy;latest["sequence"]=++sequence;received=Environment.TickCount64;
   return next["state"]?.GetValue<string>()=="playing"&&chartBytes==null;
  }
 }
 public byte[]? Chart(string? session){lock(gate)return session==chartSession&&received!=0&&Environment.TickCount64-received<2000?chartBytes:null;}
 public JsonObject Get(bool includeChart=true,bool includeHitOffsets=false){lock(gate){var copy=new JsonObject();foreach(var p in latest)if(includeHitOffsets||p.Key!="hitOffsets")copy[p.Key]=p.Value?.DeepClone();var age=Environment.TickCount64-received;var connected=received!=0&&age<2000;copy["ageMs"]=received==0?0:age;copy["connected"]=connected;copy["chartReady"]=connected&&chartBytes!=null;if(includeChart)copy["chart"]=connected&&chartBytes!=null?JsonNode.Parse(chartBytes):null;if(!connected){copy["state"]="disconnected";copy["paused"]=true;copy["time"]=0;copy["song"]=null;copy["chart"]=null;if(includeHitOffsets)copy["hitOffsets"]=null;}return copy;}}
}
