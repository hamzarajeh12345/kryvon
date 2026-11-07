import * as THREE from 'three';
import { HALF, GRID_N, CELL, ZONES, VILLAGES, MAP_SEED, STYLES, ROADS, MAP_SCALE as S } from './config.js';
import { buildLandmarks } from './landmarks.js';
import { buildStreets, buildRoadMesh } from './streets.js';
import { propGeometry, propReady } from './props.js';

// ---------- math / noise ----------
export function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
function hash2(ix, iy, seed) {
  let h = Math.imul(ix, 374761393) ^ Math.imul(iy, 668265263) ^ Math.imul(seed, 1274126177);
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  h ^= h >>> 16;
  return (h >>> 0) / 4294967296;
}
function vnoise(x, y, seed) {
  const ix = Math.floor(x), iy = Math.floor(y);
  const fx = x - ix, fy = y - iy;
  const sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
  const a = hash2(ix, iy, seed), b = hash2(ix + 1, iy, seed), c = hash2(ix, iy + 1, seed), d = hash2(ix + 1, iy + 1, seed);
  return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
}
export function fbm(x, y, seed, oct = 4) {
  let s = 0, amp = 1, f = 1, norm = 0;
  for (let i = 0; i < oct; i++) { s += amp * vnoise(x * f, y * f, seed + i * 17); norm += amp; amp *= 0.5; f *= 2; }
  return s / norm;
}
export const clamp = (v, a, b) => (v < a ? a : v > b ? b : v);
export const lerp = (a, b, t) => a + (b - a) * t;
export const smooth = (e0, e1, x) => { const t = clamp((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t); };
const gauss = (x, z, cx, cz, r) => Math.exp(-((x - cx) ** 2 + (z - cz) ** 2) / (r * r));

// terrain is designed on ±800 m ("design space") and stretched by MAP_SCALE
const DS = { x: -70, z: 230, rx: 55, rz: 130 };
const deadSeaD0 = (x, z) => Math.hypot((x - DS.x) / DS.rx, (z - DS.z) / DS.rz);
const coastX0 = z => -560 + 45 * Math.sin(z * 0.004 + 0.6) + 50 * (fbm(z * 0.006, 3.3, 7, 3) - 0.5);
export const DEAD_SEA = { x: DS.x * S, z: DS.z * S, rx: DS.rx * S, rz: DS.rz * S };
export const deadSeaD = (x, z) => deadSeaD0(x / S, z / S);
export const coastX = z => coastX0(z / S) * S;

const PETRA = ZONES.find(z => z.id === 'petra');
const ALEPPO = ZONES.find(z => z.id === 'aleppo');

export function naturalHeight(x, z) { return naturalHeight0(x / S, z / S); }
function naturalHeight0(x, z) {
  let h = 5 + 9 * fbm(x * 0.004 + 10, z * 0.004 + 10, 1, 4);
  h += 75 * gauss(x, z, -420, -600, 220) * (0.55 + 0.45 * fbm(x * 0.02, z * 0.02, 2, 3)); // Lebanon / cedar range
  h += 18 * gauss(x, z, 90, -600, 260);
  h += 26 * gauss(x, z, 320, -170, 170);   // Ajloun hills
  h += 22 * gauss(x, z, -300, 140, 170);   // Judean hills
  h += 24 * gauss(x, z, 270, 120, 160);    // Amman's jabals
  h += 30 * gauss(x, z, 340, 560, 210);    // Shara highlands
  h += 6 * fbm(x * 0.03, z * 0.03, 5, 2);
  const d = deadSeaD0(x, z);
  if (z > -260) { // Jordan rift valley
    const v = Math.exp(-((x + 70) ** 2) / (85 * 85)) * smooth(-260, -120, z);
    const floor = lerp(-6, 3, smooth(1.4, 2.4, d));
    h = lerp(h, floor, v);
  }
  if (d < 1.7) h = lerp(-22, h, smooth(0.75, 1.7, d)); // Dead Sea basin
  const dc = x - coastX0(z);
  h = lerp(-10, h, smooth(-50, 70, dc));             // Mediterranean
  return h;
}

export const ALL_ZONES = [...ZONES, ...VILLAGES];

// ---------- surface detail texture (multiplied over vertex colours) ----------
export const DETAIL = (() => {
  if (typeof document === 'undefined') return null;
  const S = 256, c = document.createElement('canvas'); c.width = c.height = S;
  const g = c.getContext('2d'), img = g.createImageData(S, S);
  for (let j = 0; j < S; j++) for (let i = 0; i < S; i++) {
    // tileable value noise: sample on a torus
    const u = i / S * Math.PI * 2, v = j / S * Math.PI * 2;
    const n = fbm(Math.cos(u) * 6 + 20, Math.sin(u) * 6 + Math.cos(v) * 6, 91, 4) * 0.7 + fbm(Math.sin(v) * 14, Math.cos(u) * 14 + 7, 17, 2) * 0.3;
    const k = (0.8 + n * 0.22 + (Math.random() - 0.5) * 0.05) * 255;
    const o = (j * S + i) * 4; img.data[o] = img.data[o + 1] = img.data[o + 2] = Math.min(255, k); img.data[o + 3] = 255;
  }
  g.putImageData(img, 0, 0);
  const t = new THREE.CanvasTexture(c);
  t.wrapS = t.wrapT = THREE.RepeatWrapping; t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 4;
  return t;
})();

// ---------- World ----------
export class World {
  constructor(scene, preset) {
    this.scene = scene;
    this.zones = ALL_ZONES;
    this.N = GRID_N;
    this.heights = new Float32Array(this.N * this.N);
    this.colliders = [];
    this.buildings = [];
    this.lootSpots = [];
    this.boxes = [];
    this.cyls = [];
    this.landmarks = [];
    this.streets = [];
    this.matCache = new Map();
    this.stamp = 0;
    this.flat = ALL_ZONES.filter(z => z.fr > 0).map(z => ({ x: z.x, z: z.z, fr: z.fr, fh: Math.max(3, naturalHeight(z.x, z.z)) }));
    this.buildHeights();
    this.buildRoads();
    this.buildTerrain();
    this.buildWater();
    const rng = mulberry32(MAP_SEED);
    this.buildStructures(rng);
    this.buildPropHouses(mulberry32(MAP_SEED + 77));
    this.roadMesh = buildRoadMesh(this);
    this.buildRocks(rng);
    this.buildTrees(rng, preset);
    this.buildBoxMesh();
    this.buildCylMesh();
    this.buildGrid();
  }

  rawHeight(x, z) {
    let h = naturalHeight(x, z);
    for (const f of this.flat) {
      const dx = x - f.x, dz = z - f.z;
      const lim = f.fr * 1.4;
      if (dx * dx + dz * dz < lim * lim) h = lerp(h, f.fh, smooth(lim, f.fr * 0.85, Math.sqrt(dx * dx + dz * dz)));
    }
    h += 16 * smooth(72, 50, Math.hypot(x - ALEPPO.x, z - ALEPPO.z)); // citadel mound
    const dp = Math.hypot(x - PETRA.x, z - PETRA.z);
    if (dp < 170) { // Petra rock massif, Treasury basin and the Siq
      let rock = 34 * smooth(168, 122, dp) * (0.85 + 0.3 * fbm(x * 0.05, z * 0.05, 9, 2));
      rock *= smooth(36, 50, dp);
      const sx = x - PETRA.x;
      if (sx < -30 && sx > -180) {
        const cz = PETRA.z + 10 * Math.sin(sx * 0.045);
        rock *= smooth(5, 10, Math.abs(z - cz));
      }
      h += rock;
    }
    return h;
  }

  buildHeights() {
    const N = this.N;
    for (let iz = 0; iz < N; iz++) for (let ix = 0; ix < N; ix++)
      this.heights[iz * N + ix] = this.rawHeight(-HALF + ix * CELL, -HALF + iz * CELL);
  }

  // road mask on the height grid (0..1), rasterised from gently wobbling polylines between towns
  buildRoads() {
    const N = this.N, mask = this.roadMask = new Float32Array(N * N), byId = id => ALL_ZONES.find(z => z.id === id);
    this.roadLines = [];
    ROADS.forEach(([a, b], ri) => {
      const A = byId(a), B = byId(b); if (!A || !B) return;
      const len = Math.hypot(B.x - A.x, B.z - A.z), n = Math.max(4, Math.ceil(len / 20));
      const px = -(B.z - A.z) / len, pz = (B.x - A.x) / len, pts = [];
      for (let i = 0; i <= n; i++) {
        const t = i / n, w = (fbm(t * 3 + ri * 1.7, ri * 0.9, 77, 3) - 0.5) * 0.22 * len * Math.sin(Math.PI * t);
        pts.push([A.x + (B.x - A.x) * t + px * w, A.z + (B.z - A.z) * t + pz * w]);
      }
      this.roadLines.push({ pts, major: !A.minor && !B.minor });
      for (let i = 0; i < pts.length - 1; i++) {
        const [x0, z0] = pts[i], [x1, z1] = pts[i + 1], sl = Math.hypot(x1 - x0, z1 - z0), steps = Math.ceil(sl / 2);
        for (let k = 0; k <= steps; k++) {
          const x = x0 + (x1 - x0) * k / steps, z = z0 + (z1 - z0) * k / steps;
          if (this.heights[this.gi(x, z)] < this.waterLevel(x, z) + 0.3) continue;
          const gx = Math.round((x + HALF) / CELL), gz = Math.round((z + HALF) / CELL);
          for (let dz = -2; dz <= 2; dz++) for (let dx = -2; dx <= 2; dx++) {
            const ix = gx + dx, iz = gz + dz; if (ix < 0 || iz < 0 || ix >= N || iz >= N) continue;
            const d = Math.hypot(-HALF + ix * CELL - x, -HALF + iz * CELL - z), v = smooth(7.5, 2.5, d);
            if (v > mask[iz * N + ix]) mask[iz * N + ix] = v;
          }
        }
      }
    });
  }
  gi(x, z) { return clamp(Math.round((z + HALF) / CELL), 0, this.N - 1) * this.N + clamp(Math.round((x + HALF) / CELL), 0, this.N - 1); }
  roadAt(x, z) {
    if (!this.roadMask) return 0;
    const N = this.N, gx = clamp((x + HALF) / CELL, 0, N - 1.001), gz = clamp((z + HALF) / CELL, 0, N - 1.001);
    const ix = gx | 0, iz = gz | 0, fx = gx - ix, fz = gz - iz, m = this.roadMask, i = iz * N + ix;
    return (m[i] * (1 - fx) + m[i + 1] * fx) * (1 - fz) + (m[i + N] * (1 - fx) + m[i + N + 1] * fx) * fz;
  }

  getHeight(x, z) {
    const N = this.N;
    const gx = clamp((x + HALF) / CELL, 0, N - 1.001), gz = clamp((z + HALF) / CELL, 0, N - 1.001);
    const ix = gx | 0, iz = gz | 0, fx = gx - ix, fz = gz - iz;
    const h = this.heights, i = iz * N + ix;
    const a = h[i], b = h[i + 1], c = h[i + N], d = h[i + N + 1];
    return a + (b - a) * fx + (c - a) * fz + (a - b - c + d) * fx * fz;
  }
  waterLevel(x, z) { return deadSeaD(x, z) < 2.2 ? -10 : 0; }
  isWater(x, z) { return this.getHeight(x, z) < this.waterLevel(x, z); }
  inMap(x, z) { return Math.abs(x) < HALF - 2 && Math.abs(z) < HALF - 2; }

  biomeColor(x, z, h, ny, out) {
    let r = 0.62, g = 0.58, b = 0.42;
    const mix = (cr, cg, cb, t) => { r += (cr - r) * t; g += (cg - g) * t; b += (cb - b) * t; };
    const wx = x, wz = z; x /= S; z /= S; // design space for the biome regions
    mix(0.33, 0.46, 0.25, smooth(-330, -470, z) * smooth(-60, -220, x));           // green north-west
    mix(0.80, 0.68, 0.47, smooth(200, 330, x) * smooth(-200, -320, z));            // Badia
    mix(0.82, 0.63, 0.43, smooth(400, 480, z) * smooth(-260, -160, x));            // Wadi Araba sands
    mix(0.50, 0.55, 0.30, smooth(260, 360, z) * smooth(-120, -220, x));            // olive plains
    mix(0.74, 0.45, 0.37, smooth(175, 125, Math.hypot(wx - PETRA.x, wz - PETRA.z))); // Petra rose rock
    if (this.waterLevel(wx, wz) === 0) mix(0.86, 0.79, 0.6, smooth(3.5, 1.2, h));    // beach
    else mix(0.85, 0.8, 0.7, smooth(-6, -9.5, h));                                  // salt shore
    mix(0.5, 0.45, 0.38, smooth(0.86, 0.7, ny));                                    // steep rock
    mix(0.95, 0.96, 0.98, smooth(54, 62, h) * smooth(-150, -300, z));               // snow caps (northern mountains only)
    x = wx; z = wz;
    for (const f of this.flat) {
      const d = Math.hypot(x - f.x, z - f.z);
      if (d < f.fr) mix(0.63, 0.6, 0.55, 0.45 * smooth(f.fr, f.fr * 0.6, d));
    }
    const rd = this.roadAt ? this.roadAt(x, z) : 0;
    if (rd > 0) mix(0.55, 0.5, 0.42, rd * 0.85);                                    // roads
    const n = 0.9 + 0.2 * fbm(x * 0.08, z * 0.08, 11, 2);
    out.setRGB(r * n, g * n, b * n);
  }

  buildTerrain() {
    const N = this.N;
    const geo = new THREE.PlaneGeometry(HALF * 2, HALF * 2, N - 1, N - 1);
    geo.rotateX(-Math.PI / 2);
    const pos = geo.attributes.position;
    for (let i = 0; i < pos.count; i++) pos.setY(i, this.heights[i]);
    geo.computeVertexNormals();
    const nrm = geo.attributes.normal;
    const colors = new Float32Array(pos.count * 3);
    const c = new THREE.Color();
    for (let i = 0; i < pos.count; i++) {
      this.biomeColor(pos.getX(i), pos.getZ(i), pos.getY(i), nrm.getY(i), c);
      colors[i * 3] = c.r; colors[i * 3 + 1] = c.g; colors[i * 3 + 2] = c.b;
    }
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    const tex = DETAIL ? DETAIL.clone() : null;
    if (tex) { tex.needsUpdate = true; tex.repeat.set(220, 220); }
    const mesh = new THREE.Mesh(geo, new THREE.MeshLambertMaterial({ vertexColors: true, map: tex }));
    mesh.receiveShadow = true;
    this.scene.add(mesh);
    this.terrain = mesh;
  }

  buildWater() {
    const sea = new THREE.Mesh(new THREE.PlaneGeometry(2400, 4000),
      new THREE.MeshPhongMaterial({ color: 0x1f6f8b, transparent: true, opacity: 0.88, shininess: 90, specular: 0x88bbcc }));
    sea.rotation.x = -Math.PI / 2; sea.position.set(-1600, 0, 0);
    this.scene.add(sea);
    const ds = new THREE.Mesh(new THREE.CircleGeometry(1, 48),
      new THREE.MeshPhongMaterial({ color: 0x3f9fb0, transparent: true, opacity: 0.9, shininess: 120, specular: 0xbbe0e6 }));
    ds.rotation.x = -Math.PI / 2; ds.scale.set(DEAD_SEA.rx * 1.6, DEAD_SEA.rz * 1.6, 1);
    ds.position.set(DEAD_SEA.x, -10, DEAD_SEA.z);
    this.scene.add(ds);
    this.waterMeshes = [sea, ds];
  }

  // ---------- structures ----------
  addBox(cx, cy, cz, sx, sy, sz, color, collide = true) {
    this.boxes.push({ cx, cy, cz, sx, sy, sz, color });
    if (collide) this.colliders.push({ minX: cx - sx / 2, maxX: cx + sx / 2, minY: cy - sy / 2, maxY: cy + sy / 2, minZ: cz - sz / 2, maxZ: cz + sz / 2, s: 0 });
  }
  // vertical cylinder from y0 up by h (instanced); collider is the inscribed box
  addCyl(x, y0, z, r, h, color, collide = true) {
    this.cyls.push({ x, y: y0 + h / 2, z, r, h, color });
    if (collide) { const k = r * 0.85; this.colliders.push({ minX: x - k, maxX: x + k, minY: y0, maxY: y0 + h, minZ: z - k, maxZ: z + k, s: 0 }); }
  }
  // a one-off decorative mesh (domes, rocks, roofs …)
  addMesh(geo, color, x, y, z, o = {}) {
    if (o.jitter) {
      const p = geo.attributes.position, seen = new Map();
      for (let i = 0; i < p.count; i++) {
        const k = `${p.getX(i).toFixed(2)},${p.getY(i).toFixed(2)},${p.getZ(i).toFixed(2)}`;
        let j = seen.get(k); if (!j) seen.set(k, j = [1 + (Math.random() - 0.5) * o.jitter * 2, 1 + (Math.random() - 0.5) * o.jitter * 2]);
        p.setX(i, p.getX(i) * j[0]); p.setZ(i, p.getZ(i) * j[1]);
      }
      geo.computeVertexNormals();
    }
    const key = `${color}|${o.emissive || 0}|${o.flat ? 1 : 0}|${o.side || 0}`;
    let mat = this.matCache.get(key);
    if (!mat) this.matCache.set(key, mat = new THREE.MeshLambertMaterial({ color, emissive: o.emissive || 0, flatShading: !!o.flat, side: o.side || THREE.FrontSide }));
    const m = new THREE.Mesh(geo, mat);
    m.position.set(x, y, z); m.castShadow = true; m.receiveShadow = true;
    this.scene.add(m);
    return m;
  }
  loot(x, z, tier, type, zone) { this.lootSpots.push({ x, y: this.getHeight(x, z), z, tier, type, zone }); }

  minH(x0, z0, x1, z1) {
    let m = Infinity, M = -Infinity;
    for (let i = 0; i <= 3; i++) for (let j = 0; j <= 3; j++) {
      const h = this.getHeight(lerp(x0, x1, i / 3), lerp(z0, z1, j / 3));
      if (h < m) m = h; if (h > M) M = h;
    }
    return [m, M];
  }
  // a straight wall split into terrain-following chunks
  wallLine(x0, z0, x1, z1, h, thick, color) {
    const len = Math.hypot(x1 - x0, z1 - z0);
    const n = Math.max(1, Math.ceil(len / 10));
    for (let i = 0; i < n; i++) {
      const ax = lerp(x0, x1, i / n), az = lerp(z0, z1, i / n), bx = lerp(x0, x1, (i + 1) / n), bz = lerp(z0, z1, (i + 1) / n);
      const [m, M] = this.minH(ax, az, bx, bz);
      const bottom = m - 3, top = M + h;
      const sx = Math.abs(bx - ax) || thick, sz = Math.abs(bz - az) || thick;
      this.addBox((ax + bx) / 2, (bottom + top) / 2, (az + bz) / 2, sx + (sx === thick ? 0 : 0.01), top - bottom, sz, color);
    }
  }
  wallRing(cx, cz, half, h, thick, gate, color, occ) {
    const g = gate / 2;
    const segs = [
      [cx - half, cz - half, cx - g, cz - half], [cx + g, cz - half, cx + half, cz - half],
      [cx - half, cz + half, cx - g, cz + half], [cx + g, cz + half, cx + half, cz + half],
      [cx - half, cz - half, cx - half, cz - g], [cx - half, cz + g, cx - half, cz + half],
      [cx + half, cz - half, cx + half, cz - g], [cx + half, cz + g, cx + half, cz + half],
    ];
    for (const s of segs) this.wallLine(...s, h, thick, color);
    // corner towers
    for (const [tx, tz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) {
      const x = cx + tx * half, z = cz + tz * half, [m] = this.minH(x - 2, z - 2, x + 2, z + 2);
      this.addBox(x, m + (h + 5) / 2 - 1.5, z, thick * 2.6, h + 5 + 3, thick * 2.6, color);
    }
    occ.push({ minX: cx - half - 3, maxX: cx + half + 3, minZ: cz - half - 3, maxZ: cz + half + 3, ring: true, inner: half - 3 });
  }

  overlaps(r, occ, gap) {
    for (const o of occ) {
      if (o.ring) { // only the wall band is blocked
        const inX = r.minX > o.minX + 6 + gap && r.maxX < o.maxX - 6 - gap && r.minZ > o.minZ + 6 + gap && r.maxZ < o.maxZ - 6 - gap;
        const outX = r.maxX < o.minX - gap || r.minX > o.maxX + gap || r.maxZ < o.minZ - gap || r.minZ > o.maxZ + gap;
        if (!inX && !outX) return true;
        continue;
      }
      if (r.minX < o.maxX + gap && r.maxX > o.minX - gap && r.minZ < o.maxZ + gap && r.maxZ > o.minZ - gap) return true;
    }
    return false;
  }

  addBuilding(x, z, w, d, h, st, type, zone, rng) {
    const minX = x - w / 2, maxX = x + w / 2, minZ = z - d / 2, maxZ = z + d / 2;
    const [base] = this.minH(minX, minZ, maxX, maxZ);
    const top = base + h, bottom = base - 2, t = 0.35, doorW = 2.2, doorH = 2.6;
    const color = st.wall[(rng() * st.wall.length) | 0];
    const doorSide = (rng() * 4) | 0;
    const sides = [
      { ax: 'x', fixed: minZ + t / 2, a: minX, b: maxX, out: -1 },
      { ax: 'x', fixed: maxZ - t / 2, a: minX, b: maxX, out: 1 },
      { ax: 'z', fixed: minX + t / 2, a: minZ + t, b: maxZ - t, out: -1 },
      { ax: 'z', fixed: maxX - t / 2, a: minZ + t, b: maxZ - t, out: 1 },
    ];
    const piece = (s, a, b, y0, y1, col = color, collide = true, thick = t, offset = 0) => {
      if (b - a < 0.05) return;
      const c = (a + b) / 2, len = b - a, cy = (y0 + y1) / 2, sy = y1 - y0;
      if (s.ax === 'x') this.addBox(c, cy, s.fixed + offset, len, sy, thick, col, collide);
      else this.addBox(s.fixed + offset, cy, c, thick, sy, len, col, collide);
    };
    const signColors = { hospital: 0xd23b3b, police: 0x2f5fb3, shop: 0x2f9e5b, school: 0xd9a520 };
    sides.forEach((s, i) => {
      let gap = null;
      if (i === doorSide) {
        const c = (s.a + s.b) / 2 + (rng() - 0.5) * Math.max(0, s.b - s.a - doorW - 2);
        gap = [c - doorW / 2, c + doorW / 2];
        piece(s, s.a, gap[0], bottom, top);
        piece(s, gap[1], s.b, bottom, top);
        piece(s, gap[0], gap[1], base + doorH, top);
        if (signColors[type]) piece(s, gap[0] - 0.4, gap[1] + 0.4, base + doorH + 0.1, base + doorH + 0.8, signColors[type], false, 0.12, s.out * 0.24);
      } else piece(s, s.a, s.b, bottom, top);
      // windows (decor)
      const len = s.b - s.a, n = Math.max(1, Math.floor(len / 4));
      for (let k = 0; k < n; k++) {
        const c = s.a + (k + 0.5) * len / n;
        if (gap && c > gap[0] - 1.2 && c < gap[1] + 1.2) continue;
        for (let fl = 0; fl < (h > 6.5 ? 2 : 1); fl++)
          piece(s, c - 0.6, c + 0.6, base + 1.4 + fl * 3, base + 2.5 + fl * 3, 0x2a3138, false, 0.08, s.out * 0.2);
      }
    });
    this.addBox(x, top + 0.15, z, w, 0.3, d, st.roof);
    // low parapet – flat Levantine roofs
    this.addBox(x, top + 0.5, minZ + 0.1, w, 0.5, 0.2, color, false);
    this.addBox(x, top + 0.5, maxZ - 0.1, w, 0.5, 0.2, color, false);
    this.addBox(minX + 0.1, top + 0.5, z, 0.2, 0.5, d, color, false);
    this.addBox(maxX - 0.1, top + 0.5, z, 0.2, 0.5, d, color, false);
    this.decorateBuilding({ x, z, w, d, minX, maxX, minZ, maxZ, base, top, h, color, st, type, zone, doorSide, sides }, rng);
    const b = { minX, maxX, minZ, maxZ, base, top, type, zone };
    this.buildings.push(b);
    const nLoot = { 1: 1 + (rng() < 0.6 ? 1 : 0), 2: 2 + (rng() < 0.5 ? 1 : 0), 3: 3 + (rng() < 0.6 ? 1 : 0) }[zone.tier] + (w * d > 110 ? 1 : 0);
    for (let k = 0; k < nLoot; k++) {
      const lx = lerp(minX + 1.1, maxX - 1.1, rng()), lz = lerp(minZ + 1.1, maxZ - 1.1, rng());
      this.lootSpots.push({ x: lx, y: this.getHeight(lx, lz), z: lz, tier: zone.tier, type, zone: zone.id });
    }
  }

  // floor bands, balconies, AC units, awnings, water tanks, solar heaters, dishes (decor only – no colliders)
  decorateBuilding(B, rng) {
    const { x, z, w, d, minX, maxX, minZ, maxZ, base, top, h, color, st, type, zone, doorSide, sides } = B;
    const darker = new THREE.Color(color).multiplyScalar(0.82).getHex();
    const modern = ['beirut', 'amman', 'resort'].includes(zone.style), old = ['damascus', 'jerusalem', 'citadel'].includes(zone.style);
    const floors = Math.max(1, Math.round(h / 3.1));
    // floor bands and a base course
    for (let f = 1; f < floors; f++) {
      const y = base + f * 3.05;
      this.addBox(x, y, minZ - 0.06, w + 0.12, 0.18, 0.12, darker, false); this.addBox(x, y, maxZ + 0.06, w + 0.12, 0.18, 0.12, darker, false);
      this.addBox(minX - 0.06, y, z, 0.12, 0.18, d + 0.12, darker, false); this.addBox(maxX + 0.06, y, z, 0.12, 0.18, d + 0.12, darker, false);
    }
    this.addBox(x, base + 0.3, minZ - 0.05, w + 0.1, 0.6, 0.1, darker, false); this.addBox(x, base + 0.3, maxZ + 0.05, w + 0.1, 0.6, 0.1, darker, false);
    sides.forEach((s, i) => {
      const len = s.b - s.a, mid = (s.a + s.b) / 2;
      const at = (along, y, out, sx, sy, sz, col) => {   // place a box on this façade
        if (s.ax === 'x') this.addBox(along, y, s.fixed + s.out * out, sx, sy, sz, col, false);
        else this.addBox(s.fixed + s.out * out, y, along, sz, sy, sx, col, false);
      };
      // balcony on upper floors (modern + some old houses), never above the door
      if (floors >= 2 && i !== doorSide && rng() < (modern ? 0.75 : 0.35)) {
        for (let f = 1; f < floors; f++) {
          const y = base + f * 3.05 + 0.05, bw = Math.min(len * 0.6, 4.2);
          at(mid, y, 0.75, bw, 0.15, 1.3, 0xd8d2c6);
          at(mid, y + 0.55, 1.35, bw, 0.9, 0.06, modern ? 0x2a2d30 : 0x5a3a24);        // railing
          if (rng() < 0.4) at(mid - bw * 0.3, y + 1.4, 0.6, bw * 0.4, 1.6, 0.05, [0xc0392b, 0x2f6fae, 0xe8c547, 0x3f8f5a][(rng() * 4) | 0]); // washing / awning cloth
        }
      }
      // AC units under some windows
      if (modern && rng() < 0.6) at(s.a + len * (0.2 + rng() * 0.6), base + 1 + Math.floor(rng() * floors) * 3.05, 0.35, 0.8, 0.55, 0.45, 0xe6e6e2);
      // shop awning / clinic cross / police light over the door
      if (i === doorSide) {
        const awn = { shop: 0x2f9e5b, hospital: 0xffffff, police: 0x2f5fb3 }[type];
        if (awn != null) at(mid, base + 3.0, 0.7, Math.min(len, 3.6), 0.12, 1.3, awn);
        if (type === 'hospital') { at(mid, base + 3.6, 0.2, 0.7, 0.22, 0.06, 0xd23b3b); at(mid, base + 3.6, 0.2, 0.22, 0.7, 0.06, 0xd23b3b); }
        if (old) at(mid, base + 2.75, 0.18, 2.8, 0.25, 0.1, darker);                         // stone lintel
      }
    });
    // roof life: black water tanks (+ stands), solar heaters, dishes, a stair hut
    const tanks = 1 + ((rng() * (w * d > 90 ? 3 : 2)) | 0);
    for (let k = 0; k < tanks; k++) {
      const tx = lerp(minX + 1, maxX - 1, rng()), tz = lerp(minZ + 1, maxZ - 1, rng());
      this.addCyl(tx, top + 0.3, tz, 0.55, 1.1, rng() < 0.75 ? 0x1c1c1e : 0xd8d0c0, false);
      this.addBox(tx, top + 0.3 + 1.15, tz, 0.3, 0.12, 0.3, 0x333333, false);
    }
    if (modern || rng() < 0.4) {
      const sx = lerp(minX + 1.5, maxX - 1.5, rng()), sz = lerp(minZ + 1.5, maxZ - 1.5, rng());
      this.boxes.push({ cx: sx, cy: top + 0.9, cz: sz, sx: 1.6, sy: 0.06, sz: 1.1, color: 0x1f3a5a, rotY: rng() * 3 });           // solar panel (tilted look via thin slab)
      this.addCyl(sx + 0.9, top + 0.9, sz, 0.22, 1.1, 0xdddddd, false);
      this.addBox(sx, top + 0.6, sz, 0.08, 0.6, 0.08, 0x777777, false);
    }
    if (rng() < 0.5) { // satellite dish
      const dx = lerp(minX + 0.8, maxX - 0.8, rng()), dz = lerp(minZ + 0.8, maxZ - 0.8, rng());
      this.addBox(dx, top + 0.6, dz, 0.06, 0.6, 0.06, 0x888888, false);
      this.addCyl(dx, top + 0.85, dz, 0.4, 0.06, 0xe8e8e8, false);
    }
    if (floors >= 2 && w * d > 70 && rng() < 0.5) this.addBox(lerp(minX + 1.6, maxX - 1.6, rng()), top + 1.2, lerp(minZ + 1.6, maxZ - 1.6, rng()), 2.4, 2.1, 2.4, color, false); // stair hut
    if (old && rng() < 0.25) { // little dome on old-city houses
      const g = new THREE.SphereGeometry(1.3, 12, 8, 0, Math.PI * 2, 0, Math.PI / 2);
      this.addMesh(g, 0xd8ccb0, x, top + 0.3, z);
    }
  }

  buildStructures(rng) {
    for (const zone of this.zones) {
      const st = STYLES[zone.style];
      const occ = [];
      if (zone.id === 'jerusalem') this.wallRing(zone.x, zone.z, 78, 9, 2.2, 10, 0xd9c89c, occ);
      if (zone.id === 'aleppo') this.wallRing(zone.x, zone.z, 34, 7, 1.8, 7, 0xb19a72, occ);
      if (zone.id === 'jerash') {
        const cx = zone.x + 50, cz = zone.z - 45;               // Ajloun-style castle
        this.wallRing(cx, cz, 16, 8, 1.6, 4, 0x9c8a6a, occ);
        const [m] = this.minH(cx - 4, cz - 4, cx + 4, cz + 4);
        this.addBox(cx, m + 6, cz, 7, 14, 7, 0x8f7e60);
        this.lootSpots.push({ x: cx + 8, y: this.getHeight(cx + 8, cz + 8), z: cz + 8, tier: 3, type: 'police', zone: 'jerash' });
        // Roman colonnade (cover)
        for (let i = 0; i < 12; i++) for (const off of [-5, 5]) {
          const px = zone.x - 60 + i * 6, pz = zone.z + 30 + off;
          this.addBox(px, this.getHeight(px, pz) + 3, pz, 0.9, 6, 0.9, 0xd9c7a0);
        }
        occ.push({ minX: zone.x - 63, maxX: zone.x + 10, minZ: zone.z + 22, maxZ: zone.z + 38 });
      }
      if (zone.id === 'petra') { // the Treasury façade carved into the basin wall
        const fx = zone.x + 40, fz = zone.z, g = this.getHeight(zone.x + 30, fz);
        this.addBox(fx, g + 11, fz, 5, 24, 22, 0xc98068);
        for (let i = 0; i < 6; i++) this.addBox(fx - 3.4, g + 4, fz - 7.5 + i * 3, 0.9, 8, 0.9, 0xd8957a);
        this.addBox(fx - 3.4, g + 8.5, fz, 1.4, 1, 18, 0xd8957a, false);
        this.addBox(fx - 3.6, g + 15, fz, 2.4, 6, 4.5, 0xd8957a);
        for (let k = 0; k < 4; k++) this.lootSpots.push({ x: fx - 7 - k, y: g, z: fz - 4 + k * 3, tier: 3, type: 'police', zone: 'petra' });
        occ.push({ minX: fx - 8, maxX: fx + 3, minZ: fz - 12, maxZ: fz + 12 });
      }
      buildLandmarks(this, zone, occ, rng);
      buildStreets(this, zone, occ);
      const nSchool = ['beirut', 'amman', 'damascus', 'jerusalem'].includes(zone.style) ? 2 : ['village', 'jerash', 'citadel', 'mountain', 'resort'].includes(zone.style) ? 1 : 0;
      for (let k = 0; k < nSchool; k++) this.buildSchool(zone, occ, rng);
      let placed = 0;
      for (let tries = 0; tries < zone.buildings * 40 && placed < zone.buildings; tries++) {
        const a = rng() * Math.PI * 2, rr = Math.sqrt(rng()) * zone.br;
        const x = zone.x + Math.cos(a) * rr, z = zone.z + Math.sin(a) * rr;
        const w = lerp(st.size[0], st.size[1], rng()), d = lerp(st.size[0], st.size[1], rng());
        const r = { minX: x - w / 2, maxX: x + w / 2, minZ: z - d / 2, maxZ: z + d / 2 };
        if (this.overlaps(r, occ, st.gap)) continue;
        let wet = false;
        for (const [px, pz] of [[r.minX, r.minZ], [r.maxX, r.minZ], [r.minX, r.maxZ], [r.maxX, r.maxZ], [x, z]])
          if (this.isWater(px, pz) || this.getHeight(px, pz) < this.waterLevel(px, pz) + 1) wet = true;
        if (wet) continue;
        const [m, M] = this.minH(r.minX, r.minZ, r.maxX, r.maxZ);
        if (M - m > 2.2) continue;
        const roll = rng();
        const type = roll < 0.12 ? 'hospital' : roll < 0.24 ? 'police' : roll < 0.36 ? 'shop' : 'house';
        this.addBuilding(x, z, w, d, lerp(st.h[0], st.h[1], rng()), st, type, zone, rng);
        occ.push(r);
        placed++;
      }
      zone.occ = occ;
    }
  }

  // school compound: two-storey main block + side wing, perimeter wall with gate, flag pole, football pitch
  buildSchool(zone, occ, rng) {
    const W = 38, D = 30, st0 = STYLES[zone.style];
    const st = { ...st0, wall: [0xeadcae, 0xf1e4bf, 0xe2d2a0] };
    for (let t = 0; t < 160; t++) {
      const a = rng() * Math.PI * 2, rr = zone.br * (0.3 + rng() * (t < 60 ? 0.8 : 1.3)) + (t < 60 ? 0 : W / 2);
      const cx = zone.x + Math.cos(a) * rr, cz = zone.z + Math.sin(a) * rr;
      const r = { minX: cx - W / 2, maxX: cx + W / 2, minZ: cz - D / 2, maxZ: cz + D / 2 };
      if (this.overlaps(r, occ, 2)) continue;
      let wet = false;
      for (const [px, pz] of [[r.minX, r.minZ], [r.maxX, r.minZ], [r.minX, r.maxZ], [r.maxX, r.maxZ], [cx, cz]])
        if (this.isWater(px, pz) || this.getHeight(px, pz) < this.waterLevel(px, pz) + 1) wet = true;
      if (wet) continue;
      const [m, M] = this.minH(r.minX, r.minZ, r.maxX, r.maxZ);
      if (M - m > 2.5) continue;
      this.addBuilding(cx, r.minZ + 6.5, 28, 10, 7.6, st, 'school', zone, rng);          // main block
      this.addBuilding(r.minX + 6.5, cz + 5, 10, 13, 4.6, st, 'school', zone, rng);      // wing
      const wall = 0xd8cfb8, wh = 2.2, g = this.getHeight(cx, cz);
      this.addBox(cx, m + wh / 2 - 0.5, r.minZ, W, wh + 1, 0.3, wall);
      this.addBox(r.minX, m + wh / 2 - 0.5, cz, 0.3, wh + 1, D, wall);
      this.addBox(r.maxX, m + wh / 2 - 0.5, cz, 0.3, wh + 1, D, wall);
      const gate = 6, side = (W - gate) / 2;                                              // front wall with a gate
      this.addBox(r.minX + side / 2, m + wh / 2 - 0.5, r.maxZ, side, wh + 1, 0.3, wall);
      this.addBox(r.maxX - side / 2, m + wh / 2 - 0.5, r.maxZ, side, wh + 1, 0.3, wall);
      for (const s of [-1, 1]) this.addBox(cx + s * (gate / 2 + 0.3), m + 1.6, r.maxZ, 0.6, 3.6, 0.6, 0xbfb49a);
      this.addBox(cx, m + 3.2, r.maxZ, gate + 1.2, 0.4, 0.4, 0x2f5a8a, false);           // gate sign
      // flag pole and flag (three stripes + red triangle panel)
      const fx = cx + 6, fz = r.maxZ - 3;
      this.addBox(fx, g + 4.5, fz, 0.12, 9, 0.12, 0xdddddd, false);
      [0x111111, 0xf4f4f4, 0x1e7a3a].forEach((c, i) => this.addBox(fx + 1.3, g + 8.4 - i * 0.42, fz, 2.4, 0.42, 0.05, c, false));
      this.addBox(fx + 0.45, g + 7.98, fz, 0.7, 1.26, 0.06, 0xc8102e, false);
      // football pitch: painted lines + two goals
      const px = cx + 5, pz = cz + 4, pw = 20, pd = 12;
      this.addBox(px, g + 0.03, pz, pw, 0.05, pd, 0x7a8a5a, false);
      this.addBox(px, g + 0.06, pz, 0.15, 0.05, pd, 0xeeeeee, false);
      for (const s of [-1, 1]) {
        const gx = px + s * (pw / 2 - 0.3);
        this.addBox(gx, g + 1.1, pz - 1.6, 0.1, 2.2, 0.1, 0xffffff, false);
        this.addBox(gx, g + 1.1, pz + 1.6, 0.1, 2.2, 0.1, 0xffffff, false);
        this.addBox(gx, g + 2.2, pz, 0.1, 0.1, 3.3, 0xffffff, false);
      }
      for (let k = 0; k < 3; k++) {                                                       // loot in the yard too
        const lx = lerp(r.minX + 14, r.maxX - 2, rng()), lz = lerp(cz - 2, r.maxZ - 2, rng());
        this.lootSpots.push({ x: lx, y: this.getHeight(lx, lz), z: lz, tier: Math.max(2, zone.tier), type: 'school', zone: zone.id });
      }
      occ.push(r);
      (this.schools = this.schools || []).push({ x: cx, z: cz, zone: zone.id });
      return true;
    }
    return false;
  }

  // generated 3D houses (solid, not enterable) around towns: apartments in cities, stone villas in villages
  buildPropHouses(rng) {
    const KIND = {
      beirut: [['h_apartment', 5], ['h_villa', 2], ['h_modern', 1]], amman: [['h_apartment', 5], ['h_modern', 2]],
      damascus: [['h_apartment', 1], ['h_villa', 2]], jerusalem: [['h_villa', 2], ['h_apartment', 1]], resort: [['h_modern', 3], ['h_villa', 1]],
      village: [['h_villa', 3], ['h_modern', 1], ['h_apartment', 1]], jerash: [['h_villa', 1]], citadel: [['h_villa', 1]], mountain: [['h_villa', 1]],
    };
    const SIZE = { h_apartment: 13, h_villa: 9, h_modern: 7.5 };
    const geo = {};
    for (const k in SIZE) if (propReady(k)) { geo[k] = propGeometry(k, { height: SIZE[k] }); geo[k].geometry.computeBoundingBox(); }
    if (!Object.keys(geo).length) return;
    const placed = {};
    for (const zone of this.zones) {
      const kinds = (KIND[zone.style] || []).filter(([k]) => geo[k]); if (!kinds.length) continue;
      const total = kinds.reduce((a, b) => a + b[1], 0);
      const want = Math.round(zone.buildings * (zone.style === 'village' ? 0.6 : 0.5)) + 3;
      const occ = zone.occ || [];
      let n = 0;
      for (let t = 0; t < want * 30 && n < want; t++) {
        let pr = rng() * total, key = kinds[0][0]; for (const [k, w] of kinds) { pr -= w; if (pr <= 0) { key = k; break; } }
        const bb = geo[key].geometry.boundingBox, q = (rng() * 4) | 0, yaw = q * Math.PI / 2;
        let w = bb.max.x - bb.min.x, d = bb.max.z - bb.min.z; if (q & 1) [w, d] = [d, w];
        const a = rng() * Math.PI * 2, rr = zone.br * (0.45 + rng() * 0.75);
        const x = zone.x + Math.cos(a) * rr, z = zone.z + Math.sin(a) * rr;
        const r = { minX: x - w / 2, maxX: x + w / 2, minZ: z - d / 2, maxZ: z + d / 2 };
        if (Math.abs(x) > HALF - 20 || Math.abs(z) > HALF - 20 || this.overlaps(r, occ, 3)) continue;
        if ([[x, z], [r.minX, r.minZ], [r.maxX, r.minZ], [r.minX, r.maxZ], [r.maxX, r.maxZ]].some(([px, pz]) => this.roadAt(px, pz) > 0.25)) continue;
        let wet = false;
        for (const [px, pz] of [[r.minX, r.minZ], [r.maxX, r.minZ], [r.minX, r.maxZ], [r.maxX, r.maxZ], [x, z]])
          if (this.isWater(px, pz) || this.getHeight(px, pz) < this.waterLevel(px, pz) + 1) wet = true;
        if (wet) continue;
        const [m, M] = this.minH(r.minX, r.minZ, r.maxX, r.maxZ);
        if (M - m > 2.5) continue;
        (placed[key] = placed[key] || []).push({ x, y: m - 0.25, z, yaw });
        this.colliders.push({ minX: r.minX + 0.3, maxX: r.maxX - 0.3, minY: m - 1, maxY: m + bb.max.y - 0.5, minZ: r.minZ + 0.3, maxZ: r.maxZ - 0.3, s: 0 });
        occ.push(r); n++;
      }
    }
    const M4 = new THREE.Matrix4(), Q = new THREE.Quaternion(), UP = new THREE.Vector3(0, 1, 0), ONE = new THREE.Vector3(1, 1, 1);
    this.propHouses = [];
    for (const k in placed) {
      const list = placed[k], mesh = new THREE.InstancedMesh(geo[k].geometry, geo[k].material, list.length);
      list.forEach((h, i) => mesh.setMatrixAt(i, M4.compose(new THREE.Vector3(h.x, h.y, h.z), Q.setFromAxisAngle(UP, h.yaw), ONE)));
      mesh.castShadow = mesh.receiveShadow = true; mesh.frustumCulled = false;
      this.scene.add(mesh); this.propHouses.push(mesh);
    }
  }

  nearZone(x, z, pad = 1) {
    for (const zn of this.zones) { const d = Math.hypot(x - zn.x, z - zn.z); if (d < zn.br * pad + 12) return true; }
    return false;
  }

  buildRocks(rng) {
    const geo = new THREE.DodecahedronGeometry(1, 0);
    const list = [];
    for (let i = 0; i < 4000 * S * S && list.length < 520 * S * S; i++) {
      const x = (rng() * 2 - 1) * (HALF - 10), z = (rng() * 2 - 1) * (HALF - 10);
      if (this.isWater(x, z) || this.nearZone(x, z)) continue;
      const desert = (x / S > 220 && z / S < -200) || (z / S > 420) || (x / S > 380);
      if (!desert && rng() < 0.65) continue;
      const s = 1.1 + rng() * 2.2;
      list.push({ x, z, s, y: this.getHeight(x, z), rot: rng() * 6.28 });
    }
    const mesh = new THREE.InstancedMesh(geo, new THREE.MeshLambertMaterial({ color: 0xffffff, flatShading: true }), list.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), c = new THREE.Color();
    list.forEach((r, i) => {
      q.setFromEuler(new THREE.Euler(r.rot * 0.3, r.rot, 0));
      m.compose(new THREE.Vector3(r.x, r.y + r.s * 0.35, r.z), q, new THREE.Vector3(r.s, r.s * 0.8, r.s));
      mesh.setMatrixAt(i, m);
      this.biomeColor(r.x, r.z, r.y, 0.6, c); c.multiplyScalar(0.85);
      mesh.setColorAt(i, c);
      const hs = r.s * 0.75;
      this.colliders.push({ minX: r.x - hs, maxX: r.x + hs, minY: r.y - 1, maxY: r.y + r.s * 1.05, minZ: r.z - hs, maxZ: r.z + hs, s: 0 });
    });
    mesh.castShadow = true; mesh.receiveShadow = true;
    this.scene.add(mesh);
  }

  buildTrees(rng, preset) {
    const dens = preset.trees;
    const cedars = [], olives = [];
    for (let i = 0; i < 9000 * dens * S * S; i++) {
      const x = (-580 + rng() * 520) * S, z = (-790 + rng() * 430) * S;
      const h = this.getHeight(x, z);
      if (h < 10 || h > 58 || this.nearZone(x, z, 0.7) || fbm(x * 0.012, z * 0.012, 21, 2) < 0.45) continue;
      cedars.push({ x, z, y: h, s: 0.8 + rng() * 0.7 });
    }
    for (let i = 0; i < 7000 * dens * S * S; i++) {
      const x = (rng() * 2 - 1) * HALF, z = (-300 + rng() * 1090) * S, xd = x / S, zd = z / S;
      const plain = (xd < -120 && zd > 250) || (Math.hypot(xd - 310, zd + 170) < 190) || (Math.hypot(xd + 300, zd - 140) < 200);
      if (!plain || this.isWater(x, z) || this.nearZone(x, z, 0.9)) continue;
      if (fbm(x * 0.02, z * 0.02, 33, 2) < 0.5) continue;
      olives.push({ x, z, y: this.getHeight(x, z), s: 0.7 + rng() * 0.6 });
    }
    const inst = (geo, mat, list, f) => {
      const mesh = new THREE.InstancedMesh(geo, mat, list.length);
      const m = new THREE.Matrix4();
      list.forEach((t, i) => { f(m, t); mesh.setMatrixAt(i, m); });
      mesh.castShadow = true;
      this.scene.add(mesh);
      return mesh;
    };
    const crown = new THREE.ConeGeometry(2.4, 7, 7); crown.translate(0, 5.5, 0);
    const crown2 = new THREE.ConeGeometry(3.2, 4, 7); crown2.translate(0, 3.2, 0);
    const trunk = new THREE.CylinderGeometry(0.25, 0.4, 3, 5); trunk.translate(0, 1.5, 0);
    const sc = (m, t) => m.compose(new THREE.Vector3(t.x, t.y - 0.2, t.z), new THREE.Quaternion(), new THREE.Vector3(t.s, t.s, t.s));
    inst(crown, new THREE.MeshLambertMaterial({ color: 0x2c4a2b, flatShading: true }), cedars, sc);
    inst(crown2, new THREE.MeshLambertMaterial({ color: 0x35552f, flatShading: true }), cedars, sc);
    inst(trunk, new THREE.MeshLambertMaterial({ color: 0x5a4030 }), cedars, sc);
    const oc = new THREE.IcosahedronGeometry(1.9, 0); oc.scale(1.25, 0.85, 1.25); oc.translate(0, 2.8, 0);
    const ot = new THREE.CylinderGeometry(0.22, 0.35, 2.2, 5); ot.translate(0, 1.1, 0);
    inst(oc, new THREE.MeshLambertMaterial({ color: 0x7c8a58, flatShading: true }), olives, sc);
    inst(ot, new THREE.MeshLambertMaterial({ color: 0x6a5642 }), olives, sc);
    this.treeCount = cedars.length + olives.length;
  }

  buildBoxMesh() {
    const mesh = new THREE.InstancedMesh(new THREE.BoxGeometry(1, 1, 1), new THREE.MeshLambertMaterial({ color: 0xffffff, map: DETAIL }), this.boxes.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), c = new THREE.Color(), p = new THREE.Vector3(), s = new THREE.Vector3(), Y = new THREE.Vector3(0, 1, 0);
    this.boxes.forEach((b, i) => {
      q.setFromAxisAngle(Y, b.rotY || 0);
      m.compose(p.set(b.cx, b.cy, b.cz), q, s.set(b.sx, b.sy, b.sz));
      mesh.setMatrixAt(i, m);
      mesh.setColorAt(i, c.setHex(b.color));
    });
    mesh.castShadow = true; mesh.receiveShadow = true;
    this.scene.add(mesh);
  }

  buildCylMesh() {
    if (!this.cyls.length) return;
    const mesh = new THREE.InstancedMesh(new THREE.CylinderGeometry(1, 1, 1, 12), new THREE.MeshLambertMaterial({ color: 0xffffff }), this.cyls.length);
    const m = new THREE.Matrix4(), q = new THREE.Quaternion(), c = new THREE.Color(), p = new THREE.Vector3(), s = new THREE.Vector3();
    this.cyls.forEach((b, i) => {
      m.compose(p.set(b.x, b.y, b.z), q, s.set(b.r, b.h, b.r));
      mesh.setMatrixAt(i, m);
      mesh.setColorAt(i, c.setHex(b.color));
    });
    mesh.castShadow = true; mesh.receiveShadow = true;
    this.scene.add(mesh);
  }

  landmarkNear(x, z, r) {
    let best = null, bd = r;
    for (const l of this.landmarks) { const d = Math.hypot(x - l.x, z - l.z); if (d < bd) { bd = d; best = l; } }
    return best;
  }

  // ---------- spatial grid ----------
  buildGrid() {
    this.G = 16;
    this.grid = new Map();
    this.colliders.forEach((c, i) => {
      for (let gx = this.gc(c.minX); gx <= this.gc(c.maxX); gx++)
        for (let gz = this.gc(c.minZ); gz <= this.gc(c.maxZ); gz++) {
          const k = gx * 4096 + gz;
          let a = this.grid.get(k); if (!a) this.grid.set(k, a = []);
          a.push(i);
        }
    });
  }
  gc(v) { return Math.floor((v + HALF) / this.G); }
  query(minX, maxX, minZ, maxZ, out) {
    out.length = 0;
    const st = ++this.stamp;
    for (let gx = this.gc(minX); gx <= this.gc(maxX); gx++)
      for (let gz = this.gc(minZ); gz <= this.gc(maxZ); gz++) {
        const a = this.grid.get(gx * 4096 + gz); if (!a) continue;
        for (const i of a) { const c = this.colliders[i]; if (c.s !== st) { c.s = st; out.push(c); } }
      }
    return out;
  }

  // push a vertical circle (actor) out of colliders
  collideCircle(pos, radius, y0, y1) {
    const list = this.query(pos.x - radius, pos.x + radius, pos.z - radius, pos.z + radius, this._q || (this._q = []));
    for (const c of list) {
      if (c.maxY < y0 || c.minY > y1) continue;
      const cx = clamp(pos.x, c.minX, c.maxX), cz = clamp(pos.z, c.minZ, c.maxZ);
      const dx = pos.x - cx, dz = pos.z - cz, d2 = dx * dx + dz * dz;
      if (d2 > radius * radius) continue;
      if (d2 > 1e-8) { const d = Math.sqrt(d2), p = radius - d; pos.x += dx / d * p; pos.z += dz / d * p; }
      else {
        const pl = pos.x - c.minX, pr = c.maxX - pos.x, pn = pos.z - c.minZ, ps = c.maxZ - pos.z;
        const m = Math.min(pl, pr, pn, ps);
        if (m === pl) pos.x = c.minX - radius; else if (m === pr) pos.x = c.maxX + radius;
        else if (m === pn) pos.z = c.minZ - radius; else pos.z = c.maxZ + radius;
      }
    }
  }

  // distance to the first wall/terrain hit along a normalized ray, or Infinity
  raycast(ox, oy, oz, dx, dy, dz, maxD, terrainStep = 2) {
    let best = maxD;
    const st = ++this.stamp, G = this.G;
    const step = 4, n = Math.ceil(maxD / step);
    for (let i = 0; i <= n; i++) {
      const t = Math.min(i * step, maxD);
      if (t > best) break;
      const gx = this.gc(ox + dx * t), gz = this.gc(oz + dz * t);
      for (let ax = -1; ax <= 1; ax++) for (let az = -1; az <= 1; az++) {
        const a = this.grid.get((gx + ax) * 4096 + gz + az); if (!a) continue;
        for (const ci of a) {
          const c = this.colliders[ci];
          if (c.s === st) continue; c.s = st;
          const h = rayAABB(ox, oy, oz, dx, dy, dz, c);
          if (h < best) best = h;
        }
      }
    }
    // terrain march
    let prevT = 0;
    for (let t = terrainStep; t <= best + terrainStep; t += terrainStep) {
      const tt = Math.min(t, best);
      const y = oy + dy * tt, h = this.getHeight(ox + dx * tt, oz + dz * tt);
      if (y < h) {
        let a = prevT, b = tt;
        for (let k = 0; k < 6; k++) { const m = (a + b) / 2; if (oy + dy * m < this.getHeight(ox + dx * m, oz + dz * m)) b = m; else a = m; }
        return Math.min(best, a);
      }
      prevT = tt;
      if (tt >= best) break;
    }
    return best < maxD ? best : Infinity;
  }

  insideBuilding(x, z, y) {
    for (const b of this.buildings)
      if (x > b.minX + 0.3 && x < b.maxX - 0.3 && z > b.minZ + 0.3 && z < b.maxZ - 0.3 && y < b.top) return b;
    return null;
  }

  zoneAt(x, z) {
    let best = null, bd = Infinity;
    for (const zn of this.zones) { const d = Math.hypot(x - zn.x, z - zn.z); if (d < zn.r && d < bd) { bd = d; best = zn; } }
    return best;
  }

  randomLandPoint(rng = Math.random, cx = 0, cz = 0, r = HALF) {
    for (let i = 0; i < 200; i++) {
      const a = rng() * 6.283, rr = Math.sqrt(rng()) * r;
      const x = clamp(cx + Math.cos(a) * rr, -HALF + 20, HALF - 20), z = clamp(cz + Math.sin(a) * rr, -HALF + 20, HALF - 20);
      if (!this.isWater(x, z)) return { x, z };
    }
    return { x: cx, z: cz };
  }
}

export function rayAABB(ox, oy, oz, dx, dy, dz, c) {
  let tmin = 0, tmax = Infinity;
  if (Math.abs(dx) < 1e-9) { if (ox < c.minX || ox > c.maxX) return Infinity; }
  else { let a = (c.minX - ox) / dx, b = (c.maxX - ox) / dx; if (a > b) [a, b] = [b, a]; tmin = Math.max(tmin, a); tmax = Math.min(tmax, b); }
  if (Math.abs(dy) < 1e-9) { if (oy < c.minY || oy > c.maxY) return Infinity; }
  else { let a = (c.minY - oy) / dy, b = (c.maxY - oy) / dy; if (a > b) [a, b] = [b, a]; tmin = Math.max(tmin, a); tmax = Math.min(tmax, b); }
  if (Math.abs(dz) < 1e-9) { if (oz < c.minZ || oz > c.maxZ) return Infinity; }
  else { let a = (c.minZ - oz) / dz, b = (c.maxZ - oz) / dz; if (a > b) [a, b] = [b, a]; tmin = Math.max(tmin, a); tmax = Math.min(tmax, b); }
  return tmax >= tmin && tmax > 0 ? tmin : Infinity;
}
