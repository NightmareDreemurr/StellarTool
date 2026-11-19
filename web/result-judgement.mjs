const finite=Number.isFinite;
export const toFrames=ms=>ms*.06;
export const urFromPopulationSd=(n,sd)=>Number.isInteger(n)&&n>=2&&finite(sd)&&sd>=0?10*sd:null;
export function timingHistogram(result){
 if(result?.binStartMs!==-150||result.binWidthMs!==5||result.binCount!==60)return null;
 const standard=result?.groups?.standard;
 if(!standard||!Number.isInteger(standard.count)||standard.count<1||!finite(standard.meanMs))return null;
 const bins=standard.histogram;
 if(!Array.isArray(bins)||bins.length!==60||bins.some(n=>!Number.isInteger(n)||n<0))return null;
 const peak=Math.max(1,...bins);
 return {bins,peak,count:standard.count,avg:toFrames(standard.meanMs),ur:urFromPopulationSd(standard.count,standard.populationStdDevMs)};
}
export function resultLevelLabel(song){
 const utage=Number(song.id)>=100000||Boolean(song.utageKanji);
 return utage?(song.utageKanji||'宴'):(['BASIC','ADVANCED','EXPERT','MASTER','Re:MASTER'][song.difficulty]||'—');
}
const fmt=f=>f.toFixed(1).replace(/^-0\.0$/,'0.0');
const sf='http://www.w3.org/2000/svg';
export function drawResultHistogram(doc,model){
 const one=(name,klass,value)=>{const e=doc.createElement(name);if(klass)e.className=klass;if(value!=null)e.textContent=value;return e;};
 const block=one('div','result-judgement');
 const summary=one('div','result-judgement-summary');
 summary.append(one('strong','','Judgement'),one('span','',model&&model.ur!==null?`UR ${fmt(model.ur)}`:'UR —'),
  one('span','',model?`Avg. ${model.avg>=.05?'+':''}${fmt(model.avg)}f`:'Avg. —'));
 if(!model){block.append(summary,one('div','result-judgement-empty','暂无判定数据'));return block;}

 const figure=one('div','result-judgement-figure');
 const svg=doc.createElementNS(sf,'svg');
 svg.setAttribute('viewBox','0 0 240 54');svg.setAttribute('preserveAspectRatio','none');svg.setAttribute('role','img');
 svg.setAttribute('aria-label','判定分布：左 Early，右 Late，每柱 0.3 帧');
 function svgEl(key,props){const node=doc.createElementNS(sf,key);for(const [k,v]of Object.entries(props))node.setAttribute(k,v);return node;}
 svg.append(svgEl('line',{x1:120,x2:120,y1:0,y2:54,stroke:'#9ba9b8','stroke-width':1}));
 for(let i=0;i<model.bins.length;i++){
  const h=54*model.bins[i]/model.peak;if(h===0)continue;
  const blue=i<30,distance=Math.abs(i+0.5-30)/30;
  const rgb=blue?[Math.round(103-distance*78),Math.round(166-distance*64),Math.round(239-distance*30)]:
   [Math.round(244-distance*24),Math.round(141-distance*79),Math.round(154-distance*94)];
  svg.append(svgEl('rect',{x:i*4,y:54-h,width:4,height:h,fill:`rgb(${rgb.join(',')})`}));
 }
 figure.append(svg);
 const axis=one('div','result-judgement-axis');
 axis.append(one('span','','−9f'),one('span','','0f'),one('span','', '+9f'));
 figure.append(axis);block.append(summary,figure);
 return block;
}
