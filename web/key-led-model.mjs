// Device output and switch state are independent. Null is unavailable, 48 zero
// digits is a real lights-off command; both use the unlit white cap visually.
export const ledCommandFreshMs=1600;
export const unlitColor=[248,249,250];
export function buttonLedFrame(packet,player,now){
 const x=packet?.buttonLeds?.[player-1];
 if(packet?.connected!==true||!Number.isFinite(packet.ageMs)||packet.ageMs<0||packet.ageMs>=250||
    x?.source!=='maimoller-native-hid-write'||!Number.isFinite(x.ageMs)||x.ageMs<0||x.ageMs>=ledCommandFreshMs||
    typeof x.rgb!=='string'||!/^[\da-f]{48}$/i.test(x.rgb))return null;
 return {rgb:x.rgb.toLowerCase(),expires:now+ledCommandFreshMs-x.ageMs};
}
export function ledCap(rgb,pressed,lightOpacity=1) {
 const lit=typeof rgb==='string'&&rgb.length===6&&rgb!=='000000'&&lightOpacity>0;
 if(!lit&&!pressed)return null;
 const channels=lit?[0,2,4].map(i=>parseInt(rgb.slice(i,i+2),16)):unlitColor;
 const strength=lit ? .94*Math.sqrt(Math.max(...channels)/255)*lightOpacity : 0;
 return 'rgb('+channels.map((x,i)=>{
  const c=Math.round(strength*x+(1-strength)*unlitColor[i]);
  return pressed?Math.round(c*.76):c;
 }).join(',')+')';
}
