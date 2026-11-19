import test from 'node:test';import assert from 'node:assert/strict';
import {timingHistogram,resultLevelLabel,toFrames,urFromPopulationSd} from '../web/result-judgement.mjs';
const histogram=Array(60).fill(0);
histogram[29]=1;histogram[31]=1;
const sample={binWidthMs:5,binStartMs:-150,binCount:60,groups:{standard:{count:2,meanMs:5,populationStdDevMs:20,histogram}}};
test('judgement uses calibrated default scope and population UR in milliseconds',()=>{
 assert.equal(toFrames(25),1.5);
 assert.deepEqual(timingHistogram(sample),{bins:histogram,peak:1,count:2,avg:.3,ur:200});
 assert.equal(urFromPopulationSd(1,0),null);
 assert.equal(urFromPopulationSd(2,0),0);
});
test('fallback does not borrow a second scoring scope or inconsistent histogram',()=>{
 assert.equal(timingHistogram(null),null);
 assert.equal(timingHistogram({...sample,binCount:3}),null);
 assert.equal(timingHistogram({...sample,groups:{ex:sample.groups.standard}}),null);
 assert.equal(resultLevelLabel({id:11885,difficulty:3}),'MASTER');
 assert.equal(resultLevelLabel({id:100111,difficulty:0,utageKanji:'宴'}),'宴');
});
