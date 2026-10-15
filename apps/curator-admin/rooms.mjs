import { validateTheme } from './themes.mjs';
export const CAPACITY = 30;
export const MAX_ARTIFACTS = 10000;
export function normalizeRooms(data) {
    if (data.rooms === undefined) data.rooms = Array.from({length:Math.max(1,Math.ceil(data.artifacts.length/CAPACITY))}, (_,i) => ({id:`room-${i+1}`,title:`${i+1}번 전시실`,artifacts:data.artifacts.slice(i*CAPACITY,(i+1)*CAPACITY)}));
    const error = () => { throw Object.assign(new Error('전시실 배정이 잘못되었습니다. 중복·미배정·30개 초과 여부를 확인해 주세요.'),{status:409}); };
    if (!Array.isArray(data.rooms) || !data.rooms.length || data.rooms.length>1000) error();
    const ids=new Set(), entries=new Set(), all=new Set(data.artifacts);
    for (const room of data.rooms) {
        if (!room || typeof room.id!=='string' || !/^[a-z0-9][a-z0-9-]{0,63}$/.test(room.id) || ids.has(room.id) || typeof room.title!=='string' || !room.title.trim() || room.title.length>80 || !Array.isArray(room.artifacts) || room.artifacts.length>CAPACITY) error();
        if (room.theme !== undefined) validateTheme(room.theme);
        if (room.cells === undefined) room.cells=room.artifacts.map((_,i)=>i);
        if (!Array.isArray(room.cells) || room.cells.length!==room.artifacts.length || new Set(room.cells).size!==room.cells.length || room.cells.some(n=>!Number.isInteger(n)||n<0||n>=CAPACITY)) error();
        ids.add(room.id);
        for (const entry of room.artifacts) { if (!all.has(entry) || entries.has(entry)) error(); entries.add(entry); }
    }
    if (entries.size!==all.size) error();
    if (data.trash !== undefined) {
        if (!Array.isArray(data.trash) || data.trash.length>MAX_ARTIFACTS) error();
        const removed=new Set();
        for (const item of data.trash) {
            if (!item || typeof item.entry!=='string' || !/^artifacts\/[a-z0-9][a-z0-9-]{0,63}\/artifact\.json$/.test(item.entry) || all.has(item.entry) || removed.has(item.entry) || typeof item.roomId!=='string' || !Number.isInteger(item.cell) || item.cell<0 || item.cell>=CAPACITY || typeof item.deletedAt!=='string') error();
            removed.add(item.entry);
        }
    }
    return data.rooms;
}
export function place(room,entry,cell) {
    if (cell===undefined) cell=Array.from({length:CAPACITY},(_,i)=>i).find(n=>!room.cells.includes(n));
    if (!Number.isInteger(cell)||cell<0||cell>=CAPACITY||room.cells.includes(cell)) throw Object.assign(new Error('빈 칸을 선택해 주세요.'),{status:409});
    room.artifacts.push(entry); room.cells.push(cell);
}
export function remove(room,entry) {
    const index=room.artifacts.indexOf(entry);
    if (index<0) throw new Error('Missing room entry');
    room.artifacts.splice(index,1); return room.cells.splice(index,1)[0];
}
