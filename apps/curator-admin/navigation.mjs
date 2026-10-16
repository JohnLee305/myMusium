// Same room geometry and cardinal floor grid as Unity VisitorPaths.
const fail=message=>{throw Object.assign(new Error(message),{status:409});};
const exact=(v,keys)=>v&&typeof v==='object'&&!Array.isArray(v)&&Object.keys(v).length===keys.length&&keys.every(k=>Object.hasOwn(v,k));
export const center=cell=>({x:(cell%6-2.5)*2.4,z:Math.floor(cell/6)*2.8});
export function doorPoint(door){
 if(door.side==='front'||door.side==='back')return {x:(door.slot-2.5)*2.4,z:door.side==='front'?-4.1:13.1};
 return {x:door.side==='left'?-7.3:7.3,z:door.slot*2.8};
}
export function navigationFor(room){return room.navigation??{entrance:{side:'front',slot:2},exit:{side:'right',slot:0},blockedCells:[],visitOrder:[...room.artifacts].sort((a,b)=>room.cells[room.artifacts.indexOf(a)]-room.cells[room.artifacts.indexOf(b)])};}
export function orderFor(data){return data.roomOrder??data.rooms.map(r=>r.id);}
export function validateNavigation(room){
 const nav=room.navigation;if(nav===undefined)return;
 if(!exact(nav,['entrance','exit','blockedCells','visitOrder']))fail('입구·출구·통행 금지·관람 순서를 확인해 주세요.');
 for(const d of [nav.entrance,nav.exit])if(!exact(d,['side','slot'])||!['front','back','left','right'].includes(d.side)||!Number.isInteger(d.slot)||d.slot<0||d.slot>=(['front','back'].includes(d.side)?6:5))fail('문은 벽 가장자리의 유효한 위치를 선택해 주세요.');
 if(nav.entrance.side===nav.exit.side&&nav.entrance.slot===nav.exit.slot)fail('입구와 출구는 다른 위치를 선택해 주세요.');
 if(!Array.isArray(nav.blockedCells)||nav.blockedCells.length>30||new Set(nav.blockedCells).size!==nav.blockedCells.length||nav.blockedCells.some(c=>!Number.isInteger(c)||c<0||c>=30||room.cells.includes(c)))fail('통행 금지 칸에는 전시품을 배치할 수 없습니다.');
 if(!Array.isArray(nav.visitOrder)||nav.visitOrder.length!==room.artifacts.length||new Set(nav.visitOrder).size!==nav.visitOrder.length||nav.visitOrder.some(p=>!room.artifacts.includes(p)))fail('관람 순서에 이 방의 모든 전시품을 한 번씩 지정해 주세요.');
 checkReachable(room);
}
export function validateRoomOrder(data){
 if(data.roomOrder===undefined)return;
 if(!Array.isArray(data.roomOrder)||data.roomOrder.length!==data.rooms.length||new Set(data.roomOrder).size!==data.roomOrder.length||data.roomOrder.some(id=>!data.rooms.some(r=>r.id===id)))fail('방 관람 순서에 모든 방을 한 번씩 지정해 주세요.');
}
export function floorGrid(room){
 const nav=navigationFor(room),w=39,h=45,step=.4;
 const obstacles=[...room.cells.map(c=>{const p=center(c);return {x:p.x,z:p.z+.8,hx:.85,hz:.85};}),...nav.blockedCells.map(c=>{const p=center(c),minX=c%6===0?-8.2:p.x-1.2,maxX=c%6===5?8.2:p.x+1.2,minZ=Math.floor(c/6)===0?-5:p.z-1.4,maxZ=Math.floor(c/6)===4?14:p.z+1.4;return {x:(minX+maxX)/2,z:(minZ+maxZ)/2,hx:(maxX-minX)/2+.3,hz:(maxZ-minZ)/2+.3};})];
 const point=i=>({x:-7.65+(i%w)*step,z:-4.45+Math.floor(i/w)*step});
 const clear=p=>p.x>=-7.65&&p.x<7.65&&p.z>=-4.45&&p.z<13.45&&!obstacles.some(o=>Math.abs(p.x-o.x)<=o.hx&&Math.abs(p.z-o.z)<=o.hz);
 const segment=(a,b)=>{let n=Math.ceil(Math.hypot(a.x-b.x,a.z-b.z)/.1);for(let i=0;i<=n;i++){const t=n?i/n:0;if(!clear({x:a.x+(b.x-a.x)*t,z:a.z+(b.z-a.z)*t}))return false;}return true;};
 const nearest=p=>{let best=-1,d=Infinity;for(let i=0;i<w*h;i++){const q=point(i),v=(q.x-p.x)**2+(q.z-p.z)**2;if(v<d&&clear(q)&&segment(p,q)){best=i;d=v;}}return best;};
 const entrance=doorPoint(nav.entrance),exit=doorPoint(nav.exit),start=nearest(entrance),seen=new Set();
 if(clear(entrance)&&start>=0){const queue=[start];seen.add(start);for(let at=0;at<queue.length;at++){const i=queue[at],x=i%w,z=Math.floor(i/w);for(const [dx,dz] of [[1,0],[-1,0],[0,1],[0,-1]]){const nx=x+dx,nz=z+dz,j=nz*w+nx;if(nx<0||nx>=w||nz<0||nz>=h||seen.has(j)||!clear(point(j))||!segment(point(i),point(j)))continue;seen.add(j);queue.push(j);}}}
 return {reachable:p=>clear(p)&&seen.has(nearest(p)),exit,seen,point,clear};
}
export function checkReachable(room){
 const grid=floorGrid(room);if(!grid.reachable(grid.exit))fail('입구에서 출구까지 연결된 통로가 없습니다. 문 위치와 통행 금지 칸을 확인해 주세요.');
 for(let i=0;i<room.artifacts.length;i++){const p=center(room.cells[i]);p.z+=.8;if(![[0,-1.15],[-1.15,0],[1.15,0],[0,1.15]].some(([x,z])=>grid.reachable({x:p.x+x,z:p.z+z})))fail(`관람 순서 ${navigationFor(room).visitOrder.indexOf(room.artifacts[i])+1}번 전시품에 도달할 수 없습니다.`);}
}
