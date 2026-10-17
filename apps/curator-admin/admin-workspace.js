const $=id=>document.getElementById(id);
const roomNames={layout:'전시품 배치',route:'관람 동선',theme:'색상·테마'};
export function routeFor(hash){
 const value=hash.replace(/^#/,''),legacy={'layout-heading':['rooms','layout'],'theme-heading':['rooms','theme'],'route-heading':['rooms','route'],'visitors-heading':['visitors'],'reactions-heading':['artifacts','reactions'],'add-heading':['artifacts','add'],'collection-heading':['artifacts','list']};
 if(legacy[value])return {page:legacy[value][0],tab:legacy[value][1]};
 if(/^rooms-(layout|route|theme)$/.test(value))return {page:'rooms',tab:value.slice(6)};
 if(/^artifacts-(list|add|reactions)$/.test(value))return {page:'artifacts',tab:value.slice(10)};
 return {page:['home','artifacts','visitors'].includes(value)?value:'home',tab:value==='artifacts'?'list':undefined};
}
export function createWorkspace(editors){
 let data=null,selected='',page='home',roomTab='layout',artifactTab='list',scheduled=false;
 const pending=()=>Object.entries(editors).map(([kind,e])=>({kind,...e.state()})).find(s=>s.dirty||s.busy);
 const notice=text=>{$('workspace-notice').textContent=text;$('workspace-notice').hidden=!text;};
 function reflect(){
  const state=pending();$('workspace-room').value=selected;$('workspace-room').disabled=!data||!!state;
  $('room-draft-badge').textContent=state?(state.busy?'저장 중…':'저장하지 않은 변경 있음'):'저장된 상태';
  $('room-draft-badge').classList.toggle('waiting',!!state);
  $('room-change-status').textContent=state?`${roomNames[state.kind]} 내용을 먼저 저장하거나 취소하면 방과 작업을 바꿀 수 있습니다. 메뉴를 이동해도 입력은 유지됩니다.`:'같은 방에서 배치·동선·테마를 전환할 수 있습니다.';
  const room=data?.rooms.find(r=>r.id===selected);$('workspace-room-summary').textContent=room?`${room.count}개 전시품 · 빈 자리 ${30-room.count}개`:'';
  $('room-create').querySelectorAll('input,button').forEach(el=>el.disabled=!!state);
 }
 function selectRoom(id){
  if(id===selected)return true;
  if(!data?.rooms.some(r=>r.id===id)||pending()){reflect();notice('수정 중인 전시실 작업을 저장하거나 취소한 뒤 다른 방을 선택해 주세요.');return false;}
  if(Object.values(editors).some(e=>!e.select(id)))throw Error('전시실 선택을 동기화하지 못했습니다. 새로고침 전에 수정 내용을 확인해 주세요.');
  selected=id;reflect();notice('');return true;
 }
 function activateTab(type,value){
  document.querySelectorAll(`[data-${type}-panel]`).forEach(el=>el.hidden=el.dataset[type+'Panel']!==value);
  document.querySelectorAll(`[data-${type}-tab]`).forEach(el=>{const active=el.dataset[type+'Tab']===value;el.setAttribute('aria-selected',String(active));el.tabIndex=active?0:-1;});
 }
 function show(hash,write=false){
  const route=routeFor(hash);let guarded=false;
  if(route.page==='rooms'){
   const state=pending();roomTab=route.tab??roomTab;
   if(state&&state.kind!==roomTab){roomTab=state.kind;guarded=true;notice('저장하지 않은 작업 화면을 유지했습니다. 저장하거나 취소한 뒤 다른 작업을 선택해 주세요.');}
  }
  if(route.page==='artifacts')artifactTab=route.tab??artifactTab;
  page=route.page;document.querySelectorAll('[data-admin-view]').forEach(el=>el.hidden=el.dataset.adminView!==page);
  document.querySelectorAll('[data-menu]').forEach(el=>{if(el.dataset.menu===page)el.setAttribute('aria-current','page');else el.removeAttribute('aria-current');if(el.dataset.menu==='rooms')el.href='#rooms-'+roomTab;});
  activateTab('room',roomTab);activateTab('artifact',artifactTab);reflect();
  if(!guarded)notice('');
  const canonical='#'+(page==='rooms'?'rooms-'+roomTab:page==='artifacts'?'artifacts-'+artifactTab:page);
  if(write||guarded)history.replaceState(null,'',canonical);
  return !guarded;
 }
 const schedule=()=>{if(!scheduled){scheduled=true;queueMicrotask(()=>{scheduled=false;reflect();});}};
 function init(){
  window.addEventListener('hashchange',()=>show(location.hash));
  document.addEventListener('room-editor-state',schedule);
  for(const type of ['room','artifact']){
   const tabs=[...document.querySelectorAll(`[data-${type}-tab]`)];
   for(const tab of tabs){
    tab.addEventListener('click',()=>show('#'+(type==='room'?'rooms-':'artifacts-')+tab.dataset[type+'Tab'],true));
    tab.addEventListener('keydown',e=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(e.key))return;e.preventDefault();const i=tabs.indexOf(tab),next=e.key==='Home'?0:e.key==='End'?tabs.length-1:(i+(e.key==='ArrowRight'?1:-1)+tabs.length)%tabs.length;tabs[next].click();tabs.find(t=>t.getAttribute('aria-selected')==='true')?.focus();});
   }
  }
  $('workspace-room').addEventListener('change',()=>selectRoom($('workspace-room').value));
  show(location.hash);
 }
 function sync(next){
  data=next;const state=pending(),preferred=state?.roomId||selected;
  selected=next.rooms.some(r=>r.id===preferred)?preferred:next.rooms[0]?.id??'';
  $('workspace-room').replaceChildren(...next.rooms.map(r=>new Option(r.title,r.id)));
  if(!state&&selected)for(const e of Object.values(editors))if(e.state().roomId!==selected)e.select(selected);
  $('home-artifact-count').textContent=String(next.artifacts.length);$('home-room-count').textContent=String(next.rooms.length);reflect();
 }
 function openRoom(id,tab='layout'){
  const state=pending();if(state&&(id!==selected||tab!==state.kind)){show('#rooms-'+state.kind,true);notice('수정 중인 내용을 먼저 저장하거나 취소해 주세요.');return false;}
  if(!selectRoom(id))return false;show('#rooms-'+tab,true);return true;
 }
 return {init,sync,openRoom,show,state:()=>({page,roomTab,artifactTab,roomId:selected})};
}
