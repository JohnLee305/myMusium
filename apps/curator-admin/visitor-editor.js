const $=id=>document.getElementById(id);
let current=null,token='',busy=false;
function notice(text,error=false){$('visitor-status').textContent=text;$('visitor-status').classList.toggle('error',error);}
function ratios(){
    const rows=[...document.querySelectorAll('.visitor-entry')];
    let total=0;
    for(const row of rows) if(row.querySelector('input[type=checkbox]').checked && row.dataset.available==='true') total+=Number(row.querySelector('input[type=number]').value)||0;
    for(const row of rows){const active=row.querySelector('input[type=checkbox]').checked && row.dataset.available==='true';const weight=active?Number(row.querySelector('input[type=number]').value)||0:0;row.querySelector('.visitor-ratio').textContent=total>0?`약 ${Math.round(weight/total*100)}%`:'등장 안 함';}
    $('visitor-empty-note').hidden=total>0;
    $('visitor-speed-value').textContent=`${Number($('visitor-speed').value).toFixed(1)}배`;
}
function render(data){
    current=data;
    $('visitor-enabled').checked=data.settings.enabled;
    $('visitor-count').value=data.settings.count;$('visitor-speed').value=data.settings.speed;
    $('visitor-appearances').replaceChildren();
    for(const entry of data.catalog){
        const override=data.settings.appearances.find(item=>item.id===entry.id);
        const row=document.createElement('div');row.className='visitor-entry';row.dataset.id=entry.id;row.dataset.available=String(entry.available);
        const label=document.createElement('label'),enabled=document.createElement('input');enabled.type='checkbox';enabled.checked=entry.available && (override?.enabled??true);enabled.disabled=!entry.available;
        const title=document.createElement('strong');title.textContent=entry.displayName;
        label.append(enabled,title);
        const details=document.createElement('p');details.className='hint';details.textContent=entry.available?`${entry.creator} · ${entry.license}`:'현재 실험 앱 빌드에서 사용할 수 없는 외형입니다.';
        const weightLabel=document.createElement('label');weightLabel.textContent='등장 비중';
        const weight=document.createElement('input');weight.type='number';weight.min='0';weight.max='100';weight.step='0.1';weight.value=override?.weight??entry.weight;weight.disabled=!entry.available;weight.setAttribute('aria-label',entry.displayName+' 등장 비중');weightLabel.append(weight);
        const ratio=document.createElement('span');ratio.className='visitor-ratio';
        row.append(label,details,weightLabel,ratio);row.addEventListener('input',ratios);$('visitor-appearances').append(row);
    }
    const ignored=data.settings.appearances.filter(item=>!data.catalog.some(entry=>entry.id===item.id)).length;
    $('visitor-removed-note').hidden=ignored===0;
    $('visitor-controls').disabled=false;ratios();
}
async function reload(){
    if(busy)return;busy=true;$('visitor-controls').disabled=true;
    try{
        const [sessionResponse,response]=await Promise.all([fetch('/api/session'),fetch('/api/visitors')]);
        const data=await response.json();if(!response.ok)throw Error(data.error);
        token=(await sessionResponse.json()).token;render(data);notice('저장된 관람객 설정을 불러왔습니다.');
    }catch(error){notice(error.message||'관람객 설정에 연결하지 못했습니다.',true);}
    finally{busy=false;}
}
$('visitor-form').addEventListener('submit',async event=>{
    event.preventDefault();if(busy || !current || !token)return;
    const settings={schemaVersion:'1.0',enabled:$('visitor-enabled').checked,count:Number($('visitor-count').value),speed:Number($('visitor-speed').value),appearances:[...document.querySelectorAll('.visitor-entry')].map(row=>({id:row.dataset.id,enabled:row.querySelector('input[type=checkbox]').checked,weight:Number(row.querySelector('input[type=number]').value)}))};
    busy=true;$('visitor-controls').disabled=true;notice('관람객 설정을 저장하고 있습니다.');
    try{
        const response=await fetch('/api/visitors',{method:'POST',headers:{'Content-Type':'application/json','X-Museum-Token':token},body:JSON.stringify({revision:current.revision,settings})});
        const data=await response.json();if(!response.ok)throw Error(data.error);
        render(data);notice('저장했습니다. 실행 중인 최신 관람 앱에 약 1초 안에 반영되며, 다시 실행해도 유지됩니다.');
    }catch(error){notice(error.message||'저장하지 못했습니다. 입력값은 유지됩니다.',true);}
    finally{busy=false;$('visitor-controls').disabled=false;}
});
$('visitor-reload').addEventListener('click',reload);
$('visitor-speed').addEventListener('input',ratios);
reload();
