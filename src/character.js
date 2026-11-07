// Low-poly character builder. A "look" describes face, hair, body and clothes; it becomes a handful of
// merged, vertex-coloured meshes (torso+head, hair, 2 arms, 2 legs) so 100 characters stay cheap to draw.
// Forward is -Z. Arms and legs are pivot groups so Actor can animate them.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { LOOK } from './look.js';

export const BODY_MAT = new THREE.MeshLambertMaterial({ vertexColors: true });
const C = new THREE.Color(), C2 = new THREE.Color();

// ---------- geometry helpers ----------
function prep(geo, color, pattern, m) {
  let g = geo.index ? geo.toNonIndexed() : geo.clone();
  for (const k of Object.keys(g.attributes)) if (k !== 'position' && k !== 'normal') g.deleteAttribute(k);
  if (!g.attributes.normal) g.computeVertexNormals();
  if (m) g.applyMatrix4(m);
  const pos = g.attributes.position, n = pos.count, col = new Float32Array(n * 3);
  const base = new THREE.Color(color);
  for (let t = 0; t < n; t += 3) {
    const cx = (pos.getX(t) + pos.getX(t + 1) + pos.getX(t + 2)) / 3, cy = (pos.getY(t) + pos.getY(t + 1) + pos.getY(t + 2)) / 3, cz = (pos.getZ(t) + pos.getZ(t + 1) + pos.getZ(t + 2)) / 3;
    C.copy(base);
    if (pattern) pattern(C, cx, cy, cz, t / 3);
    for (let v = 0; v < 3; v++) { col[(t + v) * 3] = C.r; col[(t + v) * 3 + 1] = C.g; col[(t + v) * 3 + 2] = C.b; }
  }
  g.setAttribute('color', new THREE.BufferAttribute(col, 3));
  return g;
}
const M4 = (x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0, sx = 1, sy = 1, sz = 1) =>
  new THREE.Matrix4().compose(new THREE.Vector3(x, y, z), new THREE.Quaternion().setFromEuler(new THREE.Euler(rx, ry, rz)), new THREE.Vector3(sx, sy, sz));
const hash = (a, b, c) => { const s = Math.sin(a * 127.1 + b * 311.7 + c * 74.7) * 43758.5453; return s - Math.floor(s); };

// fabric patterns, applied per triangle
const PATTERNS = {
  plain: null,
  camo: base => (c, x, y, z) => { const v = Math.sin(x * 23 + Math.sin(y * 17) * 2) + Math.sin(z * 19 + y * 13); c.copy(base).multiplyScalar(v > 0.9 ? 0.62 : v < -0.6 ? 1.28 : 1); },
  digital: base => (c, x, y, z) => { const h = hash(Math.floor(x * 30), Math.floor(y * 30), Math.floor(z * 30)); c.copy(base).multiplyScalar(h < 0.3 ? 0.65 : h > 0.8 ? 1.35 : 1); },
  tiger: base => (c, x, y) => { const v = Math.sin(y * 60 + Math.sin(x * 20) * 3); c.copy(base).multiplyScalar(v > 0.55 ? 0.55 : 1.05); },
};
const pat = (name, color) => PATTERNS[name] ? PATTERNS[name](new THREE.Color(color)) : null;
const checker = (a, b) => { const A = new THREE.Color(a), B = new THREE.Color(b); return (c, x, y, z) => c.copy(((Math.floor(x * 45) + Math.floor(y * 45) + Math.floor(z * 45)) & 1) ? A : B); };

// cut a face opening into a sphere (for the hijab)
function withoutFront(geo, test) {
  const g = geo.toNonIndexed(), p = g.attributes.position, keep = [];
  for (let t = 0; t < p.count; t += 3) {
    const cx = (p.getX(t) + p.getX(t + 1) + p.getX(t + 2)) / 3, cy = (p.getY(t) + p.getY(t + 1) + p.getY(t + 2)) / 3, cz = (p.getZ(t) + p.getZ(t + 1) + p.getZ(t + 2)) / 3;
    if (!test(cx, cy, cz)) for (let v = 0; v < 3; v++) keep.push(p.getX(t + v), p.getY(t + v), p.getZ(t + v));
  }
  const out = new THREE.BufferGeometry(); out.setAttribute('position', new THREE.Float32BufferAttribute(keep, 3)); out.computeVertexNormals();
  return out;
}

const merged = parts => { const g = mergeGeometries(parts, false); parts.forEach(p => p.dispose()); return g; };
const mesh = g => { const m = new THREE.Mesh(g, BODY_MAT); m.castShadow = true; return m; };

// ---------- the builder ----------
// returns { torso, hair, armL, armR, legL, legR } (meshes/groups positioned in the actor's local space)
export function buildBody(look) {
  const L = { ...LOOK.defaults, ...look };
  const skin = LOOK.skins[L.skin] ?? LOOK.skins[2];
  const hairC = LOOK.hairColors[L.hairColor] ?? LOOK.hairColors[0];
  const topC = LOOK.clothColors[L.topColor] ?? 0x222222, pantsC = LOOK.clothColors[L.pantsColor] ?? 0x222222, vestC = LOOK.clothColors[L.vestColor] ?? 0x1c1c1c;
  const female = L.sex === 'f', heavy = L.build === 'heavy' ? 1.18 : L.build === 'slim' ? 0.9 : 1;
  const sh = (female ? 0.86 : 1) * heavy;            // shoulder width factor
  const sleeves = { tank: 0, tee: 1, longsleeve: 2, hoodie: 2, jacket: 2, fur: 2, suit: 2 }[L.top] ?? 1;
  const topPat = pat(L.topPattern, topC), pantsPat = pat(L.pantsPattern, pantsC);
  const gloveC = L.gloves ? 0x1a1a1a : skin;
  const T = [];

  // pelvis, belt, torso, neck
  T.push(prep(new THREE.BoxGeometry(0.36 * sh, 0.2, 0.22), pantsC, pantsPat, M4(0, 0.94, 0)));
  T.push(prep(new THREE.BoxGeometry(0.38 * sh, 0.06, 0.24), 0x2a2420, null, M4(0, 1.03, 0)));
  const torsoTop = L.top === 'tank' ? skin : topC;
  T.push(prep(new THREE.CapsuleGeometry(0.17, 0.32, 4, 10), topC, topPat, M4(0, 1.27, 0, 0, 0, 0, 1.28 * sh, 1, 0.78)));
  if (L.top === 'tank') { // bare shoulders + neckline
    for (const s of [-1, 1]) T.push(prep(new THREE.SphereGeometry(0.09, 8, 6), skin, null, M4(s * 0.21 * sh, 1.45, 0, 0, 0, 0, 1 * heavy, 0.8, 0.9)));
    T.push(prep(new THREE.BoxGeometry(0.16, 0.08, 0.02), skin, null, M4(0, 1.47, -0.125)));
  }
  T.push(prep(new THREE.CylinderGeometry(0.07, 0.08, 0.12, 8), skin, null, M4(0, 1.55, 0)));
  if (L.top === 'suit') {
    T.push(prep(new THREE.BoxGeometry(0.12, 0.26, 0.02), 0xf2f2ee, null, M4(0, 1.36, -0.135)));
    T.push(prep(new THREE.BoxGeometry(0.035, 0.22, 0.02), 0x1a1a1a, null, M4(0, 1.33, -0.147)));
  }
  if (L.top === 'hoodie') T.push(prep(new THREE.TorusGeometry(0.13, 0.05, 6, 12, Math.PI * 1.2), topC, topPat, M4(0, 1.5, 0.06, Math.PI / 2 - 0.3, 0, Math.PI * 0.4)));
  if (L.top === 'fur') T.push(prep(new THREE.TorusGeometry(0.13, 0.055, 6, 14), 0xc8b08a, (c, x, y, z) => c.multiplyScalar(0.85 + 0.3 * hash(x * 9, y * 9, z * 9)), M4(0, 1.5, 0, Math.PI / 2)));
  if (L.top === 'jacket') T.push(prep(new THREE.BoxGeometry(0.3 * sh, 0.06, 0.22), topC, topPat, M4(0, 1.5, 0)));

  // vest / harness
  if (L.vest === 'plate') {
    T.push(prep(new THREE.BoxGeometry(0.4 * sh, 0.36, 0.3), vestC, (c, x, y, z) => c.multiplyScalar(0.9 + 0.15 * hash(Math.floor(x * 12), Math.floor(y * 12), z)), M4(0, 1.27, 0)));
    for (let i = -1; i <= 1; i++) T.push(prep(new THREE.BoxGeometry(0.1, 0.12, 0.06), vestC, c => c.multiplyScalar(0.8), M4(i * 0.12 * sh, 1.16, -0.17)));
    for (const s of [-1, 1]) T.push(prep(new THREE.BoxGeometry(0.08, 0.04, 0.32), vestC, null, M4(s * 0.15 * sh, 1.46, 0)));
  } else if (L.vest === 'harness') {
    for (const s of [-1, 1]) T.push(prep(new THREE.BoxGeometry(0.05, 0.42, 0.3), vestC, null, M4(s * 0.12 * sh, 1.28, 0, 0, 0, s * 0.12)));
    T.push(prep(new THREE.BoxGeometry(0.4 * sh, 0.05, 0.27), vestC, null, M4(0, 1.18, 0)));
    T.push(prep(new THREE.BoxGeometry(0.1, 0.09, 0.05), vestC, null, M4(0.1, 1.08, -0.14)));
  }
  if (L.tags) { T.push(prep(new THREE.BoxGeometry(0.03, 0.045, 0.006), 0xc0c4c8, null, M4(0.02, 1.36, -0.145))); T.push(prep(new THREE.TorusGeometry(0.08, 0.004, 3, 16), 0xb0b4b8, null, M4(0, 1.44, -0.06, 1.2))); }
  if (L.patch !== 'none') {
    const px = -0.235 * sh, py = 1.4;
    if (L.patch === 'jo') { // Jordan flag: black / white / green with a red triangle and a white star
      T.push(prep(new THREE.BoxGeometry(0.012, 0.024, 0.11), 0x111111, null, M4(px, py + 0.024, 0)));
      T.push(prep(new THREE.BoxGeometry(0.012, 0.024, 0.11), 0xffffff, null, M4(px, py, 0)));
      T.push(prep(new THREE.BoxGeometry(0.012, 0.024, 0.11), 0x0a7a3a, null, M4(px, py - 0.024, 0)));
      const tri = new THREE.Shape([new THREE.Vector2(-0.036, 0), new THREE.Vector2(0.036, 0), new THREE.Vector2(0, 0.05)]);
      T.push(prep(new THREE.ShapeGeometry(tri), 0xce1126, null, M4(px - 0.008, py, -0.055, 0, -Math.PI / 2, -Math.PI / 2)));
      T.push(prep(new THREE.SphereGeometry(0.006, 4, 3), 0xffffff, null, M4(px - 0.01, py, -0.04)));
    } else if (L.patch === 'crown') T.push(prep(new THREE.BoxGeometry(0.012, 0.07, 0.08), 0x2a2a2a, (c, x, y) => c.setHex(y > py + 0.005 ? 0xd9a520 : 0x2a2a2a), M4(px, py, 0)));
    else if (L.patch === 'olive') T.push(prep(new THREE.CircleGeometry(0.04, 10), 0x5a7a3a, null, M4(px - 0.006, py, 0, 0, -Math.PI / 2)));
  }

  // head
  const hw = [1, 1.06, 0.94][L.face] ?? 1, hh = [1, 0.97, 1.05][L.face] ?? 1;
  T.push(prep(new THREE.SphereGeometry(0.2, 14, 11), skin, null, M4(0, 1.74, 0, 0, 0, 0, hw * (female ? 0.94 : 1), hh, 1)));
  T.push(prep(new THREE.BoxGeometry(0.035, 0.06, 0.05), skin, null, M4(0, 1.72, -0.2)));                          // nose
  for (const s of [-1, 1]) T.push(prep(new THREE.SphereGeometry(0.04, 6, 5), skin, null, M4(s * 0.2 * hw, 1.73, 0.01, 0, 0, 0, 0.5, 1, 0.8))); // ears
  const eyeC = LOOK.eyeColors[L.eyes] ?? 0x3a2414;
  for (const s of [-1, 1]) {
    T.push(prep(new THREE.SphereGeometry(0.026, 8, 6), 0xf4f1ea, null, M4(s * 0.07, 1.765, -0.172, 0, 0, 0, 1, 0.75, 0.6)));
    T.push(prep(new THREE.SphereGeometry(0.014, 6, 5), eyeC, null, M4(s * 0.07, 1.765, -0.187)));
    T.push(prep(new THREE.BoxGeometry(0.06, 0.014, 0.02), hairC, null, M4(s * 0.07, 1.805, -0.18, 0, 0, s * (female ? -0.08 : 0.06))));
  }
  T.push(prep(new THREE.BoxGeometry(0.07, 0.015, 0.02), L.lips === 'red' ? 0xb0182a : new THREE.Color(skin).multiplyScalar(0.75).getHex(), null, M4(0, 1.655, -0.188)));
  // beard
  if (!female && L.beard !== 'none') {
    const bc = L.beard === 'stubble' ? new THREE.Color(hairC).lerp(new THREE.Color(skin), 0.55).getHex() : hairC;
    if (L.beard !== 'goatee') T.push(prep(withoutFront(new THREE.SphereGeometry(0.206, 14, 10, 0, Math.PI * 2, Math.PI * 0.52, Math.PI * 0.4), (x, y, z) => (y > -0.05 && z < -0.1 && Math.abs(x) < 0.06) || z > 0.05), bc, null, M4(0, 1.74, 0.005, 0, 0, 0, hw, hh, 1)));
    T.push(prep(new THREE.BoxGeometry(0.1, 0.022, 0.03), bc, null, M4(0, 1.678, -0.188)));                     // moustache
    if (L.beard === 'goatee') T.push(prep(new THREE.BoxGeometry(0.06, 0.07, 0.05), bc, null, M4(0, 1.6, -0.165)));
  }
  // glasses
  if (L.glasses !== 'none') {
    const lens = L.glasses === 'aviator' ? 0x15181c : 0xcfe7f2, frame = L.glasses === 'aviator' ? 0x8a7a50 : 0x222222;
    for (const s of [-1, 1]) T.push(prep(new THREE.BoxGeometry(0.075, 0.05, 0.012), lens, null, M4(s * 0.07, 1.762, -0.198, 0, 0, s * -0.1)));
    T.push(prep(new THREE.BoxGeometry(0.05, 0.008, 0.01), frame, null, M4(0, 1.78, -0.2)));
    for (const s of [-1, 1]) T.push(prep(new THREE.BoxGeometry(0.008, 0.008, 0.2), frame, null, M4(s * 0.165 * hw, 1.775, -0.1)));
  }
  // scarf / shemagh around the neck (not with a hijab)
  if (L.scarf !== 'none' && !L.hijab) {
    const sc = { shemagh_bw: checker(0x111111, 0xeeeeee), shemagh_rw: checker(0xb3202a, 0xf2f2f2), olive: null, black: null }[L.scarf];
    const base = { shemagh_bw: 0xeeeeee, shemagh_rw: 0xf2f2f2, olive: 0x4f6b3a, black: 0x161616 }[L.scarf];
    T.push(prep(new THREE.TorusGeometry(0.12, 0.06, 8, 16), base, sc, M4(0, 1.53, 0, Math.PI / 2, 0, 0, 1.05, 1, 1)));
    T.push(prep(new THREE.ConeGeometry(0.12, 0.22, 4, 1), base, sc, M4(0, 1.4, -0.12, 0.25, Math.PI / 4, Math.PI, 1, 1, 0.35)));
  }
  const torso = mesh(merged(T));

  // hair / hijab (separate so a helmet can hide it)
  const H = [];
  if (L.hijab) {
    const hc = LOOK.clothColors[L.hijabColor] ?? 0x111111;
    H.push(prep(withoutFront(new THREE.SphereGeometry(0.228, 16, 12), (x, y, z) => z < -0.12 && y > -0.13 && y < 0.1 && Math.abs(x) < 0.14), hc, (c, x, y, z) => c.multiplyScalar(0.92 + 0.12 * hash(x * 30, y * 30, z * 30)), M4(0, 1.74, 0.01, 0, 0, 0, hw * 0.98, hh * 1.04, 1.02)));
    H.push(prep(new THREE.CylinderGeometry(0.16, 0.3 * sh, 0.26, 12, 1, true), hc, null, M4(0, 1.47, 0.02)));       // drape over the shoulders
    H.push(prep(new THREE.BoxGeometry(0.3, 0.3, 0.04), hc, null, M4(0, 1.48, 0.19, -0.15)));                      // back
    H.push(prep(new THREE.ConeGeometry(0.17, 0.2, 10, 1, true), hc, null, M4(0, 1.36, -0.08, 0.1, 0, Math.PI)));   // front fold
  } else {
    const st = L.hair;
    if (st === 'curly') {
      for (let i = 0; i < 46; i++) {
        const a = hash(i, 1, 7) * Math.PI * 2, el = 0.15 + hash(i, 3, 1) * 1.15;
        if (Math.sin(a) < -0.55 && el > 0.95) continue; // keep the forehead clear
        const r = 0.2;
        const x = Math.cos(a) * Math.sin(el) * r * hw, y = 1.74 + Math.cos(el) * r * hh + 0.02, z = Math.sin(a) * Math.sin(el) * r;
        if (y < 1.74 && z < 0) continue;
        H.push(prep(new THREE.IcosahedronGeometry(0.05 + hash(i, 9, 2) * 0.02, 0), hairC, c => c.multiplyScalar(0.85 + 0.3 * hash(i, 2, 2)), M4(x, y, z)));
      }
    } else if (st !== 'bald') {
      const cap = st === 'buzz' ? 0.204 : 0.214;
      H.push(prep(new THREE.SphereGeometry(cap, 14, 8, 0, Math.PI * 2, 0, Math.PI * (st === 'buzz' ? 0.42 : 0.5)), hairC, null, M4(0, 1.75, 0.015, -0.25, 0, 0, hw, hh, 1)));
      if (st === 'long') H.push(prep(new THREE.BoxGeometry(0.34, 0.36, 0.1), hairC, null, M4(0, 1.6, 0.13)));
      if (st === 'bun') H.push(prep(new THREE.SphereGeometry(0.075, 8, 6), hairC, null, M4(0, 1.86, 0.16)));
      if (st === 'short') H.push(prep(new THREE.BoxGeometry(0.3, 0.06, 0.08), hairC, null, M4(0, 1.88, -0.12, 0.3)));
    }
  }
  const hair = H.length ? mesh(merged(H)) : new THREE.Group();

  // arms (pivot at the shoulder, hanging down)
  const arm = side => {
    const A = [], up = sleeves >= 1 ? topC : skin, low = sleeves >= 2 ? topC : skin, uPat = sleeves >= 1 ? topPat : null, lPat = sleeves >= 2 ? topPat : null, k = heavy * (female ? 0.9 : 1);
    A.push(prep(new THREE.CapsuleGeometry(0.065 * k, 0.2, 3, 8), up, uPat, M4(0, -0.14, 0)));
    if (sleeves === 1) A.push(prep(new THREE.CapsuleGeometry(0.06 * k, 0.12, 3, 8), skin, null, M4(0, -0.2, 0)));
    A.push(prep(new THREE.CapsuleGeometry(0.055 * k, 0.2, 3, 8), low, lPat, M4(0, -0.4, 0)));
    if (L.top === 'suit' || L.top === 'jacket') A.push(prep(new THREE.CylinderGeometry(0.062 * k, 0.062 * k, 0.04, 8), L.top === 'suit' ? 0xf2f2ee : topC, null, M4(0, -0.5, 0)));
    A.push(prep(new THREE.BoxGeometry(0.08, 0.1, 0.06), gloveC, null, M4(0, -0.58, 0)));
    const g = new THREE.Group(); g.add(mesh(merged(A))); g.position.set(side * 0.27 * sh, 1.47, 0);
    return g;
  };
  // legs (pivot at the hip)
  const leg = side => {
    const A = [];
    A.push(prep(new THREE.CapsuleGeometry(0.085, 0.3, 3, 8), pantsC, pantsPat, M4(0, -0.22, 0)));
    A.push(prep(new THREE.CapsuleGeometry(0.07, 0.3, 3, 8), pantsC, pantsPat, M4(0, -0.58, 0)));
    if (L.vest === 'plate') A.push(prep(new THREE.BoxGeometry(0.12, 0.1, 0.05), 0x1a1a1a, null, M4(0, -0.42, -0.07)));
    A.push(prep(new THREE.BoxGeometry(0.13, 0.12, 0.25), L.boots === 'tan' ? 0x8a6a44 : 0x1c1a18, null, M4(0, -0.82, -0.035)));
    const g = new THREE.Group(); g.add(mesh(merged(A))); g.position.set(side * 0.1, 0.9, 0);
    return g;
  };
  return { torso, hair, armL: arm(-1), armR: arm(1), legL: leg(-1), legR: leg(1) };
}

// weapon model: greys in vertex colours, tinted by the skin colour on its own material
export function buildGun() {
  const P = [];
  P.push(prep(new THREE.BoxGeometry(0.07, 0.11, 0.42), 0xbbbbbb, null, M4(0, 0, -0.18)));
  P.push(prep(new THREE.CylinderGeometry(0.018, 0.018, 0.36, 8), 0x777777, null, M4(0, 0.02, -0.56, Math.PI / 2)));
  P.push(prep(new THREE.BoxGeometry(0.055, 0.15, 0.07), 0x999999, null, M4(0, -0.11, -0.2, 0.2)));
  P.push(prep(new THREE.BoxGeometry(0.05, 0.1, 0.05), 0x999999, null, M4(0, -0.08, -0.02, -0.3)));
  P.push(prep(new THREE.BoxGeometry(0.06, 0.09, 0.22), 0xaaaaaa, null, M4(0, -0.01, 0.13)));
  P.push(prep(new THREE.BoxGeometry(0.03, 0.03, 0.2), 0x666666, null, M4(0, 0.075, -0.22)));
  const g = mergeGeometries(P, false);
  return g;
}
