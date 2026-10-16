import http from 'node:http';
import { createReactionService } from './reaction-service.mjs';
import { generateReactions } from './reactions.mjs';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { CAPACITY, MAX_ARTIFACTS, normalizeRooms, place, remove } from './rooms.mjs';

import { validateTheme } from './themes.mjs';
import { DEFAULT_VISITORS, validateVisitorSettings, validateVisitorCatalog } from './visitors.mjs';
const revision = () => crypto.createHash('sha256').update(fs.readFileSync(safe('manifest.json'))).digest('hex');
const itemRevision = entry => crypto.createHash('sha256').update(fs.readFileSync(safe(entry))).digest('hex');
const themeRevision = room => crypto.createHash('sha256').update(JSON.stringify(room.theme ?? null)).digest('hex');

const here = path.dirname(fileURLToPath(import.meta.url));
const options = process.argv.slice(2);
const option = (key, fallback) => options.includes(key) ? options[options.indexOf(key) + 1] : fallback;
const root = path.resolve(option('--archive', path.join(here, '../../museum-archive')));
const port = Number(option('--port', '4181'));
const visitorCatalogFile=path.resolve(option('--visitor-catalog',path.join(here,'visitor-catalog.json')));
const token = crypto.randomBytes(32).toString('hex');
const MAX_GLB = 64 * 1024 * 1024;
const samples = ['sample-vase', 'sample-books', 'sample-sculpture'];
const idPattern = /^[a-z0-9][a-z0-9-]{0,63}$/;
let saving = false;
const fail = (message, status = 400) => { throw Object.assign(new Error(message), { status }); };

function safe(relative, exists = true) {
    if (typeof relative !== 'string' || relative.includes('\\') || relative.includes(':') || relative.split('/').some(p => !p || p === '.' || p === '..')) fail('잘못된 파일 경로입니다.');
    let current = root;
    for (const part of ['', ...relative.split('/')]) {
        current = path.join(current, part);
        if (fs.existsSync(current)) {
            if (fs.lstatSync(current).isSymbolicLink()) fail('링크 폴더는 사용할 수 없습니다.');
        } else if (exists) fail('파일을 찾을 수 없습니다.');
    }
    return current;
}
function json(relative) {
    const file = safe(relative);
    if (fs.statSync(file).size > (relative === 'manifest.json' ? 4 : 1) * 1024 * 1024) fail('JSON 파일이 너무 큽니다.');
    return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
}
function manifest() {
    const data = json('manifest.json');
    if (data.schemaVersion !== '1.0' || !Array.isArray(data.artifacts) || data.artifacts.length > MAX_ARTIFACTS ||
        new Set(data.artifacts).size !== data.artifacts.length || data.artifacts.some(p => typeof p !== 'string' || !/^artifacts\/[a-z0-9][a-z0-9-]{0,63}\/artifact\.json$/.test(p))) fail('전시 목록이 손상되었습니다. manifest.json을 확인해 주세요.', 409);
    normalizeRooms(data);
    return data;
}
function validateGlb(bytes) {
    if (bytes.length < 20 || bytes.length > MAX_GLB || bytes.toString('ascii', 0, 4) !== 'glTF' || bytes.readUInt32LE(4) !== 2 || bytes.readUInt32LE(8) !== bytes.length) fail('유효한 GLB 2.0 파일이 아닙니다. 최대 64MB까지 지원합니다.');
    const length = bytes.readUInt32LE(12);
    if (length > bytes.length - 20 || length % 4 !== 0 || bytes.readUInt32LE(16) !== 0x4e4f534a) fail('GLB 내부 데이터가 손상되었습니다.');
    let data;
    try { data = JSON.parse(bytes.toString('utf8', 20, 20 + length)); } catch { fail('GLB 내부 JSON이 손상되었습니다.'); }
    if (data.asset?.version !== '2.0') fail('GLB 2.0만 지원합니다.');
    for (const field of ['buffers', 'images']) {
        if (data[field] !== undefined && !Array.isArray(data[field])) fail('GLB 리소스 목록이 잘못되었습니다.');
        for (const entry of data[field] || []) {
            if (!entry || (entry.uri !== undefined && (typeof entry.uri !== 'string' || !entry.uri.startsWith('data:')))) fail('텍스처와 모델 데이터가 파일 안에 포함된 GLB를 사용해 주세요.');
        }
    }
    if (!Array.isArray(data.meshes) || !data.meshes.length) fail('표시할 3D 모델이 없는 GLB입니다.');
}
async function readBody(req) {
    if (Number(req.headers['content-length']) > MAX_GLB) fail('모델은 64MB 이하여야 합니다.', 413);
    const chunks = []; let total = 0;
    for await (const chunk of req) {
        total += chunk.length;
        if (total > MAX_GLB) fail('모델은 64MB 이하여야 합니다.', 413);
        chunks.push(chunk);
    }
    return Buffer.concat(chunks);
}
function state() {
    const data = manifest();
    const membership = new Map(data.rooms.flatMap(room => room.artifacts.map((entry,i) => [entry, {roomId:room.id,roomTitle:room.title,position:room.cells[i]+1,cell:room.cells[i]}])));
    const artifacts = data.artifacts.map((relative, index) => {
        try {
            const item = json(relative);
            return { id: item.id, title: item.title, description: item.description, scale: item.scale, itemRevision:itemRevision(relative), displayed: true, ...membership.get(relative) };
        } catch { return { id: relative, title: '읽을 수 없는 전시품', description: '해당 JSON 파일을 확인해 주세요.', error: true, displayed: true, ...membership.get(relative) }; }
    });
    const trash=(data.trash??[]).map(t=>{try {return {...t,...json(t.entry)};} catch {return {...t,id:t.entry.split('/')[1],title:'읽을 수 없는 전시품',error:true};}});
    return { token, artifacts, trash, revision:revision(), rooms:data.rooms.map(room=>({id:room.id,title:room.title,count:room.artifacts.length,cells:room.cells,artifacts:room.artifacts,theme:room.theme??null,themeRevision:themeRevision(room)})), capacity: CAPACITY, maxArtifacts: MAX_ARTIFACTS, samples: samples.filter(id => fs.existsSync(path.join(root, `artifacts/${id}/models/pc.glb`))) };
}
function addRoom(data, title) {
    if (data.rooms.length>=1000) fail('전시실은 최대 1,000개까지 지원합니다.',409);
    if (typeof title!=='string' || !title.trim() || title.length>80) fail('전시실 이름을 1~80자로 입력해 주세요.');
    const room={id:`room-${crypto.randomUUID()}`,title:title.trim(),artifacts:[],cells:[]};
    data.rooms.push(room); return room;
}
function visitorCatalog(){
    try {
        if(fs.statSync(visitorCatalogFile).size>128*1024) fail('캐릭터 목록이 너무 큽니다.',409);
        return validateVisitorCatalog(JSON.parse(fs.readFileSync(visitorCatalogFile,'utf8').replace(/^\uFEFF/,'')));
    } catch(error) { fail('실험 앱의 캐릭터 목록을 읽을 수 없습니다. 빌드 후 다시 열어 주세요.',409); }
}
function visitorRevision(){
    const file=safe('visitors.json',false);
    return fs.existsSync(file)?crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'):'missing';
}
function visitorState(){
    const file=safe('visitors.json',false),catalog=visitorCatalog();
    let settings={...DEFAULT_VISITORS,appearances:[]};
    if(fs.existsSync(file)){
        if(fs.statSync(file).size>64*1024) fail('관람객 설정 파일이 너무 큽니다.',409);
        try {settings=validateVisitorSettings(JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'')));}
        catch {fail('관람객 설정 파일이 손상되었습니다. 파일을 확인해 주세요.',409);}
    }
    return {settings,catalog:catalog.appearances,revision:visitorRevision(),edition:'타이쿤 실험',port};
}
function saveVisitors(value){
    const settings=validateVisitorSettings(value.settings,visitorCatalog());
    if(value.revision!==visitorRevision()) fail('다른 화면에서 관람객 설정이 바뀌었습니다. 저장된 설정을 다시 불러와 주세요.',409);
    const file=safe('visitors.json',false),unique=crypto.randomUUID(),pending=safe(`.visitors-${unique}.tmp`,false),backups=safe('.admin-backups',false);
    try{
        fs.mkdirSync(backups,{recursive:true});
        if(fs.existsSync(file)) fs.copyFileSync(file,path.join(backups,`visitors-${unique}.json`),fs.constants.COPYFILE_EXCL);
        fs.writeFileSync(pending,JSON.stringify(settings,null,2)+'\n',{flag:'wx'});
        fs.renameSync(pending,file);
    } finally {fs.rmSync(pending,{force:true});}
}
function destinationRoom(data, id) {
    let room;
    if (!id || id==='auto') room=data.rooms.find(r=>r.artifacts.length<CAPACITY) || addRoom(data,`${data.rooms.length+1}번 전시실`);
    else room=data.rooms.find(r=>r.id===id);
    if (!room) fail('선택한 전시실이 없습니다. 새로고침해 주세요.',409);
    if (room.artifacts.length>=CAPACITY) fail('선택한 방이 30개로 가득 찼습니다. 다른 방이나 자동 배정을 선택해 주세요.',409);
    return room;
}
function saveManifest(data) {
    normalizeRooms(data);
    const file=safe('manifest.json'), unique=crypto.randomUUID(), pending=safe(`.manifest-${unique}.tmp`,false);
    const backups=safe('.admin-backups',false);
    try {
        fs.mkdirSync(backups,{recursive:true});
        fs.copyFileSync(file,path.join(backups,`manifest-${unique}.json`),fs.constants.COPYFILE_EXCL);
        fs.writeFileSync(pending,JSON.stringify(data,null,2)+'\n',{flag:'wx'});
        fs.renameSync(pending,file);
    } finally { fs.rmSync(pending,{force:true}); }
}
function publish(meta, bytes) {
    const id = meta.id || `exhibit-${Date.now().toString(36)}-${crypto.randomBytes(3).toString('hex')}`;
    if (typeof id !== 'string' || !idPattern.test(id)) fail('관리 ID는 영문 소문자·숫자·하이픈으로 1~64자 입력해 주세요.');
    for (const [field, max, label] of [['title', 80, '이름'], ['description', 400, '설명']]) {
        if (typeof meta[field] !== 'string' || !meta[field].trim() || meta[field].length > max) fail(`${label}을 1~${max}자로 입력해 주세요.`);
    }
    if (typeof meta.scale !== 'number' || !Number.isFinite(meta.scale) || meta.scale < .01 || meta.scale > 10) fail('크기는 0.01~10 사이의 숫자로 입력해 주세요.');
    validateGlb(bytes);
    const data = manifest();
    if (data.artifacts.length >= MAX_ARTIFACTS) fail('현재 등록 한도인 10,000개에 도달했습니다.', 409);
    const room = destinationRoom(data, meta.roomId);
    const relative = `artifacts/${id}/artifact.json`;
    const destination = safe(`artifacts/${id}`, false);
    if (data.artifacts.includes(relative) || fs.existsSync(destination)) fail('같은 관리 ID가 이미 있습니다. 다른 ID를 입력해 주세요.', 409);
    const manifestFile = safe('manifest.json');
    const original = fs.readFileSync(manifestFile);
    const unique = crypto.randomUUID();
    const stage = safe(`artifacts/.import-${unique}`, false);
    const pending = safe(`.manifest-${unique}.tmp`, false);
    const backupDir = safe('.admin-backups', false);
    let published = false, reactionDrafts=null, reactionWarning=null;
    try {
        fs.mkdirSync(path.join(stage, 'models'), { recursive: true });
        fs.writeFileSync(path.join(stage, 'models/pc.glb'), bytes, { flag: 'wx' });
        const artifact = { schemaVersion: '1.0', id, title: meta.title.trim(), description: meta.description.trim(), model: `artifacts/${id}/models/pc.glb`, scale: meta.scale };
        fs.writeFileSync(path.join(stage, 'artifact.json'), JSON.stringify(artifact, null, 2) + '\n', { flag: 'wx' });
        // Optional content is staged with the model. Generation problems never invalidate a v1 exhibit.
        try { reactionDrafts=generateReactions(artifact); }
        catch { reactionWarning='설명 형식을 확인한 뒤 감상 반응 후보를 만들어 주세요.'; }
        if(reactionDrafts)fs.writeFileSync(path.join(stage,'reactions.json'),JSON.stringify(reactionDrafts,null,2)+'\n',{flag:'wx'});
        data.artifacts.push(relative);
        place(room,relative);
        normalizeRooms(data);
        fs.writeFileSync(pending, JSON.stringify(data, null, 2) + '\n', { flag: 'wx' });
        fs.mkdirSync(backupDir, { recursive: true });
        fs.writeFileSync(path.join(backupDir, `manifest-${unique}.json`), original, { flag: 'wx' });
        if (!fs.readFileSync(manifestFile).equals(original)) fail('저장 중 목록이 변경됐습니다. 새로고침 후 다시 시도해 주세요.', 409);
        fs.renameSync(stage, destination);
        published = true;
        fs.renameSync(pending, manifestFile);
        console.log(`ARTIFACT_ADDED: ${id} count=${data.artifacts.length}`);
        return { id, displayed: true, roomId:room.id, roomTitle:room.title, reactionCount:reactionDrafts?.reactions.length??0, reactionWarning };
    } catch (error) {
        // These are unique paths created by this request; existing exhibit folders are never removed.
        if (published) fs.rmSync(destination, { recursive: true, force: true });
        throw error;
    } finally {
        fs.rmSync(stage, { recursive: true, force: true });
        fs.rmSync(pending, { force: true });
    }
}
function send(res, status, data, type = 'application/json; charset=utf-8') {
    res.writeHead(status, {
        'Content-Type': type, 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff',
        'Content-Security-Policy': "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; connect-src 'self' data: blob:; object-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'",
    });
    res.end(typeof data === 'object' && !Buffer.isBuffer(data) ? JSON.stringify(data) : data);
}
const reactions=createReactionService({safe,json,manifest,fail});
const assets = { '/preview.js':['preview.js','text/javascript; charset=utf-8'], '/preview-checks.mjs':['preview-checks.mjs','text/javascript; charset=utf-8'], ...Object.fromEntries(['three.module.js','three.core.js','GLTFLoader.js','OrbitControls.js','BufferGeometryUtils.js'].map(file=>['/vendor/'+file,['vendor/'+file,'text/javascript; charset=utf-8']])), '/layout-editor.js':['layout-editor.js','text/javascript; charset=utf-8'], '/themes.mjs':['themes.mjs','text/javascript; charset=utf-8'], '/theme-editor.js':['theme-editor.js','text/javascript; charset=utf-8'], '/': ['index.html', 'text/html; charset=utf-8'], '/style.css': ['style.css', 'text/css; charset=utf-8'], '/app.js': ['app.js', 'text/javascript; charset=utf-8'] };
assets['/visitor-editor.js']=['visitor-editor.js','text/javascript; charset=utf-8'];
assets['/visitor-style.css']=['visitor-style.css','text/css; charset=utf-8'];
assets['/reaction-editor.js']=['reaction-editor.js','text/javascript; charset=utf-8'];
assets['/reaction-style.css']=['reaction-style.css','text/css; charset=utf-8'];
const server = http.createServer(async (req, res) => {
    try {
        const host = `127.0.0.1:${server.address().port}`;
        if (req.headers.host !== host || (req.headers.origin && req.headers.origin !== `http://${host}`)) fail('이 PC의 관리 화면에서만 요청할 수 있습니다.', 403);
        const url = new URL(req.url, `http://${host}`);
        if(req.method==='GET' && url.pathname==='/api/reactions')return send(res,200,reactions.get(url.searchParams.get('artifactId')));
        if (req.method === 'GET' && url.pathname === '/api/health') return send(res, 200, { service: 'vr-museum-curator', archive: root, edition:'tycoon' });
        if (req.method === 'GET' && url.pathname === '/api/visitors') return send(res,200,visitorState());
        if (req.method === 'GET' && url.pathname === '/api/session') return send(res, 200, { token });
        if (req.method === 'GET' && url.pathname === '/api/state') return send(res, 200, state());
        if (req.method === 'GET' && url.pathname.startsWith('/api/model/')) {
            const id=url.pathname.slice('/api/model/'.length);
            if(!idPattern.test(id)) fail('잘못된 모델 ID입니다.',400);
            const entry=`artifacts/${id}/artifact.json`;
            if(!manifest().artifacts.includes(entry) && !samples.includes(id)) fail('등록된 모델이 없습니다.',404);
            const item=json(entry);
            if(item.id!==id || typeof item.model!=='string' || !item.model.startsWith('artifacts/'+id+'/models/') || !/^[a-zA-Z0-9_-]+\.glb$/.test(item.model.split('/').at(-1))) fail('모델 경로가 잘못되었습니다.',409);
            const file=safe(item.model);if(fs.statSync(file).size>MAX_GLB)fail('모델은 64MB 이하여야 합니다.',413);
            return send(res,200,fs.readFileSync(file),'model/gltf-binary');
        }
        if (req.method === 'GET' && assets[url.pathname]) {
            const [file, type] = assets[url.pathname];
            return send(res, 200, fs.readFileSync(path.join(here, file)), type);
        }
        if (req.method !== 'POST' || !['/api/reactions','/api/visitors','/api/artifacts','/api/rooms','/api/move','/api/room-theme','/api/shutdown','/api/layout','/api/edit','/api/trash','/api/restore'].includes(url.pathname)) return send(res, 404, { error: '페이지를 찾을 수 없습니다.' });
        if (req.headers['x-museum-token'] !== token) fail('관리 화면을 새로고침한 후 다시 등록해 주세요.', 403);
        if(url.pathname==='/api/reactions'){
            if(req.headers['content-type']!=='application/json')fail('반응은 JSON 형식으로 저장해 주세요.',415);
            if(Number(req.headers['content-length'])>65536)fail('반응 요청은 64KiB 이하여야 합니다.',413);
            if(saving)fail('저장 중입니다. 잠시 후 다시 시도해 주세요.',409);
            saving=true;
            try{
                const chunks=[];let size=0;
                for await(const chunk of req){size+=chunk.length;if(size>65536)fail('반응 요청이 너무 큽니다.',413);chunks.push(chunk);}
                let value;try{value=JSON.parse(Buffer.concat(chunks).toString('utf8'));}catch{fail('반응 요청을 읽을 수 없습니다.');}
                if(!value || typeof value!=='object' || Array.isArray(value))fail('반응 요청 형식이 잘못되었습니다.');
                return send(res,200,reactions.post(value));
            }finally{saving=false;}
        }
        if(url.pathname==='/api/visitors'){
            if(req.headers['content-type']!=='application/json') fail('잘못된 설정 형식입니다.',415);
            if(Number(req.headers['content-length'])>64*1024) fail('관람객 설정이 너무 큽니다.',413);
            if(saving) fail('저장 중입니다. 잠시 후 다시 시도해 주세요.',409);
            saving=true;
            try{
                const chunks=[];let size=0;
                for await(const chunk of req){size+=chunk.length;if(size>64*1024) fail('관람객 설정이 너무 큽니다.',413);chunks.push(chunk);}
                let value;try{value=JSON.parse(Buffer.concat(chunks).toString('utf8'));}catch{fail('관람객 설정을 읽을 수 없습니다.');}
                if(!value || typeof value!=='object' || Array.isArray(value)) fail('관람객 설정 형식이 잘못되었습니다.');
                saveVisitors(value); return send(res,200,visitorState());
            }finally{saving=false;}
        }
        if (req.headers['content-type'] !== 'application/octet-stream') fail('잘못된 업로드 형식입니다.', 415);
        if (saving) fail('다른 전시품을 저장 중입니다. 잠시 후 다시 시도해 주세요.', 409);
        saving = true;
        try {
            let meta;
            try { meta = JSON.parse(decodeURIComponent(req.headers['x-museum-metadata'] || '')); } catch { fail('입력 정보를 읽을 수 없습니다.'); }
            if (!meta || typeof meta !== 'object' || Array.isArray(meta)) fail('입력 형식이 잘못되었습니다.');
            let bytes = await readBody(req);
            if (url.pathname === '/api/shutdown') {
                send(res, 200, { stopped: true });
                server.close(() => console.log('CURATOR_STOPPED'));
                server.closeIdleConnections();
                return;
            }
            if (url.pathname !== '/api/artifacts') {
                const data=manifest();
                if (['/api/layout','/api/edit','/api/trash','/api/restore'].includes(url.pathname) && meta.revision!==revision()) fail('다른 화면에서 목록이 변경됐습니다. 새로고침 후 다시 시도해 주세요.',409);
                if (url.pathname==='/api/layout') {
                    const room=data.rooms.find(r=>r.id===meta.roomId);
                    if (!room) fail('전시실이 없습니다.',404);
                    if (!Array.isArray(meta.entries) || meta.entries.length!==room.artifacts.length || new Set(meta.entries.map(e=>e?.entry)).size!==room.artifacts.length || meta.entries.some(e=>!e || !room.artifacts.includes(e.entry))) fail('방의 전시품 목록이 일치하지 않습니다.',409);
                    room.artifacts=meta.entries.map(e=>e.entry); room.cells=meta.entries.map(e=>e.cell);
                    saveManifest(data); return send(res,200,{saved:true});
                }
                if (url.pathname==='/api/restore') {
                    const entry=`artifacts/${meta.artifactId}/artifact.json`, record=data.trash?.find(t=>t.entry===entry);
                    if (!record) fail('휴지통에서 전시품을 찾을 수 없습니다.',404);
                    if(data.artifacts.length>=MAX_ARTIFACTS) fail('등록 한도에 도달했습니다.',409);
                    json(entry);
                    const target=destinationRoom(data,meta.roomId||record.roomId);
                    place(target,entry,meta.cell); data.artifacts.push(entry); data.trash=data.trash.filter(t=>t!==record);
                    saveManifest(data); return send(res,200,{restored:true});
                }
                if (url.pathname==='/api/room-theme') {
                    const room=data.rooms.find(r=>r.id===meta.roomId);
                    if (!room) fail('선택한 전시실이 없습니다. 새로고침해 주세요.',404);
                    if (meta.themeRevision!==themeRevision(room)) fail('다른 화면에서 이 방의 테마가 변경됐습니다. 페이지를 새로고침한 뒤 다시 조정해 주세요.',409);
                    if (meta.theme===null) delete room.theme;
                    else room.theme=validateTheme(meta.theme);
                    saveManifest(data); return send(res,200,{roomId:room.id,theme:room.theme??null,themeRevision:themeRevision(room)});
                }
                if (url.pathname==='/api/rooms') {
                    const room=addRoom(data,meta.title); saveManifest(data); return send(res,201,{room});
                }
                if (typeof meta.artifactId!=='string' || !idPattern.test(meta.artifactId)) fail('잘못된 전시품 ID입니다.');
                const entry=`artifacts/${meta.artifactId}/artifact.json`;
                const source=data.rooms.find(r=>r.artifacts.includes(entry));
                if (!source) fail('전시품을 찾을 수 없습니다.',404);
                if (url.pathname==='/api/edit') {
                    const item=json(entry);
                    if(meta.itemRevision!==itemRevision(entry)) fail('전시품 설명이 변경됐습니다. 새로고침해 주세요.',409);
                    for(const [field,max] of [['title',80],['description',400]]) if(typeof meta[field]!=='string'||!meta[field].trim()||meta[field].length>max) fail('이름은 1~80자, 설명은 1~400자로 입력해 주세요.');
                    if(typeof meta.scale!=='number'||!Number.isFinite(meta.scale)||meta.scale<.01||meta.scale>10) fail('크기는 0.01~10 사이로 입력해 주세요.');
                    item.title=meta.title.trim(); item.description=meta.description.trim(); item.scale=meta.scale;
                    const unique=crypto.randomUUID(),file=safe(entry),pending=safe(`artifacts/${meta.artifactId}/.edit-${unique}.tmp`,false),backups=safe('.admin-backups',false);
                    try { fs.mkdirSync(backups,{recursive:true}); fs.copyFileSync(file,path.join(backups,`artifact-${meta.artifactId}-${unique}.json`),fs.constants.COPYFILE_EXCL); fs.writeFileSync(pending,JSON.stringify(item,null,2)+'\n',{flag:'wx'}); fs.renameSync(pending,file); }
                    finally {fs.rmSync(pending,{force:true});}
                    return send(res,200,{saved:true});
                }
                if (url.pathname==='/api/trash') {
                    if((data.trash?.length??0)>=MAX_ARTIFACTS) fail('휴지통 보관 한도에 도달했습니다.',409);
                    const cell=remove(source,entry); data.artifacts=data.artifacts.filter(p=>p!==entry);
                    (data.trash??=[]).push({entry,roomId:source.id,cell,deletedAt:new Date().toISOString()});
                    saveManifest(data); return send(res,200,{removed:true});
                }
                if(meta.revision!==undefined && meta.revision!==revision()) fail('목록이 변경됐습니다. 새로고침해 주세요.',409);
                if (source.id===meta.roomId) return send(res,200,{roomId:source.id});
                const target=destinationRoom(data,meta.roomId);
                place(target,entry,meta.cell); remove(source,entry);
                saveManifest(data); return send(res,200,{roomId:target.id});
            }
            if (meta.sampleId) {
                if (!samples.includes(meta.sampleId)) fail('사용할 수 없는 샘플입니다.');
                const sample = safe(`artifacts/${meta.sampleId}/models/pc.glb`);
                if (fs.statSync(sample).size > MAX_GLB) fail('샘플 파일이 너무 큽니다.');
                bytes = fs.readFileSync(sample);
            }
            return send(res, 201, publish(meta, bytes));
        } finally { saving = false; }
    } catch (error) {
        console.error(`REQUEST_FAILED: ${error.message}`);
        if (!res.headersSent && !res.destroyed) send(res, error.status || 500, { error: error.status ? error.message : '저장소를 읽거나 저장하지 못했습니다. 파일과 실행 로그를 확인해 주세요.' });
        req.resume();
    }
});
server.requestTimeout = 120000;
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
server.listen(port, '127.0.0.1', () => console.log(`CURATOR_READY http://127.0.0.1:${server.address().port} archive=${root}`));

