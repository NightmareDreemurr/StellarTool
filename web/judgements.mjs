// Semantics of committed SDEZ NoteJudge.ETiming, not inferred from chart time.
export function judgeMeaning(timing){
 if(!Number.isInteger(timing)||timing<0||timing>14)return null;
 const grade=timing===0||timing===14?'MISS':timing===1||timing===13?'GOOD':
  timing>=2&&timing<=4||timing>=10&&timing<=12?'GREAT':
  timing===7?'CRITICAL':'PERFECT';
 return {grade,side:timing>=1&&timing<=6?'FAST':timing>=8&&timing<=13?'LATE':null};
}
export const judgeColors={CRITICAL:'#ffb621',PERFECT:'#f5c52b',GREAT:'#e889ed',GOOD:'#69bfef',MISS:'#8b91a6'};
// Taiko-inspired overlay pop, not an extracted animation: quick expansion,
// gentle settle and short vertical exit. Native TAP pair geometry stays rigid.
export const judgeMotionDuration=.38;
export function judgeFlight(age,height=100){
 if(!Number.isFinite(age)||age<0||age>=judgeMotionDuration)return null;
 const p=age/judgeMotionDuration,ease=1-(1-p)**3;
 const scale=age<.045?.92+.16*(1-(1-age/.045)**2):
  age<.12?1+.08*(1-(age-.045)/.075)**2:1;
 const fade=Math.max(0,(age-.09)/(judgeMotionDuration-.09));
 return {rise:height*.85*ease,alpha:(1-fade)**2,scale};
}
export function judgementFeed(){
 let key=null,sequence=0,events=[];
 return {
  clear(){key=null;sequence=0;events=[];},
  accept(packet,{session,time,effectStartTime=-Infinity}={}){
   if(packet?.source!=='game-judgement'||packet.schemaVersion!==1||!packet.available||packet.session!==session||
      !Number.isFinite(time)||!Array.isArray(packet.events)||packet.events.length>128){this.clear();return;}
   const next=packet.session+':'+packet.epoch;
   if(key!==next){key=next;sequence=0;events=[];}
   for(const event of packet.events){
    if(!Number.isSafeInteger(event.sequence)||event.sequence<=sequence||!Number.isSafeInteger(event.noteIndex)||
       event.noteIndex<0||!Number.isFinite(event.gameTime)||!judgeMeaning(event.timing))continue;
    sequence=event.sequence;
    if(event.gameTime<=effectStartTime||time-event.gameTime>.5||event.gameTime-time>.05)continue;
    events.push({...event,...judgeMeaning(event.timing)});
   }
   events=events.filter(e=>e.gameTime>effectStartTime&&time-e.gameTime<.5).slice(-128);
  },
  active(time,cutoff=-Infinity){
   events=events.filter(e=>e.gameTime>cutoff&&time-e.gameTime<.5);
   return events.filter(e=>time>=e.gameTime);
  },
 };
}
