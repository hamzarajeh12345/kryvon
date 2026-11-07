// Real 3D characters (rigged GLB from assets/game/<id>.glb) with skeletal animation.
// Falls back to the procedural body in character.js when a model is missing.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import * as SkeletonUtils from 'three/addons/utils/SkeletonUtils.js';
import { MeshoptDecoder } from 'three/addons/libs/meshopt_decoder.module.js';

export const HERO_IDS = ['hamza', 'sara', 'recon', 'medic', 'engineer', 'heavy'];
const HEIGHT = 1.8;
const CLIP = {
  idle: 'HumanSpIdle', walk: 'HumanSpWalking', run: 'HumanSpRun', sprint: 'HumanSpTwoCycleSprint',
  crouch: 'HumanSpCrouchedWalking', crawl: 'HumanSpLowCrawl', jump: 'HumanSpJumping',
  sit: 'HumanSpSitting', die: 'HumanSpDying', salute: 'HumanSpSalute',
};
const CACHE = {};
let pending = null;

// keep animations in place: drop the hips' horizontal travel (root motion)
function inPlace(clip) {
  for (const t of clip.tracks) {
    if (!/hips\.position$/i.test(t.name)) continue;
    const v = t.values, x0 = v[0], z0 = v[2];
    for (let i = 0; i < v.length; i += 3) { v[i] = x0; v[i + 2] = z0; }
  }
  return clip;
}

export function loadHeroes(ids = HERO_IDS) {
  if (pending) return pending;
  const loader = new GLTFLoader().setMeshoptDecoder(MeshoptDecoder);
  pending = Promise.all(ids.map(id => loader.loadAsync(`/assets/game/${id}.glb`).then(g => {
    g.scene.updateMatrixWorld(true);
    g.scene.traverse(o => { if (o.isSkinnedMesh) o.skeleton.update(); });
    const box = new THREE.Box3().setFromObject(g.scene, true); // precise: applies skinning
    const h = box.max.y - box.min.y || 1;
    g.scene.traverse(o => {
      if (o.isMesh) {
        o.castShadow = true; o.receiveShadow = false;
        if (o.material) { o.material.envMapIntensity = 0.8; if (o.material.metalness > 0.6) o.material.metalness = 0.6; }
      }
    });
    CACHE[id] = { scene: g.scene, scale: HEIGHT / h, minY: box.min.y, clips: Object.fromEntries(g.animations.map(c => [c.name, inPlace(c)])) };
  }).catch(() => { /* missing model: procedural fallback */ })));
  return pending;
}

export const heroReady = id => !!CACHE[id];

export class HeroBody {
  constructor(id) {
    const c = CACHE[id];
    this.id = id;
    this.root = new THREE.Group();
    const model = SkeletonUtils.clone(c.scene);
    model.scale.setScalar(c.scale);
    model.position.y = -c.minY * c.scale;
    model.rotation.y = Math.PI; // generated models face +Z, actors face -Z
    this.root.add(model);
    this.mixer = new THREE.AnimationMixer(model);
    this.actions = {};
    for (const k in CLIP) {
      const clip = c.clips[CLIP[k]];
      if (!clip) continue;
      const a = this.mixer.clipAction(clip);
      if (k === 'die') { a.setLoop(THREE.LoopOnce, 1); a.clampWhenFinished = true; }
      if (k === 'jump') { a.setLoop(THREE.LoopOnce, 1); a.clampWhenFinished = true; }
      this.actions[k] = a;
    }
    this.cur = null;
    this.play('idle');
  }

  play(name, fade = 0.2) {
    if (!this.actions[name]) name = 'idle';
    if (name === this.cur || !this.actions[name]) return;
    const next = this.actions[name].reset().setEffectiveWeight(1).fadeIn(fade).play();
    if (this.cur && this.actions[this.cur]) this.actions[this.cur].fadeOut(fade);
    this.cur = name;
    return next;
  }

  update(dt, speed = 1) { this.mixer.timeScale = speed; this.mixer.update(dt); }

  dispose() { this.mixer.stopAllAction(); this.root.removeFromParent(); }
}
