using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using StellarTool.Mod;

int assertions=0;
void Check(bool v,string n){if(!v)throw new Exception(n);assertions++;}
void Near(double? got,double want,string n){Check(got.HasValue&&Math.Abs(got.Value-want)<1e-8,n);}
CalibratedHitSample Hit(int index,float offset=0,double time=1,CalibratedHitGroup group=CalibratedHitGroup.Standard,int player=0,bool head=false,int timing=7)=>new(){Player=player,NoteIndex=index,OffsetMs=offset,GameTime=time,Group=group,Kind=head?CalibratedHitKind.Hold:CalibratedHitKind.Tap,HoldHead=head,Timing=timing};
var q=new CalibratedHitBuffer(0,32,2);
Check(!q.TryCommit(Hit(0)),"inactive rejects hit");
q.SetContext("A",true,32,false,0);
Check(q.TryCommit(Hit(0,-10)),"first hit");
Check(!q.TryCommit(Hit(0,20)),"same phase dedupe");
Check(q.TryCommit(Hit(0,10,1.1,head:true)),"phase identity separate");
Check(q.TryCommit(Hit(1,0,1.2)),"third hit");
var s=q.Snapshot(true,1.3);var st=s.distribution.groups.standard;
Check(s.sequence==3&&s.events.Length==2&&s.overwritten==1,"bounded ring exposes recent loss");
Check(st.count==3,"cumulative unaffected by ring overwrite");Near(st.meanMs,0,"Welford mean");Near(st.m2Ms2,200,"Welford M2");Near(st.populationStdDevMs,Math.Sqrt(200.0/3),"population sd");Near(st.sampleStdDevMs,10,"sample sd");
Check(!q.TryCommit(Hit(3,float.NaN))&&!q.TryCommit(Hit(3,float.PositiveInfinity)),"nonfinite offset rejected");
Check(!q.TryCommit(Hit(3,0,double.NaN))&&!q.TryCommit(Hit(3,0,1,player:1)),"bad clock and wrong player rejected");
Check(!q.TryCommit(Hit(-1))&&!q.TryCommit(Hit(32)),"note bounds");
Check(!q.TryCommit(Hit(3,timing:0))&&!q.TryCommit(Hit(3,timing:14))&&!q.TryCommit(Hit(3,timing:15)),"miss/unjudged no fabricated offset");
var invalid=Hit(3);invalid.Kind=(CalibratedHitKind)99;Check(!q.TryCommit(invalid),"unknown kind fail closed");
q.TryCommit(Hit(2,-150,1.3));q.TryCommit(Hit(3,150,1.4));q.TryCommit(Hit(4,-150.001f,1.5));q.TryCommit(Hit(5,150.001f,1.6));q.TryCommit(Hit(6,-145,1.7));
var b=q.Snapshot(true,1.7).distribution.groups.standard;
Check(b.histogram[0]==1&&b.histogram[1]==1&&b.histogram[59]==1&&b.belowRange==1&&b.aboveRange==1,"custom bin exact boundaries and overflow");
Check(b.histogram.Sum()+b.belowRange+b.aboveRange==b.count,"hist conservation");
Check(st.count==3&&st.histogram.Sum()==3,"published statistics immutable across new commits");
q.TryCommit(Hit(7,44,1.8,CalibratedHitGroup.Ex));q.TryCommit(Hit(8,55,1.9,CalibratedHitGroup.Touch));q.TryCommit(Hit(9,66,2,CalibratedHitGroup.TouchHold));
var g=q.Snapshot(true,2).distribution.groups;Check(g.standard.count==8&&g.ex.count==1&&g.touch.count==1&&g.touchHold.count==1,"optional scopes isolated");
Near(g.ex.meanMs,44,"EX preserved not mapped to TAP");Check(g.ex.sampleStdDevMs==null,"one sample SD unavailable");
q.SetContext(null,false,0,true,20);var frozen=q.Snapshot(true,20);
Check(frozen.available&&frozen.state=="frozen"&&frozen.session=="A"&&frozen.distribution.groups.standard.count==8,"result preserves matching attempt after chart cleared");
Check(!q.TryCommit(Hit(10,20,20)),"frozen rejects callback");
q.SetContext("B",true,32,false,0);var next=q.Snapshot(true,0);
Check(next.attempt>frozen.attempt&&next.sequence==0&&next.distribution.groups.standard.count==0,"new attempt resets");
Check(next.distribution.groups.standard.meanMs==null&&next.distribution.groups.standard.populationStdDevMs==null,"empty stats unavailable not zero-quality");
q.TryCommit(Hit(0,3,10));Check(!q.CanProbe(1,false,2),"seek rejected before next context");
Check(!q.Snapshot(true,2).available,"pending seek cannot publish mixed attempt");
q.SetContext("B",true,32,false,2);Check(q.TryCommit(Hit(0,4,2.1)),"seek new attempt resets dedupe");
Check(q.Snapshot(true,2.1).distribution.groups.standard.count==1,"seek not mixed statistics");
q.SetContext("C",true,32,false,0);q.TryCommit(Hit(0,3,10));q.CanProbe(1,false,1);
q.SetContext(null,false,0,true,1);Check(!q.Snapshot(true,1).available,"result immediately after unobserved seek refuses stale attempt");
q.SetContext("D",true,33,false,0);Check(!q.Snapshot(true,0).available,"oversized chart fail closed");
q.SetContext("D",true,32,false,0);q.TryCommit(Hit(0,3,1));q.SetContext("D",false,0,false,2);Check(!q.Snapshot(true,2).available,"selection clears live result");
q.SetContext("P",true,32,false,0);float precise=BitConverter.Int32BitsToSingle(unchecked((int)0xBDCCCCCD));q.TryCommit(Hit(0,precise));
var e=q.Snapshot(true,1).events[0];Check(e.offsetMs==(double)precise&&e.offsetBits==0xBDCCCCCD,"engine float exact promotion and bits");
var doc=JsonDocument.Parse(Json.Write(e));double round=doc.RootElement.GetProperty("offsetMs").GetDouble();
Check(round==e.offsetMs&&BitConverter.SingleToInt32Bits((float)round)==unchecked((int)e.offsetBits),"production JSON numeric R round trip");
var negzero=new CalibratedFloatBits{bits=0x80000000}.value;q.TryCommit(Hit(1,negzero,1.1));Check(q.Snapshot(true,1.1).events.Last().offsetBits==0x80000000,"negative zero preserved in bits");
Check(q.Snapshot(true,10).events.Length==0&&q.Snapshot(true,10).distribution.groups.standard.count==2,"recent expiry does not erase result stats");

// Reflection.Emit readers must preserve actual field-based native shapes.
var reader=CalibratedHitReader.Build(typeof(FakeNote),false,typeof(FakeBreak));
var holdReader=CalibratedHitReader.Build(typeof(FakeHold),true,typeof(FakeBreak));
var fake=new FakeBreak{MonitorIndex=1,NoteIndex=9,JudgeType=JudgeKind.Break,JudgeResult=Timing.FastPerfect,JudgeTimingDiffMsec=precise,NoteMng=Fixture.Notes[1]};
var read=reader(fake);Check(read.Player==1&&read.Index==9&&read.Timing==6&&read.IsBreak&&read.OffsetMs==precise&&ReferenceEquals(read.NotesManager,Fixture.Notes[1]),"native protected/enum/float field reader");
var fh=new FakeHold{JudgeResult=Timing.End,JudgeHeadResult=Timing.LateGreat};Check(holdReader(fh).Timing==10,"hold reads head grade not final End");
var current=CalibratedHitReader.CurrentNotes(typeof(FakeNotes));Check(ReferenceEquals(current(1),FakeNotes.Instances[1]),"native manager identity delegate");
var enabled=CalibratedHitReader.CurrentScoreEnabled(typeof(FakeManager),typeof(FakeSingleton<>),typeof(FakeScore));
Check(enabled(0),"live score public fields enabled");FakeManager.Scores[0].IsTrackSkip=true;Check(!enabled(0),"track skip guard");FakeManager.Scores[0].IsTrackSkip=false;FakeManager.Scores[0].IsEnable=false;Check(!enabled(0),"disabled score guard");FakeManager.Scores[0].IsEnable=true;
for(int i=0;i<1000;i++)reader(fake);
long alloc=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)reader(fake);Check(GC.GetAllocatedBytesForCurrentThread()==alloc,"typed reader allocation free");
var big=new CalibratedHitBuffer(0,30000,512);big.SetContext("large",true,30000,false,0);
for(int i=0;i<1000;i++)big.TryCommit(Hit(i,i%13,i*.001));
alloc=GC.GetAllocatedBytesForCurrentThread();for(int i=1000;i<20000;i++)big.TryCommit(Hit(i,i%13,i*.001));
Check(GC.GetAllocatedBytesForCurrentThread()==alloc,"19000 callbacks buffer allocation free");Check(big.Snapshot(true,20).distribution.groups.standard.count==20000,"all-track summary exceeds recent ring");

// Exercise the exact callback methods with cached delegates and validated readers,
// but do not install any Harmony patches in the test process.
var tt=typeof(CalibratedHitTelemetry);const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
void Set(string n,object v)=>tt.GetField(n,flags).SetValue(null,v);
Set("readers",new[]{reader,holdReader,holdReader,reader,holdReader});Set("currentNotes",(Func<int,object>)Fixture.CurrentNotes);Set("scoreEnabled",(Func<int,bool>)Fixture.ScoreEnabled);
Set("gameClock",(Func<float>)Fixture.GameClock);Set("autoPlay",(Func<bool>)Fixture.Auto);Set("advDemo",(Func<bool>)Fixture.Demo);Set("ready",true);
var before=(BeforeDelegate)tt.GetMethod("Before0",flags).CreateDelegate(typeof(BeforeDelegate));
var beforeHold=(BeforeDelegate)tt.GetMethod("Before1",flags).CreateDelegate(typeof(BeforeDelegate));
var after=(Action<object,bool,CalibratedHitTelemetry.Probe>)tt.GetMethod("After",flags).CreateDelegate(typeof(Action<object,bool,CalibratedHitTelemetry.Probe>));
var suppress=(Action)tt.GetMethod("SuppressBegin",flags).CreateDelegate(typeof(Action));var release=(Action)tt.GetMethod("SuppressEnd",flags).CreateDelegate(typeof(Action));
Fixture.Clock=1000;CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false);CalibratedHitTelemetry.Snapshot(1,"cb1",true,20,false);
var fn=new FakeNote{NoteMng=Fixture.Notes[0],JudgeType=JudgeKind.Tap,JudgeResult=Timing.End,JudgeTimingDiffMsec=precise};
void Invoke(FakeNote n,bool success=true){before(n,out var p);n.JudgeResult=Timing.Critical;after(n,success,p);}
Invoke(fn);var cb=CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false);
Check(cb.sequence==1&&cb.events[0].offsetBits==0xBDCCCCCD,"actual postfix uses corrected field unchanged");
fn.NoteIndex=1;fn.JudgeResult=Timing.End;Invoke(fn,false);Check(CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).sequence==1,"failed judge not sampled");
fn.NoteIndex=2;fn.JudgeResult=Timing.Critical;Invoke(fn);Check(CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).sequence==1,"already judged source not sampled");
fn.NoteIndex=3;fn.JudgeResult=Timing.End;Fixture.AutoPlay=true;Invoke(fn);Fixture.AutoPlay=false;
fn.NoteIndex=4;fn.JudgeResult=Timing.End;Fixture.Skipped=true;Invoke(fn);Fixture.Skipped=false;
fn.NoteIndex=5;fn.JudgeResult=Timing.End;Fixture.AdvDemo=true;Invoke(fn);Fixture.AdvDemo=false;
fn.NoteIndex=6;fn.JudgeResult=Timing.End;suppress();Invoke(fn);release();
fn.NoteIndex=7;fn.JudgeResult=Timing.End;fn.NoteMng=new object();Invoke(fn);fn.NoteMng=Fixture.Notes[0];
Check(CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).sequence==1,"auto/skip/demo/chain/wrong manager all excluded");
fn.NoteIndex=8;fn.JudgeResult=Timing.End;before(fn,out var miss);fn.JudgeResult=Timing.TooLate;after(fn,true,miss);
Check(CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).sequence==1,"miss actual callback has no fake numeric offset");
var head=new FakeHold{NoteMng=Fixture.Notes[0],NoteIndex=9,JudgeType=JudgeKind.Tap,JudgeHeadResult=Timing.End,JudgeResult=Timing.End,JudgeTimingDiffMsec=17.3f};
beforeHold(head,out var hp);head.JudgeHeadResult=Timing.LatePerfect;after(head,true,hp);cb=CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false);
Check(cb.events.Last().phase=="hold-head"&&cb.events.Last().timing==8&&cb.events.Last().kind=="hold","hold head event independent of final grade");
fn.MonitorIndex=1;fn.NoteMng=Fixture.Notes[1];fn.NoteIndex=0;fn.JudgeResult=Timing.End;Invoke(fn);
Check(CalibratedHitTelemetry.Snapshot(1,"cb1",true,20,false).sequence==1&&CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).sequence==2,"players independent");
fn.MonitorIndex=0;fn.NoteMng=Fixture.Notes[0];fn.IsExNote=true;fn.JudgeType=JudgeKind.ExTap;fn.NoteIndex=10;fn.JudgeResult=Timing.End;Invoke(fn);
Check(CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).distribution.groups.ex.count==1,"EX event isolated");
var beforeTouch=(BeforeDelegate)tt.GetMethod("Before3",flags).CreateDelegate(typeof(BeforeDelegate));
var beforeTouchHold=(BeforeDelegate)tt.GetMethod("Before4",flags).CreateDelegate(typeof(BeforeDelegate));
fn.IsExNote=false;fn.JudgeType=JudgeKind.Touch;fn.NoteIndex=11;fn.JudgeResult=Timing.End;
beforeTouch(fn,out var tp);fn.JudgeResult=Timing.LatePerfect;after(fn,true,tp);
head.JudgeType=JudgeKind.Touch;head.NoteIndex=12;head.JudgeHeadResult=Timing.End;
beforeTouchHold(head,out var thp);head.JudgeHeadResult=Timing.Critical;after(head,true,thp);
var optional=CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).distribution.groups;
Check(optional.touch.count==1&&optional.touchHold.count==1&&optional.standard.count==2,"Touch and TouchHold separate from ordinary window");
fn.NoteIndex=13;fn.JudgeResult=Timing.End;before(fn,out var badType);fn.JudgeResult=Timing.Critical;after(fn,true,badType);
Check(CalibratedHitTelemetry.Snapshot(0,"cb0",true,30000,false).sequence==5,"unsupported hook/type combination rejected");
fn.IsExNote=false;fn.JudgeType=JudgeKind.Tap;
for(int i=100;i<1000;i++){fn.NoteIndex=i;fn.JudgeResult=Timing.End;Invoke(fn);}
alloc=GC.GetAllocatedBytesForCurrentThread();for(int i=1000;i<11000;i++){fn.NoteIndex=i;fn.JudgeResult=Timing.End;Invoke(fn);}
Check(GC.GetAllocatedBytesForCurrentThread()==alloc,"10000 exact healthy callback pairs allocation free");

// Inspect installed SDEZ metadata without loading Unity or executing game code.
var dll=Environment.GetEnvironmentVariable("STELLAR_TEST_GAME_ASSEMBLY");
bool metadataChecked=false;
if(!string.IsNullOrWhiteSpace(dll)) {
 metadataChecked=true;
 using var fs=File.OpenRead(dll);using var pe=new PEReader(fs);var md=pe.GetMetadataReader();
 TypeDefinition Find(string ns,string name)=>md.TypeDefinitions.Select(md.GetTypeDefinition).First(t=>md.GetString(t.Namespace)==ns&&md.GetString(t.Name)==name);
 bool Field(TypeDefinition t,string n)=>t.GetFields().Any(h=>md.GetString(md.GetFieldDefinition(h).Name)==n);
 bool Method(TypeDefinition t,string n)=>t.GetMethods().Any(h=>md.GetString(md.GetMethodDefinition(h).Name)==n);
 var baseType=Find("Monitor","NoteBase");foreach(var n in new[]{"MonitorIndex","NoteIndex","JudgeType","JudgeResult","JudgeTimingDiffMsec","IsExNote","NoteMng"})Check(Field(baseType,n),"installed native field "+n);
 foreach(var p in new[]{("NoteBase","Judge"),("HoldNote","JudgeHoldHead"),("BreakHoldNote","JudgeHoldHead"),("TouchNoteB","Judge"),("TouchHoldC","JudgeHoldHead")})Check(Method(Find("Monitor",p.Item1),p.Item2),"installed exact hook "+p);
 var score=Find("Manager","GameScoreList");Check(Field(score,"IsEnable")&&Field(score,"IsTrackSkip"),"installed live-score guard is fields, not properties");
}
Console.WriteLine($"{assertions} calibrated hit assertions passed; 10,000 exact callback pairs + 19,000 buffer commits allocated 0 bytes; {(metadataChecked ? "optional game metadata checked" : "optional game metadata skipped")}.");

public delegate void BeforeDelegate(object instance,out CalibratedHitTelemetry.Probe state);
public enum Timing {TooFast,FastGood,FastGreat3rd,FastGreat2nd,FastGreat,FastPerfect2nd,FastPerfect,Critical,LatePerfect,LatePerfect2nd,LateGreat,LateGreat2nd,LateGreat3rd,LateGood,TooLate,End}
public enum JudgeKind {Tap,HoldOut,SlideOut,Touch,ExTap,Break,End}
public class FakeNote {public int MonitorIndex,NoteIndex;public JudgeKind JudgeType;public Timing JudgeResult=Timing.End;public float JudgeTimingDiffMsec;public bool IsExNote;public object NoteMng;}
public class FakeBreak:FakeNote{}
public class FakeHold:FakeNote {public Timing JudgeHeadResult=Timing.End;}
public static class Fixture {public static readonly object[] Notes={new object(),new object()};public static float Clock;public static bool AutoPlay,AdvDemo,Skipped;public static object CurrentNotes(int p)=>Notes[p];public static bool ScoreEnabled(int p)=>!Skipped;public static float GameClock()=>Clock;public static bool Auto()=>AutoPlay;public static bool Demo()=>AdvDemo;}
public class FakeNotes {public static readonly FakeNotes[] Instances={new(),new()};public static FakeNotes Instance(int p)=>Instances[p];}
public class FakeScore {public bool IsEnable=true,IsTrackSkip;}
public class FakeManager {public static readonly FakeScore[] Scores={new(),new()};public FakeScore GetGameScore(int p,int track=-1)=>Scores[p];}
public class FakeSingleton<T> where T:new() {public static T Instance {get;}=new();}
