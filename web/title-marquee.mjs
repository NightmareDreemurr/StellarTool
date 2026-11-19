// Preserve the actual title node/animation across telemetry-only card rebuilds.
const states=new WeakMap();
export function titleMarquee(section,key){
 apply(section,key,'song-title');apply(section,key+':artist','song-artist');
}
function apply(section,key,cls){
 const placeholder=section.querySelector('.'+cls);if(!placeholder)return;
 let map=states.get(section);if(!map){map=new Map();states.set(section,map);}
 let state=map.get(cls);
 key+=':'+placeholder.textContent;
 if(!state||state.key!==key){
  state?.observer?.disconnect();state?.animation?.cancel();
  state={key,node:placeholder,text:placeholder.textContent,animation:null,width:0,content:0};map.set(cls,state);
  const doc=section.ownerDocument,track=doc.createElement('span'),first=doc.createElement('span'),copy=doc.createElement('span');
  track.className='song-title-track';first.textContent=copy.textContent=state.text;
  copy.className='song-title-copy';copy.setAttribute('aria-hidden','true');track.append(first,copy);placeholder.replaceChildren(track);
  placeholder.setAttribute('title',state.text);placeholder.setAttribute('aria-label',state.text);
  state.track=track;state.first=first;
  doc.fonts?.ready.then(()=>measure(state));
  if(globalThis.ResizeObserver){state.observer=new ResizeObserver(()=>measure(state));state.observer.observe(placeholder);}
 }else if(state.node!==placeholder)placeholder.replaceWith(state.node);
 measure(state);
}
function measure(s){
 const width=s.node.clientWidth,content=s.first.getBoundingClientRect().width;
 if(width===s.width&&content===s.content)return;s.width=width;s.content=content;
 s.animation?.cancel();s.animation=null;
 const reduced=globalThis.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
 const overflow=width>0&&content>width+1&&!reduced&&typeof s.track.animate==='function';
 s.node.classList.toggle('title-scrolling',overflow);
 if(!overflow)return;
 const distance=content+32,travel=distance/28,hold=1.5,total=hold+travel;
 s.animation=s.track.animate([{transform:'translateX(0)',offset:0},{transform:'translateX(0)',offset:hold/total},{transform:`translateX(-${distance}px)`,offset:1}],{duration:total*1000,iterations:Infinity,easing:'linear'});
}
