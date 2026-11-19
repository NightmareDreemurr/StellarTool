import test from 'node:test';
import assert from 'node:assert/strict';
import {songInfoView,playFlagIcon,resultDirection,categoryMetrics,songRank,categoryAsset,categoryDistribution,trackLabel,songLevelLabel,averageAchievementBadge,averageDxBadge} from '../web/songinfo.mjs';

test('real states supersede any stale song or result data',()=>{
  assert.equal(songInfoView({state:'idle',song:{isResult:true}}).label,'待机中');
  assert.equal(songInfoView({state:'login'}).label,'正在登录中');
  assert.equal(songInfoView({state:'mode'}).label,'正在选择游玩模式');
  assert.equal(songInfoView({state:'disconnected',song:{isResult:true}}).label,'游戏未连接');
  assert.equal(songInfoView({state:'result',song:{selection:'difficulty'}}).label,'结算资料读取中');
  const s={state:'result',song:{isResult:true,achievement:98,previousBest:{achievement:100}}};
  assert.equal(songInfoView(s).song.achievement,98);
  assert.equal(songInfoView({state:'select',song:{achievement:100}}).result,false);
  assert.equal(songInfoView(s,'empty').kind,'empty');
});
test('game combo and sync icon assets are the distinct difficulty-screen assets',()=>{
  for(const [lane,mapping] of Object.entries({
    combo:{Silver:'FC',Gold:'FC+',AllPerfect:'AP',AllPerfectPlus:'AP+',None:'—'},
    sync:{ChainLow:'FS',ChainHi:'FS+',SyncLow:'FSD',SyncHi:'FSD+',SyncPlay:'SYNC',None:'—'},
  })){
    for(const [name,label] of Object.entries(mapping)){
      const icon=playFlagIcon(name,lane);assert.equal(icon.label,label);
      assert.match(icon.url,/^\/assets\/songinfo\/UI_MSS_MBase_Icon_\w+\.png$/);
    }
  }
  assert.equal(playFlagIcon('Gold','sync').label,'—');
});
test('result deltas are numeric only, with better=red and worse=blue at point of comparison',()=>{
  assert.equal(resultDirection('achievement',100.1234,100.1233),'better');
  assert.equal(resultDirection('rating',299,307),'worse');
  assert.equal(resultDirection('dxPercent',99,99),'');
  assert.equal(resultDirection('dxScore',1010,null),'');
  assert.equal(resultDirection('dxScore',0,1),'worse');
  assert.equal(resultDirection('rank',1,0),'');
  assert.equal(resultDirection('combo',2,1),'');
  assert.equal(resultDirection('sync',4,1),'');
  assert.equal(resultDirection('achievement',0,100,false),'');
});
test('six complete real category tabs render in official order and keep missing values separate',()=>{
  const category={total:10,clear:10,average:99.1275,clearRank:[10,9,8,7,6,4,0],combo:[10,8,4,2],sync:[3,2,1,1,0],dx:[10,5,4,3,1],tab:2};
  assert.equal(songInfoView({state:'category',category}).kind,'category');
  const m=categoryMetrics(category);
  assert.deepEqual(m.map(x=>x.key),['clear','average','clearRank','combo','sync','dx']);
  assert.equal(m[0].value,'10 / 10');assert.equal(m[1].value,'99.1275%');
  assert.equal(m[2].active,true);assert.equal(m[2].tiers[0].value,'MAX!');
  assert.equal(m[2].tiers[6].value,'0');assert.equal(categoryMetrics({})[3].tiers[0].value,'—');
  for(const metric of m)for(const t of metric.tiers??[])
    assert.match(t.url,/^\/assets\/songinfo\/[^/]+(?:\/[^/]+)?\.png$/);
});

test('a real 0 percent result uses the Rank D asset, while a missing historical score stays blank',()=>{
  for(const rank of ['Begin','Rank_Begin',0,'Rank_D',undefined])
    assert.equal(songRank({achievement:0,rank},true),'Rank_D');
  assert.equal(songRank({achievement:1.3135,rank:'Begin'},true),'Rank_D');
  assert.equal(songRank({achievement:null,rank:'Begin'},false),null);
  assert.equal(songRank({rank:'Rank_D'},false),null);
  assert.equal(songRank({achievement:100.5,rank:'Rank_SSSP'},true),'Rank_SSSP');
});

test('official category artwork uses the actual runtime Sprite name without arbitrary paths',()=>{
  for(const t of ['Chugeki','Niconico','Original','PopsAnime','Toho','Utage','Variety','MaimaiTitle_Ver270']){
    const f=categoryAsset({assetName:'UI_CMN_TabTitle_'+t});
    assert.equal(f,`/assets/songinfo/UI_CMN_TabTitle_${t}.png`);
  }
  const level='UI_CMN_TabTitle_Level_01';
  assert.equal(categoryAsset({assetName:level}),null);
  assert.equal(categoryAsset({assetName:level,assetResourcePath:'Common/Sprites/Tab/Title/'+level}),`/assets/songinfo/level-title/${level}.png`);
  assert.equal(categoryAsset({assetName:level,assetResourcePath:'Level/'+level+'.png'}),`/assets/songinfo/level-nested/${level}.png`);
  assert.notEqual(categoryAsset({assetName:level,assetResourcePath:level}),categoryAsset({assetName:level,assetResourcePath:'Level/'+level}));

  assert.equal(categoryAsset({assetName:'../bad.png'}),null);
  assert.equal(categoryAsset({asset:'https://example.org/asset.png'}),null);
});

test('distribution segment counts are exclusive, ranked best first and fill the same total',()=>{
  const c={total:18,clear:16,clearRank:[16,14,10,8,5,3,1],combo:[10,7,4,1],sync:[12,9,7,2,1],dx:[10,7,5,3,1],tab:0};
  const rows=categoryDistribution(c);
  assert.deepEqual(rows.map(x=>x.key),['clearRank','combo','sync','dx']);
  assert.deepEqual(rows[0].counts.map(x=>x.name),['SSS+','SSS','SS+','SS','S+','S','CLEAR','其他']);
  assert.deepEqual(rows[0].counts.map(x=>x.count),[1,2,2,3,2,4,2,2]);
  assert.equal(rows[0].clear,16);assert.equal(rows[0].active,true);
  for(const {counts,total} of rows)assert.equal(counts.reduce((sum,x)=>sum+x.count,0),total);
});

test('unplayed charts can form a plain other segment; inconsistent and missing stats are not fabricated',()=>{
  const allUnplayed=categoryDistribution({total:5,combo:[0,0,0,0]});
  assert.deepEqual(allUnplayed[1].counts.map(x=>x.count),[0,0,0,0,5]);
  assert.equal(categoryDistribution({total:4,combo:[4,3,4,0]})[1].counts,null);
  assert.equal(categoryDistribution({total:4,combo:[5,2,1,0]})[1].counts,null);
  assert.equal(categoryDistribution({total:4,dx:[1,0]})[3].counts,null);
  assert.equal(categoryDistribution({total:0,clearRank:[0,0,0,0,0,0,0]})[0].counts,null);
});

test('regular and selected-photo results use their own track; other settlement clears stale song',()=>{
  assert.equal(trackLabel(1),'TRACK 01');assert.equal(trackLabel(12),'TRACK 12');
  assert.equal(trackLabel(null),'TRACK —');
  const results=[{isResult:true,trackNumber:1,achievement:95},{isResult:true,trackNumber:2,achievement:100}];
  assert.equal(songInfoView({state:'result',song:results[0]}).song.trackNumber,1);
  assert.equal(songInfoView({state:'photo',song:results[1]}).song.trackNumber,2);
  assert.equal(songInfoView({state:'settling',song:results[0]}).label,'正在结算中');
  assert.equal(songInfoView({state:'photo',song:{...results[0],isResult:false}}).label,'正在结算中');
});

test('Averages use exact Stellar thresholds before rounding and the App assets',()=>{
  for(const [v,n] of [[0,'D'],[49.9999,'D'],[50,'C'],[94,'AAA'],[99.4999,'SS'],[99.5,'SSp'],[100.5,'SSSp']]){
    const b=averageAchievementBadge(v);
    assert.ok(b.url.endsWith(`Rank_${n}.png`));
  }
  for(const [v,n] of [[85,1],[90,2],[93,3],[95,4],[97,5],[98.9999,5],[99,6],[99.9999,6],[100,7]]){
    const b=averageDxBadge(v);assert.ok(b.url.endsWith(`dx_star_${n}.png`));
  }
  assert.equal(averageDxBadge(84.9999),null);assert.equal(averageDxBadge(100.01),null);
  assert.equal(averageAchievementBadge(null),null);
  assert.equal(averageDxBadge(null),null);
});

test('map and travel-partner choice never render a stale settling/result card',()=>{
  const s={song:{isResult:true,trackNumber:2}};
  assert.equal(songInfoView({...s,state:'map-select'}).label,'正在选择地图');
  assert.equal(songInfoView({...s,state:'partner-select'}).label,'正在选择旅行伙伴');
  assert.equal(songInfoView({...s,state:'settling'}).label,'正在结算中');
});
test('UTAGE displays the captured actual chart constant independent of an inherited difficulty',()=>{
  const base={id:100003,utageKanji:'宴',difficulty:3,levels:[{value:12.7,enabled:true},{},{},{value:0,enabled:false}],utageConstant:12.7};
  assert.equal(songLevelLabel(base),'宴 12.7');
  assert.equal(songLevelLabel({...base,selection:'difficulty'}),'宴 12.7');
  assert.equal(songLevelLabel({...base,photoIndex:2,trackNumber:2},true),'TRACK 02 · 宴 12.7');
  assert.equal(songLevelLabel({...base,utageKanji:'',utageConstant:null}),'宴 —');
  assert.equal(songLevelLabel({id:101, difficulty:0,levels:[{value:9}]}),'BASIC 9.0');
});
