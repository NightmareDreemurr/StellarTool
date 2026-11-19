using System;
using System.Runtime.InteropServices;

namespace StellarTool.Mod {

// These are accepted game-note judgements, not timestamps from an input device.
public enum CalibratedHitGroup { Standard, Ex, Touch, TouchHold }
public enum CalibratedHitKind { Tap, Break, Hold, BreakHold, Touch, TouchHold }

public struct CalibratedHitSample {
 public int Player, NoteIndex, Timing;
 public CalibratedHitGroup Group;
 public CalibratedHitKind Kind;
 public bool HoldHead;
 public float OffsetMs;
 public double GameTime;
 public long Sequence;
}

[StructLayout(LayoutKind.Explicit)]
public struct CalibratedFloatBits {
 [FieldOffset(0)] public float value;
 [FieldOffset(0)] public uint bits;
 public static uint Of(float value) { return new CalibratedFloatBits { value=value }.bits; }
}

// One game-thread owner per player. All callback storage is fixed and preallocated.
public sealed class CalibratedHitBuffer {
 public const int DefaultNoteCapacity=65536, DefaultRecentCapacity=512;
 public const int BinCount=60;
 public const double BinStartMs=-150, BinWidthMs=5, RecentSeconds=2;
 readonly uint[] seen;
 readonly CalibratedHitSample[] recent;
 readonly RunningGroup[] groups={new RunningGroup(),new RunningGroup(),new RunningGroup(),new RunningGroup()};
 uint generation=1;
 int count,write,noteCount;
 long sequence,overwritten,attempt;
 string session;
 bool active,hasAttempt,frozen,awaitingSeek;
 double lastGameTime=double.NaN;
 HitOffsetDistribution cachedDistribution;
 long distributionSequence=-1;
 readonly int player;
 public CalibratedHitBuffer(int player,int noteCapacity=DefaultNoteCapacity,int recentCapacity=DefaultRecentCapacity) {
  if(player<0||player>1||noteCapacity<1||recentCapacity<1)throw new ArgumentOutOfRangeException();
  this.player=player;seen=new uint[checked(noteCapacity*2)];recent=new CalibratedHitSample[recentCapacity];
 }
 public bool Active {get{return active&&!awaitingSeek;}}
 public long Attempt {get{return attempt;}}
 public long Sequence {get{return sequence;}}
 public string Session {get{return session;}}
 public int NoteCapacity {get{return seen.Length/2;}}

 // Called outside judgement callbacks. A result freezes the exact previous attempt,
 // even when the caller has already cleared its chart or note-count references.
 public void SetContext(string nextSession,bool playing,int nextNoteCount,bool retainResult,double gameTime) {
  if(retainResult&&!playing) {
   if(awaitingSeek){ClearAttempt();hasAttempt=false;session=null;noteCount=0;}
   active=false;frozen=hasAttempt;awaitingSeek=false;return;
  }
  if(!playing||string.IsNullOrEmpty(nextSession)||nextNoteCount<1||nextNoteCount>NoteCapacity) {
   if(hasAttempt)ClearAttempt();
   active=false;frozen=false;hasAttempt=false;awaitingSeek=false;session=null;noteCount=0;return;
  }
  if(!hasAttempt||session!=nextSession||noteCount!=nextNoteCount||frozen||awaitingSeek||
     (IsFinite(gameTime)&&IsFinite(lastGameTime)&&gameTime<lastGameTime-0.1)) {
   ClearAttempt();session=nextSession;noteCount=nextNoteCount;hasAttempt=true;
  }
  active=true;frozen=false;awaitingSeek=false;
  if(IsFinite(gameTime))lastGameTime=gameTime;
 }

 void ClearAttempt() {
  attempt++;sequence=overwritten=0;count=write=0;lastGameTime=double.NaN;
  if(++generation==0) {Array.Clear(seen,0,seen.Length);generation=1;}
  for(int i=0;i<groups.Length;i++)groups[i].Clear();
  cachedDistribution=null;distributionSequence=-1;
 }

 // Reject a rewind immediately, before the next 20 Hz context snapshot. Do not
 // mix pre-seek and post-seek samples; SetContext starts the subsequent attempt.
 public bool CanProbe(int noteIndex,bool head,double gameTime) {
  if(!Active||noteIndex<0||noteIndex>=noteCount||!IsFinite(gameTime))return false;
  if(IsFinite(lastGameTime)&&gameTime<lastGameTime-0.1) {awaitingSeek=true;return false;}
  return seen[noteIndex*2+(head?1:0)]!=generation;
 }

 public bool TryCommit(CalibratedHitSample sample) {
  if(sample.Player!=player||sample.Timing<1||sample.Timing>13||
     (int)sample.Kind<0||(int)sample.Kind>5||
     (int)sample.Group<0||(int)sample.Group>=groups.Length||float.IsNaN(sample.OffsetMs)||float.IsInfinity(sample.OffsetMs)||
     !CanProbe(sample.NoteIndex,sample.HoldHead,sample.GameTime))return false;
  seen[sample.NoteIndex*2+(sample.HoldHead?1:0)]=generation;
  lastGameTime=sample.GameTime;
  sample.Sequence=++sequence;
  if(count==recent.Length&&sample.GameTime-recent[write].GameTime<=RecentSeconds)overwritten++;
  recent[write]=sample;write=(write+1)%recent.Length;if(count<recent.Length)count++;
  groups[(int)sample.Group].Add(sample.OffsetMs);
  return true;
 }

 public HitOffsetSnapshot Snapshot(bool adapterReady,double gameTime) {
  bool available=adapterReady&&hasAttempt&&!awaitingSeek;
  string state=available?(active?"active":frozen?"frozen":"unavailable"):"unavailable";
  if(!active&&frozen)gameTime=lastGameTime;
  int n=0;
  if(available)for(int i=0;i<count;i++)if(IsRecent(recent[At(i)],gameTime))n++;
  var events=new HitOffsetEvent[n];n=0;
  if(available)for(int i=0;i<count;i++) {
   var item=recent[At(i)];if(!IsRecent(item,gameTime))continue;
   events[n++]=new HitOffsetEvent(item);
  }
  if(cachedDistribution==null||distributionSequence!=sequence) {
   cachedDistribution=new HitOffsetDistribution(groups[0].Snapshot(),groups[1].Snapshot(),groups[2].Snapshot(),groups[3].Snapshot());
   distributionSequence=sequence;
  }
  return new HitOffsetSnapshot(player,session,attempt,available,state,sequence,overwritten,events,cachedDistribution);
 }
 int At(int offset){return (write-count+offset+recent.Length)%recent.Length;}
 static bool IsRecent(CalibratedHitSample sample,double now){return now>=sample.GameTime&&now-sample.GameTime<=RecentSeconds;}
 static bool IsFinite(double value){return !double.IsNaN(value)&&!double.IsInfinity(value);}

 sealed class RunningGroup {
  readonly int[] bins=new int[BinCount];
  int n,below,above;
  double mean,m2,min,max;
  public void Clear(){n=below=above=0;mean=m2=min=max=0;Array.Clear(bins,0,bins.Length);}
  public void Add(float raw) {
   // float -> double is exact. Welford uses all samples, not the bounded UI ring.
   double x=raw;int next=++n;double delta=x-mean;mean+=delta/next;m2+=delta*(x-mean);
   if(next==1){min=max=x;}else{if(x<min)min=x;if(x>max)max=x;}
   if(x<BinStartMs)below++;
   else if(x>BinStartMs+BinWidthMs*BinCount)above++;
   else {int bin=(int)((x-BinStartMs)/BinWidthMs);if(bin==BinCount)bin--;bins[bin]++;}
  }
  public HitOffsetStatistics Snapshot(){return new HitOffsetStatistics(n,mean,m2,min,max,(int[])bins.Clone(),below,above);}
 }
}

public sealed class HitOffsetEvent {
 public long sequence {get;private set;}
 public int noteIndex {get;private set;}
 public string phase {get;private set;}
 public string kind {get;private set;}
 public string group {get;private set;}
 public int timing {get;private set;}
 public double offsetMs {get;private set;}
 public uint offsetBits {get;private set;}
 public double gameTime {get;private set;}
 static readonly string[] Kinds={"tap","break","hold","break-hold","touch","touch-hold"};
 static readonly string[] Groups={"standard","ex","touch","touchHold"};
 public HitOffsetEvent(CalibratedHitSample sample) {
  sequence=sample.Sequence;noteIndex=sample.NoteIndex;phase=sample.HoldHead?"hold-head":"hit";
  kind=Kinds[(int)sample.Kind];group=Groups[(int)sample.Group];timing=sample.Timing;
  offsetMs=(double)sample.OffsetMs;offsetBits=CalibratedFloatBits.Of(sample.OffsetMs);gameTime=sample.GameTime;
 }
}
public sealed class HitOffsetStatistics {
 public int count {get;private set;}
 public double? meanMs {get;private set;}
 public double m2Ms2 {get;private set;}
 public double? populationStdDevMs {get;private set;}
 public double? sampleStdDevMs {get;private set;}
 public double? minMs {get;private set;}
 public double? maxMs {get;private set;}
 public int[] histogram {get;private set;}
 public int belowRange {get;private set;}
 public int aboveRange {get;private set;}
 public HitOffsetStatistics(int n,double mean,double m2,double min,double max,int[] bins,int below,int above) {
  count=n;meanMs=n>0?(double?)mean:null;m2Ms2=m2;
  populationStdDevMs=n>0?(double?)Math.Sqrt(Math.Max(0,m2/n)):null;
  sampleStdDevMs=n>1?(double?)Math.Sqrt(Math.Max(0,m2/(n-1))):null;
  minMs=n>0?(double?)min:null;maxMs=n>0?(double?)max:null;histogram=bins;belowRange=below;aboveRange=above;
 }
}
public sealed class HitOffsetGroups {
 public HitOffsetStatistics standard {get;private set;}
 public HitOffsetStatistics ex {get;private set;}
 public HitOffsetStatistics touch {get;private set;}
 public HitOffsetStatistics touchHold {get;private set;}
 public HitOffsetGroups(HitOffsetStatistics a,HitOffsetStatistics b,HitOffsetStatistics c,HitOffsetStatistics d){standard=a;ex=b;touch=c;touchHold=d;}
}
public sealed class HitOffsetDistribution {
 public double binStartMs {get{return CalibratedHitBuffer.BinStartMs;}}
 public double binWidthMs {get{return CalibratedHitBuffer.BinWidthMs;}}
 public int binCount {get{return CalibratedHitBuffer.BinCount;}}
 public string interval {get{return "left-closed-right-open-final-inclusive";}}
 public HitOffsetGroups groups {get;private set;}
 public HitOffsetDistribution(HitOffsetStatistics a,HitOffsetStatistics b,HitOffsetStatistics c,HitOffsetStatistics d){groups=new HitOffsetGroups(a,b,c,d);}
}
public sealed class HitOffsetSnapshot {
 public int schemaVersion {get{return 1;}}
 public string source {get{return "game-calibrated-hit";}}
 public int player {get;private set;}
 public string session {get;private set;}
 public long attempt {get;private set;}
 public bool available {get;private set;}
 public string state {get;private set;}
 public long sequence {get;private set;}
 public long overwritten {get;private set;}
 public HitOffsetEvent[] events {get;private set;}
 public HitOffsetDistribution distribution {get;private set;}
 public string precision {get{return "engine-float32-not-hardware-timestamp";}}
 public bool slideOffsetsAvailable {get{return false;}}
 public bool missOffsetsAvailable {get{return false;}}
 public HitOffsetSnapshot(int p,string s,long a,bool ok,string status,long seq,long loss,HitOffsetEvent[] e,HitOffsetDistribution d){player=p;session=s;attempt=a;available=ok;state=status;sequence=seq;overwritten=loss;events=e;distribution=d;}
}
}
