export const msToFrames=value=>value*60/1000;
export function cursorColor(value){return /^(?:[0-9a-f]{6}|[0-9a-f]{8})$/i.test(value??'')?'#'+value:'#ffffff';}
export function meterOptions(params){
 const n=(key,fallback,min,max)=>{const v=Number(params.get(key));return params.has(key)&&Number.isFinite(v)?Math.max(min,Math.min(max,v)):fallback;};
 return {width:n('width',800,240,3840),height:n('height',90,70,500),range:n('range',9,1,60),preview:params.get('preview')==='true',cursorColor:cursorColor(params.get('cursorColor'))};
}
// Synthetic, static landmarks for OBS placement; never merged with live samples.
export function previewMeterView(range,now){
 return {available:true,emaOffsetMs:range*.14/.06,ticks:[[-.76,2600],[-.40,1200],[-.19,500],[.12,150],[.34,900],[.68,1900]]
  .map(([part,age],sequence)=>({offsetMs:range*part/.06,at:now-age,sequence}))};
}
export function meterFeed(){
 let key=null,sequence=0,ticks=[],distribution=null,available=false,overflow=0,emaOffsetMs=null;
 return {clear(){key=null;sequence=0;ticks=[];distribution=null;available=false;overflow=0;emaOffsetMs=null;},
 accept(snapshot,now){
  const p=snapshot?.hitOffsets;
  if(snapshot?.state!=='playing'||!p?.available||p.source!=='game-calibrated-hit'||p.schemaVersion!==1||p.session!==snapshot.sessionId||!Array.isArray(p.events)||p.events.length>512||Number(snapshot.ageMs)>2000){this.clear();return;}
  const next=p.session+':'+p.attempt;if(key!==next){this.clear();key=next;}
  if(Number.isSafeInteger(p.sequence)&&p.sequence<sequence)return;
  available=true;distribution=p.distribution??null;overflow=Number(p.overwritten)||0;
  for(const e of p.events){
   if(!Number.isSafeInteger(e.sequence)||e.sequence<=sequence)continue;
   sequence=e.sequence;
   if(e.group!=='standard'||!Number.isFinite(e.offsetMs)||!Number.isFinite(e.gameTime)||!Number.isFinite(snapshot.time))continue;
   const age=(snapshot.time-e.gameTime)*1000;
   if(age< -50||age>=5000)continue;
   emaOffsetMs=emaOffsetMs===null?e.offsetMs:emaOffsetMs*.9+e.offsetMs*.1;
   ticks.push({offsetMs:e.offsetMs,at:now-Math.max(0,age),sequence:e.sequence});
  }
  ticks=ticks.filter(e=>now-e.at<5000).slice(-50);
 },
 view(now){ticks=ticks.filter(e=>now-e.at<5000);return {available,ticks,distribution,overflow,emaOffsetMs};}
 };
}
export function tickOpacity(age){return age<0||age>=5000?0:age<100?.8*(1-(1-age/100)**5):.8*(1-(age-100)/4900);}
