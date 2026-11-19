import test from 'node:test';import assert from 'node:assert/strict';
import {drawJudgeSprites,breakFlash} from '../web/judge-sprites.mjs';
import {renderTaiko} from '../web/taiko.mjs';
const images=Object.fromEntries(['Critical_Break','Critical','Perfect','Great','Good','Miss','FastPerfect','LatePerfect','FastGreat','LateGreat','FastGood','LateGood'].map(name=>[name,{name,complete:true,naturalWidth:223,naturalHeight:82}]));
test('original Critical asset already contains CRITICAL PERFECT, side only actual FAST/LATE',()=>{
 for(const [grade,side,expected]of [['CRITICAL',null,['Critical']],['PERFECT','FAST',['FastPerfect']],['GREAT','LATE',['LateGreat']],['MISS',null,['Miss']]]){const calls=[];drawJudgeSprites({drawImage:i=>calls.push(i.name)},{grade,side},90,50,{images});assert.deepEqual(calls,expected);}
});
test('decorative parabolic note and actual sprite have separate trajectories and both seek reset',()=>{
 const chart={resolution:384,bpms:[{time:0,tick:0,bpm:120}],events:[{noteIndex:1,time:1,tick:192,lane:0,type:'Tap'}]};
 for(const age of [.04,.12,.22]){const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
 renderTaiko(ctx,chart,1+age,{showTiming:true,judgeImages:images,judgements:[{noteIndex:1,timing:7,gameTime:1}]});
 const sprite=calls.find(c=>c[0]==='drawImage');assert.ok(sprite);assert.ok(Math.abs(sprite[2]+sprite[4]/2-90)<1e-9);
 assert.ok(calls.some(c=>c[0]==='arc'&&c[1]>90&&c[2]<190&&c[3]>2));
 calls.length=0;renderTaiko(ctx,chart,1+age,{showTiming:true,judgeImages:images,effectStartTime:1+age,judgements:[{noteIndex:1,timing:7,gameTime:1}]});assert.ok(!calls.some(c=>c[0]==='drawImage'));
 }
});
test('all note kinds use TAP grade visuals regardless of break/each and retain actual direction',()=>{
 for(const type of ['Tap','Hold','TouchTap','TouchHold','Slide']){
  const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
  const chart={resolution:384,bpms:[{time:0,tick:0,bpm:120}],events:[{noteIndex:1,time:1,tick:192,lane:0,type,break:true}]};
  renderTaiko(ctx,chart,1.1,{judgeImages:images,judgements:[{noteIndex:1,timing:8,gameTime:1}]});
  assert.deepEqual(calls.filter(c=>c[0]==='drawImage').map(c=>c[1].name),['LatePerfect'],type);
 }
});
test('Type3C draws exactly one centered full-size sprite, never a separate direction label',()=>{
 for(const grade of ['PERFECT','GREAT','GOOD','CRITICAL','MISS'])for(const side of ['FAST','LATE'])for(const width of [54,110,223]){
 const calls=[];drawJudgeSprites({drawImage:(...a)=>calls.push(a)},{grade,side},90,40,{width,images});
 assert.equal(calls.length,1);assert.equal(calls[0][3],width);
 assert.equal(calls[0][1]+width/2,90);
 assert.ok(Math.abs(calls[0][2]+calls[0][4]/2-40)<1e-9);
 const base=grade[0]+grade.slice(1).toLowerCase();
 assert.equal(calls[0][0].name,['CRITICAL','MISS'].includes(grade)?base:(side==='FAST'?'Fast':'Late')+base);
 }
});

test('BREAK critical original additive layer follows two-frame pulse, never alters noncritical grade',()=>{
 for(const [age,expected]of [[0,false],[1/60,false],[2/60,true],[3/60,true],[4/60,false],[14/60,true],[.25,false],[.3,false]])assert.equal(breakFlash(age),expected);
 for(const [grade,side,isBreak,expected]of [['CRITICAL',null,true,['Critical','Critical_Break']],['CRITICAL',null,false,['Critical']],['PERFECT','FAST',true,['FastPerfect']]]){
 const calls=[],ctx={fillRect(){},fillText(){},globalCompositeOperation:'source-over',drawImage:(image,...args)=>calls.push([image.name,...args])};
 drawJudgeSprites(ctx,{grade,side},90,40,{images,width:110,age:.04,isBreak});assert.deepEqual(calls.map(c=>c[0]),expected);assert.equal(ctx.globalCompositeOperation,'source-over');
 if(calls.length===2)assert.deepEqual(calls[0].slice(1),calls[1].slice(1));
 }
});
test('mixed simultaneous normal/break slots use exact event ID for break effect',()=>{
 const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
 const c={resolution:384,bpms:[{time:0,tick:0,bpm:120}],events:[{noteIndex:1,time:1,tick:192,lane:0,type:'Tap',break:false},{noteIndex:2,time:1,tick:192,lane:2,type:'Tap',break:true}]};
 renderTaiko(ctx,c,1.04,{judgeImages:images,judgements:[{noteIndex:1,timing:7,gameTime:1}]});assert.ok(!calls.some(c=>c[0]==='drawImage'&&c[1].name==='Critical_Break'));
 calls.length=0;renderTaiko(ctx,c,1.04,{judgeImages:images,judgements:[{noteIndex:2,timing:7,gameTime:1}]});assert.ok(calls.some(c=>c[0]==='drawImage'&&c[1].name==='Critical_Break'));
});
test('noncritical BREAK draws only native Type3C sprite, never text or marker',()=>{
 for(const grade of ['PERFECT','GREAT','GOOD','MISS'])for(const side of ['FAST','LATE']){
  const calls=[],ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
  drawJudgeSprites(ctx,{grade,side},90,40,{images,isBreak:true,age:.10});
  assert.equal(calls.filter(c=>c[0]==='drawImage').length,1);
  assert.ok(!calls.some(c=>['fillText','fillRect'].includes(c[0])));
 }
});
test('mixed same-slot actual critical commits render one base plus original BREAK overlay',()=>{
 const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
 const c={resolution:384,bpms:[{time:0,tick:0,bpm:120}],events:[{noteIndex:1,time:1,tick:192,lane:0,type:'Tap',break:false},{noteIndex:2,time:1,tick:192,lane:2,type:'Tap',break:true}]};
 for(const ids of [[1,2],[2,1]]) {
  calls.length=0;
  renderTaiko(ctx,c,1.04,{judgeImages:images,judgements:ids.map(noteIndex=>({noteIndex,timing:7,gameTime:1}))});
  assert.deepEqual(calls.filter(c=>c[0]==='drawImage').map(c=>c[1].name),['Critical','Critical_Break']);
 }
});

test('missing optional judgement art renders a plain-text grade',()=>{
 const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
 drawJudgeSprites(ctx,{grade:'CRITICAL',side:null},90,40,{images:{}});
 assert.deepEqual(calls.find(c=>c[0]==='fillText'),['fillText','CRITICAL PERFECT',90,40,110]);
 assert.ok(!calls.some(c=>c[0]==='drawImage'));
});
