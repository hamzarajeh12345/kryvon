import * as THREE from 'three';

function glowTexture(inner = 'rgba(255,255,255,1)', outer = 'rgba(255,255,255,0)') {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d'), gr = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  gr.addColorStop(0, inner); gr.addColorStop(1, outer);
  g.fillStyle = gr; g.fillRect(0, 0, 64, 64);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

export class Effects {
  constructor(scene) {
    this.scene = scene;
    this.glow = glowTexture();
    this.list = [];
    // tracer pool
    this.tracers = [];
    for (let i = 0; i < 80; i++) {
      const geo = new THREE.BufferGeometry().setAttribute('position', new THREE.BufferAttribute(new Float32Array(6), 3));
      const line = new THREE.Line(geo, new THREE.LineBasicMaterial({ color: 0xffe08a, transparent: true, opacity: 0.9 }));
      line.visible = false; line.frustumCulled = false;
      scene.add(line);
      this.tracers.push({ line, t: 0 });
    }
    this.ti = 0;
  }

  tracer(a, b, color = 0xffe08a) {
    const tr = this.tracers[this.ti++ % this.tracers.length];
    const p = tr.line.geometry.attributes.position;
    p.setXYZ(0, a.x, a.y, a.z); p.setXYZ(1, b.x, b.y, b.z); p.needsUpdate = true;
    tr.line.material.color.setHex(color);
    tr.line.visible = true; tr.t = 0.06;
  }

  sprite(pos, color, size, life, opts = {}) {
    const m = new THREE.SpriteMaterial({ map: this.glow, color, transparent: true, depthWrite: false, blending: opts.additive === false ? THREE.NormalBlending : THREE.AdditiveBlending, opacity: opts.opacity ?? 1 });
    const s = new THREE.Sprite(m);
    s.position.copy(pos); s.scale.setScalar(size);
    this.scene.add(s);
    this.list.push({ s, life, max: life, grow: opts.grow ?? 1, vy: opts.vy ?? 0, vx: opts.vx ?? 0, vz: opts.vz ?? 0, size, op: opts.opacity ?? 1 });
    return s;
  }

  muzzle(pos) { this.sprite(pos, 0xffc860, 0.9, 0.05); }
  impact(pos, blood) { this.sprite(pos, blood ? 0xb01010 : 0xd8c8a0, blood ? 0.7 : 0.5, 0.25, { additive: false, grow: 2, opacity: 0.8 }); }
  explosion(pos, big = 1) {
    this.sprite(pos.clone().setY(pos.y + 1), 0xffa040, 6 * big, 0.35, { grow: 2.5 });
    this.sprite(pos.clone().setY(pos.y + 1.5), 0xffffff, 3 * big, 0.12, { grow: 2 });
    for (let i = 0; i < 4; i++)
      this.sprite(pos.clone().add(new THREE.Vector3((Math.random() - 0.5) * 3, 1 + Math.random() * 2, (Math.random() - 0.5) * 3)), 0x3a3530, 4 * big, 2 + Math.random(), { additive: false, grow: 1.8, vy: 1.5, opacity: 0.7 });
  }
  smoke(pos, color = 0xff2020) {
    this.sprite(pos.clone().add(new THREE.Vector3((Math.random() - 0.5), 0.5, (Math.random() - 0.5))), color, 2.2, 5, { additive: false, grow: 3.5, vy: 3.2, vx: (Math.random() - 0.5) * 0.8, vz: (Math.random() - 0.5) * 0.8, opacity: 0.55 });
  }
  flare(pos) { this.sprite(pos, 0xff3030, 7, 0.25, { grow: 1.2 }); this.sprite(pos, 0xffffff, 2, 0.15); }

  update(dt) {
    for (const tr of this.tracers) if (tr.line.visible) { tr.t -= dt; if (tr.t <= 0) tr.line.visible = false; }
    for (let i = this.list.length - 1; i >= 0; i--) {
      const e = this.list[i];
      e.life -= dt;
      if (e.life <= 0) { this.scene.remove(e.s); e.s.material.dispose(); this.list.splice(i, 1); continue; }
      const k = 1 - e.life / e.max;
      e.s.scale.setScalar(e.size * (1 + (e.grow - 1) * k));
      e.s.material.opacity = e.op * (1 - k);
      e.s.position.x += e.vx * dt; e.s.position.y += e.vy * dt; e.s.position.z += e.vz * dt;
    }
  }
}

// rain / snow / dust particles that follow the camera
export class WeatherFX {
  constructor(scene, kind) {
    this.kind = kind;
    if (!['rain', 'snow', 'khamaseen'].includes(kind)) return;
    const n = kind === 'rain' ? 3500 : kind === 'snow' ? 2500 : 3000;
    const pos = new Float32Array(n * 3);
    for (let i = 0; i < n * 3; i++) pos[i] = (Math.random() - 0.5) * 80;
    const geo = new THREE.BufferGeometry().setAttribute('position', new THREE.BufferAttribute(pos, 3));
    const col = { rain: 0x9ab0c8, snow: 0xffffff, khamaseen: 0xc89a5a }[kind];
    this.points = new THREE.Points(geo, new THREE.PointsMaterial({ color: col, size: kind === 'rain' ? 0.12 : kind === 'snow' ? 0.25 : 0.18, transparent: true, opacity: 0.75, depthWrite: false }));
    this.points.frustumCulled = false;
    scene.add(this.points);
    this.n = n;
  }
  update(dt, cam) {
    if (!this.points) return;
    const p = this.points.geometry.attributes.position, a = p.array;
    const vy = this.kind === 'rain' ? -38 : this.kind === 'snow' ? -2.5 : -0.5;
    const vx = this.kind === 'khamaseen' ? 14 : this.kind === 'snow' ? 0.8 : 1.5;
    for (let i = 0; i < this.n; i++) {
      a[i * 3] += vx * dt; a[i * 3 + 1] += vy * dt;
      if (a[i * 3 + 1] < -40) a[i * 3 + 1] += 80;
      if (a[i * 3] > 40) a[i * 3] -= 80;
    }
    p.needsUpdate = true;
    this.points.position.copy(cam.position);
  }
}
