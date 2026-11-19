import test from 'node:test';import assert from 'node:assert/strict';
import {judgeMeaning,judgeFlight,judgementFeed} from '../web/judgements.mjs';
const packet=(events,extra={})=>({source:'game-judgement',schemaVersion:1,available:true,session:'s',epoch:1,events,...extra});
test('all committed grades map exactly; misses and Critical do not invent FAST/LATE',()=>{
 const grades=['MISS','GOOD','GREAT','GREAT','GREAT','PERFECT','PERFECT','CRITICAL','PERFECT','PERFECT','GREAT','GREAT','GREAT','GOOD','MISS'];
 grades.forEach((g,i)=>assert.equal(judgeMeaning(i).grade,g));
 for(const i of [0,7,14])assert.equal(judgeMeaning(i).side,null);
 assert.equal(judgeMeaning(6).side,'FAST');assert.equal(judgeMeaning(8).side,'LATE');assert.equal(judgeMeaning(15),null);
});
test('vertical motion is eased, fade nonlinear, lifetime bounded',()=>{
 const a=judgeFlight(0),b=judgeFlight(.25),c=judgeFlight(.37);
 assert.deepEqual(a,{rise:0,alpha:1,scale:.92});assert.ok(b.alpha<.25);
 assert.ok(b.rise>85*.5&&c.rise>b.rise);assert.equal(judgeFlight(.38),null);assert.equal(judgeFlight(-.1),null);
});
test('bounded recent snapshots deduplicate events, do not replay on seek or unknown source',()=>{
 const f=judgementFeed(),e={sequence:1,noteIndex:4,timing:6,gameTime:10};
 f.accept(packet([e]),{session:'s',time:10.1});f.accept(packet([e]),{session:'s',time:10.2});
 assert.equal(f.active(10.2).length,1);assert.equal(f.active(10.2)[0].side,'FAST');
 f.accept(packet([e]),{session:'s',time:20,effectStartTime:20});assert.equal(f.active(20).length,0);
 f.accept(packet([{...e,sequence:2,gameTime:20.1}]),{session:'s',time:20.2,effectStartTime:20});assert.equal(f.active(20.2).length,1);
 f.accept(packet([e],{source:'chart-guessed'}),{session:'s',time:10.1});assert.equal(f.active(10.1).length,0);
});
test('session reset, expired and unknown IDs/grades do not fabricate events',()=>{
 const f=judgementFeed();f.accept(packet([{sequence:1,noteIndex:1,timing:7,gameTime:1}]),{session:'other',time:1});assert.equal(f.active(1).length,0);
 f.accept(packet([{sequence:1,noteIndex:1,timing:15,gameTime:1}]),{session:'s',time:1});assert.equal(f.active(1).length,0);
 f.accept(packet([{sequence:2,noteIndex:1,timing:14,gameTime:1}]),{session:'s',time:2});assert.equal(f.active(2).length,0);
});
test('simultaneous different note IDs are preserved, never deduplicated by time or lane',()=>{
 const f=judgementFeed(),events=[{sequence:1,noteIndex:10,timing:7,gameTime:2},{sequence:2,noteIndex:11,timing:4,gameTime:2}];
 f.accept(packet(events),{session:'s',time:2.05});f.accept(packet(events),{session:'s',time:2.1});
 assert.deepEqual(f.active(2.1).map(x=>[x.noteIndex,x.grade,x.side]),[[10,'CRITICAL',null],[11,'GREAT','FAST']]);
});

test('short quick pop settles without horizontal displacement; alpha holds then fades',()=>{
 assert.ok(judgeFlight(.045).scale>1);assert.equal(judgeFlight(.12).scale,1);
 assert.equal(judgeFlight(.08).alpha,1);assert.ok(judgeFlight(.2).alpha<1);
 for(let i=1;i<38;i++){const a=judgeFlight((i-1)/100),b=judgeFlight(i/100);assert.ok(b.rise>=a.rise);assert.ok(b.alpha<=a.alpha);assert.equal(b.x,undefined);}
});
