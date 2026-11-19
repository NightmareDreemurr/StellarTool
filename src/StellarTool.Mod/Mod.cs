using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;

using HarmonyLib;
using MelonLoader;
[assembly: MelonInfo(typeof(StellarTool.Mod.Entry), "Stellar Tool", "0.1.0", "Stellar")]
namespace StellarTool.Mod {
// Reflection contract is evidenced in both recovered NotesManager/NotesReader assemblies.
// Built against verified MelonLoader 0.6.4; game-runtime coverage is recorded separately.
public sealed class Entry : MelonMod {
 static readonly object Gate=new object(); static Dictionary<string,object> pending;static volatile bool running;static object selectProcess;static object activeProcess; static string state="idle";static int epoch;static bool preparing;static Entry current;
 int resultEpoch=-1,resultPreviousId=-1,resultPreviousDifficulty=-1;object resultSong;Dictionary<string,object> resultPrevious;
 long settlementEpoch=-1;readonly Dictionary<int,Tuple<int,int,Dictionary<string,object>>> trackBest=new Dictionary<int,Tuple<int,int,Dictionary<string,object>>>();
 readonly Dictionary<int,Tuple<int,int,object>> trackHitTiming=new Dictionary<int,Tuple<int,int,object>>();
 string hitSession; long hitCredit=-1; int hitTrack,hitMusic,hitDifficulty,hitNoteCount;
 object photoSong,photoScore;int photoTrack=-1,photoIndex=-1;
 object lastSpike;long stageTick;string slowStage;double slowStageMs;ConstructorInfo rateCtor4,rateCtor5;DateTime nextMusic;double? musicDuration;string durationSession;DateTime nextSong;string songKey;IEnumerator chartRoutine;double captureMs,sampleMs,samplePeakMs;object lastSong;string endpoint,token;int player; DateTime next;Thread sender; object cachedReader;object chart;string session=Guid.NewGuid().ToString();float lastTime;int readEpoch=-1;int readCount=-1;string transportError;DateTime nextError;
 static readonly BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 static readonly Dictionary<Tuple<Type,string>,MemberInfo> Members=new Dictionary<Tuple<Type,string>,MemberInfo>();
 static readonly Dictionary<Tuple<Type,string,int>,MethodInfo> Methods=new Dictionary<Tuple<Type,string,int>,MethodInfo>();
 static readonly Dictionary<string,Type> Types=new Dictionary<string,Type>();
 static object Get(object o,string n){if(o==null)return null;var t=o as Type??o.GetType();var key=Tuple.Create(t,n);MemberInfo m;if(!Members.TryGetValue(key,out m)){m=(MemberInfo)t.GetField(n,All)??t.GetProperty(n,All);Members[key]=m;}var f=m as FieldInfo;return f!=null?f.GetValue(o is Type?null:o):(m as PropertyInfo)?.GetValue(o is Type?null:o,null);}
 static object Call(object o,string n,params object[] args){if(o==null)return null;var t=o as Type??o.GetType();var key=Tuple.Create(t,n,args.Length);MethodInfo m;if(!Methods.TryGetValue(key,out m)){m=t.GetMethods(All).First(x=>x.Name==n&&x.GetParameters().Length==args.Length);Methods[key]=m;}return m.Invoke(o is Type?null:o,args);}
 static Type TypeOf(string n){Type t;if(Types.TryGetValue(n,out t))return t;t=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(n)).FirstOrDefault(x=>x!=null);if(t!=null)Types[n]=t;return t;}
 static double Number(object o)=>o==null?0:Convert.ToDouble(o,System.Globalization.CultureInfo.InvariantCulture);
 // Manager.MusicClearrankID.Begin and Rank_D are aliases for zero.
 // Enum.ToString may print Begin depending on runtime. Use stable real ranks.
 static readonly string[] ClearRankNames={"Rank_D","Rank_C","Rank_B","Rank_BB","Rank_BBB","Rank_A","Rank_AA","Rank_AAA","Rank_S","Rank_SP","Rank_SS","Rank_SSP","Rank_SSS","Rank_SSSP"};
 static string ClearRank(object rank){
  if(rank==null)return null;
  var value=Convert.ToInt32(rank);
  return value>=0&&value<ClearRankNames.Length?ClearRankNames[value]:null;
 }
 static double Seconds(object notesTime)=>Number(Get(notesTime,"msec"))/1000;
 void MarkStage(string name){var now=System.Diagnostics.Stopwatch.GetTimestamp();var ms=(now-stageTick)*1000.0/System.Diagnostics.Stopwatch.Frequency;if(ms>slowStageMs){slowStageMs=ms;slowStage=name;}stageTick=now;}
 public override void OnInitializeMelon(){current=this;
  var game=TypeOf("Manager.NotesManager");if(game==null){LoggerInstance.Error("Assembly-CSharp NotesManager unavailable; disabled");return;}string digest;using(var sha=SHA256.Create())using(var stream=File.OpenRead(game.Assembly.Location))digest=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(digest!="c291f0febe70cc24a679683ef07c059c1f856c052e1f6be7a22b55c6b16a94eb"&&digest!="dc0e46a4133d4ee4781ba6deb2fe30063dd3f82ee91c31a5a8a7e61e644d9446"){LoggerInstance.Error("Unverified game assembly "+digest+"; disabled");return;}

  var file=Path.Combine("UserData","StellarTool","bridge.cfg");if(!File.Exists(file)){LoggerInstance.Error("Missing "+file+"; bridge disabled. See README.");return;}
  var cfg=File.ReadAllLines(file).Where(x=>x.Contains("=")).Select(x=>x.Split(new[]{'='},2)).ToDictionary(x=>x[0].Trim(),x=>x[1].Trim());endpoint=cfg["endpoint"];token=cfg["token"];player=cfg.ContainsKey("player")?int.Parse(cfg["player"]):0;if(player<0||player>1)throw new InvalidOperationException("player must be 0 or 1");var uri=new Uri(endpoint);if(!uri.IsLoopback||uri.Scheme!="http")throw new InvalidOperationException("Bridge must use loopback HTTP");
  var h=new HarmonyLib.Harmony("stellar.tool.readonly");
  foreach(var name in new[]{"Process.AdvertiseProcess","Process.EntryProcess","Process.UserDataDLProcess","Process.PlInformationProcess","Process.Information.InformationProcess","Process.ModeSelect.ModeSelectProcess","Process.MusicSelectProcess","Process.GameProcess","Process.ResultProcess"}) {
   var t=TypeOf(name);if(t==null){LoggerInstance.Warning("Unverified process unavailable: "+name);continue;}
   Patch(h,t,"OnStart",nameof(Start));Patch(h,t,"OnRelease",nameof(Release));
   if(name=="Process.MusicSelectProcess")Patch(h,t,"OnGameStart",nameof(Prepare));
   if(name=="Process.ResultProcess"){
    var m=t.GetMethod("OnStart",All);
    if(m!=null&&m.DeclaringType==t)h.Patch(m,prefix:new HarmonyMethod(typeof(Entry).GetMethod(nameof(BeforeResult),All)){priority=HarmonyLib.Priority.First});
   }
  }
  var rateType=TypeOf("Manager.UserDatas.UserRate");rateCtor4=rateType.GetConstructor(new[]{typeof(int),typeof(int),typeof(uint),typeof(uint)});var combo=TypeOf("DB.PlayComboflagID");if(combo!=null)rateCtor5=rateType.GetConstructor(new[]{typeof(int),typeof(int),typeof(uint),typeof(uint),combo});
  try {SettlementTelemetry.Start(h,TypeOf,Get);} catch(Exception ex) {LoggerInstance.Warning("Settlement telemetry unavailable: "+ex.GetBaseException().Message);}
  try {ActualJudgeTelemetry.Start(h,TypeOf,player);} catch(Exception ex) {LoggerInstance.Warning("Actual judgement telemetry unavailable: "+ex.GetBaseException().Message);}
  try {CalibratedHitTelemetry.Start(h,TypeOf);} catch(Exception ex) {LoggerInstance.Warning("Calibrated hit timing unavailable: "+ex.GetBaseException().Message);}
  try {InputTelemetry.Start(game.Assembly,h,endpoint,token);} catch(Exception ex) {LoggerInstance.Warning("Input telemetry unavailable: "+ex.GetBaseException().Message);}
  running=true;sender=new Thread(SendLoop){IsBackground=true,Name="Stellar HTTP publisher"};sender.Start();
 }
 static void Patch(HarmonyLib.Harmony h,Type t,string name,string cb){var m=t.GetMethod(name,All);if(m!=null&&m.DeclaringType==t)h.Patch(m,postfix:new HarmonyMethod(typeof(Entry).GetMethod(cb,All)));}
 static void Prepare(){preparing=true;if(current!=null){current.next=DateTime.MinValue;current.OnUpdate();}}
 static void Start(object __instance){
  activeProcess=__instance;epoch++;selectProcess=null;
  switch(__instance.GetType().Name){
   case "AdvertiseProcess": state="idle";preparing=false;break;
   case "EntryProcess": state="entry";preparing=false;break;
   case "UserDataDLProcess":
   case "PlInformationProcess":
   case "InformationProcess": state="login";preparing=false;break;
   case "ModeSelectProcess": state="mode";preparing=false;break;
   case "MusicSelectProcess": state="select";selectProcess=__instance;preparing=false;break;
   case "ResultProcess": state="result";preparing=false;break;
   case "GameProcess": state="playing";break;
  }
  if(current!=null)current.next=DateTime.MinValue;
 }
 static void Release(object __instance){
  if(!ReferenceEquals(activeProcess,__instance))return;
  if(state=="entry")state=EntryStatus(__instance);
  else if(state=="mode")state="select";
  else if(state!="login")state=preparing?"preparing":"idle";
  activeProcess=null;selectProcess=null;
 }
 static string EntryStatus(object process){
  if(process==null)return "idle";
  try{
   var context=Get(process,"_context");if(context==null)return "idle";
   var args=new object[]{null,null};
   Call(context,"GetCurrentStateObj",args);
   var phase=Convert.ToString(args[0]);var info=args[1];
   // ConfirmEntry itself is the idle scan prompt; only a read in progress or
   // a post-card screen means login. Do not read or publish Aime identifiers.
   if(phase=="ConfirmEntry"){
    var reader=Convert.ToString(Get(Get(info,"_tryAime"),"Mode"));
    var ui=Convert.ToString(Get(Get(info,"_mode"),"Mode"));
    return reader=="GetResult"||ui=="ResultNewAime"||ui=="ResultNewFelica"||ui.StartsWith("NewFelica",StringComparison.Ordinal)||ui=="NewAimeDispFelicaSiteAndQR"?"login":"idle";
   }
   switch(phase){
    case "ConfirmPlay":case "ConfirmNewUser":case "ConfirmContinue":case "ConfirmContinueOne":
    case "ConfirmFreedom":case "ConfirmFreedomOne":case "ConfirmSinglePlayState":case "DoneEntry":return "login";
   }
  }catch{}
  return "idle";
 }
 static object InstanceOf(string name){
  var t=TypeOf(name);var generic=TypeOf("MAI2.Util.Singleton`1");
  return t==null||generic==null?null:generic.MakeGenericType(t).GetProperty("Instance",BindingFlags.Public|BindingFlags.Static)?.GetValue(null,null);
 }
 object SingleRate(int id,int difficulty,object score){
  if(score==null||difficulty<0||difficulty>4)return null;
  var ach=Get(score,"achivement");if(ach==null)return null;
  var combo=Get(score,"combo");
  if(rateCtor5!=null&&combo!=null){
   var enumType=rateCtor5.GetParameters()[4].ParameterType;
   return Get(rateCtor5.Invoke(new object[]{id,difficulty,Convert.ToUInt32(ach),0u,Enum.ToObject(enumType,Convert.ToInt32(combo))}),"SingleRate");
  }
  return rateCtor4==null?null:Get(rateCtor4.Invoke(new object[]{id,difficulty,Convert.ToUInt32(ach),0u}),"SingleRate");
 }
 // OnStart builds the current score and can update ScoreDic before returning.
 // Capture a primitive-only copy of the same song/difficulty PB before that.
 static void BeforeResult(){
  var self=current;if(self==null)return;
  self.resultPrevious=null;self.resultPreviousId=self.resultPreviousDifficulty=-1;
  try{
   var game=TypeOf("Manager.GameManager");
   var ids=Get(game,"SelectMusicID") as IList;var difficulties=Get(game,"SelectDifficultyID") as IList;
   if(ids==null||ids.Count<1||difficulties==null||difficulties.Count<=self.player)return;
   var id=Convert.ToInt32(ids[0]);var diff=Convert.ToInt32(difficulties[self.player]);
   var user=Call(InstanceOf("Manager.UserDataManager"),"GetUserData",self.player);
   var scores=Get(user,"ScoreDic") as IList;
   if(id<=0||scores==null||diff<0||diff>=scores.Count)return;
   var table=scores[diff] as IDictionary;
   var best=table!=null&&table.Contains(id)?table[id]:null;
   if(best==null)return;
   self.resultPrevious=new Dictionary<string,object>{{"achievement",Number(Get(best,"achivement"))/10000.0},{"rating",self.SingleRate(id,diff,best)},{"rank",ClearRank(Get(best,"scoreRank"))},{"dxScore",Get(best,"deluxscore")},{"combo",Get(best,"combo")?.ToString()},{"sync",Get(best,"sync")?.ToString()}};
   self.resultPreviousId=id;self.resultPreviousDifficulty=diff;
  }catch(Exception e){self.transportError="Previous best unavailable: "+e.GetBaseException().Message;}
 }
 public override void OnUpdate(){if(!running)return;
 if(chartRoutine!=null){var budget=System.Diagnostics.Stopwatch.StartNew();try{do{if(!chartRoutine.MoveNext()){chartRoutine=null;next=DateTime.MinValue;break;}}while(budget.Elapsed.TotalMilliseconds<1.5);}catch(Exception ex){chartRoutine=null;transportError="Chart capture unavailable: "+ex.GetBaseException().Message;}captureMs=budget.Elapsed.TotalMilliseconds;}
 if(DateTime.UtcNow<next)return;next=DateTime.UtcNow.AddMilliseconds(50);if(transportError!=null&&DateTime.UtcNow>=nextError){LoggerInstance.Warning(transportError);transportError=null;nextError=DateTime.UtcNow.AddSeconds(5);}var sampleWatch=System.Diagnostics.Stopwatch.StartNew();stageTick=System.Diagnostics.Stopwatch.GetTimestamp();slowStageMs=0;slowStage="";var gcBefore=GC.CollectionCount(0);try{
  var nm=TypeOf("Manager.NotesManager");if(nm==null)return;var time=(float)(Number(Call(nm,"GetCurrentMsec"))/1000);var instance=Call(nm,"Instance",player);var playing=Convert.ToBoolean(Call(instance,"IsPlaying"));var watch=Get(nm,"_stopwatch");var clockRunning=watch!=null&&Convert.ToBoolean(Get(watch,"IsRunning"));
  MarkStage("clock");object song=null,category=null;bool selectingCategory=false;
  var settlement=SettlementTelemetry.GetPhase(selectProcess,player);
  if(settlementEpoch!=SettlementTelemetry.CreditEpoch){settlementEpoch=SettlementTelemetry.CreditEpoch;trackBest.Clear();trackHitTiming.Clear();photoTrack=-1;photoIndex=-1;photoScore=null;photoSong=null;}
  if(settlement!="photo"){photoTrack=-1;photoIndex=-1;photoScore=null;photoSong=null;}
  if(settlement=="none"&&state=="select"&&selectProcess!=null&&!preparing){
   try{selectingCategory=GenreTelemetry.IsCategory(selectProcess,player);if(selectingCategory)category=GenreTelemetry.Read(selectProcess,player);}catch(Exception e){transportError="Category unavailable: "+e.GetBaseException().Message;}
  }
  if(settlement=="none"&&state=="select"&&selectProcess!=null&&!selectingCategory){var selected=Call(selectProcess,"GetMusic",0);var md=Get(selected,"MusicData");if(md!=null){var id=Convert.ToInt32(Get(Get(md,"name"),"id"));var diffArray=Get(selectProcess,"CurrentDifficulty") as Array;var difficulty=diffArray==null?0:Convert.ToInt32(diffArray.GetValue(player));if(id>=100000&&Get(selected,"Difficulty") is int utageSlot)difficulty=utageSlot;if(preparing){var finalDiff=Get(TypeOf("Manager.GameManager"),"SelectDifficultyID") as Array;if(finalDiff!=null) difficulty=Convert.ToInt32(finalDiff.GetValue(player));}var sub=Get(selectProcess,"_currentPlayerSubSequence") as Array;var subName=sub==null?"Music":sub.GetValue(player).ToString();var isDifficulty=preparing||subName=="Difficulty"||subName=="UtageDifficulty"||subName=="Menu";var key=id+":"+difficulty+":"+isDifficulty+":"+epoch;if(key==songKey&&DateTime.UtcNow<nextSong&&lastSong!=null){song=lastSong;}else{songKey=key;nextSong=DateTime.UtcNow.AddSeconds(5);var levels=new List<object>();foreach(var n in (IEnumerable)Get(selected,"ScoreData"))levels.Add(new{value=Number(Get(n,"level"))+Number(Get(n,"levelDecimal"))/10,enabled=Get(n,"isEnable")});var score=Call(selectProcess,"GetUserScore",player,difficulty,id);var scoreData=Get(selected,"ScoreData") as IList;var dxMax=scoreData!=null&&difficulty>=0&&difficulty<scoreData.Count?(int)(Number(Get(scoreData[difficulty],"maxNotes"))*3):0;var dxPercent=score==null||dxMax<=0?(double?)null:Number(Get(score,"deluxscore"))*100/dxMax;var dxRank=dxPercent.HasValue?Convert.ToInt32(Call(TypeOf("Manager.GameManager"),"GetDeluxcoreRank",(int)dxPercent.Value)):(int?)null;var rate=SingleRate(id,difficulty,score);song=new Dictionary<string,object>{{"cover","/assets/jackets/"+Path.GetFileName(Convert.ToString(Get(md,"jacketFile")).Replace('\\','/'))},{"title",Get(Get(md,"name"),"str")},{"artist",Get(Get(md,"artistName"),"str")},{"id",id},{"difficulty",difficulty},{"selection",isDifficulty?"difficulty":"song"},{"chartType",Get(selectProcess,"ScoreType").ToString()},{"utageKanji",Get(md,"utageKanjiName")},{"levels",levels},{"utageConstant",id>=100000?ChartConstant(ChartSlot(scoreData,difficulty)):null},{"achievement",score==null?null:(object)(Number(Get(score,"achivement"))/10000)},{"rating",rate},{"rank",ClearRank(Get(score,"scoreRank"))},{"dxScore",score==null?null:Get(score,"deluxscore")},{"dxMax",dxMax>0?(object)dxMax:null},{"dxPercent",dxPercent},{"dxRank",dxRank},{"combo",Get(score,"combo")?.ToString()},{"sync",Get(score,"sync")?.ToString()},{"resourceStatus","jacket/SD-DX/DXStar mapping pending"}};}}}
  if(settlement=="photo"){
   try{song=ReadPhoto();}catch(Exception e){transportError="Selected photo result unavailable: "+e.GetBaseException().Message;}
  }else if(settlement=="settling"){song=null;}
  else if(state=="result"){if(resultEpoch!=epoch){resultEpoch=epoch;resultSong=null;try{resultSong=ReadResult(activeProcess);}catch(Exception e){transportError="Song result unavailable: "+e.GetBaseException().Message;}}song=resultSong;}else if(song!=null)lastSong=song;else if(preparing) song=lastSong;
  MarkStage("selection-card");
  if(state=="playing"&&settlement=="none") {var reader=Call(instance,"getReader");var count=((ICollection)Call(reader,"GetNoteList")).Count;if(count!=readCount||readEpoch!=epoch||!ReferenceEquals(reader,cachedReader)||time<lastTime-0.1f){cachedReader=reader;readEpoch=epoch;readCount=count;session=Guid.NewGuid().ToString();chart=null;chartRoutine=ReadChart(reader);}lastTime=time;}else{cachedReader=null;chart=null;chartRoutine=null;}
  object hitOffsets=null;
  if(state=="playing"&&settlement=="none"){
   try{
    if(hitSession!=session||hitCredit!=settlementEpoch){
     var game=TypeOf("Manager.GameManager");var ids=Get(game,"SelectMusicID") as IList;var diffs=Get(game,"SelectDifficultyID") as IList;
     hitTrack=Convert.ToInt32(Get(game,"MusicTrackNumber"));
     hitMusic=ids!=null&&ids.Count>player?Convert.ToInt32(ids[player]):-1;
     hitDifficulty=diffs!=null&&diffs.Count>player?Convert.ToInt32(diffs[player]):-1;
     hitSession=session;hitCredit=settlementEpoch;hitNoteCount=readCount;
    }
    hitOffsets=CalibratedHitTelemetry.Snapshot(player,session,true,Math.Max(0,hitNoteCount),false);
   }catch(Exception ex){transportError="Calibrated hit timing unavailable: "+ex.GetBaseException().Message;}
  }
  MarkStage("chart-lifecycle");if(durationSession!=session){durationSession=session;musicDuration=null;nextMusic=DateTime.MinValue;}
  if(state=="playing"&&playing&&!musicDuration.HasValue&&DateTime.UtcNow>=nextMusic){nextMusic=DateTime.UtcNow.AddSeconds(1);try{var soundType=TypeOf("Manager.SoundCtrl");var singleton=TypeOf("MAI2.Util.Singleton`1").MakeGenericType(soundType);var sound=singleton.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);var acb=Call(sound,"GetCueSheet",1);if(acb!=null){var method=acb.GetType().GetMethods(All).First(m=>m.Name=="GetCueInfo"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(int));var args=new object[]{0,null};if((bool)method.Invoke(acb,args)){var length=Number(Get(args[1],"length"));if(length>0)musicDuration=length/1000;}}}catch(Exception ex){transportError="Music duration unavailable: "+ex.GetBaseException().Message;}}
  MarkStage("audio-length");var outputState=settlement!="none"?settlement:state=="playing"&&!playing?"preparing":state=="entry"?EntryStatus(activeProcess):selectingCategory?"category":state; if(outputState=="playing")preparing=false;
  var snapshot=new Dictionary<string,object>{{"state",outputState},{"player",player},{"time",time},{"paused",!playing||!clockRunning},{"sessionId",session},{"song",song},{"category",category},{"chart",chart},{"adapterStatus","experimental-unverified-runtime"},{"musicDuration",state=="playing"?(object)musicDuration:null},{"musicDurationSource","CRI CueInfo.length milliseconds"},{"captureSliceMs",captureMs},{"sampleMs",sampleMs},{"samplePeakMs",samplePeakMs},{"lastSpike",lastSpike}};
  var judgePhase=state=="playing"?Convert.ToString(Get(activeProcess,"_sequence")):null;
  if(hitOffsets!=null)snapshot["hitOffsets"]=hitOffsets;
  snapshot["judgements"]=ActualJudgeTelemetry.Snapshot(session,state=="playing"&&settlement=="none"&&(judgePhase=="StartWait"||judgePhase=="Play"||judgePhase=="PlayEnd"));
  // Only primitives/arrays enter worker; never Unity/game objects off-thread.
  lock(Gate)pending=snapshot;
 }catch(Exception ex){transportError="Snapshot unavailable: "+ex.GetBaseException().Message;}finally{MarkStage("snapshot-publish");sampleMs=sampleWatch.Elapsed.TotalMilliseconds;samplePeakMs=Math.Max(samplePeakMs,sampleMs);if(sampleMs>8){lastSpike=new Dictionary<string,object>{{"utc",DateTime.UtcNow.ToString("o")},{"state",state},{"sessionId",session},{"totalMs",sampleMs},{"slowestStage",slowStage},{"slowestStageMs",slowStageMs},{"gen0CollectionsDuringSample",GC.CollectionCount(0)-gcBefore}};}}}
 object ReadResult(object process){
  var players=Get(process,"_userData") as IList;var scores=Get(process,"_userScores") as IList;var hides=Get(process,"_isHideScore") as IList;
  if(players==null||player>=players.Count||!Convert.ToBoolean(Get(players[player],"IsEntry"))||scores==null||player>=scores.Count||scores[player]==null||hides!=null&&player<hides.Count&&Convert.ToBoolean(hides[player]))return null;
  var id=Convert.ToInt32(Get(process,"_musicID"));var game=TypeOf("Manager.GameManager");var diffs=Get(game,"SelectDifficultyID") as IList;
  if(id<=0||diffs==null||player>=diffs.Count)return null;var difficulty=Convert.ToInt32(diffs[player]);
  var md=Call(InstanceOf("Manager.DataManager"),"GetMusic",id);if(md==null)return null;
  var gameScores=Get(process,"_gameScoreLists") as IList;var played=gameScores!=null&&player<gameScores.Count?gameScores[player]:null;
  var notes=Get(Get(played,"SessionInfo"),"notesData");
  var previous=id==resultPreviousId&&difficulty==resultPreviousDifficulty?resultPrevious:null;
  var track=SettlementTelemetry.ResultTrackNumber;if(track<=0)track=Convert.ToInt32(Get(game,"MusicTrackNumber"));
  if(track>0&&previous!=null)trackBest[track]=Tuple.Create(id,difficulty,previous);
  object timing=null;
  try{
   if(track>0&&track<=99&&hitCredit==settlementEpoch&&hitTrack==track&&hitMusic==id&&hitDifficulty==difficulty&&hitSession!=null){
    var frozen=CalibratedHitTelemetry.Snapshot(player,hitSession,false,Math.Max(0,hitNoteCount),true);
    if(frozen!=null&&frozen.available&&Equals(frozen.session,hitSession)){
     timing=frozen.distribution;
     if(timing!=null)trackHitTiming[track]=Tuple.Create(id,difficulty,timing);
    }
   }
  }catch(Exception ex){transportError="Result hit timing unavailable: "+ex.GetBaseException().Message;}
  return ScoreSong(id,difficulty,md,scores[player],notes,previous,track,null,timing);
 }
 object ReadPhoto(){
  PhotoTrackSelection selected;
  if(!SettlementTelemetry.TryReadPhoto(player,out selected)){photoSong=null;photoScore=null;photoTrack=-1;photoIndex=-1;return null;}
  if(photoTrack==selected.TrackNumber&&photoIndex==selected.PhotoIndex&&ReferenceEquals(photoScore,selected.Score)&&photoSong!=null)return photoSong;
  var id=Convert.ToInt32(Get(selected.Score,"id"));
  Tuple<int,int,Dictionary<string,object>> entry;
  var previous=trackBest.TryGetValue(selected.TrackNumber,out entry)&&entry.Item1==id&&entry.Item2==selected.Difficulty?entry.Item3:null;
  Tuple<int,int,object> timing;
  var timingMatch=trackHitTiming.TryGetValue(selected.TrackNumber,out timing)&&timing.Item1==id&&timing.Item2==selected.Difficulty?timing.Item3:null;
  photoSong=ScoreSong(id,selected.Difficulty,selected.Music,selected.Score,selected.Notes,previous,selected.TrackNumber,selected.PhotoIndex,timingMatch);
  photoTrack=selected.TrackNumber;photoIndex=selected.PhotoIndex;photoScore=selected.Score;
  return photoSong;
 }
 static string DisplayLevel(object notes){
  try {
   var value=Get(notes,"musicLevelID");
   if(value==null)return null;
   // SDEZ MusicLevelIDEnum.GetLevelNum() reads MusicLevelData.levelNum.
   var level=Call(InstanceOf("Manager.DataManager"),"GetMusicLevel",Convert.ToInt32(value));
   var label=Convert.ToString(Get(level,"levelNum"))?.Trim();
   return string.IsNullOrWhiteSpace(label)||label.Length>16?null:label;
  }catch{return null;}
 }
 static object ChartSlot(IList entries,int index)=>entries!=null&&index>=0&&index<entries.Count?entries[index]:null;
 static double? ChartConstant(object notes){
  var level=Get(notes,"level");var fraction=Get(notes,"levelDecimal");var enabled=Get(notes,"isEnable");
  if(level==null||fraction==null||(enabled is bool active&&!active))return null;
  var whole=Number(level);var decimalDigit=Number(fraction);
  return whole>0&&decimalDigit>=0&&decimalDigit<=9?whole+decimalDigit/10:(double?)null;
 }
 object ScoreSong(int id,int difficulty,object md,object score,object notes,object previousBest,int track,int? photo,object timing){
  var allNotes=Get(md,"notesData") as IList;
  var levels=new List<object>();
  if(allNotes!=null)foreach(var n in allNotes)levels.Add(new{value=Number(Get(n,"level"))+Number(Get(n,"levelDecimal"))/10,enabled=Get(n,"isEnable")});
  var max=(int)(Number(Get(notes,"maxNotes"))*3);
  var achievement=Convert.ToUInt32(Get(score,"achivement"));var dx=Number(Get(score,"deluxscore"));var percent=max>0?(double?)(dx*100/max):null;
  var game=TypeOf("Manager.GameManager");
  return new Dictionary<string,object>{{"cover","/assets/jackets/"+Path.GetFileName(Convert.ToString(Get(md,"jacketFile")).Replace((char)92,'/'))},{"title",Get(Get(md,"name"),"str")},{"artist",Get(Get(md,"artistName"),"str")},{"id",id},{"difficulty",difficulty},{"selection","difficulty"},{"isResult",true},{"hitTiming",timing},{"displayLevel",DisplayLevel(notes)??DisplayLevel(ChartSlot(allNotes,difficulty))},{"trackNumber",track>0?(object)track:null},{"photoIndex",photo},{"previousBest",previousBest},{"chartType",Call(game,"GetScoreKind",id)?.ToString()},{"utageKanji",Get(md,"utageKanjiName")},{"levels",levels},{"utageConstant",id>=100000?ChartConstant(notes)??ChartConstant(ChartSlot(allNotes,difficulty)):null},{"achievement",achievement/10000.0},{"rating",SingleRate(id,difficulty,score)},{"rank",ClearRank(Get(score,"scoreRank"))},{"dxScore",dx},{"dxMax",max>0?(object)max:null},{"dxPercent",percent},{"dxRank",percent.HasValue?(object)Convert.ToInt32(Call(game,"GetDeluxcoreRank",(int)percent.Value)):null},{"combo",Get(score,"combo")?.ToString()},{"sync",Get(score,"sync")?.ToString()}};
 }
 IEnumerator ReadChart(object reader){if(reader==null)yield break;var events=new List<object>();foreach(var note in (IEnumerable)Call(reader,"GetNoteList")){var type=Get(note,"type");var typeName=Call(type,"getEnum").ToString();events.Add(new Dictionary<string,object>{{"noteIndex",Get(note,"indexNote")},{"time",Seconds(Get(note,"time"))},{"endTime",Seconds(Get(note,"end"))},{"tick",Get(Get(note,"time"),"grid")},{"lane",Get(note,"startButtonPos")},{"type",typeName},{"connected",Convert.ToBoolean(Call(type,"isConnectSlide"))},{"break",Convert.ToBoolean(Call(type,"isBreak"))},{"each",Get(note,"isEach")},{"touchEffect",Get(note,"effect").ToString()}});yield return null;}
  var c=Call(reader,"GetCompositioin");var bpms=new List<object>();foreach(var b in (IEnumerable)Get(c,"_bpmList")){bpms.Add(new{time=Seconds(Get(b,"time")),tick=Get(Get(b,"time"),"grid"),bpm=Number(Get(b,"bpm"))});yield return null;}var meters=new List<object>();foreach(var m in (IEnumerable)Get(c,"_meterList")){meters.Add(new{time=Seconds(Get(m,"time")),numerator=Number(Get(m,"num")),denominator=Number(Get(m,"denomi"))});yield return null;}var lists=(Array)Get(c,"_barList");var bars=new List<object>();var beats=new List<object>();foreach(var bar in (IEnumerable)lists.GetValue(2)){bars.Add(new{time=Seconds(Get(bar,"time")),bar=Number(Get(bar,"numTotal"))+1});yield return null;}foreach(var mark in (IEnumerable)lists.GetValue(3)){beats.Add(new{time=Seconds(Get(mark,"time")),beat=Number(Get(mark,"numBar"))+1});yield return null;}chart=new{events,bpms,meters,bars,beats,resolution=Call(reader,"getResolution"),duration=Seconds(Get(c,"_endGameTime")),noteEndTime=Seconds(Get(c,"_endGameTime")),durationSource="last-judgeable-note",musicDuration=(double?)null};yield break;
 }
 void SendLoop(){object sentChart=null;while(running){Dictionary<string,object> data;lock(Gate){data=pending;pending=null;}if(data!=null)try{
 object fullChart;data.TryGetValue("chart",out fullChart);var outgoing=new Dictionary<string,object>(data);if(fullChart!=null&&ReferenceEquals(fullChart,sentChart))outgoing.Remove("chart");
 var json=Json.Write(outgoing);var request=(HttpWebRequest)WebRequest.Create(endpoint);request.Method="POST";request.ContentType="application/json";request.Headers["X-Stellar-Token"]=token;request.Timeout=1000;request.ReadWriteTimeout=1000;request.Proxy=null;var bytes=System.Text.Encoding.UTF8.GetBytes(json);request.ContentLength=bytes.Length;using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);using(var response=request.GetResponse()){sentChart=response.Headers["X-Stellar-Need-Chart"]=="1"?null:fullChart;}
 }catch(Exception ex){transportError="Local HTTP publisher unavailable: "+ex.GetBaseException().Message;}Thread.Sleep(20);}}
 public override void OnLateInitializeMelon(){try{NativeButtonLeds.TryInstall(new HarmonyLib.Harmony("stellar.tool.native-led-readonly"));}catch(Exception ex){LoggerInstance.Warning("Native button LED capture unavailable: "+ex.GetBaseException().Message);}}
 public override void OnDeinitializeMelon(){InputTelemetry.Stop();running=false;sender?.Join(1200);}
}
}
