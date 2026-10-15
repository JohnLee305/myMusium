import { initLayoutEditor, syncLayoutEditor, selectLayoutItem } from './layout-editor.js';
import { initThemeEditor, syncThemeEditor } from './theme-editor.js';
import './visitor-editor.js';
const $ = id => document.getElementById(id);
let token = '', saving = false, cached = null;
function message(text, error = false) {
    $('message').hidden = false;
    $('message').classList.toggle('error', error);
    $('message').textContent = text;
}
function sourceChanged() {
    const sample = document.querySelector('input[name=source]:checked').value === 'sample';
    $('sample-area').hidden = !sample;
    $('file-area').hidden = sample;
    $('model').required = !sample;
}
document.querySelectorAll('input[name=source]').forEach(input => input.addEventListener('change', sourceChanged));
$('description').addEventListener('input', () => $('description-count').textContent = $('description').value.length);
$('model').addEventListener('change', () => {
    const file = $('model').files[0];
    $('file-note').textContent = file ? `${file.name} · ${(file.size / 1048576).toFixed(2)} MB` : 'GLB 파일 · 최대 64MB';
});
async function refresh() {
    $('refresh').disabled = true;
    try {
        const response = await fetch('/api/state');
        const data = await response.json();
        if (!response.ok) throw new Error(data.error);
        token = data.token;
        $('total').textContent = data.artifacts.length;
        cached = data;
        syncThemeEditor(data);
        syncLayoutEditor(data);
        $('capacity-text').textContent = `${data.rooms.length}개 전시실 · 총 ${data.artifacts.length}개 전시품 · 방당 ${data.capacity}개`;
        for (const [selectId,firstValue,firstTitle] of [['target-room','auto','자동 배정 · 가득 차면 새 방 생성'],['room-filter','all','전체 전시실']]) {
            const select=$(selectId), previous=select.value;
            select.replaceChildren(new Option(firstTitle,firstValue));
            for (const room of data.rooms) {
                const option=new Option(`${room.title} · ${room.count}/30`,room.id);
                option.disabled=selectId==='target-room' && room.count>=30;
                select.append(option);
            }
            select.value=data.rooms.some(r=>r.id===previous)?previous:firstValue;
        }
        renderArtifacts();
        for (const option of $('sample').options) option.disabled = !data.samples.includes(option.value);
        if (!data.samples.includes($('sample').value)) $('sample').value = data.samples[0] || '';
        $('submit').disabled = saving || data.artifacts.length >= data.maxArtifacts;
    } catch (error) {
        token = ''; $('submit').disabled = true;
        $('capacity-text').textContent = '전시 목록에 연결하지 못했습니다.';
        message(error.message || '관리 도구를 실행한 후 새로고침해 주세요.', true);
        throw error;
    } finally { $('refresh').disabled = false; }
}
function renderArtifacts() {
        const data=cached;
        const room=data.rooms.find(r=>r.id===$('room-filter').value);
        $('room-summary').textContent=room ? `${room.title}: ${room.count}개 · 남은 자리 ${30-room.count}개` : '전시품의 배치·수정을 누르면 격자에서 위치와 설명을 바꿀 수 있습니다.';
        $('artifacts').replaceChildren();
        data.artifacts.forEach((item, index) => {
            if (room && item.roomId!==room.id) return;
            const card = document.createElement('article'); card.className = 'card';
            const icon = document.createElement('div'); icon.className = 'object-icon'; icon.textContent = ['◒','▤','◯'][index % 3]; icon.setAttribute('aria-hidden','true');
            const content = document.createElement('div'); content.className = 'card-body';
            const heading = document.createElement('h3'); heading.textContent = item.title;
            const description = document.createElement('p'); description.textContent = item.description;
            const meta = document.createElement('div'); meta.className = 'card-meta';
            const badge = document.createElement('span'); badge.className = 'badge' + (!item.displayed || item.error ? ' waiting' : '');
            badge.textContent = item.error ? '파일 확인 필요' : `${item.roomTitle} · ${item.position}번`;
            const size = document.createElement('span'); size.textContent = item.scale ? `크기 × ${item.scale}` : '';
            meta.append(badge, size); content.append(heading, description, meta); card.append(icon, content); $('artifacts').append(card);
            if (!item.error) {
                const controls=document.createElement('div'); controls.className='card-move';
                const edit=document.createElement('button');edit.type='button';edit.className='secondary';edit.textContent='배치·수정';edit.onclick=()=>selectLayoutItem(item);
                const preview=document.createElement('button');preview.type='button';preview.className='secondary';preview.textContent='3D 보기';preview.onclick=()=>document.dispatchEvent(new CustomEvent('preview-artifact',{detail:item}));
                controls.append(edit,preview); content.append(controls);
            }
        });
}
$('room-filter').addEventListener('change', renderArtifacts);
async function manage(endpoint,metadata,notice) {
    if (saving || !token) return;
    saving=true; $('submit').disabled=true;
    try {
        const response=await fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/octet-stream','X-Museum-Token':token,'X-Museum-Metadata':encodeURIComponent(JSON.stringify(metadata))},body:new Uint8Array()});
        const result=await response.json(); if (!response.ok) throw new Error(result.error);
        if (endpoint==='/api/rooms') $('room-form').reset();
        try { await refresh(); message(notice); } catch { message('저장은 완료됐습니다. 새로고침으로 결과를 확인해 주세요.',true); }
    } catch(error) { message(error.message,true); }
    finally { saving=false; $('submit').disabled=!token || cached.artifacts.length>=cached.maxArtifacts; }
}
$('room-form').addEventListener('submit',event=>{event.preventDefault(); manage('/api/rooms',{title:$('room-title').value.trim()},'새 전시실을 만들었습니다. 등록할 방으로 선택할 수 있습니다.');});
$('refresh').addEventListener('click', () => refresh().catch(() => {}));
$('add-form').addEventListener('submit', async event => {
    event.preventDefault();
    if (saving || !token) return;
    const sample = document.querySelector('input[name=source]:checked').value === 'sample';
    const file = $('model').files[0];
    if (!sample && (!file || !/\.glb$/i.test(file.name) || file.size > 64 * 1048576)) return message('64MB 이하의 .glb 파일을 선택해 주세요.', true);
    const metadata = { roomId:$('target-room').value, id: $('artifact-id').value.trim(), title: $('title').value.trim(), description: $('description').value.trim(), scale: Number($('scale').value) };
    if (sample) metadata.sampleId = $('sample').value;
    if (sample && !metadata.sampleId) return message('사용할 수 있는 샘플이 없습니다. 내 GLB 파일을 선택해 주세요.', true);
    saving = true; $('submit').disabled = true; $('refresh').disabled = true;
    message('전시품을 저장하고 있습니다. 잠시만 기다려 주세요.');
    try {
        const response = await fetch('/api/artifacts', { method: 'POST', headers: { 'Content-Type': 'application/octet-stream', 'X-Museum-Token': token, 'X-Museum-Metadata': encodeURIComponent(JSON.stringify(metadata)) }, body: sample ? new Uint8Array() : file });
        const result = await response.json();
        if (!response.ok) throw new Error(result.error);
        $('add-form').reset(); $('description-count').textContent = '0'; $('file-note').textContent = 'GLB 파일 · 최대 64MB'; sourceChanged();
        const notice = `${result.roomTitle}에 등록했습니다. 관람 앱을 다시 열고 해당 방으로 이동해 주세요.`;
        try { await refresh(); message(notice); } catch { message('등록은 완료됐지만 목록을 새로 불러오지 못했습니다. 중복 등록하지 말고 새로고침해 주세요.', true); }
    } catch (error) { message(error.message || '연결이 끊겼습니다. 새로고침으로 등록 여부를 확인한 후 다시 시도해 주세요.', true); }
    finally { saving = false; $('submit').disabled = !token || (cached && cached.artifacts.length >= cached.maxArtifacts); $('refresh').disabled = false; }
});
initLayoutEditor(refresh);
initThemeEditor(refresh);
sourceChanged();
refresh().catch(() => {});

import('./preview.js').then(module=>module.initPreview()).catch(()=>{document.getElementById('preview-status').textContent='3D 미리보기를 시작하지 못했습니다. 페이지를 새로고침해 주세요. 기존 등록 기능은 사용할 수 있습니다.';});
