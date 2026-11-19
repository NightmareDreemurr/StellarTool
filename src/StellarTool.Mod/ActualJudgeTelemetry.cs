using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
namespace StellarTool.Mod {
public static class ActualJudgeTelemetry {
 static readonly CommittedJudgeBuffer Buffer=new CommittedJudgeBuffer();
 static Func<object,int,CommittedJudge> read;static Func<object> currentScore;static Func<float> gameClock;
 static Func<bool> autoPlay;static int suppress;static bool ready,enabled;static string session;static long epoch;
 static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static void Start(HarmonyLib.Harmony harmony,Func<string,Type> typeOf,int player){
  enabled=ready=false;
  var scoreType=typeOf("Manager.GameScoreList");var manager=typeOf("Manager.GamePlayManager");
  var notes=typeOf("Manager.NotesManager");var singleton=typeOf("MAI2.Util.Singleton`1").MakeGenericType(manager);
  read=ActualJudgeReader.Build(scoreType);
  var dynamicCurrent=new DynamicMethod("StellarCurrentHumanScore",typeof(object),Type.EmptyTypes,typeof(ActualJudgeTelemetry),true);
  var il=dynamicCurrent.GetILGenerator();il.Emit(OpCodes.Call,singleton.GetProperty("Instance",All).GetGetMethod(true));
  il.Emit(OpCodes.Ldc_I4,player);il.Emit(OpCodes.Ldc_I4_M1);il.Emit(OpCodes.Callvirt,manager.GetMethod("GetGameScore",All,null,new[]{typeof(int),typeof(int)},null));il.Emit(OpCodes.Ret);
  currentScore=(Func<object>)dynamicCurrent.CreateDelegate(typeof(Func<object>));
  gameClock=(Func<float>)Delegate.CreateDelegate(typeof(Func<float>),notes.GetMethod("GetCurrentMsec",All));
  var method=scoreType.GetMethod("SetResult",All);if(method==null||method.GetParameters().Length!=3)throw new InvalidOperationException("Unverified judge method");
  harmony.Patch(method,prefix:new HarmonyMethod(typeof(ActualJudgeTelemetry).GetMethod("Before",All)),postfix:new HarmonyMethod(typeof(ActualJudgeTelemetry).GetMethod("After",All)));
  autoPlay=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),typeOf("Manager.GameManager").GetMethod("IsAutoPlay",All,null,Type.EmptyTypes,null));
  foreach(var name in new[]{"FinishPlay","FinishPlayGhost"}){
   var finish=scoreType.GetMethod(name,All);if(finish!=null)harmony.Patch(finish,
    prefix:new HarmonyMethod(typeof(ActualJudgeTelemetry).GetMethod("FinishBegin",All)),
    finalizer:new HarmonyMethod(typeof(ActualJudgeTelemetry).GetMethod("FinishEnd",All)));
  }
  ready=true;
 }
 static void Before(object __instance,int index,out bool __state){
  __state=false;if(!ready||!enabled||suppress!=0)return;
  try{__state=ReferenceEquals(__instance,currentScore())&&!read(__instance,index).Judged;}catch{}
 }
 static void After(object __instance,int index,bool __state){
  if(!__state||!enabled||suppress!=0)return;
  try{Buffer.Commit(false,read(__instance,index),index,gameClock()/1000.0,autoPlay());}catch{}
 }
 static void FinishBegin(){suppress++;}
 static void FinishEnd(){if(suppress>0)suppress--;}
 // Called from Entry.OnUpdate after seek/session detection, never from HTTP worker.
 public static object Snapshot(string chartSession,bool playing){
  if(chartSession!=session||!playing){if(enabled||chartSession!=session){Buffer.Clear();epoch++;}session=chartSession;enabled=playing&&ready;}
  else enabled=playing&&ready;
  var recent=ready&&enabled?Buffer.Recent(gameClock()/1000.0):new ActualJudgeEvent[0];
  var events=new object[recent.Length];
  for(int i=0;i<events.Length;i++){var e=recent[i];events[i]=new{sequence=e.Sequence,noteIndex=e.NoteIndex,timing=e.Timing,scoreType=e.ScoreType,gameTime=e.GameTime,autoPlay=e.AutoPlay};}
  return new{schemaVersion=1,source="game-judgement",available=ready&&enabled,session,epoch,sequence=Buffer.Sequence,overwritten=Buffer.Overwritten,events};
 }
}
}
