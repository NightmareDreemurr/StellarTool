import {buttonLedFrame,ledCap} from './key-led-model.mjs';
import {parseInputOptions,sensorGeometry,keyLayout,inputBit,isMaskSet,playerMasks} from './input-layout.mjs?v=cad1';

const freshMs=250;
const validAge=age=>Number.isFinite(age)&&age>=0&&age<freshMs;

export function inputFrames(player,sensor){
  const mask=packet=>{
    const value=playerMasks({...packet,schemaVersion:1,connected:true},player);
    return BigInt(sensor?value.touch:value.keys);
  };
  let session=null,sequence=-1,held=0n,rising=0n,expires=0;
  const retired=new Set();
  const clear=()=>{held=0n;rising=0n;expires=0;};
  return {
    clear,
    accept(packet,now){
      if(typeof packet?.session!=='string'||!Number.isSafeInteger(packet.sequence)||packet.sequence<0||
         typeof packet.connected!=='boolean'||!Number.isFinite(packet.ageMs)||packet.ageMs<0||
         !Array.isArray(packet.players)||!Array.isArray(packet.events)||packet.events.length>32)return false;
      const changed=packet.session!==session;
      if((changed&&retired.has(packet.session))||(!changed&&packet.sequence<sequence))return false;
      const duplicate=!changed&&packet.sequence===sequence;
      const reset=changed||packet.reset===true||packet.sequence!==sequence+1;
      if(changed&&session!==null){
        retired.add(session);
        if(retired.size>32)retired.delete(retired.values().next().value);
      }
      session=packet.session;sequence=packet.sequence;
      if(!packet.connected||!validAge(packet.ageMs)){clear();return true;}
      if(duplicate)return true;
      if(reset||now>=expires)clear();
      if(!reset)for(const event of packet.events){
        if(event?.sampleAgeMs!==undefined&&!validAge(event.sampleAgeMs))continue;
        const next=mask(event);
        rising|=next&~held;held=next;
      }
      const current=mask(packet);
      if(!reset)rising|=current&~held;
      held=current;expires=now+freshMs-packet.ageMs;
      return true;
    },
    frame(now){
      if(now>=expires)clear();
      const next=Number(held|rising);
      const repaint=next!==Number(held);
      rising=0n;
      return {next,repaint,expires};
    },
  };
}

// All network work belongs to this OBS/browser page. On older hosts (or when a
// proxy blocks WebSocket upgrades) retain the previous bounded GET poll.
export function startInputFeed({player,sensor,render,useLeds=false,env=globalThis}){
  const doc=env.document;
  const now=()=>env.performance.now();
  let socket,watch,retry,pollTimer,pollRequest,raf,stale,healthy=false,stopped=false;
  let frames=inputFrames(player,sensor),led=null;
  const visible=()=>!stopped&&!doc.hidden;
  const cancelPaint=()=>{
    if(raf!==undefined)env.cancelAnimationFrame(raf);
    raf=undefined;env.clearTimeout(stale);stale=undefined;
  };
  const clear=()=>{cancelPaint();frames.clear();led=null;render(0);};
  const paint=()=>{
    raf=undefined;env.clearTimeout(stale);stale=undefined;
    const at=now(),{next,repaint,expires}=frames.frame(at);
    if(led&&at>=led.expires)led=null;
    if(useLeds)render(next,led?.rgb??null);else render(next);
    if(repaint)schedulePaint();
    else {
      const nextExpiry=Math.min(next&&expires>at?expires:Infinity,led?.expires??Infinity);
      if(Number.isFinite(nextExpiry))stale=env.setTimeout(schedulePaint,Math.ceil(nextExpiry-at));
    }
  };
  function schedulePaint(){
    if(raf===undefined&&visible())raf=env.requestAnimationFrame(paint);
  }
  function stopPoll(){
    env.clearTimeout(pollTimer);pollTimer=undefined;
    const request=pollRequest;pollRequest=undefined;request?.abort();
  }
  async function poll(){
    pollTimer=undefined;
    if(!visible()||healthy||pollRequest)return;
    const controller=new env.AbortController();
    pollRequest=controller;
    let nextDelay=250;
    const timeout=env.setTimeout(()=>controller.abort(),freshMs);
    try{
      const started=now();
      const res=await env.fetch('/api/input',{cache:'no-store',signal:controller.signal});
      if(!res.ok)throw new Error('Legacy input unavailable');
      const packet=await res.json(),masks=playerMasks(packet,player);
      if(pollRequest===controller&&visible()&&!healthy&&!controller.signal.aborted){
        if(useLeds)render(masks.keys,buttonLedFrame(packet,player,now())?.rgb??null);
        else render(sensor?masks.touch:masks.keys);
      }
      nextDelay=Math.max(12,40-(now()-started));
    }catch{
      if(pollRequest===controller&&visible()&&!healthy)render(0);
    }finally{
      env.clearTimeout(timeout);
      if(pollRequest===controller){
        pollRequest=undefined;
        if(visible()&&!healthy)pollTimer=env.setTimeout(poll,nextDelay);
      }
    }
  }
  function stopSocket(){
    env.clearTimeout(watch);watch=undefined;
    const old=socket;socket=undefined;healthy=false;
    try{old?.close();}catch{}
  }
  function fallback(){
    const wasHealthy=healthy;
    stopSocket();
    if(wasHealthy)clear();
    if(!visible())return;
    if(!pollRequest&&pollTimer===undefined)void poll();
    if(typeof env.WebSocket==='function'&&retry===undefined)
      retry=env.setTimeout(connect,5000);
  }
  function connect(){
    retry=undefined;
    if(!visible()||socket)return;
    if(typeof env.WebSocket!=='function'){fallback();return;}
    let connection;
    try{
      const url=new URL('/api/input-stream',env.location.href);
      url.protocol=url.protocol==='https:'?'wss:':'ws:';
      connection=new env.WebSocket(url.href);
    }catch{fallback();return;}
    socket=connection;frames=inputFrames(player,sensor);
    const failed=()=>{if(socket===connection)fallback();};
    const watchdog=ms=>{env.clearTimeout(watch);watch=env.setTimeout(failed,ms);};
    watchdog(500);
    connection.onclose=failed;connection.onerror=failed;
    connection.onmessage=event=>{
      if(socket!==connection||!visible())return;
      try{
        if(typeof event.data!=='string'||event.data.length>32768)return;
        const packet=JSON.parse(event.data),at=now();
        if(!frames.accept(packet,at))return;
        if(useLeds)led=buttonLedFrame(packet,player,at);
      }catch{return;}
      if(!healthy){healthy=true;stopPoll();}
      watchdog(500);
      schedulePaint();
    };
  }
  const visibility=()=>{
    if(!visible()){
      env.clearTimeout(retry);retry=undefined;stopPoll();stopSocket();clear();
    }else connect();
  };
  doc.addEventListener('visibilitychange',visibility);
  connect();
  return ()=>{stopped=true;doc.removeEventListener('visibilitychange',visibility);visibility();};
}

// Independent schematic: labeled circles on four concentric tracks, not a
// measurement, trace, nor calibration of any arcade touch panel.
const polarPoint=(angle,radius)=>[540+radius*Math.sin(angle*Math.PI/180),540-radius*Math.cos(angle*Math.PI/180)];
export function schematicSensors(){
  const tracks=[['A',448,22.5],['B',176,22.5],['D',352,0],['E',264,0]];
  return [
    ...tracks.flatMap(([ring,radius,offset])=>Array.from({length:8},(_,i)=>{
      const id=ring+(i+1);
      return {id,bit:inputBit(id),label:polarPoint(45*i+offset,radius),radius:34};
    })),
    ...[1,2].map(i=>({id:'C'+i,bit:inputBit('C'+i),label:[i===1?590:490,540],radius:35})),
  ];
}
export function schematicKeys(){
  return keyLayout.map(({id,angle},bit)=>({id,bit,center:polarPoint(angle,420),radius:53}));
}

async function loadSensorShapes(fetch){
  try{
    const res=await fetch('/assets/sensor-layout.json');
    if(res.ok)return {native:true,shapes:sensorGeometry(await res.json())};
  }catch{}
  return {native:false,shapes:schematicSensors()};
}

async function hasLocalKeyArt(fetch,prefix){
  try{
    // The local host serves GET assets; range-aware proxies can return a single byte.
    const res=await fetch(prefix+'KEY_01.png',{headers:{Range:'bytes=0-0'}});
    const exists=res.ok&&res.headers.get('content-type')?.startsWith('image/png');
    if(res.body)void res.body.cancel().catch(()=>{});
    return exists;
  }catch{return false;}
}

if(typeof document!=='undefined')await mountInputOverlay();
export async function mountInputOverlay(env=globalThis){
const {document,location}=env;
const fetch=(...args)=>env.fetch(...args);
const sensor=location.pathname.replace(/\/$/,'')==='/sensor';
const opts=parseInputOptions(location.search,sensor?'sensor':'keys');
const useLeds=!sensor&&new URLSearchParams(location.search).get('ledMode')!=='legacy';
const root=document.querySelector('#overlay');
root.style.setProperty('--size',opts.size+'px');
root.style.setProperty('--background',opts.background);
root.style.setProperty('--light',opts.light);
root.classList.toggle('no-sensor-outline',sensor&&!opts.outline);

const ns='http://www.w3.org/2000/svg';
function add(parent,tag,attrs={}) {
  const e=document.createElementNS(ns,tag);
  for(const [k,v] of Object.entries(attrs))e.setAttribute(k,v);
  parent.append(e);
  return e;
}

const channels=[];
if(sensor){
  const svg=add(root,'svg',{viewBox:'0 0 1080 1080',role:'img','aria-label':'触摸感应器'});
  const {native,shapes}=await loadSensorShapes(fetch);
  root.dataset.geometry=native?'local':'schematic';
  if(!native)svg.setAttribute('aria-label','触摸感应器示意图');
  for (const {id,points,label,bit,radius} of shapes){
    const g=add(svg,'g');
    const tile=add(g,native?'polygon':'circle',{
      class:'sensor-tile','data-channel':id,
      ...(native?{points}:{cx:label[0],cy:label[1],r:radius}),
    });
    if(opts.labels){
    const t=add(g,'text',{class:'sensor-id',x:label[0],y:label[1]});
    t.textContent=id;
    }
    channels.push([tile,bit]);
  }
}else if(!await hasLocalKeyArt(fetch,'/assets/cad-keys-v1/')){
  root.dataset.geometry='schematic';
  root.style.setProperty('--light-opacity',opts.lightOpacity);
  const svg=add(root,'svg',{viewBox:'0 0 1080 1080',role:'img','aria-label':'外圈按键示意图'});
  add(svg,'circle',{cx:540,cy:540,r:480,'stroke-opacity':opts.frameOpacity,class:'schematic-key-frame'});
  for(const {id,bit,center:[cx,cy],radius:r} of schematicKeys()){
    const cap=add(svg,'circle',{cx,cy,r,'data-channel':id,
      class:useLeds?'schematic-key schematic-led':'schematic-key schematic-legacy'});
    channels.push([cap,bit]);
  }
}else{
  const prefix='/assets/cad-keys-v1/';
  root.dataset.geometry='local';
  root.classList.add('cad-key-ring');
  root.setAttribute('role','img');
  root.setAttribute('aria-label','外圈按键');
  root.style.setProperty('--light-opacity',opts.lightOpacity);
  const clip=document.createElement('div');
  clip.className='cad-key-clip';
  root.append(clip);
  const art=document.createElement('div');
  art.className='cad-key-art';
  clip.append(art);
  function image(file,opacity=1,kind='lines'){
    const img=document.createElement('img');
    img.className='cad-key-'+kind;
    img.alt='';
    img.src=prefix+file;
    img.style.opacity=opacity;
    art.append(img);
  }
  if(opts.frameOpacity===1) image('white-wand-full.svg',1,'back');
  else{
    image('white-wand-body.svg',1,'back');
    if(opts.frameOpacity>0)image('white-wand-plates.svg',opts.frameOpacity,'back');
  }
  for(const {id} of keyLayout){
    const layer=document.createElement('div');
    layer.className='cad-key-light';
    layer.setAttribute('data-channel',id);
    const i=Number(id)-1,stem='KEY_'+id.padStart(2,'0');
    if(useLeds){
      layer.className='cad-led-cap';
      layer.style.inset='0';
      layer.style.setProperty('--key-mask',`url("${prefix}${stem}.png")`);
    }else layer.style.setProperty('--key-mask',`url("${prefix}${stem}.png")`);
    art.append(layer);
    channels.push([layer,i]);
  }

  if(opts.frameOpacity===1)image('lines.svg');
  else{
    image('button-ring-body.svg');
    if(opts.frameOpacity>0)image('button-ring-plates.svg',opts.frameOpacity);
  }
}

let visibleMask=0,visibleLeds=null;
function render(mask,rgb=null){
  if(mask===visibleMask&&rgb===visibleLeds)return;
  for(const [el,i] of channels){
    const down=isMaskSet(mask,i),wasDown=isMaskSet(visibleMask,i);
    if(down!==wasDown)el.classList.toggle('is-down',down);
    if(useLeds){
      const hex=rgb?.slice(i*6,i*6+6)??null,old=visibleLeds?.slice(i*6,i*6+6)??null;
      if(down!==wasDown||hex!==old){
        const color=ledCap(hex,down,opts.lightOpacity);
        el.style.setProperty('--key-color',color??(root.dataset.geometry==='schematic'?'#f8f8fa':'transparent'));
      }
    }
  }
  visibleMask=mask;visibleLeds=rgb;
}

return startInputFeed({player:opts.player,sensor,render,useLeds,env});
}
