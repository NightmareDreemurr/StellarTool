import test from 'node:test';import assert from 'node:assert/strict';
import {meterFeed,msToFrames,meterOptions,tickOpacity,cursorColor,previewMeterView}from'../web/judgement-meter-model.mjs';
const snap=(events=[],extra={})=>({state:'playing',time:10,sessionId:'s',hitOffsets:{schemaVersion:1,source:'game-calibrated-hit',available:true,session:'s',attempt:1,events,...extra}});
const e=(sequence,offsetMs=-16.666666)=>({sequence,group:'standard',gameTime:10,offsetMs});
test('60Hz frames, not raw ms or quantised frame integer',()=>{assert.ok(Math.abs(msToFrames(-16.666666)+1)<1e-6);assert.equal(msToFrames(25),1.5);});
test('bounded options and monotonic fade',()=>{assert.deepEqual(meterOptions(new URLSearchParams()),{width:800,height:90,range:9,preview:false,cursorColor:'#ffffff'});assert.equal(meterOptions(new URLSearchParams('range=999')).range,60);assert.equal(tickOpacity(5000),0);assert.ok(tickOpacity(100)>tickOpacity(3000));});
test('dedup, source filtering, reset and no fake samples',()=>{const f=meterFeed();f.accept(snap([e(1),{...e(2),group:'touch'},e(3,NaN)]),1000);assert.equal(f.view(1000).ticks.length,1);f.accept(snap([e(1)]),1100);assert.equal(f.view(1100).ticks.length,1);f.accept(snap([],{attempt:2}),1200);assert.equal(f.view(1200).ticks.length,0);f.accept({...snap([e(4)]),state:'result'},1300);assert.equal(f.view(1300).available,false);});
test('authoritative distribution retained independently of event window; old samples expire',()=>{const f=meterFeed(),distribution={groups:{standard:{count:400,meanMs:-8,populationStdDevMs:20}}};f.accept(snap([e(1)],{distribution}),1000);assert.equal(f.view(1000).distribution,distribution);assert.equal(f.view(6001).ticks.length,0);f.accept(snap([],{source:'invented'}),6100);assert.equal(f.view(6100).available,false);});

test('cursor color accepts only RGB/RGBA hex; default white and explicit alpha supported',()=>{
 assert.equal(cursorColor(null),'#ffffff');assert.equal(cursorColor('FF9900'),'#FF9900');assert.equal(cursorColor('33AACC80'),'#33AACC80');
 for(const bad of ['red','#fff','12345','xyzabc','ffffff;display:none'])assert.equal(cursorColor(bad),'#ffffff');
});
test('recent cursor EMA follows real standard samples and resets per attempt',()=>{
 const f=meterFeed();f.accept(snap([e(1,-10),e(2,10)]),1000);assert.equal(f.view(1000).emaOffsetMs,-8);
 f.accept(snap([e(1,5)],{attempt:2}),1100);assert.equal(f.view(1100).emaOffsetMs,5);
 f.clear();assert.equal(f.view(1200).emaOffsetMs,null);
});
test('OBS preview is explicit and deterministic at any range without a real snapshot',()=>{
 assert.equal(meterOptions(new URLSearchParams('preview=false')).preview,false);
 assert.equal(meterOptions(new URLSearchParams('preview=true&width=901&range=5')).preview,true);
 const a=previewMeterView(5,1000),b=previewMeterView(5,2000);
 assert.equal(a.available,true);assert.equal(a.ticks.length,6);
 assert.deepEqual(a.ticks.map(x=>x.offsetMs),b.ticks.map(x=>x.offsetMs));
 assert.deepEqual(a.ticks.map(x=>1000-x.at),b.ticks.map(x=>2000-x.at));
 assert.ok(Math.abs(msToFrames(a.emaOffsetMs)-.7)<1e-8);
});
