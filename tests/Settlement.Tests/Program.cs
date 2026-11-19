using StellarTool.Mod;
using System.Reflection;
int checks=0;void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
var state=new SettlementState();var photo1=new object();var photo2=new object();
state.Started("ResultProcess",new object(),3);Check(state.Phase=="none"&&state.ResultTrackNumber==3,"result exact ordinal");
state.Started("MapResultProcess",new object(),3);Check(state.Phase=="settling","map hides last result");
state.Started("NextTrackProcess",new object(),4);Check(state.Phase=="settling"&&state.ResultTrackNumber==3,"next track count not used for previous result");
state.Started("PhotoEditProcess",photo1,4);state.Started("PhotoEditProcess",photo2,4);state.Released(photo1);Check(state.Phase=="photo","old fade release cannot wipe newer photo");
state.Released(photo2);Check(state.Phase=="settling","photo exit clears selection");
state.Started("MusicSelectProcess",new object(),4);Check(state.Phase=="none","next music exits settling");
var mapPick=new object();var partnerPick=new object();
Check(SettlementState.IsWatched("RegionalSelectProcess")&&SettlementState.IsWatched("CharacterSelectProces"),"both verified selection process names hooked");
state.Started("NextTrackProcess",new object(),4);
state.Started("RegionalSelectProcess",mapPick,4);Check(state.Phase=="map-select","actual map choice supersedes settling");
state.Started("CharacterSelectProces",partnerPick,4);state.Released(mapPick);Check(state.Phase=="partner-select","old map fade cannot clear actual partner selection");
state.Released(partnerPick);Check(state.Phase=="none","current partner selection releases");
Check(state.WithMusicSelection("Character")=="partner-select"&&state.WithMusicSelection("Genre")=="none","nested song-select Character state only");
state.Started("MapResultProcess",new object(),4);
Check(state.WithMusicSelection("Character")=="settling","stale internal selector cannot override true map progress");
state.Started("CharacterSelectProces",new object(),4,false);Check(state.Phase=="settling","invisible automatic partner skip does not override map result");
state.Started("MusicSelectProcess",new object(),4);Check(state.Phase=="none","actual song selection clears map result");
var credit=state.CreditEpoch;state.Reset();Check(state.CreditEpoch>credit,"credit identity invalidates previousBest cache");Check(state.ResultTrackNumber==0&&state.PhotoProcess==null,"new credit cannot leak");
object Get(object o,string name){if(o==null)return null;var t=o.GetType();return t.GetField(name)?.GetValue(o)??t.GetProperty(name)?.GetValue(o);}
var p=new Photo();
for(int i=0;i<3;i++){p._userScores[0,i]=new Score{id=100+i};p._gameScoreLists[0,i]=new Played{SessionInfo=new Info{musicId=100+i,difficulty=i}};p._notesDatas[0,i]=new object();p._musicDatas[i]=new object();p._difficulties[0,i]=i;}
foreach(int index in new[]{2,0,1,0,2}){p._currentTrackNumbers[0]=index;Check(SettlementSelection.TryRead(p,0,Get,out var s)&&s.TrackNumber==index+1&&((Score)s.Score).id==100+index,"selected historical track, not latest");}
p._currentPhotoIndexs[0]=7;Check(SettlementSelection.TryRead(p,0,Get,out var same)&&same.TrackNumber==3&&same.PhotoIndex==7,"photo index not track ordinal");
p._currentTrackNumbers[0]=3;Check(!SettlementSelection.TryRead(p,0,Get,out _),"out of range no fallback");
p._currentTrackNumbers[0]=1;p._userScores[0,1]=null;Check(!SettlementSelection.TryRead(p,0,Get,out _),"missing selection no previous-song fallback");
p._currentTrackNumbers[0]=0;((Played)p._gameScoreLists[0,0]).SessionInfo.musicId=999;Check(!SettlementSelection.TryRead(p,0,Get,out _),"mismatched cached fields rejected");
Check(!SettlementSelection.TryRead(p,1,Get,out _),"non-entry player hidden");
p._currentTrackNumbers[0]=2;p._isHideScore=new[]{true,false};Check(!SettlementSelection.TryRead(p,0,Get,out _),"privacy-hidden photo not emitted");
Console.WriteLine(checks+" settlement checks passed");
class User {public bool IsEntry=true;}
class Score {public int id;}
class Info {public int musicId,difficulty;}
class Played {public Info SessionInfo;}
class Photo {
 public bool[] _isHideScore;
 public User[] _userDatas={new User(),new User{IsEntry=false}};
 public int[] _currentTrackNumbers=new int[2],_currentPhotoIndexs=new int[2];
 public object[,] _userScores=new object[2,3],_gameScoreLists=new object[2,3],_notesDatas=new object[2,3];
 public int[,] _difficulties=new int[2,3];public object[] _musicDatas=new object[3];
}
