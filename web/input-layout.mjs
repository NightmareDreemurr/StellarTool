const integer = (raw, fallback, min, max) =>
  /^[0-9]+$/.test(raw ?? '') ? Math.min(max, Math.max(min, Number(raw))) : fallback;

export function parseInputOptions(query, kind = 'keys') {
  const q = new URLSearchParams(query);
  const color = (key, pattern, fallback) => {
    const raw = (q.get(key) ?? '').replace(/^#/, '');
    return pattern.test(raw) ? '#' + raw : fallback;
  };
  const opacity = name => {
    const v=q.get(name);
    return /^(?:0(?:\.[0-9]+)?|1(?:\.0+)?|\.[0-9]+)$/.test(v??'') ? Number(v) : 1;
  };
  return {
    size: integer(q.get('size'), 3840, 16, 8192),
    player: q.get('player') === '2' ? 2 : 1,
    background: color('background', /^(?:[0-9a-f]{6}|[0-9a-f]{8})$/i, '#00000000'),
    light: color('light', kind === 'sensor' ? /^(?:[0-9a-f]{6}|[0-9a-f]{8})$/i : /^[0-9a-f]{6}$/i, kind === 'sensor' ? '#51E9FFF5' : '#EC2127'),
    outline: kind !== 'sensor' || !['false','0'].includes(q.get('outline')?.toLowerCase()),
    ...(kind==='sensor'?{labels:['true','1'].includes(q.get('labels')?.toLowerCase())}:{frameOpacity:opacity('frameOpacity'),lightOpacity:opacity('lightOpacity')}),
  };
}

// SDEZ TestMode TouchPanel prefab, exported from the shipped CustomGraphic polygons.
export function sensorGeometry(data) {
  if (data?.schemaVersion !== 1 || data?.areas?.length !== 34 ||
      data.viewBox?.join(' ') !== '0 0 1080 1080') throw new Error('Missing sensor geometry');
  const layout = data.areas.map(({name,bit,points,label}) => {
    const point = a => Array.isArray(a) && a.length === 2 && a.every(Number.isFinite);
    if (!/^(?:[ABDE][1-8]|C[12])$/.test(name) || inputBit(name) !== bit ||
        !point(label) || !Array.isArray(points) || points.length < 3 || !points.every(point)) {
      throw new Error('Invalid sensor geometry');
    }
    return {id:name,bit,points:points.map(p=>p.join(',')).join(' '),label};
  });
  if (new Set(layout.map(x=>x.bit)).size !== 34) throw new Error('Duplicate sensor area');
  return layout;
}

export const keyLayout = Array.from({length:8},(_,i)=>({id:String(i+1),angle:22.5+i*45}));

export function isMaskSet(mask, index) {
  // JS bitwise operations truncate at 32 bits: E7 and E8 must survive.
  return Number.isSafeInteger(mask) && mask >= 0 && index >= 0 && index < 34 &&
    Math.floor(mask / 2**index) % 2 === 1;
}

export function inputBit(id) {
  return {A:0, B:8, C:16, D:18, E:26}[id[0]] + Number(id.slice(1)) - 1;
}

export function playerMasks(snapshot, player) {
  const p = snapshot?.players?.find?.(p => p?.player === player - 1);
  if (snapshot?.connected !== true || snapshot?.schemaVersion !== 1 || !p) return { touch:0, keys:0 };
  return {
    touch:p.touchAvailable === true && /^[0-3][0-9a-f]{8}$/i.test(p.touchMask ?? '') ? parseInt(p.touchMask,16) : 0,
    keys:p.keysAvailable === true && Number.isInteger(p.keyMask) && p.keyMask >= 0 && p.keyMask <= 255 ? p.keyMask : 0,
  };
}
