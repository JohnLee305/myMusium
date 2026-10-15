import crypto from 'node:crypto';

export const PERSONAS=['general','detail','story','collector'];
export const MAX_REACTION_BYTES=64*1024;
const bad=message=>{throw Object.assign(new Error(message),{status:400});};
const id={test:value=>typeof value==='string'&&/^[a-z0-9][a-z0-9-]{0,63}$/.test(value)};
const exact=(v,fields)=>v && typeof v==='object' && !Array.isArray(v) && Object.keys(v).length===fields.length && fields.every(k=>Object.hasOwn(v,k));
const text=(v,max,multiline=false)=>typeof v==='string' && v.trim().length>0 && v.length<=max && !(multiline?/[\u0000-\u0008\u000b\u000c\u000e-\u001f\u007f<>]/:/[\u0000-\u001f\u007f<>]/).test(v) && !/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/u.test(v);
export const sourceHash=item=>crypto.createHash('sha256').update(`${item.title.length}:${item.title}${item.description.length}:${item.description}`,'utf8').digest('hex');
export function emptyReactions(item){return {schemaVersion:'1.0',artifactId:item.id,enabled:true,source:{title:item.title,description:item.description,sha256:sourceHash(item)},points:[],reactions:[]};}
export function validateReactions(v,artifactId){
    if(!exact(v,['schemaVersion','artifactId','enabled','source','points','reactions']) || v.schemaVersion!=='1.0' || !id.test(v.artifactId) || v.artifactId!==artifactId || typeof v.enabled!=='boolean') bad('전시품 반응 형식과 ID를 확인해 주세요.');
    if(!exact(v.source,['title','description','sha256']) || !text(v.source.title,80,true) || !text(v.source.description,400,true) || !/^[a-f0-9]{64}$/.test(v.source.sha256) || sourceHash(v.source)!==v.source.sha256) bad('반응의 원문과 지문이 일치하지 않습니다.');
    if(!Array.isArray(v.points) || v.points.length>16 || !Array.isArray(v.reactions) || v.reactions.length>48) bad('감상 포인트는 16개, 반응은 48개까지 저장할 수 있습니다.');
    const points=new Set(),seen=new Set();
    for(const p of v.points){
        if(!exact(p,['id','evidence']) || !id.test(p.id) || points.has(p.id) || !text(p.evidence,400,true) || !(v.source.title.includes(p.evidence)||v.source.description.includes(p.evidence))) bad('감상 근거는 원문에서 그대로 가져와야 합니다.');
        points.add(p.id);
    }
    for(const r of v.reactions){
        if(!exact(r,['id','persona','tier','text','evidencePointIds','status']) || !id.test(r.id) || seen.has(r.id) || !PERSONAS.includes(r.persona) || !['short','normal','rare'].includes(r.tier) || !text(r.text,72) || !['draft','approved','rejected'].includes(r.status) || !Array.isArray(r.evidencePointIds) || r.evidencePointIds.length>16 || new Set(r.evidencePointIds).size!==r.evidencePointIds.length || r.evidencePointIds.some(p=>!points.has(p))) bad('반응 문구·성향·승인 상태·근거를 확인해 주세요. 문구는 72자 이하입니다.');
        seen.add(r.id);
    }
    if(Buffer.byteLength(JSON.stringify(v))>MAX_REACTION_BYTES) bad('반응 파일은 64KiB 이하여야 합니다.');
    return v;
}
// Quotes are copied from the supplied description; no unsupported factual claims are inferred.
export function generateReactions(item,previous=null){
    const next=emptyReactions(item);
    if(previous && previous.source.sha256===next.source.sha256){
        next.enabled=previous.enabled;next.points=structuredClone(previous.points);next.reactions=structuredClone(previous.reactions);
    }
    const sentence=item.description.split(/[\n.!?。！？]+/).map(s=>s.trim()).find(Boolean)||item.description;
    const fragment=Array.from(sentence).slice(0,18).join('');
    const evidence=fragment;
    let point=next.points.find(p=>p.evidence===evidence);
    if(!point && next.points.length<16){point={id:'point-'+crypto.createHash('sha256').update(evidence).digest('hex').slice(0,16),evidence};next.points.push(point);}
    const quote='‘'+fragment+(sentence.length>fragment.length?'…':'')+'’';
    const candidates={general:[`${quote}라는 소개가 눈에 들어오네.`, '어떤 이야기가 담겨 있을지 더 읽어 보고 싶다.'],detail:[`설명에 적힌 ${quote} 부분을 다시 살펴봐야겠어.`, '작은 부분도 천천히 살펴보고 싶네.'],story:[`${quote}라는 이야기가 기억에 남네.`, '이 물건을 소개한 사람의 이야기가 궁금하다.'],collector:[`${quote}라는 설명과 함께 감상하니 새롭네.`, '다른 전시품과 나란히 감상해 보고 싶다.']};
    for(const persona of PERSONAS) for(let n=0;n<candidates[persona].length;n++){
        const candidate={id:`template-${persona}-${n}`,persona,tier:n===0?'normal':'short',text:candidates[persona][n],evidencePointIds:n===0&&point?[point.id]:[],status:'draft'};
        if(next.reactions.length<48 && !next.reactions.some(r=>r.id===candidate.id)) next.reactions.push(candidate);
    }
    return validateReactions(next,item.id);
}
