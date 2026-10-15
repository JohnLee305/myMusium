import { COLOR_FIELDS, DEFAULT_THEME, PRESETS, effectiveTheme } from './themes.mjs';
const $ = id => document.getElementById(id);
let data, roomId='', draft={}, dirty=false, saving=false, revision='';
const fields = new Map();
function note(text,error=false) { $('theme-status').textContent=text; $('theme-status').classList.toggle('error',error); }
function changed() { dirty=true; $('theme-preset').value='custom'; paint(); note('미리보기 중 · 저장하면 이 방에만 적용됩니다.'); }
function paint() {
 for(const [key,inputs] of fields) { inputs.color.value=draft[key]; inputs.hex.value=draft[key]; $('theme-preview').style.setProperty('--'+key,draft[key]); }
 const pairs=[['명판',draft.plaqueText,draft.plaqueBackground],['안내판',draft.signText,draft.signBackground],['상세창',draft.detailText,draft.detailBackground]];
 function lum(hex) { const c=hex.slice(1).match(/../g).map(v=>parseInt(v,16)/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4); return c[0]*.2126+c[1]*.7152+c[2]*.0722; }
 const low=pairs.filter(([,a,b])=>(Math.max(lum(a),lum(b))+.05)/(Math.min(lum(a),lum(b))+.05)<4.5).map(([name])=>name);
 $('theme-contrast').textContent=low.length?`${low.join('·')}의 글자와 바탕색이 비슷합니다. 가독성을 확인해 주세요.`:'글자와 바탕색의 대비가 충분합니다.';
 $('theme-save').disabled=saving || !data;
}
function loadRoom(id) {
 const room=data.rooms.find(r=>r.id===id); if(!room) return;
 roomId=id; revision=room.themeRevision; draft=effectiveTheme(room.theme); dirty=false;
 $('theme-room').value=id; $('theme-preview-title').textContent=room.title;
 $('theme-preset').value=Object.entries(PRESETS).find(([,p])=>COLOR_FIELDS.every(([key])=>p.colors[key]===draft[key]))?.[0]||'custom';
 paint(); note('저장된 색상입니다. 변경 후 아래 저장 버튼을 눌러 주세요.');
}
export function syncThemeEditor(next) {
 data=next;
 $('theme-room').replaceChildren(...data.rooms.map(r=>new Option(r.title,r.id)));
 const selected=data.rooms.some(r=>r.id===roomId)?roomId:data.rooms[0]?.id;
 if(dirty && selected===roomId) { $('theme-room').value=roomId; return; }
 loadRoom(selected);
}
export function initThemeEditor(refresh) {
 for(const [id,preset] of Object.entries(PRESETS)) $('theme-preset').append(new Option(preset.name,id));
 $('theme-preset').append(new Option('직접 조정','custom'));
 const groups=new Map();
 for(const [key,label,group] of COLOR_FIELDS) {
  if(!groups.has(group)) { const section=document.createElement('details'); section.open=group==='공간'; const heading=document.createElement('summary'); heading.textContent=group; const grid=document.createElement('div'); grid.className='color-grid'; section.append(heading,grid); $('theme-colors').append(section); groups.set(group,grid); }
  const row=document.createElement('div'); row.className='color-field';
  const caption=document.createElement('label'); caption.htmlFor='color-'+key; caption.textContent=label;
  const color=document.createElement('input'); color.type='color'; color.id='color-'+key; color.setAttribute('aria-label',label+' 색상 선택');
  const hex=document.createElement('input'); hex.type='text'; hex.maxLength=7; hex.pattern='#[0-9a-fA-F]{6}'; hex.required=true; hex.setAttribute('aria-label',label+' 색상 코드');
  color.addEventListener('input',()=>{draft[key]=color.value.toUpperCase();changed();});
  hex.addEventListener('input',()=>{if(/^#[0-9a-fA-F]{6}$/.test(hex.value)){draft[key]=hex.value.toUpperCase();changed();}});
  row.append(caption,color,hex); groups.get(group).append(row); fields.set(key,{color,hex});
 }
 $('theme-room').addEventListener('change',()=>{const id=$('theme-room').value;if(dirty&&!confirm('저장하지 않은 색상이 있습니다. 변경을 취소하고 다른 방을 보시겠어요?')) {$('theme-room').value=roomId;return;}loadRoom(id);});
 $('theme-preset').addEventListener('change',()=>{const id=$('theme-preset').value;if(!PRESETS[id])return;draft={...PRESETS[id].colors};dirty=true;paint();note('테마 미리보기 중 · 저장하면 선택한 방에 적용됩니다.');});
 $('theme-reset').addEventListener('click',()=>{draft={...DEFAULT_THEME};dirty=true;$('theme-preset').value='classic';paint();note('기본색 미리보기 중 · 저장하면 복원됩니다.');});
 $('theme-revert').addEventListener('click',()=>loadRoom(roomId));
 $('theme-form').addEventListener('submit',async event=>{
  event.preventDefault();if(saving||!data)return;saving=true;$('theme-controls').disabled=true;paint();
  try {
   const response=await fetch('/api/room-theme',{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':data.token,'X-Museum-Metadata':encodeURIComponent(JSON.stringify({roomId,theme:draft,themeRevision:revision}))},body:new Uint8Array()});
   const result=await response.json();if(!response.ok)throw new Error(result.error);
   dirty=false;
   try { await refresh(); note('이 방의 테마를 저장했습니다. 관람 앱을 종료한 뒤 다시 실행해 주세요.'); }
   catch {note('저장은 완료됐습니다. 페이지를 새로고침해 결과를 확인해 주세요.',true);}
  } catch(error) {note(error.message,true);}
  finally {saving=false;$('theme-controls').disabled=false;paint();}
 });
 window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
}
