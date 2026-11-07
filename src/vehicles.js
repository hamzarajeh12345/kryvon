// Vehicles – original low-poly designs, arcade driving that follows the terrain, collisions, damage,
// run-overs, passengers (your squad hops in with you), wrecks.
// Forward is -Z (same as actors). Each vehicle is 2 meshes: body + wheels (vertex coloured, merged).
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { prep, M4, BODY_MAT } from './character.js';
import { parkingSpots } from './streets.js';
import { clamp, mulberry32 } from './world.js';

const GLASS = 0x1b2630, DARK = 0x1c1d1f, CHROME = 0xb8bcc0, LIGHT = 0xfff3c0, TAIL = 0xc0262d, TYRE = 0x161616, RIM = 0x8a8d90;
const WRECK = new THREE.MeshLambertMaterial({ color: 0x2a2724 });

export const VEHICLES = {
  pickup:  { ar: 'بيك أب',     len: 5.0, wid: 2.0, hgt: 1.95, maxSpeed: 30, accel: 8.5, brake: 20, reverse: 8, turn: 1.2, hp: 320, offroad: 0.85, cam: 8.5, camH: 2.6,
             seats: [[-0.45, 1.1, -0.35], [0.45, 1.1, -0.35], [-0.5, 1.45, 1.5], [0.5, 1.45, 1.5]], colors: [0xeeeeea, 0xb83a2a, 0xc9b38a, 0x2f5a8a, 0x4a4f3a] },
  taxi:    { ar: 'تكسي',       len: 4.5, wid: 1.85, hgt: 1.5, maxSpeed: 34, accel: 10, brake: 22, reverse: 9, turn: 1.35, hp: 230, offroad: 0.7, cam: 7.5, camH: 2.3,
             seats: [[-0.42, 0.95, -0.2], [0.42, 0.95, -0.2], [-0.42, 0.95, 0.75], [0.42, 0.95, 0.75]], colors: [0xf2c200, 0xf2c200, 0xeeeeea, 0x2f6b4a, 0x6a1f2a] },
  minibus: { ar: 'سرفيس',      len: 5.6, wid: 2.05, hgt: 2.45, maxSpeed: 25, accel: 6.5, brake: 18, reverse: 7, turn: 1.0, hp: 400, offroad: 0.7, cam: 9.5, camH: 3,
             seats: [[-0.5, 1.25, -1.6], [0.5, 1.25, -1.6], [-0.5, 1.25, -0.2], [0.5, 1.25, -0.2], [-0.5, 1.25, 1.0], [0.5, 1.25, 1.0]], colors: [0xeeeeea, 0xe8dcc0, 0xdfe6ea] },
  buggy:   { ar: 'باغي رملي',  len: 3.4, wid: 1.9, hgt: 1.6, maxSpeed: 31, accel: 12, brake: 22, reverse: 9, turn: 1.55, hp: 170, offroad: 1.0, cam: 7, camH: 2.2,
             seats: [[-0.4, 0.85, 0.0], [0.4, 0.85, 0.0]], colors: [0xe07a1f, 0x3a8a3a, 0x2a2a2a, 0xc9a46a] },
  moto:    { ar: 'دراجة نارية', len: 2.1, wid: 0.8, hgt: 1.3, maxSpeed: 38, accel: 13, brake: 24, reverse: 4, turn: 1.9, hp: 100, offroad: 0.85, cam: 5.5, camH: 2,
             seats: [[0, 1.05, 0.1], [0, 1.15, 0.6]], colors: [0x1c1c1c, 0xb83a2a, 0x2f5a8a, 0xd9a520] },
};

// ---------- models ----------
function wheels(list, r, w) {
  const P = [];
  for (const [x, z] of list) {
    P.push(prep(new THREE.CylinderGeometry(r, r, w, 14), TYRE, null, M4(x, r, z, 0, 0, Math.PI / 2)));
    P.push(prep(new THREE.CylinderGeometry(r * 0.55, r * 0.55, w + 0.02, 10), RIM, null, M4(x, r, z, 0, 0, Math.PI / 2)));
  }
  return mergeGeometries(P, false);
}
function lights(P, wid, y, zFront, zBack) {
  for (const s of [-1, 1]) {
    P.push(prep(new THREE.BoxGeometry(0.32, 0.14, 0.05), LIGHT, null, M4(s * (wid / 2 - 0.25), y, zFront - 0.01)));
    P.push(prep(new THREE.BoxGeometry(0.3, 0.14, 0.05), TAIL, null, M4(s * (wid / 2 - 0.22), y, zBack + 0.01)));
  }
}
const B = (P, w, h, d, c, x, y, z, rx = 0, ry = 0) => P.push(prep(new THREE.BoxGeometry(w, h, d), c, null, M4(x, y, z, rx, ry)));

function buildModel(type, color) {
  const P = [];
  let wheelGeo;
  switch (type) {
    case 'pickup': {
      B(P, 2.0, 0.55, 4.9, color, 0, 0.82, 0);                          // body
      B(P, 1.92, 0.95, 1.95, color, 0, 1.55, -0.45);                     // cab
      B(P, 1.8, 0.62, 0.06, GLASS, 0, 1.6, -1.44, -0.35);                 // windscreen
      B(P, 1.95, 0.5, 1.5, GLASS, 0, 1.68, -0.45);                       // side windows band
      B(P, 1.94, 0.12, 1.98, color, 0, 2.06, -0.45);                     // roof
      B(P, 1.9, 0.32, 1.4, color, 0, 1.22, -1.95);                       // hood
      B(P, 1.6, 0.25, 0.08, DARK, 0, 0.98, -2.47);                       // grille
      B(P, 2.06, 0.2, 0.18, DARK, 0, 0.62, -2.5); B(P, 2.06, 0.2, 0.18, DARK, 0, 0.62, 2.5);   // bumpers
      for (const s of [-1, 1]) B(P, 0.08, 0.45, 2.3, color, s * 0.96, 1.3, 1.3);               // bed sides
      B(P, 1.9, 0.45, 0.08, color, 0, 1.3, 2.43);                        // tailgate
      B(P, 1.84, 0.06, 2.3, 0x3a3a3a, 0, 1.1, 1.3);                      // bed floor
      P.push(prep(new THREE.CylinderGeometry(0.36, 0.36, 0.22, 12), TYRE, null, M4(0, 1.35, 2.1, Math.PI / 2))); // spare
      B(P, 2.1, 0.08, 0.08, DARK, 0, 1.75, 0.55);                        // roll bar
      lights(P, 2.0, 0.98, -2.45, 2.45);
      wheelGeo = wheels([[-0.95, -1.6], [0.95, -1.6], [-0.95, 1.6], [0.95, 1.6]], 0.42, 0.32);
      break;
    }
    case 'taxi': {
      B(P, 1.85, 0.6, 4.4, color, 0, 0.72, 0);
      B(P, 1.72, 0.62, 2.2, color, 0, 1.3, 0.2);
      B(P, 1.74, 0.42, 2.0, GLASS, 0, 1.32, 0.2);
      B(P, 1.6, 0.5, 0.06, GLASS, 0, 1.3, -0.95, -0.55); B(P, 1.6, 0.45, 0.06, GLASS, 0, 1.3, 1.35, 0.55);
      B(P, 1.7, 0.08, 1.9, color, 0, 1.63, 0.2);
      if (color === 0xf2c200) { B(P, 0.7, 0.2, 0.25, 0xffffff, 0, 1.78, 0.2); B(P, 0.5, 0.1, 0.26, 0x222222, 0, 1.78, 0.2); B(P, 0.06, 0.62, 4.42, 0x222222, 0.93, 0.78, 0); B(P, 0.06, 0.62, 4.42, 0x222222, -0.93, 0.78, 0); }
      B(P, 1.9, 0.18, 0.16, CHROME, 0, 0.55, -2.22); B(P, 1.9, 0.18, 0.16, CHROME, 0, 0.55, 2.22);
      B(P, 1.2, 0.18, 0.06, DARK, 0, 0.8, -2.21);
      lights(P, 1.85, 0.82, -2.2, 2.2);
      wheelGeo = wheels([[-0.86, -1.4], [0.86, -1.4], [-0.86, 1.4], [0.86, 1.4]], 0.34, 0.26);
      break;
    }
    case 'minibus': {
      B(P, 2.05, 1.85, 5.5, color, 0, 1.42, 0);
      B(P, 2.08, 0.6, 4.3, GLASS, 0, 1.75, 0.45);                         // passenger windows
      B(P, 1.9, 0.75, 0.06, GLASS, 0, 1.75, -2.76, -0.12);                 // windscreen
      B(P, 2.08, 0.12, 5.52, 0x2f6fae, 0, 1.15, 0);                        // stripe
      B(P, 1.9, 0.08, 4.8, 0xd8d8d8, 0, 2.4, 0.2);                         // roof rack
      B(P, 2.1, 0.2, 0.16, DARK, 0, 0.55, -2.8); B(P, 2.1, 0.2, 0.16, DARK, 0, 0.55, 2.8);
      lights(P, 2.05, 0.85, -2.76, 2.76);
      wheelGeo = wheels([[-0.95, -1.9], [0.95, -1.9], [-0.95, 1.8], [0.95, 1.8]], 0.4, 0.3);
      break;
    }
    case 'buggy': {
      B(P, 1.5, 0.18, 3.0, DARK, 0, 0.62, 0);
      B(P, 1.7, 0.3, 1.0, color, 0, 0.8, -1.25);                           // nose
      B(P, 1.3, 0.55, 0.9, 0x444444, 0, 0.95, 1.2);                        // engine
      for (const s of [-1, 1]) {
        B(P, 0.07, 1.0, 0.07, DARK, s * 0.72, 1.2, -0.55, 0.25); B(P, 0.07, 1.0, 0.07, DARK, s * 0.72, 1.2, 0.6, -0.25);
        B(P, 0.07, 0.07, 1.25, DARK, s * 0.72, 1.68, 0.02);
        B(P, 0.45, 0.5, 0.12, 0x2a2a2a, s * 0.4, 0.95, 0.25);              // seat backs
      }
      B(P, 1.5, 0.07, 0.07, DARK, 0, 1.68, -0.6); B(P, 1.5, 0.07, 0.07, DARK, 0, 1.68, 0.65);
      for (const s of [-1, 1]) B(P, 0.2, 0.14, 0.08, LIGHT, s * 0.35, 1.78, -0.62);
      wheelGeo = wheels([[-0.92, -1.15], [0.92, -1.15], [-0.92, 1.15], [0.92, 1.15]], 0.48, 0.42);
      break;
    }
    case 'moto': {
      B(P, 0.16, 0.2, 1.3, DARK, 0, 0.6, 0);
      B(P, 0.32, 0.26, 0.55, color, 0, 0.85, -0.25);                      // tank
      B(P, 0.3, 0.12, 0.8, 0x1a1a1a, 0, 0.88, 0.35);                       // seat
      B(P, 0.7, 0.05, 0.05, CHROME, 0, 1.15, -0.62);                       // handlebar
      B(P, 0.06, 0.55, 0.06, CHROME, 0, 0.88, -0.7, 0.35);                 // fork
      B(P, 0.18, 0.14, 0.08, LIGHT, 0, 1.0, -0.78);
      B(P, 0.1, 0.1, 0.7, CHROME, 0.18, 0.45, 0.4);                        // exhaust
      B(P, 0.3, 0.12, 0.4, color, 0, 0.75, 0.82);                          // rear fender
      wheelGeo = wheels([[0, -0.78], [0, 0.78]], 0.34, 0.14);
      break;
    }
  }
  return { body: mergeGeometries(P, false), wheels: wheelGeo };
}

// ---------- one vehicle ----------
export class Vehicle {
  constructor(sys, type, x, z, yaw, color) {
    this.sys = sys; this.type = type; this.def = VEHICLES[type];
    this.pos = new THREE.Vector3(x, sys.world.getHeight(x, z), z);
    this.yaw = yaw; this.speed = 0; this.steer = 0; this.hp = this.def.hp; this.dead = false;
    this.seats = this.def.seats.map(() => null);
    const geo = sys.geometry(type, color);
    this.mesh = new THREE.Group();
    this.body = new THREE.Mesh(geo.body, BODY_MAT); this.body.castShadow = true; this.body.receiveShadow = true;
    this.wheels = new THREE.Mesh(geo.wheels, BODY_MAT); this.wheels.castShadow = true;
    this.mesh.add(this.body, this.wheels);
    this.mesh.rotation.order = 'YXZ';
    sys.game.scene.add(this.mesh);
    this.smokeT = 0;
    this.sync(0);
  }
  get driver() { return this.seats[0]; }
  forward() { return { x: -Math.sin(this.yaw), z: -Math.cos(this.yaw) }; }
  toLocal(x, z) { const dx = x - this.pos.x, dz = z - this.pos.z, c = Math.cos(this.yaw), s = Math.sin(this.yaw); return { x: dx * c - dz * s, z: dx * s + dz * c }; }
  seatWorld(i) {
    const [sx, sy, sz] = this.def.seats[i], c = Math.cos(this.yaw), s = Math.sin(this.yaw);
    return new THREE.Vector3(this.pos.x + sx * c + sz * s, this.pos.y + sy, this.pos.z - sx * s + sz * c);
  }
  // three circles along the body, for collisions
  circles() {
    const f = this.forward(), r = Math.min(this.def.wid / 2, this.def.len / 3), k = this.def.len / 2 - r;
    return [[this.pos.x + f.x * k, this.pos.z + f.z * k], [this.pos.x, this.pos.z], [this.pos.x - f.x * k, this.pos.z - f.z * k]].map(([x, z]) => ({ x, z, r }));
  }

  update(dt, ctrl) {
    const w = this.sys.world, d = this.def;
    if (this.dead) { this.speed *= 0.9; return this.sync(dt); }
    const wet = w.getHeight(this.pos.x, this.pos.z) < w.waterLevel(this.pos.x, this.pos.z) - 0.7;
    if (wet) { this.hp -= 12 * dt; if (this.hp <= 0) this.destroy(null); }
    const onRoad = w.roadAt(this.pos.x, this.pos.z) > 0.35 || this.sys.onStreet(this.pos.x, this.pos.z);
    const max = d.maxSpeed * (onRoad ? 1 : d.offroad) * (ctrl.boost ? 1.12 : 1) * (wet ? 0.2 : 1);

    // throttle / brake / reverse
    const t = ctrl.throttle;
    if (t > 0.05) this.speed = this.speed < -0.3 ? this.speed + d.brake * dt : Math.min(max, this.speed + d.accel * t * dt * (1 - 0.6 * Math.max(0, this.speed) / max));
    else if (t < -0.05) this.speed = this.speed > 0.3 ? this.speed - d.brake * dt : Math.max(-d.reverse, this.speed + d.accel * t * 0.6 * dt);
    else this.speed -= Math.sign(this.speed) * Math.min(Math.abs(this.speed), 2.2 * dt);
    if (ctrl.brake) this.speed -= Math.sign(this.speed) * Math.min(Math.abs(this.speed), d.brake * 1.2 * dt);
    if (this.speed > max) this.speed = Math.max(max, this.speed - d.brake * 0.5 * dt);

    // slopes slow you down uphill
    const f = this.forward(), half = d.len / 2;
    const hf = w.getHeight(this.pos.x + f.x * half, this.pos.z + f.z * half), hb = w.getHeight(this.pos.x - f.x * half, this.pos.z - f.z * half);
    this.speed -= 9.8 * ((hf - hb) / d.len) * dt * 0.7 * Math.sign(this.speed || 1);

    // steering (less effective at very low and very high speed; handbrake helps turning)
    this.steer += (ctrl.steer - this.steer) * Math.min(1, dt * 6);
    const sp = Math.abs(this.speed), grip = clamp(sp / 5, 0, 1) * (1 - 0.35 * clamp(sp / d.maxSpeed, 0, 1)) * (ctrl.brake ? 1.35 : 1);
    this.yaw -= this.steer * d.turn * grip * Math.sign(this.speed) * dt;

    // move + collide
    const ox = this.pos.x, oz = this.pos.z, f2 = this.forward();
    this.pos.x = clamp(this.pos.x + f2.x * this.speed * dt, -w.halfSize + 6, w.halfSize - 6);
    this.pos.z = clamp(this.pos.z + f2.z * this.speed * dt, -w.halfSize + 6, w.halfSize - 6);
    const step = Math.hypot(this.pos.x - ox, this.pos.z - oz);
    const climb = step > 0.01 ? (w.getHeight(this.pos.x, this.pos.z) - w.getHeight(ox, oz)) / step : 0;
    if (this.hitsWorld() || climb > 1.4 || this.sys.hitsVehicle(this)) {
      this.pos.x = ox; this.pos.z = oz;
      const impact = Math.abs(this.speed);
      if (impact > 7) this.crash(impact);
      this.speed = -this.speed * 0.25;
    }
    if (sp > 5) this.sys.runOver(this);
    if (this.hp < d.hp * 0.3 && (this.smokeT -= dt) <= 0) { this.smokeT = 0.25; this.sys.game.effects.smoke(this.pos.clone().add(new THREE.Vector3(0, 1.3, 0)), 0x333333); }
    this.sync(dt);
  }

  hitsWorld() {
    const w = this.sys.world, g = this.pos.y, out = this._q || (this._q = []);
    for (const c of this.circles()) {
      w.query(c.x - c.r, c.x + c.r, c.z - c.r, c.z + c.r, out);
      for (const k of out) {
        if (k.maxY < g + 0.45 || k.minY > g + this.def.hgt) continue;
        const cx = clamp(c.x, k.minX, k.maxX), cz = clamp(c.z, k.minZ, k.maxZ);
        if ((c.x - cx) ** 2 + (c.z - cz) ** 2 < c.r * c.r) return true;
      }
    }
    return false;
  }

  crash(impact) {
    this.damage((impact - 6) * 3, this.driver);
    for (const a of this.seats) if (a && impact > 12) a.takeDamage((impact - 12) * 2, 'body', null, 'Crash', true);
    this.sys.game.audio.burst(Math.min(1, impact / 25), 0.3, 500, 1);
  }

  damage(n, attacker) {
    if (this.dead) return;
    this.hp -= n;
    if (this.hp <= 0) this.destroy(attacker);
  }

  destroy(attacker) {
    if (this.dead) return;
    this.dead = true; this.hp = 0;
    this.body.material = WRECK; this.wheels.material = WRECK;
    const occupants = this.seats.filter(Boolean);
    for (const a of occupants) this.sys.exit(a, true);
    this.sys.game.explode(this.pos.x, this.pos.y + 1, this.pos.z, 7, 120, attacker, 'Vehicle explosion');
  }

  sync(dt) {
    const w = this.sys.world, d = this.def, f = this.forward();
    const half = d.len / 2, hw = d.wid / 2, rx = Math.cos(this.yaw), rz = -Math.sin(this.yaw);
    const hf = w.getHeight(this.pos.x + f.x * half, this.pos.z + f.z * half), hb = w.getHeight(this.pos.x - f.x * half, this.pos.z - f.z * half);
    const hr = w.getHeight(this.pos.x + rx * hw, this.pos.z + rz * hw), hl = w.getHeight(this.pos.x - rx * hw, this.pos.z - rz * hw);
    const water = w.waterLevel(this.pos.x, this.pos.z);
    this.pos.y = Math.max((hf + hb + hr + hl) / 4, water - 1.1);
    this.mesh.position.copy(this.pos);
    this.mesh.rotation.set(Math.atan2(hf - hb, d.len), this.yaw, this.type === 'moto' ? -this.steer * 0.35 * clamp(Math.abs(this.speed) / 15, 0, 1) : Math.atan2(hr - hl, d.wid));
    // seat the passengers
    this.seats.forEach((a, i) => {
      if (!a) return;
      const s = this.seatWorld(i);
      a.pos.set(s.x, s.y - 0.95, s.z); a.yaw = this.yaw; a.grounded = true;
    });
  }

  /** ray (normalized dir) vs the vehicle's box; returns distance or Infinity */
  rayHit(o, dir, maxT) {
    const d = this.def, c = Math.cos(this.yaw), s = Math.sin(this.yaw);
    const px = o.x - this.pos.x, pz = o.z - this.pos.z;
    const lx = px * c - pz * s, lz = px * s + pz * c, ly = o.y - this.pos.y;
    const dx = dir.x * c - dir.z * s, dz = dir.x * s + dir.z * c, dy = dir.y;
    let t0 = 0, t1 = maxT;
    for (const [p, v, lo, hi] of [[lx, dx, -d.wid / 2, d.wid / 2], [ly, dy, 0.3, d.hgt], [lz, dz, -d.len / 2, d.len / 2]]) {
      if (Math.abs(v) < 1e-9) { if (p < lo || p > hi) return Infinity; continue; }
      let a = (lo - p) / v, b = (hi - p) / v; if (a > b) [a, b] = [b, a];
      t0 = Math.max(t0, a); t1 = Math.min(t1, b); if (t0 > t1) return Infinity;
    }
    return t0;
  }
}

// ---------- the fleet ----------
export class VehicleSystem {
  constructor(game, opts = {}) {
    this.game = game; this.world = game.world; this.list = []; this.geos = new Map(); this.hitCd = new Map();
    this.world.halfSize = this.world.halfSize || 800;
    const rng = mulberry32(opts.seed || 4242);
    if (opts.spots) { for (const s of opts.spots) this.spawn(s.type, s.x, s.z, s.yaw, rng); return; }
    const spots = parkingSpots(this.world, rng).sort(() => rng() - 0.5);
    for (const s of spots) {
      if (this.list.length >= (opts.max || 55)) break;
      if (this.world.insideBuilding(s.x, s.z, 1000) || this.list.some(v => Math.hypot(v.pos.x - s.x, v.pos.z - s.z) < 9)) continue;
      const desert = (s.x > 250 && s.z < -200) || s.z > 420;
      const pick = (pairs) => { let t = 0; for (const p of pairs) t += p[1]; let r = rng() * t; for (const p of pairs) { r -= p[1]; if (r <= 0) return p[0]; } return pairs[0][0]; };
      const type = desert ? pick([['buggy', 4], ['pickup', 4], ['moto', 2]]) : pick([['pickup', 3], ['taxi', 3], ['minibus', 1.5], ['moto', 2], ['buggy', 1]]);
      this.spawn(type, s.x, s.z, s.yaw, rng);
    }
  }
  spawn(type, x, z, yaw, rng = Math.random) {
    const d = VEHICLES[type], color = d.colors[(rng() * d.colors.length) | 0];
    const v = new Vehicle(this, type, x, z, yaw, color);
    this.list.push(v); return v;
  }
  geometry(type, color) {
    const k = type + color;
    if (!this.geos.has(k)) this.geos.set(k, buildModel(type, color));
    return this.geos.get(k);
  }
  onStreet(x, z) { for (const s of this.world.streets) if (x > s.minX && x < s.maxX && z > s.minZ && z < s.maxZ) return true; return false; }

  update(dt, playerCtrl) {
    for (const v of this.list) {
      const ctrl = v.driver && v.driver.isPlayer ? playerCtrl : { throttle: 0, steer: 0, brake: !v.driver };
      v.update(dt, ctrl);
    }
  }

  /** closest usable vehicle whose body is within r metres */
  near(x, z, r) {
    let best = null, bd = r;
    for (const v of this.list) {
      if (v.dead) continue;
      const l = v.toLocal(x, z), dx = Math.max(0, Math.abs(l.x) - v.def.wid / 2), dz = Math.max(0, Math.abs(l.z) - v.def.len / 2), d = Math.hypot(dx, dz);
      if (d < bd && v.seats.some(s => !s)) { bd = d; best = v; }
    }
    return best;
  }

  enter(a, v, preferDriver = true) {
    let i = preferDriver && !v.seats[0] ? 0 : v.seats.findIndex((s, k) => !s && k > 0);
    if (i < 0) i = v.seats.findIndex(s => !s);
    if (i < 0) return false;
    v.seats[i] = a; a.vehicle = v; a.seat = i; a.state = 'vehicle'; a.action = null; a.vy = 0;
    v.sync(0);
    return true;
  }

  exit(a, thrown = false) {
    const v = a.vehicle; if (!v) return;
    v.seats[a.seat] = null; a.vehicle = null;
    const side = v.def.seats[a.seat][0] <= 0 ? -1 : 1, c = Math.cos(v.yaw), s = Math.sin(v.yaw);
    const ox = side * (v.def.wid / 2 + (thrown ? 1.6 : 0.9)), oz = v.def.seats[a.seat][2];
    const x = v.pos.x + ox * c + oz * s, z = v.pos.z - ox * s + oz * c;
    a.pos.set(x, this.world.getHeight(x, z), z);
    a.state = 'ground'; a.grounded = true; a.vy = 0;
  }

  ejectAll() { for (const v of this.list) for (const a of v.seats) if (a) this.exit(a); }

  hitsVehicle(v) {
    for (const o of this.list) {
      if (o === v || Math.abs(o.pos.x - v.pos.x) > 8 || Math.abs(o.pos.z - v.pos.z) > 8) continue;
      for (const a of v.circles()) for (const b of o.circles()) if ((a.x - b.x) ** 2 + (a.z - b.z) ** 2 < (a.r + b.r) ** 2) { o.speed += v.speed * 0.3; return true; }
    }
    return false;
  }

  /** keeps walkers out of vehicle bodies */
  pushOut(p, radius) {
    for (const v of this.list) {
      if (Math.abs(v.pos.x - p.x) > 5 || Math.abs(v.pos.z - p.z) > 5) continue;
      for (const c of v.circles()) {
        const dx = p.x - c.x, dz = p.z - c.z, d = Math.hypot(dx, dz), m = c.r + radius;
        if (d < m && d > 1e-4) { p.x = c.x + dx / d * m; p.z = c.z + dz / d * m; }
      }
    }
  }

  runOver(v) {
    const now = this.game.time;
    for (const a of this.game.actors) {
      if (!a.alive || a.state !== 'ground' || a.vehicle) continue;
      if (Math.abs(a.pos.x - v.pos.x) > 4 || Math.abs(a.pos.z - v.pos.z) > 4) continue;
      const l = v.toLocal(a.pos.x, a.pos.z);
      if (Math.abs(l.x) > v.def.wid / 2 + 0.3 || Math.abs(l.z) > v.def.len / 2 + 0.3) continue;
      if ((this.hitCd.get(a) || 0) > now) continue;
      this.hitCd.set(a, now + 0.6);
      const driver = v.driver;
      if (driver && a.team === driver.team && this.game.teamSize > 1) continue;
      a.takeDamage(Math.abs(v.speed) * 5, 'body', driver, 'Vehicle', true);
      const side = l.x >= 0 ? 1 : -1, c = Math.cos(v.yaw), s = Math.sin(v.yaw);
      a.pos.x += side * 1.5 * c; a.pos.z -= side * 1.5 * s;
      v.speed *= 0.85;
    }
  }

  rayHit(o, dir, maxT) {
    let best = null, bt = maxT;
    for (const v of this.list) {
      if (Math.abs(v.pos.x - o.x) > bt + 6 && Math.abs(v.pos.z - o.z) > bt + 6) continue;
      const t = v.rayHit(o, dir, bt);
      if (t < bt) { bt = t; best = v; }
    }
    return best ? { v: best, t: bt } : null;
  }

  blast(x, y, z, radius, dmg, attacker) {
    for (const v of this.list) {
      const d = Math.hypot(v.pos.x - x, v.pos.y + 1 - y, v.pos.z - z);
      if (d < radius + v.def.len / 2) v.damage(dmg * 1.5 * Math.max(0.2, 1 - d / (radius + v.def.len / 2)), attacker);
    }
  }
}
