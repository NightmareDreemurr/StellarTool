import test from 'node:test';
import assert from 'node:assert/strict';
import {detectSweeps} from '../web/sweep.mjs';
const events=(lanes,ticks=lanes.map((_,i)=>i*24),times=lanes.map((_,i)=>i*100))=>lanes.flatMap((lane,i)=>(Array.isArray(lane)?lane:[lane]).map(lane=>({lane,tick:ticks[i],timeMs:times[i]})));
const roles=e=>detectSweeps(e).map(s=>s.role);
test('three accepted, two rejected; counts slots not chord note count',()=>{
 assert.deepEqual(roles(events([0,1,2])),['head','tail','tail']);
 assert.deepEqual(roles(events([[0,4],[1,5]])),['solo','solo']);
});
test('strict 133ms maximum boundary',()=>{
 for(const gap of [132.999,133,133.001]) assert.deepEqual(roles(events([0,1,2],undefined,[0,gap,gap*2])),gap<133?['head','tail','tail']:['solo','solo','solo']);
});
test('rhythm pivot belongs to new head, never old tail',()=>{
 // Lane 3 -> 4 at 16th; lane 4 -> 5 -> 6 at 24th (384 ticks/bar).
 assert.deepEqual(roles(events([0,1,2,3,4,5],[0,24,48,72,88,104],[0,100,200,300,366.667,433.333])),['head','tail','tail','head','tail','tail']);
 assert.deepEqual(roles(events([2,3,4,5],[0,24,40,56],[0,100,166.667,233.333])),['solo','head','tail','tail']);
});
test('ring wrap and fixed direction',()=>{
 assert.deepEqual(roles(events([7,0,1,2])),['head','tail','tail','tail']);
 assert.deepEqual(roles(events([0,7,6,5])),['head','tail','tail','tail']);
 assert.deepEqual(roles(events([0,2,4])),['solo','solo','solo']);
});
test('same tick lane deduplicated; touch/connected omitted',()=>{
 const e=events([0,1,2]);e.push({...e[0]},{tick:12,timeMs:50,lane:3,type:'touch'},{tick:36,timeMs:150,lane:4,connected:true});
 assert.deepEqual(roles(e),['head','tail','tail']);assert.deepEqual(detectSweeps(e)[0].lanes,[0]);
});
test('cross BPM preserves equal ticks but per-step wall cap applies',()=>{
 assert.deepEqual(roles(events([0,1,2,3],undefined,[0,100,225,325])),['head','tail','tail','tail']);
 assert.deepEqual(roles(events([0,1,2],undefined,[0,100,233])),['solo','solo','solo']);
});
test('Stellar length-three isolated axis filter retained',()=>{
 assert.deepEqual(roles(events([1,0,1,2])),['solo','solo','solo','solo']);
});
test('head may be previous chain endpoint simultaneously',()=>{
 const s=detectSweeps(events([0,1,2,3,4,5],[0,24,48,72,88,104],[0,100,200,300,366,432]));
 assert.equal(s[3].role,'head');assert.equal(s[3].isEnd,true);
});
