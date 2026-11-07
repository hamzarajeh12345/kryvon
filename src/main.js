import * as THREE from 'three';
import { loadHeroes } from './heroes.js';
import { loadProps, propReady, propClone } from './props.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { HALF, BOT_NAMES, WEAPONS, ITEMS, PRESETS, WEATHER, AIRDROP_INTERVAL, RARITY_COLORS, AMMO, ARMOR, ATTACHMENTS, ATT_SLOTS, QUICK_CHAT, ZONES } from './config.js';
import { World, clamp } from './world.js';
import { Actor, rayActor, itemName, itemRarity, magCap, recoilMul, spreadMul, suppressed } from './actor.js';
import { MODES, rewards, tierOf, randomLoadout } from './progression.js';
import { Ring, RedZone, Heatmap } from './zones.js';
import { LootSystem, airdropItems } from './loot.js';
import { Effects, WeatherFX } from './effects.js';
import { Audio } from './audio.js';
import { HUD } from './hud.js';
import { Input } from './input.js';
import { initBot, updateBot, hearShot, distract } from './bots.js';
import { UI } from './ui.js';
import { buildPlaneMesh } from './plane-model.js';
import { VehicleSystem, VEHICLES } from './vehicles.js';
import { account, RANKS, RANKS_AR, rankOf } from './account.js';

const $ = id => document.getElementById(id);
const fwd = (yaw, pitch) => new THREE.Vector3(-Math.sin(yaw) * Math.cos(pitch), Math.sin(pitch), -Math.cos(yaw) * Math.cos(pitch));
const pickWeighted = arr => { let s = 0; for (const a of arr) s += a.w; let r = Math.random() * s; for (const a of arr) { r -= a.w; if (r <= 0) return a; } return arr[0]; };

// ---------------- plane ----------------
class Plane {
  constructor(game, alt = 320, speed = 85, through = null, len = 2500) {
    this.game = game;
    const a = Math.random() * Math.PI * 2;
    this.dir = new THREE.Vector3(Math.cos(a), 0, Math.sin(a));
    const perp = new THREE.Vector3(-this.dir.z, 0, this.dir.x);
    const c = through ? new THREE.Vector3(through.x, 0, through.z) : perp.clone().multiplyScalar((Math.random() - 0.5) * 500);
    this.a = c.clone().addScaledVector(this.dir, -len / 2); this.b = c.clone().addScaledVector(this.dir, len / 2);
    this.a.y = this.b.y = alt;
    this.len = len; this.t = 0; this.speed = speed; this.active = true;
    this.pos = this.a.clone();
    let g, props = [];
    if (propReady('jet')) { g = new THREE.Group(); const j = propClone('jet', { len: 24 }); j.position.y = -2; g.add(j); }
    else ({ group: g, props } = buildPlaneMesh());
    this.props = props;
    g.rotation.y = Math.atan2(-this.dir.x, -this.dir.z);
    this.mesh = g; game.scene.add(g);
  }
  update(dt) {
    this.t += dt * this.speed;
    this.pos.copy(this.a).addScaledVector(this.dir, this.t);
    this.mesh.position.copy(this.pos);
    for (const p of this.props) p.rotation.z += dt * 38;
    if (this.t > this.len) { this.active = false; this.game.scene.remove(this.mesh); }
  }
  // progress along the path closest to point p
  along(p) { return new THREE.Vector3(p.x - this.a.x, 0, p.z - this.a.z).dot(this.dir); }
  perpDist(p) { const s = this.along(p); const q = this.a.clone().addScaledVector(this.dir, s); return Math.hypot(p.x - q.x, p.z - q.z); }
}

// ---------------- game ----------------
class Game {
  constructor() {
    this.canvas = $('game');
    this.audio = new Audio();
    this.ui = new UI(this);
    this.ui.boot();
  }

  start({ map, settings, user, mode }) {
    this.map = map; this.settings = settings; this.user = user;
    this.mode = map.training ? MODES[0] : (MODES.find(m => m.id === mode) || MODES[0]);
    this.teamSize = this.mode.size;
    this.audio.init();
    this.audio.setVolumes(settings.audio);
    $('lobby').style.display = 'none'; $('auth').style.display = 'none';
    $('loading').style.display = 'flex';
    const lm = document.getElementById('loadmsg'); if (lm) lm.textContent = 'تحميل الشخصيات…';
    setTimeout(() => Promise.all([loadHeroes(), loadProps()]).catch(() => {}).then(() => this.build()), 50);
  }

  build() {
    const S = this.settings, map = this.map;
    const preset = this.preset = { ...(PRESETS[S.graphics.preset] || PRESETS.medium) };
    if (!S.graphics.shadows) preset.shadows = 0;
    preset.far *= S.graphics.view / 100;
    const r = this.renderer = new THREE.WebGLRenderer({ canvas: this.canvas, antialias: preset.aa, powerPreference: 'high-performance' });
    r.setPixelRatio(Math.min(devicePixelRatio, 2) * preset.pixel);
    r.setSize(innerWidth, innerHeight);
    r.shadowMap.enabled = preset.shadows > 0;
    r.shadowMap.type = THREE.PCFSoftShadowMap;
    r.toneMapping = THREE.ACESFilmicToneMapping; r.toneMappingExposure = 0.95;
    r.outputColorSpace = THREE.SRGBColorSpace;
    this.scene = new THREE.Scene();
    { const pm = new THREE.PMREMGenerator(r); this.scene.environment = pm.fromScene(new RoomEnvironment(), 0.04).texture; pm.dispose(); }
    this.camera = new THREE.PerspectiveCamera(72, innerWidth / innerHeight, 0.1, 2600);
    addEventListener('resize', () => { r.setSize(innerWidth, innerHeight); this.camera.aspect = innerWidth / innerHeight; this.camera.updateProjectionMatrix(); });

    this.weather = pickWeighted(WEATHER);
    this.setupSky();
    this.world = new World(this.scene, preset);
    this.effects = new Effects(this.scene);
    this.weatherFX = new WeatherFX(this.scene, this.weather.id);
    this.loot = new LootSystem(this);
    this.loot.spawnAll(this.world.lootSpots);
    this.world.halfSize = HALF;
    this.ring = new Ring(this);
    this.redzone = new RedZone(this);
    this.heat = new Heatmap();
    this.input = new Input(this.canvas, S);
    this.audio.muffle = this.weather.id === 'rain' ? 0.6 : 1;

    // actors
    const matches = this.user.stats.matches;
    const skillScale = matches < 2 ? 0.55 : matches < 5 ? 0.8 : 1; // GDD §17: softer bots in first matches
    this.actors = [];
    this.player = new Actor(this, 0, this.user.username, true);
    if (this.user.look) this.player.setLook(this.user.look);
    this.player.applyLoadout(this.user.loadout || {});
    this.actors.push(this.player);
    this.total = map.players;
    this.seenLandmarks = new Set();
    this.teams = {}; this.pings = []; this.chatLog = [];
    if (map.training) { this.player.team = 0; this.teams[0] = [this.player]; this.setupTraining(); }
    else {
      const names = BOT_NAMES.slice().sort(() => Math.random() - 0.5);
      for (let i = 1; i < this.total; i++) {
        const b = new Actor(this, i, names[(i - 1) % names.length] + (i > names.length ? i : ''), false);
        initBot(b, skillScale);
        b.applyLoadout(randomLoadout());
        this.actors.push(b);
      }
      // teams: you + bot teammates are team 0
      this.actors.forEach((a, i) => { a.team = Math.floor(i / this.teamSize); (this.teams[a.team] ||= []).push(a); });
      for (const a of this.teams[0]) if (!a.isPlayer) { a.ai.mate = true; a.ai.skill = Math.max(a.ai.skill, 0.55); }
      this.teamCount = Object.keys(this.teams).length;
      this.setupIsland();
    }
    this.aliveCount = this.total;
    this.teamsLeft = this.teamCount || 1;
    if (map.training) {
      const s = this.spawnPoint, yaw = 0;
      this.vehicles = new VehicleSystem(this, { spots: [
        { type: 'pickup', x: s.x + 9, z: s.z + 4, yaw }, { type: 'taxi', x: s.x + 14, z: s.z + 4, yaw },
        { type: 'buggy', x: s.x + 9, z: s.z + 11, yaw }, { type: 'moto', x: s.x + 13, z: s.z + 11, yaw }, { type: 'minibus', x: s.x + 19, z: s.z + 6, yaw } ] });
    } else this.vehicles = new VehicleSystem(this);

    this.time = 0; this.matchT = 0; this.shake = 0; this.bloom = 0;
    this.projectiles = []; this.drops = []; this.flares = [];
    this.airdropT = AIRDROP_INTERVAL[0] + Math.random() * (AIRDROP_INTERVAL[1] - AIRDROP_INTERVAL[0]);
    this.saraUsed = false; this.lastZone = null; this.over = false;
    this.hud = new HUD(this);
    this.bindHudButtons();
    this.ui.bindPause();
    this.applySettings(S);
    $('loading').style.display = 'none';
    $('hud').style.display = 'block';
    $('weather').textContent = `${map.ar} · ${this.weather.name}`;
    if (map.training) this.sara('أهلين بساحة التدريب! كل الأسلحة والسكوبات والقطع قدامك، والأهداف على بعد 10–200 متر. افتح الإعدادات (Esc) وعدّل الحساسية وإنت بتجرّب.', 10);
    else this.sara(`أهلين بساحة الانتظار! الطيارة بتقلع بعد شوي. ${this.teamSize > 1 ? `إنت بفريق مع ${this.teams[0].filter(a => !a.isPlayer).map(a => '<b>' + a.name + '</b>').join(' و ')}. ` : ''}<b>Z</b> رسائل سريعة · <b>V</b> علامة · <b>I</b> الحقيبة.`, 9);
    this.hud.buildTeam();
    this.input.lock();
    this.last = performance.now(); this.fpsT = 0; this.fpsN = 0;
    requestAnimationFrame(t => this.loop(t));
  }

  // ---------- waiting area → plane ----------
  setupIsland() {
    const z = ZONES.find(q => q.id === 'deadsea');
    this.island = { x: z.x - 10, z: z.z, t: 20 };
    this.phase = 'island'; this.noDamage = true;
    for (const a of this.actors) {
      const p = this.world.randomLandPoint(Math.random, this.island.x, this.island.z, 32);
      a.pos.set(p.x, this.world.getHeight(p.x, p.z), p.z); a.state = 'ground'; a.grounded = true; a.mesh.visible = true;
      a.yaw = Math.random() * 6.28;
      if (!a.isPlayer) a.ai.wander = { x: p.x, z: p.z, t: 0 };
    }
    this.camYaw = 0; this.camPitch = -0.1;
  }
  board() {
    const m = this.map, full = m.radius >= 1000;
    this.phase = 'match'; this.noDamage = false;
    this.plane = full ? new Plane(this) : new Plane(this, 300, 80, { x: m.cx, z: m.cz }, m.radius * 2 + 320);
    this.vehicles.ejectAll();
    for (const a of this.actors) { a.pos.copy(this.plane.pos); a.state = 'plane'; a.hp = 100; a.action = null; a.vy = 0; }
    this.planBotJumps();
    this.camYaw = Math.atan2(-this.plane.dir.x, -this.plane.dir.z); this.camPitch = -0.25;
    this.audio.plane(Math.min(32, this.plane.len / this.plane.speed));
    this.sara(`يلا! افتح الخريطة (<b>M</b>) واختار وين تنزل، واضغط <b>SPACE</b> عشان تنط${this.teamSize > 1 ? ' – فريقك بنط معك' : ''}. الأسماء الذهبية = لوت عالي، و◆ = آثار.`, 8);
  }
  updateIsland(dt) {
    this.island.t -= dt;
    for (const a of this.actors) {
      if (a.isPlayer || !a.alive) continue;
      const w = a.ai.wander; w.t -= dt;
      if (w.t <= 0) { const p = this.world.randomLandPoint(Math.random, this.island.x, this.island.z, 30); w.x = p.x; w.z = p.z; w.t = 2 + Math.random() * 5; }
      const dx = w.x - a.pos.x, dz = w.z - a.pos.z, d = Math.hypot(dx, dz);
      if (d > 1.5) { a.yaw = Math.atan2(-dx, -dz); a.move(dx / d * 0.6, dz / d * 0.6, dt, false, Math.random() < 0.003); } else a.move(0, 0, dt, false, false);
      a.updateMesh(dt);
    }
    if (this.island.t <= 0) this.board();
  }

  // ---------- teams ----------
  mates(a) { return (this.teams[a.team] || []).filter(m => m !== a); }
  teamUp(t) { return (this.teams[t] || []).some(m => m.alive); }
  canKnock(a) { return this.teamSize > 1 && !this.map.training && this.mates(a).some(m => m.alive && !m.knocked); }
  onKnock(v, attacker, cause, head) {
    this.hud.killfeed(`<b>${this.tagName(attacker)}</b> <span class="wpn">${head ? '🎯 ' : ''}${cause}</span> <b>${this.tagName(v)}</b> <i>(إسقاط)</i>`);
    if (attacker === this.player) { this.audio.hit(true); this.toast(`أسقطت <b>${v.name}</b>`); }
    if (v === this.player) { this.player.action = null; this.sara('انصبت! زحف لورا غطا – فريقك رح ييجي ينعشك.', 5); }
    else if (v.team === 0) this.teamChat(v, 'انصبت! حدا ينعشني!');
    // whole team down → everyone bleeds out at once
    if (!this.mates(v).some(m => m.alive && !m.knocked)) for (const m of [v, ...this.mates(v)]) if (m.alive && m.knocked) this.onKill(m, m.knockedBy || attacker, 'Squad wiped', false);
  }
  onRevive(v, by) {
    this.hud.killfeed(`<b>${this.tagName(by)}</b> <span class="wpn">✚</span> <b>${this.tagName(v)}</b>`);
    if (by === this.player) this.toast(`أنعشت <b>${v.name}</b>`);
    if (v.team === 0 && by !== this.player) this.teamChat(by, `قوم يا ${v.name}، ما تخاف!`);
  }
  tagName(a) { return a ? (a.team === 0 ? `<span class="${a.isPlayer ? 'me' : 'mate'}">${a.name}</span>` : a.name) : '?'; }

  // ---------- squad mates in vehicles ----------
  mateBoard(v) {
    for (const m of this.mates(this.player)) {
      if (!m.alive || m.knocked || m.state !== 'ground' || Math.hypot(m.pos.x - v.pos.x, m.pos.z - v.pos.z) > 14) continue;
      if (this.vehicles.enter(m, v, false)) this.teamChat(m, 'ركبت!');
    }
  }
  mateExit(v) { for (const m of this.mates(this.player)) if (m.vehicle === v) this.vehicles.exit(m); }
  updateMates() {
    const p = this.player;
    if (p.state !== 'vehicle' || !p.vehicle) return;
    // mates walking up to your car hop in when they reach it
    for (const m of this.mates(p)) {
      if (!m.alive || m.knocked || m.state !== 'ground' || !m.ai) continue;
      const d = Math.hypot(m.pos.x - p.vehicle.pos.x, m.pos.z - p.vehicle.pos.z);
      if (d < 4 && p.vehicle.seats.some(s => !s)) this.vehicles.enter(m, p.vehicle, false);
      else if (d < 40) { m.ai.order = { x: p.vehicle.pos.x, z: p.vehicle.pos.z }; m.ai.orderT = this.time + 2; }
    }
  }

  // ---------- quick chat & pings ----------
  teamChat(from, text) {
    this.hud.chat(`<b class="${from.isPlayer ? 'me' : 'mate'}">${from.name}:</b> ${text}`);
    if (from.isPlayer) this.audio.ui('click');
  }
  quickChat(i) {
    const text = QUICK_CHAT[i]; if (!text) return;
    this.teamChat(this.player, text);
    const p = this.player.pos, mates = this.mates(this.player).filter(m => m.alive && !m.knocked);
    if (!mates.length) return;
    const m = mates[(Math.random() * mates.length) | 0];
    const reply = { 0: 'وين؟ حط علامة!', 1: 'جاي لعندك!', 2: 'جايين!', 3: '👍', 4: 'حلو، جاي آخذ.', 5: 'ماشيين للطوق.', 6: 'معك، بغطيك!', 7: 'جاي جاي!' }[i];
    setTimeout(() => m.alive && this.teamChat(m, reply), 700 + Math.random() * 900);
    if ([1, 2, 6, 7].includes(i)) for (const b of mates) { b.ai.order = { x: p.x, z: p.z }; b.ai.orderT = this.time + 25; }
  }
  ping() {
    const cam = this.camera.position, f = fwd(this.camYaw, this.camPitch);
    const d = this.world.raycast(cam.x, cam.y, cam.z, f.x, f.y, f.z, 700, 2);
    if (d === Infinity) { this.toast('بعيد كثير'); return; }
    const x = cam.x + f.x * d, z = cam.z + f.z * d, y = cam.y + f.y * d;
    const enemy = this.actors.find(a => a.alive && a.team !== this.player.team && a.state === 'ground' && Math.hypot(a.pos.x - x, a.pos.z - z) < 6);
    this.addPing(x, y, z, enemy ? 'enemy' : 'go', this.player);
    const dist = Math.round(Math.hypot(x - this.player.pos.x, z - this.player.pos.z));
    this.teamChat(this.player, enemy ? `🔴 عدو هون (${dist} م)` : `📍 روحوا لهون (${dist} م)`);
    if (!enemy) for (const b of this.mates(this.player)) if (b.alive && !b.knocked) { b.ai.order = { x, z }; b.ai.orderT = this.time + 30; }
  }
  addPing(x, y, z, kind, by) {
    for (const p of this.pings.filter(q => q.by === by)) this.removePing(p);
    const col = kind === 'enemy' ? 0xff3b3b : 0xffd34d;
    const g = new THREE.Group();
    const pole = new THREE.Mesh(new THREE.CylinderGeometry(0.06, 0.06, 6, 6), new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: 0.8, depthTest: false }));
    pole.position.y = 3; g.add(pole);
    const gem = new THREE.Mesh(new THREE.OctahedronGeometry(0.7), new THREE.MeshBasicMaterial({ color: col, depthTest: false }));
    gem.position.y = 6.6; g.add(gem);
    g.renderOrder = 10; g.position.set(x, y, z); this.scene.add(g);
    const p = { x, y, z, kind, by, mesh: g, gem, until: this.time + 25 };
    this.pings.push(p); this.audio.ui('pickup');
    return p;
  }
  removePing(p) { this.scene.remove(p.mesh); this.pings.splice(this.pings.indexOf(p), 1); }
  updatePings() {
    for (const p of [...this.pings]) {
      if (this.time > p.until) { this.removePing(p); continue; }
      p.gem.rotation.y += 0.05;
      const d = this.camera.position.distanceTo(p.mesh.position); p.mesh.scale.setScalar(Math.max(1, d / 40));
    }
  }
  callout(b, enemy) {
    if (this.time < (this.calloutT || 0) || b.team !== 0) return;
    this.calloutT = this.time + 9;
    const dx = enemy.pos.x - b.pos.x, dz = enemy.pos.z - b.pos.z, ang = (Math.atan2(dx, -dz) * 180 / Math.PI + 360) % 360;
    const dir = ['الشمال', 'الشمال الشرقي', 'الشرق', 'الجنوب الشرقي', 'الجنوب', 'الجنوب الغربي', 'الغرب', 'الشمال الغربي'][Math.round(ang / 45) % 8];
    this.teamChat(b, `🔴 عدو باتجاه ${dir} – ${Math.round(Math.hypot(dx, dz))} م`);
    this.addPing(enemy.pos.x, enemy.pos.y, enemy.pos.z, 'enemy', b).until = this.time + 8;
  }

  // ---------- backpack actions ----------
  dropFromBag(kind, key) {
    const p = this.player, put = it => this.loot.drop(it, p.pos.x, p.pos.z);
    if (p.state !== 'ground') return;
    if (kind === 'weapon') {
      const w = p.weapons[key]; if (!w) return;
      put({ kind: 'weapon', key: w.key, mag: w.mag, zoom: w.zoom, att: { ...w.att }, ammo: w.def.exclusive ? p.ammo[w.def.ammo] : undefined });
      if (w.def.exclusive) p.ammo[w.def.ammo] = 0;
      p.weapons[key] = null; if (p.cur === key && p.weapons[1 - key]) p.cur = 1 - key;
      if (p.action && p.action.type === 'reload') p.action = null;
    } else if (kind === 'attach') {
      const [i, slot] = key, w = p.weapons[i]; if (!w || !w.att[slot]) return;
      put({ kind: 'attach', key: w.att[slot] });
      if (w.att[slot] === 'extmag') { const over = w.mag - w.def.mag; if (over > 0) { w.mag -= over; p.ammo[w.def.ammo] += over; } }
      w.att[slot] = null;
    } else if (kind === 'scope') {
      const w = p.weapons[key]; if (!w || w.zoom <= 1) return;
      put({ kind: 'scope', zoom: w.zoom }); w.zoom = 1;
    } else if (kind === 'helmet' || kind === 'vest') {
      if (!p[kind].lvl) return; put({ kind, lvl: p[kind].lvl, dur: p[kind].dur }); p[kind] = { lvl: 0, dur: 0 };
    } else if (kind === 'pack') {
      if (!p.pack) return;
      if (p.load() > 50) { this.toast('فضّي الحقيبة أول – الحمل أكثر من 50'); return; }
      put({ kind: 'pack', lvl: p.pack }); p.pack = 0;
    } else if (kind === 'item') { if (!p.items[key]) return; put({ kind: 'item', key, n: p.items[key] }); p.items[key] = 0; }
    else if (kind === 'ammo') { if (!p.ammo[key]) return; put({ kind: 'ammo', key, n: p.ammo[key] }); p.ammo[key] = 0; }
    p.refreshLook(); this.audio.ui('click');
  }

  applySettings(S) {
    this.settings = S;
    if (this.input) this.input.apply(S);
    this.audio.setVolumes(S.audio);
    this.canvas.style.filter = S.graphics.brightness !== 100 ? `brightness(${S.graphics.brightness / 100})` : '';
    const root = document.documentElement.style;
    root.setProperty('--cross', S.controls.crossColor);
    root.setProperty('--hud-scale', S.controls.hudScale / 100);
    root.setProperty('--btn-op', S.controls.btnOpacity / 100);
    $('cross').dataset.style = S.controls.crossStyle;
    $('fireL').style.display = S.controls.leftFire ? '' : 'none';
    $('fps').style.display = S.graphics.showFps ? 'block' : 'none';
  }

  // ---------- training grounds ----------
  setupTraining() {
    const m = this.map, w = this.world, p = this.player;
    const sp = w.randomLandPoint(Math.random, m.cx, m.cz, 4);
    p.pos.set(sp.x, w.getHeight(sp.x, sp.z), sp.z); p.state = 'ground'; p.grounded = true; p.mesh.visible = true;
    this.camYaw = 0; this.camPitch = -0.05; // facing north, toward the targets
    this.spawnPoint = p.pos.clone();
    const row = [], add = it => row.push(it);
    for (const k in WEAPONS) add({ kind: 'weapon', key: k, mag: WEAPONS[k].mag, zoom: 1, ammo: WEAPONS[k].ammoWith });
    for (const k in AMMO) if (AMMO[k].pack) add({ kind: 'ammo', key: k, n: 240 });
    for (const z of [1.3, 2, 4, 8]) add({ kind: 'scope', zoom: z });
    add({ kind: 'helmet', lvl: 3, dur: ARMOR.helmet.dur[3] }); add({ kind: 'vest', lvl: 3, dur: ARMOR.vest.dur[3] }); add({ kind: 'pack', lvl: 3 });
    for (const k of ['firstaid', 'hospital', 'coffee', 'syringe', 'frag', 'slipper', 'flare']) add({ kind: 'item', key: k, n: 5 });
    this.trainingRack = row;
    this.restockTraining();
    this.dummies = [];
    [10, 25, 50, 75, 100, 150, 200].forEach((d, i) => {
      for (const side of [-1, 1]) {
        const b = new Actor(this, this.actors.length, `${d}m`, false);
        initBot(b, 0); b.ai.dummy = true; b.ai.strafe = side === 1 && i % 2 === 0;
        const x = sp.x + side * (4 + i * 1.5), z = sp.z - d;
        b.home = { x, z }; b.pos.set(x, w.getHeight(x, z), z); b.state = 'ground'; b.grounded = true; b.yaw = Math.PI;
        if (i % 3 === 1) { b.armored = true; b.helmet = { lvl: 2, dur: ARMOR.helmet.dur[2] }; b.vest = { lvl: 2, dur: ARMOR.vest.dur[2] }; b.refreshLook(); }
        this.actors.push(b); this.dummies.push(b);
      }
    });
  }
  restockTraining() {
    const sp = this.spawnPoint;
    for (const e of this.loot.items) if (e.alive && e.training) this.loot.remove(e);
    this.trainingRack.forEach((it, i) => {
      const col = i % 8, r = Math.floor(i / 8), x = sp.x - 7 + col * 2, z = sp.z + 3 + r * 1.6;
      const e = this.loot.add(JSON.parse(JSON.stringify(it)), x, this.world.getHeight(x, z), z);
      if (e) e.training = true;
    });
  }

  setupSky() {
    const W = {
      clear:     { top: 0x3f86d8, bot: 0xcfe3f2, fog: 0xc6dcee, sun: 0xfff1d6, si: 2.6, hemi: 1.1, near: 120, far: 1, elev: 0.9 },
      sunset:    { top: 0x2c3a78, bot: 0xffa060, fog: 0xe8a77a, sun: 0xffa060, si: 2.0, hemi: 0.8, near: 100, far: 0.9, elev: 0.18 },
      fog:       { top: 0x9aa3ab, bot: 0xc4c8cb, fog: 0xbfc3c6, sun: 0xffffff, si: 1.2, hemi: 1.2, near: 8, far: 0.22, elev: 0.8 },
      rain:      { top: 0x4c5560, bot: 0x8a949e, fog: 0x7f8a94, sun: 0xc8d0d8, si: 0.9, hemi: 1.0, near: 30, far: 0.45, elev: 0.8 },
      snow:      { top: 0x9fb2c6, bot: 0xe6edf3, fog: 0xdfe6ee, sun: 0xffffff, si: 1.6, hemi: 1.2, near: 30, far: 0.45, elev: 0.7 },
      khamaseen: { top: 0xb07a3a, bot: 0xd9a560, fog: 0xc8964f, sun: 0xffc080, si: 1.2, hemi: 1.0, near: 10, far: 0.18, elev: 0.7 },
    }[this.weather.id];
    this.sky = W;
    const far = this.preset.far * W.far;
    this.scene.fog = new THREE.Fog(W.fog, W.near, Math.max(110, far));
    this.scene.background = new THREE.Color(W.fog);
    const geo = new THREE.SphereGeometry(2400, 24, 12), cols = [], top = new THREE.Color(W.top), bot = new THREE.Color(W.bot), c = new THREE.Color();
    const pos = geo.attributes.position;
    for (let i = 0; i < pos.count; i++) { const t = clamp(pos.getY(i) / 2400 * 1.6, 0, 1); c.copy(bot).lerp(top, t); cols.push(c.r, c.g, c.b); }
    geo.setAttribute('color', new THREE.Float32BufferAttribute(cols, 3));
    this.skyMesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ vertexColors: true, side: THREE.BackSide, fog: false, depthWrite: false }));
    this.scene.add(this.skyMesh);
    this.scene.add(new THREE.HemisphereLight(W.top, 0x8a7a5a, W.hemi));
    const sun = this.sun = new THREE.DirectionalLight(W.sun, W.si);
    this.sunOff = new THREE.Vector3(Math.cos(0.8) * 150, 220 * W.elev + 30, Math.sin(0.8) * 150);
    if (this.preset.shadows) {
      sun.castShadow = true;
      sun.shadow.mapSize.set(this.preset.shadows, this.preset.shadows);
      const s = sun.shadow.camera; s.left = -70; s.right = 70; s.top = 70; s.bottom = -70; s.near = 1; s.far = 600;
      sun.shadow.bias = -0.0008; sun.shadow.normalBias = 0.4;
    }
    this.scene.add(sun); this.scene.add(sun.target);

    // sun disc and drifting clouds (sprites, cheap)
    const disc = document.createElement('canvas'); disc.width = disc.height = 128;
    const dg = disc.getContext('2d'), gr = dg.createRadialGradient(64, 64, 0, 64, 64, 64);
    gr.addColorStop(0, 'rgba(255,250,235,1)'); gr.addColorStop(0.18, 'rgba(255,240,200,0.95)'); gr.addColorStop(0.4, 'rgba(255,220,160,0.25)'); gr.addColorStop(1, 'rgba(255,220,160,0)');
    dg.fillStyle = gr; dg.fillRect(0, 0, 128, 128);
    this.sunSprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: new THREE.CanvasTexture(disc), fog: false, depthWrite: false, transparent: true, color: W.sun }));
    this.sunSprite.scale.setScalar(260); this.scene.add(this.sunSprite);
    const cc = document.createElement('canvas'); cc.width = 256; cc.height = 128;
    const cg = cc.getContext('2d');
    for (let i = 0; i < 18; i++) {
      const x = 40 + Math.random() * 176, y = 50 + Math.random() * 40, r = 18 + Math.random() * 34, g2 = cg.createRadialGradient(x, y, 0, x, y, r);
      g2.addColorStop(0, 'rgba(255,255,255,0.85)'); g2.addColorStop(1, 'rgba(255,255,255,0)'); cg.fillStyle = g2; cg.fillRect(0, 0, 256, 128);
    }
    const cloudTex = new THREE.CanvasTexture(cc);
    const cloudy = { clear: 26, sunset: 22, fog: 10, rain: 40, snow: 36, khamaseen: 8 }[this.weather.id] || 20;
    this.clouds = [];
    for (let i = 0; i < cloudy; i++) {
      const s = new THREE.Sprite(new THREE.SpriteMaterial({ map: cloudTex, fog: false, depthWrite: false, transparent: true, opacity: 0.55 + Math.random() * 0.35, color: this.weather.id === 'rain' ? 0x8a929a : 0xffffff }));
      s.position.set((Math.random() * 2 - 1) * 1300, 240 + Math.random() * 160, (Math.random() * 2 - 1) * 1300);
      s.scale.set(260 + Math.random() * 260, 110 + Math.random() * 90, 1);
      this.scene.add(s); this.clouds.push(s);
    }
  }

  bindHudButtons() {
    $('mapbtn').onclick = () => this.toggleMap();
    $('bigmapwrap').onclick = () => this.toggleMap(false);
    $('again').onclick = () => { location.search = '?autostart=1&map=' + this.map.id; };
    $('menuBtn').onclick = () => { location.search = ''; };
  }

  // ---------- bots drop plan ----------
  planBotJumps() {
    const m = this.map, inside = this.world.zones.filter(z => Math.hypot(z.x - m.cx, z.z - m.cz) < m.radius * 0.85);
    const zones = inside.length ? inside : this.world.zones;
    for (const b of this.actors) {
      if (b.isPlayer) continue;
      const cands = zones.map(z => ({ z, w: (z.minor ? 0.6 : z.tier === 3 ? 3 : z.tier) / (1 + Math.max(0, this.plane.perpDist(z) - 150) / 80) }));
      const z = pickWeighted(cands).z;
      const p = this.world.randomLandPoint(Math.random, z.x, z.z, z.br);
      b.ai.landing = p;
      b.ai.jumpAt = clamp(this.plane.along(p) - 120 - Math.random() * 160, 60 + Math.random() * 100, this.plane.len - 80);
      b.ai.openAt = 70 + Math.random() * 60;
    }
  }

  jump(a) {
    a.state = 'fall'; a.pos.copy(this.plane.pos); a.pos.y -= 4; a.vy = 0;
    a.mesh.visible = true;
    if (a.isPlayer) { this.audio.ui('jump'); this.audio.stopPlane(); }
  }

  updateAir(a, dt, sx, sz, open) {
    const g = this.world.getHeight(a.pos.x, a.pos.z), lvl = this.world.waterLevel(a.pos.x, a.pos.z);
    const floor = Math.max(g, lvl - 1);
    const agl = a.pos.y - floor;
    const autoAt = a.isPlayer && !this.settings.controls.autoOpenChute ? 25 : 85;
    if (a.state === 'fall' && (agl < autoAt || (open && agl < 260))) a.state = 'chute';
    const hs = a.state === 'fall' ? 30 : 13, vs = a.state === 'fall' ? 50 : 7;
    const l = Math.hypot(sx, sz); if (l > 1) { sx /= l; sz /= l; }
    a.pos.x = clamp(a.pos.x + sx * hs * dt, -HALF + 5, HALF - 5);
    a.pos.z = clamp(a.pos.z + sz * hs * dt, -HALF + 5, HALF - 5);
    a.pos.y -= vs * dt;
    if (sx || sz) a.yaw = Math.atan2(-sx, -sz);
    if (a.pos.y <= floor + 0.05) {
      a.pos.y = floor; a.state = 'ground'; a.grounded = true;
      if (a.isPlayer) this.sara('You landed. Loot fast – <b>hospitals</b> (red sign) have healing, <b>police</b> (blue) have weapons, <b>shops</b> (green) have bags. Press <b>E</b> to pick up.', 8);
    }
  }

  // ---------- main loop ----------
  loop(t) {
    requestAnimationFrame(t2 => this.loop(t2));
    const cap = this.settings.graphics.fps;
    if (cap && t - this.last < 1000 / cap - 1.5) return;
    const dt = Math.min(0.05, (t - this.last) / 1000); this.last = t;
    this.fpsN++; this.fpsT += dt;
    if (this.fpsT >= 0.5) { $('fps').textContent = Math.round(this.fpsN / this.fpsT) + ' FPS'; this.fpsN = 0; this.fpsT = 0; }
    if (!this.over || this.spectate) this.update(dt);
    else if (this.phase === 'island') this.update(dt);
    this.renderer.render(this.scene, this.camera);
    this.input.endFrame();
  }

  update(dt) {
    this.time += dt;
    const inp = this.input;
    if (inp.hit('KeyM') || inp.hit('Tab')) this.toggleMap();
    if (this.phase === 'island') {
      this.updateIsland(dt);
      this.updatePlayer(dt);
      this.player.tickStatus(dt); this.player.updateMesh(dt);
      this.vehicles.update(dt, this.vehicleCtrl || {});
      for (const a of this.actors) if (a.vehicle) a.updateMesh(dt);
      this.effects.update(dt); this.weatherFX.update(dt, this.camera);
      this.updateCamera(dt); this.updatePings(); this.hud.update(dt);
      return;
    }
    if (this.plane && this.plane.active) {
      this.plane.update(dt);
      for (const a of this.actors) if (a.state === 'plane') {
        a.pos.copy(this.plane.pos);
        if (!a.isPlayer && (a.ai.mate ? this.player.state !== 'plane' && this.time >= (a.ai.mateJumpAt ??= this.time + 0.4 + a.id % 4 * 0.35) : this.plane.t >= a.ai.jumpAt)) this.jump(a);
      }
      if (this.player.state === 'plane' && (inp.hit('Space') || this.plane.t > this.plane.len - 60)) this.jump(this.player);
      if (!this.plane.active) for (const a of this.actors) if (a.state === 'plane') this.jump(a);
    }
    this.updatePlayer(dt);
    this.vehicles.update(dt, this.vehicleCtrl || {});
    this.updateMates();
    for (const a of this.actors) {
      if (!a.alive) continue;
      if (!a.isPlayer) {
        if (a.state === 'fall' || a.state === 'chute') {
          let L = a.ai.landing;
          if (a.ai.mate) { const P = this.player, k = a.id % 4 * 1.7; L = { x: P.pos.x + Math.cos(k) * 9, z: P.pos.z + Math.sin(k) * 9 }; }
          const dx = L.x - a.pos.x, dz = L.z - a.pos.z, d = Math.hypot(dx, dz);
          const agl = a.pos.y - this.world.getHeight(a.pos.x, a.pos.z);
          this.updateAir(a, dt, d > 3 ? dx / d : 0, d > 3 ? dz / d : 0, agl < a.ai.openAt + 40);
        } else if (a.state === 'ground') {
          if (a.ai.dummy) { if (a.ai.strafe) { const t = this.time * 0.9 + a.id; a.move(Math.cos(t) > 0 ? 1 : -1, 0, dt, false, false); a.yaw = Math.PI; } }
          else updateBot(this, a, dt);
        }
      }
      if (a.state === 'ground' || a.state === 'vehicle') a.tickStatus(dt);
      a.updateMesh(dt);
    }
    // Al-Tawq damage (ignores armor)
    this.ring.update(dt);
    const dps = this.ring.dps;
    for (const a of this.actors) if (a.alive && (a.state === 'ground' || a.state === 'vehicle') && !this.ring.inside(a.pos.x, a.pos.z)) a.takeDamage(dps * dt, 'body', null, 'Al-Tawq', true);
    if (!this.map.training) this.redzone.update(dt);
    this.heat.update(dt, this.time);
    if (!this.map.training) this.updateAirdrops(dt);
    this.updateLandmarks(dt);
    this.updatePings();
    this.updateProjectiles(dt);
    this.updateCrates(dt);
    this.effects.update(dt);
    this.weatherFX.update(dt, this.camera);
    this.updateCamera(dt);
    this.updateZoneName();
    this.hud.update(dt);
  }

  // ---------- player ----------
  updatePlayer(dt) {
    const p = this.player, inp = this.input;
    const zoom = this.scoped ? p.weapon.zoom : 1, sv = this.settings.sens;
    let pct = sv.camera;
    if (p.state === 'plane' || p.state === 'fall' || p.state === 'chute') pct = sv.para;
    else if (p.state === 'vehicle') pct = sv.vehicle ?? sv.camera;
    else if (this.scoped) pct = zoom >= 8 ? sv.x8 : zoom >= 4 ? sv.x4 : sv.x2;
    else if (this.aiming) pct = p.weapon && p.weapon.zoom === 1.3 ? sv.reddot : sv.ads;
    const k = 0.0022 * pct / 100 / zoom, inv = sv.invertY ? -1 : 1;
    let gx = 0, gy = 0;
    if (sv.gyro === 'always' || (sv.gyro === 'scope' && this.aiming)) { const gk = sv.gyroSens / 100; gx = inp.gx * gk; gy = inp.gy * gk; }
    this.camYaw -= (inp.mdx + gx) * k; this.camPitch = clamp(this.camPitch - (inp.mdy + gy) * k * inv, -1.25, 1.15);
    this.prompt = '';
    if (!p.alive) { this.scoped = false; return; }
    const ax = inp.axes();
    const f = fwd(this.camYaw, 0), right = new THREE.Vector3(Math.cos(this.camYaw), 0, -Math.sin(this.camYaw));
    const mx = f.x * ax.y + right.x * ax.x, mz = f.z * ax.y + right.z * ax.x;
    if (p.state === 'plane') { this.prompt = '<b>SPACE</b> Jump'; return; }
    if (p.state === 'fall' || p.state === 'chute') {
      this.updateAir(p, dt, mx, mz, inp.hit('Space'));
      if (p.state === 'fall') this.prompt = `<b>SPACE</b> Open parachute · ${Math.round(p.pos.y - this.world.getHeight(p.pos.x, p.pos.z))} m`;
      return;
    }
    this.vehicleCtrl = null;
    if (p.state === 'vehicle') {
      const v = p.vehicle;
      if (p.seat === 0) this.vehicleCtrl = { throttle: ax.y, steer: ax.x, brake: inp.down('Space'), boost: inp.sprint() };
      this.prompt = `<b>F</b> نزول · ${VEHICLES[v.type].ar}${p.seat === 0 ? '' : ' (راكب)'}`;
      if (inp.hit('KeyF')) { this.vehicles.exit(p); this.mateExit(v); this.audio.stopEngine(); }
      if (inp.hit('KeyM') && false) this.toggleMap();
      return;
    }
    if (p.state !== 'ground') return;
    if (inp.hit('KeyI')) this.hud.toggleBag();
    if (inp.hit('KeyZ')) this.hud.toggleChat();
    if (inp.hit('KeyV') || inp.hit('Ping')) this.ping();
    if (p.knocked) {
      this.scoped = false; this.aiming = false;
      p.yaw = this.camYaw; p.move(mx, mz, dt, false, false);
      this.prompt = `<span class="warn">انصبت</span> · ${Math.ceil(p.hp)} · زحف للغطا`;
      return;
    }
    const aiming = inp.aimToggle && !!p.weapon && !p.swimming;
    this.scoped = aiming && p.weapon.zoom >= 2;
    this.aiming = aiming;
    p.yaw = this.camYaw; p.pitch = this.camPitch;
    const sprint = inp.sprint() && ax.y > 0 && !inp.fire && !aiming;
    p.move(mx * (aiming ? 0.6 : 1), mz * (aiming ? 0.6 : 1), dt, sprint, inp.hit('Space'));
    this.bloom = Math.max(0, this.bloom - dt * 2.5);

    // weapons
    if (inp.hit('Digit1') || inp.hit('Digit2') || inp.hit('Swap') || inp.hit('KeyX')) {
      const s = inp.hit('Digit1') ? 0 : inp.hit('Digit2') ? 1 : 1 - p.cur;
      if (p.weapons[s] && s !== p.cur) { p.cur = s; if (p.action && p.action.type === 'reload') p.action = null; p.refreshLook(); p.fireCd = 0.35; this.audio.ui('click'); }
    }
    if (inp.hit('KeyR')) p.startReload();
    const w = p.weapon;
    if (w && w.mag <= 0 && !p.action && this.settings.controls.autoReload && p.ammo[w.def.ammo] > 0) p.startReload();
    if (this.phase === 'island') { this.prompt = `الطيارة بتقلع بعد <b>${Math.ceil(this.island.t)}</b> ثانية`; return; }
    if (this.settings.controls.autoPickup) this.autoPickup(p);
    // get into a vehicle
    const car = !p.knocked && this.vehicles.near(p.pos.x, p.pos.z, 2.2);
    if (car && !this.prompt) this.prompt = `<b>F</b> ركوب ${VEHICLES[car.type].ar}`;
    if (car && inp.hit('KeyF')) { this.vehicles.enter(p, car); this.audio.startEngine(); this.mateBoard(car); this.camYaw = car.yaw; return; }
    // revive a knocked teammate
    const down = this.mates(p).find(m => m.alive && m.knocked && Math.hypot(m.pos.x - p.pos.x, m.pos.z - p.pos.z) < 2.2);
    if (down) {
      if (p.action && p.action.type === 'revive') { this.prompt = `بتنعش <b>${down.name}</b>…`; return; }
      this.prompt = `<b>E</b> إنعاش <b>${down.name}</b>`;
      if (inp.hit('KeyE')) { p.action = { type: 'revive', target: down, t: 7, total: 7 }; }
      return;
    }
    if (this.map.training && inp.hit('KeyU')) { this.restockTraining(); this.toast('تم تعبئة الرف'); }
    if (inp.fire && w) {
      if (w.mag <= 0) { if (!this.dryClick) { this.audio.ui('click'); this.dryClick = true; } p.startReload(); }
      else if (p.canFire() && (w.def.auto || !this.firedHeld)) { this.playerFire(); this.firedHeld = true; }
    }
    if (!inp.fire) { this.firedHeld = false; this.dryClick = false; }
    if (inp.hit('KeyH')) { const h = p.bestHeal(); if (h) p.startUse(h); else this.toast(p.hp >= 75 ? 'Use a Hospital Kit to heal above 75' : 'No healing items'); }
    if (inp.hit('KeyB')) { const b = p.bestBoost(); if (b) p.startUse(b); else this.toast('No boosts'); }
    if (inp.hit('KeyT')) { const order = ['frag', 'slipper', 'flare']; p.throwSel = order[(order.indexOf(p.throwSel) + 1) % 3]; }
    if (inp.hit('KeyG')) this.throwItem(p, p.throwSel);
    if (inp.hit('KeyQ')) this.eyeOfTheGuide();

    // interaction
    const e = this.loot.near(p.pos.x, p.pos.z, 2.4);
    const c = this.loot.nearCrate(p.pos.x, p.pos.z, 2.8);
    if (c && (!e || Math.hypot(c.x - p.pos.x, c.z - p.pos.z) < Math.hypot(e.x - p.pos.x, e.z - p.pos.z))) {
      this.prompt = `<b>E</b> Loot ${c.kind === 'death' ? c.label + "'s crate" : c.kind === 'flare' ? 'Flare crate' : 'Aid Crate'} (${c.items.length})`;
      if (inp.hit('KeyE')) { const n = this.loot.lootAll(p, c); this.audio.ui('pickup'); if (!n) this.toast('Nothing useful / bag full'); }
    } else if (e) {
      this.prompt = `<b>E</b> Pick up <span style="color:${RARITY_COLORS[e.rar]}">${itemName(e.it)}</span>`;
      if (inp.hit('KeyE')) {
        const res = this.loot.take(p, e);
        if (res.taken || res.partial) this.audio.ui('pickup');
        else this.toast(e.it.kind === 'scope' ? 'No weapon that fits this scope (or already better)' : e.it.kind === 'helmet' || e.it.kind === 'vest' || e.it.kind === 'pack' ? 'You already have equal or better' : 'Backpack full');
      }
    }
  }

  autoPickup(p) {
    this.apT = (this.apT || 0) - 1;
    if (this.apT > 0) return; this.apT = 6;
    const e = this.loot.near(p.pos.x, p.pos.z, 1.6, e => e.it.kind === 'ammo' ? p.weapons.some(w => w && w.def.ammo === e.it.key) : e.it.kind === 'item');
    if (!e) return;
    const res = this.loot.take(p, e);
    if (res.taken || res.partial) this.audio.ui('pickup');
  }

  updateLandmarks(dt) {
    this.lmT = (this.lmT || 0) - dt;
    if (this.lmT > 0) return; this.lmT = 0.5;
    const p = this.player;
    if (p.state !== 'ground' && p.state !== 'chute') return;
    const l = this.world.landmarkNear(p.pos.x, p.pos.z, 38);
    if (!l || this.seenLandmarks.has(l.id)) return;
    this.seenLandmarks.add(l.id);
    this.announce(`◆ ${l.ar}`, l.name, 'lm');
    this.sara(`<b>${l.ar}</b>: ${l.info}`, 7);
  }

  currentSpread() {
    const p = this.player, w = p.weapon;
    if (!w) return 1;
    let s = (this.aiming ? w.def.ads : w.def.spread) * spreadMul(w);
    if (p.moving) s *= this.aiming ? 1.5 : 1.5;
    if (!p.grounded) s *= 2.5;
    return s + this.bloom;
  }

  playerFire() {
    const p = this.player, w = p.weapon;
    const spread = this.currentSpread() * Math.PI / 180;
    const cam = this.camera.position.clone();
    const dir = fwd(this.camYaw, this.camPitch);
    const a = Math.random() * Math.PI * 2, r = Math.sqrt(Math.random()) * spread;
    const right = new THREE.Vector3(Math.cos(this.camYaw), 0, -Math.sin(this.camYaw)), up = new THREE.Vector3().crossVectors(right, dir);
    if (!w.def.pellets) dir.addScaledVector(right, Math.cos(a) * r).addScaledVector(up, Math.sin(a) * r).normalize();
    const eye = p.eye();
    const tStart = Math.max(0, eye.clone().sub(cam).dot(dir));
    this.fire(p, dir, cam, tStart);
    // recoil
    const rc = w.def.recoil * recoilMul(w);
    this.camPitch = clamp(this.camPitch + rc * (this.aiming ? 0.55 : 1) * Math.PI / 180, -1.25, 1.15);
    this.camYaw += (Math.random() - 0.5) * rc * 0.4 * Math.PI / 180;
    this.bloom = Math.min(3, this.bloom + (w.def.auto ? 0.18 : 0.5));
    this.shake = Math.max(this.shake, w.def.cls === 'SNIPER' ? 0.25 : 0.08);
  }

  // shared hitscan for everyone
  fire(s, dir, origin = null, tStart = 0) {
    const w = s.weapon, def = w.def;
    if (!origin) origin = s.eye();
    w.mag--; s.fireCd = 60 / def.rpm;
    if (s.action && s.action.type === 'heal') s.action = null;
    const cam = this.camera.position, dCam = Math.hypot(s.pos.x - cam.x, s.pos.z - cam.z);
    const muzzle = s.gunMesh.getWorldPosition(new THREE.Vector3()).addScaledVector(fwd(s.yaw, s.pitch), 0.8);
    const pellets = def.pellets || 1;
    const right = new THREE.Vector3(dir.z, 0, -dir.x).normalize(), up = new THREE.Vector3().crossVectors(right, dir);
    for (let i = 0; i < pellets; i++) {
      const d = new THREE.Vector3(dir.x, dir.y, dir.z);
      if (pellets > 1) { const a = Math.random() * 6.283, r = Math.sqrt(Math.random()) * def.spread * Math.PI / 180; d.addScaledVector(right, Math.cos(a) * r).addScaledVector(up, Math.sin(a) * r).normalize(); }
      const o = new THREE.Vector3(origin.x, origin.y, origin.z).addScaledVector(d, tStart);
      const maxD = def.range;
      let ht = this.world.raycast(o.x, o.y, o.z, d.x, d.y, d.z, maxD, 2);
      let hitA = null, part = null, hitV = null;
      const vh = this.vehicles.rayHit(o, d, Math.min(ht, maxD));
      if (vh) { ht = vh.t; hitV = vh.v; }
      const lim = Math.min(ht, maxD);
      for (const a of this.actors) {
        if (a === s || !a.alive || a.state === 'plane' || (a.team === s.team && this.teamSize > 1) || (s.vehicle && a.vehicle === s.vehicle)) continue;
        const ex = a.pos.x - o.x, ez = a.pos.z - o.z;
        if (Math.abs(ex) > lim + 2 || Math.abs(ez) > lim + 2) continue;
        const r = rayActor(o.x, o.y, o.z, d.x, d.y, d.z, a, Math.min(ht, maxD));
        if (r) { ht = r.t; hitA = a; part = r.part; }
      }
      const end = o.clone().addScaledVector(d, Math.min(ht, maxD));
      if (hitA) {
        const fall = ht > def.range * 0.6 ? 1 - 0.4 * (ht - def.range * 0.6) / (def.range * 0.4) : 1;
        const dmg = def.dmg * (part === 'head' ? (def.head || 2.1) : 1) * fall;
        if (dCam < 350) this.effects.impact(end, true);
        hitA.takeDamage(dmg, part, s, def.name);
      } else if (hitV && hitV.driver !== s) { hitV.damage(def.dmg * 0.55, s); if (dCam < 300) this.effects.impact(end, false); }
      else if (ht < maxD && dCam < 250) this.effects.impact(end, false);
      if (dCam < 400 && (i === 0 || pellets < 4 || i % 3 === 0) && !(suppressed(w) && s !== this.player)) this.effects.tracer(muzzle, end, def.exclusive ? 0xffb020 : 0xffe08a);
    }
    const quiet = suppressed(w);
    if (dCam < 400 && !quiet) this.effects.muzzle(muzzle);
    const L = this.listener();
    this.audio.shot(def.cls, Math.hypot(s.pos.x - L.x, s.pos.z - L.z) * (quiet ? 3.5 : 1), this.pan(s.pos));
    this.heat.add(s.pos.x, s.pos.z, 1, this.time);
    hearShot(this, s);
  }

  listener() { return this.player.alive ? this.player.pos : this.camera.position; }
  pan(p) {
    const L = this.listener(), dx = p.x - L.x, dz = p.z - L.z, d = Math.hypot(dx, dz) || 1;
    const right = new THREE.Vector3(Math.cos(this.camYaw), 0, -Math.sin(this.camYaw));
    return (dx * right.x + dz * right.z) / d * 0.8;
  }

  explode(x, y, z, radius, dmg, attacker, cause, shelter = false) {
    const pos = new THREE.Vector3(x, y, z), L = this.listener(), d0 = Math.hypot(x - L.x, z - L.z);
    if (Math.hypot(x - this.camera.position.x, z - this.camera.position.z) < 600) this.effects.explosion(pos, radius / 7);
    this.audio.explosion(d0, this.pan(pos));
    if (d0 < radius * 4) this.shake = Math.max(this.shake, 0.6 * (1 - d0 / (radius * 4)));
    for (const a of this.actors) {
      if (!a.alive || (a.state !== 'ground' && a.state !== 'vehicle')) continue;
      if (attacker && a !== attacker && a.team === attacker.team && this.teamSize > 1) continue;
      const d = Math.hypot(a.pos.x - x, a.pos.y + 1 - y, a.pos.z - z);
      if (d > radius) continue;
      if (shelter && this.world.insideBuilding(a.pos.x, a.pos.z, a.pos.y + 1)) continue; // GDD §4B: buildings protect
      const e = new THREE.Vector3(a.pos.x - x, a.pos.y + 1 - (y + 0.6), a.pos.z - z), dl = e.length();
      if (dl > 0.5 && this.world.raycast(x, y + 0.6, z, e.x / dl, e.y / dl, e.z / dl, dl - 0.4, 1) !== Infinity) continue;
      a.takeDamage(dmg * Math.pow(1 - d / radius, 0.6), 'body', attacker, cause);
    }
    if (this.vehicles) this.vehicles.blast(x, y, z, radius, dmg, attacker);
    if (attacker) this.heat.add(x, z, 3, this.time);
  }

  // ---------- throwables ----------
  throwItem(a, key) {
    if (!a.items[key] || a.state !== 'ground') { if (a.isPlayer) this.toast(`No ${ITEMS[key].name}`); return; }
    a.items[key]--;
    const dir = a.isPlayer ? fwd(this.camYaw, this.camPitch + 0.18) : fwd(a.yaw, 0.35);
    const pos = a.eye().addScaledVector(dir, 0.6);
    const isFlare = key === 'flare';
    const vel = isFlare ? new THREE.Vector3(dir.x * 6, 48, dir.z * 6) : dir.clone().multiplyScalar(key === 'slipper' ? 24 : 20);
    const col = { frag: 0x3a4a2a, slipper: 0x2a6fd8, flare: 0xff3030 }[key];
    const mesh = new THREE.Mesh(key === 'slipper' ? new THREE.BoxGeometry(0.12, 0.05, 0.3) : new THREE.SphereGeometry(0.12, 8, 6), new THREE.MeshLambertMaterial({ color: col, emissive: isFlare ? 0xff2020 : 0 }));
    mesh.position.copy(pos); this.scene.add(mesh);
    this.projectiles.push({ key, pos, vel, t: 0, owner: a, mesh, landed: false, inZone: this.ring.inside(a.pos.x, a.pos.z) });
    if (key === 'flare') this.audio.tone(200, 0.6, 0.3, 'sawtooth', 4);
    else this.audio.burst(0.2, 0.2, 1500, 1);
  }

  updateProjectiles(dt) {
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const pr = this.projectiles[i];
      pr.t += dt;
      if (pr.key === 'flare') {
        pr.vel.y -= 9 * dt; pr.pos.addScaledVector(pr.vel, dt);
        this.effects.flare(pr.pos);
        if (pr.vel.y < 0) { this.flareBurst(pr); this.scene.remove(pr.mesh); this.projectiles.splice(i, 1); }
        else pr.mesh.position.copy(pr.pos);
        continue;
      }
      if (!pr.landed) {
        pr.vel.y -= 16 * dt;
        const step = pr.vel.clone().multiplyScalar(dt), sl = step.length();
        if (sl > 0) {
          const hd = this.world.raycast(pr.pos.x, pr.pos.y, pr.pos.z, step.x / sl, step.y / sl, step.z / sl, sl, 0.5);
          if (hd !== Infinity) {
            const g = this.world.getHeight(pr.pos.x, pr.pos.z);
            if (pr.pos.y + step.y <= g + 0.15) { pr.vel.y *= -0.3; pr.vel.x *= 0.5; pr.vel.z *= 0.5; }
            else { pr.vel.x *= -0.35; pr.vel.z *= -0.35; }
            if (pr.key === 'slipper') this.slipperLand(pr);
          } else pr.pos.add(step);
        }
        // direct slipper hit on an actor: no damage, all embarrassment
        if (pr.key === 'slipper' && !pr.landed) for (const a of this.actors) {
          if (a === pr.owner || !a.alive || a.state !== 'ground') continue;
          if (Math.hypot(a.pos.x - pr.pos.x, a.pos.z - pr.pos.z) < 0.5 && pr.pos.y > a.pos.y && pr.pos.y < a.pos.y + 1.9) {
            this.hud.killfeed(`<b>${pr.owner.name}</b> 🩴 <b>${a.name}</b> <i>(direct hit!)</i>`); this.slipperLand(pr); break;
          }
        }
        const g = this.world.getHeight(pr.pos.x, pr.pos.z);
        if (pr.pos.y < g + 0.12) { pr.pos.y = g + 0.12; if (Math.abs(pr.vel.y) < 1.5 && Math.hypot(pr.vel.x, pr.vel.z) < 1.5) { pr.landed = true; if (pr.key === 'slipper') this.slipperLand(pr); } }
        pr.mesh.position.copy(pr.pos);
        if (pr.key === 'slipper') pr.mesh.rotation.x += dt * 20;
      }
      if (pr.key === 'frag' && pr.t > 3.6) { this.explode(pr.pos.x, pr.pos.y, pr.pos.z, 8, 110, pr.owner, 'Frag Grenade'); this.scene.remove(pr.mesh); this.projectiles.splice(i, 1); }
      else if (pr.key === 'slipper' && pr.t > 8) { this.scene.remove(pr.mesh); this.projectiles.splice(i, 1); }
    }
  }
  slipperLand(pr) {
    if (pr.distracted) return; pr.distracted = true; pr.landed = true;
    const L = this.listener();
    this.audio.slipper(Math.hypot(pr.pos.x - L.x, pr.pos.z - L.z), this.pan(pr.pos));
    distract(this, pr.pos.x, pr.pos.z);
  }

  // GDD §6: flare inside the ring → special crate; outside → Jawad armored vehicle
  flareBurst(pr) {
    const x = pr.owner.pos.x, z = pr.owner.pos.z;
    for (let k = 0; k < 30; k++) setTimeout(() => this.effects.flare(new THREE.Vector3(x, pr.pos.y - k * 0.8, z)), k * 120);
    this.heat.add(x, z, 2, this.time);
    if (pr.inZone) {
      this.callDrop(x, z, 'flare');
      this.announce('🎆 Flare fired – a special crate is coming!', 'طلقة إشارة');
    } else {
      this.announce('🎆 Flare fired outside Al-Tawq – the "Jawad" armored vehicle arrives with the vehicles update', 'جواد');
    }
  }

  // ---------- airdrops ----------
  callDrop(x, z, kind = 'air') {
    const plane = new Plane(this, 240, 95, { x, z });
    const s = plane.along({ x, z });
    this.drops.push({ plane, x, z, s, kind, dropped: false });
  }
  updateAirdrops(dt) {
    this.airdropT -= dt;
    if (this.airdropT <= 0 && this.ring.phase < 7) {
      this.airdropT = AIRDROP_INTERVAL[0] + Math.random() * (AIRDROP_INTERVAL[1] - AIRDROP_INTERVAL[0]);
      const c = this.ring.cur;
      let p = null;
      for (let i = 0; i < 30 && !p; i++) { const q = this.world.randomLandPoint(Math.random, c.x, c.z, c.r * 0.8); if (!this.world.insideBuilding(q.x, q.z, 1000)) p = q; }
      if (p) {
        this.callDrop(p.x, p.z, 'air');
        this.announce('📦 Aid Crate incoming – look for the red smoke!', 'صندوق المعونة');
        this.sara('Aid Crate! It carries the <b>Mountain Rifle</b> or <b>Al-Haddar</b> – the strongest weapons in KRYVON. Everyone saw it too…', 7);
      }
    }
    for (let i = this.drops.length - 1; i >= 0; i--) {
      const d = this.drops[i];
      if (d.plane.active) d.plane.update(dt);
      if (!d.dropped && d.plane.t >= d.s) {
        d.dropped = true;
        const c = this.loot.addCrate(d.x, d.z, airdropItems(d.kind === 'flare'), d.kind, '');
        c.landed = false; c.mesh.position.y = d.plane.pos.y - 6;
        const chute = new THREE.Mesh(new THREE.SphereGeometry(2.6, 14, 6, 0, 6.283, 0, 1.2), new THREE.MeshLambertMaterial({ color: d.kind === 'flare' ? 0xd02020 : 0x2f6fb0, side: THREE.DoubleSide }));
        chute.position.y = 4.5; c.mesh.add(chute); c.chute = chute;
      }
      if (!d.plane.active && d.dropped) this.drops.splice(i, 1);
    }
  }
  updateCrates(dt) {
    for (const c of this.loot.crates) {
      if (c.kind === 'death') continue;
      if (!c.landed) {
        c.mesh.position.y -= 7 * dt;
        if (c.mesh.position.y <= c.y) { c.mesh.position.y = c.y; c.landed = true; c.mesh.remove(c.chute); c.smokeLeft = 70; }
      } else if (c.smokeLeft > 0) {
        c.smokeLeft -= dt; c.smokeT -= dt;
        if (c.smokeT <= 0) { c.smokeT = 0.18; this.effects.smoke(c.mesh.position, 0xff2a2a); }
      }
    }
  }

  // ---------- Sara: Eye of the Guide (once per match) ----------
  eyeOfTheGuide() {
    if (this.saraUsed) { this.toast("Sara's Eye of the Guide is used for this match"); return; }
    if (this.player.state !== 'ground') return;
    const p = this.player.pos;
    const e = this.loot.near(p.x, p.z, 260, e => e.rar >= 3);
    if (!e) { this.sara('Nothing rare nearby… try another area, then ask me again.', 5); return; }
    this.saraUsed = true; this.saraMark = e;
    this.sara(`Eye of the Guide: <b style="color:${RARITY_COLORS[e.rar]}">${itemName(e.it)}</b> is ${Math.round(Math.hypot(e.x - p.x, e.z - p.z))} m away – marked purple on your map.`, 8);
  }

  // ---------- events ----------
  onDamage(v, dmg, part, attacker, cause) {
    if (attacker === this.player && v !== this.player) {
      this.audio.hit(part === 'head');
      const hm = $('hitmark'); hm.className = part === 'head' ? 'show head' : 'show'; clearTimeout(this.hmT); this.hmT = setTimeout(() => hm.className = '', 120);
    }
    if (v === this.player && cause !== 'Al-Tawq') {
      this.audio.hurt();
      const fl = $('dmgflash'); fl.style.opacity = Math.min(0.7, 0.2 + dmg / 60); clearTimeout(this.dfT); this.dfT = setTimeout(() => fl.style.opacity = 0, 160);
      if (attacker) {
        const a = Math.atan2(-(attacker.pos.x - v.pos.x), -(attacker.pos.z - v.pos.z)) - this.camYaw;
        const di = $('dmgdir'); di.style.transform = `rotate(${-a}rad)`; di.style.opacity = 1; clearTimeout(this.ddT); this.ddT = setTimeout(() => di.style.opacity = 0, 900);
      }
    }
  }

  onKill(v, attacker, cause, headshot) {
    if (!v.alive) return;
    if (this.map.training) {
      if (attacker === this.player && v !== this.player) { this.audio.kill(); this.toast(`🎯 ${v.name}${headshot ? ' · Headshot' : ''}`); const hm = $('hitmark'); hm.className = 'show kill'; this.player.kills++; }
      v.hp = 100; v.action = null;
      if (v.home) v.pos.set(v.home.x, this.world.getHeight(v.home.x, v.home.z), v.home.z);
      if (v === this.player) { v.pos.copy(this.spawnPoint); this.toast('رجعت لنقطة البداية'); }
      if (v.armored) { v.helmet = { lvl: 2, dur: ARMOR.helmet.dur[2] }; v.vest = { lvl: 2, dur: ARMOR.vest.dur[2] }; v.refreshLook(); }
      return;
    }
    if (v.vehicle) { v.vehicle.seats[v.seat] = null; v.vehicle = null; if (v === this.player) this.audio.stopEngine(); }
    v.alive = false; v.state = 'dead'; v.mesh.visible = false; v.knocked = false; v.action = null;
    this.aliveCount--;
    v.place = this.aliveCount + 1;
    const teamOut = !this.teamUp(v.team);
    if (teamOut) { this.teamsLeft--; for (const m of this.teams[v.team]) m.teamPlace = this.teamsLeft + 1; }
    const items = v.inventory();
    if (items.length) this.loot.addCrate(v.pos.x, v.pos.z, items, 'death', v.name);
    this.heat.add(v.pos.x, v.pos.z, 6, this.time);
    const killer = attacker || (cause === 'Al-Tawq' ? null : v.lastHitBy);
    if (killer && killer !== v) killer.kills++;
    const me = n => n === this.player.name && (killer === this.player || v === this.player) ? `<span class="me">${n}</span>` : n;
    this.hud.killfeed(killer && killer !== v
      ? `<b>${me(killer.name)}</b> <span class="wpn">${headshot ? '🎯 ' : ''}${cause}</span> <b>${me(v.name)}</b>`
      : `<b>${me(v.name)}</b> <span class="wpn">${cause === 'Al-Tawq' ? 'was caught by Al-Tawq' : cause === 'Red Zone' ? 'was hit by the Red Zone' : 'died'}</span>`);
    if (killer === this.player && v !== this.player) { this.audio.kill(); this.toast(`Eliminated <b>${v.name}</b>${headshot ? ' · Headshot' : ''}`); const hm = $('hitmark'); hm.className = 'show kill'; }
    for (const a of this.actors) if (a.ai && a.ai.target === v) a.ai.target = null;
    if (v === this.player) { this.deathBy = { killer, cause }; if (this.teamUp(0)) { this.spectating = true; this.sara('متت – بتتفرّج على فريقك هلّق. إذا فاز الفريق، بتفوز معه!', 6); } }
    if (teamOut && v.team === 0) this.endMatch(false, this.deathBy && this.deathBy.killer, this.deathBy ? this.deathBy.cause : cause);
    else if (this.teamsLeft === 1 && this.teamUp(0)) this.endMatch(true);
  }

  endMatch(win, killer, cause) {
    if (this.over) return;
    this.over = true; this.spectate = true;
    const p = this.player, team = this.teams[0];
    const place = win ? 1 : (p.teamPlace || this.teamsLeft + 1);
    const kills = team.filter(m => m.isPlayer).reduce((s, m) => s + m.kills, 0);
    this.ui.togglePause(false); this.ui.closeModal(); this.hud.closePanels();
    const est = rewards({ place, total: this.total, kills, mode: this.mode.id });
    const tpBefore = this.user.tp || 0;
    $('end').style.display = 'flex';
    $('endtitle').innerHTML = win ? 'KRYVON SURVIVOR<br><span class="knafeh">كنافة النصر! 🧀</span>' : `#${place} <small>من ${this.teamCount} ${this.teamSize > 1 ? 'فريق' : 'لاعب'}</small>`;
    $('endsub').innerHTML = win ? 'كرايفون: أرض وحدة… ناجي واحد' : killer ? `قتلك <b>${killer.name}</b> · ${cause}` : `مت بـ ${cause}`;
    const fmt = t => `${Math.floor(t / 60)}:${String(Math.floor(t % 60)).padStart(2, '0')}`;
    const render = (r, tpAfter) => {
      const t0 = tierOf(tpBefore), t1 = tierOf(tpAfter);
      $('endstats').innerHTML = `
        <div><b>${p.kills}</b>قتلات</div><div><b>${Math.round(p.damageDealt)}</b>ضرر</div><div><b>${fmt(this.time)}</b>نجوت</div>
        <div><b>+${r.xp}</b>XP</div><div><b>+${r.coins}</b>🪙 عملات</div>
        <div class="tierbox" style="--tc:${t1.color}"><b>${r.tp >= 0 ? '+' : ''}${r.tp}</b>نقاط التصنيف
          <span class="tname">${t1.ar} ${t1.roman}${t1.index !== t0.index ? ' ⬆' : ''}</span><i><u style="width:${Math.round(t1.progress * 100)}%"></u></i></div>
        ${this.teamSize > 1 ? `<table class="teamtbl"><tr><th>الفريق</th><th>قتلات</th><th>ضرر</th></tr>${team.map(m => `<tr class="${m.isPlayer ? 'me' : ''}"><td>${m.name}</td><td>${m.kills}</td><td>${Math.round(m.damageDealt)}</td></tr>`).join('')}</table>` : ''}`;
    };
    render(est, Math.max(0, tpBefore + est.tp));
    if (!this.map.training) account.reportMatch({ map: this.map.id, mode: this.mode.id, team: 0, place, kills, damage: p.damageDealt, total: this.total, time: Math.round(this.time) })
      .then(r => { if (r && account.user) render({ xp: r.xp, coins: r.coins, tp: r.tp }, account.user.tp); });
    if (win) this.audio.victory();
    document.exitPointerLock && document.exitPointerLock();
  }

  // ---------- camera ----------
  updateCamera(dt) {
    const p = this.player, cam = this.camera;
    const baseFov = this.settings.graphics.fov;
    let pivot, dist, fov = baseFov, shoulder = 0;
    const f = fwd(this.camYaw, this.camPitch);
    if (p.state === 'plane' && this.plane) { pivot = this.plane.pos.clone(); dist = 45; }
    else if (!p.alive) { // spectate a living teammate, or the place you fell
      const m = this.mates(p).find(x => x.alive && x.state === 'ground') || this.mates(p).find(x => x.alive);
      if (m && this.spectating) { pivot = m.eye(); dist = 4; this.camYaw += ((m.yaw - this.camYaw + Math.PI * 3) % (Math.PI * 2) - Math.PI) * Math.min(1, dt * 3); }
      else { pivot = p.pos.clone().add(new THREE.Vector3(0, 2, 0)); dist = 14; this.camYaw += dt * 0.15; }
    }
    else if (p.state === 'vehicle' && p.vehicle) {
      const v = p.vehicle, d = VEHICLES[v.type];
      pivot = v.pos.clone().add(new THREE.Vector3(0, d.camH, 0)); dist = d.cam + Math.abs(v.speed) * 0.08; fov = baseFov + Math.min(10, Math.abs(v.speed) * 0.3);
      if (Math.abs(this.input.mdx) < 0.5 && Math.abs(v.speed) > 4) { // drift back behind the car when you are not looking around
        const back = v.speed >= 0 ? v.yaw : v.yaw + Math.PI;
        this.camYaw += (((back - this.camYaw) % (Math.PI * 2) + Math.PI * 3) % (Math.PI * 2) - Math.PI) * Math.min(1, dt * 1.6);
        this.camPitch += (-0.22 - this.camPitch) * Math.min(1, dt * 2);
      }
      this.audio.setEngine(Math.abs(v.speed) / d.maxSpeed);
    }
    else if (p.knocked) { pivot = p.pos.clone().add(new THREE.Vector3(0, 0.9, 0)); dist = 3.4; shoulder = 0.3; }
    else if (p.state === 'fall' || p.state === 'chute') { pivot = p.pos.clone().add(new THREE.Vector3(0, 1.5, 0)); dist = 9; }
    else if (this.scoped) { pivot = p.eye(); dist = 0; fov = 70 / p.weapon.zoom; }
    else if (this.aiming) { shoulder = 0.55; dist = 1.9; fov = baseFov * (p.weapon && p.weapon.zoom === 1.3 ? 0.62 : 0.75); pivot = p.eye(); }
    else { shoulder = 0.6; dist = 3.6; pivot = p.eye().add(new THREE.Vector3(0, 0.15, 0)); }
    const right = new THREE.Vector3(Math.cos(this.camYaw), 0, -Math.sin(this.camYaw));
    pivot.addScaledVector(right, shoulder);
    if (dist > 0 && p.state === 'ground' && p.alive) {
      const t = this.world.raycast(pivot.x, pivot.y, pivot.z, -f.x, -f.y, -f.z, dist + 0.3, 0.5);
      if (t !== Infinity) dist = Math.max(0.2, t - 0.3);
    }
    const pos = pivot.clone().addScaledVector(f, -dist);
    const g = this.world.getHeight(pos.x, pos.z);
    if (pos.y < g + 0.3) pos.y = g + 0.3;
    cam.position.copy(pos);
    if (this.shake > 0) { this.shake = Math.max(0, this.shake - dt * 2); cam.position.x += (Math.random() - 0.5) * this.shake * 0.3; cam.position.y += (Math.random() - 0.5) * this.shake * 0.3; }
    cam.lookAt(pivot.clone().addScaledVector(f, 20));
    cam.fov += (fov - cam.fov) * Math.min(1, dt * 14); cam.updateProjectionMatrix();
    p.mesh.visible = p.mesh.visible && !this.scoped;
    // sun follows the player for crisp shadows nearby
    const c = p.state === 'plane' && this.plane ? this.plane.pos : p.pos;
    this.sun.position.copy(c).add(this.sunOff); this.sun.target.position.copy(c);
    this.skyMesh.position.copy(cam.position);
    if (this.sunSprite) this.sunSprite.position.copy(cam.position).addScaledVector(this.sunOff.clone().normalize(), 1900);
    if (this.clouds) for (const c of this.clouds) { c.position.x += dt * 3; if (c.position.x > 1400) c.position.x = -1400; }
  }

  updateZoneName() {
    const p = this.player;
    if (p.state !== 'ground' && p.state !== 'chute') return;
    const z = this.world.zoneAt(p.pos.x, p.pos.z);
    const id = z ? z.id : null;
    if (id !== this.lastZone) {
      this.lastZone = id;
      if (z) {
        const el = $('zonename');
        el.innerHTML = `${z.name}<small>${z.ar} · ${['', 'Low', 'Medium', 'High'][z.tier]} loot</small>`;
        el.classList.remove('show'); void el.offsetWidth; el.classList.add('show');
      }
    }
  }

  // ---------- UI helpers ----------
  toggleMap(force) {
    this.hud.mapOpen = force ?? !this.hud.mapOpen;
    $('bigmapwrap').style.display = this.hud.mapOpen ? 'flex' : 'none';
    if (this.hud.mapOpen) this.hud.drawBigMap();
  }
  announce(text, ar, cls = '') {
    const el = $('announce');
    el.className = cls; el.innerHTML = text + (ar ? `<small>${ar}</small>` : '');
    el.style.opacity = 1; clearTimeout(this.anT); this.anT = setTimeout(() => el.style.opacity = 0, 4500);
    if (cls === 'red') this.sara('Bombardment incoming – get <b>inside a building</b>, now!', 5);
  }
  toast(html) {
    const el = $('toast'); el.innerHTML = html; el.style.opacity = 1;
    clearTimeout(this.toT); this.toT = setTimeout(() => el.style.opacity = 0, 2200);
  }
  sara(html, secs = 6) {
    if (this.settings && !this.settings.audio.voice) return;
    const el = $('sara-msg'); el.innerHTML = html; $('sarabox').style.opacity = 1;
    clearTimeout(this.saT); this.saT = setTimeout(() => $('sarabox').style.opacity = 0, secs * 1000);
  }
}

window.KRYVON = new Game();
