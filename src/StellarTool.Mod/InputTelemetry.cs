using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;

namespace StellarTool.Mod {
public static class InputTelemetry {
 // Each lifetime owns its queue, credentials, worker and session. A stopping worker
 // never reads a new lifetime's state; Stop is a flag store, never Abort/Join.
 sealed class Run {
  public readonly InputCaptureBuffer Buffer=new InputCaptureBuffer();
  public bool[,] Keys;public ulong[,] Touch;public Func<bool> Initialized;
  public volatile bool Running=true,Faulted;
  public string Endpoint,Token,Session;
 }
 static Run current;static bool patched;
 static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
 public static void Start(Assembly game,HarmonyLib.Harmony harmony,string bridgeEndpoint,string bridgeToken) {
  if(Volatile.Read(ref current)!=null)return;
  var input=game.GetType("Manager.InputManager",true);var mecha=game.GetType("IO.MechaManager",true);
  var r=new Run();
  r.Keys=(bool[,])input.GetField("PlayerButtonPush",All).GetValue(null);
  var log=input.GetField("TpLog",All).GetValue(null);
  r.Touch=(ulong[,])log.GetType().GetField("_tpData",All).GetValue(log);
  if(r.Keys.GetLength(0)!=2||r.Keys.GetLength(1)!=9||r.Touch.GetLength(0)!=2||r.Touch.GetLength(1)!=3)throw new InvalidOperationException("Unverified input dimensions");
  r.Initialized=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),mecha.GetProperty("IsInitialized",All).GetGetMethod(true));
  var method=input.GetMethod("UpdateAmInput",All,null,Type.EmptyTypes,null);
  if(method==null)throw new InvalidOperationException("Input update unavailable");
  r.Endpoint=new Uri(new Uri(bridgeEndpoint),"/api/input-ingest").AbsoluteUri;r.Token=bridgeToken;
  r.Session=Guid.NewGuid().ToString("N");
  if(!patched){harmony.Patch(method,postfix:new HarmonyMethod(typeof(InputTelemetry).GetMethod("Capture",All)){priority=Priority.Last});patched=true;}
  Volatile.Write(ref current,r);
  new Thread(()=>SendLoop(r)){IsBackground=true,Name="Stellar input publisher"}.Start();
 }
 // Game-recognized state, not raw voltage or judgement. Fixed primitive queue only.
 static void Capture() {
  var r=Volatile.Read(ref current);if(r==null||!r.Running||r.Faulted)return;
  try {
   bool ready=r.Initialized();int a=0,b=0;
   if(ready)for(int i=0;i<8;i++){if(r.Keys[0,i])a|=1<<i;if(r.Keys[1,i])b|=1<<i;}
   r.Buffer.Publish(ready?r.Touch[0,0]:0UL,ready?r.Touch[1,0]:0UL,a,b,ready,Stopwatch.GetTimestamp());
  } catch {r.Faulted=true;r.Buffer.Publish(0,0,0,0,false,Stopwatch.GetTimestamp());}
 }
 static object Player(int player,ulong mask,int key,bool ready) {
  return new {player,available=ready,touchAvailable=ready,keysAvailable=ready,touchMask=(ready?mask:0UL).ToString("x9",CultureInfo.InvariantCulture),keyMask=ready?key:0};
 }
 static object[] Players(InputSample s,bool ready){return new[]{Player(0,s.Touch0,s.Key0,ready),Player(1,s.Touch1,s.Key1,ready)};}
 static double Age(InputSample s,long now){return s.Ticks==0?10000:Math.Min(10000,Math.Max(0,(now-s.Ticks)*1000.0/Stopwatch.Frequency));}
 static void SendLoop(Run r) {
  long sequence=0,floor=0,lastSent=0,previousDropped=0;bool capable=false,reset=true;long ledSequence0=0,ledSequence1=0;
  var edges=new List<InputSample>(32);
  while(r.Running) {
   long now=Stopwatch.GetTimestamp();InputSample latest,item;
   if(!r.Buffer.TryRead(out latest)){Thread.Sleep(2);continue;}
   edges.Clear();
   while(edges.Count<32&&r.Buffer.TryDequeue(out item))if(item.Sequence>floor)edges.Add(item);
   long dropped=r.Buffer.Dropped;
   bool stale=r.Faulted||Age(latest,now)>=250;
   bool resync=reset||stale||dropped!=previousDropped||!capable;
   if(resync){edges.Clear();floor=Math.Max(floor,latest.Sequence);}
   var led0=NativeButtonLeds.Read(0,now);var led1=NativeButtonLeds.Read(1,now);
   bool ledChanged=led0.Sequence!=ledSequence0||led1.Sequence!=ledSequence1;
   if(edges.Count==0&&!reset&&dropped==previousDropped&&(now-lastSent)*1000.0/Stopwatch.Frequency<(capable?(ledChanged?16:50):34)){Thread.Sleep(2);continue;}
   var sample=edges.Count>0?edges[edges.Count-1]:latest;
   if(sample.Sequence<floor){Thread.Sleep(2);continue;}
   try {
    bool ready=sample.Available&&!stale;
    var data=new Dictionary<string,object>{
     {"schemaVersion",1},{"inputSessionId",r.Session},{"sequence",++sequence},
     {"source","game-input"},{"touchSource","game-touch-state"},{"keySource","game-button-state"},
     {"sampleAgeMs",Age(sample,now)},{"sentAtUtcMs",(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalMilliseconds},
     {"players",Players(sample,ready)}
    };
    if(led0.Sequence!=0||led1.Sequence!=0)data["buttonLeds"]=new[]{led0.Json(),led1.Json()};
    if(capable){
     var events=new object[edges.Count];
     for(int i=0;i<events.Length;i++){var e=edges[i];events[i]=new{players=Players(e,e.Available),sampleAgeMs=Age(e,now),sampleSequence=e.Sequence};}
     data["events"]=events;data["reset"]=resync;data["dropped"]=dropped;
     data["sampleTicks"]=sample.Ticks;data["sendTicks"]=now;data["tickFrequency"]=Stopwatch.Frequency;
    }
    byte[] bytes=Encoding.UTF8.GetBytes(Json.Write(data));
    if(!r.Running)break;
    var req=(HttpWebRequest)WebRequest.Create(r.Endpoint);
    req.Method="POST";req.ContentType="application/json";req.Headers["X-Stellar-Token"]=r.Token;
    req.Timeout=200;req.ReadWriteTimeout=200;req.Proxy=null;req.KeepAlive=true;req.ContentLength=bytes.Length;
    using(var stream=req.GetRequestStream())stream.Write(bytes,0,bytes.Length);
    bool negotiated;
    using(var response=req.GetResponse())negotiated=response.Headers["X-Stellar-Input-Events"]=="1";
    reset=negotiated&&!capable;capable=negotiated;
    floor=Math.Max(floor,sample.Sequence);ledSequence0=led0.Sequence;ledSequence1=led1.Sequence;
   } catch {reset=true;Thread.Sleep(10);}
   previousDropped=dropped;lastSent=now;
  }
 }
 public static void Stop() {
  var r=Interlocked.Exchange(ref current,null);if(r!=null)r.Running=false;
 }
}
}
