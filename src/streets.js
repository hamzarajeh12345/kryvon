// Streets and roads.
//  • City street grids (wide asphalt in modern cities, narrow stone lanes in old cities), reserved before
//    buildings are placed so houses line up along them.
//  • One merged, vertex-coloured mesh for every road surface: highways between cities (asphalt with lane
//    markings), dirt tracks to villages, city streets with pavements, old-city cobbles.
//  • Street lamps along modern streets.
import * as THREE from 'three';

// spacing = block size, w = street width, kind = surface
const CITY = {
  beirut:    { spacing: 30, w: 8,   kind: 'asphalt', walk: true,  lamps: true },
  amman:     { spacing: 30, w: 8,   kind: 'asphalt', walk: true,  lamps: true },
  damascus:  { spacing: 24, w: 4.5, kind: 'cobble' },
  jerusalem: { spacing: 24, w: 4.5, kind: 'cobble' },
  citadel:   { spacing: 22, w: 4,   kind: 'cobble' },
  resort:    { spacing: 34, w: 7,   kind: 'asphalt', walk: true,  lamps: true },
  jerash:    { spacing: 34, w: 6,   kind: 'asphalt' },
  village:   { spacing: 0,  w: 5,   kind: 'dirt' },     // one main street + one cross street
  desert:    { spacing: 0,  w: 5,   kind: 'dirt' },
  mountain:  { spacing: 0,  w: 5,   kind: 'dirt' },
};

export function buildStreets(world, zone, occ) {
  const cfg = CITY[zone.style]; if (!cfg) return;
  const R = zone.br * 0.95, lines = [];
  if (cfg.spacing) {
    const n = Math.floor(R / cfg.spacing);
    for (let k = -n; k <= n; k++) { const o = k * cfg.spacing + cfg.spacing / 2; if (Math.abs(o) < R) { lines.push({ axis: 'x', off: o }); lines.push({ axis: 'z', off: o }); } }
  } else { lines.push({ axis: 'x', off: 0 }); lines.push({ axis: 'z', off: 3 }); }

  const pieces = [];
  for (const L of lines) {
    const half = Math.sqrt(Math.max(0, R * R - L.off * L.off));
    for (let s = -half; s < half; s += 8) {
      const a = s, b = Math.min(half, s + 8), mid = (a + b) / 2;
      const x = zone.x + (L.axis === 'x' ? mid : L.off), z = zone.z + (L.axis === 'x' ? L.off : mid);
      const hw = L.axis === 'x' ? (b - a) / 2 : cfg.w / 2, hd = L.axis === 'x' ? cfg.w / 2 : (b - a) / 2;
      const r = { minX: x - hw, maxX: x + hw, minZ: z - hd, maxZ: z + hd };
      if (world.overlaps(r, occ, 0.5)) continue;                          // landmarks, walls
      if (world.isWater(x, z) || world.getHeight(x, z) < world.waterLevel(x, z) + 0.5) continue;
      const [m, M] = world.minH(r.minX, r.minZ, r.maxX, r.maxZ);
      if (M - m > 3) continue;                                             // too steep for a street
      pieces.push({ ...r, x, z, axis: L.axis, kind: cfg.kind, walk: cfg.walk });
    }
  }
  for (const p of pieces) {
    occ.push({ minX: p.minX - 0.6, maxX: p.maxX + 0.6, minZ: p.minZ - 0.6, maxZ: p.maxZ + 0.6 });
    world.streets.push(p);
  }
  // street lamps on modern streets (every other piece, on one side)
  if (cfg.lamps) pieces.forEach((p, i) => {
    if (i % 3) return;
    const side = (i % 2 ? 1 : -1) * (cfg.w / 2 + 0.6);
    const lx = p.axis === 'x' ? p.x : p.x + side, lz = p.axis === 'x' ? p.z + side : p.z;
    const g = world.getHeight(lx, lz);
    world.addCyl(lx, g - 0.2, lz, 0.08, 6.2, 0x3a3d40);
    const ax = p.axis === 'x' ? 0 : -Math.sign(side) * 0.9, az = p.axis === 'x' ? -Math.sign(side) * 0.9 : 0;
    world.addBox(lx + ax / 2, g + 6, lz + az / 2, p.axis === 'x' ? 0.12 : 1.0, 0.1, p.axis === 'x' ? 1.0 : 0.12, 0x3a3d40, false);
    world.addBox(lx + ax, g + 5.92, lz + az, 0.35, 0.12, 0.35, 0xfff2c4, false);
  });
}

// ---------------- the merged road mesh ----------------
const COL = {
  asphalt: [0.22, 0.225, 0.235], dirt: [0.56, 0.48, 0.36], cobble: [0.55, 0.5, 0.43],
  line: [0.92, 0.88, 0.72], edge: [0.85, 0.83, 0.78], walk: [0.68, 0.65, 0.6],
};

export function buildRoadMesh(world) {
  const pos = [], col = [];
  const H = (x, z, lift) => world.getHeight(x, z) + lift;
  const quad = (ax, az, bx, bz, cx, cz, dx, dz, c, lift, noise = 0.06) => {
    const v = [[ax, az], [bx, bz], [cx, cz], [ax, az], [cx, cz], [dx, dz]];
    const k = 1 - noise / 2 + Math.random() * noise;
    for (const [x, z] of v) { pos.push(x, H(x, z, lift), z); col.push(c[0] * k, c[1] * k, c[2] * k); }
  };
  // strip along a polyline with half-width hw
  const strip = (pts, hw, c, lift, dashed = false) => {
    for (let i = 0; i < pts.length - 1; i++) {
      const [x0, z0] = pts[i], [x1, z1] = pts[i + 1];
      const len = Math.hypot(x1 - x0, z1 - z0); if (len < 0.01) continue;
      const nx = -(z1 - z0) / len, nz = (x1 - x0) / len;
      if (dashed && i % 2) continue;
      quad(x0 + nx * hw, z0 + nz * hw, x1 + nx * hw, z1 + nz * hw, x1 - nx * hw, z1 - nz * hw, x0 - nx * hw, z0 - nz * hw, c, lift, dashed ? 0 : 0.08);
    }
  };
  const resample = (pts, step) => {
    const out = [pts[0]];
    for (let i = 0; i < pts.length - 1; i++) {
      const [x0, z0] = pts[i], [x1, z1] = pts[i + 1], n = Math.max(1, Math.ceil(Math.hypot(x1 - x0, z1 - z0) / step));
      for (let k = 1; k <= n; k++) out.push([x0 + (x1 - x0) * k / n, z0 + (z1 - z0) * k / n]);
    }
    return out;
  };
  const dry = ([x, z]) => !world.isWater(x, z) && world.getHeight(x, z) > world.waterLevel(x, z) + 0.3;

  // highways and tracks between towns
  for (const road of world.roadLines) {
    const fine = resample(road.pts, 4);
    // split where the road dips into water
    let run = [];
    const flush = () => {
      if (run.length > 1) {
        if (road.major) {
          strip(run, 4.2, COL.edge, 0.06);
          strip(run, 3.9, COL.asphalt, 0.08);
          strip(resample(run, 3), 0.12, COL.line, 0.1, true);
        } else strip(run, 2.6, COL.dirt, 0.06);
      }
      run = [];
    };
    for (const p of fine) { if (dry(p)) run.push(p); else flush(); }
    flush();
  }
  // city streets
  for (const s of world.streets) {
    const c = COL[s.kind];
    const steps = 2, along = s.axis === 'x';
    for (let i = 0; i < steps; i++) {
      const t0 = i / steps, t1 = (i + 1) / steps;
      const ax = along ? s.minX + (s.maxX - s.minX) * t0 : s.minX, bx = along ? s.minX + (s.maxX - s.minX) * t1 : s.maxX;
      const az = along ? s.minZ : s.minZ + (s.maxZ - s.minZ) * t0, bz = along ? s.maxZ : s.minZ + (s.maxZ - s.minZ) * t1;
      quad(ax, az, bx, az, bx, bz, ax, bz, c, 0.08, s.kind === 'cobble' ? 0.18 : 0.08);
    }
    if (s.walk) { // pavements on both sides
      if (along) { quad(s.minX, s.minZ - 1.4, s.maxX, s.minZ - 1.4, s.maxX, s.minZ, s.minX, s.minZ, COL.walk, 0.14); quad(s.minX, s.maxZ, s.maxX, s.maxZ, s.maxX, s.maxZ + 1.4, s.minX, s.maxZ + 1.4, COL.walk, 0.14); }
      else { quad(s.minX - 1.4, s.minZ, s.minX, s.minZ, s.minX, s.maxZ, s.minX - 1.4, s.maxZ, COL.walk, 0.14); quad(s.maxX, s.minZ, s.maxX + 1.4, s.minZ, s.maxX + 1.4, s.maxZ, s.maxX, s.maxZ, COL.walk, 0.14); }
    }
    if (s.kind === 'asphalt') { // centre dash
      const cx = s.x, cz = s.z, L = along ? (s.maxX - s.minX) * 0.35 : (s.maxZ - s.minZ) * 0.35;
      if (along) quad(cx - L, cz - 0.1, cx + L, cz - 0.1, cx + L, cz + 0.1, cx - L, cz + 0.1, COL.line, 0.1, 0);
      else quad(cx - 0.1, cz - L, cx + 0.1, cz - L, cx + 0.1, cz + L, cx - 0.1, cz + L, COL.line, 0.1, 0);
    }
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(pos, 3));
  geo.setAttribute('color', new THREE.Float32BufferAttribute(col, 3));
  geo.computeVertexNormals();
  const mat = new THREE.MeshLambertMaterial({ vertexColors: true, polygonOffset: true, polygonOffsetFactor: -2, polygonOffsetUnits: -2 });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.receiveShadow = true;
  world.scene.add(mesh);
  return mesh;
}

/** Points along roads/streets where vehicles can be parked: {x, z, yaw}. */
export function parkingSpots(world, rng) {
  const out = [];
  for (const road of world.roadLines) {
    const pts = road.pts;
    for (let i = 1; i < pts.length - 1; i++) {
      if (rng() > (road.major ? 0.16 : 0.1)) continue;
      const [x0, z0] = pts[i - 1], [x1, z1] = pts[i + 1], len = Math.hypot(x1 - x0, z1 - z0) || 1;
      const side = rng() < 0.5 ? 1 : -1, nx = -(z1 - z0) / len, nz = (x1 - x0) / len;
      const x = pts[i][0] + nx * side * (road.major ? 5.5 : 3.6), z = pts[i][1] + nz * side * (road.major ? 5.5 : 3.6);
      if (world.isWater(x, z)) continue;
      out.push({ x, z, yaw: Math.atan2(-(x1 - x0), -(z1 - z0)) + (rng() < 0.3 ? Math.PI : 0) });
    }
  }
  for (const s of world.streets) {
    if (rng() > 0.07) continue;
    const along = s.axis === 'x', side = rng() < 0.5 ? 1 : -1;
    const w = along ? s.maxZ - s.minZ : s.maxX - s.minX;
    if (w < 5) continue;                       // old-city lanes are too narrow to park in
    out.push({ x: along ? s.x : s.x + side * (w / 2 - 1.3), z: along ? s.z + side * (w / 2 - 1.3) : s.z, yaw: along ? Math.PI / 2 : 0 });
  }
  return out;
}
