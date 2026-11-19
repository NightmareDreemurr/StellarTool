import test from 'node:test';import assert from 'node:assert/strict';
import {rhythmLabelVisibility as visibility,rhythmLabelIsCompact as compact} from '../web/rhythm-labels.mjs';
const run=(labels,sizes=[],times=labels.map((_,i)=>i*.1))=>labels.map((label,i)=>({label,size:sizes[i]||'full',time:times[i]}));
test('strictly shorter than sixteenth including 24 and 32, dotted and fraction boundaries',()=>{
 for(const s of ['24','32','64','32.','48/2'])assert.equal(compact(s),true,s);
 for(const s of ['16','24.','32/2','8.','1/0','bad'])assert.equal(compact(s),false,s);
});
test('same compact label full run only first; sixteenths retain all; no notes mutated',()=>{const n=run(['32','32','32']);const before=JSON.stringify(n);assert.deepEqual(visibility(n),[true,false,false]);assert.equal(JSON.stringify(n),before);assert.deepEqual(visibility(run(['16','16'])),[true,true]);});
test('changes, null and unequal actual intervals restart, not bar boundaries',()=>{
 assert.deepEqual(visibility(run(['24','24','32','32',null,'32'])),[true,false,true,false,false,true]);
 assert.deepEqual(visibility(run(['32','32','32','32'],[],[0,.1,.3,.5])),[true,false,true,false]);
 const n=run(['32','32']);n[1].bar=2;assert.deepEqual(visibility(n),[true,false]);
});
test('sweep-containing run keeps every full head, suppresses tails only',()=>{
 assert.deepEqual(visibility(run(['32','32','32','32'],['full','tail','full','chordTail'])),[true,false,true,false]);
});
