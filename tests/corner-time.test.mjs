import test from 'node:test';
import assert from 'node:assert/strict';
import {formatCornerTime,cornerTimeLine} from '../web/model.mjs';
test('minute-second-four decimals and rollover',()=>{assert.equal(formatCornerTime(0),'0:00.0000');assert.equal(formatCornerTime(61.23456),'1:01.2346');assert.equal(formatCornerTime(59.99996),'1:00.0000');assert.equal(formatCornerTime(3600),'60:00.0000');assert.equal(formatCornerTime(-1.25),'-0:01.2500');});
test('unknown duration is not zero or note endpoint',()=>{for(const value of [undefined,null,NaN,Infinity,0,-1,'120'])assert.equal(cornerTimeLine(1,{musicDuration:value,duration:120,noteEndTime:110}),'0:01.0000 / —');assert.equal(cornerTimeLine(1,{musicDuration:125.25}),'0:01.0000 / 2:05.2500');});
test('invalid current time stays unknown',()=>{for(const value of [undefined,null,NaN,Infinity,'1'])assert.equal(formatCornerTime(value),'—');});

import {cornerMetronome} from '../web/model.mjs';
test('Corner shows the actual beat count and highlights changes, using one-based game indices',()=>{
 const c={bpms:[{time:0,bpm:120}],meters:[{time:0,numerator:4,denominator:4},{time:2,numerator:3,denominator:4},{time:3.5,numerator:6,denominator:8}],beats:[{time:0,beat:1},{time:1.5,beat:4},{time:2,beat:1},{time:3,beat:3},{time:3.5,beat:1},{time:4.75,beat:6}]};
 for(const [time,count,current] of [[0,4,1],[1.5,4,4],[1.999,4,4],[2,3,1],[3,3,3],[3.5,6,1],[4.75,6,6]]){assert.deepEqual(cornerMetronome(c,time),{count,current,first:current===1});}
 assert.deepEqual(cornerMetronome({...c,beats:[]},4.75),{count:6,current:6,first:false});
 assert.deepEqual(cornerMetronome({},2),{count:4,current:1,first:true});
});
