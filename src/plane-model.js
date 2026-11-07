// KRYVON transport plane – original design: high-wing, four turboprops, sand livery with a terracotta band.
// Forward is -Z (the Plane class rotates the group to face its flight direction). Length ≈ 30 m, span ≈ 40 m.
import * as THREE from 'three';

const M = (color, shin = 30) => new THREE.MeshPhongMaterial({ color, shininess: shin, specular: 0x333333 });
const MAT = {
  body: M(0xcdbb92), belly: M(0xa99a78), band: M(0xb8492e), dark: M(0x1d242b, 90), glass: M(0x1b2b38, 120),
  engine: M(0x8f8a7c), spinner: M(0x2a2a2a, 60), prop: M(0x1a1a1a), gold: M(0xd9a520, 80),
};

function extrude(points, depth, bevel = 0.12) {
  const s = new THREE.Shape(points.map(([x, y]) => new THREE.Vector2(x, y)));
  const g = new THREE.ExtrudeGeometry(s, { depth, bevelEnabled: true, bevelThickness: bevel, bevelSize: bevel, bevelSegments: 2, curveSegments: 4 });
  g.translate(0, 0, -depth / 2);
  return g;
}

export function buildPlaneMesh() {
  const g = new THREE.Group(), props = [];
  const add = (geo, mat, x = 0, y = 0, z = 0) => { const m = new THREE.Mesh(geo, mat); m.position.set(x, y, z); g.add(m); return m; };

  // fuselage: lathe profile (radius, length) – rounded nose, upswept tail cone
  const prof = [[0, -15], [0.9, -14.7], [1.7, -13.9], [2.25, -12.4], [2.5, -10], [2.55, -4], [2.55, 5], [2.4, 8], [2.0, 10.5], [1.4, 12.8], [0.8, 14.6], [0.35, 15.4], [0, 15.5]];
  const fus = new THREE.LatheGeometry(prof.map(([r, z]) => new THREE.Vector2(r, z)), 28);
  fus.rotateX(Math.PI / 2);
  // lift the tail section so the rear sweeps up like a cargo plane
  const p = fus.attributes.position;
  for (let i = 0; i < p.count; i++) { const z = p.getZ(i); if (z > 6) p.setY(i, p.getY(i) + (z - 6) * (z - 6) * 0.018); }
  fus.computeVertexNormals();
  const body = add(fus, MAT.body); body.scale.y = 0.94;
  // belly fairing + landing gear pods
  add(new THREE.BoxGeometry(3.6, 0.9, 16), MAT.belly, 0, -2.05, 0.5);
  for (const s of [-1, 1]) add(new THREE.CapsuleGeometry(0.75, 6, 4, 10), MAT.belly, s * 2.55, -1.5, 0.5).rotation.x = Math.PI / 2;
  // livery band and cockpit glass
  const band = add(new THREE.CylinderGeometry(2.6, 2.6, 1.3, 28, 1, true), MAT.band, 0, 0, -1.5); band.rotation.x = Math.PI / 2; band.scale.z = 0.94;
  const band2 = add(new THREE.CylinderGeometry(2.6, 2.6, 0.35, 28, 1, true), MAT.gold, 0, 0, -0.5); band2.rotation.x = Math.PI / 2; band2.scale.z = 0.94;
  for (const s of [-1, 0, 1]) {
    const w = add(new THREE.BoxGeometry(s ? 0.9 : 1.1, 0.55, 0.9), MAT.glass, s * 0.95, 1.15, -13.1 + Math.abs(s) * 0.25);
    w.rotation.x = -0.55; w.rotation.y = s * 0.45;
  }
  for (let i = 0; i < 9; i++) for (const s of [-1, 1]) add(new THREE.BoxGeometry(0.06, 0.38, 0.38), MAT.dark, s * 2.53, 0.55, -8 + i * 1.6);
  // door outline
  for (const s of [-1, 1]) add(new THREE.BoxGeometry(0.05, 1.9, 1.1), MAT.belly, s * 2.55, -0.2, -10.2);

  // high wing (tapered, slightly swept) sitting on top of the fuselage
  const wing = extrude([[-20, -1.2], [-20, 1.0], [-6, 2.6], [6, 2.6], [20, 1.0], [20, -1.2], [6, -2.4], [-6, -2.4]], 0.32, 0.14);
  wing.rotateX(-Math.PI / 2);
  add(wing, MAT.body, 0, 2.25, -1.8);
  add(new THREE.BoxGeometry(12, 0.7, 5.2), MAT.body, 0, 2.05, -1.8);                   // wing root fairing
  for (const s of [-1, 1]) add(new THREE.BoxGeometry(3.2, 0.1, 0.7), MAT.band, s * 18.4, 2.45, -2.4); // tip stripes

  // four turboprops
  for (const x of [-12, -6.3, 6.3, 12]) {
    const n = add(new THREE.CylinderGeometry(0.62, 0.78, 5.2, 14), MAT.engine, x, 1.75, -3.6); n.rotation.x = Math.PI / 2;
    add(new THREE.CylinderGeometry(0.66, 0.66, 0.5, 14), MAT.dark, x, 1.75, -6.0).rotation.x = Math.PI / 2;
    const sp = add(new THREE.ConeGeometry(0.42, 1.0, 14), MAT.spinner, x, 1.75, -6.75); sp.rotation.x = -Math.PI / 2;
    const prop = new THREE.Group(); prop.position.set(x, 1.75, -6.45);
    for (let k = 0; k < 4; k++) {
      const b = new THREE.Mesh(new THREE.BoxGeometry(0.22, 2.1, 0.06), MAT.prop);
      b.position.y = 1.05; const arm = new THREE.Group(); arm.rotation.z = k * Math.PI / 2; b.rotation.y = 0.35; arm.add(b); prop.add(arm);
    }
    const disc = new THREE.Mesh(new THREE.CircleGeometry(2.15, 24), new THREE.MeshBasicMaterial({ color: 0x222222, transparent: true, opacity: 0.12, side: THREE.DoubleSide, depthWrite: false }));
    prop.add(disc);
    g.add(prop); props.push(prop);
  }

  // tail: tall fin + stabilisers on the upswept cone
  const fin = extrude([[0, 0], [5.2, 0], [4.4, 7.2], [2.6, 7.4]], 0.3, 0.1);
  fin.rotateY(-Math.PI / 2);
  add(fin, MAT.body, 0, 2.2, 10.0);
  const finTop = add(new THREE.BoxGeometry(0.5, 1.2, 2.0), MAT.band, 0, 8.9, 13.9);
  finTop.rotation.x = 0.0;
  const stab = extrude([[-7, 0.6], [-7, -0.8], [-1, -1.6], [1, -1.6], [7, -0.8], [7, 0.6], [1, 1.6], [-1, 1.6]], 0.22, 0.08);
  stab.rotateX(-Math.PI / 2);
  add(stab, MAT.body, 0, 3.0, 13.0);

  g.traverse(o => { if (o.isMesh) { o.castShadow = true; } });
  g.scale.setScalar(0.95);
  return { group: g, props };
}
