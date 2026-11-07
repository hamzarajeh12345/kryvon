import * as THREE from 'three';
import { RING, HALF, MAP_SCALE } from './config.js';

// ---------------- Al-Tawq (The Ring) ----------------
export class Ring {
  constructor(game) {
    this.game = game; this.world = game.world;
    const m = game.map;
    // smaller maps: scale every circle by the area size, and shorten the timers a bit
    this.k = m.training ? 1 : Math.min(1, m.radius / (1150 * MAP_SCALE));
    this.tk = this.k < 1 ? 0.75 + 0.25 * this.k : 1;
    this.cur = { x: m.cx, z: m.cz, r: m.training ? 5000 : m.radius };
    this.phase = 0; this.state = m.training ? 'final' : 'wait';
    this.timer = this.cfg(0).wait;
    this.next = m.training ? null : this.pickNext(this.cur, this.cfg(0));
    this.from = null;
    const mat = new THREE.MeshBasicMaterial({ color: 0x2f7dff, transparent: true, opacity: 0.22, side: THREE.DoubleSide, depthWrite: false });
    this.mesh = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 500, 96, 1, true), mat);
    this.mesh.position.y = 150; this.mesh.renderOrder = 2;
    game.scene.add(this.mesh);
  }

  // fraction of the circle that is water or off-map (sunflower sampling)
  waterFraction(cx, cz, r, n = 180) {
    let wet = 0;
    for (let i = 0; i < n; i++) {
      const rr = r * Math.sqrt((i + 0.5) / n), a = i * 2.39996323;
      const x = cx + Math.cos(a) * rr, z = cz + Math.sin(a) * rr;
      if (!this.world.inMap(x, z) || this.world.isWater(x, z)) wet++;
    }
    return wet / n;
  }

  // next center is random but fully inside the previous circle; GDD §4A: never closes in the sea
  pickNext(prev, cfg) {
    let best = null, bestW = Infinity;
    const slack = Math.max(0, prev.r - cfg.r);
    const strict = cfg.water === 0;
    for (let i = 0; i < 500; i++) {
      const a = Math.random() * Math.PI * 2, rr = Math.sqrt(Math.random()) * slack;
      const x = prev.x + Math.cos(a) * rr, z = prev.z + Math.sin(a) * rr;
      if (Math.abs(x) > HALF - 10 || Math.abs(z) > HALF - 10) continue;
      if (strict && this.world.isWater(x, z)) continue;
      const w = this.waterFraction(x, z, Math.max(cfg.r, 6), strict ? 260 : 140);
      if (w <= cfg.water) return { x, z, r: cfg.r, water: w };
      if (w < bestW) { bestW = w; best = { x, z, r: cfg.r, water: w }; }
    }
    return best || { x: prev.x, z: prev.z, r: cfg.r, water: 0 };
  }

  cfg(i) { const c = RING[Math.min(i, RING.length - 1)]; return { ...c, r: Math.max(2, c.r * this.k), wait: c.wait * this.tk, shrink: c.shrink * this.tk }; }
  get dps() { return this.game.map.training ? 0 : RING[Math.min(this.phase, RING.length - 1)].dps; }
  inside(x, z) { return Math.hypot(x - this.cur.x, z - this.cur.z) < this.cur.r; }

  update(dt) {
    if (this.state === 'final') return this.sync();
    this.timer -= dt;
    if (this.state === 'wait' && this.timer <= 0) {
      this.state = 'shrink'; this.timer = this.cfg(this.phase).shrink; this.from = { ...this.cur };
      this.game.announce(`Al-Tawq is closing!`, 'الطوق يضيق');
    } else if (this.state === 'shrink') {
      const t = 1 - Math.max(0, this.timer) / this.cfg(this.phase).shrink;
      this.cur = { x: this.from.x + (this.next.x - this.from.x) * t, z: this.from.z + (this.next.z - this.from.z) * t, r: this.from.r + (this.next.r - this.from.r) * t };
      if (this.timer <= 0) {
        this.cur = { x: this.next.x, z: this.next.z, r: this.next.r };
        this.phase++;
        if (this.phase < RING.length) {
          this.next = this.pickNext(this.cur, this.cfg(this.phase));
          this.state = 'wait'; this.timer = this.cfg(this.phase).wait;
          this.game.announce(`Phase ${this.phase + 1}/8 – new ring marked on the map`);
        } else { this.state = 'final'; this.next = null; }
      }
    }
    this.sync();
  }
  sync() { this.mesh.visible = !this.game.map.training; this.mesh.position.x = this.cur.x; this.mesh.position.z = this.cur.z; this.mesh.scale.set(this.cur.r, 1, this.cur.r); }
}

// ---------------- Red Zone (bombardment) ----------------
export class RedZone {
  constructor(game) {
    this.game = game; this.state = 'idle'; this.nextT = 80 + Math.random() * 60; this.r = 60;
    const mat = new THREE.MeshBasicMaterial({ color: 0xff2a2a, transparent: true, opacity: 0.16, side: THREE.DoubleSide, depthWrite: false });
    this.mesh = new THREE.Mesh(new THREE.CylinderGeometry(1, 1, 220, 64, 1, true), mat);
    this.mesh.visible = false; this.mesh.position.y = 60;
    game.scene.add(this.mesh);
  }
  update(dt) {
    const g = this.game, ring = g.ring;
    if (this.state === 'idle') {
      this.nextT -= dt;
      if (this.nextT <= 0 && ring.phase < 6 && ring.cur.r > 90) {
        const p = g.world.randomLandPoint(Math.random, ring.cur.x, ring.cur.z, Math.max(10, ring.cur.r - this.r * 0.6));
        this.x = p.x; this.z = p.z; this.r = Math.min(70, ring.cur.r * 0.35);
        this.state = 'warn'; this.t = 10;
        this.mesh.visible = true; this.mesh.position.set(this.x, 60, this.z); this.mesh.scale.set(this.r, 1, this.r);
        g.audio.siren(Math.hypot(g.player.pos.x - this.x, g.player.pos.z - this.z));
        g.announce('⚠ RED ZONE – take cover inside a building!', 'المنطقة الحمراء', 'red');
      }
    } else if (this.state === 'warn') {
      this.t -= dt;
      this.mesh.material.opacity = 0.12 + 0.1 * Math.abs(Math.sin(performance.now() * 0.006));
      if (this.t <= 0) { this.state = 'active'; this.t = 20 + Math.random() * 10; this.bombT = 0; }
    } else if (this.state === 'active') {
      this.t -= dt; this.bombT -= dt;
      this.mesh.material.opacity = 0.22;
      while (this.bombT <= 0) {
        this.bombT += 0.16;
        const a = Math.random() * 6.283, rr = Math.sqrt(Math.random()) * this.r;
        const x = this.x + Math.cos(a) * rr, z = this.z + Math.sin(a) * rr;
        g.explode(x, g.world.getHeight(x, z), z, 7, 85, null, 'Red Zone', true);
      }
      if (this.t <= 0) { this.state = 'idle'; this.nextT = 60 + Math.random() * 70; this.mesh.visible = false; }
    }
  }
  get visible() { return this.state !== 'idle'; }
}

// ---------------- Black Zone (combat heatmap) ----------------
export class Heatmap {
  constructor() { this.N = 40; this.events = []; this.grid = new Float32Array(this.N * this.N); this.timer = 30; this.max = 1; }
  add(x, z, w, now) { this.events.push({ x, z, w, t: now }); }
  update(dt, now) {
    this.timer -= dt;
    if (this.timer > 0) return;
    this.timer = 30; // GDD §4C: refresh every 30 s, window = last 60 s
    this.events = this.events.filter(e => e.t > now - 60);
    this.grid.fill(0);
    const N = this.N;
    for (const e of this.events) {
      const gx = Math.floor((e.x + HALF) / (2 * HALF) * N), gz = Math.floor((e.z + HALF) / (2 * HALF) * N);
      if (gx >= 0 && gz >= 0 && gx < N && gz < N) this.grid[gz * N + gx] += e.w;
    }
  }
}
