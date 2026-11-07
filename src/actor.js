import * as THREE from 'three';
import { WEAPONS, ITEMS, AMMO, ARMOR, HALF, ATTACHMENTS } from './config.js';
import { itemById, DEFAULT_LOADOUT } from './progression.js';
import { buildBody, buildGun } from './character.js';
import { randomLook } from './look.js';
import { clamp } from './world.js';

const GEO = {
  body: new THREE.CapsuleGeometry(0.3, 0.5, 4, 10),
  head: new THREE.SphereGeometry(0.21, 12, 10),
  leg: (() => { const g = new THREE.BoxGeometry(0.2, 0.82, 0.24); g.translate(0, -0.41, 0); return g; })(),
  arm: (() => { const g = new THREE.BoxGeometry(0.15, 0.6, 0.16); g.translate(0, -0.28, 0); return g; })(),
  pack: new THREE.BoxGeometry(0.42, 0.5, 0.24),
  gun: (() => { const g = new THREE.BoxGeometry(0.09, 0.12, 0.85); g.translate(0, 0, -0.3); return g; })(),
  scarf: new THREE.TorusGeometry(0.19, 0.07, 6, 14),
  helmet: new THREE.SphereGeometry(0.235, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2),
  chute: new THREE.SphereGeometry(3.4, 18, 8, 0, Math.PI * 2, 0, Math.PI / 2.4),
};
const SKIN = [0xe0b48f, 0xc99a74, 0xa9775a, 0x8a5c42, 0xf0c9a8];
const CLOTH = [0x6b6f4a, 0x8a7a5a, 0x4f5b66, 0x7a4a3a, 0x3f4a3a, 0x9a8f7a, 0x5a4a6a, 0x2f3f5a, 0x7a6a40, 0x4a4a4a];
const HELMET_COL = [0, 0xe8c020, 0x30343a, 0x44513a];
const GUN_GEO = buildGun();

export function itemName(it) {
  switch (it.kind) {
    case 'weapon': return WEAPONS[it.key].name;
    case 'ammo': return `${AMMO[it.key].name} ×${it.n}`;
    case 'item': return ITEMS[it.key].name + (it.n > 1 ? ` ×${it.n}` : '');
    case 'helmet': return `${ARMOR.helmet.names[it.lvl]} (Lv.${it.lvl})`;
    case 'vest': return `${ARMOR.vest.names[it.lvl]} (Lv.${it.lvl})`;
    case 'pack': return `${ARMOR.pack.names[it.lvl]} (Lv.${it.lvl})`;
    case 'scope': return { 1.3: 'Red Dot', 2: '2x Scope', 4: '4x Scope', 8: '8x Scope' }[it.zoom] || 'Scope';
    case 'attach': return ATTACHMENTS[it.key].name;
  }
  return '?';
}
export function itemRarity(it) {
  switch (it.kind) {
    case 'weapon': return WEAPONS[it.key].rarity;
    case 'ammo': return 0;
    case 'item': return ITEMS[it.key].rarity;
    case 'helmet': case 'vest': case 'pack': return [0, 0, 2, 3][it.lvl] + (it.gold ? 1 : 0);
    case 'scope': return it.zoom >= 8 ? 3 : it.zoom >= 4 ? 2 : 1;
    case 'attach': return ATTACHMENTS[it.key].rarity;
  }
  return 0;
}

// attachment effects on a weapon instance
export const magCap = w => Math.round(w.def.mag * (w.att && w.att.mag === 'extmag' ? 1.5 : 1));
export const reloadTime = w => w.def.reload * (w.att && w.att.mag === 'quickmag' ? 0.7 : 1);
export const recoilMul = w => (w.att && w.att.muzzle === 'compensator' ? 0.7 : 1) * (w.att && w.att.stock ? 0.85 : 1);
export const spreadMul = w => (w.att && w.att.stock ? 0.8 : 1);
export const suppressed = w => !!(w && w.att && w.att.muzzle === 'suppressor');
const fits = (w, key) => w && ATTACHMENTS[key].fits.includes(w.def.cls);

export class Actor {
  constructor(game, id, name, isPlayer) {
    this.game = game; this.id = id; this.name = name; this.isPlayer = isPlayer;
    this.pos = new THREE.Vector3(); this.vy = 0; this.vel = new THREE.Vector3();
    this.yaw = 0; this.pitch = 0;
    this.hp = 100; this.boost = 0; this.alive = true;
    this.helmet = { lvl: 0, dur: 0 }; this.vest = { lvl: 0, dur: 0 }; this.pack = 0;
    this.weapons = [null, null]; this.cur = 0;
    this.ammo = {}; for (const k in AMMO) this.ammo[k] = 0;
    this.items = { gauze: 0, firstaid: 0, hospital: 0, tea: 0, coffee: 0, syringe: 0, frag: 0, slipper: 0, flare: 0 };
    this.throwSel = 'frag';
    this.state = 'plane'; // plane | fall | chute | ground | dead
    this.grounded = false; this.swimming = false;
    this.action = null;    // { type:'heal'|'reload', key, t, total }
    this.fireCd = 0; this.kills = 0; this.damageDealt = 0;
    this.walk = 0; this.lastHitBy = null; this.place = 0;
    this.team = id; this.knocked = false; this.knockCount = 0; this.knockedBy = null;
    this.buildMesh();
  }

  buildMesh() {
    const g = new THREE.Group();
    this.mesh = g;
    this.packMesh = new THREE.Mesh(GEO.pack, new THREE.MeshLambertMaterial({ color: 0x5a4a32 })); this.packMesh.position.set(0, 1.25, 0.26); this.packMesh.visible = false; g.add(this.packMesh);
    this.helmetMesh = new THREE.Mesh(GEO.helmet, new THREE.MeshLambertMaterial({ color: 0x333333 })); this.helmetMesh.position.y = 1.79; this.helmetMesh.scale.setScalar(1.06); this.helmetMesh.visible = false; g.add(this.helmetMesh);
    this.gunMesh = new THREE.Mesh(GUN_GEO, new THREE.MeshLambertMaterial({ color: 0x404244, vertexColors: true })); this.gunMesh.position.set(0.2, 1.36, -0.32); this.gunMesh.visible = false; g.add(this.gunMesh);
    this.chute = new THREE.Mesh(GEO.chute, new THREE.MeshLambertMaterial({ color: this.isPlayer ? 0xc0262d : 0xd8d0b8, side: THREE.DoubleSide })); this.chute.position.y = 6; this.chute.visible = false; g.add(this.chute);
    this.chuteStripe = new THREE.Mesh(new THREE.TorusGeometry(2.6, 0.18, 6, 24), new THREE.MeshLambertMaterial({ color: 0xf0e6d2 })); this.chuteStripe.rotation.x = Math.PI / 2; this.chuteStripe.position.y = 1.35; this.chute.add(this.chuteStripe);
    this.gunColor = 0x404244;
    this.packMesh.castShadow = this.helmetMesh.castShadow = this.gunMesh.castShadow = true;
    this.setLook(randomLook());
    g.visible = false;
    this.game.scene.add(g);
  }

  // (re)build the body from a look {sex, skin, hair, …}
  setLook(look) {
    this.look = look;
    const g = this.mesh;
    for (const k of ['torso', 'hair', 'legL', 'legR', 'armL', 'armR']) if (this[k]) {
      g.remove(this[k]); this[k].traverse(o => o.geometry && o.geometry.dispose());
    }
    Object.assign(this, buildBody(look));
    g.add(this.torso, this.hair, this.legL, this.legR, this.armL, this.armR);
    this.refreshLook();
  }

  applyLoadout(l) {
    l = { ...DEFAULT_LOADOUT, ...l }; this.loadout = l;
    const ch = itemById(l.chute) || itemById('chute_red'); this.chute.material.color.setHex(ch.color); this.chuteStripe.material.color.setHex(ch.color2);
    this.gunColor = (itemById(l.gun) || itemById('gun_default')).color;
    this.refreshLook();
  }

  get weapon() { return this.weapons[this.cur]; }
  capacity() { return ARMOR.pack.capacity[this.pack]; }
  load() {
    let w = 0;
    for (const k in this.ammo) w += this.ammo[k] * AMMO[k].w;
    for (const k in this.items) w += this.items[k] * ITEMS[k].w;
    return w;
  }
  freeSpace() { return this.capacity() - this.load(); }

  refreshLook() {
    this.helmetMesh.visible = this.helmet.lvl > 0;
    if (this.hair) this.hair.visible = !(this.helmet.lvl > 0 && this.look && !this.look.hijab);
    if (this.helmet.lvl) this.helmetMesh.material.color.setHex(HELMET_COL[this.helmet.lvl]);
    this.packMesh.visible = this.pack > 0;
    this.packMesh.scale.setScalar(0.8 + this.pack * 0.15);
    this.gunMesh.visible = !!this.weapon;
    if (this.weapon) this.gunMesh.scale.z = this.weapon.def.cls === 'PISTOL' ? 0.45 : this.weapon.def.cls === 'SNIPER' ? 1.35 : this.weapon.def.cls === 'LMG' ? 1.2 : 1;
    this.gunMesh.material.color.setHex(this.weapon && this.weapon.def.exclusive ? 0xd9a520 : this.gunColor === 0x202224 ? 0x404244 : this.gunColor);
  }

  // ---------- pickup (returns {taken, drop:[items]}) ----------
  pickUp(it) {
    const drop = [];
    switch (it.kind) {
      case 'weapon': {
        const def = WEAPONS[it.key];
        const inst = { key: it.key, def, mag: it.mag ?? def.mag, zoom: it.zoom || 1, att: { ...(it.att || {}) } };
        let slot = this.weapons[0] ? (this.weapons[1] ? -1 : 1) : 0;
        if (slot < 0) {
          if (!this.isPlayer) { // bots replace their weakest weapon if better
            const worst = this.weapons[0].def.rarity <= this.weapons[1].def.rarity ? 0 : 1;
            if (def.rarity <= this.weapons[worst].def.rarity) return { taken: false, drop };
            slot = worst;
          } else slot = this.cur;
          const old = this.weapons[slot];
          drop.push({ kind: 'weapon', key: old.key, mag: old.mag, zoom: old.zoom, att: { ...old.att } });
          if (old.def.exclusive) { drop.push({ kind: 'ammo', key: old.def.ammo, n: this.ammo[old.def.ammo] }); this.ammo[old.def.ammo] = 0; }
        }
        this.weapons[slot] = inst;
        if (def.exclusive) this.ammo[def.ammo] += it.ammo ?? def.ammoWith;
        if (!this.weapon) this.cur = slot;
        if (this.isPlayer && slot !== this.cur && !this.weapons[this.cur]) this.cur = slot;
        if (!this.isPlayer && (!this.weapon || def.rarity > this.weapon.def.rarity)) this.cur = slot;
        this.refreshLook();
        return { taken: true, drop };
      }
      case 'ammo': {
        const per = AMMO[it.key].w;
        const n = per === 0 ? it.n : Math.min(it.n, Math.floor(this.freeSpace() / per));
        if (n <= 0) return { taken: false, drop };
        this.ammo[it.key] += n;
        if (n < it.n) { it.n -= n; return { taken: false, partial: true, drop }; }
        return { taken: true, drop };
      }
      case 'item': {
        const per = ITEMS[it.key].w;
        const n = Math.min(it.n, Math.floor(this.freeSpace() / per));
        if (n <= 0) return { taken: false, drop };
        this.items[it.key] += n;
        if (n < it.n) { it.n -= n; return { taken: false, partial: true, drop }; }
        return { taken: true, drop };
      }
      case 'helmet': case 'vest': {
        const cur = this[it.kind];
        const better = it.lvl > cur.lvl || (it.lvl === cur.lvl && it.dur > cur.dur + 1);
        if (!better) return { taken: false, drop };
        if (cur.lvl) drop.push({ kind: it.kind, lvl: cur.lvl, dur: cur.dur });
        this[it.kind] = { lvl: it.lvl, dur: it.dur ?? ARMOR[it.kind].dur[it.lvl] };
        this.refreshLook();
        return { taken: true, drop };
      }
      case 'pack': {
        if (it.lvl <= this.pack) return { taken: false, drop };
        if (this.pack) drop.push({ kind: 'pack', lvl: this.pack });
        this.pack = it.lvl; this.refreshLook();
        return { taken: true, drop };
      }
      case 'scope': {
        const w = this.weapon || this.weapons[0] || this.weapons[1];
        if (!w || w.def.cls === 'SG' || w.def.cls === 'PISTOL' || w.zoom >= it.zoom) return { taken: false, drop };
        if (w.zoom > 1) drop.push({ kind: 'scope', zoom: w.zoom });
        w.zoom = it.zoom;
        return { taken: true, drop };
      }
      case 'attach': {
        const a = ATTACHMENTS[it.key];
        const order = [this.weapon, this.weapons[0], this.weapons[1]];
        const w = order.find(x => fits(x, it.key) && !x.att[a.slot]) || (this.isPlayer ? order.find(x => fits(x, it.key) && x.att[a.slot] !== it.key) : null);
        if (!w) return { taken: false, drop };
        if (w.att[a.slot]) drop.push({ kind: 'attach', key: w.att[a.slot] });
        if (w.att.mag === 'extmag' && a.slot === 'mag') { const over = w.mag - Math.round(w.def.mag); if (over > 0) { w.mag -= over; this.ammo[w.def.ammo] += over; } }
        w.att[a.slot] = it.key;
        return { taken: true, drop };
      }
    }
    return { taken: false, drop };
  }

  wants(it) { // AI / auto-loot filter
    switch (it.kind) {
      case 'weapon': return !this.weapons[0] || !this.weapons[1] || WEAPONS[it.key].rarity > Math.min(this.weapons[0].def.rarity, this.weapons[1].def.rarity);
      case 'ammo': return this.weapons.some(w => w && w.def.ammo === it.key) && this.ammo[it.key] < 180 && this.freeSpace() > 2;
      case 'item': return this.freeSpace() > ITEMS[it.key].w && (this.items[it.key] < (it.key === 'gauze' ? 8 : 4));
      case 'helmet': case 'vest': return it.lvl > this[it.kind].lvl;
      case 'pack': return it.lvl > this.pack;
      case 'scope': return this.weapons.some(w => w && w.zoom < it.zoom && w.def.cls !== 'SG' && w.def.cls !== 'PISTOL');
      case 'attach': return this.weapons.some(w => fits(w, it.key) && !w.att[ATTACHMENTS[it.key].slot]);
    }
    return false;
  }

  // everything this actor carries, for the death crate
  inventory() {
    const out = [];
    for (const w of this.weapons) if (w) {
      out.push({ kind: 'weapon', key: w.key, mag: w.mag, zoom: w.zoom, att: { ...w.att }, ammo: w.def.exclusive ? this.ammo[w.def.ammo] : undefined });
      if (w.def.exclusive) this.ammo[w.def.ammo] = 0;
    }
    for (const k in this.ammo) if (this.ammo[k] > 0) out.push({ kind: 'ammo', key: k, n: this.ammo[k] });
    for (const k in this.items) if (this.items[k] > 0) out.push({ kind: 'item', key: k, n: this.items[k] });
    if (this.helmet.lvl) out.push({ kind: 'helmet', lvl: this.helmet.lvl, dur: this.helmet.dur });
    if (this.vest.lvl) out.push({ kind: 'vest', lvl: this.vest.lvl, dur: this.vest.dur });
    if (this.pack) out.push({ kind: 'pack', lvl: this.pack });
    return out;
  }

  // ---------- combat ----------
  takeDamage(raw, part, attacker, cause, bypassArmor = false) {
    if (!this.alive) return 0;
    if (this.game.noDamage) return 0;
    if (this.knocked) {                       // knocked: no armor, damage drains the bleed-out pool
      this.hp -= raw;
      if (attacker && attacker !== this) { this.lastHitBy = attacker; attacker.damageDealt += raw; }
      this.game.onDamage(this, raw, part, attacker, cause);
      if (this.hp <= 0) { this.hp = 0; this.game.onKill(this, attacker || this.knockedBy, cause, part === 'head'); }
      return raw;
    }
    let dmg = raw;
    if (!bypassArmor) {
      const slot = part === 'head' ? 'helmet' : 'vest';
      const a = this[slot];
      if (a.lvl) {
        dmg = raw * (1 - ARMOR[slot].reduce[a.lvl]);
        a.dur -= raw;
        if (a.dur <= 0) { this[slot] = { lvl: 0, dur: 0 }; this.refreshLook(); }
      }
    }
    this.hp -= dmg;
    if (attacker && attacker !== this) { this.lastHitBy = attacker; attacker.damageDealt += dmg; }
    if (this.action && this.action.type === 'heal') this.action = null;
    this.game.onDamage(this, dmg, part, attacker, cause);
    if (this.hp <= 0) {
      if (this.game.canKnock(this)) {
        this.knocked = true; this.hp = 100; this.knockedBy = attacker; this.action = null; this.knockCount++;
        this.game.onKnock(this, attacker, cause, part === 'head');
      } else { this.hp = 0; this.game.onKill(this, attacker, cause, part === 'head'); }
    }
    return dmg;
  }
  revived() { this.knocked = false; this.hp = 20; this.action = null; this.knockedBy = null; }

  canFire() {
    const w = this.weapon;
    return w && this.state === 'ground' && !this.knocked && !this.swimming && this.fireCd <= 0 && w.mag > 0 && !(this.action && this.action.type === 'reload');
  }

  startReload() {
    const w = this.weapon;
    if (!w || this.knocked || w.mag >= magCap(w) || this.ammo[w.def.ammo] <= 0 || (this.action && this.action.type === 'reload')) return false;
    const t = reloadTime(w);
    this.action = { type: 'reload', t, total: t };
    return true;
  }
  finishReload() {
    const w = this.weapon; if (!w) return;
    const need = magCap(w) - w.mag, n = Math.min(need, this.ammo[w.def.ammo]);
    w.mag += n; this.ammo[w.def.ammo] -= n;
  }

  // healing per GDD §9
  startUse(key) {
    const def = ITEMS[key];
    if (!def || this.knocked || this.items[key] <= 0 || this.state !== 'ground') return false;
    if (def.kind === 'heal' && this.hp >= def.cap) return false;
    if (def.kind === 'boost' && this.boost >= 100) return false;
    this.action = { type: 'heal', key, t: def.time, total: def.time };
    return true;
  }
  finishUse(key) {
    const def = ITEMS[key];
    this.items[key]--;
    if (def.kind === 'heal') this.hp = clamp(Math.max(this.hp, Math.min(def.cap, this.hp + def.heal)), 0, 100);
    if (def.kind === 'boost') this.boost = Math.min(100, this.boost + def.boost);
  }
  bestHeal() {
    if (this.hp < 75) {
      if (this.hp < 40 && this.items.hospital) return 'hospital';
      if (this.items.firstaid && this.hp < 60) return 'firstaid';
      if (this.items.gauze) return 'gauze';
      if (this.items.firstaid) return 'firstaid';
      if (this.items.hospital) return 'hospital';
    } else if (this.hp < 100 && this.items.hospital) return 'hospital';
    return null;
  }
  bestBoost() {
    if (this.boost >= 100) return null;
    if (this.boost < 30 && this.items.syringe) return 'syringe';
    if (this.items.coffee && this.boost <= 60) return 'coffee';
    if (this.items.tea) return 'tea';
    if (this.items.coffee) return 'coffee';
    if (this.items.syringe) return 'syringe';
    return null;
  }

  speedMul() {
    let m = 1;
    if (this.boost >= 60) m *= 1.06;
    if (this.action && this.action.type === 'heal') m *= 0.45;
    if (this.swimming) m *= 0.45;
    if (this.knocked) m *= 0.22;
    if (this.action && this.action.type === 'revive') m = 0;
    return m;
  }

  tickStatus(dt) {
    if (this.fireCd > 0) this.fireCd -= dt;
    if (this.knocked) {                      // bleeding out; faster after each knock
      if (!this.game.noDamage) this.hp -= (1.6 + this.knockCount * 0.9) * dt;
      if (this.hp <= 0) { this.hp = 0; this.game.onKill(this, this.knockedBy, 'Bled out', false); }
      return;
    }
    if (this.action && this.action.type === 'revive') {
      const t = this.action.target;
      if (!t.alive || !t.knocked || Math.hypot(t.pos.x - this.pos.x, t.pos.z - this.pos.z) > 2.6) { this.action = null; return; }
    }
    if (this.boost > 0) { // boost heals over time
      const rate = this.boost >= 90 ? 0.9 : this.boost >= 60 ? 0.7 : this.boost >= 20 ? 0.5 : 0.3;
      this.hp = Math.min(100, this.hp + rate * dt);
      this.boost = Math.max(0, this.boost - dt * 0.4);
    }
    if (this.action) {
      this.action.t -= dt;
      if (this.action.t <= 0) {
        const a = this.action; this.action = null;
        if (a.type === 'reload') this.finishReload();
        else if (a.type === 'revive') { a.target.revived(); this.game.onRevive(a.target, this); }
        else this.finishUse(a.key);
        if (this.isPlayer) this.game.audio.ui(a.type === 'reload' ? 'reload' : 'heal');
      }
    }
  }

  // ---------- movement (shared by player & bots) ----------
  move(dx, dz, dt, sprint, wantJump) {
    const w = this.game.world;
    const p = this.pos;
    if (this.state !== 'ground') return;
    const speed = (sprint ? 8.6 : 5.6) * this.speedMul();
    const len = Math.hypot(dx, dz);
    if (len > 1) { dx /= len; dz /= len; }
    const vx = dx * speed, vz = dz * speed;
    const oldX = p.x, oldZ = p.z, oldG = w.getHeight(p.x, p.z);
    p.x += vx * dt; p.z += vz * dt;
    // steep slopes block (try sliding per axis)
    if (this.grounded) {
      const ng = w.getHeight(p.x, p.z), dist = Math.hypot(p.x - oldX, p.z - oldZ);
      if (dist > 1e-4 && (ng - oldG) / dist > 1.5) {
        p.set(oldX, p.y, oldZ);
        if (w.getHeight(oldX + vx * dt, oldZ) - oldG < Math.abs(vx * dt) * 1.5) p.x += vx * dt;
        else if (w.getHeight(oldX, oldZ + vz * dt) - oldG < Math.abs(vz * dt) * 1.5) p.z += vz * dt;
      }
    }
    w.collideCircle(p, 0.38, p.y + 0.35, p.y + 1.85);
    p.x = clamp(p.x, -HALF + 2, HALF - 2); p.z = clamp(p.z, -HALF + 2, HALF - 2);
    const g = w.getHeight(p.x, p.z), lvl = w.waterLevel(p.x, p.z);
    this.swimming = g < lvl - 1.2;
    if (this.swimming) { p.y = lvl - 1.25; this.vy = 0; this.grounded = true; }
    else {
      if (wantJump && this.grounded) { this.vy = 6.2; this.grounded = false; }
      this.vy -= 20 * dt;
      p.y += this.vy * dt;
      if (this.grounded && p.y > g && p.y - g < 0.7 && this.vy <= 0) p.y = g; // stick to slopes
      if (p.y <= g) { p.y = g; this.vy = 0; this.grounded = true; } else if (p.y - g > 0.05) this.grounded = false;
    }
    const moved = Math.hypot(p.x - oldX, p.z - oldZ);
    this.walk += moved * 1.7;
    this.moving = moved > 0.01;
    this.sprinting = sprint && this.moving;
  }

  updateMesh(dt) {
    const g = this.mesh;
    if (this.state === 'plane' || this.state === 'dead') { g.visible = false; return; }
    g.visible = true;
    g.position.copy(this.pos);
    g.rotation.y = this.yaw;
    const sw = this.moving && this.state === 'ground' ? Math.sin(this.walk) * (this.sprinting ? 0.8 : 0.55) : 0;
    this.legL.rotation.x = sw; this.legR.rotation.x = -sw;
    const aimArm = 1.25 + this.pitch * 0.8;
    this.armR.rotation.x = this.weapon ? aimArm : sw * 0.6;
    this.armL.rotation.x = this.weapon ? aimArm + 0.2 : -sw * 0.6;
    this.armL.rotation.z = this.weapon ? -0.45 : 0; this.armR.rotation.z = this.weapon ? 0.12 : 0;
    this.gunMesh.rotation.x = this.pitch;
    this.chute.visible = this.state === 'chute';
    if (this.state === 'fall') { g.rotation.x = -1.1; } else if (this.knocked) { g.rotation.x = -1.35; g.position.y += 0.35; } else g.rotation.x = 0;
    if (this.swimming) g.position.y -= 0.2;
  }

  eye() { return new THREE.Vector3(this.pos.x, this.pos.y + 1.62, this.pos.z); }
}

// ray vs actor hitbox: head sphere + body cylinder. returns {t, part} or null
export function rayActor(ox, oy, oz, dx, dy, dz, a, maxT) {
  const px = a.pos.x, py = a.pos.y + (a.swimming ? -0.2 : 0) + (a.knocked ? -1.1 : 0), pz = a.pos.z;
  let best = null;
  // head
  const hx = px, hy = py + 1.74, hz = pz, r = 0.25;
  const lx = ox - hx, ly = oy - hy, lz = oz - hz;
  const b = lx * dx + ly * dy + lz * dz, c = lx * lx + ly * ly + lz * lz - r * r;
  const disc = b * b - c;
  if (disc >= 0) { const t = -b - Math.sqrt(disc); if (t > 0 && t < maxT) best = { t, part: 'head' }; }
  // body cylinder r .4 from y+0.05 to y+1.52
  const R = 0.4, ex = ox - px, ez = oz - pz;
  const A = dx * dx + dz * dz, B = ex * dx + ez * dz, C = ex * ex + ez * ez - R * R;
  if (A > 1e-9) {
    const d2 = B * B - A * C;
    if (d2 >= 0) {
      const t = (-B - Math.sqrt(d2)) / A;
      const y = oy + dy * t;
      if (t > 0 && t < maxT && y > py + 0.05 && y < py + 1.52 && (!best || t < best.t)) best = { t, part: 'body' };
    }
  }
  return best;
}
