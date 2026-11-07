// Low-poly character builder. A "look" describes face, hair, body and clothes; it becomes a handful of
// merged, vertex-coloured meshes (torso+head, hair, 2 arms, 2 legs) so 100 characters stay cheap to draw.
// Forward is -Z. Arms and legs are pivot groups so Actor can animate them.
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { LOOK } from './look.js';

export const BODY_MAT = new THREE.MeshLambertMaterial({ vertexColors: true });
const C = new THREE.Color(), C2 = new THREE.Color();

// ---------- geometry helpers ----------
export function prep(geo, color, pattern, m) {
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
export const M4 = (x = 0, y = 0, z = 0, rx = 0, ry = 0, rz = 0, sx = 1, sy = 1, sz = 1) =>
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
  const sleeves = { tank: 0, tee: 1, longsleeve: 2, hoodie: 2, jacket: 2, fur: 2, suit: 2, thobe: 2, armor: 2 }[L.top] ?? 1;
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
  if (L.top === 'thobe') { // long robe down to the shins, with a placket and pocket
    T.push(prep(new THREE.CylinderGeometry(0.2 * sh, 0.27, 0.62, 12, 1, true), topC, topPat, M4(0, 0.66, 0)));
    T.push(prep(new THREE.BoxGeometry(0.03, 0.22, 0.01), new THREE.Color(topC).multiplyScalar(0.8).getHex(), null, M4(0, 1.42, -0.136)));
    T.push(prep(new THREE.BoxGeometry(0.07, 0.05, 0.01), new THREE.Color(topC).multiplyScalar(0.85).getHex(), null, M4(-0.1, 1.33, -0.137)));
  }
  if (L.top === 'armor') { // bulky plates, collar and belly guard
    T.push(prep(new THREE.BoxGeometry(0.46 * sh, 0.42, 0.34), topC, (c, x, y, z) => c.multiplyScalar(0.85 + 0.25 * hash(Math.floor(x * 8), Math.floor(y * 8), Math.floor(z * 8))), M4(0, 1.27, 0)));
    T.push(prep(new THREE.TorusGeometry(0.15, 0.05, 6, 14), topC, null, M4(0, 1.52, 0, Math.PI / 2)));
    T.push(prep(new THREE.BoxGeometry(0.3 * sh, 0.14, 0.26), topC, c => c.multiplyScalar(0.8), M4(0, 1.0, 0)));
  }
  if (L.gear === 'medic') {
    T.push(prep(new THREE.BoxGeometry(0.16, 0.14, 0.1), 0xf0ece4, null, M4(0.24 * sh, 1.02, 0.02)));
    T.push(prep(new THREE.BoxGeometry(0.06, 0.02, 0.012), 0xc0262d, null, M4(0.24 * sh, 1.03, -0.035)));
    T.push(prep(new THREE.BoxGeometry(0.02, 0.06, 0.012), 0xc0262d, null, M4(0.24 * sh, 1.03, -0.035)));
    T.push(prep(new THREE.BoxGeometry(0.03, 0.5, 0.02), 0x5a4a32, null, M4(0.02, 1.3, -0.15, 0, 0, -0.75)));            // strap
  } else if (L.gear === 'drone') {
    T.push(prep(new THREE.BoxGeometry(0.3, 0.36, 0.16), 0x3a3f34, null, M4(0, 1.3, 0.2)));
    for (const sx of [-1, 1]) for (const sy of [-1, 1]) {
      T.push(prep(new THREE.BoxGeometry(0.2, 0.015, 0.02), 0x222222, null, M4(sx * 0.12, 1.5 + sy * 0.02, 0.24, 0, 0, sx * sy * 0.6)));
      T.push(prep(new THREE.CylinderGeometry(0.07, 0.07, 0.01, 12), 0x555555, null, M4(sx * 0.21, 1.53 + sy * 0.06, 0.24)));
    }
    T.push(prep(new THREE.BoxGeometry(0.12, 0.05, 0.1), 0x2a2a2a, null, M4(0, 1.52, 0.24)));
  } else if (L.gear === 'radio') {
    T.push(prep(new THREE.BoxGeometry(0.26, 0.32, 0.14), 0x3a3f34, null, M4(0, 1.28, 0.2)));
    T.push(prep(new THREE.CylinderGeometry(0.006, 0.006, 0.5, 4), 0x111111, null, M4(0.1, 1.68, 0.22, 0, 0, -0.08)));
  }

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
    else if (L.patch === 'medic') { T.push(prep(new THREE.BoxGeometry(0.012, 0.07, 0.07), 0xf2f2f2, null, M4(px, py, 0))); T.push(prep(new THREE.TorusGeometry(0.022, 0.007, 4, 10, Math.PI * 1.4), 0xc0262d, null, M4(px - 0.008, py, 0, 0, -Math.PI / 2, 0.8))); }
  }
  if (L.patchBoth && L.patch !== 'none') { // mirror the shoulder patch on the right arm
    const n = T.length, px = 0.235 * sh;
    if (L.patch === 'jo') { T.push(prep(new THREE.BoxGeometry(0.012, 0.024, 0.11), 0x111111, null, M4(px, 1.424, 0))); T.push(prep(new THREE.BoxGeometry(0.012, 0.024, 0.11), 0xffffff, null, M4(px, 1.4, 0))); T.push(prep(new THREE.BoxGeometry(0.012, 0.024, 0.11), 0x0a7a3a, null, M4(px, 1.376, 0))); }
    else T.push(prep(new THREE.BoxGeometry(0.012, 0.07, 0.08), L.patch === 'crown' ? 0xd9a520 : L.patch === 'medic' ? 0xf2f2f2 : 0x5a7a3a, null, M4(px, 1.4, 0)));
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
    if (st === 'curly' && !['cap', 'cap_nvg', 'heavy_helmet', 'ghutra'].includes(L.headwear)) {
      for (let i = 0; i < 46; i++) {
        const a = hash(i, 1, 7) * Math.PI * 2, el = 0.15 + hash(i, 3, 1) * 1.15;
        if (Math.sin(a) < -0.55 && el > 0.95) continue; // keep the forehead clear
        const r = 0.2;
        const x = Math.cos(a) * Math.sin(el) * r * hw, y = 1.74 + Math.cos(el) * r * hh + 0.02, z = Math.sin(a) * Math.sin(el) * r;
        if (y < 1.74 && z < 0) continue;
        H.push(prep(new THREE.IcosahedronGeometry(0.05 + hash(i, 9, 2) * 0.02, 0), hairC, c => c.multiplyScalar(0.85 + 0.3 * hash(i, 2, 2)), M4(x, y, z)));
      }
    } else if (st !== 'bald' && !['cap', 'cap_nvg', 'heavy_helmet', 'ghutra'].includes(L.headwear)) {
      const cap = st === 'buzz' ? 0.204 : 0.214;
      H.push(prep(new THREE.SphereGeometry(cap, 14, 8, 0, Math.PI * 2, 0, Math.PI * (st === 'buzz' ? 0.42 : 0.5)), hairC, null, M4(0, 1.75, 0.015, -0.25, 0, 0, hw, hh, 1)));
      if (st === 'long') H.push(prep(new THREE.BoxGeometry(0.34, 0.36, 0.1), hairC, null, M4(0, 1.6, 0.13)));
      if (st === 'ponytail') { H.push(prep(new THREE.SphereGeometry(0.05, 8, 6), hairC, null, M4(0, 1.8, 0.2))); H.push(prep(new THREE.CapsuleGeometry(0.045, 0.22, 3, 8), hairC, null, M4(0, 1.62, 0.25, 0.35))); }
      if (st === 'bun') H.push(prep(new THREE.SphereGeometry(0.075, 8, 6), hairC, null, M4(0, 1.86, 0.16)));
      if (st === 'short') H.push(prep(new THREE.BoxGeometry(0.3, 0.06, 0.08), hairC, null, M4(0, 1.88, -0.12, 0.3)));
    }
  }
  if (!L.hijab && L.hair === 'ponytail' && ['cap', 'cap_nvg', 'heavy_helmet'].includes(L.headwear)) H.push(prep(new THREE.CapsuleGeometry(0.045, 0.22, 3, 8), hairC, null, M4(0, 1.6, 0.26, 0.35)));
  // headwear
  const hw2 = L.hijab && L.headwear !== 'headset' ? 'none' : L.headwear;
  if (hw2 === 'cap' || hw2 === 'cap_nvg') {
    const capC = L.headwear === 'cap_nvg' ? topC : (LOOK.clothColors[L.vestColor] ?? 0x2a2d30);
    H.push(prep(new THREE.SphereGeometry(0.218, 14, 8, 0, Math.PI * 2, 0, Math.PI * 0.5), capC, topPat, M4(0, 1.77, 0.01, -0.12, 0, 0, hw, 0.85, 1.02)));
    H.push(prep(new THREE.BoxGeometry(0.24, 0.015, 0.13), capC, null, M4(0, 1.83, -0.24, -0.1)));
    if (hw2 === 'cap_nvg') {
      H.push(prep(new THREE.BoxGeometry(0.06, 0.05, 0.06), 0x1a1a1a, null, M4(0, 1.9, -0.2)));
      for (const s of [-1, 1]) H.push(prep(new THREE.CylinderGeometry(0.024, 0.028, 0.1, 10), 0x111111, null, M4(s * 0.04, 1.9, -0.27, Math.PI / 2)));
      H.push(prep(new THREE.BoxGeometry(0.17, 0.03, 0.02), 0x222222, null, M4(0, 1.88, 0.2)));
    }
  }
  if (hw2 === 'headset' || hw2 === 'cap_nvg') {
    H.push(prep(new THREE.TorusGeometry(0.222, 0.014, 4, 20, Math.PI), 0x1c1c1c, null, M4(0, 1.75, 0.02)));
    for (const s of [-1, 1]) H.push(prep(new THREE.CylinderGeometry(0.055, 0.055, 0.05, 12), 0x2a2a2a, null, M4(s * 0.215 * hw, 1.73, 0.01, 0, 0, Math.PI / 2)));
    H.push(prep(new THREE.BoxGeometry(0.01, 0.01, 0.14), 0x1c1c1c, null, M4(-0.2 * hw, 1.68, -0.1, 0.4)));            // mic boom
  }
  if (hw2 === 'ghutra') { // checked head-cloth with a black agal
    const gc = checker(0xb3202a, 0xf2f2f2);
    H.push(prep(withoutFront(new THREE.SphereGeometry(0.232, 16, 12), (x, y, z) => z < -0.12 && y > -0.13 && y < 0.11 && Math.abs(x) < 0.15), 0xf2f2f2, gc, M4(0, 1.75, 0.01, 0, 0, 0, hw, hh * 1.03, 1.02)));
    H.push(prep(new THREE.BoxGeometry(0.34, 0.3, 0.05), 0xf2f2f2, gc, M4(0, 1.56, 0.19, -0.2)));
    for (const s of [-1, 1]) H.push(prep(new THREE.BoxGeometry(0.05, 0.28, 0.1), 0xf2f2f2, gc, M4(s * 0.2, 1.5, -0.05, 0, 0, s * 0.15)));
    for (const dy of [0, 0.03]) H.push(prep(new THREE.TorusGeometry(0.19, 0.016, 6, 22), 0x111111, null, M4(0, 1.86 + dy, 0.02, Math.PI / 2 - 0.2)));
  }
  if (hw2 === 'heavy_helmet') {
    const hc = new THREE.Color(topC).multiplyScalar(0.9).getHex();
    H.push(prep(new THREE.SphereGeometry(0.25, 16, 10, 0, Math.PI * 2, 0, Math.PI * 0.58), hc, null, M4(0, 1.75, 0.01, -0.1, 0, 0, hw, 1, 1.04)));
    H.push(prep(new THREE.BoxGeometry(0.32, 0.09, 0.03), 0x1b2b38, null, M4(0, 1.79, -0.235, 0.2)));                // visor
    for (const s of [-1, 1]) H.push(prep(new THREE.BoxGeometry(0.05, 0.12, 0.12), hc, null, M4(s * 0.23 * hw, 1.7, 0)));
    H.push(prep(new THREE.BoxGeometry(0.06, 0.05, 0.05), 0x222222, null, M4(0.18, 1.86, 0.08)));                      // light
  }
  const hair = H.length ? mesh(merged(H)) : new THREE.Group();

  // arms (pivot at the shoulder, hanging down)
  const arm = side => {
    void side;
    const A = [], up = sleeves >= 1 ? topC : skin, low = sleeves >= 2 ? topC : skin, uPat = sleeves >= 1 ? topPat : null, lPat = sleeves >= 2 ? topPat : null, k = heavy * (female ? 0.9 : 1);
    A.push(prep(new THREE.CapsuleGeometry(0.065 * k, 0.2, 3, 8), up, uPat, M4(0, -0.14, 0)));
    if (sleeves === 1) A.push(prep(new THREE.CapsuleGeometry(0.06 * k, 0.12, 3, 8), skin, null, M4(0, -0.2, 0)));
    A.push(prep(new THREE.CapsuleGeometry(0.055 * k, 0.2, 3, 8), low, lPat, M4(0, -0.4, 0)));
    if (L.top === 'suit' || L.top === 'jacket') A.push(prep(new THREE.CylinderGeometry(0.062 * k, 0.062 * k, 0.04, 8), L.top === 'suit' ? 0xf2f2ee : topC, null, M4(0, -0.5, 0)));
    A.push(prep(new THREE.BoxGeometry(0.08, 0.1, 0.06), gloveC, null, M4(0, -0.58, 0)));
    if (L.top === 'armor') A.push(prep(new THREE.SphereGeometry(0.1 * k, 8, 6, 0, Math.PI * 2, 0, Math.PI * 0.55), topC, null, M4(0, -0.02, 0, 0, 0, 0, 1.15, 0.9, 1.1)));
    if (L.watch && side < 0) { A.push(prep(new THREE.CylinderGeometry(0.06 * k, 0.06 * k, 0.04, 10), 0x1a1a1a, null, M4(0, -0.5, 0))); A.push(prep(new THREE.BoxGeometry(0.045, 0.045, 0.02), 0x2a2a2a, null, M4(0, -0.5, -0.06 * k))); }
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

// weapon models per class: greys/wood in vertex colours, tinted by the weapon skin on the material
const WOOD = 0x9a6a40;
export function buildGun(cls = 'AR') {
  const P = [], b = (w, h, d, c, x, y, z, rx = 0) => P.push(prep(new THREE.BoxGeometry(w, h, d), c, null, M4(x, y, z, rx)));
  const cyl = (r, l, c, x, y, z) => P.push(prep(new THREE.CylinderGeometry(r, r, l, 8), c, null, M4(x, y, z, Math.PI / 2)));
  const scope = (r, l, y = 0.085, z = -0.2) => { cyl(r, l, 0x333333, 0, y, z); cyl(r * 1.3, 0.04, 0x444444, 0, y, z - l / 2); cyl(r * 1.2, 0.04, 0x444444, 0, y, z + l / 2); b(0.02, 0.04, 0.03, 0x555555, 0, y - r - 0.01, z); };
  switch (cls) {
    case 'SMG':
      b(0.065, 0.1, 0.32, 0xbbbbbb, 0, 0, -0.12); cyl(0.016, 0.14, 0x777777, 0, 0.015, -0.34);
      b(0.04, 0.17, 0.04, 0x8a8a8a, 0, -0.12, -0.12); b(0.045, 0.1, 0.045, 0x999999, 0, -0.08, 0.0, -0.3);
      b(0.02, 0.02, 0.22, 0x666666, 0, 0.01, 0.13); b(0.02, 0.07, 0.02, 0x666666, 0, -0.02, 0.24);
      b(0.025, 0.035, 0.08, 0x333333, 0, 0.07, -0.1); break;
    case 'DMR':
      b(0.07, 0.11, 0.5, 0xbbbbbb, 0, 0, -0.2); cyl(0.02, 0.42, 0x6a6a6a, 0, 0.02, -0.66);
      b(0.055, 0.11, 0.07, 0x999999, 0, -0.1, -0.2); b(0.05, 0.1, 0.05, 0x999999, 0, -0.08, 0, -0.3);
      b(0.06, 0.1, 0.26, 0xaaaaaa, 0, -0.01, 0.17); scope(0.025, 0.24); break;
    case 'SNIPER':
      b(0.07, 0.1, 0.48, WOOD, 0, -0.01, -0.12); cyl(0.017, 0.62, 0x5a5a5a, 0, 0.02, -0.66);
      b(0.06, 0.12, 0.3, WOOD, 0, -0.03, 0.22); b(0.05, 0.08, 0.05, 0x8a8a8a, 0, -0.08, -0.05);
      b(0.025, 0.02, 0.05, 0x888888, 0.05, 0.03, 0.04); scope(0.032, 0.34, 0.1, -0.15);
      for (const s2 of [-1, 1]) P.push(prep(new THREE.BoxGeometry(0.012, 0.2, 0.012), 0x333333, null, M4(s2 * 0.03, -0.1, -0.55, 0, 0, s2 * 0.3))); break;
    case 'SG':
      for (const s2 of [-1, 1]) cyl(0.022, 0.6, 0x6a6a6a, s2 * 0.022, 0.02, -0.38);
      b(0.08, 0.1, 0.22, 0x888888, 0, 0, -0.02); b(0.07, 0.07, 0.24, WOOD, 0, -0.03, -0.22);
      b(0.06, 0.12, 0.3, WOOD, 0, -0.04, 0.2, 0.15); break;
    case 'LMG':
      b(0.09, 0.13, 0.5, 0xaaaaaa, 0, 0, -0.18); cyl(0.024, 0.48, 0x5a5a5a, 0, 0.02, -0.64);
      b(0.13, 0.15, 0.13, 0x6a6a5a, -0.02, -0.13, -0.2); b(0.06, 0.1, 0.05, 0x999999, 0, -0.09, 0, -0.3);
      b(0.06, 0.12, 0.26, 0x999999, 0, -0.01, 0.17); b(0.05, 0.03, 0.1, 0x777777, 0, 0.09, -0.4);
      for (const s2 of [-1, 1]) P.push(prep(new THREE.BoxGeometry(0.014, 0.22, 0.014), 0x333333, null, M4(s2 * 0.035, -0.11, -0.72, 0, 0, s2 * 0.3))); break;
    case 'PISTOL':
      b(0.04, 0.06, 0.2, 0xbbbbbb, 0, 0.02, -0.12); b(0.035, 0.12, 0.05, 0x888888, 0, -0.06, -0.04, -0.2); break;
    default: // assault rifle
      b(0.07, 0.11, 0.42, 0xbbbbbb, 0, 0, -0.18); cyl(0.018, 0.36, 0x777777, 0, 0.02, -0.56);
      b(0.08, 0.09, 0.2, 0x9a9a9a, 0, 0.0, -0.42);                                    // handguard
      P.push(prep(new THREE.BoxGeometry(0.055, 0.17, 0.07), 0x999999, null, M4(0, -0.12, -0.2, 0.25)));   // curved mag
      b(0.05, 0.1, 0.05, 0x999999, 0, -0.08, -0.02, -0.3); b(0.06, 0.09, 0.22, 0xaaaaaa, 0, -0.01, 0.13);
      b(0.03, 0.02, 0.32, 0x555555, 0, 0.065, -0.24); b(0.03, 0.045, 0.06, 0x333333, 0, 0.09, -0.16);   // rail + optic
  }
  return mergeGeometries(P, false);
}
