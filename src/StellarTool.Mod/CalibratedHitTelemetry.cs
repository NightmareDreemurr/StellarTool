using System;
using System.Reflection;
using HarmonyLib;

namespace StellarTool.Mod {
// Independent of SetResult/final grades: HOLD samples are successful head hits.
// Do not use JudgeGrade.Initialize's msec (HOLD supplies a literal zero), nor
// recalculate from network arrival or subtract A/B again. The game already did it.
public static class CalibratedHitTelemetry {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static readonly CalibratedHitBuffer[] Buffers={new CalibratedHitBuffer(0),new CalibratedHitBuffer(1)};
 static Func<object,CalibratedNativeNote>[] readers;
 static Func<int,object> currentNotes;
 static Func<int,bool> scoreEnabled;
 static Func<float> gameClock;
 static Func<bool> autoPlay,advDemo;
 static bool ready,started;
 static int suppressed;

 public static void Start(HarmonyLib.Harmony harmony,Func<string,Type> typeOf) {
  if(started)throw new InvalidOperationException("Calibrated hit adapter already started");
  ready=false;
  Type note=Required(typeOf,"Monitor.NoteBase"),hold=Required(typeOf,"Monitor.HoldNote"),
   breakHold=Required(typeOf,"Monitor.BreakHoldNote"),touch=Required(typeOf,"Monitor.TouchNoteB"),
   touchHold=Required(typeOf,"Monitor.TouchHoldC"),breakNote=Required(typeOf,"Monitor.BreakNote"),
   notes=Required(typeOf,"Manager.NotesManager"),manager=Required(typeOf,"Manager.GamePlayManager"),
   score=Required(typeOf,"Manager.GameScoreList"),game=Required(typeOf,"Manager.GameManager");
  VerifyEnum(CalibratedHitReader.Field(note,"JudgeResult").FieldType,new[]{"TooFast","FastGood","FastGreat3rd","FastGreat2nd","FastGreat","FastPerfect2nd","FastPerfect","Critical","LatePerfect","LatePerfect2nd","LateGreat","LateGreat2nd","LateGreat3rd","LateGood","TooLate","End"});
  VerifyEnum(CalibratedHitReader.Field(note,"JudgeType").FieldType,new[]{"Tap","HoldOut","SlideOut","Touch","ExTap","Break","End"});
  readers=new[]{CalibratedHitReader.Build(note,false,breakNote),CalibratedHitReader.Build(hold,true,breakNote),
   CalibratedHitReader.Build(breakHold,true,breakNote),CalibratedHitReader.Build(touch,false,breakNote),CalibratedHitReader.Build(touchHold,true,breakNote)};
  currentNotes=CalibratedHitReader.CurrentNotes(notes);
  scoreEnabled=CalibratedHitReader.CurrentScoreEnabled(manager,Required(typeOf,"MAI2.Util.Singleton`1"),score);
  gameClock=(Func<float>)Delegate.CreateDelegate(typeof(Func<float>),notes.GetMethod("GetCurrentMsec",All,null,Type.EmptyTypes,null));
  autoPlay=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),game.GetMethod("IsAutoPlay",All,null,Type.EmptyTypes,null));
  advDemo=(Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>),game.GetProperty("IsAdvDemo",All).GetGetMethod(true));
  var targets=new[]{JudgeMethod(note,"Judge"),JudgeMethod(hold,"JudgeHoldHead"),JudgeMethod(breakHold,"JudgeHoldHead"),JudgeMethod(touch,"Judge"),JudgeMethod(touchHold,"JudgeHoldHead")};
  // Validate every symbol before applying any hooks. A partial patch stays inert
  // if the loader refuses one; no game input, score or configuration is modified.
  var chain=touch.GetMethod("SetChainPlayResult",All|BindingFlags.DeclaredOnly);
  var finish=score.GetMethod("FinishPlay",All);var ghostFinish=score.GetMethod("FinishPlayGhost",All);
  if(chain==null||finish==null||ghostFinish==null)throw new InvalidOperationException("Unverified synthetic-result guards");
  for(int i=0;i<targets.Length;i++)harmony.Patch(targets[i],prefix:Patch("Before"+i),postfix:Patch("After"));
  // Chained touch awards are game-generated; do not present them as an additional
  // physical hit offset. Optional touch samples cover direct successful hits only.
  foreach(var method in new[]{chain,finish,ghostFinish})harmony.Patch(method,prefix:Patch("SuppressBegin"),finalizer:Patch("SuppressEnd"));
  started=ready=true;
 }
 static Type Required(Func<string,Type> types,string name){var t=types(name);if(t==null)throw new InvalidOperationException("Missing game type: "+name);return t;}
 static void VerifyEnum(Type type,string[] names) {
  if(!type.IsEnum||Enum.GetUnderlyingType(type)!=typeof(int))throw new InvalidOperationException("Unsupported native timing enum");
  for(int i=0;i<names.Length;i++)if(!Enum.IsDefined(type,names[i])||Convert.ToInt32(Enum.Parse(type,names[i]))!=i)throw new InvalidOperationException("Unsupported native timing map");
 }
 static MethodInfo JudgeMethod(Type t,string name) {
  var method=t.GetMethod(name,All|BindingFlags.DeclaredOnly,null,Type.EmptyTypes,null);
  if(method==null||method.IsStatic||method.ReturnType!=typeof(bool))throw new InvalidOperationException("Unverified hit hook: "+t.FullName+"."+name);
  return method;
 }
 static HarmonyMethod Patch(string name){return new HarmonyMethod(typeof(CalibratedHitTelemetry).GetMethod(name,All)){priority=Priority.Last};}
 public struct Probe {public bool Armed;public int Reader,Player,Index;}
 static void Before0(object __instance,out Probe __state){Before(__instance,0,out __state);}
 static void Before1(object __instance,out Probe __state){Before(__instance,1,out __state);}
 static void Before2(object __instance,out Probe __state){Before(__instance,2,out __state);}
 static void Before3(object __instance,out Probe __state){Before(__instance,3,out __state);}
 static void Before4(object __instance,out Probe __state){Before(__instance,4,out __state);}
 static void Before(object instance,int reader,out Probe state) {
  state=default(Probe);if(!ready||suppressed!=0)return;
  try {
   if(autoPlay()||advDemo())return;
   var n=readers[reader](instance);bool head=reader==1||reader==2||reader==4;
   if(n.Player<0||n.Player>1||n.Timing!=15||!Buffers[n.Player].CanProbe(n.Index,head,gameClock()/1000.0)||
      !ReferenceEquals(n.NotesManager,currentNotes(n.Player))||!scoreEnabled(n.Player))return;
   state=new Probe{Armed=true,Reader=reader,Player=n.Player,Index=n.Index};
  }catch{ready=false;} // Fail closed, with no logging/I/O in the game callback.
 }
 static void After(object __instance,bool __result,Probe __state) {
  if(!ready||!__state.Armed||!__result||suppressed!=0)return;
  try {
   if(autoPlay()||advDemo()||!scoreEnabled(__state.Player))return;
   var n=readers[__state.Reader](__instance);
   if(n.Player!=__state.Player||n.Index!=__state.Index||n.Timing<1||n.Timing>13||
      !ReferenceEquals(n.NotesManager,currentNotes(n.Player)))return;
   int reader=__state.Reader;
   if(reader==0?(n.JudgeType!=0&&n.JudgeType!=4&&n.JudgeType!=5):
      reader==1?(n.JudgeType!=0&&n.JudgeType!=4):reader==2?(n.JudgeType!=5&&n.JudgeType!=4):n.JudgeType!=3)return;
   CalibratedHitGroup group=n.Ex||n.JudgeType==4?CalibratedHitGroup.Ex:reader==3?CalibratedHitGroup.Touch:reader==4?CalibratedHitGroup.TouchHold:CalibratedHitGroup.Standard;
   CalibratedHitKind kind=reader==0?(n.IsBreak?CalibratedHitKind.Break:CalibratedHitKind.Tap):reader==1?CalibratedHitKind.Hold:reader==2?CalibratedHitKind.BreakHold:reader==3?CalibratedHitKind.Touch:CalibratedHitKind.TouchHold;
   Buffers[n.Player].TryCommit(new CalibratedHitSample{Player=n.Player,NoteIndex=n.Index,Timing=n.Timing,Group=group,Kind=kind,
    HoldHead=reader==1||reader==2||reader==4,OffsetMs=n.OffsetMs,GameTime=gameClock()/1000.0});
  }catch{ready=false;}
 }
 static void SuppressBegin(){suppressed++;}
 static void SuppressEnd(){if(suppressed>0)suppressed--;}

 // Main game thread only, after the existing chart/session/seek detection. HTTP
 // and serialization workers receive only this detached immutable-by-convention DTO.
 public static HitOffsetSnapshot Snapshot(int player,string session,bool active,int noteCount,bool retainResult) {
  if(player<0||player>1)throw new ArgumentOutOfRangeException("player");
  double now=ready?gameClock()/1000.0:double.NaN;
  Buffers[player].SetContext(session,active&&ready,noteCount,retainResult,now);
  return Buffers[player].Snapshot(ready,now);
 }
}
}
