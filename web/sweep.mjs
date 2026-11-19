/** Outer-ring rhythm chains. Port of Stellar rhythm_sizing.dart (2026-11-18).
 * Input lanes are 0..7; tick is musical integer time, timeMs is absolute chart ms.
 * Head wins at a shared endpoint when the following chain changes rhythm.
 */
export function detectSweeps(events) {
  const grouped = new Map();
  for (const e of events) {
    if (e.connected || e.type === 'touch' || e.type === 'touchHold') continue;
    if (!Number.isFinite(e.tick) || !Number.isFinite(e.timeMs) || !Number.isInteger(e.lane) || e.lane < 0 || e.lane > 7) continue;
    let s = grouped.get(e.tick);
    if (!s) grouped.set(e.tick, s = {tick:e.tick,timeMs:e.timeMs,lanes:new Set()});
    s.lanes.add(e.lane);
  }
  const slots = [...grouped.values()].sort((a,b)=>a.tick-b.tick);
  const add = (p,d)=>(p+d+8)%8;
  function build(start,lane,direction) {
    if (start+1>=slots.length || !slots[start+1].lanes.has(add(lane,direction))) return null;
    const interval=slots[start+1].tick-slots[start].tick;
    const dt=slots[start+1].timeMs-slots[start].timeMs;
    if(interval<=0 || dt<0 || dt>=133) return null;
    const indices=[start,start+1],positions=[lane,add(lane,direction)];
    let cursor=start+2;
    while(cursor<slots.length) {
      const last=indices.at(-1),target=slots[last].tick+interval;
      while(cursor<slots.length && slots[cursor].tick<target) cursor++;
      if(cursor===slots.length || slots[cursor].tick!==target) break;
      const dt=slots[cursor].timeMs-slots[last].timeMs, next=add(positions.at(-1),direction);
      if(dt<0 || dt>=133 || !slots[cursor].lanes.has(next)) break;
      indices.push(cursor);positions.push(next);cursor++;
    }
    return indices.length>=3 ? {indices,positions,direction,interval}:null;
  }
  const candidates=[];
  for(let i=0;i<=slots.length-3;) {
    const found=[];
    for(const p of slots[i].lanes) for(const d of [-1,1]) {const c=build(i,p,d);if(c) found.push(c);}
    candidates.push(...found);
    i=found.length?Math.max(...found.map(c=>c.indices.at(-1))):i+1;
  }
  for(let s=0;s<slots.length;s++) if(slots[s].lanes.size>=2) {
    for(const p of slots[s].lanes) for(const d of [-1,1]) {
      const c=build(s,p,d);
      if(c&&!candidates.some(o=>o.indices[0]===s&&o.positions[0]===p&&o.direction===d)) candidates.push(c);
    }
  }
  let chains=candidates.filter(c=>!candidates.some(o=>o!==c&&o.direction===c.direction&&o.indices[0]<c.indices[0]&&c.indices[0]<o.indices.at(-1)));
  function usage() {
    const result=new Map();
    for(const c of chains) c.indices.forEach((s,k)=>{if(!result.has(s))result.set(s,new Set());result.get(s).add(c.positions[k]);});
    return result;
  }
  let changed=true;
  while(changed) {
    changed=false;
    const heads=new Map();
    for(const c of chains) {const s=c.indices[0];if(!heads.has(s))heads.set(s,new Set());heads.get(s).add(c.positions[0]);}
    for(const c of chains) for(let k=1;k<c.indices.length-1;k++) {
      const s=c.indices[k];
      if(slots[s].lanes.size>=2 && [...slots[s].lanes].some(p=>p!==c.positions[k]&&heads.get(s)?.has(p))) {
        c.indices.length=k+1;c.positions.length=k+1;changed=true;break;
      }
    }
    let used=usage();
    for(const c of chains) while(c.indices.length>=3) {
      const s=c.indices.at(-1);
      if(slots[s].lanes.size<2 || [...slots[s].lanes].every(p=>used.get(s)?.has(p))) break;
      c.indices.pop();c.positions.pop();changed=true;
    }
    const before=chains.length;chains=chains.filter(c=>c.indices.length>=3);changed ||= before!==chains.length;
    used=usage();
    const reject=new Set();
    for(const c of chains) if(c.indices.length===3) {
      const axis=c.positions[1],beforeTick=slots[c.indices[0]].tick-c.interval,afterTick=slots[c.indices.at(-1)].tick+c.interval;
      for(let j=0;j<slots.length;j++) if((slots[j].tick===beforeTick||slots[j].tick===afterTick)&&slots[j].lanes.size===1&&slots[j].lanes.has(axis)&&!used.get(j)?.size) reject.add(c);
    }
    if(reject.size) {chains=chains.filter(c=>!reject.has(c));changed=true;}
  }
  const result=slots.map(s=>({...s,lanes:[...s.lanes].sort((a,b)=>a-b),role:'solo',isEnd:false}));
  for(const c of chains) result[c.indices[0]].role='head';
  for(const c of chains) {for(const i of c.indices.slice(1)) if(result[i].role!=='head') result[i].role='tail';result[c.indices.at(-1)].isEnd=true;}
  return result;
}
