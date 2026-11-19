using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace StellarTool.Mod {
// Main-thread-only, read-only adapter for the cached SDEZ category aggregates.
// Evidence: MusicSelectProcess.GenreSelectData / InputGenreSelectData;
// Monitor.MusicPlayStatusController.SetData. Existing counts are never recomputed.
// The additional average DX rate reads only the selected category, in bounded slices.
internal static class GenreTelemetry {
 const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static readonly int[] RankIds={5,8,9,10,11,12,13}; // A,S,S+,SS,SS+,SSS,SSS+
 static readonly string[] CounterNames={"fcNum","fcpNum","apNum","appNum","spNum","fsNum","fspNum","fsdNum","fsdpNum"};
 static readonly object[][] PlayerArgs={new object[]{0},new object[]{1}};
 static readonly object[][] GenreArgs={new object[]{0,0},new object[]{1,0}};
 static Type attemptedType;
 static Contract contract;
 static readonly Cache[] Caches={new Cache(),new Cache()};
 public static string LastError {get;private set;}

 sealed class Contract {
  internal FieldInfo Phase,Id,Total,Extra,Tournament,Achievement,Ranks,Dx;
  internal FieldInfo[] Counters;
  internal PropertyInfo Index,Sort,Score;
  internal MethodInfo Genre,Label,Tab,Difficulty,Entry,Sprite;
  internal PropertyInfo SpriteName;
  internal PropertyInfo DataManagerInstance,LevelFileName;
  internal MethodInfo LevelLookup;
  internal object[] RankKeys,DxKeys;
  internal Type DataType;
  internal bool DxAttempted;
  internal DxContract DxAdapter;
  internal string DxError;
  internal Contract(Type type) {
   if(type.FullName!="Process.MusicSelectProcess")throw new NotSupportedException("Unsupported selection process");
   Phase=NeedField(type,"_currentPlayerSubSequence");
   Index=NeedProperty(type,"CurrentCategorySelect");Sort=NeedProperty(type,"CategorySortSetting");Score=NeedProperty(type,"ScoreType");
   Genre=NeedMethod(type,"GetGenreSelectData",typeof(int),typeof(int));
   Label=NeedMethod(type,"GetCategoryName",typeof(int),typeof(int));
   Tab=NeedMethod(type,"GetPlayStatus");Difficulty=NeedMethod(type,"GetCurrentDifficulty",typeof(int));Entry=NeedMethod(type,"IsEntry",typeof(int));
   DataType=Genre.ReturnType;
   if(DataType.FullName!="Process.MusicSelectProcess+GenreSelectData")throw new NotSupportedException("Unsupported category data");
   Id=NeedField(DataType,"categoryID");Total=NeedField(DataType,"totalMusicNum");Extra=NeedField(DataType,"isExtra");Tournament=NeedField(DataType,"isTournament");Achievement=NeedField(DataType,"totalAchivement");
   Ranks=NeedField(DataType,"overRankNumList");Dx=NeedField(DataType,"overDxScoreList");
   Counters=new FieldInfo[CounterNames.Length];for(int i=0;i<Counters.Length;i++)Counters[i]=NeedField(DataType,CounterNames[i]);
   RankKeys=Keys(Ranks.FieldType,RankIds);DxKeys=Keys(Dx.FieldType,new[]{1,2,3,4,5});
   // Optional narrow lookup: this returns an already cached sprite, not Resources.Load.
   Sprite=type.GetMethod("GetTabSprite",Flags,null,new[]{DataType},null);
   if(Sprite!=null)SpriteName=Sprite.ReturnType.GetProperty("name",Flags);
   // Only Level has two different sprites sharing the same name. The exact
   // MusicLevelData.FileName (with or without "Level/") selects its resource.
   var manager=type.Assembly.GetType("Manager.DataManager",false);
   if(manager!=null&&manager.BaseType!=null){
    DataManagerInstance=manager.BaseType.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static);
    LevelLookup=manager.GetMethod("GetMusicLevel",Flags,null,new[]{typeof(int)},null);
    if(LevelLookup!=null)LevelFileName=LevelLookup.ReturnType.GetProperty("FileName",Flags);
   }
  }
 }
 sealed class Cache {
  internal object Process,Data,Result;
  internal int Index,Sort,Score,Difficulty,Tab,Id,Total;
  internal bool Extra,Tournament;
  internal uint Achievement;
  internal readonly int[] Scratch=new int[21];
  internal readonly int[] Counts=new int[21];
  internal readonly object[] SpriteArgs=new object[1];
  internal DxJob Dx;
  internal void Clear(){Process=null;Data=null;Result=null;Dx=null;SpriteArgs[0]=null;}
 }
 // A new statistic, not a value stored by GenreSelectData. Binding is optional:
 // any mismatch leaves averageDx unavailable without suppressing official counts.
 sealed class DxContract {
  internal PropertyInfo Lists,Random,Wait,Maximum;
  internal FieldInfo Choices,RowDifficulty,Notes,ScoreValue;
  internal MethodInfo GetId,GetScore,Remaster;
  internal Type CombineType,RowType;
  internal readonly int[] SpecialIds=new int[6]; // 12,4,14,197,300,107 in the data table
  internal readonly object[] ScoreKinds=new object[2];
  internal DxContract(Type processType) {
   Lists=NeedProperty(processType,"CombineMusicDataList");
   CombineType=processType.GetNestedType("CombineMusicSelectData",BindingFlags.Public|BindingFlags.NonPublic);
   RowType=processType.GetNestedType("MusicSelectData",BindingFlags.Public|BindingFlags.NonPublic);
   if(CombineType==null||RowType==null)throw new NotSupportedException("Category chart types unavailable");
   Random=NeedProperty(CombineType,"isRandomScore");Wait=NeedProperty(CombineType,"isWaitConnectScore");Choices=NeedField(CombineType,"musicSelectData");
   var scoreKind=NeedProperty(processType,"ScoreType").PropertyType;
   if(!scoreKind.IsEnum)throw new NotSupportedException("Category score kind unavailable");
   ScoreKinds[0]=Enum.ToObject(scoreKind,0);ScoreKinds[1]=Enum.ToObject(scoreKind,1);
   GetId=NeedMethod(CombineType,"GetID",scoreKind);
   RowDifficulty=NeedField(RowType,"Difficulty");Notes=NeedField(RowType,"ScoreData");
   var noteTypes=Notes.FieldType.GetGenericArguments();
   if(noteTypes.Length!=1)throw new NotSupportedException("Chart maximum notes unavailable");
   Maximum=NeedProperty(noteTypes[0],"maxNotes");
   GetScore=NeedMethod(processType,"GetUserScore",typeof(int),typeof(int),typeof(int));
   ScoreValue=NeedField(GetScore.ReturnType,"deluxscore");
   Remaster=NeedMethod(processType,"IsRemasterEnableForMusicID",typeof(int));
   var manager=processType.Assembly.GetType("Manager.DataManager",true);
   var instance=manager.BaseType.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);
   var genre=NeedMethod(manager,"GetMusicGenre",typeof(int));var genreId=NeedMethod(genre.ReturnType,"GetID");
   int[] keys={12,4,14,197,300,107};var args=new object[1];
   for(int i=0;i<keys.Length;i++){args[0]=keys[i];var record=genre.Invoke(instance,args);if(record==null)throw new NotSupportedException("Category ID metadata unavailable");SpecialIds[i]=Convert.ToInt32(genreId.Invoke(record,null));}
  }
 }
 sealed class DxJob {
  internal IList Items;
  internal int Count,Position,Seen,Expected;
  internal double Sum;
  internal double? Average;
  internal string Status="pending",Error;
  internal readonly object[] IdArgs=new object[1],ScoreArgs=new object[3],RemasterArgs=new object[1];
  internal void Fail(string reason){Status="unavailable";Error=reason;Average=null;}
 }
 const int DxItemsPerSample=64;
 static readonly long DxSliceTicks=Math.Max(1,Stopwatch.Frequency*35/100000); // 0.35 ms; checked before every item
 static DxJob AdvanceDx(Contract c,object process,int player,int index,int categoryId,int sort,int score,int difficulty,bool extra,int total,DxJob job) {
  long start=Stopwatch.GetTimestamp();
  try {
   if(!c.DxAttempted){c.DxAttempted=true;try{c.DxAdapter=new DxContract(process.GetType());}catch(Exception ex){c.DxError=ex.GetBaseException().Message;}}
   if(c.DxAdapter==null){if(job==null){job=new DxJob();job.Fail(c.DxError??"Category chart binding unavailable");}return job;}
   var d=c.DxAdapter;
   var lists=d.Lists.GetValue(process,null) as IList;
   var items=lists!=null&&index>=0&&index<lists.Count?lists[index] as IList:null;
   if(items==null||score<0||score>1){if(job==null||job.Error!="Exact selected chart list unavailable"){job=new DxJob();job.Fail("Exact selected chart list unavailable");}return job;}
   if(job!=null&&job.Error=="Exact selected chart list unavailable")job=null;
   // Stop/restart if the actual list was replaced while the UI kept the same ID.
   if(job==null||job.Items!=null&&(!ReferenceEquals(job.Items,items)||job.Count!=items.Count)){
    job=new DxJob{Items=items,Count=items.Count,Expected=total};job.IdArgs[0]=d.ScoreKinds[score];job.ScoreArgs[0]=player;
   }
   if(job.Status!="pending")return job;
   int examined=0;
   while(job.Position<job.Count&&examined<DxItemsPerSample&&Stopwatch.GetTimestamp()-start<DxSliceTicks){
    object combine=items[job.Position++];examined++;
    if(combine==null||combine.GetType()!=d.CombineType){job.Fail("Unrecognized category chart entry");break;}
    if(Convert.ToBoolean(d.Random.GetValue(combine,null)))continue; // exactly as InputGenreSelectData
    if(Convert.ToBoolean(d.Wait.GetValue(combine,null))){job.Fail("Connection placeholder is not a chart");break;}
    var choices=d.Choices.GetValue(combine) as IList;
    var row=choices!=null&&score<choices.Count?choices[score]:null;
    if(row==null||row.GetType()!=d.RowType){job.Fail("Selected SD/DX chart data missing");break;}
    int id=Convert.ToInt32(d.GetId.Invoke(combine,job.IdArgs));
    if(id<=0||id==2||id==3){job.Fail("Invalid chart ID");break;}
    bool ownDifficulty=(!extra&&sort==2)||categoryId==d.SpecialIds[4];
    for(int i=0;i<4;i++)ownDifficulty|=extra&&categoryId==d.SpecialIds[i];
    int diff=ownDifficulty?Convert.ToInt32(d.RowDifficulty.GetValue(row)):difficulty;
    if(diff==4){job.RemasterArgs[0]=id;if(!Convert.ToBoolean(d.Remaster.Invoke(process,job.RemasterArgs)))diff=3;}
    if(categoryId==d.SpecialIds[5])diff=0;
    if(diff<0||diff>4){job.Fail("Resolved chart difficulty unavailable");break;}
    job.ScoreArgs[1]=diff;job.ScoreArgs[2]=id;var record=d.GetScore.Invoke(process,job.ScoreArgs);
    job.Seen++;
    if(record==null)continue; // Missing personal score contributes zero but stays in the ALL-chart denominator.
    var notes=d.Notes.GetValue(row) as IList;
    var note=notes!=null&&diff<notes.Count?notes[diff]:null;
    int maximumNotes=note==null?0:Convert.ToInt32(d.Maximum.GetValue(note,null));
    if(maximumNotes<=0){job.Fail("Played chart has no verified DX maximum");break;}
    uint value=Convert.ToUInt32(d.ScoreValue.GetValue(record));
    // Preserve the exact ratio, not the integer-truncated percent used for DX stars.
    // No clamp or weighted sum: each game-listed chart contributes equally.
    job.Sum+=value*100.0/(maximumNotes*3.0);
   }
   if(job.Status=="pending"&&job.Position==job.Count){
    if(job.Seen!=job.Expected)job.Fail("Chart list count differs from official category total");
    else{job.Average=job.Sum/job.Expected;job.Status="ready";}
   }
   return job;
  }catch(Exception ex){if(job==null)job=new DxJob();job.Fail(ex.GetBaseException().Message);return job;}
 }
 static void AddDx(Dictionary<string,object> result,DxJob dx){
  result["averageDx"]=dx.Average.HasValue?(object)dx.Average.Value:null;
  result["averageDxStatus"]=dx.Status;result["averageDxScope"]="all-charts-unplayed-zero";
  result["averageDxUnavailableReason"]=dx.Error;
 }
 static FieldInfo NeedField(Type t,string name){var f=t.GetField(name,Flags);if(f==null)throw new MissingFieldException(t.FullName,name);return f;}
 static PropertyInfo NeedProperty(Type t,string name){var p=t.GetProperty(name,Flags);if(p==null||!p.CanRead)throw new MissingMemberException(t.FullName,name);return p;}
 static MethodInfo NeedMethod(Type t,string name,params Type[] args){var m=t.GetMethod(name,Flags,null,args,null);if(m==null)throw new MissingMethodException(t.FullName,name);return m;}
 static object[] Keys(Type dictionary,int[] ids){var args=dictionary.GetGenericArguments();if(args.Length!=2||!args[0].IsEnum||args[1]!=typeof(int))throw new NotSupportedException("Unsupported category counter map");var keys=new object[ids.Length];for(int i=0;i<ids.Length;i++)keys[i]=Enum.ToObject(args[0],ids[i]);return keys;}
 static Contract Bind(object process){if(process==null)return null;var t=process.GetType();if(t!=attemptedType){attemptedType=t;contract=null;foreach(var c in Caches)c.Clear();try{contract=new Contract(t);LastError=null;}catch(Exception ex){LastError=ex.GetBaseException().Message;}}return contract;}
 static bool InGenre(Contract c,object process,int player){var a=c.Phase.GetValue(process) as Array;return a!=null&&a.Rank==1&&player<a.Length&&string.Equals(Convert.ToString(a.GetValue(player)),"Genre",StringComparison.Ordinal);}
 public static bool IsCategory(object selectProcess,int player){if(player<0||player>1)return false;try{var c=Bind(selectProcess);return c!=null&&InGenre(c,selectProcess,player);}catch(Exception ex){LastError=ex.GetBaseException().Message;return false;}}
 static int Count(IDictionary values,object key){return values.Contains(key)?Convert.ToInt32(values[key]):0;}
 static int[] Copy(int[] source,int start,int count){var result=new int[count];Array.Copy(source,start,result,0,count);return result;}

 // Returns only detached primitives/arrays; safe for the existing serialization worker.
 // Null clears the prior category on non-Genre/inactive/unsupported/empty state.
 public static object Read(object selectProcess,int player) {
  if(player<0||player>1)return null;
  var cache=Caches[player];
  try {
   var c=Bind(selectProcess);
   if(c==null||!InGenre(c,selectProcess,player)||!Convert.ToBoolean(c.Entry.Invoke(selectProcess,PlayerArgs[player]))){cache.Clear();return null;}
   var data=c.Genre.Invoke(selectProcess,GenreArgs[player]); // offset 0, NOT an absolute category index
   if(data==null||data.GetType()!=c.DataType){cache.Clear();return null;}
   int total=Convert.ToInt32(c.Total.GetValue(data));
   if(total<=0){cache.Clear();return null;} // The official average divides by this count.
   int id=Convert.ToInt32(c.Id.GetValue(data)),index=Convert.ToInt32(c.Index.GetValue(selectProcess,null));
   int sort=Convert.ToInt32(c.Sort.GetValue(selectProcess,null)),score=Convert.ToInt32(c.Score.GetValue(selectProcess,null));
   int difficulty=Convert.ToInt32(c.Difficulty.Invoke(selectProcess,PlayerArgs[player])),tab=Convert.ToInt32(c.Tab.Invoke(selectProcess,null));
   if(index<0||sort<0||sort>5||tab<0||tab>5||difficulty<0||difficulty>4){cache.Clear();return null;}
   bool extra=Convert.ToBoolean(c.Extra.GetValue(data)),tournament=Convert.ToBoolean(c.Tournament.GetValue(data));
   uint achievement=Convert.ToUInt32(c.Achievement.GetValue(data));
   var ranks=c.Ranks.GetValue(data) as IDictionary;var dx=c.Dx.GetValue(data) as IDictionary;
   if(ranks==null||dx==null){cache.Clear();return null;}
   var counts=cache.Scratch;
   for(int i=0;i<7;i++)counts[i]=Count(ranks,c.RankKeys[i]);
   for(int i=0;i<9;i++)counts[7+i]=Convert.ToInt32(c.Counters[i].GetValue(data));
   for(int i=0;i<5;i++)counts[16+i]=Count(dx,c.DxKeys[i]);
   bool same=ReferenceEquals(cache.Process,selectProcess)&&ReferenceEquals(cache.Data,data)&&cache.Index==index&&cache.Id==id&&cache.Total==total&&cache.Sort==sort&&cache.Score==score&&cache.Difficulty==difficulty&&cache.Tab==tab&&cache.Extra==extra&&cache.Tournament==tournament&&cache.Achievement==achievement;
   for(int i=0;i<counts.Length;i++){if(counts[i]<0||counts[i]>total){cache.Clear();return null;}same&=cache.Counts[i]==counts[i];}
   var oldDx=cache.Dx;string oldDxStatus=oldDx==null?null:oldDx.Status;
   cache.Dx=AdvanceDx(c,selectProcess,player,index,id,sort,score,difficulty,extra,total,same?cache.Dx:null);
   if(same&&cache.Result!=null){
    if(!ReferenceEquals(oldDx,cache.Dx)||oldDxStatus!=cache.Dx.Status){var updated=new Dictionary<string,object>((Dictionary<string,object>)cache.Result);AddDx(updated,cache.Dx);cache.Result=updated;}
    return cache.Result;
   }
   string label=Convert.ToString(c.Label.Invoke(selectProcess,GenreArgs[player]));
   string assetName=null;
   if(c.Sprite!=null&&c.SpriteName!=null){try{cache.SpriteArgs[0]=data;var sprite=c.Sprite.Invoke(selectProcess,cache.SpriteArgs);if(sprite!=null)assetName=Convert.ToString(c.SpriteName.GetValue(sprite,null));}catch{ /* No guessed asset path when an optional sprite is unavailable. */ }finally{cache.SpriteArgs[0]=null;}}
   string assetResourcePath=null;
   if(sort==2&&!extra&&assetName!=null&&c.DataManagerInstance!=null&&c.LevelLookup!=null&&c.LevelFileName!=null){
    try{
     var manager=c.DataManagerInstance.GetValue(null,null);cache.SpriteArgs[0]=id;
     var level=c.LevelLookup.Invoke(manager,cache.SpriteArgs);
     var file=level==null?null:Convert.ToString(c.LevelFileName.GetValue(level,null));
     // These two paths are confirmed by the SDEZ ResourceManager. Never choose
     // a same-name variant heuristically or accept arbitrary external paths.
     if(assetName.StartsWith("UI_CMN_TabTitle_Level_",StringComparison.Ordinal)&&(file==assetName||file=="Level/"+assetName))assetResourcePath="Common/Sprites/Tab/Title/"+file;
    }catch{ /* Optional metadata failure must not suppress real aggregate data. */ }finally{cache.SpriteArgs[0]=null;}
   }
   uint averageRaw=achievement/(uint)total; // truncates before conversion, matching official UI
   cache.Result=new Dictionary<string,object>{
    {"id",id},{"label",label},{"asset",null},{"assetName",assetName},{"assetResourcePath",assetResourcePath},{"total",total},{"clear",counts[0]},
    {"average",averageRaw/10000.0},{"averageRaw",averageRaw},{"totalAchievementRaw",achievement},
    {"clearRank",Copy(counts,0,7)},{"combo",Copy(counts,7,4)},{"sync",Copy(counts,11,5)},{"dx",Copy(counts,16,5)},
    {"tab",tab},{"index",index},{"sort",sort},{"scoreType",score},{"difficulty",difficulty},{"isExtra",extra},{"isTournament",tournament},
    {"source","game-category-cache"}
   };
   AddDx((Dictionary<string,object>)cache.Result,cache.Dx);
   cache.Process=selectProcess;cache.Data=data;cache.Index=index;cache.Id=id;cache.Total=total;cache.Sort=sort;cache.Score=score;cache.Difficulty=difficulty;cache.Tab=tab;cache.Extra=extra;cache.Tournament=tournament;cache.Achievement=achievement;
   Array.Copy(counts,cache.Counts,counts.Length);LastError=null;return cache.Result;
  } catch(Exception ex){cache.Clear();LastError=ex.GetBaseException().Message;return null;}
 }
}
}
