// Observation-only seek guard. Never interpolate skipped notes into hit effects.
// Poll receipt time is local monotonic time; producer clocks are not subtracted.
export function practiceTimeline(){
 let previous=null,reset=true,effectStart=Infinity;
 return {
  accept(snapshot,receivedAt){
   const time=Number(snapshot?.time),playing=snapshot?.state==='playing';
   const valid=playing&&Number.isFinite(time)&&typeof snapshot.sessionId==='string'&&snapshot.sessionId.length>0;
   if(!valid){if(previous)reset=true;previous=null;effectStart=Infinity;return;}
   const chartReady=Array.isArray(snapshot.chart?.events);
   const next={time,at:receivedAt,session:snapshot.sessionId,paused:!!snapshot.paused,chartReady};
   let discontinuity=!previous||next.session!==previous.session||next.paused!==previous.paused||next.chartReady!==previous.chartReady;
   if(previous&&!discontinuity){
    const elapsed=Math.max(0,(receivedAt-previous.at)/1000);
    const expected=previous.paused?0:elapsed;
    const delta=time-previous.time;
    // Allow ordinary polling jitter, not backwards seek or a jump across notes.
    discontinuity=delta<-.10||Math.abs(delta-expected)>.35;
   }
   if(discontinuity){reset=true;effectStart=time;}
   previous=next;
  },
  consumeReset(){const value=reset;reset=false;return value;},
  get effectStartTime(){return effectStart;},
 };
}
export function cornerReady(snapshot){
 return snapshot?.state==='playing'&&Number.isFinite(snapshot.time)&&
  typeof snapshot.sessionId==='string'&&snapshot.sessionId.length>0&&Array.isArray(snapshot.chart?.events);
}
// A seek is an effect-layer discontinuity, not a change of screen.
export function practiceFrame(snapshot,{chartLoading=false,reset=false}={}){
 if(snapshot?.state!=='playing')return {corner:'hide',rhythm:'idle'};
 const coherent=cornerReady(snapshot)&&!chartLoading;
 return {corner:coherent&&!reset?'update':'hold',rhythm:coherent&&!reset?'chart':'backdrop'};
}
export function showTaikoEffect(noteTime,time,{effectStartTime=-Infinity,effectsEnabled=true}={}){
 return effectsEnabled&&noteTime>effectStartTime&&time>=noteTime&&time-noteTime<.28;
}

// Bound only playback reads. Even an adapter ignoring abort cannot deliver a
// late value into the caller after the timeout has won.
export async function playbackRead(request,{timeoutMs=1500,env=globalThis}={}){
 const controller=new env.AbortController();let timer;
 const deadline=new Promise((_,reject)=>{timer=env.setTimeout(()=>{
  const error=new Error('Playback request timed out');error.name='TimeoutError';
  reject(error);controller.abort();
 },timeoutMs);});
 try{return await Promise.race([Promise.resolve().then(()=>request(controller.signal)),deadline]);}
 finally{env.clearTimeout(timer);}
}
