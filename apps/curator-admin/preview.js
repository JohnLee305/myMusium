import * as THREE from './vendor/three.module.js';
import {GLTFLoader} from './vendor/GLTFLoader.js';
import {OrbitControls} from './vendor/OrbitControls.js';
import {inspectGlb,sizeCheck,LIMITS} from './preview-checks.mjs';
const $=id=>document.getElementById(id);
let renderer,scene,camera,controls,model,box,size,ticket=0,loaded=false,loading=false,bytes=0,triangles=0,readOnly=false,savedScale=1;
function note(text,error=false){$('preview-status').textContent=text;$('preview-status').classList.toggle('error',error);}
function dispose(root){if(!root)return;const textures=new Set(),materials=new Set(),geometries=new Set();root.traverse(o=>{if(o.geometry)geometries.add(o.geometry);for(const m of (Array.isArray(o.material)?o.material:[o.material]).filter(Boolean)){materials.add(m);for(const v of Object.values(m))if(v?.isTexture)textures.add(v);}});textures.forEach(t=>{t.source?.data?.close?.();t.dispose();});materials.forEach(m=>m.dispose());geometries.forEach(g=>g.dispose());root.removeFromParent();}
function draw(){if(renderer)renderer.render(scene,camera);}
function setup(){
 if(renderer)return;
 renderer=new THREE.WebGLRenderer({antialias:true,alpha:false});renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.outputColorSpace=THREE.SRGBColorSpace;
 $('preview-canvas').append(renderer.domElement);renderer.domElement.setAttribute('aria-label','3D 모델: 드래그로 회전하고 휠로 확대합니다.');renderer.domElement.tabIndex=0;
 scene=new THREE.Scene();scene.background=new THREE.Color('#e8ece6');camera=new THREE.PerspectiveCamera(40,1,.001,10000);
 scene.add(new THREE.HemisphereLight(0xffffff,0x455346,2.5));const light=new THREE.DirectionalLight(0xffffff,3);light.position.set(3,5,4);scene.add(light);
 controls=new OrbitControls(camera,renderer.domElement);controls.enablePan=false;controls.addEventListener('change',draw);
 box=new THREE.Box3Helper(new THREE.Box3(new THREE.Vector3(-.475,0,-.475),new THREE.Vector3(.475,1.8,.475)),0x8c6d38);box.visible=$('preview-bounds').checked;scene.add(box);
 const resize=()=>{const w=$('preview-canvas').clientWidth;renderer.setSize(Math.max(1,w),300);camera.aspect=w/300;camera.updateProjectionMatrix();draw();};new ResizeObserver(resize).observe($('preview-canvas'));resize();
 renderer.domElement.addEventListener('webglcontextlost',event=>{event.preventDefault();loaded=false;note('3D 표시가 중단됐습니다. 페이지를 새로고침해 주세요.',true);});
}
function resetCamera(){if(!model)return;const b=new THREE.Box3().setFromObject(model,true);if($('preview-bounds').checked)b.union(box.box);const center=b.getCenter(new THREE.Vector3()),radius=Math.max(b.getSize(new THREE.Vector3()).length()/2,.05);const distance=radius/Math.sin(THREE.MathUtils.degToRad(camera.fov/2))/Math.min(1,camera.aspect)*1.15;controls.target.copy(center);camera.position.copy(center).add(new THREE.Vector3(1,.65,1.7).normalize().multiplyScalar(distance));camera.near=Math.max(.00001,distance/10000);camera.far=Math.max(100,distance*10);camera.updateProjectionMatrix();controls.minDistance=distance*.05;controls.maxDistance=distance*5;controls.update();draw();}
function updateScale(){
 if(!loaded||!model)return;
 const scale=readOnly?savedScale:Number($('scale').value),check=sizeCheck(size,scale);
 $('preview-fit').disabled=readOnly||check.suggested===null;
 if(!check.valid){note('전시 크기는 0.01~10 사이로 입력해 주세요.',true);return;}
 model.scale.setScalar(scale);model.position.set(0,0,0);model.updateMatrixWorld(true);const b=new THREE.Box3().setFromObject(model,true),center=b.getCenter(new THREE.Vector3());model.position.set(-center.x,-b.min.y,-center.z);model.updateMatrixWorld(true);
 const [w,h,d]=check.dimensions.map(n=>(n*100).toFixed(1));$('preview-measure').textContent=`가로 ${w} × 깊이 ${d} × 높이 ${h}cm · ${(bytes/1048576).toFixed(2)}MB · 약 ${triangles.toLocaleString()}개 삼각형`;
 note((readOnly?'등록된 모델 · ':'')+(check.fits?'전시대 크기 범위 안에 들어갑니다.':`전시대 범위를 넘습니다. ${check.suggested===null?'최소 배율보다 더 줄여야 하므로 모델 편집이 필요합니다.':`권장 배율: ${check.suggested} 이하`}`)+(triangles>100000?' 면 수가 많아 실제 관람 앱에서 속도를 확인해 주세요.':''),!check.fits);resetCamera();
}
async function load(source,label,scale=null){
 const current=++ticket;loaded=false;loading=true;readOnly=scale!==null;savedScale=scale??1;dispose(model);model=null;size=null;$('preview-fit').disabled=true;$('preview-measure').textContent='';$('preview-title').textContent=label;note('모델을 불러오는 중입니다…');
 try{
  const buffer=await source();if(current!==ticket)return;inspectGlb(buffer);bytes=buffer.byteLength;setup();draw();
  const manager=new THREE.LoadingManager();manager.setURLModifier(url=>{if(!url.startsWith('blob:')&&!url.startsWith('data:'))throw Error('외부 리소스를 읽을 수 없습니다.');return url;});
  const gltf=await new GLTFLoader(manager).parseAsync(buffer,'');if(current!==ticket){dispose(gltf.scene);return;}
  model=new THREE.Group();model.add(gltf.scene);model.updateMatrixWorld(true);const bounds=new THREE.Box3().setFromObject(model,true);const v=bounds.getSize(new THREE.Vector3());size=[v.x,v.y,v.z];if(bounds.isEmpty()||size.some(n=>!Number.isFinite(n))||Math.max(...size)<=0)throw Error('모델의 실제 크기를 계산할 수 없습니다.');
  triangles=0;model.traverse(o=>{if(o.isMesh)triangles+=(o.geometry.index?.count??o.geometry.attributes.position?.count??0)/3;});triangles=Math.round(triangles);scene.add(model);loaded=true;updateScale();
 }catch(error){if(current!==ticket)return;dispose(model);model=null;draw();note(error.message?.startsWith('THREE')?'이 모델은 미리보기에서 읽을 수 없습니다. 일반 GLB로 다시 내보내 주세요.':error.message||'모델을 읽지 못했습니다.',true);}
 finally{if(current===ticket)loading=false;}
}
async function fetchModel(id){const r=await fetch(`/api/model/${encodeURIComponent(id)}`);if(!r.ok)throw Error((await r.json()).error);return r.arrayBuffer();}
function sourceChanged(){
 const sample=document.querySelector('input[name=source]:checked').value==='sample';
 if(sample){const id=$('sample').value;if(id)load(()=>fetchModel(id),'등록할 샘플');return;}
 const file=$('model').files[0];if(!file){++ticket;dispose(model);model=null;loaded=false;loading=false;readOnly=false;draw();$('preview-title').textContent='등록 전 3D 미리보기';$('preview-measure').textContent='';$('preview-fit').disabled=true;note('GLB 파일을 선택하면 이곳에 표시됩니다.');return;}
 load(async()=>{if(!/\.glb$/i.test(file.name))throw Error('.glb 파일을 선택해 주세요.');if(file.size>LIMITS.bytes)throw Error('파일 용량이 64MB를 넘습니다.');return file.arrayBuffer();},file.name);
}
export function initPreview(){
 document.querySelectorAll('input[name=source]').forEach(el=>el.addEventListener('change',sourceChanged));$('sample').addEventListener('change',sourceChanged);$('model').addEventListener('change',sourceChanged);$('scale').addEventListener('input',updateScale);
 $('preview-reset').onclick=resetCamera;$('preview-bounds').onchange=()=>{if(box){box.visible=$('preview-bounds').checked;resetCamera();}};
 $('preview-fit').onclick=()=>{if(!size||readOnly)return;const n=sizeCheck(size,1).suggested;if(n!==null){$('scale').value=n;$('scale').dispatchEvent(new Event('input',{bubbles:true}));}};
 $('preview-current').onclick=sourceChanged;
 document.addEventListener('preview-artifact',e=>{load(()=>fetchModel(e.detail.id),e.detail.title,e.detail.scale);$('preview-title').scrollIntoView({behavior:'smooth'});});
 $('add-form').addEventListener('submit',event=>{if(!readOnly&&(!loaded||loading||!sizeCheck(size,Number($('scale').value)).fits)){event.preventDefault();event.stopImmediatePropagation();note('등록 전에 미리보기를 확인하고 전시대 범위에 맞게 크기를 조정해 주세요.',true);$('preview-title').scrollIntoView();}else if(readOnly){event.preventDefault();event.stopImmediatePropagation();sourceChanged();note('등록할 모델을 다시 불러옵니다. 크기를 확인한 뒤 등록해 주세요.');}},true);
 $('add-form').addEventListener('reset',()=>setTimeout(sourceChanged,0));sourceChanged();
}
