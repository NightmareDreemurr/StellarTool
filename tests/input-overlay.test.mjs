import test from 'node:test';
import assert from 'node:assert/strict';
import {parseInputOptions,sensorGeometry,keyLayout,isMaskSet,inputBit,playerMasks} from '../web/input-layout.mjs';
test('size and validated color options are OBS-safe',()=>{
  assert.deepEqual(parseInputOptions(''),{size:3840,player:1,background:'#00000000',light:'#EC2127',outline:true,frameOpacity:1,lightOpacity:1});
  assert.deepEqual(parseInputOptions('?size=768&player=2&background=10141a55&light=%23fed044'),{size:768,player:2,background:'#10141a55',light:'#fed044',outline:true,frameOpacity:1,lightOpacity:1});
  assert.equal(parseInputOptions('size=1e3&light=FFFFFF00').size,3840);
  assert.equal(parseInputOptions('size=100000&background=red').size,8192);
  assert.equal(parseInputOptions('background=red').background,'#00000000');
});
test('sensor lights accept alpha and sensor borders can be hidden independently',()=>{
  assert.deepEqual(parseInputOptions('?light=%23FF6B4A80&outline=false','sensor'),{size:3840,player:1,background:'#00000000',light:'#FF6B4A80',outline:false,labels:false});
  assert.equal(parseInputOptions('outline=0','sensor').outline,false);
  assert.equal(parseInputOptions('outline=true','sensor').outline,true);
  assert.equal(parseInputOptions('light=FF6B4A80&outline=false','keys').light,'#EC2127');
});
test('schematic covers all 34 inputs and eight exterior keys in player-facing order',()=>{
  const sensorLayout=schematicSensors();
  assert.equal(sensorLayout.length,34);
  assert.deepEqual(sensorLayout.map(({id})=>inputBit(id)).sort((a,b)=>a-b),Array.from({length:34},(_,i)=>i));
  assert.ok(sensorLayout.find(x=>x.id==='D1').label[1]<300);
  assert.ok(sensorLayout.find(x=>x.id==='C1').label[0]>540);
  assert.ok(sensorLayout.find(x=>x.id==='C2').label[0]<540);
  assert.deepEqual(keyLayout.map(({angle})=>angle),[22.5,67.5,112.5,157.5,202.5,247.5,292.5,337.5]);
  assert.deepEqual(schematicKeys().map(x=>x.bit),[0,1,2,3,4,5,6,7]);
  assert.ok(schematicKeys().every(({center:[x,y],radius:r})=>x-r>=0&&y-r>=0&&x+r<=1080&&y+r<=1080));
  assert.ok(sensorLayout.every(({label:[x,y],radius:r})=>x-r>=0&&y-r>=0&&x+r<=1080&&y+r<=1080));
  assert.ok(sensorLayout.every((a,i)=>sensorLayout.slice(i+1).every(b=>
    Math.hypot(a.label[0]-b.label[0],a.label[1]-b.label[1])>a.radius+b.radius)));
});
test('optional local polygons are validated using synthetic data',()=>{
  const geometry={schemaVersion:1,viewBox:[0,0,1080,1080],
    areas:schematicSensors().map(({id:name,bit,label:[x,y]})=>({
      name,bit,label:[x,y],points:[[x-10,y],[x,y-10],[x+10,y],[x,y+10]],
    })),
  };
  assert.equal(sensorGeometry(geometry).length,34);
  assert.throws(()=>sensorGeometry({...geometry,areas:geometry.areas.map(x=>({...x,name:'D1',bit:18}))}),/Duplicate/);
});
test('high sensor bits and simultaneous inputs survive without stale or unsupported states',()=>{
  const frame={schemaVersion:1,connected:true,players:[{player:0,touchAvailable:true,keysAvailable:false,touchMask:'300000001',keyMask:255},{player:1,touchAvailable:false,keysAvailable:true,touchMask:'300000001',keyMask:129}]};
  const {touch,keys}=playerMasks(frame,1);
  assert.equal(keys,0); assert.ok(isMaskSet(touch,inputBit('A1')));assert.ok(isMaskSet(touch,inputBit('E7')));assert.ok(isMaskSet(touch,inputBit('E8')));assert.ok(!isMaskSet(touch,inputBit('A2')));
  assert.deepEqual(playerMasks(frame,2),{touch:0,keys:129});
  assert.deepEqual(playerMasks({...frame,connected:false},1),{touch:0,keys:0});
});

test('sensor labels are hidden by default, configurable independently, and ignored on keys',()=>{
 assert.equal(parseInputOptions('', 'sensor').labels,false);
 assert.equal(parseInputOptions('labels=false','sensor').labels,false);
 assert.equal(parseInputOptions('labels=true&outline=false','sensor').labels,true);
 assert.equal(parseInputOptions('labels=true&outline=false','sensor').outline,false);
 assert.equal(parseInputOptions('labels=true','keys').labels,undefined);
});

test('keys fixed-frame opacity defaults to opaque and accepts inclusive fractional values',()=>{
 for(const [raw,expected] of [['',1],['.42',.42],['0',0],['1.00',1],['-0.1',1],['1.1',1],['NaN',1],['0x0',1]])assert.equal(parseInputOptions('frameOpacity='+raw).frameOpacity,expected);
 assert.equal(parseInputOptions('frameOpacity=0','sensor').frameOpacity,undefined);
});

test('CAD overlay light opacity is independent and only affects exterior keys',()=>{
 for(const [raw,expected] of [['',1],['.42',.42],['0',0],['1.00',1],['-0.1',1],['1.1',1],['NaN',1]])assert.equal(parseInputOptions('lightOpacity='+raw).lightOpacity,expected);
 assert.equal(parseInputOptions('lightOpacity=.2&frameOpacity=.6').frameOpacity,.6);
 assert.equal(parseInputOptions('lightOpacity=.2','sensor').lightOpacity,undefined);
});

import {inputFrames,startInputFeed,schematicSensors,schematicKeys,mountInputOverlay} from '../web/input-overlay.mjs';
const players=(keyMask=0,touchMask='000000000')=>[{player:0,keysAvailable:true,touchAvailable:true,keyMask,touchMask}];
const packet=(sequence,options={})=>({session:'one',sequence,connected:true,ageMs:0,players:players(),events:[],...options});
test('push renders a short tap once and preserves the highest sensor bit',()=>{
  const keys=inputFrames(1,false),sensors=inputFrames(1,true);
  for(const frames of [keys,sensors]){
    frames.accept(packet(1,{reset:true}),1);
    frames.accept(packet(2,{events:[{players:players(1,'200000000')},{players:players()}]}),2);
  }
  assert.deepEqual(keys.frame(16),{next:1,repaint:true,expires:252});
  assert.equal(keys.frame(32).next,0);
  assert.equal(sensors.frame(16).next,2**33);
  assert.equal(sensors.frame(32).next,0);
});
test('push gaps/resets discard edge history; stale duplicate and retired session cannot leave lights on',()=>{
  const f=inputFrames(1,false);
  f.accept(packet(1),10);
  f.accept(packet(3,{events:[{players:players(1)}],players:players(2)}),20);
  assert.equal(f.frame(25).next,2);
  f.accept(packet(3,{players:players(2),ageMs:250,connected:false}),30);
  assert.equal(f.frame(31).next,0);
  f.accept(packet(1,{session:'two',players:players(4),ageMs:240}),40);
  assert.equal(f.accept(packet(4,{players:players(8)}),41),false);
  assert.equal(f.frame(49).next,4);
  assert.equal(f.frame(50).next,0);
});
test('legacy polling receives on WS failure and stops on reconnect or visibility loss',async()=>{
  class FakeSocket{
    static all=[];
    constructor(url){this.url=url;FakeSocket.all.push(this);}
    close(){this.closed=true;}
    packet(p){this.onmessage({data:JSON.stringify(p)});}
  }
  const doc=new EventTarget();doc.hidden=false;
  const timers=new Map(),frames=new Map();let ticks=0,id=0,fetches=0;
  const env={document:doc,location:{href:'http://localhost/keys'},performance:{now:()=>ticks},WebSocket:FakeSocket,AbortController,
    setTimeout(fn,ms){const key=++id;timers.set(key,{fn,ms});return key;},
    clearTimeout:key=>timers.delete(key),
    requestAnimationFrame(fn){const key=++id;frames.set(key,fn);return key;},
    cancelAnimationFrame:key=>frames.delete(key),
    async fetch(url){assert.equal(url,'/api/input');fetches++;return {ok:true,json:async()=>({schemaVersion:1,connected:true,players:players(1)})};},
  };
  const rendered=[];
  const stop=startInputFeed({player:1,sensor:false,render:x=>rendered.push(x),env});
  const first=FakeSocket.all[0];assert.equal(first.url,'ws://localhost/api/input-stream');
  first.onerror();
  await new Promise(setImmediate);
  assert.equal(rendered.at(-1),1);
  const retry=[...timers].find(([,t])=>t.ms===5000);
  timers.delete(retry[0]);retry[1].fn();
  const second=FakeSocket.all[1];
  second.packet(packet(1,{players:players(2)}));
  const frame=[...frames][0];frames.delete(frame[0]);ticks=16;frame[1]();
  assert.equal(rendered.at(-1),2);
  assert.equal(fetches,1);assert.ok(![...timers.values()].some(t=>t.ms===40));
  doc.hidden=true;doc.dispatchEvent(new Event('visibilitychange'));
  assert.equal(rendered.at(-1),0);assert.equal(second.closed,true);
  second.packet(packet(2,{players:players(4)}));assert.equal(frames.size,0);
  doc.hidden=false;doc.dispatchEvent(new Event('visibilitychange'));assert.equal(FakeSocket.all.length,3);
  stop();assert.equal(FakeSocket.all[2].closed,true);assert.equal(timers.size,0);
});

import {buttonLedFrame,ledCap} from '../web/key-led-model.mjs';
test('real RGB independent from press, expiry, P2 null, and opaque black command distinct from missing',()=>{
  const rgb='00ff00'.repeat(8),data={connected:true,ageMs:30,buttonLeds:[{source:'maimoller-native-hid-write',rgb,ageMs:500},null]};
  assert.deepEqual(buttonLedFrame(data,1,1000),{rgb,expires:2100});
  assert.equal(buttonLedFrame(data,2,1000),null);
  assert.equal(buttonLedFrame({...data,ageMs:250},1,1000),null);
  assert.equal(buttonLedFrame({...data,buttonLeds:[{...data.buttonLeds[0],ageMs:1600}]},1,1000),null);
  assert.equal(ledCap('000000',false),null);assert.equal(ledCap(null,false),null);
  assert.equal(ledCap(null,true),'rgb(188,189,190)');assert.equal(ledCap('ff0000',false),'rgb(255,15,15)');
  assert.equal(ledCap('ff0000',true),'rgb(194,11,11)');assert.equal(ledCap('ff0000',false,0),null);
});

test('dim same-hue LEDs are tint-weaker both held and released; brightness is not normalized',()=>{
  for(const pressed of [true,false]){
    const channels=rgb=>rgb.match(/\d+/g).map(Number);
    const tint=rgb=>{const [r,g,b]=channels(ledCap(rgb,pressed));return r-(g+b)/2;};
    assert.ok(tint('110000')>0);
    assert.ok(tint('110000')<tint('550000'));
    assert.ok(tint('550000')<tint('ff0000'));
  }
});


test('both displays mount and react to input without the asset directory', async()=>{
  class Element {
    constructor(tag){
      this.tag=tag;this.children=[];this.dataset={};this.attrs={};
      this.classes=new Set();
      this.classList={toggle:(c,state)=>state?this.classes.add(c):this.classes.delete(c)};
      this.style={setProperty:(name,value)=>{this.style[name]=value;}};
    }
    setAttribute(name,value){this.attrs[name]=value;}
    append(child){this.children.push(child);}
  }
  for(const path of ['/keys','/sensor']){
    const root=new Element('div');
    const doc=new EventTarget();
    doc.hidden=false;
    doc.querySelector=()=>root;
    doc.createElementNS=(_,tag)=>new Element(tag);
    const env={document:doc,location:new URL('http://localhost'+path+'?labels=true'),
      performance:{now:()=>0},AbortController,
      setTimeout,clearTimeout,
      async fetch(url){
        if(url.startsWith('/assets/'))return {ok:false};
        assert.equal(url,'/api/input');
        return {ok:true,json:async()=>({schemaVersion:1,connected:true,players:players(1,'200000000'),
          buttonLeds:[{source:'maimoller-native-hid-write',rgb:'ff0000'+'000000'.repeat(7),ageMs:0}],ageMs:0})};
      },
    };
    const stop=await mountInputOverlay(env);
    try{
      await new Promise(setImmediate);
      assert.equal(root.dataset.geometry,'schematic');
      const svg=root.children[0];
      if(path==='/keys'){
        assert.equal(svg.children.length,9);
        const first=svg.children[1];
        assert.equal(first.attrs['data-channel'],'1');
        assert.equal(first.classes.has('is-down'),true);
        assert.equal(first.style['--key-color'],'rgb(194,11,11)');
      }else{
        assert.equal(svg.children.length,34);
        const zone=svg.children.find(g=>g.children[0].attrs['data-channel']==='E8');
        assert.equal(zone.children[0].classes.has('is-down'),true);
        assert.equal(zone.children[1].textContent,'E8');
      }
    }finally{stop();}
  }
});
