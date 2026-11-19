import test from 'node:test';import assert from 'node:assert/strict';
import {practiceTimeline,practiceFrame,cornerReady,showTaikoEffect} from '../web/practice-timeline.mjs';
const packet=(time,extra={})=>({state:'playing',sessionId:'a',time,paused:false,chart:{events:[]},...extra});
test('normal polling and interpolation do not reset; same-session forward/back seeks do',()=>{
 const p=practiceTimeline();p.accept(packet(10),0);assert.equal(p.consumeReset(),true);assert.equal(p.consumeReset(),false);
 p.accept(packet(10.1),100);assert.equal(p.consumeReset(),false);
 p.accept(packet(35),200);assert.equal(p.consumeReset(),true);assert.equal(p.effectStartTime,35);
 p.accept(packet(12),300);assert.equal(p.consumeReset(),true);assert.equal(p.effectStartTime,12);
 p.accept(packet(12.1),400);assert.equal(p.consumeReset(),false);
});
test('paused new session resumes Corner once chart available without waiting for unpause',()=>{
 const p=practiceTimeline();p.accept(packet(30),0);p.consumeReset();
 const waiting=packet(5,{sessionId:'seek',paused:true,chart:null});
 p.accept(waiting,100);assert.equal(p.consumeReset(),true);assert.equal(cornerReady(waiting),false);
 const ready=packet(5,{sessionId:'seek',paused:true});p.accept(ready,200);
 assert.equal(p.consumeReset(),true);assert.equal(cornerReady(ready),true);
 p.accept(ready,300);assert.equal(p.consumeReset(),false);
});
test('discontinuity suppresses skipped/recent old flights, not future genuinely crossed notes',()=>{
 const p=practiceTimeline();p.accept(packet(20),0);p.consumeReset();
 const options={effectStartTime:p.effectStartTime};
 assert.equal(showTaikoEffect(19.9,20,options),false);
 assert.equal(showTaikoEffect(20,20,options),false);
 assert.equal(showTaikoEffect(20.1,20.15,options),true);
 assert.equal(showTaikoEffect(20.1,20.15,{...options,effectsEnabled:false}),false);
 assert.equal(showTaikoEffect(20.1,20.5,options),false);
});
test('pause/resume and reconnect clear old frame; ordinary long receipt gap is not a seek',()=>{
 const p=practiceTimeline();p.accept(packet(1),0);p.consumeReset();
 p.accept(packet(2),1000);assert.equal(p.consumeReset(),false);
 p.accept(packet(2,{paused:true}),1100);assert.equal(p.consumeReset(),true);
 p.accept(packet(2,{paused:false}),1200);assert.equal(p.consumeReset(),true);
 p.accept({state:'disconnected'},1300);assert.equal(p.consumeReset(),true);
 p.accept(packet(4),1400);assert.equal(p.consumeReset(),true);
 assert.equal(cornerReady(packet(NaN)),false);
});
test('rapid alternating seeks in one cached chart clear every discontinuity without replay',()=>{
 const p=practiceTimeline();let at=0;
 for(const t of [50,5,42,7,44,8]){
  p.accept(packet(t),at);at+=100;
  assert.equal(p.consumeReset(),true);
  assert.equal(p.effectStartTime,t);
  assert.equal(showTaikoEffect(t-.05,t,{effectStartTime:p.effectStartTime}),false);
 }
 p.accept(packet(8.1),at);assert.equal(p.consumeReset(),false);
});

import {playbackRead} from '../web/practice-timeline.mjs';
test('hung chart read is aborted and late older value cannot overwrite the next successful read',async()=>{
 let timeout,signal,resolveOld,current='old';const env={AbortController,setTimeout:cb=>(timeout=cb,1),clearTimeout:()=>{}};
 const slow=playbackRead(s=>{signal=s;return new Promise(resolve=>resolveOld=resolve);},{env});
 await Promise.resolve();timeout();
 await assert.rejects(slow,{name:'TimeoutError'});assert.equal(signal.aborted,true);
 current=await playbackRead(()=>Promise.resolve('new-chart'),{env});
 resolveOld('stale-chart');await Promise.resolve();assert.equal(current,'new-chart');
});
test('Corner holds and rhythm shows backdrop through paused rapid seeks, then both resume',()=>{
  const timeline=practiceTimeline();
  function expect(snapshot,at,loading,corner,rhythm){
    timeline.accept(snapshot,at);
    assert.deepEqual(practiceFrame(snapshot,{chartLoading:loading,reset:timeline.consumeReset()}),{corner,rhythm});
  }
  let at=0;
  for(const [time,id] of [[30,'start'],[4,'rw'],[45,'ff'],[9,'rw-2']]){
    const waiting=packet(time,{sessionId:id,paused:true,chart:null});
    expect(waiting,at+=100,true,'hold','backdrop');
    expect(waiting,at+=100,false,'hold','backdrop'); // timed out; chart will be retried
    const ready={...waiting,chart:{events:[]}};
    expect(ready,at+=100,false,'hold','backdrop'); // discard prior canvas effects exactly once
    expect(ready,at+=100,false,'update','chart'); // resumes even paused
  }
  expect({state:'select'},at+=100,false,'hide','idle');
  expect({state:'preparing'},at+=100,false,'hide','idle');
  expect({state:'disconnected'},at+=100,false,'hide','idle');
});
