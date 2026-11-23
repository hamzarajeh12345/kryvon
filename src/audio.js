// All sounds are synthesized at runtime (original, no samples).
export class Audio {
  constructor() { this.ctx = null; this.muffle = 1; this.vol = 0.6; this.vSfx = 1; this.vUi = 0.8; }
  setVolumes(a) {
    this.vol = a.master / 100; this.vSfx = a.sfx / 100; this.vUi = a.ui / 100;
    if (this.ctx) { this.master.gain.value = this.vol; this.sfxBus.gain.value = this.vSfx; this.uiBus.gain.value = this.vUi; }
  }
  init() {
    if (this.ctx) return;
    try {
      this.ctx = new (window.AudioContext || window.webkitAudioContext)();
      this.master = this.ctx.createGain(); this.master.gain.value = this.vol; this.master.connect(this.ctx.destination);
      this.sfxBus = this.ctx.createGain(); this.sfxBus.gain.value = this.vSfx; this.sfxBus.connect(this.master);
      this.uiBus = this.ctx.createGain(); this.uiBus.gain.value = this.vUi; this.uiBus.connect(this.master);
      this.bus = this.sfxBus;
      const len = this.ctx.sampleRate;
      this.noise = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
      const d = this.noise.getChannelData(0); for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    } catch { this.ctx = null; }
  }
  att(dist) { return this.muffle / (1 + dist / 25); }
  out(pan) {
    const g = this.ctx.createGain(), bus = this.bus || this.master;
    if (this.ctx.createStereoPanner && pan) { const p = this.ctx.createStereoPanner(); p.pan.value = Math.max(-1, Math.min(1, pan)); g.connect(p); p.connect(bus); }
    else g.connect(bus);
    return g;
  }
  burst(vol, dur, freq, q, pan = 0, type = 'lowpass') {
    if (!this.ctx || vol < 0.005) return;
    const t = this.ctx.currentTime, src = this.ctx.createBufferSource();
    src.buffer = this.noise; src.playbackRate.value = 0.7 + Math.random() * 0.3;
    const f = this.ctx.createBiquadFilter(); f.type = type; f.frequency.value = freq; f.Q.value = q;
    const g = this.out(pan);
    g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    src.connect(f); f.connect(g); src.start(t, Math.random() * 0.5); src.stop(t + dur + 0.05);
  }
  tone(freq, dur, vol, type = 'sine', slide = 0, pan = 0, delay = 0) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime + delay, o = this.ctx.createOscillator(), g = this.out(pan);
    o.type = type; o.frequency.setValueAtTime(freq, t);
    if (slide) o.frequency.exponentialRampToValueAtTime(Math.max(20, freq * slide), t + dur);
    g.gain.setValueAtTime(vol, t); g.gain.exponentialRampToValueAtTime(0.001, t + dur);
    o.connect(g); o.start(t); o.stop(t + dur + 0.05);
  }
  shot(cls, dist, pan) {
    const a = this.att(dist);
    if (dist > 600) return;
    const far = dist > 120;
    const p = { AR: [0.9, 0.18, 2200], SMG: [0.7, 0.12, 2800], SNIPER: [1.2, 0.5, 1500], DMR: [1, 0.3, 1800], SG: [1.2, 0.3, 1200], PISTOL: [0.6, 0.12, 2600], LMG: [1, 0.2, 1900] }[cls] || [0.8, 0.2, 2000];
    this.burst(p[0] * a, p[1] * (far ? 2 : 1), far ? 700 : p[2], 0.7, pan);
    if (!far) this.tone(120, 0.08, 0.4 * a, 'triangle', 0.5, pan);
  }
  explosion(dist, pan) { const a = this.att(dist) * 1.6; this.burst(a, 1.2, 400, 0.5, pan); this.tone(60, 0.6, a * 0.8, 'sine', 0.4, pan); }
  onUi(f) { if (!this.ctx) return; this.bus = this.uiBus; try { f(); } finally { this.bus = this.sfxBus; } }
  hit(head) { this.onUi(() => { this.tone(head ? 1800 : 1100, 0.06, 0.25, 'square'); if (head) this.tone(2600, 0.08, 0.15, 'sine', 1, 0, 0.03); }); }
  kill() { this.onUi(() => { this.tone(880, 0.1, 0.25, 'triangle'); this.tone(1320, 0.18, 0.25, 'triangle', 1, 0, 0.08); }); }
  hurt() { this.burst(0.35, 0.15, 600, 1); }
  slipper(dist, pan) { const a = this.att(dist); this.burst(a * 1.2, 0.07, 3000, 3, pan, 'bandpass'); }
  footstep(surface, dist = 0, pan = 0) {
    if (!this.ctx) return;
    const a = this.att(dist);
    const f = { gravel: 1800, rock: 1400, road: 900, grass: 700, mud: 420, sand: 520, water: 260 }[surface] || 900;
    const dur = surface === 'water' ? 0.18 : 0.07;
    this.burst(0.16 * a, dur, f, surface === 'sand' || surface === 'mud' ? 1.2 : 3, pan, 'bandpass');
  }
  armorBreak(part) {
    this.onUi(() => {
      this.burst(0.32, 0.12, part === 'head' ? 2400 : 1700, 2, 0, 'bandpass');
      this.tone(part === 'head' ? 620 : 420, 0.12, 0.16, 'square', 0.7);
    });
  }
  ui(kind) { this.onUi(() => this._ui(kind)); }
  _ui(kind) {
    if (kind === 'pickup') this.tone(660, 0.07, 0.15, 'sine', 1.5);
    else if (kind === 'reload') { this.burst(0.25, 0.05, 3500, 4, 0, 'bandpass'); setTimeout(() => this.burst(0.3, 0.06, 2500, 4, 0, 'bandpass'), 120); }
    else if (kind === 'heal') this.tone(520, 0.25, 0.15, 'sine', 1.5);
    else if (kind === 'click') this.tone(300, 0.03, 0.1, 'square');
    else if (kind === 'jump') this.burst(0.4, 1.2, 900, 0.5);
  }
  startEngine() {
    if (!this.ctx || this.engine) return;
    const o = this.ctx.createOscillator(), o2 = this.ctx.createOscillator(), f = this.ctx.createBiquadFilter(), g = this.ctx.createGain();
    o.type = 'sawtooth'; o2.type = 'square'; o.frequency.value = 45; o2.frequency.value = 22.5;
    f.type = 'lowpass'; f.frequency.value = 420; g.gain.value = 0.0001;
    o.connect(f); o2.connect(f); f.connect(g); g.connect(this.sfxBus || this.master);
    o.start(); o2.start();
    g.gain.setTargetAtTime(0.06, this.ctx.currentTime, 0.2);
    this.engine = { o, o2, f, g };
  }
  setEngine(r) {
    if (!this.engine) return;
    const t = this.ctx.currentTime, e = this.engine;
    e.o.frequency.setTargetAtTime(45 + r * 110, t, 0.08); e.o2.frequency.setTargetAtTime(22 + r * 55, t, 0.08);
    e.f.frequency.setTargetAtTime(380 + r * 900, t, 0.1); e.g.gain.setTargetAtTime(0.05 + r * 0.06, t, 0.1);
  }
  stopEngine() {
    if (!this.engine) return;
    const e = this.engine; this.engine = null;
    e.g.gain.setTargetAtTime(0.0001, this.ctx.currentTime, 0.15);
    setTimeout(() => { try { e.o.stop(); e.o2.stop(); } catch { } }, 600);
  }
  siren(dist) { // air-raid siren
    if (!this.ctx) return;
    const t = this.ctx.currentTime, o = this.ctx.createOscillator(), g = this.out(0);
    o.type = 'sawtooth';
    for (let i = 0; i < 3; i++) { o.frequency.setValueAtTime(320, t + i * 3); o.frequency.linearRampToValueAtTime(760, t + i * 3 + 1.5); o.frequency.linearRampToValueAtTime(320, t + i * 3 + 3); }
    const v = Math.max(0.05, 0.25 / (1 + dist / 400));
    g.gain.setValueAtTime(0.001, t); g.gain.linearRampToValueAtTime(v, t + 0.5); g.gain.setValueAtTime(v, t + 8.5); g.gain.linearRampToValueAtTime(0.001, t + 9.5);
    const f = this.ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 1400;
    o.connect(f); f.connect(g); o.start(t); o.stop(t + 9.6);
  }
  plane(dur) {
    if (!this.ctx) return;
    const t = this.ctx.currentTime, o = this.ctx.createOscillator(), g = this.out(0);
    o.type = 'sawtooth'; o.frequency.value = 55;
    const f = this.ctx.createBiquadFilter(); f.type = 'lowpass'; f.frequency.value = 300;
    g.gain.setValueAtTime(0.001, t); g.gain.linearRampToValueAtTime(0.12, t + 1); g.gain.setValueAtTime(0.12, t + dur - 1); g.gain.linearRampToValueAtTime(0.001, t + dur);
    o.connect(f); f.connect(g); o.start(t); o.stop(t + dur);
    this.planeNode = { o, g };
  }
  stopPlane() { if (this.planeNode) { try { this.planeNode.g.gain.cancelScheduledValues(0); this.planeNode.g.gain.setTargetAtTime(0.001, this.ctx.currentTime, 0.3); } catch { } this.planeNode = null; } }
  // short oud-like pluck melody (maqam hijaz flavour) for victory
  victory() {
    const notes = [293.7, 311.1, 370, 392, 440, 466.2, 523.3, 587.3];
    [0, 2, 3, 4, 3, 2, 1, 0, 4, 7].forEach((n, i) => { this.tone(notes[n], 0.5, 0.22, 'triangle', 0.98, 0, i * 0.18); this.tone(notes[n] * 2, 0.3, 0.06, 'sine', 1, 0, i * 0.18); });
    [0, 0.36, 0.54, 0.9, 1.26, 1.44].forEach(d => setTimeout(() => this.burst(0.4, 0.15, 180, 1), d * 1000)); // darbuka "dum"
  }
}
