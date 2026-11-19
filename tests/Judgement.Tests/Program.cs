using StellarTool.Mod;
int count=0;void Check(bool yes,string name){if(!yes)throw new Exception(name);count++;}
var q=new CommittedJudgeBuffer(2);var critical=new CommittedJudge{Judged=true,Timing=7,Type=0};
Check(!q.Commit(true,critical,0,1),"already committed not published");
Check(!q.Commit(false,new CommittedJudge{Judged=false},0,1),"disabled/rejected original not published");
Check(!q.Commit(false,new CommittedJudge{Judged=true,Timing=14,Skipped=true},0,1),"skip does not invent mass misses");
Check(q.Commit(false,critical,0,1),"real critical");
Check(q.Commit(false,new CommittedJudge{Judged=true,Timing=14,Type=2},1,1.1),"post-original final timing used");
Check(q.Commit(false,critical,2,1.2,true),"auto identified");var events=q.Recent(1.3);
Check(events.Length==2&&events[0].Timing==14&&events[1].AutoPlay,"bounded ordered actual events");
Check(q.Overwritten==1,"recent overwritten reported");
Check(q.Recent(3).Length==0,"stale history expires");
q.Clear();Check(q.Sequence==0&&q.Recent(3).Length==0,"seek reset");
for(int i=0;i<10000;i++)q.Commit(false,critical,i,1);
long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)q.Commit(false,critical,i,1);
Check(GC.GetAllocatedBytesForCurrentThread()==before,"commit path zero allocations");
var read=ActualJudgeReader.Build(typeof(FakeScore));var fake=new FakeScore();
var got=read(fake,0);Check(got.Judged&&got.Timing==14&&got.Type==3&&!got.Skipped,"typed reader returns final committed struct without enum boxing");
fake.IsTrackSkip=true;Check(read(fake,0).Skipped,"skip guard read");
for(int i=0;i<1000;i++)read(fake,0);before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)read(fake,0);
Check(GC.GetAllocatedBytesForCurrentThread()==before,"typed accessor zero allocations");
var fieldRead=ActualJudgeReader.Build(typeof(RealShapeScore));var fieldScore=new RealShapeScore();
Check(!fieldRead(fieldScore,0).Skipped,"actual SDEZ public field shape binds");
fieldScore.IsTrackSkip=true;Check(fieldRead(fieldScore,0).Skipped,"actual field mutation observed");
for(int i=0;i<1000;i++)fieldRead(fieldScore,0);before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)fieldRead(fieldScore,0);
Check(GC.GetAllocatedBytesForCurrentThread()==before,"field accessor zero allocations");
try{ActualJudgeReader.Build(typeof(BadShapeScore));throw new Exception("bad shape accepted");}catch(InvalidOperationException){count++;}
Console.WriteLine(count+" judgement buffer assertions passed; 10000 commits zero allocations");

public enum Kind {Tap=0,Slide=3}
public struct FakeJudge {public bool Judged;public int Timing;public Kind Type;}
public class FakeScore {public bool IsTrackSkip {get;set;} public FakeJudge GetScoreAt(int index)=>new FakeJudge{Judged=true,Timing=14,Type=Kind.Slide};}

public class RealShapeScore {public bool IsTrackSkip;public bool IsEnable=true;public FakeJudge GetScoreAt(int index)=>new FakeJudge{Judged=true,Timing=7,Type=Kind.Tap};}
public class BadShapeScore {public int IsTrackSkip;public FakeJudge GetScoreAt(int index)=>default;}
