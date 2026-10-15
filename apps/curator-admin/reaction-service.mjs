import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {MAX_REACTION_BYTES,emptyReactions,validateReactions,generateReactions,sourceHash} from './reactions.mjs';

export function createReactionService({safe,json,manifest,fail}){
    const relative=id=>`artifacts/${id}/reactions.json`;
    function artifact(id){
        if(typeof id!=='string'||!/^[a-z0-9][a-z0-9-]{0,63}$/.test(id))fail('잘못된 전시품 ID입니다.');
        if(!manifest().artifacts.includes(`artifacts/${id}/artifact.json`))fail('현재 전시 중인 전시품이 아닙니다.',404);
        const item=json(`artifacts/${id}/artifact.json`);
        if(item.id!==id||typeof item.title!=='string'||typeof item.description!=='string')fail('전시품 원문을 읽을 수 없습니다.',409);
        return item;
    }
    function revision(id){const file=safe(relative(id),false);return fs.existsSync(file)?crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'):'missing';}
    function read(id){const file=safe(relative(id),false);if(!fs.existsSync(file))return null;if(fs.statSync(file).size>MAX_REACTION_BYTES)fail('반응 파일이 너무 큽니다.',409);return validateReactions(JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'')),id);}
    function history(id){const folder=safe('.admin-backups',false);if(!fs.existsSync(folder))return [];return fs.readdirSync(folder).filter(name=>new RegExp(`^reactions-${id}-[0-9]{13}-[a-f0-9-]{36}\\.json$`).test(name)).sort().reverse().slice(0,20);}
    function get(id){
        const item=artifact(id);let document,error=null;
        try{document=read(id)||emptyReactions(item);}catch{document=emptyReactions(item);error='반응 파일이 손상되었습니다. 이전 저장을 복원하거나 후보를 다시 만들어 주세요.';}
        return {artifact:{id:item.id,title:item.title,description:item.description},document,revision:revision(id),sourceHash:sourceHash(item),stale:document.source.sha256!==sourceHash(item),error,history:history(id)};
    }
    function write(id,document){
        const file=safe(relative(id),false),unique=`${Date.now()}-${crypto.randomUUID()}`,pending=safe(`artifacts/${id}/.reactions-${unique}.tmp`,false),backups=safe('.admin-backups',false);
        try{fs.mkdirSync(backups,{recursive:true});if(fs.existsSync(file))fs.copyFileSync(file,path.join(backups,`reactions-${id}-${unique}.json`),fs.constants.COPYFILE_EXCL);fs.writeFileSync(pending,JSON.stringify(document,null,2)+'\n',{flag:'wx'});fs.renameSync(pending,file);}finally{fs.rmSync(pending,{force:true});}
    }
    function post(value){
        const item=artifact(value.artifactId),id=item.id;
        if(value.revision!==revision(id)||value.sourceHash!==sourceHash(item))fail('원문 또는 반응이 다른 화면에서 바뀌었습니다. 다시 불러와 주세요.',409);
        let document;
        if(value.action==='generate'){
            let previous=null;try{previous=read(id);}catch{}
            document=generateReactions(item,previous);
        }else if(value.action==='restore'){
            if(!history(id).includes(value.backup))fail('복원할 저장 기록을 찾을 수 없습니다.',404);
            const file=safe('.admin-backups/'+value.backup);if(fs.statSync(file).size>MAX_REACTION_BYTES)fail('백업 파일이 너무 큽니다.');
            document=validateReactions(JSON.parse(fs.readFileSync(file,'utf8')),id);
            // An old source remains stale after restoration and cannot be displayed until reviewed again.
        }else if(value.action==='save'){
            document=validateReactions(value.document,id);
            if(document.source.sha256!==sourceHash(item))fail('원문이 바뀌었습니다. 새 원문으로 후보를 만든 뒤 검토해 주세요.',409);
        }else fail('지원하지 않는 반응 작업입니다.');
        write(id,document);return get(id);
    }
    return {get,post};
}
