// Mirrors Stellar App corner_indicators.dart and rhythm_analysis.dart.
// Chart times are in seconds here; Flutter stores milliseconds.
const memo=new WeakMap();
export const CORNER_DURATION=.6,TAIKO_DURATION=1.4;
export const CORNER_TIMING_COLOR=Object.freeze({1:'#ff2626','-1':'#26ff26',2:'#ffd926'});
export const TAIKO_TIMING_COLOR=Object.freeze({1:'#ef4444','-1':'#22c55e',2:'#facc15'});
export function timingChanges(chart){
 const old=memo.get(chart);if(old)return old;
 const byPosition=new Map(), tempos=chart.bpms||[],meters=chart.meters||[];
 const entry=p=>{const key=Number.isFinite(p.tick)?'k'+p.tick:'t'+p.time;
  let event=byPosition.get(key);if(!event){event={time:p.time,tick:p.tick,bpm:0,cornerBpm:0,meter:false};byPosition.set(key,event);}return event;};
 for(let i=1;i<tempos.length;i++){
  const p=tempos[i],d=p.bpm-tempos[i-1].bpm;
  if(!Number.isFinite(p.time)||!Number.isFinite(d)||d===0)continue;
  const event=entry(p);event.bpm=Math.sign(d);event.cornerBpm=Math.abs(d)>.001?event.bpm:0;
 }
 for(let i=1;i<meters.length;i++){
  const p=meters[i],prev=meters[i-1];
  if(Number.isFinite(p.time)&&(p.numerator!==prev.numerator||p.denominator!==prev.denominator))entry(p).meter=true;
 }
 const events=[...byPosition.values()].sort((a,b)=>a.time-b.time);memo.set(chart,events);return events;
}
export function cornerTimingCue(chart,time,after=-Infinity){
 const events=timingChanges(chart);
 for(let i=upperBound(events,time)-1;i>=0;i--){
  const e=events[i],age=time-e.time;
  if(age<0)continue;if(age>=CORNER_DURATION||e.time<=after)break;
  const kind=e.cornerBpm||(e.meter?2:0);if(kind)return {kind,amount:1-age/CORNER_DURATION};
 }
 return null;
}
function upperBound(events,time){
 let lo=0,hi=events.length;
 while(lo<hi){const mid=(lo+hi)>>>1;if(events[mid].time<=time)lo=mid+1;else hi=mid;}
 return lo;
}
export function drawTaikoTiming(ctx,chart,time,endTime,{x,tickAt,judgeX,width,top,height,after=-Infinity}){
 const events=timingChanges(chart);
 const begin=upperBound(events,time-TAIKO_DURATION);
 for(let i=begin;i<events.length&&events[i].time<=endTime;i++){
  const e=events[i],kind=e.meter?2:e.bpm,color=TAIKO_TIMING_COLOR[kind];
  const start=x(Number.isFinite(e.tick)?e.tick:tickAt(e.time)),end=x(tickAt(e.time+TAIKO_DURATION));
  const left=Math.max(judgeX,start),right=Math.min(width,end);
  if(right>left&&end>start){
   const gradient=ctx.createLinearGradient(start,0,end,0);
   gradient.addColorStop(0,color+'8c');gradient.addColorStop(1,color+'00');
   ctx.fillStyle=gradient;ctx.fillRect(left,top,right-left,height);
  }
  const age=time-e.time;
  if(age>=0&&age<TAIKO_DURATION&&e.time>after){
   ctx.globalAlpha=.55*Math.sqrt(1-age/TAIKO_DURATION);
   ctx.fillStyle=color;ctx.fillRect(0,top,judgeX,height);ctx.globalAlpha=1;
  }
 }
}
