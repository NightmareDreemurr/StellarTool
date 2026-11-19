using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace StellarTool.Mod {
 // Passive observer of AquaMai native output; never opens or writes a device.
 internal static class NativeButtonLeds {
  const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
  const string TypeName="AquaMai.Mods.GameSystem.MaimollerIO.Libs.MaimollerDeviceNative";
  const int StaleMs=1600; // native board keepalive is 500 ms
  sealed class Cell {
   internal long Version,Tick,Sequence;
   internal int A,B,C,D,E,F,G,H;
  }
  static readonly Cell[] Cells={new Cell(),new Cell()};
  static bool installed;
  internal struct Led {
   internal long Sequence;
   internal int A,B,C,D,E,F,G,H;
   internal double Age;
   internal object Json()=>Sequence==0?null:new{source="maimoller-native-hid-write",
    rgb=Hex(A)+Hex(B)+Hex(C)+Hex(D)+Hex(E)+Hex(F)+Hex(G)+Hex(H),ageMs=Age};
  }
  [ThreadStatic] static byte[] nativeReport;
  [ThreadStatic] static int nativePlayer;
  internal static void TryInstall(HarmonyLib.Harmony harmony) {
   if(installed)return;
   Type device=null;
   foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
    device=assembly.GetType(TypeName,false);if(device!=null)break;
   }
   if(device==null)return;
   var frame=device.GetMethod("WriteOutputReport",All,null,Type.EmptyTypes,null);
   var raw=device.Assembly.GetType("AquaMai.Mods.GameSystem.Lib.HidRawIO",false)?.GetMethod("Write",All);
   var args=raw?.GetParameters();
   if(frame?.ReturnType!=typeof(void)||raw?.ReturnType!=typeof(bool)||args?.Length!=2||args[1].ParameterType!=typeof(byte[])||
    device.GetField("_player",All)?.FieldType!=typeof(int)||device.GetField("_sendBuffer",All)?.FieldType!=typeof(byte[]))return;
   HarmonyMethod Hook(string name)=>new HarmonyMethod(typeof(NativeButtonLeds).GetMethod(name,All));
   harmony.Patch(raw,postfix:Hook(nameof(ObserveWrite)));
   harmony.Patch(frame,prefix:Hook(nameof(BeginReport)),finalizer:Hook(nameof(EndReport)));
   installed=true;
  }
  static void BeginReport(int ____player,byte[] ____sendBuffer){nativePlayer=____player;nativeReport=____sendBuffer;}
  static void EndReport(){nativeReport=null;}
  static void ObserveWrite(byte[] __1,bool __result){
   var player=nativePlayer;
   if(!__result||__1==null||!ReferenceEquals(__1,nativeReport)||__1.Length!=64||__1[0]!=1||(uint)player>=2)return;
   var c=Cells[player];var stamp=Stopwatch.GetTimestamp();
   var a=Rgb(__1,1);var b=Rgb(__1,4);var d=Rgb(__1,7);var e=Rgb(__1,10);
   var f=Rgb(__1,13);var g=Rgb(__1,16);var h=Rgb(__1,19);var i=Rgb(__1,22);
   Interlocked.Increment(ref c.Version);
   if(c.Tick==0||c.A!=a||c.B!=b||c.C!=d||c.D!=e||c.E!=f||c.F!=g||c.G!=h||c.H!=i)c.Sequence++;
   c.A=a;c.B=b;c.C=d;c.D=e;c.E=f;c.F=g;c.G=h;c.H=i;c.Tick=stamp;
   Interlocked.Increment(ref c.Version);
  }
  static int Rgb(byte[] data,int i)=>(data[i]<<16)|(data[i+1]<<8)|data[i+2];
  // Existing telemetry worker only. One bounded seqlock read does not touch the HID writer's lock.
  internal static Led Read(int player,long now){
   var c=Cells[player];
   for(int retry=0;retry<2;retry++) {
    long before=Volatile.Read(ref c.Version);if((before&1)!=0)continue;
    long stamp=c.Tick;
    var frame=new Led{Sequence=c.Sequence,A=c.A,B=c.B,C=c.C,D=c.D,E=c.E,F=c.F,G=c.G,H=c.H};
    if(before!=Interlocked.Read(ref c.Version))continue;
    frame.Age=Math.Max(0,(now-stamp)*1000.0/Stopwatch.Frequency);
    if(stamp!=0&&frame.Age<StaleMs)return frame;
    break;
   }
   return default(Led);
  }
  static string Hex(int rgb)=>rgb.ToString("x6",CultureInfo.InvariantCulture);
 }
}
