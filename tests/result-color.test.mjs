import test from 'node:test';import assert from 'node:assert/strict';import fs from 'node:fs';
import {resultDirection} from '../web/songinfo.mjs';
test('numeric result increases red, decreases blue; no baseline/equal/art stay neutral',()=>{
 const css=fs.readFileSync(new URL('../web/style.css',import.meta.url),'utf8');
 assert.match(css,/\.song \.score-better\{color:#c51b38\}/);assert.match(css,/\.song \.score-worse\{color:#1b62ce\}/);
 for(const key of ['achievement','rating','dxScore','dxPercent']){
 assert.equal(resultDirection(key,2,1,true),'better');assert.equal(resultDirection(key,1,2,true),'worse');
 assert.equal(resultDirection(key,1,1,true),'');assert.equal(resultDirection(key,1,null,true),'');assert.equal(resultDirection(key,2,1,false),'');
 }
 for(const key of ['rank','combo','sync'])assert.equal(resultDirection(key,2,1,true),'');
});
