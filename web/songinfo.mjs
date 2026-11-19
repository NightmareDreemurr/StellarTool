import {timingHistogram,drawResultHistogram,resultLevelLabel} from './result-judgement.mjs';
import {titleMarquee} from './title-marquee.mjs';
import {rankAsset} from './model.mjs';

const difficulties=['BASIC','ADVANCED','EXPERT','MASTER','Re:MASTER'];
const levelColors=['#28a85c','#e6a916','#db4859','#954bc2','#b795d4','#d94caa'];
const modeLabels={idle:'待机中',login:'正在登录中',mode:'正在选择游玩模式',settling:'正在结算中','map-select':'正在选择地图','partner-select':'正在选择旅行伙伴',disconnected:'游戏未连接'};
const aliases={
  silver:'FC',gold:'FCp',allperfect:'AP',allperfectplus:'APp',
  chainlow:'FS',chainhi:'FSp',synclow:'FSD',synchi:'FSDp',syncplay:'SP',
  fc:'FC','fc+':'FCp',fcp:'FCp',ap:'AP','ap+':'APp',app:'APp',sp:'SP',sync:'SP',
  fs:'FS','fs+':'FSp',fsp:'FSp',fsd:'FSD','fsd+':'FSDp',fsdp:'FSDp',
};
const prefixes={combo:new Set(['FC','FCp','AP','APp']),sync:new Set(['SP','FS','FSp','FSD','FSDp'])};

export function playFlagIcon(flag,lane){
  const normalized=String(flag??'').trim().toLowerCase().replace(/^(?:play(?:combo|sync)flagid\.)/,'');
  const code=aliases[normalized];
  const label=prefixes[lane]?.has(code)?code:'Blank';
  return {label:label==='Blank'?'—':label==='SP'?'SYNC':label.replace(/p$/,'+'),url:`/assets/songinfo/UI_MSS_MBase_Icon_${label}.png`};
}

const finite=v=>typeof v==='number'&&Number.isFinite(v);
const levelText=v=>finite(v)?v.toFixed(1):'—';
export function songLevelLabel(song,result=false){
  const utage=Number(song.id)>=100000||Boolean(song.utageKanji);
  const title=utage?(song.utageKanji||'宴'):(difficulties[song.difficulty]||'—');
  const value=utage?song.utageConstant:song.levels?.[song.difficulty]?.value;
  return (result?trackLabel(song.trackNumber)+' · ':'')+title+' '+levelText(value);
}
const percent=(v,d)=>finite(v)?v.toFixed(d)+'%':'—';
export function songRank(song,result=false){
  if(!finite(song?.achievement))return null;
  const rank=song.rank;
  const text=String(rank??'').trim();
  if((text==="0"||/^(?:Rank_)?Begin$/i.test(text))||(result&&song.achievement===0&&!rankAsset(text)))return 'Rank_D';
  return rankAsset(rank)?String(rank):null;
}
const comparisonFields=new Set(['achievement','rating','dxScore','dxPercent']);

// Missing historical entries are different from a legitimate zero; neither
// rank nor the game’s achievement-flag artwork uses a comparison tint.
export function resultDirection(field,current,previous,isResult=true){
  if(!isResult||!comparisonFields.has(field)||!finite(current)||!finite(previous)||current===previous)return '';
  return current>previous?'better':'worse';
}

export function songInfoView(s,mode='songinfo'){
  if(mode!=='songinfo')return {kind:'empty'};
  const state=s?.state;
  if(Object.prototype.hasOwnProperty.call(modeLabels,state))return {kind:'status',label:modeLabels[state]};
  if(state==='category')return s.category?{kind:'category',category:s.category}:{kind:'status',label:'分类数据未就绪'};
  if(state==='result'||state==='photo')return s.song?.isResult===true?{kind:'song',song:s.song,result:true}:{kind:'status',label:state==='photo'?'正在结算中':'结算资料读取中'};
  if(s?.song)return {kind:'song',song:s.song,result:false};
  return {kind:'status',label:state==='select'||state==='preparing'?'曲目信息读取中':'待机中'};
}

const metricInfo=[
  ['clear','进度'],['average','平均达成率'],
  ['clearRank','Clear Rank',['A','S','S+','SS','SS+','SSS','SSS+']],
  ['combo','FC／AP',['FC','FC+','AP','AP+']],
  ['sync','Sync',['SYNC','FS','FS+','FSD','FSD+']],
  ['dx','DX Score',['1','2','3','4','5']],
];
const categoryBadge={
  A:'CLEAR',S:'S','S+':'Sp',SS:'SS','SS+':'SSp',SSS:'SSS','SSS+':'SSSp',
  FC:'FC','FC+':'FCp',AP:'AP','AP+':'APp',SYNC:'2P',FS:'FS','FS+':'FSp',FSD:'FDX','FSD+':'FDXp',
  1:'dxstar_1',2:'dxstar_2',3:'dxstar_3',4:'dxstar_4',5:'dxstar_5',
};
const nativeCountColors={
 A:'#f12f4f',S:'#6078b6','S+':'#6078b6',SS:'#ff7417','SS+':'#ff7417',SSS:'#f12f4f','SSS+':'#f12f4f',
 FC:'#19a530','FC+':'#19a530',AP:'#ed402e','AP+':'#eb402e',
 SYNC:'#0c86b6',FS:'#0c86b6','FS+':'#0c86b6',FSD:'#e75719','FSD+':'#e75719',
 1:'#5eba0c',2:'#5eba0c',3:'#ec6e1a',4:'#ec6e1a',5:'#f1ac08',
};
export function categoryMetrics(cat={}){
  const total=Number.isInteger(cat.total)&&cat.total>=0?cat.total:null;
  const count=x=>Number.isInteger(x)&&x>=0&&(total===null||x<=total)?x:null;
  return metricInfo.map(([key,label,tiers],index)=>{
    const active=Number.isInteger(cat.tab)&&cat.tab===index;
    if(key==='clear')return {key,label,active,value:count(cat.clear)!==null&&total!==null?`${cat.clear} / ${total}`:'—'};
    if(key==='average')return {key,label,active,value:percent(cat.average,4)};
    const aliases={combo:'fcAp',sync:'syncRanks',dx:'dxStars'};
    const arr=cat[key]??cat[aliases[key]];
    return {key,label,active,tiers:tiers.map((item,i)=>({label:item,url:key==='dx'?stellarDxTierBadge(Number(item)).url:`/assets/songinfo/UI_MSS_PlayStatusBase_${categoryBadge[item]}.png`,value:count(arr?.[i])===null?'—':total>0&&arr[i]===total?'MAX!':String(arr[i])}))};
  });
}

function element(tag,cls='',value,d=document){
  const e=d.createElement(tag);if(cls)e.className=cls;
  if(value!=null)e.textContent=String(value);return e;
}
function bitmap(src,label,cls='',d=document){
  const img=element('img',cls,null,d);img.src=src;img.alt=label;
  return img;
}
function addWithFallback(holder,img,label){
  img.onerror=()=>img.replaceWith(element('span',img.className+' badge-fallback',label,holder.ownerDocument));holder.append(img);
}
function mark(el,field,value,prior,result){
  const direction=resultDirection(field,value,prior,result);
  if(direction){el.classList.add('score-'+direction);el.dataset.comparison=direction;}
  return el;
}
export function categoryAsset(cat={}){
  const asset=String(cat.asset??'');
  if(/^\/assets\/(?:songinfo\/)?UI_[A-Za-z0-9_]+\.png$/.test(asset))return asset;
  const n=String(cat.assetName??'').replace(/\.png$/i,'');
  if(/^UI_CMN_TabTitle_Level_[A-Za-z0-9_]+$/.test(n)){
    const source=String(cat.assetResourcePath??'').replace(/^Common\/Sprites\/Tab\/Title\//,'').replace(/\.png$/i,'');
    if(source===n)return `/assets/songinfo/level-title/${n}.png`;
    if(source==='Level/'+n)return `/assets/songinfo/level-nested/${n}.png`;
    return null; // Same Sprite.name is used by two different official textures.
  }
  return /^UI_(?:(?:CMN|MSS|CLC)_TabTitle_[A-Za-z0-9_]+|TTR_Rank_[A-Za-z0-9_]+)$/.test(n)?`/assets/songinfo/${n}.png`:null;
}
// Official counters are thresholds ("at least"), not a distribution.
const distributionDefinitions=[
 {key:'clearRank',label:'Clear Rank',tab:2,tiers:[
  ['CLEAR','#2d94aa'],['S','#48b68f'],['S+','#9abd57'],['SS','#eda934'],
  ['SS+','#ee884d'],['SSS','#e47791'],['SSS+','#bc5cb7']]},
 {key:'combo',label:'FC / AP',tab:3,tiers:[['FC','#78e700'],['FC+','#5ac600'],['AP','#f8c940'],['AP+','#ff7800']]},
 {key:'sync',label:'Sync',tab:4,tiers:[['SYNC','#75d6ea'],['FS','#40b7d2'],['FS+','#5b8dce'],['FDX','#f1b844'],['FDX+','#e49a36']]},
 {key:'dx',label:'DX Score',tab:5,tiers:[['1★','#78e700'],['2★','#5ac600'],['3★','#ff9a00'],['4★','#ff7800'],['5★','#ffd708']]},
];

export function categoryDistribution(cat={}){
 const total=cat.total;
 const validTotal=Number.isSafeInteger(total)&&total>0;
 return distributionDefinitions.map(({key,label,tab,tiers})=>{
  const counters=cat[key];
  const active=cat.tab===tab||(key==='clearRank'&&cat.tab===0);
  if(!validTotal||!Array.isArray(counters)||counters.length!==tiers.length||
     counters.some((x,i)=>!Number.isSafeInteger(x)||x<0||x>total||(i>0&&x>counters[i-1])))
   return {key,label,active,counts:null};
  const counts=tiers.map(([name,color],i)=>({name,color,count:counters[i]-(counters[i+1]??0)})).reverse();
  counts.push({name:'其他',color:'#bec9cc',count:total-counters[0]});
  return {key,label,active,counts,total,clear:key==='clearRank'?counters[0]:null};
 });
}

// Exact Stellar App rank thresholds (FlutterB50SongPainter.rankFor) and
// DX visuals (score_badges.dart), evaluated before the numbers are rounded.
const averageAssetRoot='/assets/songinfo/stellar-averages/';
export function averageAchievementBadge(value){
  if(!finite(value)||value<0)return null;
  const ranks=[[100.5,'SSSp','SSS+'],[100,'SSS','SSS'],[99.5,'SSp','SS+'],[99,'SS','SS'],
    [98,'Sp','S+'],[97,'S','S'],[94,'AAA','AAA'],[90,'AA','AA'],[80,'A','A'],[75,'BBB','BBB'],
    [70,'BB','BB'],[60,'B','B'],[50,'C','C'],[0,'D','D']];
  const [,name,label]=ranks.find(([threshold])=>value>=threshold);
  return {url:averageAssetRoot+'UI_GAM_Rank_'+name+'.png',label:'Rank '+label};
}
export function stellarDxTierBadge(level){
  return Number.isInteger(level)&&level>=1&&level<=7
    ?{url:averageAssetRoot+'dx_star_'+level+'.png',label:level===7?'DX MAX':`DX ${level} 星`}:null;
}
export function averageDxBadge(value){
  if(!finite(value)||value<85||value>100)return null;
  const [,level]=[[100,7],[99,6],[97,5],[95,4],[93,3],[90,2],[85,1]].find(([threshold])=>value>=threshold);
  return stellarDxTierBadge(level);
}
function categoryAverages(cat,d,badges=false){
  const averages=element('div','category-averages',null,d);
  const avgItem=(label,value,digits,help,badge)=>{
    const el=element('div','category-average',null,d);
    if(help)el.title=help;
    el.append(element('span','',label,d),element('strong','',percent(value,digits),d));
    if(badge)addWithFallback(el,bitmap(badge.url,badge.label,'category-average-badge',d),badge.label);
    averages.append(el);
  };
  avgItem('平均达成率',cat.average,4,null,badges?averageAchievementBadge(cat.average):null);
  if(cat.averageDxStatus||finite(cat.averageDx)){
    const valid=cat.averageDxStatus==='ready'&&finite(cat.averageDx)&&cat.averageDx>=0&&cat.averageDx<=100;
    const help=valid?'分类所有谱面按各自 DX 满分换算，未玩计为 0。':cat.averageDxStatus==='pending'?'正在读取本分类的逐谱数据。':'本分类缺少可验证的 DX 满分或成绩，未计算。';
    avgItem('平均 DX 达成率',valid?cat.averageDx:null,2,help,badges&&valid?averageDxBadge(cat.averageDx):null);
  }
  return averages;
}

function categoryBadges(cat,d,native){
  const wrap=element('div',native?'category-native-summary':'category-metrics',null,d);
  const grid=native?element('div','category-native-grid',null,d):wrap;
  if(native){wrap.append(categoryAverages(cat,d,true));wrap.append(grid);}
  const metrics=categoryMetrics(cat);
  const distributions=native?new Map(categoryDistribution(cat).map(row=>[row.key,row])):null;
  const ordered=native?[metrics[2],metrics[5],metrics[3],metrics[4]]:metrics;
  for(const item of ordered){
    const column=element('section',native?'category-native-group':'category-stat',null,d);column.dataset.metric=item.key;
    if(item.active||native&&item.key==='clearRank'&&cat.tab===0)column.dataset.active='true';
    const heading=native&&item.key==='clearRank'?'Rank':item.label;
    const title=element('div',native?'category-native-title':'category-stat-title',null,d);
    title.append(element('span','category-title-text',heading,d));
    if(native){
      const row=distributions.get(item.key);
      const strip=element('div','category-native-strip',null,d);
      if(row?.counts){
        strip.setAttribute('aria-label',row.counts.map(t=>t.name+' '+t.count).join('；'));
        for(const tier of row.counts){
          if(!tier.count)continue;
          const area=element('span','category-native-strip-segment',null,d);
          area.style.width=(100*tier.count/row.total)+'%';
          area.style.background=tier.color;
          if(tier.name!=='其他')area.append(element('span','category-native-strip-label',tier.name,d));
          strip.append(area);
        }
      }
      title.append(strip);
    }
    column.append(title);
    if(item.tiers){
      const group=element('div',native?'category-native-tiers':'category-tiers',null,d);
      for(const tier of native?[...item.tiers].reverse():item.tiers){
        const cell=element('span',native?'category-native-badge':'category-tier',null,d);cell.title=`${tier.label} ${tier.value}`;
        addWithFallback(cell,bitmap(tier.url,tier.label,'category-tier-icon',d),tier.label);
        const count=element('strong',native?'category-native-count':'category-value',tier.value,d);
        if(native&&nativeCountColors[tier.label])count.style.color=nativeCountColors[tier.label];
        cell.append(count);group.append(cell);
      }
      column.append(group);
    }else column.append(element('strong','category-value category-big-value',item.value,d));
    grid.append(column);
  }
  return wrap;
}

export function resolveCategoryStyle(value){
  return ['icon','bar','hybrid'].includes(value)?value:'legacy';
}

function categoryCard(section,cat,d,style){
  section.classList.add('song-category');
  const cover=element('div','category-cover',null,d);
  const title=String(cat.label||cat.title||'分类');
  const asset=categoryAsset(cat);
  if(asset)addWithFallback(cover,bitmap(asset,title,'category-cover-asset',d),title);
  else cover.append(element('strong','category-label',title,d));
  section.append(cover);
  section.dataset.categoryStyle=style;
  if(style==='legacy'||style==='icon'){
    section.append(categoryBadges(cat,d,style==='icon'));
    if(style==='icon')for(const label of section.querySelectorAll('.category-native-strip-label')){
      const fits=label.getBoundingClientRect().width+4<=label.parentElement.getBoundingClientRect().width;
      label.hidden=!fits;
    }
    return;
  }
  const summary=element('div','category-summary',null,d);
  summary.append(categoryAverages(cat,d));
  const measured=[];
  for(const metric of categoryDistribution(cat)){
    const row=element('div','category-distribution',null,d);
    row.dataset.metric=metric.key;
    if(metric.active)row.dataset.active='true';
    const label=element('div','distribution-label',null,d);
    label.title=metric.label;
    label.append(element('span','distribution-label-name',metric.label,d));
    if(metric.key==='clearRank')label.append(element('span','distribution-label-compact','Clear',d));
    if(metric.clear!==null&&metric.clear!==undefined)
      label.append(element('strong','distribution-clear',`${metric.clear}/${metric.total}`,d));
    const track=element('div','distribution-track',null,d);
    const legend=element('div','distribution-legend',null,d);
    if(metric.counts){
      track.setAttribute('aria-label',metric.counts.map(k=>`${k.name} ${k.count}`).join('；'));
      for(const part of metric.counts){
        if(!part.count)continue;
        const span=element('div','distribution-segment',null,d);span.style.setProperty('--seg-color',part.color);
        span.style.width=(100*part.count/metric.total)+'%';span.title=`${part.name} ${part.count}`;
        const number=element('strong','distribution-number',part.count,d);span.append(number);track.append(span);
        const tag=element('span','distribution-tag',null,d);tag.style.setProperty('--seg-color',part.color);
        const badgeCode=categoryBadge[part.name==='FDX'?'FSD':part.name==='FDX+'?'FSD+':part.name==='CLEAR'?'A':part.name.replace('★','')];
        if(style==='hybrid'&&badgeCode)addWithFallback(tag,bitmap(`/assets/songinfo/UI_MSS_PlayStatusBase_${badgeCode}.png`,part.name,'distribution-native-icon',d),part.name);
        else tag.append(element('span','distribution-tag-name',part.name,d));
        const outside=element('strong','distribution-tag-count',part.count,d);tag.append(outside);legend.append(tag);
        measured.push({span,number,outside});
      }
    }else{
      track.classList.add('distribution-unavailable');
      legend.append(element('span','distribution-unavailable-text','—',d));
    }
    row.append(label,track,legend);summary.append(row);
  }
  section.append(summary);
  // The numeric element remains intrinsically measurable when hidden; do not
  // let a tiny segment clip a count or inflate the bar's true proportion.
  for(const {span,number,outside} of measured){
    const fits=number.getBoundingClientRect().width+6<=span.getBoundingClientRect().width;
    number.classList.toggle('distribution-number-outside',!fits);
    outside.hidden=fits;
  }
}
export function trackLabel(number){
  return Number.isInteger(number)&&number>=1?`TRACK ${String(number).padStart(2,'0')}`:'TRACK —';
}

function songCard(section,song,result,d){
  const judgement=result?timingHistogram(song.hitTiming):null;
  const judgementCard=result&&(judgement||song.photoIndex==null);
  section.classList.toggle('song-with-judgement',!!judgementCard);
  const cover=element('div','cover',null,d);
  if(/^\/assets\/jackets\/UI_Jacket_[0-9]{6}\.png$/.test(song.cover??'')){
    const jacket=bitmap(song.cover,'歌曲封面','song-jacket',d);
    addWithFallback(cover,jacket,'封面资源缺失');
  }else cover.append(element('span','', '封面暂不可用',d));
  if(typeof song.chartType==='string'&&song.chartType){
    const dx=/deluxe|dx/i.test(song.chartType);
    addWithFallback(cover,bitmap(`/assets/${dx?'Deluxe':'Standard'}.png`,dx?'DX':'SD','typebadge',d),dx?'DX':'SD');
  }
  const box=element('div','songmeta',null,d);
  box.append(element('div','song-title',song.title??'—',d),element('div','song-artist',song.artist??'—',d),element('div','','ID '+(song.id??'—'),d));
  const level=element('div','song-level',null,d);const selected=result||song.selection==='difficulty';
  if(Number(song.id)>=100000||song.utageKanji||selected){
    level.textContent=songLevelLabel(song,result);
    level.style.color=levelColors[Number(song.id)>=100000||song.utageKanji?5:song.difficulty]||levelColors[0];
  }else{
    (song.levels||[]).forEach((v,i)=>{
      if(!v?.enabled)return;
      const e=element('span','song-level-item',levelText(v.value),d);
      e.style.color=levelColors[i%levelColors.length];level.append(e);
    });
  }
  if(!judgementCard){
    box.append(level);
    section.append(cover,box);
  }
  if(!selected)return;
  const best=song.previousBest??song.history??null;
  const cmp=(el,field,v)=>mark(el,field,v,best?.[field],result);
  const scores=element('div','scores',null,d),top=element('div','scoretop',null,d);
  const ach=element('div',result?'achievement result-achievement':'achievement',percent(song.achievement,4),d);
  if(!result)cmp(ach,'achievement',song.achievement);
  const rank=element('span','rankgroup',null,d);
  const rankName=songRank(song,result),url=rankAsset(rankName);
  if(url){const label=String(rankName).replace(/^Rank_/i,'').replace(/P$/i,'+');addWithFallback(rank,bitmap(url,label,'rankbadge',d),label);}
  else rank.append(element('span','ranktext','—',d));
  const rating=cmp(element('span','singlerating',finite(song.rating)?' (+'+song.rating+')':'—',d),'rating',song.rating);
  rank.append(rating);top.append(ach,rank);
  const dxPct=finite(song.dxPercent)?song.dxPercent:finite(song.dxScore)&&finite(song.dxMax)&&song.dxMax>0?song.dxScore/song.dxMax*100:null;
  const lastPct=finite(best?.dxPercent)?best.dxPercent:finite(best?.dxScore)&&finite(song.dxMax)&&song.dxMax>0?best.dxScore/song.dxMax*100:null;
  const bottom=element('div','dxline',null,d);
  bottom.append('DX ',cmp(element('span','dx-earned',finite(song.dxScore)?song.dxScore:'—',d),'dxScore',song.dxScore),` / ${song.dxMax??'—'} · `,mark(element('span','dx-percent',percent(dxPct,2),d),'dxPercent',dxPct,lastPct,result));
  const dxArt=Number(song.id)>=100000||song.utageKanji?null:averageDxBadge(dxPct);
  if(dxArt)addWithFallback(bottom,bitmap(dxArt.url,dxArt.label,'dxstar',d),dxArt.label);
  scores.append(top,bottom);
  const badges=element('div','song-flag-stack',null,d);
  for(const [field,lane] of [['combo','combo'],['sync','sync']]){
    const data=playFlagIcon(song[field],lane);
    addWithFallback(badges,bitmap(data.url,data.label,'song-flag song-flag-'+lane,d),data.label);
  }
  if(judgementCard){
    const grades=element('div','result-grades',null,d);
    const summary=element('div','result-grade-details',null,d);
    const resultLevel=element('div','result-track',null,d);
    const value=Number(song.id)>=100000||song.utageKanji
      ?(song.displayLevel??(finite(song.utageConstant)?levelText(song.utageConstant):null)):song.displayLevel;
    resultLevel.textContent=trackLabel(song.trackNumber)+' · '+resultLevelLabel(song)+(value?' '+value:'');
    resultLevel.style.color=levelColors[Number(song.id)>=100000||song.utageKanji?5:song.difficulty]||levelColors[0];
    // Use actual glyph/font/icon sizing. This dormant twin reserves max display widths,
    // so the left chart always receives exactly the space the scores do not need.
    const reserve=element('div','result-score-measure',null,d);
    reserve.setAttribute('aria-hidden','true');
    const a=element('div','result-measure-top',null,d);
    a.append(element('span','achievement','999.9999%',d));
    const r=element('span','result-measure-rank',null,d);
    addWithFallback(r,bitmap(rankAsset('Rank_SSSP'),'','rankbadge',d),'SSS+');
    r.append(element('span','singlerating','(+9999)',d));
    a.append(r);
    const x=element('div','dxline',null,d);
    x.append('DX ',element('span','dx-earned','9999',d),' / 9999 · ',element('span','dx-percent','100.00%',d));
    addWithFallback(x,bitmap(stellarDxTierBadge(7).url,'','dxstar',d),'DX MAX');
    reserve.append(a,x);
    const scoreArea=element('div','result-score-area',null,d);
    scoreArea.append(reserve,scores);
    summary.append(scoreArea,badges);
    grades.append(resultLevel,summary);
    section.append(drawResultHistogram(d,judgement),grades);
  }else section.append(scores,badges);
}

export function renderSongInfo(section,snapshot,idle,categoryStyle){
  const doc=section.ownerDocument;
  const v=songInfoView(snapshot,idle);
  section.className='song';
  section.dataset.songinfo=v.kind;
  delete section.dataset.categoryStyle;
  section.replaceChildren();
  if(v.kind==='status')section.append(element('div','song-status',v.label,doc));
  else if(v.kind==='category')categoryCard(section,v.category,doc,resolveCategoryStyle(categoryStyle));
  else if(v.kind==='song'){songCard(section,v.song,v.result,doc);titleMarquee(section,String(v.song.id)+':'+String(v.song.title));}
}
