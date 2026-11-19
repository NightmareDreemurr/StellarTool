import {drawTaikoTiming} from './timing-changes.mjs';
import {rhythmLabelVisibility} from './rhythm-labels.mjs';
import {showTaikoEffect} from './practice-timeline.mjs';
import {drawJudgeSprites} from './judge-sprites.mjs';
import {judgeFlight,judgeMeaning} from './judgements.mjs';
import {detectSweeps} from './sweep.mjs';
import {rhythmSlots,splashRoles,beat,beatGrid} from './model.mjs';
import {tickAt,timeAtTick} from './score.mjs';
// Stellar rhythm_analysis.dart:474 and MayobotUI maimai-chart-rhythm-bar.tsx:85.
export const rhythmPalette=Object.freeze({'4':'#000086','8':'#fe0000','16':'#0473f6','32':'#2cfcfc','64':'#b8f6ff','6':'#005b06','12':'#fe700a','24':'#72ff34','48':'#d6ff38','5':'#5e0437','10':'#8e2872','20':'#fd4291','7':'#492785','14':'#7c3ecd','9':'#376d6d','18':'#049d6f'});
const cache=new WeakMap(),noteIds=new WeakMap();
const gcd=(a,b)=>b?gcd(b,a%b):a;
export function rhythmValue(whole,interval){
 if(!(whole>0&&interval>0))return {label:null,diamond:false};
 const g=gcd(whole,interval),p=whole/g,q=interval/g;
 if(q===1)return {label:String(p),diamond:false};
 const half=p/2;
 if(q===3&&Number.isInteger(half)&&half>0&&(half&(half-1))===0)return {label:half+'.',diamond:true};
 return {label:p+'/'+q,diamond:false};
}
export function buildTaiko(chart){
 const slots=rhythmSlots(chart.events||[]),whole=chart.resolution||384;
 const chains=detectSweeps((chart.events||[]).map(e=>({...e,timeMs:e.time*1000,type:/touch/i.test(e.type)?'touch':e.type})));
 const roles=new Map(chains.map(x=>[x.tick,x])),splashes=splashRoles(chart.events||[],chains);
 const notes=slots.map((s,i)=>{
  const prev=i?slots[i-1]:null,next=slots[i+1],before=prev?s.tick-prev.tick:null,after=next?next.tick-s.tick:null;
  let interval=null;
  if((before!==null&&before<=whole/2)||(after!==null&&after<=whole/2)) interval=prev&&next?(next.time-s.time<s.time-prev.time?after:before):(before??after);
  const value=rhythmValue(whole,interval),outer=s.lanes.size,touch=s.touchCount,splash=splashes.get(s.tick)||0,role=roles.get(s.tick)?.role||'solo';
  const each=outer>=2||touch>=2||(outer>0&&touch>0);
  const size=outer>=2?(role==='tail'&&splash!==1?'chordTail':'full'):outer===1&&touch===0?(role==='tail'?'tail':'full'):(splash===2?'tail':'full');
  return {...s,...value,each,role,splash,size,fill:value.label===null?'#27272a':rhythmPalette[value.label.replace(/\.$/,'')]||'#9ca3af',stroke:s.break?'#fb923c':each?'#facc15':'#ffffff'};
 });
 for(let i=0;i<notes.length;i++){
  const n=notes[i],neighbor=notes[i-1]?.size==='chordTail'||notes[i+1]?.size==='chordTail';
  n.radiusRatio=n.size==='full'?1:n.size==='tail'?(n.break?.75:.5):neighbor?(n.break?.75:.7):1;
 }
 const visible=rhythmLabelVisibility(notes);notes.forEach((n,i)=>n.showLabel=visible[i]);
 return notes;
}
export function taikoGeometry(chart,time,{width=800,height=100,showTiming=false,judgeX=showTiming?90:26}={}){
 const noteBottom=height*.8,radius=noteBottom*5/16,ringRadius=radius,whole=chart.resolution||384;
 judgeX=Math.max(judgeX,radius+4);
 const sixteenth=2*radius,pxPerTick=sixteenth*16/whole,nowTick=tickAt(chart,time);
 return {radius,ringRadius,sixteenth,pxPerTick,nowTick,judgeX,noteY:noteBottom/2,noteBottom,valueY:height*.9,barY:height*.75,x:tick=>judgeX+(tick-nowTick)*pxPerTick};
}
export function renderTaiko(ctx,chart,time,{width=800,height=100,top=150,showTiming=false,judgeX=showTiming?90:26,linecolor='#000000',bpmcolor='#ec2127',barcolor='#000000',effectStartTime=-Infinity,effectsEnabled=true,judgements=[],judgeImages}={}){
 let notes=cache.get(chart);if(!notes){notes=buildTaiko(chart);cache.set(chart,notes);}
 const geometry=taikoGeometry(chart,time,{width,height,judgeX,showTiming}),fullRadius=geometry.radius,ringRadius=geometry.ringRadius,y=top+geometry.noteY;
 judgeX=geometry.judgeX;
 ctx.save();ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='600 11px MiSans,sans-serif';
 const current=beat(chart,time);
 const rightTick=geometry.nowTick+(width-judgeX+fullRadius)/geometry.pxPerTick,endTime=timeAtTick(chart,rightTick);
 drawTaikoTiming(ctx,chart,time,endTime,{x:geometry.x,tickAt:t=>tickAt(chart,t),judgeX,width,top,height,after:effectStartTime});
 if(showTiming){ctx.textAlign='left';ctx.font='600 14px MiSans,sans-serif';ctx.fillStyle=bpmcolor;ctx.fillText(String(current.bpm),5,top+22);ctx.fillStyle=barcolor;ctx.fillText(`${current.numerator}/${current.denominator}`,5,top+44);}
 // Two regions: 80% note area (including bar-label buffer), 20% values.
 ctx.strokeStyle=linecolor;ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(0,top+geometry.noteBottom);ctx.lineTo(width,top+geometry.noteBottom);ctx.stroke();
 for(const p of beatGrid(chart,Math.max(0,time),endTime)){
  const gx=geometry.x(Number.isFinite(p.tick)?p.tick:tickAt(chart,p.time));
  if(gx<0||gx>width)continue;
  if(p.beat===1){
   // Moving bar lines span the note area, with a geometric hole at the ring.
   const dx=gx-judgeX,ring=ringRadius+3;
   ctx.strokeStyle=linecolor;ctx.lineWidth=1;ctx.globalAlpha=.45;ctx.beginPath();
   if(Math.abs(dx)<ring){const half=Math.sqrt(ring*ring-dx*dx);ctx.moveTo(gx,top);ctx.lineTo(gx,Math.max(top,y-half));ctx.moveTo(gx,Math.min(top+geometry.noteBottom,y+half));ctx.lineTo(gx,top+geometry.noteBottom);}
   else{ctx.moveTo(gx,top);ctx.lineTo(gx,top+geometry.noteBottom);}
   ctx.stroke();ctx.globalAlpha=1;ctx.textAlign='left';ctx.font='600 10px MiSans,sans-serif';ctx.fillStyle=linecolor;ctx.fillText(String(p.bar),gx+4,top+geometry.barY);
   continue;
  }
  const dotY=y,ring=ringRadius+3;
  if(Math.hypot(gx-judgeX,dotY-y)>ring+2){ctx.fillStyle=linecolor;ctx.globalAlpha=.65;ctx.beginPath();ctx.arc(gx,dotY,1.8,0,Math.PI*2);ctx.fill();ctx.globalAlpha=1;}
 }
 if(showTiming)for(const change of [...(chart.bpms||[]),...(chart.meters||[])]){
  const cx=geometry.x(Number.isFinite(change.tick)?change.tick:tickAt(chart,change.time));if(cx<judgeX||cx>width)continue;
  ctx.fillStyle=change.bpm?bpmcolor:barcolor;ctx.fillText(change.bpm?String(change.bpm):`${change.numerator}/${change.denominator}`,cx,top+16);
 }
 ctx.textAlign='center';ctx.font=`600 ${Math.max(12,Math.min(22,height*.16))}px MiSans,sans-serif`;
 // Keep the judgement circle distinct from the semantic note outlines.
 ctx.strokeStyle=linecolor;ctx.lineWidth=2;ctx.beginPath();ctx.arc(judgeX,y,ringRadius,0,Math.PI*2);ctx.stroke();
 // Unjudged chart notes only scroll. Crossing the timeline never fabricates a hit.
 const drawNote=(n,nx,ny,r,alpha)=>{
  ctx.globalAlpha=alpha;ctx.fillStyle=n.fill;ctx.strokeStyle=n.stroke;ctx.lineWidth=n.break||n.each?2.5:1.5;
  ctx.beginPath();if(n.diamond){ctx.moveTo(nx,ny-r);ctx.lineTo(nx+r,ny);ctx.lineTo(nx,ny+r);ctx.lineTo(nx-r,ny);ctx.closePath();}else ctx.arc(nx,ny,r,0,Math.PI*2);ctx.fill();ctx.stroke();
  if(n.break&&n.each){ctx.strokeStyle='#facc15';ctx.lineWidth=1.5;ctx.beginPath();ctx.arc(nx,ny,r+3*alpha,0,Math.PI*2);ctx.stroke();}
 };
 // Decorative note flight follows chart time, never fabricates a grade.
 for(const n of notes){
  const age=time-n.time,x=geometry.x(n.tick);
  if(x>width+fullRadius+4||age>=.28||(age>=0&&!showTaikoEffect(n.time,time,{effectStartTime,effectsEnabled})))continue;
  const p=age<0?0:age/.28,nx=age<0?x:judgeX+50*p,ny=y+height*(-1.36*p+.68*p*p);
  drawNote(n,nx,ny,fullRadius*n.radiusRatio*(1-p),1-p);
  if(age<0&&n.label&&n.showLabel){ctx.globalAlpha=1;ctx.fillStyle=linecolor;ctx.fillText(n.label,x,top+geometry.valueY);}
 }
 // Exact note IDs include connected-slide root IDs and hold tail judgement.
 // Build visual lookup once per chart; never guess a match from lane/time.
 let byId=noteIds.get(chart);if(!byId){
  const byTick=new Map(notes.map(n=>[n.tick,n]));byId=new Map();
  for(const e of chart.events||[])if(Number.isInteger(e.noteIndex)&&!e.connected&&byTick.has(e.tick))byId.set(e.noteIndex,{...byTick.get(e.tick),judgementBreak:e.break===true});
  noteIds.set(chart,byId);
 }
 // Actual grade sprites rise vertically from the ring, independently of notes.
 // Exact simultaneous commits share one sprite only if their grade/side agree.
 const visible=new Map();
 if(effectsEnabled)for(const event of judgements){
  if(event.gameTime<=effectStartTime)continue;
  const age=time-event.gameTime,motion=judgeFlight(age,height),n=byId.get(event.noteIndex),meaning=judgeMeaning(event.timing);
  if(!motion||!n||!meaning)continue;
  const isBreak=n.judgementBreak&&meaning.grade==='CRITICAL';
  const key=event.gameTime+':'+meaning.grade+':'+meaning.side;
  const existing=visible.get(key);
  if(existing){existing.isBreak||=isBreak;continue;}
  visible.set(key,{motion,meaning,age,isBreak});
 }
 for(const {motion,meaning,age,isBreak} of visible.values())
  drawJudgeSprites(ctx,meaning,judgeX,y-motion.rise,{alpha:motion.alpha,width:Math.min(judgeX*2-4,height*1.1)*motion.scale,images:judgeImages,isBreak,age});
 ctx.restore();return notes;
}
