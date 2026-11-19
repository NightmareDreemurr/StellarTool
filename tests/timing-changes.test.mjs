import test from 'node:test';
import assert from 'node:assert/strict';
import {timingChanges,cornerTimingCue,drawTaikoTiming} from '../web/timing-changes.mjs';
const chart={
 bpms:[{tick:0,time:0,bpm:130},{tick:32,time:1,bpm:130},{tick:64,time:2,bpm:150},{tick:128,time:4,bpm:120},{tick:192,time:6,bpm:120.0005}],
 meters:[{tick:0,time:0,numerator:4,denominator:4},{tick:32,time:1,numerator:4,denominator:4},{tick:64,time:2,numerator:3,denominator:4},{tick:160,time:5,numerator:3,denominator:8}],
};
test('ignore initial/duplicate BPM and meter, merge simultaneous changes',()=>{
 assert.deepEqual(timingChanges(chart).map(({time,bpm,cornerBpm,meter})=>[time,bpm,cornerBpm,meter]),[[2,1,1,true],[4,-1,-1,false],[5,0,0,true],[6,1,0,false]]);
});
test('Corner changes exactly on its timestamp, prioritizes BPM, fades linearly, excludes skipped changes',()=>{
 assert.equal(cornerTimingCue(chart,1.999),null);
 assert.deepEqual(cornerTimingCue(chart,2),{kind:1,amount:1});
 assert.ok(Math.abs(cornerTimingCue(chart,2.3).amount-.5)<1e-8);
 assert.equal(cornerTimingCue(chart,2.601),null);
 assert.equal(cornerTimingCue(chart,2.1,2),null);
 assert.equal(cornerTimingCue(chart,4).kind,-1);
 assert.equal(cornerTimingCue(chart,5).kind,2);
 assert.equal(cornerTimingCue(chart,6),null);
});
function taikoAt(time,after){
 const fills=[],stops=[];
 const ctx={globalAlpha:1,createLinearGradient(){return {addColorStop(i,s){stops.push(s);}}},fillRect(...bounds){fills.push({color:this.fillStyle,alpha:this.globalAlpha,bounds});}};
 drawTaikoTiming(ctx,chart,time,time+1,{x:k=>50+(k-time*32)*4,tickAt:t=>t*32,judgeX:50,width:500,top:150,height:100,after});
 return {fills,stops};
}
test('Taiko has anticipatory gradient and changes exactly at judge crossing; meter wins same-tick, no seek flash',()=>{
 const prior=taikoAt(1.999);
 assert.ok(prior.stops.includes('#facc158c'));assert.ok(prior.fills.every(row=>row.bounds[0]>=50));
 const at=taikoAt(2).fills.find(row=>row.bounds[0]===0);
 assert.deepEqual(at,{color:'#facc15',alpha:.55,bounds:[0,150,50,100]});
 assert.ok(Math.abs(taikoAt(2.7).fills.find(row=>row.bounds[0]===0).alpha-.55*Math.sqrt(.5))<1e-8);
 assert.ok(taikoAt(2,2).fills.every(row=>row.bounds[0]>=50));
 assert.ok(taikoAt(3.401).fills.every(row=>row.bounds[0]>=50));
});
