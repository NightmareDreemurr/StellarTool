using System;
using System.Collections.Generic;
namespace StellarTool.Mod {
// Pure lifecycle model, including the identity guard for overlapping fade releases.
public sealed class SettlementState {
 static readonly HashSet<string> Settling=new HashSet<string>{
  "MapResultProcess","NextTrackProcess","UnlockProcess","UnlockMusicProcess",
  "GetPresentProcess","GetMusicProcess","DataSaveProcess","GameOverProcess",
  "ContinueProcess","AddFriendProcess","FriendSeasonRankingProcess",
  "CourseResultProcess","FestaPlayResultProcess","FestaFinalResultProcess",
  "CircleFinalResultProcess","CircleRewardProcess"
 };
 public string Phase {get;private set;}="none";
 public long CreditEpoch {get;private set;}
 public int ResultTrackNumber {get;private set;}
 public object PhotoProcess {get;private set;}
 object selectionProcess;
 public void Reset(){CreditEpoch++;Phase="none";ResultTrackNumber=0;PhotoProcess=null;selectionProcess=null;}
 public void Started(string name,object process,int currentTrack,bool showSelection=true){
  if(name=="EntryProcess"||name=="AdvertiseProcess"){Reset();return;}
  if(name=="MusicSelectProcess"||name=="GameProcess"||name=="ResultProcess"){
   Phase="none";PhotoProcess=null;selectionProcess=null;
   if(name=="ResultProcess")ResultTrackNumber=currentTrack>0?currentTrack:0;
   return;
  }
  if(name=="RegionalSelectProcess"||name=="CharacterSelectProces"){
   if(showSelection){selectionProcess=process;PhotoProcess=null;Phase=name=="RegionalSelectProcess"?"map-select":"partner-select";}
   return;
  }
  if(name=="PhotoEditProcess"){PhotoProcess=process;selectionProcess=null;Phase="photo";return;}
  if(Settling.Contains(name)){PhotoProcess=null;selectionProcess=null;Phase="settling";}
 }
 public string WithMusicSelection(string subsequence){
  return Phase=="none"&&subsequence=="Character"?"partner-select":Phase;
 }
 public void Released(object process){
  if(selectionProcess!=null&&ReferenceEquals(selectionProcess,process)){selectionProcess=null;Phase="none";}
  if(ReferenceEquals(PhotoProcess,process)){PhotoProcess=null;Phase="settling";}
 }
 public static bool IsWatched(string name){
  return Settling.Contains(name)||name=="EntryProcess"||name=="AdvertiseProcess"||
   name=="MusicSelectProcess"||name=="GameProcess"||name=="ResultProcess"||name=="PhotoEditProcess"||name=="RegionalSelectProcess"||name=="CharacterSelectProces";
 }
}
public sealed class PhotoTrackSelection {
 // Main-thread extraction only. Caller must detach primitive DTOs before publishing.
 public object Score,GameScore,Music,Notes;
 public int Difficulty,TrackNumber,PhotoIndex;
}
public static class SettlementSelection {
 public static bool TryRead(object process,int player,Func<object,string,object> get,out PhotoTrackSelection selection){
  selection=null;if(process==null||player<0||player>1)return false;
  var users=get(process,"_userDatas") as Array;
  if(users==null||users.Rank!=1||player>=users.Length||!Equals(get(users.GetValue(player),"IsEntry"),true))return false;
  var tracks=get(process,"_currentTrackNumbers") as Array;
  var photos=get(process,"_currentPhotoIndexs") as Array;
  if(tracks==null||tracks.Rank!=1||player>=tracks.Length)return false;
  int track=Convert.ToInt32(tracks.GetValue(player));
  var scores=get(process,"_userScores") as Array;var games=get(process,"_gameScoreLists") as Array;
  var notes=get(process,"_notesDatas") as Array;var diffs=get(process,"_difficulties") as Array;
  var music=get(process,"_musicDatas") as Array;
  foreach(var a in new[]{scores,games,notes,diffs})
   if(a==null||a.Rank!=2||player>=a.GetLength(0)||track<0||track>=a.GetLength(1))return false;
  if(music==null||music.Rank!=1||track>=music.Length)return false;
  var hidden=get(process,"_isHideScore") as Array;
  if(hidden!=null&&((hidden.Rank==1&&player<hidden.Length&&Equals(hidden.GetValue(player),true))||
   (hidden.Rank==2&&player<hidden.GetLength(0)&&track<hidden.GetLength(1)&&Equals(hidden.GetValue(player,track),true))))return false;
  var score=scores.GetValue(player,track);var played=games.GetValue(player,track);var md=music.GetValue(track);var nd=notes.GetValue(player,track);
  if(score==null||played==null||md==null||nd==null)return false;
  int difficulty=Convert.ToInt32(diffs.GetValue(player,track));
  var info=get(played,"SessionInfo");
  int id=Convert.ToInt32(get(score,"id"));
  if(id<=0||Convert.ToInt32(get(info,"musicId"))!=id||Convert.ToInt32(get(info,"difficulty"))!=difficulty)return false;
  selection=new PhotoTrackSelection{Score=score,GameScore=played,Music=md,Notes=nd,Difficulty=difficulty,TrackNumber=track+1,
   PhotoIndex=photos!=null&&photos.Rank==1&&player<photos.Length?Convert.ToInt32(photos.GetValue(player)):0};
  return true;
 }
}
}
