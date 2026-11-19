using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace StellarTool;

public sealed record InputPlayer(int Player,bool Available,bool TouchAvailable,bool KeysAvailable,string TouchMask,int KeyMask);
public sealed record ButtonLed(string Source,string Rgb,double AgeMs);
public sealed record InputResponse(int SchemaVersion,bool Connected,bool Available,long AgeMs,long Sequence,string Source,string TouchSource,string KeySource,InputPlayer[] Players,ButtonLed?[]? ButtonLeds=null);
public sealed record InputEvent(InputPlayer[] Players,double SampleAgeMs,long SampleSequence);
public sealed record InputPacket(long Tick,double SampleAgeMs,long Sequence,string Session,InputPlayer[] Players,InputEvent[] Events,bool Reset,long Dropped,long SentAtUtcMs,long HostReceivedAtUtcMs,ButtonLed?[]? ButtonLeds=null);
public sealed record InputPush(string Session,long Sequence,bool Reset,bool Connected,long AgeMs,InputPlayer[] Players,InputEvent[] Events,long Dropped,bool Heartbeat,long SentAtUtcMs,long HostReceivedAtUtcMs,long HostSentAtUtcMs,ButtonLed?[]? ButtonLeds=null);

public sealed class InputSnapshotStore {
 public const int FreshnessMs=250, MaxEvents=32, MaxPayloadBytes=24*1024, MaxSubscribers=32;
 private const int MaxPending=16, MaxRetired=256;
 private const ulong Mask34=(1UL<<34)-1;
 private static readonly InputPlayer[] Empty={new(0,false,false,false,"000000000",0),new(1,false,false,false,"000000000",0)};
 private readonly object gate=new();
 private readonly List<Channel<InputPacket>> subscribers=new();
 private readonly HashSet<string> retired=new(StringComparer.Ordinal);
 private readonly Queue<string> retirementOrder=new();
 private InputPacket? latest;
 private readonly Func<long> clock;
 public InputSnapshotStore(Func<long>? clock=null){this.clock=clock??(()=>Environment.TickCount64);}
 private static double Age(JsonNode? node) {
  var age=double.TryParse(node?.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:double.NaN;
  if(!double.IsFinite(age)||age<0||age>10000)throw new ArgumentException("Invalid input age");
  return age;
 }
 private static InputPlayer[] ReadPlayers(JsonNode? node) {
  var source=node?.AsArray();if(source==null||source.Count!=2)throw new ArgumentException("Exactly two input players required");
  var players=new InputPlayer[2];
  for(int i=0;i<2;i++){
   var p=source[i]?.AsObject()??throw new ArgumentException("Invalid player");
   if(p["player"]?.GetValue<int>()!=i)throw new ArgumentException("Input player ordering");
   var touch=p["touchMask"]?.GetValue<string>();
   if(touch==null||touch.Length!=9||!touch.All(Uri.IsHexDigit)||!ulong.TryParse(touch,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out var bits)||bits>Mask34)throw new ArgumentException("Input touch mask is not 34 bits");
   var key=p["keyMask"]?.GetValue<int>()??-1;if(key<0||key>255)throw new ArgumentException("Input key mask is not eight bits");
   bool ta=p["touchAvailable"]?.GetValue<bool>()==true,ka=p["keysAvailable"]?.GetValue<bool>()==true;
   players[i]=new(i,ta||ka,ta,ka,ta?touch.ToLowerInvariant():"000000000",ka?key:0);
  }
  return players;
 }
 // Optional addon: malformed LED metadata must not suppress touch/press traffic.
 private static ButtonLed?[]? ReadLeds(JsonNode? node){
  if(node is not JsonArray {Count:2} rows)return null;
  var result=new ButtonLed?[2];
  for(var i=0;i<2;i++){
   if(rows[i] is not JsonObject row)continue;
   try{
    var src=row["source"]?.GetValue<string>();var rgb=row["rgb"]?.GetValue<string>();var age=Age(row["ageMs"]);
    if(src=="maimoller-native-hid-write"&&rgb?.Length==48&&rgb.All(Uri.IsHexDigit)&&age<1600)
     result[i]=new(src,rgb.ToLowerInvariant(),age);
   }catch(InvalidOperationException){}catch(ArgumentException){}
  }
  return result.Any(x=>x!=null)?result:null;
 }
 private static ButtonLed?[]? FreshLeds(InputPacket? packet,long elapsed,bool connected){
  if(!connected||packet?.ButtonLeds is not {} leds)return null;
  var result=new ButtonLed?[2];
  for(var i=0;i<2;i++)if(leds[i] is {} led&&led.AgeMs+elapsed<1600)result[i]=led with{AgeMs=led.AgeMs+elapsed};
  return result.Any(x=>x!=null)?result:null;
 }
 public void Put(JsonObject data) {
  if(data["schemaVersion"]?.GetValue<int>()!=1||data["source"]?.GetValue<string>()!="game-input"||data["touchSource"]?.GetValue<string>()!="game-touch-state"||data["keySource"]?.GetValue<string>()!="game-button-state")throw new ArgumentException("Unsupported input schema/source");
  var session=data["inputSessionId"]?.GetValue<string>();if(session==null||session.Length!=32||!session.All(Uri.IsHexDigit))throw new ArgumentException("Invalid input session");
  session=session.ToLowerInvariant();
  var seq=data["sequence"]?.GetValue<long>()??0;if(seq<1)throw new ArgumentException("Invalid input sequence");
  var age=Age(data["sampleAgeMs"]);
  var players=ReadPlayers(data["players"]);
  var reset=data["reset"]?.GetValue<bool>()??false;
  var dropped=data["dropped"]?.GetValue<long>()??0;if(dropped<0)throw new ArgumentException("Invalid dropped count");
  var eventNodes=data["events"]?.AsArray();
  if(eventNodes?.Count>MaxEvents)throw new ArgumentException("Too many input events");
  InputEvent[] events=eventNodes==null?Array.Empty<InputEvent>():new InputEvent[eventNodes.Count];
  long sample=0;
  for(int i=0;i<events.Length;i++){
   var node=eventNodes![i]?.AsObject()??throw new ArgumentException("Invalid input event");
   var s=node["sampleSequence"]?.GetValue<long>()??0;
   if(s<=sample)throw new ArgumentException("Invalid input event sequence");
   sample=s;events[i]=new(ReadPlayers(node["players"]),Age(node["sampleAgeMs"]),s);
  }
  var sent=data["sentAtUtcMs"]?.GetValue<long>()??0;var leds=ReadLeds(data["buttonLeds"]);
  lock(gate) {
   var previous=latest;
   if(retired.Contains(session)||previous!=null&&previous.Session==session&&seq<=previous.Sequence)return;
   if(previous==null||previous.Session!=session) {
    reset=true;
    if(previous!=null&&retired.Add(previous.Session)){
     retirementOrder.Enqueue(previous.Session);
     if(retirementOrder.Count>MaxRetired)retired.Remove(retirementOrder.Dequeue());
    }
   }else if(seq!=previous.Sequence+1||dropped!=previous.Dropped)reset=true;
   var incoming=new InputPacket(clock(),age,seq,session,players,reset?Array.Empty<InputEvent>():events,reset,dropped,sent,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),leds);
   Volatile.Write(ref latest,incoming);
   foreach(var channel in subscribers)if(!channel.Writer.TryWrite(incoming)){
    while(channel.Reader.TryRead(out _)){}
    channel.Writer.TryWrite(Reset(incoming));
   }
  }
 }
 private static InputPacket Reset(InputPacket p)=>p with {Reset=true,Events=Array.Empty<InputEvent>()};
 public Channel<InputPacket>? Subscribe() {
  lock(gate){
   if(subscribers.Count>=MaxSubscribers)return null;
   var channel=Channel.CreateBounded<InputPacket>(new BoundedChannelOptions(MaxPending){FullMode=BoundedChannelFullMode.Wait});
   subscribers.Add(channel);
   if(latest is {} p)channel.Writer.TryWrite(Reset(p));
   return channel;
  }
 }
 public void Unsubscribe(Channel<InputPacket> channel) { lock(gate){subscribers.Remove(channel);channel.Writer.TryComplete();} }
 public InputPush Push(InputPacket? p,bool heartbeat=false) {
  long elapsed=p==null?0:Math.Max(0,clock()-p.Tick);
  long age=p==null?0:elapsed+(long)Math.Ceiling(p.SampleAgeMs);
  bool connected=p!=null&&age<FreshnessMs;
  bool reset=p?.Reset!=false||!connected||(!heartbeat&&p!.Events.Any(e=>e.SampleAgeMs+elapsed>=FreshnessMs));
  return new(p?.Session??"",p?.Sequence??0,reset,connected,age,connected?p!.Players:Empty,reset||heartbeat?Array.Empty<InputEvent>():p!.Events,p?.Dropped??0,heartbeat,p?.SentAtUtcMs??0,p?.HostReceivedAtUtcMs??0,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),FreshLeds(p,elapsed,connected));
 }
 public InputResponse Get() {
  // Sample age and host elapsed time are monotonic independently; do not subtract cross-machine wall clocks.
  var p=Volatile.Read(ref latest);long elapsed=p==null?0:Math.Max(0,clock()-p.Tick);long age=elapsed+(long)Math.Ceiling(p?.SampleAgeMs??0);
  bool connected=p!=null&&age<FreshnessMs;var players=connected?p!.Players:Empty;
  return new(1,connected,connected&&players.Any(x=>x.Available),age,p?.Sequence??0,"game-input","game-touch-state","game-button-state",players,FreshLeds(p,elapsed,connected));
 }
}
