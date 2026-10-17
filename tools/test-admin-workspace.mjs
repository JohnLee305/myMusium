import fs from 'node:fs';
import assert from 'node:assert/strict';
const html=fs.readFileSync('apps/curator-admin/index.html','utf8');
const ids=new Map(),all=[];
class Element {
 constructor(tag,attrs={}){this.tagName=tag.toUpperCase();this.attrs={...attrs};this.children=[];this.parent=null;this.listeners={};this._value='';this._text='';this.hidden='hidden'in attrs;this.disabled='disabled'in attrs;this.dataset={};this.style={setProperty:(k,v)=>{this.style[k]=v;}};for(const [k,v]of Object.entries(attrs))if(k.startsWith('data-'))this.dataset[k.slice(5).replace(/-([a-z])/g,(_,c)=>c.toUpperCase())]=v;this.classList={toggle:()=>{}};if(attrs.id){assert.ok(!ids.has(attrs.id),'duplicate DOM ID '+attrs.id);ids.set(attrs.id,this);}all.push(this);}
 get id(){return this.attrs.id??'';}
 set id(v){if(ids.get(this.attrs.id)===this)ids.delete(this.attrs.id);this.attrs.id=String(v);assert.ok(!ids.has(this.attrs.id),'duplicate dynamic DOM ID '+v);ids.set(this.attrs.id,this);}
 get value(){return this.tagName==='SELECT'?(this.children.some(c=>c.value===this._value)?this._value:this.children[0]?.value??''):this._value;}
 set value(v){this._value=String(v);}
 get options(){return this.children;}
 set textContent(v){this._text=String(v);this.children=[];}
 get textContent(){return this._text+this.children.map(c=>c.textContent).join('');}
 append(...nodes){for(const n of nodes){n.parent=this;this.children.push(n);}}
 replaceChildren(...nodes){this.children=[];this._value='';this.append(...nodes);}
 setAttribute(k,v){this.attrs[k]=String(v);}getAttribute(k){return this.attrs[k]??null;}removeAttribute(k){delete this.attrs[k];}
 addEventListener(k,f){(this.listeners[k]??=[]).push(f);}
 querySelectorAll(selector){return all.filter(e=>{let p=e.parent;while(p&&p!==this)p=p.parent;return p===this&&matches(e,selector);});}
 focus(){}scrollIntoView(){}
 async trigger(type,extra={}){const e={target:this,preventDefault(){},...extra};if(this['on'+type])await this['on'+type](e);for(const f of this.listeners[type]??[])await f(e);}
 click(){return this.trigger('click');}
}
function matches(e,selector){return selector.split(',').some(part=>{part=part.trim();if(part.startsWith('[')){const m=part.match(/^\[([^=\]]+)(?:=["']?([^\]"']+)["']?)?\]$/);return m&&m[1]in e.attrs&&(!m[2]||e.attrs[m[1]]===m[2]);}return e.tagName.toLowerCase()===part;});}
const dom=new Element('document'),stack=[dom],voids=new Set(['meta','link','input','br','hr','img']);
for(const match of html.matchAll(/<!--[\s\S]*?-->|<![^>]+>|<\/?[a-z][^>]*>/gi)){
 const raw=match[0];if(raw.startsWith('<!'))continue;const close=raw.startsWith('</'),tag=raw.match(/^<\/?([a-z0-9-]+)/i)[1].toLowerCase();
 if(close){assert.equal(stack.at(-1).tagName,tag.toUpperCase(),'unbalanced HTML '+raw);stack.pop();continue;}
 const attrs={};for(const a of raw.slice(tag.length+1).matchAll(/([\w-]+)(?:="([^"]*)"|='([^']*)')?/g))attrs[a[1]]=a[2]??a[3]??'';
 if(tag==='form')assert.ok(!stack.some(e=>e.tagName==='FORM'),'nested form');
 const e=new Element(tag,attrs);stack.at(-1).append(e);if(!voids.has(tag)&&!raw.endsWith('/>'))stack.push(e);
}
assert.equal(stack.length,1);const $=id=>{assert.ok(ids.has(id),'missing DOM '+id);return ids.get(id);};
const events={};globalThis.document={getElementById:$,createElement:tag=>new Element(tag),querySelectorAll:s=>all.filter(e=>matches(e,s)),addEventListener:(k,f)=>(events[k]??=[]).push(f),dispatchEvent:e=>{for(const f of events[e.type]??[])f(e);}};
globalThis.window={addEventListener(){}};globalThis.location={hash:'#theme-heading'};globalThis.history={replaceState:(_,__,hash)=>{location.hash=hash;}};globalThis.Option=class extends Element{constructor(text,value){super('option');this._text=text;this.value=value;}};globalThis.confirm=()=>{throw Error('Unexpected discard dialog');};
const layout=await import('../apps/curator-admin/layout-editor.js'),nav=await import('../apps/curator-admin/navigation-editor.js'),theme=await import('../apps/curator-admin/theme-editor.js'),{createWorkspace,routeFor}=await import('../apps/curator-admin/admin-workspace.js');
const refs=['artifacts/a/artifact.json','artifacts/b/artifact.json'];let data={token:'fixture',revision:'r1',roomOrder:['room-a','room-b'],trash:[],rooms:refs.map((entry,i)=>({id:'room-'+(i?'b':'a'),title:'방 '+i,count:1,artifacts:[entry],cells:[0],theme:null,themeRevision:'t'+i,navigation:{entrance:{side:'front',slot:2},exit:{side:'right',slot:0},blockedCells:[],visitOrder:[entry]}})),artifacts:['a','b'].map((id,i)=>({id,title:id,description:'fixture',scale:1,roomId:'room-'+(i?'b':'a'),itemRevision:'i1'}))};
const workspace=createWorkspace({layout:{state:layout.layoutWorkspaceState,select:layout.selectLayoutRoom},route:{state:nav.navigationWorkspaceState,select:nav.selectNavigationRoom},theme:{state:theme.themeWorkspaceState,select:theme.selectThemeRoom}});
async function refresh(){layout.syncLayoutEditor(structuredClone(data));nav.syncNavigationEditor(structuredClone(data));theme.syncThemeEditor(structuredClone(data));workspace.sync(structuredClone(data));await Promise.resolve();}
workspace.init();layout.initLayoutEditor(refresh);nav.initNavigationEditor(refresh);theme.initThemeEditor(refresh);await refresh();
let count=0;const pass=s=>{count++;console.log('PASS '+s);};
assert.equal(document.querySelectorAll('[data-admin-view]').length,4);assert.equal(document.querySelectorAll('[data-room-panel]').length,3);pass('real HTML balanced, unique IDs, four menus and three room workspaces');
assert.deepEqual(routeFor('#layout-heading'),{page:'rooms',tab:'layout'});assert.deepEqual(routeFor('#reactions-heading'),{page:'artifacts',tab:'reactions'});assert.equal(workspace.state().roomTab,'theme');pass('existing theme/layout/reaction bookmarks stay reachable');
for(const hash of ['#home','#artifacts','#visitors','#rooms-layout']){workspace.show(hash);assert.equal(document.querySelectorAll('[data-admin-view]').filter(e=>!e.hidden).length,1);}pass('one menu visible at a time');
const sameRoom=id=>{for(const get of [layout.layoutWorkspaceState,nav.navigationWorkspaceState,theme.themeWorkspaceState])assert.equal(get().roomId,id);assert.equal($('workspace-room').value,id);};
$('workspace-room').value='room-b';await $('workspace-room').trigger('change');sameRoom('room-b');pass('one room selection synchronizes layout/navigation/theme');
workspace.show('#rooms-layout');await $('room-grid').children.find(e=>e.dataset.cell===0).click();await $('room-grid').children.find(e=>e.dataset.cell===1).click();await Promise.resolve();assert.equal(layout.layoutWorkspaceState().dirty,true);assert.equal($('workspace-room').disabled,true);
workspace.show('#home');workspace.show('#rooms-theme');assert.equal(workspace.state().roomTab,'layout');assert.equal(layout.layoutWorkspaceState().dirty,true);assert.equal(workspace.openRoom('room-a'),false);sameRoom('room-b');pass('layout edits survive menu changes and block cross-room/cross-mode loss');
await $('layout-cancel').click();await Promise.resolve();assert.equal(layout.layoutWorkspaceState().dirty,false);assert.equal($('workspace-room').disabled,false);pass('layout cancel resets only its own draft and unlocks room');
workspace.show('#rooms-theme');$('theme-preset').value='viva';await $('theme-preset').trigger('change');const color=$('color-backWall').value;workspace.show('#visitors');await refresh();workspace.show('#rooms-layout');assert.equal(workspace.state().roomTab,'theme');assert.equal($('color-backWall').value,color);assert.equal(theme.themeWorkspaceState().dirty,true);pass('theme draft survives hidden menu and shared data refresh');
let failSave=true,savedMeta;globalThis.fetch=async(url,options)=>{
 if(failSave)return {ok:false,json:async()=>({error:'fixture save conflict'})};
 if(url==='/api/room-theme'){savedMeta=JSON.parse(decodeURIComponent(options.headers['X-Museum-Metadata']));data.rooms.find(r=>r.id===savedMeta.roomId).theme=structuredClone(savedMeta.theme);}
 if(url==='/api/navigation'){savedMeta=JSON.parse(options.body);data.rooms.find(r=>r.id===savedMeta.roomId).navigation=structuredClone(savedMeta.navigation);}
 return {ok:true,json:async()=>({})};
};
await $('theme-form').trigger('submit');await Promise.resolve();assert.equal(theme.themeWorkspaceState().dirty,true);assert.equal($('workspace-room').disabled,true);assert.match($('theme-status').textContent,/conflict/);pass('failed save preserves theme input and blocks room switch');
failSave=false;await $('theme-form').trigger('submit');await Promise.resolve();assert.equal(savedMeta.roomId,'room-b');assert.equal(savedMeta.theme.backWall,color);assert.equal(theme.themeWorkspaceState().dirty,false);sameRoom('room-b');pass('theme saves through original API for shared selected room');
workspace.show('#rooms-route');await $('nav-grid').children[0].click();await Promise.resolve();assert.equal(nav.navigationWorkspaceState().dirty,true);workspace.show('#artifacts-reactions');workspace.show('#rooms-theme');assert.equal(workspace.state().roomTab,'route');assert.equal(nav.navigationWorkspaceState().dirty,true);pass('navigation draft stays intact when visiting reactions and returning');
await $('nav-save').click();await Promise.resolve();assert.equal(savedMeta.roomId,'room-b');assert.ok(savedMeta.navigation.blockedCells.includes(24));assert.equal(nav.navigationWorkspaceState().dirty,false);assert.equal($('workspace-room').disabled,false);pass('navigation keeps original JSON API and unlocks on successful save');
assert.equal(workspace.openRoom('room-a','layout'),true);sameRoom('room-a');pass('collection edit shortcut opens correct room and layout');
workspace.show('#rooms-layout');await $('tab-room-layout').trigger('keydown',{key:'ArrowRight'});assert.equal(workspace.state().roomTab,'route');assert.equal($('tab-room-route').getAttribute('aria-selected'),'true');pass('keyboard arrows switch room tabs with selected ARIA state');
for(const hash of ['#artifacts-list','#artifacts-add','#artifacts-reactions']){workspace.show(hash);assert.equal(document.querySelectorAll('[data-artifact-panel]').filter(e=>!e.hidden).length,1);}pass('registration, collection, reaction controls remain separately reachable');
console.log(`ADMIN_WORKSPACE_TESTS_PASS: ${count} cases`);
