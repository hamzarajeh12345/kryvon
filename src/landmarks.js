// Landmarks (آثار) – simplified, original low-poly takes on real Levant heritage sites.
// Each builder gets the world, the landmark record {x,z,...}, the zone's occupancy list and the map rng.
// Everything is built from boxes/cylinders/simple meshes; actors walk on terrain only, so landmarks
// are hollow at ground level (courtyards, halls, colonnades) or solid cover.
import * as THREE from 'three';
import { LANDMARKS } from './config.js';
import { coastX } from './world.js';

const STONE = 0xd8c7a0, LIME = 0xe6dcc4, BASALT = 0x3c3b3a, ROSE = 0xc98a6a, SAND = 0xb8653f;

export function buildLandmarks(w, zone, occ, rng) {
  for (const L of LANDMARKS) {
    if (L.zone !== zone.id) continue;
    const rec = { ...L, x: zone.x + L.dx, z: zone.z + L.dz };
    const f = BUILD[L.id];
    if (f) f(w, rec, occ, rng, zone);
    w.landmarks.push(rec);
  }
}

const rect = (occ, x, z, hw, hd) => occ.push({ minX: x - hw, maxX: x + hw, minZ: z - hd, maxZ: z + hd });
const base = (w, x, z, hw, hd) => w.minH(x - hw, z - hd, x + hw, z + hd)[0];

function minaret(w, x, z, h, color) {
  const g = base(w, x, z, 2, 2);
  w.addBox(x, g + h / 2 - 1, z, 3.6, h + 2, 3.6, color);
  w.addBox(x, g + h + 0.2, z, 4.6, 0.5, 4.6, color, false);           // balcony
  w.addBox(x, g + h + 2.6, z, 2.6, 4.4, 2.6, color, false);           // upper stage
  w.addBox(x, g + h + 5, z, 3.4, 0.4, 3.4, color, false);
  w.addMesh(new THREE.ConeGeometry(1.6, 4, 8), 0x5e6a6e, x, g + h + 7.2, z);
}

function dome(w, x, y, z, r, color, drumColor, drumH = 2.5, sy = 1, emissive = 0) {
  if (drumColor != null) w.addMesh(new THREE.CylinderGeometry(r * 0.92, r * 0.92, drumH, 20), drumColor, x, y + drumH / 2, z);
  const d = w.addMesh(new THREE.SphereGeometry(r, 24, 12, 0, Math.PI * 2, 0, Math.PI / 2), color, x, y + (drumColor != null ? drumH : 0), z, { emissive });
  d.scale.y = sy;
  return d;
}

function colonnade(w, x0, z0, x1, z1, step, r, h, color, rng, missing = 0, lintel = true) {
  const len = Math.hypot(x1 - x0, z1 - z0), n = Math.max(1, Math.round(len / step));
  let topMax = -Infinity;
  for (let i = 0; i <= n; i++) {
    const x = x0 + (x1 - x0) * i / n, z = z0 + (z1 - z0) * i / n;
    if (missing && rng() < missing) {   // fallen drum lying next to the base
      const g = w.getHeight(x + 1.5, z + 1.5);
      const m = w.addMesh(new THREE.CylinderGeometry(r, r, h * 0.35, 10), color, x + 1.5, g + r, z + 1.5);
      m.rotation.z = Math.PI / 2; m.rotation.y = rng() * 3;
      w.colliders.push({ minX: x + 1.5 - r * 2, maxX: x + 1.5 + r * 2, minY: g, maxY: g + r * 2, minZ: z + 1.5 - r * 2, maxZ: z + 1.5 + r * 2, s: 0 });
      continue;
    }
    const g = w.getHeight(x, z);
    w.addCyl(x, g - 0.5, z, r, h + 0.5, color);
    w.addBox(x, g + h + 0.2, z, r * 2.6, 0.4, r * 2.6, color, false); // capital
    topMax = Math.max(topMax, g + h);
  }
  if (lintel && topMax > -Infinity) {
    const cx = (x0 + x1) / 2, cz = (z0 + z1) / 2, horizontal = Math.abs(x1 - x0) > Math.abs(z1 - z0);
    w.addBox(cx, topMax + 0.9, cz, horizontal ? len + r * 2 : r * 2.4, 1, horizontal ? r * 2.4 : len + r * 2, color, false);
  }
}

function arch(w, x, z, alongX, openings, height, depth, color) {
  // openings: [[from,to], ...] gaps measured along the axis, relative to x/z; piers fill between
  const span = [openings[0][0] - 3, openings[openings.length - 1][1] + 3];
  const edges = [span[0], ...openings.flat(), span[1]];
  const g = base(w, x, z, (span[1] - span[0]) / 2, depth);
  for (let i = 0; i < edges.length; i += 2) {
    const a = edges[i], b = edges[i + 1], c = (a + b) / 2, l = b - a;
    if (alongX) w.addBox(x + c, g + height / 2 - 1, z, l, height + 2, depth, color);
    else w.addBox(x, g + height / 2 - 1, z + c, depth, height + 2, l, color);
  }
  const c = (span[0] + span[1]) / 2, l = span[1] - span[0];
  if (alongX) w.addBox(x + c, g + height + 1.5, z, l, 3, depth, color, false);
  else w.addBox(x, g + height + 1.5, z + c, depth, 3, l, color, false);
}

const BUILD = {
  // ---------------- Damascus ----------------
  umayyad(w, L, occ, rng, zone) {
    const { x, z } = L, W = 64, D = 46, col = 0xcdb892, h = 7;
    w.wallLine(x - W / 2, z - D / 2, x - 5, z - D / 2, h, 1.2, col);          // north wall, gate in the middle
    w.wallLine(x + 5, z - D / 2, x + W / 2, z - D / 2, h, 1.2, col);
    w.wallLine(x - W / 2, z + D / 2, x + W / 2, z + D / 2, h, 1.2, col);      // south
    w.wallLine(x - W / 2, z - D / 2, x - W / 2, z - 4, h, 1.2, col);          // west (Bab Al-Barid)
    w.wallLine(x - W / 2, z + 4, x - W / 2, z + D / 2, h, 1.2, col);
    w.wallLine(x + W / 2, z - D / 2, x + W / 2, z - 4, h, 1.2, col);          // east (Bab Jayrun)
    w.wallLine(x + W / 2, z + 4, x + W / 2, z + D / 2, h, 1.2, col);
    // prayer hall along the south wall, with the central dome
    const hz = z + D / 2 - 8.5, hallH = 9;
    w.addBuilding(x, hz, W - 5, 13, hallH, { wall: [0xc8b088], roof: 0x8a8070 }, 'police', zone, rng);
    const hb = base(w, x, hz, (W - 5) / 2, 6.5);
    w.addBox(x, hb + hallH + 3, hz, 9, 6, 13.2, 0xc8b088, false);              // transept gable
    dome(w, x, hb + hallH + 6, hz, 5.5, 0x77858c, 0xc8b088, 2.5, 1.15);       // "Dome of the Eagle"
    // three minarets
    minaret(w, x + 12, z - D / 2 - 0.5, 22, 0xd8c8a2);
    minaret(w, x + W / 2, z + D / 2, 26, 0xd8c8a2);
    minaret(w, x - W / 2, z + D / 2, 24, 0xd8c8a2);
    // courtyard arcades
    colonnade(w, x - W / 2 + 3, z - D / 2 + 3, x + W / 2 - 3, z - D / 2 + 3, 4.5, 0.38, 5, LIME, rng);
    colonnade(w, x - W / 2 + 3, z - D / 2 + 7, x - W / 2 + 3, z + D / 2 - 17, 4.5, 0.38, 5, LIME, rng);
    colonnade(w, x + W / 2 - 3, z - D / 2 + 7, x + W / 2 - 3, z + D / 2 - 17, 4.5, 0.38, 5, LIME, rng);
    // treasury kiosk: octagon raised on 8 columns
    const kx = x - 17, kz = z - 6, kg = w.getHeight(kx, kz);
    for (let i = 0; i < 8; i++) { const a = i / 8 * Math.PI * 2; w.addCyl(kx + Math.cos(a) * 2.2, kg - 0.3, kz + Math.sin(a) * 2.2, 0.22, 4.3, LIME); }
    w.addMesh(new THREE.CylinderGeometry(2.8, 2.8, 2.6, 8), 0x3f7f6a, kx, kg + 5.3, kz);
    dome(w, kx, kg + 6.6, kz, 1.6, 0x77858c, null);
    // ablution fountain
    const fg = w.getHeight(x, z - 6);
    w.addCyl(x, fg - 0.2, z - 6, 2.2, 0.8, 0xe8e2d2);
    w.addMesh(new THREE.CylinderGeometry(1.2, 1.2, 2.4, 8), LIME, x, fg + 1.6, z - 6);
    w.addMesh(new THREE.ConeGeometry(1.6, 1.2, 8), 0x77858c, x, fg + 3.4, z - 6);
    for (const [lx, lz] of [[-8, -12], [8, -12], [22, -2], [-24, 4], [0, 2]]) w.loot(x + lx, z + lz, 3, 'police', zone.id);
    rect(occ, x, z, W / 2 + 3, D / 2 + 3);
  },
  hamidiyah(w, L, occ, rng, zone) {
    const { x, z } = L, len = 46, hw = 4.2, col = 0xc4ae86;
    const g = base(w, x, z, len / 2, hw);
    for (const side of [-1, 1]) {
      for (let i = 0; i < 6; i++) {
        const a = x - len / 2 + i * len / 6, seg = len / 6 - 2;
        w.addBox(a + seg / 2, g + 3, z + side * hw, seg, 8, 0.5, col);
        w.addBox(a + seg + 1, g + 5, z + side * hw, 2, 4, 0.5, col);          // lintel over the shop opening
        w.loot(a + seg / 2, z + side * (hw - 1.2), 2, 'shop', zone.id);
      }
    }
    const roof = new THREE.CylinderGeometry(hw + 0.2, hw + 0.2, len, 18, 1, true, 0, Math.PI);
    roof.rotateZ(Math.PI / 2);
    w.addMesh(roof, 0x666b70, x, g + 7, z, { side: THREE.DoubleSide });
    // arched gateway at the west end
    w.addBox(x - len / 2 - 0.5, g + 9.5, z, 1.4, 5, hw * 2 + 2, col, false);
    w.addBox(x - len / 2 - 0.5, g + 4, z - hw - 0.4, 1.4, 10, 1.2, col);
    w.addBox(x - len / 2 - 0.5, g + 4, z + hw + 0.4, 1.4, 10, 1.2, col);
    rect(occ, x, z, len / 2 + 3, hw + 3);
  },

  // ---------------- Aleppo ----------------
  citadel(w, L, occ, rng, zone) {
    const x = zone.x, tz = zone.z + 39, col = 0xb19a72;
    const g = base(w, x, tz, 7, 5);
    // gate block with a walk-through passage
    w.addBox(x - 5, g + 8, tz, 6, 18, 9, col);
    w.addBox(x + 5, g + 8, tz, 6, 18, 9, col);
    w.addBox(x, g + 12, tz, 4.2, 10, 9, col, false);
    w.addBox(x, g + 17.3, tz, 16, 0.8, 10, col, false);
    for (let i = -2; i <= 2; i++) w.addBox(x + i * 3.2, g + 18.2, tz - 4.5, 1.4, 1.2, 1, col, false); // crenels
    // the stepped bridge on arches down the glacis
    for (let k = 1; k <= 5; k++) {
      const bz = tz + 5 + k * 7, bg = w.getHeight(x, bz), top = g + 9 - k * 1.4;
      w.addBox(x - 2.4, (bg + top) / 2 - 0.5, bz, 1.4, top - bg + 1, 2, col);
      w.addBox(x + 2.4, (bg + top) / 2 - 0.5, bz, 1.4, top - bg + 1, 2, col);
      w.addBox(x, top + 0.4, bz - 3.5, 6.2, 0.8, 7.2, col, false);
    }
    w.loot(x, tz - 1, 3, 'police', zone.id); w.loot(x, tz + 2, 3, 'police', zone.id);
    rect(occ, x, tz + 16, 9, 22);
  },

  // ---------------- Beirut ----------------
  raouche(w, L, occ, rng, zone) {
    const z = zone.z + 10, x = coastX(z) - 55;
    L.x = x; L.z = z;
    const rock = (px, pz, sx, sy, sz, rot) => {
      const m = w.addMesh(new THREE.DodecahedronGeometry(1, 1), 0xc9b48e, px, sy * 0.5 - 6, pz, { flat: true });
      m.scale.set(sx, sy, sz); m.rotation.y = rot;
      w.colliders.push({ minX: px - sx * 0.8, maxX: px + sx * 0.8, minY: -20, maxY: sy * 1.5 - 6, minZ: pz - sz * 0.8, maxZ: pz + sz * 0.8, s: 0 });
    };
    rock(x, z, 13, 34, 10, 0.4);                               // the big rock
    rock(x + 26, z + 18, 4, 18, 4, 0.2);                       // the arch: two legs and a top
    rock(x + 26, z + 32, 4, 18, 4, 0.9);
    const top = w.addMesh(new THREE.DodecahedronGeometry(1, 1), 0xc9b48e, x + 26, 12, z + 25, { flat: true });
    top.scale.set(5, 4, 11);
  },

  // ---------------- Cedars / Bekaa ----------------
  baalbek(w, L, occ, rng, zone) {
    const { x, z } = L, col = 0xdcc49a;
    const [m, M] = w.minH(x - 16, z - 7, x + 16, z + 7);
    const top = M + 3;
    w.addBox(x, (m - 2 + top) / 2, z, 32, top - m + 2, 14, col);               // podium
    for (let i = 0; i < 6; i++) w.addCyl(x - 12.5 + i * 5, top, z, 1.05, 19, 0xe2cba0, false);
    w.addBox(x, top + 19.6, z, 29, 1.2, 3.2, col, false);
    w.addBox(x, top + 20.9, z, 29.6, 1.4, 3.6, 0xd2b88a, false);
    w.addBox(x, top + 22.1, z, 30.4, 1, 4, col, false);
    for (let i = 0; i < 5; i++) {                                              // fallen column drums
      const fx = x - 14 + rng() * 28, fz = z + 10 + rng() * 6, g = w.getHeight(fx, fz);
      const d = w.addMesh(new THREE.CylinderGeometry(1, 1, 3.2, 12), 0xe2cba0, fx, g + 1, fz);
      d.rotation.z = Math.PI / 2; d.rotation.y = rng() * 3;
      w.colliders.push({ minX: fx - 1.8, maxX: fx + 1.8, minY: g, maxY: g + 2, minZ: fz - 1.8, maxZ: fz + 1.8, s: 0 });
    }
    for (let i = 0; i < 4; i++) w.loot(x - 12 + i * 8, z + 9, 2, 'police', zone.id);
    rect(occ, x, z + 4, 19, 13);
  },
  cedarsgod(w, L, occ, rng, zone) {
    for (let i = 0; i < 9; i++) {
      const a = i / 9 * Math.PI * 2 + rng() * 0.4, r = 18 + rng() * 26;
      const x = L.x + Math.cos(a) * r, z = L.z + Math.sin(a) * r, g = w.getHeight(x, z), s = 0.9 + rng() * 0.5;
      w.addCyl(x, g - 0.5, z, 0.9 * s, 7 * s, 0x4a3426);
      for (let k = 0; k < 4; k++) {   // flat layered crowns, like real old cedars
        const t = w.addMesh(new THREE.CylinderGeometry(1.5 * s, (7 - k * 1.4) * s, 1.6 * s, 9), k % 2 ? 0x2f4d2c : 0x284426, x, g + (6 + k * 2.6) * s, z, { flat: true });
        t.rotation.y = rng() * 3;
      }
      rect(occ, x, z, 5, 5);
    }
    w.loot(L.x, L.z, 2, 'police', zone.id);
  },

  // ---------------- Badia ----------------
  palmyra(w, L, occ, rng, zone) {
    const { x, z } = L, col = 0xe0c08c;
    colonnade(w, x - 58, z - 5, x + 18, z - 5, 5, 0.6, 7.5, col, rng, 0.18);
    colonnade(w, x - 58, z + 5, x + 18, z + 5, 5, 0.6, 7.5, col, rng, 0.18);
    arch(w, x - 64, z, false, [[-6.4, -4.4], [-2.2, 2.2], [4.4, 6.4]], 8, 3, col);
    // Temple of Bel: cella with a peristyle
    const tx = x + 52, tz = z;
    w.addBuilding(tx, tz, 12, 24, 10, { wall: [0xd7b98a], roof: 0xc2a374 }, 'police', zone, rng);
    for (let i = 0; i <= 7; i++) for (const s of [-1, 1]) {
      const pz = tz - 15 + i * 30 / 7, px = tx + s * 9;
      w.addCyl(px, w.getHeight(px, pz) - 0.5, pz, 0.55, 11, col);
    }
    for (let i = 0; i <= 3; i++) for (const s of [-1, 1]) {
      const px = tx - 9 + i * 6, pz = tz + s * 15;
      w.addCyl(px, w.getHeight(px, pz) - 0.5, pz, 0.55, 11, col);
    }
    for (let i = 0; i < 4; i++) w.loot(x - 50 + i * 18, z, 2, 'police', zone.id);
    rect(occ, x - 22, z, 46, 10);
    rect(occ, tx, tz, 12, 18);
  },

  // ---------------- Jerash ----------------
  hadrian(w, L, occ, rng, zone) {
    const { x, z } = L, col = 0xdcc8a0;
    arch(w, x, z, true, [[-6.5, -4], [-2.3, 2.3], [4, 6.5]], 9, 3.5, col);
    // oval plaza
    const ox = zone.x - 15, oz = zone.z - 35;
    for (let i = 0; i < 34; i++) {
      const a = i / 34 * Math.PI * 2;
      if (Math.abs(Math.sin(a)) > 0.97 && Math.cos(a) < 0) continue;   // openings to the streets
      const px = ox + Math.cos(a) * 27, pz = oz + Math.sin(a) * 20;
      w.addCyl(px, w.getHeight(px, pz) - 0.5, pz, 0.45, 6.5, col);
    }
    for (let i = 0; i < 3; i++) w.loot(ox - 8 + i * 8, oz, 2, 'police', zone.id);
    rect(occ, x, z, 13, 5);
    rect(occ, ox, oz, 30, 23);
  },

  // ---------------- Jerusalem ----------------
  dome(w, L, occ, rng, zone) {
    const { x, z } = L;
    const g = base(w, x, z, 10, 10);
    w.addBox(x, g + 0.05, z, 64, 0.3, 52, 0xe9e1cc, false);                     // esplanade paving
    w.addMesh(new THREE.CylinderGeometry(10, 10, 4.5, 8), 0xece6da, x, g + 2.25, z);
    w.addMesh(new THREE.CylinderGeometry(10, 10, 4.5, 8), 0x2f6fae, x, g + 6.75, z);
    w.addMesh(new THREE.CylinderGeometry(10.4, 10.4, 0.5, 8), 0xece6da, x, g + 9.2, z);
    w.addBox(x, g + 4.5, z, 14, 11, 14, 0xece6da, true);                        // collider core (hidden inside)
    dome(w, x, g + 9.4, z, 6, 0xe2b230, 0x2f6fae, 3.2, 1.2, 0x3a2a00);
    w.addMesh(new THREE.CylinderGeometry(0.15, 0.15, 3, 6), 0xe2b230, x, g + 21.5, z);
    // arcades (qanatir) at the platform edges
    for (const [ax, az] of [[0, -20], [0, 20], [-24, 0], [24, 0]]) {
      const along = az !== 0;
      for (let i = -2; i <= 2; i++) w.addCyl(x + ax + (along ? i * 3 : 0), g - 0.3, z + az + (along ? 0 : i * 3), 0.3, 4.6, 0xe9e1cc);
      w.addBox(x + ax, g + 4.8, z + az, along ? 14 : 1.2, 0.8, along ? 1.2 : 14, 0xe9e1cc, false);
    }
    for (const [lx, lz] of [[16, 0], [-16, 0], [0, 15], [0, -15]]) w.loot(x + lx, z + lz, 3, 'police', zone.id);
    rect(occ, x, z, 32, 26);
  },
  aqsa(w, L, occ, rng, zone) {
    const { x, z } = L;
    w.addBuilding(x, z, 18, 26, 8, { wall: [0xdccfb2], roof: 0x9a9890 }, 'hospital', zone, rng);
    const g = base(w, x, z, 9, 13);
    dome(w, x, g + 8.3, z + 8, 4.5, 0x8c9396, 0xdccfb2, 2.2, 1.1);
    for (let i = 0; i < 7; i++) {                                               // front porch arches
      const px = x - 7.5 + i * 2.5;
      w.addCyl(px, g - 0.3, z - 14.5, 0.32, 5, 0xdccfb2);
    }
    w.addBox(x, g + 5.2, z - 14.5, 18.5, 1, 1.4, 0xdccfb2, false);
    rect(occ, x, z - 1, 12, 16);
  },

  // ---------------- Amman ----------------
  theatre(w, L, occ, rng, zone) {
    const { x, z } = L, col = 0xd9cdb4;
    const g = base(w, x, z, 6, 6);
    for (let r = 0; r < 10; r++) {
      const R = 13 + r * 1.7, n = 10 + r * 2;
      for (let i = 0; i < n; i++) {
        const a0 = Math.PI * i / n, a1 = Math.PI * (i + 1) / n, am = (a0 + a1) / 2;
        if (r < 6 && Math.abs(am - Math.PI / 2) < 0.08) continue;           // central aisle
        const len = 2 * R * Math.sin((a1 - a0) / 2) + 0.15, hh = 0.8 + r * 0.75;
        const bx = x + Math.cos(am) * R, bz = z + Math.sin(am) * R;
        const gy = w.getHeight(bx, bz);
        const m = Math.min(g, gy) - 1, top = g + hh;
        w.boxes.push({ cx: bx, cy: (m + top) / 2, cz: bz, sx: len, sy: top - m, sz: 1.75, color: r % 2 ? col : 0xcfc2a6, rotY: -am + Math.PI / 2 });
        w.colliders.push({ minX: bx - 1.1, maxX: bx + 1.1, minY: m, maxY: top, minZ: bz - 1.1, maxZ: bz + 1.1, s: 0 });
      }
    }
    // stage building with columns
    w.addBox(x, g + 3.5, z - 4, 34, 7, 1.6, col);
    for (let i = 0; i < 9; i++) w.addCyl(x - 14 + i * 3.5, g - 0.3, z - 2.4, 0.35, 6, 0xe6dcc4);
    for (let i = 0; i < 4; i++) w.loot(x - 6 + i * 4, z + 4, 3, 'police', zone.id);
    rect(occ, x, z + 13, 32, 19);
  },
  hercules(w, L, occ, rng, zone) {
    const { x, z } = L, col = 0xd6c3a0;
    const [m, M] = w.minH(x - 10, z - 6, x + 10, z + 6);
    w.addBox(x, (m - 2 + M + 1.2) / 2, z, 22, M + 3.2 - m, 13, col);
    const top = M + 1.2;
    w.addCyl(x - 6, top, z - 3, 1, 13, 0xe0cfaa, false);
    w.addCyl(x - 1, top, z - 3, 1, 13, 0xe0cfaa, false);
    w.addCyl(x + 6, top, z + 2, 1, 9, 0xe0cfaa, false);
    w.addBox(x - 3.5, top + 13.6, z - 3, 8, 1.3, 2.4, col, false);
    // the giant stone hand fragment
    const hx = x + 4, hz = z + 10, hg = w.getHeight(hx, hz);
    w.addBox(hx, hg + 0.6, hz, 2.6, 1.2, 1.8, 0xd9caa8);
    for (let i = 0; i < 3; i++) w.addBox(hx - 0.8 + i * 0.8, hg + 0.45, hz - 1.8, 0.6, 0.6, 1.8, 0xd9caa8, false);
    w.addBox(hx + 1.6, hg + 0.4, hz - 0.2, 0.6, 0.6, 1.4, 0xd9caa8, false);
    for (let i = 0; i < 3; i++) w.loot(x - 8 + i * 8, z + 8, 3, 'police', zone.id);
    rect(occ, x, z + 2, 13, 11);
  },

  // ---------------- Dead Sea ----------------
  salt(w, L, occ, rng, zone) {
    for (let i = 0; i < 12; i++) {
      const a = rng() * Math.PI * 2, r = 4 + rng() * 18;
      const px = L.x + Math.cos(a) * r, pz = L.z + Math.sin(a) * r;
      if (w.isWater(px, pz)) continue;
      const g = w.getHeight(px, pz), h = 1.5 + rng() * 5, rr = 0.6 + rng() * 1.3;
      const s = w.addMesh(new THREE.CylinderGeometry(rr * 0.5, rr, h, 7), 0xf3f0e7, px, g + h / 2 - 0.2, pz, { flat: true });
      s.rotation.y = rng() * 3;
      w.colliders.push({ minX: px - rr, maxX: px + rr, minY: g - 1, maxY: g + h, minZ: pz - rr, maxZ: pz + rr, s: 0 });
    }
    w.loot(L.x, L.z, 2, 'hospital', zone.id);
    rect(occ, L.x, L.z, 22, 22);
  },

  // ---------------- Olive plains ----------------
  olivepress(w, L, occ, rng, zone) {
    const { x, z } = L, g = w.getHeight(x, z);
    w.addCyl(x, g - 0.3, z, 3, 1.2, 0xb8aa8c);                                  // basin
    const stone = w.addMesh(new THREE.CylinderGeometry(1.5, 1.5, 0.7, 16), 0xa89a7c, x, g + 2.4, z);
    stone.rotation.z = Math.PI / 2;
    w.addBox(x, g + 2.4, z, 7, 0.3, 0.3, 0x6a4a2a, false);                        // turning beam
    const tx = x + 10, tz = z + 6, tg = w.getHeight(tx, tz);                    // the ancient olive tree
    w.addCyl(tx, tg - 0.3, tz, 1.4, 3.6, 0x6a5642);
    const c = w.addMesh(new THREE.IcosahedronGeometry(5, 0), 0x7c8a58, tx, tg + 6, tz, { flat: true });
    c.scale.set(1.3, 0.75, 1.3);
    w.addBuilding(x - 12, z - 4, 8, 8, 4, { wall: [0xd6c6a3], roof: 0xa08c6a }, 'shop', zone, rng);
    w.loot(x + 3, z + 3, 2, 'police', zone.id);
    rect(occ, x - 2, z + 1, 16, 12);
  },

  // ---------------- Wadi Araba ----------------
  rum(w, L, occ, rng, zone) {
    const spots = [[-55, -25, 9, 32], [-30, 40, 7, 24], [40, -40, 11, 38], [60, 25, 8, 28], [5, 62, 6, 20]];
    for (const [dx, dz, r, h] of spots) {
      const px = L.x + dx, pz = L.z + dz, g = w.getHeight(px, pz);
      const m = w.addMesh(new THREE.CylinderGeometry(r * 0.7, r, h, 9, 3), SAND, px, g + h / 2 - 1, pz, { flat: true, jitter: 0.12 });
      m.rotation.y = rng() * 3;
      w.colliders.push({ minX: px - r * 0.85, maxX: px + r * 0.85, minY: g - 2, maxY: g + h, minZ: pz - r * 0.85, maxZ: pz + r * 0.85, s: 0 });
      rect(occ, px, pz, r + 3, r + 3);
    }
    // natural rock bridge
    const ax = L.x, az = L.z, g = w.getHeight(ax, az);
    for (const s of [-1, 1]) {
      const m = w.addMesh(new THREE.CylinderGeometry(2.2, 3.4, 14, 7, 2), SAND, ax + s * 7, g + 6, az, { flat: true, jitter: 0.1 });
      m.rotation.y = s;
      w.colliders.push({ minX: ax + s * 7 - 3, maxX: ax + s * 7 + 3, minY: g - 1, maxY: g + 13, minZ: az - 3, maxZ: az + 3, s: 0 });
    }
    w.addMesh(new THREE.BoxGeometry(19, 3.2, 4.5, 4, 1, 1), 0xa85a38, ax, g + 13.8, az, { flat: true, jitter: 0.12 });
    w.loot(ax, az, 2, 'police', zone.id);
    rect(occ, ax, az, 12, 5);
  },

  // ---------------- Petra ----------------
  monastery(w, L, occ, rng, zone) {
    const { x, z } = L;
    const [m, M] = w.minH(x - 13, z - 4, x + 13, z + 4);
    const g = m;
    w.addBox(x, g + 14, z - 3, 28, 32, 6, ROSE);                               // carved façade
    for (let i = 0; i < 8; i++) if (i !== 3 && i !== 4) w.addBox(x - 12.25 + i * 3.5, g + 6, z + 0.6, 1.4, 12, 1.2, 0xd8957a);
    w.addBox(x, g + 4, z + 0.2, 4, 8, 0.4, 0x2a1a14, false);                    // dark doorway
    w.addBox(x, g + 12.6, z + 0.7, 28, 1.4, 1.6, 0xd8957a, false);
    for (const s of [-1, 1]) w.addBox(x + s * 9.5, g + 20, z + 0.4, 6, 9, 1, 0xd8957a, false);
    w.addMesh(new THREE.CylinderGeometry(3, 3, 8, 14), 0xd8957a, x, g + 21, z + 0.2);
    w.addMesh(new THREE.ConeGeometry(3.5, 2.6, 14), 0xc98a6a, x, g + 26.3, z + 0.2);
    w.addMesh(new THREE.SphereGeometry(1, 10, 8), 0xd8957a, x, g + 28.2, z + 0.2);  // the urn
    for (let k = 0; k < 4; k++) w.loot(x - 6 + k * 4, z + 5, 3, 'police', zone.id);
    rect(occ, x, z, 16, 8);
  },

  // ---------------- villages ----------------
  karak(w, L, occ, rng, zone) {
    const { x, z } = L, col = 0x9c8a6a;
    w.wallRing(x, z, 20, 9, 2, 5, col, occ);
    const g = base(w, x + 13, z - 13, 4, 4);
    w.addBox(x + 13, g + 8, z - 13, 8, 18, 8, col);                             // keep
    for (let i = -1; i <= 1; i++) w.addBox(x + 13 + i * 2.8, g + 17.6, z - 17, 1.2, 1.2, 0.8, col, false);
    w.addBox(x - 10, base(w, x - 10, z + 8, 5, 5) + 2.5, z + 8, 10, 7, 3, col);  // inner hall ruin
    for (const [lx, lz] of [[0, 0], [-8, -8], [6, 10]]) w.loot(x + lx, z + lz, 2, 'police', zone.id);
  },
  gadara(w, L, occ, rng, zone) {
    const { x, z } = L;
    colonnade(w, x - 14, z, x + 14, z, 4, 0.45, 5.5, BASALT, rng, 0.15);
    for (let r = 0; r < 4; r++) {
      const R = 7 + r * 1.5, n = 8 + r * 2;
      for (let i = 0; i < n; i++) {
        const am = Math.PI * (i + 0.5) / n, bx = x + Math.cos(am) * R, bz = z + 12 + Math.sin(am) * R, gy = w.getHeight(bx, bz);
        w.boxes.push({ cx: bx, cy: gy + r * 0.35, cz: bz, sx: 2 * R * Math.sin(Math.PI / (2 * n)) + 0.1, sy: 0.9 + r * 0.7, sz: 1.4, color: BASALT, rotY: -am + Math.PI / 2 });
        w.colliders.push({ minX: bx - 0.9, maxX: bx + 0.9, minY: gy - 1, maxY: gy + 1 + r * 0.7, minZ: bz - 0.9, maxZ: bz + 0.9, s: 0 });
      }
    }
    w.loot(x, z + 13, 2, 'police', zone.id);
    rect(occ, x, z + 8, 17, 15);
  },
};
