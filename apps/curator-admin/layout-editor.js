const $=id=>document.getElementById(id);
let state,roomId='',draft=[],selected=null,dirty=false,editing=false,busy=false,moving=false,baseRevision='',refresh;
const ref=id=>`artifacts/${id}/artifact.json`;
const cellName=n=>`${Math.floor(n/6)+1}행 ${n%6+1}열`;
function status(text,error=false){$('layout-status').textContent=text;$('layout-status').classList.toggle('error',error);}
function occupied(n){return draft.find(e=>e.cell===n);}
function itemFor(entry){return state.artifacts.find(a=>ref(a.id)===entry);}
function controls(){
 $('layout-room').disabled=busy||dirty||editing;
 $('layout-save').disabled=busy||!dirty;
 $('layout-cancel').disabled=busy||(!dirty&&!editing);
 $('layout-auto').disabled=busy;
 $('item-fields').disabled=busy||dirty||!selected;
 $('layout-move').disabled=busy||!selected||editing;
 $('layout-move').textContent=moving?'이동할 칸을 클릭하세요 · 취소':'선택한 전시품 위치 바꾸기';
}
function render(){
 const grid=$('room-grid');grid.replaceChildren();
 // Back wall is at the top; row 1 is nearest the entrance, matching Unity +Z.
 for(let row=4;row>=0;row--)for(let col=0;col<6;col++){
  const cell=row*6+col,entry=occupied(cell),item=entry&&itemFor(entry.entry),blocked=state.rooms.find(r=>r.id===roomId)?.navigation.blockedCells.includes(cell);
  const button=document.createElement('button');button.type='button';button.className='grid-cell'+(entry?' filled':'')+(blocked?' route-blocked':'')+(entry?.entry===selected?' selected':'');
  button.dataset.cell=cell;button.disabled=busy||editing||blocked;button.setAttribute('aria-label',`${cellName(cell)} · ${item?.title??(blocked?'통행 금지':'빈 칸')}`);button.setAttribute('aria-pressed',String(!!entry&&entry.entry===selected));
  const num=document.createElement('small');num.textContent=cellName(cell);const title=document.createElement('span');title.textContent=item?.title??(blocked?'■ 통행 금지':'＋ 빈 칸');button.append(num,title);
  button.draggable=!!entry&&!editing;
  button.addEventListener('dragstart',e=>{selected=entry.entry;e.dataTransfer.setData('text/plain',selected);e.dataTransfer.effectAllowed='move';});
  button.addEventListener('dragover',e=>{if(!busy&&!editing)e.preventDefault();});
  button.addEventListener('drop',e=>{e.preventDefault();if(busy||editing||blocked)return;const source=e.dataTransfer.getData('text/plain');if(draft.some(a=>a.entry===source)){selected=source;move(cell);}});
  button.addEventListener('click',()=>{if(selected&&(moving||!entry))move(cell);else if(entry){selected=entry.entry;render();fillEditor();}});
  grid.append(button);
 }
 controls();
}
function move(cell){
 const source=draft.find(e=>e.entry===selected);if(!source)return;
 const target=occupied(cell);if(source.cell===cell){moving=false;render();return;}
 if(target)target.cell=source.cell;source.cell=cell;dirty=true;moving=false;
 render();fillEditor();status('배치 변경 중입니다. 저장하면 관람 앱을 다시 열 때 반영됩니다.');
}
function destinations(roomSelect,cellSelect,preferred){
 roomSelect.replaceChildren(...state.rooms.map(r=>new Option(`${r.title} (${r.count}/30)`,r.id)));
 roomSelect.value=state.rooms.some(r=>r.id===preferred)?preferred:state.rooms[0].id;
 const update=()=>{const room=state.rooms.find(r=>r.id===roomSelect.value);cellSelect.replaceChildren(...Array.from({length:30},(_,n)=>n).filter(n=>!room.cells.includes(n)&&!room.navigation.blockedCells.includes(n)).map(n=>new Option(cellName(n),String(n))));};
 roomSelect.onchange=update;update();
}
function fillEditor(){
 const item=selected&&itemFor(selected);$('item-editor').hidden=!item;
 if(!item)return;
 $('edit-name').textContent=item.title;$('edit-title').value=item.title;$('edit-description').value=item.description;$('edit-scale').value=item.scale;
 $('edit-location').textContent=cellName(draft.find(e=>e.entry===selected).cell);
 destinations($('move-room'),$('move-cell'),state.rooms.find(r=>r.id!==roomId&&r.count<30)?.id||roomId);
 controls();
}
function loadRoom(){
 const room=state.rooms.find(r=>r.id===roomId)||state.rooms[0];roomId=room.id;$('layout-room').value=roomId;
 draft=room.artifacts.map((entry,i)=>({entry,cell:room.cells[i]}));selected=null;dirty=false;editing=false;moving=false;baseRevision=state.revision;
 $('item-editor').hidden=true;render();status('전시품을 끌어 놓거나, 선택 후 위치 바꾸기를 누르고 칸을 선택하세요. 겹치면 서로 자리를 바꿉니다.');
}
async function post(endpoint,meta){
 if(busy)return false;busy=true;render();
 try{const response=await fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':state.token,'X-Museum-Metadata':encodeURIComponent(JSON.stringify({revision:baseRevision,...meta}))},body:new Uint8Array()});const result=await response.json();if(!response.ok)throw new Error(result.error);
 dirty=false;editing=false;selected=null;await refresh();loadRoom();status('저장했습니다. 관람 앱을 다시 열면 반영됩니다.');return true;
 }catch(error){status(error.message,true);return false;}finally{busy=false;render();}
}
function trashList(){
 $('trash-count').textContent=state.trash.length;$('trash-list').replaceChildren();
 for(const item of state.trash){
  const card=document.createElement('article');card.className='trash-card';const title=document.createElement('strong');title.textContent=item.title;
  const room=document.createElement('select'),cell=document.createElement('select'),restore=document.createElement('button');
  room.setAttribute('aria-label',`${item.title} 복원할 방`);cell.setAttribute('aria-label',`${item.title} 복원할 칸`);destinations(room,cell,item.roomId);
  if([...cell.options].some(o=>o.value===String(item.cell)))cell.value=String(item.cell);
  restore.type='button';restore.className='secondary';restore.textContent='복원';restore.disabled=!!item.error;
  restore.onclick=()=>{if(dirty||editing)return status('작업 중인 내용을 저장하거나 취소한 뒤 복원해 주세요.',true);if(cell.value==='')return status('빈 칸이 있는 방을 골라 주세요.',true);post('/api/restore',{artifactId:item.id,roomId:room.value,cell:Number(cell.value)});};
  card.append(title,room,cell,restore);$('trash-list').append(card);
 }
}
export function syncLayoutEditor(data){
 if(dirty||editing){status('저장하지 않은 작업을 유지했습니다. 다른 변경이 있으면 취소 후 다시 불러와 주세요.');return;}
 state=data;const previous=roomId;$('layout-room').replaceChildren(...data.rooms.map(r=>new Option(`${r.title} · ${r.count}/30`,r.id)));roomId=data.rooms.some(r=>r.id===previous)?previous:data.rooms[0].id;loadRoom();trashList();
}
export function selectLayoutItem(item){
 if(dirty||editing){status('작업 중인 내용을 먼저 저장하거나 취소해 주세요.',true);$('layout-heading').scrollIntoView();return;}
 roomId=item.roomId;loadRoom();selected=ref(item.id);render();fillEditor();$('layout-heading').scrollIntoView({behavior:'smooth'});
}
export function initLayoutEditor(refreshFn){
 refresh=refreshFn;
 $('layout-room').onchange=()=>{roomId=$('layout-room').value;loadRoom();};
 $('layout-move').onclick=()=>{moving=!moving;controls();};
 $('layout-auto').onclick=()=>{if(editing)return status('설명 수정을 저장하거나 취소해 주세요.',true);const blocked=state.rooms.find(r=>r.id===roomId).navigation.blockedCells;const free=Array.from({length:30},(_,i)=>i).filter(i=>!blocked.includes(i));draft.sort((a,b)=>a.cell-b.cell).forEach((e,i)=>e.cell=free[i]);dirty=true;render();fillEditor();status('입구 쪽부터 순서대로 정돈했습니다. 배치 저장을 눌러 주세요.');};
 $('layout-cancel').onclick=async()=>{dirty=false;editing=false;await refresh();};
 $('layout-save').onclick=()=>post('/api/layout',{roomId,entries:draft});
 $('edit-form').oninput=()=>{editing=true;controls();render();};
 $('edit-form').onsubmit=e=>{e.preventDefault();const item=itemFor(selected);post('/api/edit',{artifactId:item.id,itemRevision:item.itemRevision,title:$('edit-title').value,description:$('edit-description').value,scale:Number($('edit-scale').value)});};
 $('move-item').onclick=()=>{if(editing)return status('설명 수정을 먼저 저장하거나 취소해 주세요.',true);if($('move-room').value===roomId)return status('현재 방 안에서는 격자를 이용해 주세요.',true);if($('move-cell').value==='')return status('빈 칸이 있는 방을 골라 주세요.',true);post('/api/move',{artifactId:itemFor(selected).id,roomId:$('move-room').value,cell:Number($('move-cell').value)});};
 $('trash-item').onclick=()=>{if(editing)return status('설명 수정을 먼저 저장하거나 취소해 주세요.',true);post('/api/trash',{artifactId:itemFor(selected).id});};
 window.addEventListener('beforeunload',event=>{if(dirty||editing){event.preventDefault();event.returnValue='';}});
}
