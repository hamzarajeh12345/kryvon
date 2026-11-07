import * as THREE from 'three';
import { WEAPONS, ITEMS, AMMO, ARMOR, RARITY_COLORS, HALF, ATTACHMENTS } from './config.js';
import { itemRarity } from './actor.js';
import { buildGun, prep, M4, BODY_MAT } from './character.js';
import { propGeometry, propReady } from './props.js';
// generated weapon models: key + real length in metres
const GUN_MODEL = { AR: ['w_ar', 0.95], SMG: ['w_smg', 0.65], SG: ['w_shotgun', 1.0], PISTOL: ['w_pistol', 0.24], SNIPER: ['w_sniper', 1.25], DMR: ['w_sniper', 1.1], LMG: ['w_lmg', 1.1] };
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

const pickW = (pairs) => { // [[value, weight], ...]
  let s = 0; for (const p of pairs) s += p[1];
  let r = Math.random() * s;
  for (const p of pairs) { r -= p[1]; if (r <= 0) return p[0]; }
  return pairs[pairs.length - 1][0];
};

const GROUND_WEAPONS = Object.keys(WEAPONS).filter(k => !WEAPONS[k].exclusive);

export function rollItems(tier, type) {
  const m = { weapon: 1, ammo: 1, heal: 1, boost: 1, armor: 1, pack: 1, throw: 1, scope: 1 };
  if (type === 'hospital') { m.heal = 3.2; m.boost = 3; }
  if (type === 'police') { m.weapon = 2; m.ammo = 1.8; m.armor = 2; }
  if (type === 'shop') { m.pack = 4; m.boost = 1.5; }
  if (type === 'school') { m.weapon = 1.5; m.pack = 2.2; m.heal = 1.6; m.ammo = 1.3; }
  const cat = pickW([
    ['weapon', 22 * m.weapon], ['ammo', 20 * m.ammo], ['heal', 18 * m.heal], ['boost', (7 + tier) * m.boost],
    ['armor', 14 * m.armor], ['pack', 6 * m.pack], ['throw', 7 * m.throw], ['scope', (3 + tier * 2) * m.scope], ['attach', (7 + tier * 2) * m.weapon],
  ]);
  const out = [];
  switch (cat) {
    case 'weapon': {
      const key = pickW(GROUND_WEAPONS.map(k => [k, [[30, 30, 10, 2], [22, 26, 24, 8], [16, 22, 28, 14]][tier - 1][WEAPONS[k].rarity]]));
      const def = WEAPONS[key];
      out.push({ kind: 'weapon', key, mag: 0 });
      out.push({ kind: 'ammo', key: def.ammo, n: Math.max(AMMO[def.ammo].pack, def.mag * 2) });
      break;
    }
    case 'ammo': {
      const key = pickW([['556', 30], ['762', 24], ['9mm', 26], ['45', 12], ['12g', 12], ['300', 6 + tier * 2]]);
      out.push({ kind: 'ammo', key, n: AMMO[key].pack });
      break;
    }
    case 'heal': out.push(pickW([[{ kind: 'item', key: 'gauze', n: 5 }, 55], [{ kind: 'item', key: 'firstaid', n: 1 }, 32 + tier * 3], [{ kind: 'item', key: 'hospital', n: 1 }, 3 + tier * 3]])); break;
    case 'boost': out.push(pickW([[{ kind: 'item', key: 'tea', n: 1 }, 50], [{ kind: 'item', key: 'coffee', n: 1 }, 32], [{ kind: 'item', key: 'syringe', n: 1 }, 4 + tier * 3]])); break;
    case 'armor': {
      const lvl = pickW([[1, [70, 50, 35][tier - 1]], [2, [27, 40, 45][tier - 1]], [3, [3, 10, 20][tier - 1]]]);
      const slot = Math.random() < 0.5 ? 'helmet' : 'vest';
      out.push({ kind: slot, lvl, dur: ARMOR[slot].dur[lvl] });
      break;
    }
    case 'pack': out.push({ kind: 'pack', lvl: pickW([[1, 60], [2, 30 + tier * 4], [3, 6 + tier * 4]]) }); break;
    case 'throw': {
      if (Math.random() < 0.035) out.push({ kind: 'item', key: 'flare', n: 1 }); // very rare (GDD §6)
      else out.push({ kind: 'item', key: Math.random() < 0.6 ? 'frag' : 'slipper', n: 1 });
      break;
    }
    case 'attach': out.push({ kind: 'attach', key: pickW([['suppressor', 8 + tier * 4], ['compensator', 18], ['extmag', 22], ['quickmag', 20], ['stock', 16]]) }); break;
    case 'scope': out.push({ kind: 'scope', zoom: pickW([[1.3, 40], [2, 30], [4, 20 + tier * 3], [8, 2 + tier * 2]]) }); break;
  }
  return out;
}

// GDD §5 airdrop & §6 flare crate
export function airdropItems(flare) {
  const ex = flare ? ['jabaliya', 'haddar'] : [Math.random() < 0.5 ? 'jabaliya' : 'haddar'];
  const out = ex.map(k => ({ kind: 'weapon', key: k, mag: WEAPONS[k].mag, zoom: k === 'jabaliya' ? 8 : 1, ammo: WEAPONS[k].ammoWith }));
  out.push({ kind: 'helmet', lvl: 3, dur: ARMOR.helmet.dur[3], gold: true }, { kind: 'vest', lvl: 3, dur: ARMOR.vest.dur[3], gold: true });
  out.push({ kind: 'item', key: 'hospital', n: flare ? 2 : 1 }, { kind: 'item', key: 'syringe', n: 1 });
  if (!flare) out.push({ kind: 'scope', zoom: 8 });
  return out;
}

// ---------- item models (lying on the ground) ----------
const M = (parts) => mergeGeometries(parts, false);
const bx = (w, h, d, c, x = 0, y = 0, z = 0, rx = 0, ry = 0) => prep(new THREE.BoxGeometry(w, h, d), c, null, M4(x, y, z, rx, ry));
const cy = (r, h, c, x = 0, y = 0, z = 0, rx = 0, rz = 0) => prep(new THREE.CylinderGeometry(r, r, h, 12), c, null, M4(x, y, z, rx, 0, rz));
const MODELS = {};
function model(key) {
  if (MODELS[key]) return MODELS[key];
  let g;
  if (key.startsWith('w:')) { g = buildGun(key.slice(2)).clone(); g.rotateZ(Math.PI / 2); g.translate(0, 0.06, 0); g.scale(1.15, 1.15, 1.15); }
  else switch (key) {
    case 'ammo':   g = M([bx(0.36, 0.2, 0.22, 0x4a5a3a, 0, 0.1), bx(0.37, 0.04, 0.23, 0x2a2a2a, 0, 0.18), bx(0.1, 0.05, 0.01, 0xd9c060, 0, 0.12, -0.115)]); break;
    case 'heal':   g = M([bx(0.36, 0.2, 0.26, 0xf0ece4, 0, 0.1), bx(0.16, 0.04, 0.012, 0xd23b3b, 0, 0.12, -0.132), bx(0.045, 0.14, 0.012, 0xd23b3b, 0, 0.12, -0.132), bx(0.12, 0.04, 0.04, 0x333333, 0, 0.22)]); break;
    case 'boost':  g = M([cy(0.07, 0.24, 0x2f9e5b, 0, 0.12), cy(0.072, 0.07, 0xeeeeee, 0, 0.12), cy(0.05, 0.03, 0xb8bcc0, 0, 0.255)]); break;
    case 'syringe':g = M([cy(0.03, 0.26, 0xdde8f0, 0, 0.04, 0, 0, Math.PI / 2), cy(0.008, 0.1, 0xb8bcc0, 0.18, 0.04, 0, 0, Math.PI / 2), cy(0.02, 0.06, 0x2f6fae, -0.15, 0.04, 0, 0, Math.PI / 2)]); break;
    case 'helmet': g = M([prep(new THREE.SphereGeometry(0.2, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2), 0x3a4033, null, M4(0, 0.02, 0)), cy(0.21, 0.03, 0x2a2e26, 0, 0.02)]); break;
    case 'vest':   g = M([bx(0.48, 0.12, 0.56, 0x3a4033, 0, 0.06), bx(0.14, 0.07, 0.1, 0x2c3127, -0.13, 0.15, -0.15), bx(0.14, 0.07, 0.1, 0x2c3127, 0.13, 0.15, -0.15), bx(0.14, 0.07, 0.1, 0x2c3127, 0, 0.15, 0.05)]); break;
    case 'pack':   g = M([bx(0.42, 0.24, 0.5, 0x6a5a3a, 0, 0.12), bx(0.3, 0.12, 0.08, 0x5a4a30, 0, 0.12, -0.28), bx(0.44, 0.08, 0.2, 0x7a6a48, 0, 0.27, 0.12)]); break;
    case 'scope':  g = M([cy(0.04, 0.3, 0x2a2a2a, 0, 0.05, 0, Math.PI / 2), cy(0.055, 0.05, 0x333333, 0, 0.05, -0.15, Math.PI / 2), cy(0.05, 0.05, 0x333333, 0, 0.05, 0.15, Math.PI / 2)]); break;
    case 'attach': g = M([cy(0.035, 0.22, 0x444444, 0, 0.04, 0, Math.PI / 2), cy(0.04, 0.04, 0x222222, 0, 0.04, -0.1, Math.PI / 2)]); break;
    case 'frag':   g = M([prep(new THREE.SphereGeometry(0.07, 10, 8), 0x3a4a2a, null, M4(0, 0.07)), bx(0.03, 0.05, 0.03, 0x888888, 0, 0.15)]); break;
    case 'slipper':g = M([bx(0.12, 0.03, 0.28, 0x2a6fd8, 0, 0.02), bx(0.1, 0.03, 0.02, 0x2a6fd8, 0, 0.05, -0.04)]); break;
    case 'flare':  g = M([cy(0.04, 0.24, 0xd02020, 0, 0.04, 0, Math.PI / 2), bx(0.04, 0.1, 0.04, 0x222222, 0, 0.0, 0.06)]); break;
    default:       g = bx(0.3, 0.2, 0.3, 0x888888, 0, 0.1);
  }
  return MODELS[key] = g;
}
function modelKey(it) {
  switch (it.kind) {
    case 'weapon': return 'w:' + WEAPONS[it.key].cls;
    case 'item': return ITEMS[it.key].kind === 'heal' ? 'heal' : it.key === 'syringe' ? 'syringe' : ITEMS[it.key].kind === 'boost' ? 'boost' : it.key;
    default: return it.kind;
  }
}
// soft rarity glow under each item
const GLOW_TEX = (() => {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d'), gr = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  gr.addColorStop(0, 'rgba(255,255,255,0.9)'); gr.addColorStop(0.5, 'rgba(255,255,255,0.35)'); gr.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = gr; g.fillRect(0, 0, 64, 64);
  return new THREE.CanvasTexture(c);
})();
const GLOW_GEO = new THREE.PlaneGeometry(1.1, 1.1).rotateX(-Math.PI / 2);

export class LootSystem {
  constructor(game) {
    this.game = game;
    this.items = [];
    this.grid = new Map();
    this.capacity = 0;
    this.crates = [];
  }

  spawnAll(spots) {
    const list = [];
    for (const s of spots) {
      if (Math.random() < 0.12) continue;
      const its = rollItems(s.tier, s.type);
      its.forEach((it, i) => list.push({ it, x: s.x + i * 0.7, y: s.y, z: s.z + i * 0.3 }));
    }
    this.capacity = list.length + 800;
    this.meshes = new Map();
    this.glow = new THREE.InstancedMesh(GLOW_GEO, new THREE.MeshBasicMaterial({ map: GLOW_TEX, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending }), this.capacity);
    this.glow.count = 0; this.glow.frustumCulled = false;
    this.game.scene.add(this.glow);
    for (const l of list) this.add(l.it, l.x, l.y, l.z);
  }

  key(x, z) { return Math.floor((x + HALF) / 20) * 4096 + Math.floor((z + HALF) / 20); }

  meshFor(key) {
    let m = this.meshes.get(key);
    if (!m) {
      const gm = key.startsWith('w:') && GUN_MODEL[key.slice(2)];
      // lie the gun on its side: roll 90° about its length axis
      const real = gm && propReady(gm[0]) ? propGeometry(gm[0], { len: gm[1] }, new THREE.Euler(0, 0, Math.PI / 2)) : null;
      if (real) real.geometry.translate(0, 0.06, 0);
      m = real ? new THREE.InstancedMesh(real.geometry, real.material, 900) : new THREE.InstancedMesh(model(key), BODY_MAT, key.startsWith('w:') ? 900 : 700);
      m.count = 0; m.castShadow = true; m.frustumCulled = false; m.free = [];
      this.meshes.set(key, m); this.game.scene.add(m);
    }
    return m;
  }

  add(it, x, y, z) {
    if (this.glow.count >= this.capacity) return null;
    const key = modelKey(it), mesh = this.meshFor(key);
    const idx = mesh.free.length ? mesh.free.pop() : (mesh.count < mesh.instanceMatrix.count ? mesh.count++ : -1);
    if (idx < 0) return null;
    const gIdx = this.glow.count++;
    const e = { it, x, y, z, idx, mesh, gIdx, alive: true, rar: itemRarity(it) };
    this.items.push(e);
    const m = new THREE.Matrix4().compose(new THREE.Vector3(x, y + 0.02, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(0, Math.random() * 6.28, 0)), new THREE.Vector3(1, 1, 1));
    mesh.setMatrixAt(idx, m); mesh.instanceMatrix.needsUpdate = true;
    const gm = new THREE.Matrix4().makeTranslation(x, y + 0.03, z);
    this.glow.setMatrixAt(gIdx, gm); this.glow.setColorAt(gIdx, new THREE.Color(RARITY_COLORS[e.rar]).multiplyScalar(e.rar >= 3 ? 0.9 : 0.55));
    this.glow.instanceMatrix.needsUpdate = true; if (this.glow.instanceColor) this.glow.instanceColor.needsUpdate = true;
    const k = this.key(x, z);
    let a = this.grid.get(k); if (!a) this.grid.set(k, a = []);
    a.push(e);
    return e;
  }

  drop(it, x, z) {
    const a = Math.random() * 6.28;
    const px = x + Math.cos(a) * 0.9, pz = z + Math.sin(a) * 0.9;
    return this.add(it, px, this.game.world.getHeight(px, pz), pz);
  }

  remove(e) {
    e.alive = false;
    const zero = new THREE.Matrix4().makeScale(0, 0, 0);
    e.mesh.setMatrixAt(e.idx, zero); e.mesh.instanceMatrix.needsUpdate = true; e.mesh.free.push(e.idx);
    this.glow.setMatrixAt(e.gIdx, zero); this.glow.instanceMatrix.needsUpdate = true;
  }

  near(x, z, r, filter) {
    let best = null, bd = r * r;
    const gx = Math.floor((x + HALF) / 20), gz = Math.floor((z + HALF) / 20);
    const span = Math.ceil(r / 20);
    for (let i = -span; i <= span; i++) for (let j = -span; j <= span; j++) {
      const a = this.grid.get((gx + i) * 4096 + gz + j); if (!a) continue;
      for (const e of a) {
        if (!e.alive) continue;
        const d = (e.x - x) ** 2 + (e.z - z) ** 2;
        if (d < bd && (!filter || filter(e))) { bd = d; best = e; }
      }
    }
    return best;
  }

  // try to pick up a ground item; displaced items are dropped
  take(actor, e) {
    const res = actor.pickUp(e.it);
    if (res.taken) this.remove(e);
    for (const d of res.drop) this.drop(d, actor.pos.x, actor.pos.z);
    return res;
  }

  // ---------- crates ----------
  addCrate(x, z, items, kind, label) {
    const y = this.game.world.getHeight(x, z);
    const g = new THREE.Group();
    const col = kind === 'death' ? 0x6b4f2a : kind === 'flare' ? 0xb02020 : 0x2f6fb0;
    const box = new THREE.Mesh(new THREE.BoxGeometry(1.2, 0.8, 0.9), new THREE.MeshLambertMaterial({ color: col, emissive: kind === 'death' ? 0 : 0x221111 }));
    box.position.y = 0.4; box.castShadow = true; g.add(box);
    if (kind !== 'death') {
      const band = new THREE.Mesh(new THREE.BoxGeometry(1.25, 0.15, 0.95), new THREE.MeshLambertMaterial({ color: 0xffc531 }));
      band.position.y = 0.55; g.add(band);
    }
    g.position.set(x, y, z);
    this.game.scene.add(g);
    const c = { x, y, z, items, kind, label, mesh: g, landed: kind === 'death', smokeT: 0 };
    this.crates.push(c);
    return c;
  }
  removeCrate(c) { this.game.scene.remove(c.mesh); this.crates.splice(this.crates.indexOf(c), 1); }

  nearCrate(x, z, r) {
    let best = null, bd = r * r;
    for (const c of this.crates) {
      if (!c.landed) continue;
      const d = (c.x - x) ** 2 + (c.z - z) ** 2;
      if (d < bd) { bd = d; best = c; }
    }
    return best;
  }

  lootAll(actor, c) {
    // best weapons first, then armor, then the rest
    const order = c.items.slice().sort((a, b) => itemRarity(b) - itemRarity(a));
    let got = 0;
    for (const it of order) {
      if (!actor.isPlayer && !actor.wants(it)) continue;
      if (actor.isPlayer && it.kind === 'weapon' && actor.weapons[0] && actor.weapons[1]) {
        // swap weakest only if the crate weapon is better
        const worst = actor.weapons[0].def.rarity <= actor.weapons[1].def.rarity ? 0 : 1;
        if (WEAPONS[it.key].rarity <= actor.weapons[worst].def.rarity) continue;
        actor.cur = worst;
      }
      const res = actor.pickUp(it);
      if (res.taken) { c.items.splice(c.items.indexOf(it), 1); got++; }
      for (const d of res.drop) c.items.push(d);
    }
    if (!c.items.length) this.removeCrate(c);
    return got;
  }
}
