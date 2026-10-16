const $=id=>document.getElementById(id);
let state,nav,order=[],dirty=false,busy=false,refresh,doorMode='entrance',rendering=false;
const room=()=>state?.rooms.find(r=>r.id===$('nav-room').value);
const text=(value,error=false)=>{$('nav-status').textContent=value;$('nav-status').classList.toggle('error',error);};
function choices(){return ['front','back','left','right'].flatMap(side=>Array.from({length:['front','back'].includes(side)?6:5},(_,slot)=>new Option(`${{front:'아래 벽',back:'위 벽',left:'왼쪽 벽',right:'오른쪽 벽'}[side]} · ${slot+1}번째`,`${side}:${slot}`)));}
function doorLabel(d){return `${{front:'아래',back:'위',left:'왼쪽',right:'오른쪽'}[d.side]} 벽 ${d.slot+1}`;}
function doorCell(d){return d.side==='front'?d.slot:d.side==='back'?24+d.slot:d.side==='left'?d.slot*6:d.slot*6+5;}
function render(){
 if(!nav||rendering)return;rendering=true;
 try{
 const r=room();if(!r)return;$('nav-entrance').value=`${nav.entrance.side}:${nav.entrance.slot}`;$('nav-exit').value=`${nav.exit.side}:${nav.exit.slot}`;
 const index=order.indexOf(r.id),next=state.rooms.find(q=>q.id===order[index+1]);
 $('nav-door-summary').textContent=`입구: ${doorLabel(nav.entrance)} → 순서대로 감상 → ${next?'다음 방 문: '+next.title:'박물관 출구'} (${doorLabel(nav.exit)})`;
 const grid=$('nav-grid');grid.replaceChildren();
 for(let row=4;row>=0;row--)for(let col=0;col<6;col++){
  const cell=row*6+col,n=r.cells.indexOf(cell),entry=n<0?null:r.artifacts[n],item=state.artifacts.find(a=>`artifacts/${a.id}/artifact.json`===entry),blocked=nav.blockedCells.includes(cell);
  const b=document.createElement('button');b.type='button';b.className='grid-cell'+(entry?' filled':'')+(blocked?' route-blocked':'');
  b.disabled=busy||!!entry;b.setAttribute('aria-pressed',String(blocked));b.setAttribute('aria-label',`${row+1}행 ${col+1}열 · ${item?.title??(blocked?'통행 금지':'통로')}`);
  const label=[entry?`${nav.visitOrder.indexOf(entry)+1}. ${item?.title??'전시품'}`:blocked?'■ 통행 금지':'통로'];
  if(doorCell(nav.entrance)===cell)label.push('입구 ↗');if(doorCell(nav.exit)===cell)label.push(next?'다음 방 ↗':'출구 ↗');
  b.textContent=label.join(' · ');b.onclick=()=>{nav.blockedCells=blocked?nav.blockedCells.filter(c=>c!==cell):[...nav.blockedCells,cell];change();};grid.append(b);
 }
 list($('nav-exhibits'),nav.visitOrder,id=>state.artifacts.find(a=>`artifacts/${a.id}/artifact.json`===id)?.title??id);
 list($('nav-rooms'),order,id=>state.rooms.find(r=>r.id===id).title);
 renderWalls();
 const modeEntrance=$('nav-mode-entrance'),modeExit=$('nav-mode-exit');
 if(modeEntrance&&modeExit){modeEntrance.textContent=`입구 지정${doorMode==='entrance'?' · 선택 중':''}`;modeExit.textContent=`출구 지정${doorMode==='exit'?' · 선택 중':''}`;modeEntrance.classList.toggle('active',doorMode==='entrance');modeExit.classList.toggle('active',doorMode==='exit');}
 $('nav-room').disabled=busy||dirty;for(const id of ['nav-entrance','nav-exit'])$(id).disabled=busy;
 $('nav-save').disabled=busy||!dirty;$('nav-cancel').disabled=busy||!dirty;
 }finally{rendering=false;}
}
const WALL_SIDES=[['nav-wall-top','back',6],['nav-wall-bottom','front',6],['nav-wall-left','left',5],['nav-wall-right','right',5]];
const WALL_NAME={front:'아래',back:'위',left:'왼쪽',right:'오른쪽'};
function renderWalls(){
 for(const [id,side,count] of WALL_SIDES){
  const box=$(id);if(!box)continue;box.replaceChildren();
  for(let slot=0;slot<count;slot++){
   const b=document.createElement('button');b.type='button';
   const isEntrance=nav.entrance.side===side&&nav.entrance.slot===slot,isExit=nav.exit.side===side&&nav.exit.slot===slot;
   b.className='wall-slot'+(isEntrance?' door-entrance':'')+(isExit?' door-exit':'');
   b.setAttribute('aria-pressed',String(doorMode==='entrance'?isEntrance:isExit));
   b.setAttribute('aria-label',`${WALL_NAME[side]} 벽 ${slot+1}번째 · ${isEntrance?'입구':isExit?'출구':'문 없음'} · 눌러서 ${doorMode==='entrance'?'입구':'출구'} 지정`);
   b.textContent=`${WALL_NAME[side]}${slot+1}${isEntrance?' 입구':isExit?' 출구':''}`;
   b.disabled=busy;b.onclick=()=>{nav[doorMode]={side,slot};change();};
   box.append(b);
  }
 }
}
function setMode(mode){if(doorMode!==mode){doorMode=mode;render();}}
function list(container,values,label){container.replaceChildren();values.forEach((id,i)=>{const line=document.createElement('li');const name=document.createElement('span');name.textContent=`${i+1}. ${label(id)}`;line.append(name);for(const [step,title] of [[-1,'위로'],[1,'아래로']]){const b=document.createElement('button');b.type='button';b.className='secondary';b.textContent=title;b.setAttribute('aria-label',`${label(id)} ${title}`);b.disabled=busy||i+step<0||i+step>=values.length;b.onclick=()=>{[values[i],values[i+step]]=[values[i+step],values[i]];change();};line.append(b);}container.append(line);});}
function change(){dirty=true;render();text('동선 변경 중 · 저장할 때 입구–출구와 모든 감상 위치의 연결을 검사합니다.');}
function load(){nav=structuredClone(room().navigation);order=[...state.roomOrder];dirty=false;render();text('저장된 동선입니다. 빈 칸을 누르면 통행 금지/통로를 바꿉니다.');}
export function syncNavigationEditor(data){if(dirty||busy)return;const selected=$('nav-room').value;state=data;$('nav-room').replaceChildren(...data.rooms.map(r=>new Option(r.title,r.id)));if(data.rooms.some(r=>r.id===selected))$('nav-room').value=selected;load();}
export function initNavigationEditor(refreshFn){refresh=refreshFn;for(const id of ['nav-entrance','nav-exit'])$(id).replaceChildren(...choices());$('nav-room').onchange=load;for(const kind of ['entrance','exit'])$('nav-'+kind).onchange=()=>{const [side,slot]=$('nav-'+kind).value.split(':');nav[kind]={side,slot:Number(slot)};change();};
 $('nav-mode-entrance').onclick=()=>setMode('entrance');$('nav-mode-exit').onclick=()=>setMode('exit');
 $('nav-cancel').onclick=async()=>{dirty=false;await refresh();};
 $('nav-save').onclick=async()=>{busy=true;render();try{const response=await fetch('/api/navigation',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':state.token},body:JSON.stringify({revision:state.revision,roomId:room().id,navigation:nav,roomOrder:order})});const result=await response.json();if(!response.ok)throw Error(result.error);dirty=false;busy=false;await refresh();text('동선을 저장했습니다. 관람 앱을 다시 열면 반영됩니다.');}catch(e){text(e.message,true);}finally{busy=false;render();}};
 window.addEventListener('beforeunload',e=>{if(dirty){e.preventDefault();e.returnValue='';}});
}
