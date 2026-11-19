using System.Diagnostics;
using System.Reflection;
using StellarTool.Mod;

int checks=0;
void Check(bool correct){if(!correct)throw new Exception("NativeButtonLeds assertion "+(checks+1));checks++;}
var methods=typeof(NativeButtonLeds);
var begin=methods.GetMethod("BeginReport",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<Action<int,byte[]>>();
var observe=methods.GetMethod("ObserveWrite",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<Action<byte[],bool>>();
var end=methods.GetMethod("EndReport",BindingFlags.Static|BindingFlags.NonPublic)!.CreateDelegate<Action>();
var report=new byte[64];report[0]=1;
for(int j=1;j<=24;j++)report[j]=(byte)j;
long Now()=>Stopwatch.GetTimestamp();
var rgb="0102030405060708090a0b0c0d0e0f101112131415161718";
Check(NativeButtonLeds.Read(0,Now()).Sequence==0);
begin(0,report);observe(report,false);Check(NativeButtonLeds.Read(0,Now()).Sequence==0);
observe((byte[])report.Clone(),true);Check(NativeButtonLeds.Read(0,Now()).Sequence==0);
observe(report,true);var first=NativeButtonLeds.Read(0,Now());
Check(first.Sequence==1 && first.A==0x010203 && first.H==0x161718);
Check(first.Json()!.ToString()!.Contains(rgb));
Thread.Sleep(20);
observe(report,true);Check(NativeButtonLeds.Read(0,Now()).Sequence==1 && NativeButtonLeds.Read(0,Now()).Age<20);
end();report[1]=255;observe(report,true);Check(NativeButtonLeds.Read(0,Now()).A==0x010203);
begin(1,report);observe(report,true);end();
Check(NativeButtonLeds.Read(1,Now()).A==0xff0203 && NativeButtonLeds.Read(0,Now()).A==0x010203);
Check(NativeButtonLeds.Read(1,Now()+2*Stopwatch.Frequency).Sequence==0);
begin(1,report);for(int i=1;i<=24;i++)report[i]=0;observe(report,true);
Check(NativeButtonLeds.Read(1,Now()).Sequence==2 && NativeButtonLeds.Read(1,Now()).A==0);
var baseline=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++){begin(1,report);observe(report,true);end();}
Check(GC.GetAllocatedBytesForCurrentThread()==baseline);
Console.WriteLine($"{checks} native LED callback assertions passed; 10k observer calls allocate 0 bytes.");

// Harness tests the exact callback logic, not the AquaMai/Mono Harmony patch wiring.
namespace HarmonyLib {
 public sealed class HarmonyMethod { public HarmonyMethod(MethodInfo method){} }
 public sealed class Harmony {
  public void Patch(MethodInfo method,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){}
 }
}
