export const DEFAULT_VISITORS = Object.freeze({schemaVersion:'1.0',enabled:true,count:1,speed:1,appearances:[]});
const bad=message=>{throw Object.assign(new Error(message),{status:400});};
const keys=(value,allowed)=>value && typeof value==='object' && !Array.isArray(value) && Object.keys(value).every(key=>allowed.includes(key));
const idPattern=/^[a-z0-9][a-z0-9-]{0,63}$/;
export function validateVisitorCatalog(value){
    if(!value || value.schemaVersion!=='1.0' || !Array.isArray(value.appearances) || value.appearances.length>256) bad('캐릭터 목록이 올바르지 않습니다. 실험 앱 빌드를 확인해 주세요.');
    const seen=new Set();
    const appearances=value.appearances.map(entry=>{
        if(!entry || !idPattern.test(entry.id) || seen.has(entry.id) || typeof entry.displayName!=='string' || !entry.displayName.trim() || entry.displayName.length>80 || typeof entry.available!=='boolean' || !Number.isFinite(entry.weight) || entry.weight<0 || entry.weight>100) bad('캐릭터 목록의 항목이 올바르지 않습니다.');
        seen.add(entry.id);
        return {id:entry.id,displayName:entry.displayName,available:entry.available,weight:entry.weight,creator:String(entry.creator??'').slice(0,160),license:String(entry.license??'').slice(0,240)};
    });
    return {schemaVersion:'1.0',appearances};
}
export function validateVisitorSettings(value,catalog=null){
    if(!keys(value,['schemaVersion','enabled','count','speed','appearances']) || value.schemaVersion!=='1.0' || typeof value.enabled!=='boolean' || !Number.isInteger(value.count) || value.count<0 || value.count>6 || !Number.isFinite(value.speed) || value.speed<.5 || value.speed>1.5 || !Array.isArray(value.appearances) || value.appearances.length>256) bad('관람객은 0~6명, 이동 속도는 0.5~1.5배로 입력해 주세요.');
    const seen=new Set();
    const appearances=value.appearances.map(entry=>{
        if(!keys(entry,['id','enabled','weight']) || !idPattern.test(entry.id) || seen.has(entry.id) || typeof entry.enabled!=='boolean' || !Number.isFinite(entry.weight) || entry.weight<0 || entry.weight>100) bad('외형별 사용 여부와 등장 비중(0~100)을 확인해 주세요.');
        if(catalog && !catalog.appearances.some(item=>item.id===entry.id)) bad('현재 실험 앱에 없는 외형입니다. 저장된 설정을 다시 불러와 주세요.');
        seen.add(entry.id); return {id:entry.id,enabled:entry.enabled,weight:entry.weight};
    });
    return {schemaVersion:'1.0',enabled:value.enabled,count:value.count,speed:value.speed,appearances};
}
