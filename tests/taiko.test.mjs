import test from 'node:test';import assert from 'node:assert/strict';
import {buildTaiko,rhythmValue,rhythmPalette,renderTaiko} from '../web/taiko.mjs';
const chart=(gap=24,extra={})=>({resolution:384,events:[0,1,2,3].map((lane,i)=>({noteIndex:i,tick:i*gap,time:i*.1,lane,type:'Tap',...extra}))});
test('fill derives from rhythmic value, not Tap/Touch/Break kind',()=>{for(const [den,fill]of Object.entries(rhythmPalette)){const c=chart(40320/Number(den));c.resolution=40320;assert.equal(buildTaiko(c)[1].fill,fill);c.events.forEach(e=>{e.break=true;e.type='TouchTap';});assert.equal(buildTaiko(c)[1].fill,fill);}});
test('break and each use semantic outlines, combined keeps both flags',()=>{const c=chart();c.events.push({...c.events[1],lane:6,break:true});const n=buildTaiko(c)[1];assert.equal(n.fill,'#0473f6');assert.equal(n.stroke,'#fb923c');assert.equal(n.each,true);assert.equal(n.break,true);});
test('ordinary sweep tail and break tail radii; splash does not invent cyan',()=>{assert.equal(buildTaiko(chart())[1].radiusRatio,.5);assert.equal(buildTaiko(chart(24,{break:true}))[1].radiusRatio,.75);const n=buildTaiko(chart(24,{type:'TouchTap'}));assert.equal(n[1].splash,2);assert.equal(n[1].radiusRatio,.5);assert.equal(n[1].fill,'#0473f6');});
test('dotted diamond, sparse and unsupported values',()=>{assert.deepEqual(rhythmValue(384,72),{label:'8.',diamond:true});assert.equal(buildTaiko(chart(72))[1].fill,'#fe0000');assert.equal(buildTaiko(chart(300))[1].fill,'#27272a');assert.equal(buildTaiko(chart(15))[1].fill,'#9ca3af');});
test('render opacity, semantic strokes and judgement circle',()=>{const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...args)=>calls.push([k,...args]),set:(o,k,v)=>(o[k]=v,calls.push([k,v]),true)});renderTaiko(ctx,chart(24,{break:true}),.12,{});assert.ok(calls.some(c=>c[0]==='strokeStyle'&&c[1]==='#fb923c'));assert.ok(calls.some(c=>c[0]==='globalAlpha'&&c[1]<1));});
import {taikoGeometry} from '../web/taiko.mjs';
test('16th centres touch at two radii, other divisions scale exactly',()=>{for(const width of [320,800,1600]){const g=taikoGeometry({resolution:384},0,{width,height:100});assert.ok(Math.abs(g.x(24)-g.x(0)-g.radius*2)<1e-10);assert.ok(Math.abs((g.x(16)-g.x(0))/(g.x(24)-g.x(0))-2/3)<1e-10);assert.ok(Math.abs(g.x(48)-g.x(0)-g.radius*4)<1e-10);}});
test('BPM changes scroll velocity, not musical spacing; meter does not rescale',()=>{const c={resolution:384,bpms:[{tick:0,time:0,bpm:120},{tick:96,time:.5,bpm:240}],meters:[{tick:96,time:.5,numerator:3,denominator:8}]};const a=taikoGeometry(c,.49),b=taikoGeometry(c,.51);assert.ok(Math.abs(a.x(120)-a.x(96)-50)<1e-10);assert.ok(Math.abs(b.x(120)-b.x(96)-50)<1e-10);assert.ok(Math.abs(taikoGeometry(c,.5).nowTick-96)<1e-10);assert.ok(Math.abs(b.nowTick-99.84)<1e-8);});
test('80/20 layout and optional timing moves ring',()=>{const off=taikoGeometry({},0),on=taikoGeometry({},0,{showTiming:true});assert.equal(off.noteY,40);assert.equal(off.valueY,90);assert.equal(off.barY,75);assert.equal(off.noteBottom,80);assert.ok(off.judgeX<on.judgeX);assert.equal(off.sixteenth,50);assert.equal(on.sixteenth,50);});
test('actual flight crosses into transparent reserve; values centered',()=>{const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...args)=>calls.push([k,...args]),set:(o,k,v)=>(o[k]=v,true)});const c={...chart(),bpms:[{tick:0,time:0,bpm:120}]};renderTaiko(ctx,c,.12,{top:150,showTiming:false,judgements:[{noteIndex:0,timing:7,gameTime:0}]});assert.ok(calls.some(c=>c[0]==='arc'&&c[2]<150));assert.ok(!calls.some(c=>c[0]==='fillText'&&c[1]==='120'));assert.ok(calls.some(c=>c[0]==='fillText'&&c[3]===240));});

test('separator, full note-area bar lines, beat dots and right-hand bar label',()=>{
 const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...args)=>calls.push([k,...args]),set:(o,k,v)=>(o[k]=v,true)});
 renderTaiko(ctx,{resolution:384,events:[],bpms:[{tick:0,time:0,bpm:120}]},0,{top:150});
 assert.ok(calls.some(c=>c[0]==='moveTo'&&c[1]===0&&c[2]===230));
 assert.ok(calls.some(c=>c[0]==='lineTo'&&c[1]===800&&c[2]===230));
 assert.ok(calls.some(c=>c[0]==='fillText'&&c[1]==='1'&&c[2]===33&&c[3]===225));
 assert.ok(calls.some(c=>c[0]==='arc'&&c[2]===190&&c[3]===1.8));
 // The bar passing through x29 is split above and below the circle.
 assert.ok(calls.some(c=>c[0]==='lineTo'&&c[1]===29&&c[2]===162));
 assert.ok(calls.some(c=>c[0]==='moveTo'&&c[1]===29&&c[2]===218));
});

test('full notes occupy 5/8 of note band, matching the judgment ring, and bar dots are omitted',()=>{
 const g=taikoGeometry({},0,{height:200});assert.equal(g.radius*2,g.noteBottom*5/8);assert.equal(g.noteY,g.noteBottom/2);assert.equal(g.ringRadius,g.radius);
 const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...args)=>calls.push([k,...args]),set:(o,k,v)=>(o[k]=v,true)});
 renderTaiko(ctx,{resolution:384,events:[],bpms:[{tick:0,time:0,bpm:120}]},0,{top:150,width:1600});
 const dots=calls.filter(c=>c[0]==='arc'&&c[3]===1.8);assert.ok(dots.length>=4);assert.ok(dots.every(c=>c[2]===190));assert.ok(!dots.some(c=>c[1]===34+1120));
 assert.ok(calls.some(c=>c[0]==='arc'&&c[1]===29&&c[2]===190&&c[3]===25));
});

test('seek cutoff removes old airborne note drawing and pause never invents hits',()=>{
 const c={resolution:384,bpms:[{time:0,tick:0,bpm:120}],events:[{noteIndex:0,time:1,tick:192,lane:0,type:'Tap'}]};
 const arcs=options=>{const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});renderTaiko(ctx,c,1.1,options);return calls.filter(x=>x[0]==='arc'&&x[3]>2);};
 assert.equal(arcs({}).length,2); // decorative flight alone does not fabricate a grade
 assert.equal(arcs({judgements:[{noteIndex:0,timing:7,gameTime:1}]}).length,2);
 assert.equal(arcs({effectStartTime:1.1,judgements:[{noteIndex:0,timing:7,gameTime:1}]}).length,1); // ring only after seek
 assert.equal(arcs({effectsEnabled:false,judgements:[{noteIndex:0,timing:7,gameTime:1}]}).length,1);
});
test('simultaneous exact IDs in one rhythm slot keep both committed grades without double-opacity circle',()=>{
 const calls=[];const ctx=new Proxy({},{get:(o,k)=>k in o?o[k]:(...a)=>calls.push([k,...a]),set:(o,k,v)=>(o[k]=v,true)});
 const c={resolution:384,bpms:[{time:0,tick:0,bpm:120}],events:[{noteIndex:10,time:1,tick:192,lane:0,type:'Tap'},{noteIndex:11,time:1,tick:192,lane:2,type:'Tap'}]};
 renderTaiko(ctx,c,1.1,{judgements:[{noteIndex:10,timing:7,gameTime:1},{noteIndex:11,timing:4,gameTime:1},{noteIndex:999,timing:7,gameTime:1}]});
 assert.equal(calls.filter(x=>x[0]==='arc'&&x[3]>2).length,2);
 const text=calls.filter(x=>x[0]==='fillText'&&['CRITICAL PERFECT','GREAT'].includes(x[1]));
 assert.deepEqual(text.map(x=>x[1]),['CRITICAL PERFECT','GREAT']);
});
