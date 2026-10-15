export const LIMITS={bytes:64*1024*1024,width:.95,height:1.8,depth:.95,minScale:.01,maxScale:10};
export function inspectGlb(buffer){
 if(buffer.byteLength<20||buffer.byteLength>LIMITS.bytes)throw Error('GLB 파일은 64MB 이하여야 합니다.');
 const view=new DataView(buffer);
 if(view.getUint32(0,true)!==0x46546c67||view.getUint32(4,true)!==2||view.getUint32(8,true)!==buffer.byteLength)throw Error('유효한 GLB 2.0 파일이 아닙니다.');
 const n=view.getUint32(12,true);
 if(n%4||n>buffer.byteLength-20||view.getUint32(16,true)!==0x4e4f534a)throw Error('GLB 내부 데이터가 손상되었습니다.');
 let data;try{data=JSON.parse(new TextDecoder().decode(new Uint8Array(buffer,20,n)));}catch{throw Error('GLB 내부 설명을 읽을 수 없습니다.');}
 if(data.asset?.version!=='2.0'||!Array.isArray(data.meshes)||!data.meshes.length)throw Error('표시할 모델이 있는 GLB 2.0 파일을 선택해 주세요.');
 for(const key of ['buffers','images']){
  if(data[key]!==undefined&&!Array.isArray(data[key]))throw Error('모델 리소스 형식이 잘못되었습니다.');
  for(const resource of data[key]??[])if(!resource||(resource.uri!==undefined&&(typeof resource.uri!=='string'||!resource.uri.startsWith('data:'))))throw Error('외부 파일을 참조합니다. 텍스처와 데이터를 포함한 GLB로 내보내 주세요.');
 }
 const compressed=['KHR_draco_mesh_compression','EXT_meshopt_compression','KHR_texture_basisu'].filter(x=>(data.extensionsUsed??[]).includes(x));
 if(compressed.length)throw Error('이 미리보기에서 지원하지 않는 압축 모델입니다. 압축을 해제한 GLB로 내보내 주세요.');
 return data;
}
export function sizeCheck(size,scale){
 const valid=Number.isFinite(scale)&&scale>=LIMITS.minScale&&scale<=LIMITS.maxScale;
 const dimensions=size.map(n=>n*scale);
 const fits=valid&&dimensions.every((n,i)=>Number.isFinite(n)&&n<=[LIMITS.width,LIMITS.height,LIMITS.depth][i]);
 const max=Math.min(...size.map((n,i)=>n>0?[LIMITS.width,LIMITS.height,LIMITS.depth][i]/n:Infinity),10);
 // Leave a small margin for renderer-specific bounds and round down to the UI step.
 const suggested=Math.floor(max*.99*100)/100;
 return {valid,fits,dimensions,suggested:suggested>=.01?suggested:null};
}
