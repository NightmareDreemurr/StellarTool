using System.Text.Json.Nodes;
using StellarTool.Mod;
namespace StellarTool.Tests;
internal static class InputTests {
 public static void Run() {
  InputCaptureQueueTests.Run();
  int checks=0;void Check(bool ok,string name){if(!ok)throw new Exception("input: "+name);checks++;}
  void Reject(Action action,string name){try{action();}catch(ArgumentException){checks++;return;}throw new Exception("input accepted "+name);}
  long tick=1000,utc=1800000000000;var store=new InputSnapshotStore(()=>tick);
  JsonObject Packet(long seq=1,string mask="200000001",int key=129)=>JsonNode.Parse($$"""{"schemaVersion":1,"inputSessionId":"0123456789abcdef0123456789abcdef","sequence":{{seq}},"source":"game-input","touchSource":"game-touch-state","keySource":"game-button-state","sampleAgeMs":0,"sentAtUtcMs":{{utc}},"players":[{"player":0,"available":true,"touchAvailable":true,"keysAvailable":true,"touchMask":"{{mask}}","keyMask":{{key}}},{"player":1,"available":true,"touchAvailable":true,"keysAvailable":true,"touchMask":"000030000","keyMask":66}]}""")!.AsObject();
  Check(!store.Get().Connected&&!store.Get().Available,"starts unavailable");
  store.Put(Packet());var got=store.Get();Check(got.Connected&&got.Available,"fresh");
  Check(got.Players[0].TouchMask=="200000001"&&got.Players[0].KeyMask==129,"A1+E8 and keys1+8 simultaneous");
  Check(got.Players[1].TouchMask=="000030000"&&got.Players[1].KeyMask==66,"P2 C1+C2 and keys2+7 independent");
  tick+=249;Check(store.Get().Available,"249ms fresh");tick++;got=store.Get();Check(!got.Connected&&!got.Available&&got.Players.All(p=>p.TouchMask=="000000000"&&p.KeyMask==0),"250ms clears all");
  store.Put(Packet(2,"000000000",0));Check(store.Get().Players[0].TouchMask=="000000000"&&store.Get().Players[0].KeyMask==0,"release without latch");
  store.Put(Packet(1));Check(store.Get().Sequence==2&&store.Get().Players[0].KeyMask==0,"old sequence ignored");
  Reject(()=>store.Put(Packet(3,"400000000")),"35th bit");Reject(()=>store.Put(Packet(3,"20000001")),"mask width");Reject(()=>store.Put(Packet(3,"z00000000")),"mask characters");Reject(()=>store.Put(Packet(3,key:256)),"select bit");
  var p=Packet(3);p["players"]!.AsArray().RemoveAt(1);Reject(()=>store.Put(p),"player count");
  p=Packet(3);p["players"]![0]!["player"]=1;Reject(()=>store.Put(p),"player order");
  p=Packet(3);p["sampleAgeMs"]=251;store.Put(p);Check(!store.Get().Available,"stale before send");
  p=Packet(4);p["sentAtUtcMs"]=utc-300;store.Put(p);Check(store.Get().Available,"remote clock behind tolerated");
  p=Packet(5);p["sentAtUtcMs"]=utc+300;store.Put(p);Check(store.Get().Available,"remote clock ahead tolerated");
  p=Packet(6);p["players"]![0]!["touchAvailable"]=false;store.Put(p);got=store.Get();Check(got.Players[0].TouchMask=="000000000"&&got.Players[0].KeysAvailable&&got.Players[0].KeyMask==129,"partial channel unavailable clears only channel");
  p=Packet(7);p["source"]="browser-touch";Reject(()=>store.Put(p),"fabricated source");
  var ledStore=new InputSnapshotStore(()=>tick);
  JsonObject Led(string rgb,double age)=>new(){["source"]="maimoller-native-hid-write",["rgb"]=rgb,["ageMs"]=age};
  string rgb="ff000000ff000000ff000000101010202020303030404040";
  p=Packet(1,key:0);p["buttonLeds"]=new JsonArray(Led(rgb,500),null);ledStore.Put(p);
  Check(ledStore.Get().ButtonLeds?[0]?.Rgb==rgb&&ledStore.Get().ButtonLeds?[1]==null&&ledStore.Get().Players[0].KeyMask==0,"unpressed physical RGB and unavailable P2 are independent");
  var leds=ledStore.Subscribe()!;
  Check(leds.Reader.TryRead(out var ledPacket)&&ledStore.Push(ledPacket).ButtonLeds?[0]?.AgeMs==500,"WS carries RGB on reconnect");
  tick+=25;Check(ledStore.Get().ButtonLeds?[0]?.AgeMs==525,"increment monotonic sample age");
  p=Packet(2,key:1);p["buttonLeds"]=new JsonArray(Led(new string('0',48),1550),Led("malformed",0));ledStore.Put(p);
  Check(ledStore.Get().ButtonLeds?[0]?.Rgb==new string('0',48)&&ledStore.Get().ButtonLeds?[1]==null,"commanded black versus missing");
  tick+=50;Check(ledStore.Get().Connected&&ledStore.Get().ButtonLeds==null&&ledStore.Get().Players[0].KeyMask==1,"LED expires independently of key state");
  p=Packet(3,key:3);p["buttonLeds"]=new JsonArray(new JsonObject{["source"]=123,["rgb"]=rgb,["ageMs"]=0},null);ledStore.Put(p);
  Check(ledStore.Get().ButtonLeds==null&&ledStore.Get().Players[0].KeyMask==3,"optional malformed LED doesn't reject presses");
  ledStore.Put(Packet(4));Check(ledStore.Get().ButtonLeds==null,"legacy producer stays compatible");
  var batch=new InputSnapshotStore(()=>tick);
  var subscribers=Enumerable.Range(0,7).Select(_=>batch.Subscribe()!).ToArray();
  batch.Put(Packet(1));
  Check(subscribers.All(c=>c.Reader.TryRead(out var frame)&&frame.Reset),"seven subscribers get initial reset");
  JsonObject Edge(int key,long seq,double age=0)=>new(){["players"]=Packet(key:key)["players"]!.DeepClone(),["sampleSequence"]=seq,["sampleAgeMs"]=age};
  p=Packet(2,key:0);p["events"]=new JsonArray(Edge(1,1),Edge(0,2));batch.Put(p);
  foreach(var c in subscribers){
   Check(c.Reader.TryRead(out var f)&&f.Events.Length==2&&f.Events[0].Players[0].KeyMask==1&&f.Players[0].KeyMask==0,"short tap survives fanout");
  }
  Check(batch.Get().Players[0].KeyMask==0,"v1 stays latest-only");
  for(int i=3;i<=40;i++){
   batch.Put(Packet(i,key:i&1));
   for(int j=0;j<6;j++)Check(subscribers[j].Reader.TryRead(out var f)&&f.Sequence==i&&!f.Reset,"stalled seventh client does not block fast clients");
  }
  Check(subscribers[6].Reader.TryRead(out var recovered)&&recovered.Reset&&recovered.Sequence>3,"bounded slow-client reset");
  tick+=250;var stale=batch.Push(recovered,true);Check(stale.Heartbeat&&!stale.Connected&&stale.Reset&&stale.Events.Length==0&&stale.Players.All(player=>player.KeyMask==0),"stale same-sequence heartbeat clears");
  p=Packet(41);p["events"]=new JsonArray(Edge(1,3,250));batch.Put(p);
  Check(subscribers[0].Reader.TryRead(out var expired)&&batch.Push(expired).Reset&&batch.Push(expired).Events.Length==0,"stale edges no replay");
  p=Packet(42);p["events"]=new JsonArray(Edge(1,4),Edge(0,4));Reject(()=>batch.Put(p),"invalid edge sequence");
  p=Packet(42);p["events"]=new JsonArray(Enumerable.Range(1,33).Select(i=>Edge(0,i)).ToArray());Reject(()=>batch.Put(p),"oversize batch");
  p=Packet(1,key:0);p["inputSessionId"]="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";batch.Put(p);
  batch.Put(Packet(100,key:255));Check(batch.Get().Sequence==1&&batch.Get().Players[0].KeyMask==0,"retired producer cannot reclaim session");
  foreach(var c in subscribers)batch.Unsubscribe(c);
  var buffer=new InputCaptureBuffer();buffer.Publish(ulong.MaxValue,1,511,66,true,77);Check(buffer.TryRead(out var read)&&read.Touch0==17179869183UL&&read.Touch1==1&&read.Key0==255&&read.Key1==66&&read.Ticks==77,"bounded raw masks");
  for(int i=0;i<100;i++){buffer.Publish((ulong)i,(ulong)i,3,6,true,i);buffer.TryRead(out _);}
  long allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++){buffer.Publish((ulong)i,(ulong)i,3,6,true,i);buffer.TryRead(out _);}Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"capture buffer allocation-free");
  var done=false;var writer=new Thread(()=>{for(int i=1;i<100000;i++)buffer.Publish((ulong)i,(ulong)i,i&255,i&255,true,i);Volatile.Write(ref done,true);});writer.Start();
  while(!Volatile.Read(ref done))if(buffer.TryRead(out read))Check(read.Touch0==read.Touch1,"coherent concurrent touch values");writer.Join();
  Check(buffer.TryRead(out read)&&read.Touch0==99999&&read.Ticks==99999,"final copied sample");
  store.Put(Packet(8));allocated=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)store.Get();var perPoll=(GC.GetAllocatedBytesForCurrentThread()-allocated)/1000.0;
  Console.WriteLine($"{checks} input assertions passed; capture=0 allocations/10000; state read={perPoll:F0} bytes/poll (no chart data)");
 }
}
