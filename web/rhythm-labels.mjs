// Stellar rhythm_analysis.dart rhythmLabelVisibility. Values, not notes, compact.
export function rhythmLabelIsCompact(label){
 if(/^\d+$/.test(label))return Number(label)>16;
 if(/^\d+\.$/.test(label))return Number(label.slice(0,-1))>24;
 const m=/^(\d+)\/(\d+)$/.exec(label);
 return !!m&&Number(m[2])>0&&Number(m[1])>16*Number(m[2]);
}
export function rhythmLabelVisibility(notes){
 const visible=notes.map(()=>false);
 for(let i=0;i<notes.length;){
  const label=notes[i].label;
  if(label==null){i++;continue;}
  if(!rhythmLabelIsCompact(label)){visible[i++]=true;continue;}
  let end=i,step;
  while(end+1<notes.length&&notes[end+1].label===label){
   const delta=notes[end+1].time-notes[end].time;
   if(delta<=0||(step!==undefined&&Math.abs(delta-step)>1e-6*step))break;
   step??=delta;end++;
  }
  const hasTail=notes.slice(i,end+1).some(n=>n.size!=='full');
  for(let j=i;j<=end;j++)visible[j]=hasTail?notes[j].size==='full':j===i;
  i=end+1;
 }
 return visible;
}
