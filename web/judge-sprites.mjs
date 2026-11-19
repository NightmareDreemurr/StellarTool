const sprites={};
export function loadJudgeSprites(ImageType=globalThis.Image){
 if(!ImageType)return sprites;
 for(const name of ['Critical_Break','Critical','Perfect','Great','Good','Miss','FastPerfect','LatePerfect','FastGreat','LateGreat','FastGood','LateGood'])if(!sprites[name]){
  const image=new ImageType();image.src=new URL(`./assets/judgements/UI_GAM_${name}.png`,import.meta.url).href;sprites[name]=image;
 }
 return sprites;
}
// SDEZ TAP Type3C: direction-colored grade sprite, no FastLate child.
// Preserve the full 223x82 centered rectangle and original texture colors.
export function tapJudgeSprite(meaning){
 const name=meaning.grade[0]+meaning.grade.slice(1).toLowerCase();
 return ['Perfect','Great','Good'].includes(name)&&['FAST','LATE'].includes(meaning.side)?
  (meaning.side==='FAST'?'Fast':'Late')+name:name;
}
// Native additive overlay: black is no-op; white every alternate two 60Hz frames.
export function breakFlash(age){return Number.isFinite(age)&&age>=0&&age<.25&&Math.floor((age+1e-9)*30)%2===1;}
export function drawJudgeSprites(ctx,meaning,x,y,{alpha=1,width=110,images=loadJudgeSprites(),isBreak=false,age=0}={}){
 const name=tapJudgeSprite(meaning);
 const draw=(key,cy,w)=>{const image=images[key];if(image?.complete&&image.naturalWidth>0){const h=w*image.naturalHeight/image.naturalWidth;ctx.drawImage(image,x-w/2,cy-h/2,w,h);return true;}return false;};
 ctx.globalAlpha=alpha;
 if(!draw(name,y,width)&&typeof ctx.fillText==='function'){
  const label=meaning.grade==='CRITICAL'?'CRITICAL PERFECT':meaning.grade;
  const tone=meaning.side==='FAST'?'#174ecc':meaning.side==='LATE'?'#b51d36':'#242841';
  ctx.save();ctx.textAlign='center';ctx.textBaseline='middle';ctx.font='bold '+Math.max(10,Math.round(width*.145))+'px system-ui';
  ctx.lineWidth=3;ctx.strokeStyle='#fff';ctx.fillStyle=tone;
  ctx.strokeText(label,x,y,width);ctx.fillText(label,x,y,width);ctx.restore();
 }
 if(isBreak&&meaning.grade==='CRITICAL'&&breakFlash(age)){
  // Canvas lighter approximates the original SrcAlpha/One additive material.
  // Native Add alpha stays 1; here it inherits the requested custom root fade.
  const prior=ctx.globalCompositeOperation;ctx.globalCompositeOperation='lighter';draw('Critical_Break',y,width);ctx.globalCompositeOperation=prior;
 }
}

// Warm assets before the first actual judgement rather than during its short life.
loadJudgeSprites();
