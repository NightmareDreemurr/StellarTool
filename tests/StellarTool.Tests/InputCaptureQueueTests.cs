using StellarTool.Mod;
namespace StellarTool.Tests;
internal static class InputCaptureQueueTests {
 public static void Run(){
  int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception("input queue: "+message);checks++;}
  var q=new InputCaptureBuffer(8);
  q.Publish(0,0,0,0,true,1);q.Publish(0,0,1,0,true,2);q.Publish(0,0,0,0,true,3);
  Check(q.TryDequeue(out var e)&&e.Sequence==1&&e.Key0==0,"initial");
  Check(q.TryDequeue(out e)&&e.Sequence==2&&e.Key0==1,"short press retained");
  Check(q.TryDequeue(out e)&&e.Sequence==3&&e.Key0==0,"short release retained");
  q.Publish(0,0,0,0,true,4);Check(!q.TryDequeue(out _),"unchanged sample not enqueued");
  Check(q.TryRead(out e)&&e.Ticks==4&&e.Sequence==3,"heartbeat latest timestamp");
  q=new InputCaptureBuffer(4);
  for(int i=0;i<10;i++)q.Publish(0,0,i&1,0,true,i+1);
  Check(q.Dropped==7,"bounded overflow counts exact losses");
  Check(q.TryRead(out e)&&e.Sequence==10&&e.Key0==1,"overflow keeps authoritative latest");
  long floor=e.Sequence;while(q.TryDequeue(out e))Check(e.Sequence<=floor,"reset can discard queued history");
  q.Publish(0,0,0,0,true,11);Check(q.TryDequeue(out e)&&e.Sequence>floor,"future edges survive reset cutoff");
  q.Publish(ulong.MaxValue,ulong.MaxValue,255,255,false,12);Check(q.TryRead(out e)&&!e.Available&&e.Touch0==0&&e.Key0==0,"unavailable clears masks");
  // Independent lifetime queues mean an old worker cannot consume the new session.
  var other=new InputCaptureBuffer();other.Publish(1,0,1,0,true,99);while(q.TryDequeue(out _)){}
  Check(other.TryDequeue(out e)&&e.Ticks==99,"lifetime isolated");
  q=new InputCaptureBuffer();for(int i=0;i<10000;i++){q.Publish(0,0,i&1,0,true,i+1);q.TryDequeue(out _);}
  var allocated=GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++){q.Publish(0,0,i&1,0,true,i+1);q.TryDequeue(out _);}
  Check(GC.GetAllocatedBytesForCurrentThread()==allocated,"zero capture and dequeue allocation");
  Console.WriteLine(checks+" production input queue assertions passed");
 }
}
