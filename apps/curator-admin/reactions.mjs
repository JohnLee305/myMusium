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
    candidates.general.push(
        `설명에 나온 ${quote} 부분부터 보고 싶네.`,
        `${quote}라는 말이 어떻게 보이는지 궁금하다.`,
        '처음 본 인상과 자세히 본 인상이 다를까?',
        '이 전시품에서 가장 먼저 눈에 들어오는 곳을 찾아봐야겠어.',
        '다른 각도에서도 잠깐 살펴보고 싶다.',
        '소개를 읽으니 그냥 지나치기 아쉽네.',
        '내가 고른다면 어느 부분을 먼저 소개할까?',
        '한 바퀴 둘러본 뒤 다시 보면 느낌이 달라질까?');
    candidates.detail.push(
        `${quote}라는 설명과 실제 모습을 비교하고 싶다.`,
        `원문 속 ${quote}에 해당하는 부분이 궁금하네.`,
        '눈에 띄는 부분 말고 작은 부분도 찾아보고 싶어.',
        '전체 모양을 본 다음 세부로 시선을 옮겨 봐야겠어.',
        '가까이 봤을 때만 보이는 특징이 있을까?',
        '표면과 윤곽을 따로 살펴보면 어떨까?',
        '설명에서 강조한 부분을 내 눈으로 확인하고 싶다.',
        '한 가지 디테일을 골라 오래 바라보고 싶네.');
    candidates.story.push(
        `${quote}라는 소개 뒤의 이야기도 듣고 싶네.`,
        `이 물건을 ${quote}라고 소개한 이유가 궁금하다.`,
        '이 전시품을 여기에 놓기로 한 순간이 궁금해.',
        '소장한 사람에게 어떤 의미가 있는 물건일까?',
        '설명에 더해 어떤 기억을 남기고 싶었을까?',
        '누군가에게 이 전시품을 소개한다면 무슨 말을 할까?',
        '내가 이 물건을 기억한다면 어떤 장면으로 남을까?',
        '전시품을 고른 사람의 시선으로도 바라보고 싶다.');
    candidates.collector.push(
        `${quote}라는 소개가 이 전시품을 구별해 주네.`,
        `목록에 적는다면 ${quote}를 먼저 기록하고 싶다.`,
        '이 전시품 옆에는 어떤 물건이 어울릴까?',
        '같은 주제의 전시품을 함께 보면 무엇이 달라 보일까?',
        '이 컬렉션에서 맡은 자리가 궁금하네.',
        '다음에 다시 왔을 때도 알아볼 특징을 찾아봐야겠어.',
        '한 가지를 골라 기억한다면 어떤 부분이 좋을까?',
        '설명과 모습을 함께 기록해 두고 싶다.');
    for(const persona of PERSONAS) for(let n=0;n<candidates[persona].length;n++){
        const candidate={id:`template-${persona}-${n}`,persona,tier:n===0?'normal':'short',text:candidates[persona][n],evidencePointIds:point&&candidates[persona][n].includes(quote)?[point.id]:[],status:'draft'};
        if(next.reactions.length<48 && !next.reactions.some(r=>r.id===candidate.id)) next.reactions.push(candidate);
    }
    return validateReactions(next,item.id);
}
