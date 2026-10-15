const $=id=>document.getElementById(id);
let current=null,token='',busy=false,page=0,drafts=new Map();
const pageSize=12;
function notice(text,error=false){$('visitor-status').textContent=text;$('visitor-status').classList.toggle('error',error);}
function ratios(){
    const active=current.catalog.filter(entry=>entry.available && drafts.get(entry.id).enabled && drafts.get(entry.id).weight>0);
    const total=active.reduce((sum,entry)=>sum+drafts.get(entry.id).weight,0);
    for(const row of document.querySelectorAll('.visitor-entry')){
        const value=drafts.get(row.dataset.id),weight=row.dataset.available==='true' && value.enabled?value.weight:0;
        row.querySelector('.visitor-ratio').textContent=total>0?`약 ${(weight/total*100).toFixed(1)}%`:'등장 안 함';
    }
    $('visitor-empty-note').hidden=total>0;
    $('visitor-summary').textContent=`전체 ${current.catalog.length}가지 · 등장 가능 ${active.length}가지 · 입장할 때 무작위 선택`;
    $('visitor-speed-value').textContent=`${Number($('visitor-speed').value).toFixed(1)}배`;
}
function renderRows(){
    const query=$('visitor-search').value.trim().toLocaleLowerCase();
    const filtered=current.catalog.filter(entry=>`${entry.displayName} ${entry.id} ${entry.creator}`.toLocaleLowerCase().includes(query));
    const pages=Math.max(1,Math.ceil(filtered.length/pageSize));page=Math.min(page,pages-1);
    $('visitor-appearances').replaceChildren();
    for(const entry of filtered.slice(page*pageSize,(page+1)*pageSize)){
        const value=drafts.get(entry.id),row=document.createElement('div');row.className='visitor-entry';row.dataset.id=entry.id;row.dataset.available=String(entry.available);
        const label=document.createElement('label'),enabled=document.createElement('input');enabled.type='checkbox';enabled.checked=entry.available && value.enabled;enabled.disabled=!entry.available;
        const title=document.createElement('strong');title.textContent=entry.displayName;label.append(enabled,title);
        const details=document.createElement('p');details.className='hint';details.textContent=entry.available?`${entry.creator} · ${entry.license}`:'현재 실험 앱 빌드에서 사용할 수 없는 외형입니다.';
        const weightLabel=document.createElement('label');weightLabel.textContent='등장 비중';
        const weight=document.createElement('input');weight.type='number';weight.min='0';weight.max='100';weight.step='any';weight.value=value.weight;weight.disabled=!entry.available;weight.setAttribute('aria-label',entry.displayName+' 등장 비중');weightLabel.append(weight);
        const ratio=document.createElement('span');ratio.className='visitor-ratio';row.append(label,details,weightLabel,ratio);
        row.addEventListener('input',()=>{value.enabled=enabled.checked;value.weight=Number(weight.value)||0;ratios();});$('visitor-appearances').append(row);
    }
    $('visitor-page').textContent=`${filtered.length}가지 검색됨 · ${page+1} / ${pages}쪽`;
    $('visitor-previous').disabled=page===0;$('visitor-next').disabled=page===pages-1;ratios();
}
function render(data){
    current=data;page=0;
    drafts=new Map(data.catalog.map(entry=>{const saved=data.settings.appearances.find(value=>value.id===entry.id);return [entry.id,{id:entry.id,enabled:entry.available && (saved?.enabled??true),weight:saved?.weight??entry.weight}];}));
    $('visitor-enabled').checked=data.settings.enabled;$('visitor-count').value=data.settings.count;$('visitor-speed').value=data.settings.speed;
    $('visitor-removed-note').hidden=!data.settings.appearances.some(item=>!data.catalog.some(entry=>entry.id===item.id));
    $('visitor-controls').disabled=false;renderRows();
}
async function reload(){
    if(busy)return;busy=true;$('visitor-controls').disabled=true;
    try{const [sessionResponse,response]=await Promise.all([fetch('/api/session'),fetch('/api/visitors')]);const data=await response.json();if(!response.ok)throw Error(data.error);token=(await sessionResponse.json()).token;render(data);notice('저장된 관람객 설정을 불러왔습니다.');}
    catch(error){notice(error.message||'관람객 설정에 연결하지 못했습니다.',true);}finally{busy=false;}
}
$('visitor-form').addEventListener('submit',async event=>{
    event.preventDefault();if(busy || !current || !token)return;
    const settings={schemaVersion:'1.0',enabled:$('visitor-enabled').checked,count:Number($('visitor-count').value),speed:Number($('visitor-speed').value),appearances:[...drafts.values()]};
    busy=true;$('visitor-controls').disabled=true;notice('관람객 설정을 저장하고 있습니다.');
    try{const response=await fetch('/api/visitors',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:JSON.stringify({revision:current.revision,settings})});const data=await response.json();if(!response.ok)throw Error(data.error);render(data);notice('저장했습니다. 실행 중인 최신 관람 앱에 약 1초 안에 반영되며, 다시 실행해도 유지됩니다.');}
    catch(error){notice(error.message||'저장하지 못했습니다. 입력값은 유지됩니다.',true);}finally{busy=false;$('visitor-controls').disabled=false;}
});
$('visitor-reload').addEventListener('click',reload);
$('visitor-speed').addEventListener('input',()=>{if(current)ratios();});
$('visitor-search').addEventListener('input',()=>{if(current){page=0;renderRows();}});
$('visitor-previous').addEventListener('click',()=>{if(page>0){page--;renderRows();}});
$('visitor-next').addEventListener('click',()=>{page++;renderRows();});
for(const [id,enabled] of [['visitor-all-on',true],['visitor-all-off',false]]) $(id).addEventListener('click',()=>{
    for(const entry of current.catalog) if(entry.available){const draft=drafts.get(entry.id);draft.enabled=enabled;if(enabled && draft.weight===0)draft.weight=entry.weight||1;}
    renderRows();notice('전체 외형 선택을 바꿨습니다. 설정 저장을 눌러 적용해 주세요.');
});
reload();
