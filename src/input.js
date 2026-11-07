// Keyboard + mouse (pointer lock), on-screen touch controls and gyroscope.
// Key remapping: the game always asks for the *default* code of an action (e.g. 'KeyE');
// a pressed physical key is translated through the player's bindings first.
import { ACTIONS } from './settings.js';

const DEFAULT_CODES = new Set(ACTIONS.map(a => a[0]));

export class Input {
  constructor(canvas, settings) {
    this.canvas = canvas;
    this.keys = new Set(); this.pressed = new Set();
    this.mdx = 0; this.mdy = 0; this.fire = false; this.aimToggle = false;
    this.touch = matchMedia('(pointer: coarse)').matches || 'ontouchstart' in window;
    this.joy = { x: 0, y: 0 };
    this.gx = 0; this.gy = 0;         // gyro look delta (pixels-equivalent)
    this.enabled = true;
    this.apply(settings);
    addEventListener('keydown', e => {
      if (!this.enabled || e.repeat || e.target.closest && e.target.closest('input,select,textarea')) return;
      const c = this.translate(e.code); if (!c) return;
      this.keys.add(c); this.pressed.add(c);
      if (c === 'ShiftLeft' && this.s.controls.sprint === 'toggle') this.sprintToggle = !this.sprintToggle;
      if (['Space', 'Tab'].includes(e.code)) e.preventDefault();
    });
    addEventListener('keyup', e => { const c = this.translate(e.code); if (c) this.keys.delete(c); });
    addEventListener('blur', () => { this.keys.clear(); this.fire = false; });
    addEventListener('mousemove', e => { if (this.enabled && document.pointerLockElement === canvas) { this.mdx += e.movementX; this.mdy += e.movementY; } });
    canvas.addEventListener('mousedown', e => {
      if (this.touch || !this.enabled) return;
      if (document.pointerLockElement !== canvas) { this.lock(); return; }
      if (e.button === 0) this.fire = true;
      if (e.button === 2) this.aimToggle = this.s.controls.aim === 'hold' ? true : !this.aimToggle;
      if (e.button === 1) { e.preventDefault(); this.press('KeyV'); }
    });
    addEventListener('mouseup', e => {
      if (e.button === 0) this.fire = false;
      if (e.button === 2 && this.s.controls.aim === 'hold') this.aimToggle = false;
    });
    canvas.addEventListener('contextmenu', e => e.preventDefault());
    if (this.touch) this.setupTouch();
  }

  apply(settings) {
    this.s = settings;
    this.rev = {};
    const b = settings.controls.binds || {};
    for (const [code] of ACTIONS) if (b[code]) this.rev[b[code]] = code;
    if (settings.sens.gyro !== 'off') this.enableGyro(); else this.gyroOn = false;
    if (settings.controls.sprint !== 'toggle') this.sprintToggle = false;
  }
  translate(code) {
    if (this.rev[code]) return this.rev[code];
    const b = this.s.controls.binds || {};
    if (DEFAULT_CODES.has(code) && b[code] && b[code] !== code) return null; // default key moved elsewhere
    return code;
  }

  enableGyro() {
    this.gyroOn = true;
    if (this.gyroBound) return;
    const start = () => {
      if (this.gyroBound) return;
      this.gyroBound = true;
      addEventListener('devicemotion', e => {
        const r = e.rotationRate; if (!r || !this.gyroOn || !this.enabled) return;
        const dt = (e.interval || 16) > 1 ? (e.interval || 16) / 1000 : e.interval; // ms on most devices, s on some
        const land = Math.abs(window.orientation || (screen.orientation && screen.orientation.angle) || 0) === 90;
        const sign = (screen.orientation && screen.orientation.angle === 270) || window.orientation === -90 ? -1 : 1;
        // degrees/s → pixels: ~10 px per degree at 100 %
        const yawRate = land ? (r.alpha || 0) * sign : (r.beta || 0), pitchRate = land ? (r.beta || 0) * -sign : (r.alpha || 0);
        this.gx -= yawRate * dt * 10; this.gy -= pitchRate * dt * 10;
      });
    };
    if (typeof DeviceMotionEvent !== 'undefined' && DeviceMotionEvent.requestPermission) {
      // iOS asks on the next tap
      const ask = () => { DeviceMotionEvent.requestPermission().then(p => { if (p === 'granted') start(); }).catch(() => { }); removeEventListener('touchend', ask); };
      addEventListener('touchend', ask);
    } else start();
  }

  lock() { if (!this.touch && this.enabled && this.canvas.requestPointerLock) { try { const p = this.canvas.requestPointerLock(); if (p && p.catch) p.catch(() => { }); } catch { } } }
  get locked() { return this.touch || document.pointerLockElement === this.canvas; }
  down(c) { return this.enabled && this.keys.has(c); }
  hit(c) { return this.enabled && this.pressed.has(c); }
  endFrame() { this.pressed.clear(); this.mdx = 0; this.mdy = 0; this.gx = 0; this.gy = 0; }
  press(code) { this.pressed.add(code); }

  setupTouch() {
    const ui = document.getElementById('touch'); ui.style.display = 'block';
    const stick = document.getElementById('stick'), knob = document.getElementById('knob');
    let stickId = null, lookId = null, lx = 0, ly = 0, sx = 0, sy = 0;
    const btnTouch = new Map();
    ui.querySelectorAll('[data-k]').forEach(b => {
      b.addEventListener('touchstart', e => {
        e.preventDefault(); e.stopPropagation();
        const k = b.dataset.k, t = e.changedTouches[0];
        if (k === 'fire') { this.fire = true; btnTouch.set(t.identifier, 'fire'); lx = t.clientX; ly = t.clientY; }
        else if (k === 'aim') this.aimToggle = this.s.controls.aim === 'hold' ? true : !this.aimToggle;
        else if (k === 'sprint') this.sprintToggle = !this.sprintToggle;
        else this.press(k);
        b.classList.add('on');
      }, { passive: false });
      b.addEventListener('touchend', e => {
        e.preventDefault();
        if (b.dataset.k === 'fire') this.fire = false;
        if (b.dataset.k === 'aim' && this.s.controls.aim === 'hold') this.aimToggle = false;
        b.classList.remove('on');
      }, { passive: false });
    });
    const area = document.getElementById('game');
    area.addEventListener('touchstart', e => {
      for (const t of e.changedTouches) {
        if (t.clientX < innerWidth * 0.42 && stickId === null) { stickId = t.identifier; sx = t.clientX; sy = t.clientY; stick.style.left = sx - 60 + 'px'; stick.style.top = sy - 60 + 'px'; stick.style.opacity = 1; }
        else if (lookId === null) { lookId = t.identifier; lx = t.clientX; ly = t.clientY; }
      }
    }, { passive: true });
    addEventListener('touchmove', e => {
      for (const t of e.changedTouches) {
        if (t.identifier === stickId) {
          let dx = t.clientX - sx, dy = t.clientY - sy; const l = Math.hypot(dx, dy), m = 50;
          if (l > m) { dx *= m / l; dy *= m / l; }
          this.joy.x = dx / m; this.joy.y = dy / m;
          knob.style.transform = `translate(${dx}px,${dy}px)`;
        } else if (t.identifier === lookId || btnTouch.get(t.identifier) === 'fire') {
          const k = 2.2 * this.s.sens.touch / 100;
          this.mdx += (t.clientX - lx) * k; this.mdy += (t.clientY - ly) * k; lx = t.clientX; ly = t.clientY;
        }
      }
    }, { passive: true });
    const end = e => {
      for (const t of e.changedTouches) {
        if (t.identifier === stickId) { stickId = null; this.joy.x = this.joy.y = 0; knob.style.transform = ''; stick.style.opacity = 0.5; }
        if (t.identifier === lookId) lookId = null;
        if (btnTouch.get(t.identifier) === 'fire') { this.fire = false; btnTouch.delete(t.identifier); }
      }
    };
    addEventListener('touchend', end); addEventListener('touchcancel', end);
  }

  // movement axes: x = right, y = forward
  axes() {
    let x = 0, y = 0;
    if (this.down('KeyW') || this.down('ArrowUp')) y += 1;
    if (this.down('KeyS') || this.down('ArrowDown')) y -= 1;
    if (this.down('KeyD') || this.down('ArrowRight')) x += 1;
    if (this.down('KeyA') || this.down('ArrowLeft')) x -= 1;
    if (this.touch && this.enabled) { x += this.joy.x; y -= this.joy.y; }
    if (this.sprintToggle && y <= 0 && !this.touch) this.sprintToggle = false;
    return { x, y };
  }
  sprint() {
    if (!this.enabled) return false;
    if (this.sprintToggle) return true;
    if (this.s.controls.sprint === 'toggle' && !this.touch) return false;
    return this.down('ShiftLeft') || this.down('ShiftRight') || (this.touch && Math.hypot(this.joy.x, this.joy.y) > 0.95);
  }
}
