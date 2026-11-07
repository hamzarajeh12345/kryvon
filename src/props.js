// Generated 3D props (vehicles, weapons, houses, jet) from assets/game/<key>.glb.
// Every model is authored nose/front toward +Z; the game's forward is -Z, so clones are turned 180°.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';

export const PROP_KEYS = ['v_pickup', 'v_sedan', 'v_jeep', 'v_armored', 'v_moto', 'v_trike', 'jet',
  'w_ar', 'w_smg', 'w_shotgun', 'w_pistol', 'w_sniper', 'w_lmg', 'w_rocket', 'h_apartment', 'h_villa', 'h_modern'];
const CACHE = {};
let pending = null;

export function loadProps(keys = PROP_KEYS) {
  if (pending) return pending;
  const loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
  pending = Promise.all(keys.map(k => loader.loadAsync(`/assets/game/${k}.glb`).then(g => {
    g.scene.updateMatrixWorld(true);
    g.scene.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
    CACHE[k] = { scene: g.scene, box: new THREE.Box3().setFromObject(g.scene) };
  }).catch(() => {})));
  return pending;
}

export const propReady = k => !!CACHE[k];

// scale factor so the model's length (Z) or height (Y) matches the game size
function fit(c, { len, height }) {
  const s = c.box.getSize(new THREE.Vector3());
  return len ? len / s.z : height ? height / s.y : 1;
}

/** a Group holding a clone, base on y=0, centred in x/z, facing -Z */
export function propClone(k, size) {
  const c = CACHE[k]; if (!c) return null;
  const sc = fit(c, size), ctr = c.box.getCenter(new THREE.Vector3());
  const inner = c.scene.clone(true);
  inner.scale.setScalar(sc);
  inner.position.set(ctr.x * sc, -c.box.min.y * sc, ctr.z * sc); // after the 180° turn x/z flip sign
  const g = new THREE.Group(); g.add(inner); inner.rotation.y = Math.PI;
  return g;
}

/** one merged geometry + material (for InstancedMesh), normalised the same way. rot: extra Euler applied after. */
export function propGeometry(k, size, rot) {
  const c = CACHE[k]; if (!c) return null;
  const sc = fit(c, size), ctr = c.box.getCenter(new THREE.Vector3());
  const parts = []; let mat = null;
  c.scene.traverse(o => {
    if (!o.isMesh) return;
    const g = o.geometry.clone().applyMatrix4(o.matrixWorld);
    for (const n of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(n)) g.deleteAttribute(n);
    parts.push(g); mat = mat || o.material;
  });
  const geo = parts.length > 1 ? mergeGeometries(parts.map(p => p.index ? p.toNonIndexed() : p), false) : parts[0];
  geo.translate(-ctr.x, -c.box.min.y, -ctr.z); geo.scale(sc, sc, sc); geo.rotateY(Math.PI);
  if (rot) geo.applyMatrix4(new THREE.Matrix4().makeRotationFromEuler(rot));
  return { geometry: geo, material: mat };
}
