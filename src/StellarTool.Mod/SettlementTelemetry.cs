using System;
using System.Reflection;
using HarmonyLib;
namespace StellarTool.Mod {
public static class SettlementTelemetry {
 static readonly SettlementState State=new SettlementState();
 static Func<object,string,object> read;
 static Type gameManager;
 static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static string Phase {get{return State.Phase;}}
 public static long CreditEpoch {get{return State.CreditEpoch;}}
 // Travel-partner selection is also a per-player subsequence of MusicSelect.
 public static string GetPhase(object selectProcess,int player){
  if(State.Phase!="none"||selectProcess==null||player<0)return State.Phase;
  try{
   var subs=read(selectProcess,"_currentPlayerSubSequence") as Array;
   return subs!=null&&subs.Rank==1&&player<subs.Length?State.WithMusicSelection(Convert.ToString(subs.GetValue(player))):State.Phase;
  }catch{return State.Phase;}
 }
 public static int ResultTrackNumber {get{return State.ResultTrackNumber;}}
 public static void Start(HarmonyLib.Harmony harmony,Func<string,Type> typeOf,Func<object,string,object> get){
  read=get;gameManager=typeOf("Manager.GameManager");State.Reset();
  // Resolve from the same verified game assembly, without scanning unrelated DLLs.
  if(gameManager==null)return;
  foreach(var name in new[]{
   "Process.MusicSelectProcess","Process.GameProcess","Process.ResultProcess","Process.PhotoEditProcess",
   "Process.MapResultProcess","Process.NextTrackProcess","Process.UnlockProcess","Process.UnlockMusicProcess",
   "Process.GetPresentProcess","Process.GetMusicProcess","Process.DataSaveProcess","Process.GameOverProcess",
   "Process.ContinueProcess","Process.AddFriendProcess","Process.EntryProcess","Process.AdvertiseProcess",
   "Process.FriendSeasonRanking.FriendSeasonRankingProcess","Process.CourseResult.CourseResultProcess",
   "FestaPlayResultProcess","FestaFinalResultProcess","CircleFinalResultProcess","CircleRewardProcess"}){
   var type=typeOf(name);if(type==null)continue;
   Patch(harmony,type,"OnStart","Started");Patch(harmony,type,"OnRelease","Released");
  }
  Patch(harmony,gameManager,"Clear","Reset");
  var manager=typeOf("Manager.GamePlayManager");if(manager!=null)Patch(harmony,manager,"ClaerLog","Reset");
 }
 static void Patch(HarmonyLib.Harmony harmony,Type type,string name,string callback){
  var method=type.GetMethod(name,All|BindingFlags.DeclaredOnly, null,Type.EmptyTypes,null);
  if(method!=null)harmony.Patch(method,postfix:new HarmonyMethod(typeof(SettlementTelemetry).GetMethod(callback,All)){priority=Priority.First});
 }
 static void Started(object __instance){
  int track=0;try{track=Convert.ToInt32(read(gameManager,"MusicTrackNumber"));}catch{}
  var name=__instance.GetType().Name;
  bool visible=true;
  try{
   if(name=="CharacterSelectProces"){visible=!Equals(read(__instance,"_isSkipProcess"),true);}
   if(name=="RegionalSelectProcess"){var step=Convert.ToString(read(__instance,"_state"));visible=step!="ToRelease"&&step!="Release";}
  }catch{} // Optional substate only makes a known auto-skip invisible.
  State.Started(name,__instance,track,visible);
 }
 static void Released(object __instance){State.Released(__instance);}
 static void Reset(){State.Reset();}
 public static bool TryReadPhoto(int player,out PhotoTrackSelection selection){
  selection=null;if(State.Phase!="photo")return false;
  try{return SettlementSelection.TryRead(State.PhotoProcess,player,read,out selection);}
  catch{return false;} // Missing/mismatched arrays never fall back to the last song.
 }
}
}
