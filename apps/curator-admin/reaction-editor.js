const $=id=>document.getElementById(id);
const labels={general:'일반',detail:'디테일',story:'이야기',collector:'수집가'};
const tierLabels={short:'짧은 반응',normal:'감상',rare:'드문 반응'};
let current=null,token='',busy=false,dirty=false,epoch=0,page=0,visibleIds=[];
const pageSize=12;
const notice=(text,error=false)=>{$('reaction-status').textContent=text;$('reaction-status').classList.toggle('error',error);};
function summary(){
    const all=current.document.reactions;
    $('reaction-summary').textContent=`전체 ${all.length}개 · 승인 ${all.filter(r=>r.status==='approved').length}개 · 검토 중 ${all.filter(r=>r.status==='draft').length}개 · 거절 ${all.filter(r=>r.status==='rejected').length}개`;
}
function renderRows(){
    const query=$('reaction-search').value.trim().toLocaleLowerCase(),persona=$('reaction-filter-persona').value,status=$('reaction-filter-status').value;
    const matches=current.document.reactions.filter(r=>(persona==='all'||r.persona===persona)&&(status==='all'||r.status===status)&&(!query||r.text.toLocaleLowerCase().includes(query)));
    const pages=Math.max(1,Math.ceil(matches.length/pageSize));page=Math.min(page,pages-1);
    const slice=matches.slice(page*pageSize,(page+1)*pageSize);visibleIds=slice.map(r=>r.id);
    $('reaction-rows').replaceChildren();
    for(const r of slice){
        const row=document.createElement('article');row.className='reaction-row';row.dataset.id=r.id;
        const heading=document.createElement('h3');heading.textContent=labels[r.persona]+' · '+tierLabels[r.tier];
        const evidence=document.createElement('p');evidence.className='hint';evidence.textContent=r.evidencePointIds.map(id=>current.document.points.find(p=>p.id===id)?.evidence).filter(Boolean).join(' / ')||'직접 작성한 감상·질문입니다. 외형 관찰과 원문에 근거하는지 확인해 주세요.';
        const input=document.createElement('textarea');input.maxLength=72;input.rows=2;input.value=r.text;input.setAttribute('aria-label',heading.textContent+' 문구');
        const select=document.createElement('select');select.setAttribute('aria-label',heading.textContent+' 승인 상태');
        for(const [value,label] of [['draft','검토 중'],['approved','승인'],['rejected','거절']]){const option=document.createElement('option');option.value=value;option.textContent=label;select.append(option);}select.value=r.status;
        input.addEventListener('input',()=>{r.text=input.value;r.status='draft';select.value='draft';dirty=true;summary();notice('문구를 수정했습니다. 다시 승인하고 저장해 주세요.');});
        select.addEventListener('change',()=>{r.status=select.value;dirty=true;summary();});
        row.append(heading,evidence,input,select);$('reaction-rows').append(row);
    }
    if(!matches.length)$('reaction-rows').textContent=current.document.reactions.length?'검색 조건에 맞는 후보가 없습니다.':'아직 반응이 없습니다. 후보 만들기로 시작해 주세요.';
    $('reaction-page').textContent=`검색 ${matches.length}개 · ${page+1} / ${pages}쪽`;
    $('reaction-previous').disabled=page===0;$('reaction-next').disabled=page===pages-1;
    $('reaction-approve-page').disabled=!slice.length||current.stale||!!current.error;
    $('reaction-reject-page').disabled=!slice.length||current.stale||!!current.error;
    summary();
}
function render(data){
    current=data;dirty=false;page=0;
    $('reaction-source').textContent=data.artifact.title+'\n'+data.artifact.description;
    $('reaction-warning').textContent=data.error||(data.stale?'원문이 바뀌어 이전 승인은 관람에 표시되지 않습니다. 새 원문으로 후보를 만들고 다시 승인해 주세요.':'승인한 문구만 표시합니다. 근거와 표현을 직접 확인해 주세요.');
    $('reaction-enabled').checked=data.document.enabled;
    $('reaction-history').replaceChildren();
    for(const name of data.history){const option=document.createElement('option');option.value=name;option.textContent=new Date(Number(name.match(/-(\d{13})-/)[1])).toLocaleString()+' · '+name.slice(-13,-5);$('reaction-history').append(option);}
    $('reaction-restore').disabled=!data.history.length;$('reaction-save').disabled=!!data.error||data.stale;
    renderRows();
}
async function request(action){
    if(busy||!current)return;
    if(action==='generate'&&(dirty||current.stale)&&!window.confirm('새 원문으로 후보를 만들면 현재 화면의 수정 내용이 바뀝니다. 저장된 이전 문구는 백업됩니다. 계속할까요?'))return;
    if(action==='restore'&&!window.confirm('선택한 이전 저장으로 되돌릴까요? 현재 저장본도 백업합니다.'))return;
    busy=true;$('reaction-controls').disabled=true;$('reaction-artifact').disabled=true;
    try{
        const response=await fetch('/api/reactions',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:JSON.stringify({action,artifactId:current.artifact.id,revision:current.revision,sourceHash:current.sourceHash,document:action==='save'?current.document:undefined,backup:action==='restore'?$('reaction-history').value:undefined})});
        const data=await response.json();if(!response.ok)throw Error(data.error);render(data);notice(action==='generate'?`반응 후보 ${data.document.reactions.length}개를 준비했습니다. 검토·승인 후 저장해 주세요.`:'저장했습니다. 관람 앱을 다시 열면 반영됩니다.');
    }catch(error){notice(error.message,true);}finally{busy=false;$('reaction-controls').disabled=false;$('reaction-artifact').disabled=false;}
}
async function load(){
    if(busy)return;
    if(dirty&&!window.confirm('저장하지 않은 수정 내용을 취소하고 불러올까요?')){$('reaction-artifact').value=current.artifact.id;return;}
    const id=$('reaction-artifact').value;if(!id){current=null;$('reaction-controls').disabled=true;return;}
    const version=++epoch;busy=true;$('reaction-controls').disabled=true;$('reaction-artifact').disabled=true;
    try{const response=await fetch('/api/reactions?artifactId='+encodeURIComponent(id));const data=await response.json();if(!response.ok)throw Error(data.error);if(version===epoch){render(data);notice('원문과 저장된 반응을 불러왔습니다.');}}
    catch(error){current=null;notice(error.message,true);}finally{busy=false;$('reaction-artifact').disabled=false;$('reaction-controls').disabled=!current;}
}
async function list(){
    if(busy)return;if(dirty&&!window.confirm('저장하지 않은 수정 내용을 취소하고 목록을 다시 불러올까요?'))return;
    try{
        const response=await fetch('/api/state');const data=await response.json();if(!response.ok)throw Error(data.error);
        token=data.token;const selected=$('reaction-artifact').value;$('reaction-artifact').replaceChildren();
        for(const item of data.artifacts.filter(item=>!item.error)){const option=document.createElement('option');option.value=item.id;option.textContent=item.title;$('reaction-artifact').append(option);}
        if([...$('reaction-artifact').options].some(o=>o.value===selected))$('reaction-artifact').value=selected;
        dirty=false;await load();if(!current)notice('전시품을 등록하면 반응을 만들 수 있습니다.');
    }catch(error){notice(error.message,true);}
}
for(const id of ['reaction-search','reaction-filter-persona','reaction-filter-status'])$(id).addEventListener('input',()=>{if(current){page=0;renderRows();}});
$('reaction-previous').addEventListener('click',()=>{if(page>0){page--;renderRows();}});
$('reaction-next').addEventListener('click',()=>{page++;renderRows();});
for(const [id,status] of [['reaction-approve-page','approved'],['reaction-reject-page','rejected']])$(id).addEventListener('click',()=>{
    if(!current||busy)return;
    for(const r of current.document.reactions)if(visibleIds.includes(r.id))r.status=status;
    dirty=true;renderRows();notice('이 페이지의 검토 상태를 바꿨습니다. 저장을 눌러 적용해 주세요.');
});
$('reaction-artifact').addEventListener('change',load);$('reaction-reload').addEventListener('click',list);
$('reaction-enabled').addEventListener('change',()=>{current.document.enabled=$('reaction-enabled').checked;dirty=true;});
for(const action of ['generate','save','restore'])$('reaction-'+action).addEventListener('click',()=>request(action));
$('reaction-add').addEventListener('click',()=>{
    if(!current||current.document.reactions.length>=48){notice('현재는 최대48개입니다. 기존 후보 문구를 수정해 사용해 주세요.',true);return;}
    const r={id:'manual-'+crypto.randomUUID(),persona:$('reaction-persona').value,tier:'short',text:'어떤 이야기가 담겨 있을까?',evidencePointIds:[],status:'draft'};
    current.document.reactions.push(r);dirty=true;$('reaction-search').value='';$('reaction-filter-persona').value='all';$('reaction-filter-status').value='all';page=Math.floor((current.document.reactions.length-1)/pageSize);renderRows();
});
window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
list();
