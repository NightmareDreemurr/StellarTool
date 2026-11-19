using System;
namespace StellarTool.Mod {
public struct CommittedJudge { public bool Judged,Skipped;public int Timing,Type; }
public struct ActualJudgeEvent { public bool AutoPlay;public long Sequence;public int NoteIndex,Timing,ScoreType;public double GameTime; }
// Game-thread owner; bounded primitive ring. Snapshot copies happen outside judge callbacks.
public sealed class CommittedJudgeBuffer {
 readonly ActualJudgeEvent[] events;int count,write;long sequence,overwritten;
 public CommittedJudgeBuffer(int capacity=128){if(capacity<1)throw new ArgumentException("capacity");events=new ActualJudgeEvent[capacity];}
 public long Sequence {get{return sequence;}} public long Overwritten {get{return overwritten;}}
 public void Clear(){count=write=0;sequence=overwritten=0;}
 public bool Commit(bool wasJudged,CommittedJudge result,int index,double time,bool autoPlay=false){
  if(wasJudged||result.Skipped||!result.Judged||index<0||result.Timing<0||result.Timing>14||double.IsNaN(time)||double.IsInfinity(time))return false;
  if(count==events.Length&&time-events[write].GameTime<1)overwritten++;
  events[write]=new ActualJudgeEvent{AutoPlay=autoPlay,Sequence=++sequence,NoteIndex=index,Timing=result.Timing,ScoreType=result.Type,GameTime=time};
  write=(write+1)%events.Length;if(count<events.Length)count++;return true;
 }
 public ActualJudgeEvent[] Recent(double now){
  int n=0;for(int i=0;i<count;i++){var e=events[(write-count+i+events.Length)%events.Length];if(now>=e.GameTime&&now-e.GameTime<=1)n++;}
  var result=new ActualJudgeEvent[n];n=0;
  for(int i=0;i<count;i++){var e=events[(write-count+i+events.Length)%events.Length];if(now>=e.GameTime&&now-e.GameTime<=1)result[n++]=e;}
  return result;
 }
}
}
