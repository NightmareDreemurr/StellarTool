import {meterFeed,meterOptions,msToFrames,tickOpacity,previewMeterView} from './judgement-meter-model.mjs';
const opts=meterOptions(new URLSearchParams(location.search)),canvas=document.querySelector('canvas'),ctx=canvas.getContext('2d'),feed=meterFeed();
const dpr=Math.min(2,devicePixelRatio||1);canvas.width=opts.width*dpr;canvas.height=opts.height*dpr;canvas.style.width=opts.width+'px';canvas.style.height=opts.height+'px';ctx.scale(dpr,dpr);
let received=-Infinity;
async function poll(){const abort=new AbortController(),timer=setTimeout(()=>abort.abort(),1500);try{const response=await fetch('/api/state?light=1&judgement=1',{signal:abort.signal,cache:'no-store'});if(!response.ok)throw Error();const s=await response.json();received=performance.now();feed.accept(s,received);}catch{feed.clear();}finally{clearTimeout(timer);setTimeout(poll,document.hidden?500:100);}}
function draw(){
 const now=performance.now();if(now-received>2000)feed.clear();const v=opts.preview?previewMeterView(opts.range,now):feed.view(now),{width:w,height:h,range}=opts;
 ctx.clearRect(0,0,w,h);
 if(v.available){
  const left=55,right=w-55,center=w/2,y=h*.58,x=f=>Math.max(left,Math.min(right,center+f/range*(right-left)/2));
  ctx.lineWidth=2;ctx.strokeStyle='#8d9bad';ctx.beginPath();ctx.moveTo(left,y);ctx.lineTo(right,y);ctx.stroke();
  ctx.fillStyle='#566174';ctx.textAlign='center';ctx.font='700 12px MiSans,sans-serif';ctx.fillText(`−${range} f`,left,h-5);ctx.fillText('0',center,h-5);ctx.fillText(`+${range} f`,right,h-5);
  ctx.strokeStyle='#566174';ctx.beginPath();ctx.moveTo(center,y-18);ctx.lineTo(center,y+12);ctx.stroke();
  for(const e of v.ticks){const f=msToFrames(e.offsetMs);ctx.globalAlpha=tickOpacity(now-e.at);ctx.strokeStyle=f<0?'#287ccc':'#d34b65';ctx.lineWidth=2;ctx.beginPath();if(Math.abs(f)>range){const edge=x(f),sign=f<0?-1:1;ctx.moveTo(edge-sign*5,y-7);ctx.lineTo(edge,y);ctx.lineTo(edge-sign*5,y+7);}else{ctx.moveTo(x(f),y-14);ctx.lineTo(x(f),y+10);}ctx.stroke();}
  ctx.globalAlpha=1;
  // Recent actual-input EMA cursor; stats remain in DTO for settlement, not this HUD.
  if(Number.isFinite(v.emaOffsetMs)){const ax=x(msToFrames(v.emaOffsetMs));ctx.fillStyle=opts.cursorColor;ctx.beginPath();ctx.moveTo(ax-5,y-24);ctx.lineTo(ax+5,y-24);ctx.lineTo(ax,y-18);ctx.fill();}
 }
 if(!opts.preview)requestAnimationFrame(draw);
}
if(opts.preview){draw();document.fonts.ready.then(draw);}else{poll();draw();}
