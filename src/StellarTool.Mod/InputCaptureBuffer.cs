using System;
using System.Threading;
namespace StellarTool.Mod {
public struct InputSample {
 public ulong Touch0, Touch1; public int Key0, Key1; public bool Available;
 public long Sequence, Ticks;
 public bool SameState(InputSample x) { return Touch0==x.Touch0&&Touch1==x.Touch1&&Key0==x.Key0&&Key1==x.Key1&&Available==x.Available; }
}
// Single game-thread producer / single worker consumer. Fixed preallocated storage.
// Publish has no locks, waits, networking, serialization or managed allocation.
public sealed class InputCaptureBuffer {
 readonly InputSample[] ring; readonly int mask; int read,write,version;
 InputSample latest,last; bool hasLast; long sequence,dropped;
 public InputCaptureBuffer(int capacity=256) {
  if(capacity<2||(capacity&(capacity-1))!=0)throw new ArgumentException("Power of two required");
  ring=new InputSample[capacity];mask=capacity-1;
 }
 public void Publish(ulong a,ulong b,int x,int y,bool ready,long at) {
  var item=new InputSample{Touch0=ready?a&((1UL<<34)-1):0,Touch1=ready?b&((1UL<<34)-1):0,Key0=ready?x&255:0,Key1=ready?y&255:0,Available=ready,Ticks=at};
  bool changed=!hasLast||!last.SameState(item);
  if(changed)sequence++;
  item.Sequence=sequence;
  Interlocked.Increment(ref version);latest=item;Interlocked.Increment(ref version);
  if(!changed)return;
  hasLast=true;last=item;
  int current=write,next=(current+1)&mask;
  if(next==Volatile.Read(ref read)){Interlocked.Increment(ref dropped);return;}
  ring[current]=item;Volatile.Write(ref write,next);
 }
 public bool TryDequeue(out InputSample item) {
  int current=read;
  if(current==Volatile.Read(ref write)){item=default(InputSample);return false;}
  item=ring[current];Volatile.Write(ref read,(current+1)&mask);return true;
 }
 public bool TryRead(out InputSample item) {
  for(int attempt=0;attempt<3;attempt++) {
   int before=Volatile.Read(ref version);if((before&1)!=0)continue;
   var copy=latest;Thread.MemoryBarrier();
   if(before==Volatile.Read(ref version)){item=copy;return before!=0;}
  }
  item=default(InputSample);return false;
 }
 public long Dropped { get { return Interlocked.Read(ref dropped); } }
}
}
