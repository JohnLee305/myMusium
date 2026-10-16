import assert from 'node:assert/strict';

// Minimal DOM stub: no jsdom. Counts element creation / appends so a render
// loop (like the reverted ROUTE-003 OOM: 200k appends) fails fast instead of OOM.
let created = 0;
let appended = 0;

class ClassList {
  constructor() { this.set = new Set(); }
  toggle(name, force) {
    if (force === undefined) { this.set.has(name) ? this.set.delete(name) : this.set.add(name); }
    else if (force) this.set.add(name);
    else this.set.delete(name);
  }
  contains(name) { return this.set.has(name); }
}

class StubElement {
  constructor(tag = 'div') {
    this.tagName = String(tag).toUpperCase();
    this.children = [];
    this.classList = new ClassList();
    this.dataset = {};
    this.style = {};
    this.value = '';
    this.textContent = '';
    this.disabled = false;
    this.hidden = false;
    this.type = '';
    this.className = '';
    this.onclick = null;
    this.onchange = null;
    this.options = [];
  }
  setAttribute() {}
  getAttribute() { return null; }
  addEventListener() {}
  replaceChildren(...nodes) { this.children = []; if (nodes.length) this.append(...nodes); }
  append(...nodes) {
    for (const n of nodes.flat()) {
      appended++;
      if (appended > 5000) throw Error(`DOM append overflow: ${appended} appends in one test (possible render loop)`);
      this.children.push(n);
      if (this.tagName === 'SELECT' && n?.value !== undefined) this.options.push(n);
    }
  }
}

const byId = new Map();
const getOrCreate = (id) => {
  if (!byId.has(id)) byId.set(id, new StubElement(id.startsWith('nav-') ? 'div' : 'div'));
  return byId.get(id);
};

globalThis.document = {
  getElementById: (id) => getOrCreate(id),
  createElement: (tag) => {
    created++;
    if (created > 5000) throw Error(`DOM create overflow: ${created} elements in one test (possible render loop)`);
    return new StubElement(tag);
  },
  querySelectorAll: () => [],
  addEventListener() {},
  dispatchEvent() {},
};
globalThis.window = { addEventListener() {} };
globalThis.Option = class {
  constructor(text = '', value = '') { this.textContent = text; this.text = text; this.value = value; this.disabled = false; }
};

const editor = await import('../apps/curator-admin/navigation-editor.js');

const artifacts = ['a', 'b', 'c'].map((id) => `artifacts/${id}/artifact.json`);
const data = {
  token: 'test-token',
  revision: 'rev-1',
  rooms: [{ id: 'room-a', title: '시험 방', artifacts, cells: [0, 1, 2], navigation: undefined }],
  roomOrder: ['room-a'],
  artifacts: ['a', 'b', 'c'].map((id) => ({ id, title: `전시품 ${id}` })),
};
data.rooms[0].navigation = {
  entrance: { side: 'front', slot: 2 },
  exit: { side: 'right', slot: 0 },
  blockedCells: [],
  visitOrder: [...artifacts],
};

let passed = 0;
const pass = (s) => { passed++; console.log('PASS: ' + s); };

created = 0; appended = 0;
editor.initNavigationEditor(async () => {});
pass(`init ok (created=${created} appended=${appended})`);

created = 0; appended = 0;
const firstSyncData = structuredClone(data);
getOrCreate('nav-room').value = 'room-a';
editor.syncNavigationEditor(firstSyncData);
const firstCreated = created, firstAppended = appended;
assert.ok(firstCreated > 0 && firstCreated < 1000, `single render creates bounded elements (got ${firstCreated})`);
assert.ok(firstAppended > 0 && firstAppended < 2000, `single render appends bounded nodes (got ${firstAppended})`);
pass(`single syncNavigationEditor render bounded (created=${firstCreated} appended=${firstAppended})`);

// Second sync simulates a refresh: must not accumulate / loop.
created = 0; appended = 0;
editor.syncNavigationEditor(structuredClone(data));
assert.ok(created < 1000 && appended < 2000, `second render still bounded (created=${created} appended=${appended})`);
pass(`repeat syncNavigationEditor render bounded (created=${created} appended=${appended})`);

// Wall-slot UI (ROUTE-003 retry): if present, clicking a slot must change the
// door without exploding the DOM. Absent on the reverted baseline: skip.
const wallTop = byId.get('nav-wall-top');
const wallBottom = byId.get('nav-wall-bottom');
const wallLeft = byId.get('nav-wall-left');
const wallRight = byId.get('nav-wall-right');
if (wallTop && wallBottom && wallLeft && wallRight && wallTop.children.length > 0) {
  assert.equal(wallTop.children.length, 6, 'top wall has 6 slots');
  assert.equal(wallBottom.children.length, 6, 'bottom wall has 6 slots');
  assert.equal(wallLeft.children.length, 5, 'left wall has 5 slots');
  assert.equal(wallRight.children.length, 5, 'right wall has 5 slots');
  pass('wall slots rendered with fixed counts 6/6/5/5');
  const before = appended;
  created = 0; appended = 0;
  const target = wallRight.children[1] ?? wallRight.children[0];
  assert.ok(typeof target?.onclick === 'function', 'wall slot is clickable');
  target.onclick();
  assert.ok(created < 1000 && appended < 2000, `wall slot click re-render bounded (created=${created} appended=${appended})`);
  pass(`wall slot click re-renders bounded (had ${before} appends before click)`);
} else {
  pass('wall slots absent (baseline) — skipped');
}

console.log(`NAVIGATION_EDITOR_DOM_PASS: ${passed} cases | created=${firstCreated} appended=${firstAppended}`);
