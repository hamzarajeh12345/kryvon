// Menus: sign-in / sign-up, lobby, map picker, settings, account linking, leaderboard, pause.
import * as THREE from 'three';
import { HALF, MAPS, ZONES, LANDMARKS, PRESETS } from './config.js';
import { World, naturalHeight, deadSeaD } from './world.js';
import { account, RANKS, RANKS_AR, rankOf } from './account.js';
import { SCHEMA, TABS, ACTIONS, getPath, setPath, loadLocal, saveLocal, fromAccount, defaults, keyLabel } from './settings.js';
import { MODES, SLOTS, COSMETICS, tierOf, itemById } from './progression.js';
import { Actor } from './actor.js';
import { LOOK, PRESET_LOOKS, presetLook, sanitizeLook } from './look.js';

// ---------------- character preview (lobby + wardrobe) ----------------
class Preview {
  constructor(canvas) {
    this.cv = canvas;
    this.r = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
    this.r.setPixelRatio(Math.min(devicePixelRatio, 2));
    this.scene = new THREE.Scene();
    this.cam = new THREE.PerspectiveCamera(30, 1, 0.1, 50);
    this.zoom(false);
    this.scene.add(new THREE.HemisphereLight(0xfff6e6, 0x4a3e30, 2.0));
    const d = new THREE.DirectionalLight(0xffe8c8, 2.2); d.position.set(-1.5, 3, -4); this.scene.add(d);
    const rim = new THREE.DirectionalLight(0xffc070, 1.2); rim.position.set(2, 2, 3); this.scene.add(rim);
    const disc = new THREE.Mesh(new THREE.CircleGeometry(0.9, 40), new THREE.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.35 }));
    disc.rotation.x = -Math.PI / 2; this.scene.add(disc);
    this.actor = new Actor({ scene: this.scene }, 0, '', true);
    this.actor.mesh.visible = true;
    this.t = 0; this.alive = true; this.spin = 0.3; this.drag = null;
    canvas.addEventListener('pointerdown', e => { this.drag = e.clientX; this.auto = false; canvas.setPointerCapture(e.pointerId); });
    canvas.addEventListener('pointermove', e => { if (this.drag != null) { this.spin += (e.clientX - this.drag) * 0.012; this.drag = e.clientX; } });
    canvas.addEventListener('pointerup', () => { this.drag = null; });
    this.auto = true;
    const loop = () => {
      if (!this.alive || !this.cv.isConnected) { this.dispose(); return; }
      requestAnimationFrame(loop);
      const w = this.cv.clientWidth, h = this.cv.clientHeight;
      if (w && h && (this.cv.width !== Math.floor(w * this.r.getPixelRatio()) || this._h !== h)) { this.r.setSize(w, h, false); this.cam.aspect = w / h; this.cam.updateProjectionMatrix(); this._h = h; }
      this.t += 0.012; this.actor.mesh.rotation.y = this.auto ? Math.sin(this.t) * 0.6 : this.spin;
      this.r.render(this.scene, this.cam);
    };
    loop();
  }
  zoom(face) {
    if (face) { this.cam.position.set(0, 1.72, -1.25); this.cam.lookAt(0, 1.7, 0); }
    else { this.cam.position.set(0, 1.25, -4.1); this.cam.lookAt(0, 1.05, 0); }
  }
  setLook(look) { this.actor.setLook(sanitizeLook(look)); this.pose(); }
  pose() { const a = this.actor; a.armR.rotation.set(1.25, 0, 0.12); a.armL.rotation.set(1.45, 0, -0.45); a.gunMesh.visible = true; }
  set(loadout) { this.actor.applyLoadout(loadout); this.pose(); this.actor.gunMesh.visible = true; this.actor.gunMesh.material.color.setHex(this.actor.gunColor); }
  dispose() { this.alive = false; try { this.r.dispose(); } catch { } }
}

const $ = id => document.getElementById(id);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const h = (html) => { const t = document.createElement('template'); t.innerHTML = html.trim(); return t.content.firstElementChild; };

function suggestPreset() {
  const touch = matchMedia('(pointer: coarse)').matches;
  const mem = navigator.deviceMemory || 8, cores = navigator.hardwareConcurrency || 4;
  if (mem < 4 || cores < 4) return 'low';
  if (touch) return 'medium';
  return cores >= 8 ? 'high' : 'medium';
}

// ---------------- map preview ----------------
let baseImg = null;
function mapBase(S = 256) {
  if (baseImg) return baseImg;
  const c = document.createElement('canvas'); c.width = c.height = S;
  const ctx = c.getContext('2d'), img = ctx.createImageData(S, S), col = new THREE.Color();
  const fake = { flat: [], waterLevel: (x, z) => deadSeaD(x, z) < 2.2 ? -10 : 0 };
  for (let j = 0; j < S; j++) for (let i = 0; i < S; i++) {
    const x = -HALF + (i + 0.5) / S * HALF * 2, z = -HALF + (j + 0.5) / S * HALF * 2;
    const hh = naturalHeight(x, z), lvl = fake.waterLevel(x, z), k = (j * S + i) * 4;
    if (hh < lvl) { img.data[k] = 31; img.data[k + 1] = 104; img.data[k + 2] = 135; }
    else {
      const hx = naturalHeight(x + 8, z) - hh, hz = naturalHeight(x, z + 8) - hh;
      World.prototype.biomeColor.call(fake, x, z, hh, 1 / Math.hypot(hx / 8, hz / 8, 1), col);
      const sh = 1 + (-hx - hz) * 0.03;
      img.data[k] = Math.min(255, col.r * 255 * sh); img.data[k + 1] = Math.min(255, col.g * 255 * sh); img.data[k + 2] = Math.min(255, col.b * 255 * sh);
    }
    img.data[k + 3] = 255;
  }
  ctx.putImageData(img, 0, 0);
  return baseImg = c;
}
export function drawMapPreview(cv, map, opts = {}) {
  const ctx = cv.getContext('2d'), S = cv.width, P = v => (v + HALF) / (HALF * 2) * S;
  ctx.drawImage(mapBase(), 0, 0, S, S);
  if (map && !map.training && map.radius < 1000) {
    const R = map.radius / (HALF * 2) * S;
    ctx.save(); ctx.beginPath(); ctx.rect(0, 0, S, S); ctx.moveTo(P(map.cx) + R, P(map.cz)); ctx.arc(P(map.cx), P(map.cz), R, 0, Math.PI * 2, true);
    ctx.fillStyle = 'rgba(8,10,20,.62)'; ctx.fill('evenodd'); ctx.restore();
    ctx.strokeStyle = '#fff'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(P(map.cx), P(map.cz), map.radius / (HALF * 2) * S, 0, 7); ctx.stroke();
  }
  if (map && map.training) {
    ctx.fillStyle = 'rgba(8,10,20,.55)'; ctx.fillRect(0, 0, S, S);
    ctx.fillStyle = '#ffd34d'; ctx.beginPath(); ctx.arc(P(map.cx), P(map.cz), 6, 0, 7); ctx.fill();
  }
  if (opts.labels !== false) {
    ctx.textAlign = 'center'; ctx.font = `600 ${Math.max(9, S / 34)}px system-ui,Tahoma`;
    for (const z of ZONES) {
      ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.65)'; ctx.fillStyle = z.tier === 3 ? '#ffd27a' : '#fff';
      ctx.strokeText(z.ar, P(z.x), P(z.z)); ctx.fillText(z.ar, P(z.x), P(z.z));
    }
  }
  if (opts.landmarks !== false) {
    for (const l of LANDMARKS) {
      const zn = ZONES.find(z => z.id === l.zone); if (!zn) continue;
      const x = P(zn.x + l.dx), y = P(zn.z + l.dz) + S / 30;
      ctx.fillStyle = '#c99bff'; ctx.strokeStyle = '#000'; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(x, y - 3.5); ctx.lineTo(x + 3.5, y); ctx.lineTo(x, y + 3.5); ctx.lineTo(x - 3.5, y); ctx.closePath(); ctx.fill(); ctx.stroke();
    }
  }
}

// ---------------- UI controller ----------------
export class UI {
  constructor(game) {
    this.game = game;
    this.settings = loadLocal();
    if (!this.settings.graphics.preset) this.settings.graphics.preset = suggestPreset();
    this.mapId = (() => { try { return localStorage.getItem('kryvon.map') || 'sham'; } catch { return 'sham'; } })();
    this.modeId = (() => { try { return localStorage.getItem('kryvon.mode') || 'squad'; } catch { return 'squad'; } })();
    if (!MODES.some(m => m.id === this.modeId)) this.modeId = 'squad';
    if (!MAPS.some(m => m.id === this.mapId)) this.mapId = 'sham';
  }
  get map() { return MAPS.find(m => m.id === this.mapId); }

  async boot() {
    this.show('loading', 'جاري الاتصال…');
    const up = await account.serverUp();
    this.serverUp = up;
    const u = up ? await account.restore() : (account.restore(), account.user);
    const qs = new URLSearchParams(location.search);
    if (qs.get('map') && MAPS.some(m => m.id === qs.get('map'))) this.mapId = qs.get('map');
    if (qs.get('mode') && MODES.some(m => m.id === qs.get('mode'))) this.modeId = qs.get('mode');
    if (u) {
      this.onUser();
      if (qs.has('autostart')) { history.replaceState(null, '', location.pathname); return this.play(); }
      this.showLobby();
    } else this.showAuth();
  }

  show(which, msg) {
    for (const id of ['auth', 'lobby', 'loading']) $(id).style.display = id === which ? 'flex' : 'none';
    if (which === 'loading') $('loadmsg').textContent = msg || '';
  }

  onUser() {
    const u = account.user;
    if (u.settings) this.settings = fromAccount(u.settings);
    else account.saveSettings(this.settings);
    if (!this.settings.graphics.preset) this.settings.graphics.preset = suggestPreset();
    saveLocal(this.settings);
  }

  // ---------------- auth ----------------
  showAuth(tab = 'login') {
    this.show('auth');
    const el = $('auth');
    el.innerHTML = `
      <div class="card" dir="rtl">
        <h1 class="logo">KRYVON</h1>
        <p class="tag">أرض وحدة… ناجي واحد</p>
        ${this.serverUp ? `
        <div class="tabs"><button data-t="login" class="${tab === 'login' ? 'on' : ''}">تسجيل الدخول</button><button data-t="register" class="${tab === 'register' ? 'on' : ''}">حساب جديد</button></div>
        <form id="authform" autocomplete="on" novalidate>
          ${tab === 'register' ? `
            <label>اسم اللاعب<input name="username" autocomplete="username" maxlength="16" required placeholder="مثلاً Nashmi_77" dir="ltr"></label>
            <label>الإيميل<input name="email" type="email" autocomplete="email" required dir="ltr"></label>
            <label>كلمة السر<input name="password" type="password" autocomplete="new-password" minlength="6" required dir="ltr"></label>
            <label>تأكيد كلمة السر<input name="password2" type="password" autocomplete="new-password" required dir="ltr"></label>
          ` : `
            <label>الإيميل أو اسم اللاعب<input name="login" autocomplete="username" required dir="ltr"></label>
            <label>كلمة السر<input name="password" type="password" autocomplete="current-password" required dir="ltr"></label>
          `}
          <div class="err" id="autherr"></div>
          <button class="primary" type="submit">${tab === 'register' ? 'إنشاء الحساب' : 'دخول'}</button>
        </form>
        <div class="or"><span>أو</span></div>
        <div id="gbtn"></div>
        <button class="ghost" id="guestbtn">العب كضيف <small>(بتقدر تربط حسابك بعدين)</small></button>
        ` : `
        <div class="note">السيرفر مش شغّال، فالحسابات مش متوفرة هلّق. شغّل <code>npm start</code> للحسابات، أو العب أوفلاين وتقدّمك بنحفظ على هالجهاز بس.</div>
        <label>اسم اللاعب<input id="offname" maxlength="16" value="Nashmi" dir="ltr"></label>
        <button class="primary" id="offbtn">العب أوفلاين</button>
        `}
      </div>`;
    if (!this.serverUp) {
      $('offbtn').onclick = () => { account.useOffline($('offname').value.trim().slice(0, 16) || 'Nashmi'); this.onUser(); this.showLobby(); };
      return;
    }
    el.querySelectorAll('.tabs button').forEach(b => b.onclick = () => this.showAuth(b.dataset.t));
    const err = m => { $('autherr').textContent = m || ''; };
    $('authform').onsubmit = async e => {
      e.preventDefault();
      const f = Object.fromEntries(new FormData(e.target));
      const btn = e.target.querySelector('button[type=submit]'); btn.disabled = true; err('');
      try {
        if (tab === 'register') {
          if (f.password !== f.password2) throw new Error('كلمتين السر مش متطابقات');
          await account.register(f.username.trim(), f.email.trim(), f.password);
        } else await account.login(f.login.trim(), f.password);
        this.onUser(); this.showLobby();
      } catch (x) { err(translateErr(x.message)); }
      btn.disabled = false;
    };
    $('guestbtn').onclick = async () => {
      try { await account.guest(); this.onUser(); this.showLobby(); } catch (x) { err(translateErr(x.message)); }
    };
    this.googleButton($('gbtn'), async cred => { try { await account.google(cred); this.onUser(); this.showLobby(); } catch (x) { err(translateErr(x.message)); } });
  }

  googleButton(host, cb) {
    const id = account.config && account.config.google;
    if (!id) { host.innerHTML = ''; return; }
    const render = () => {
      window.google.accounts.id.initialize({ client_id: id, callback: r => cb(r.credential) });
      window.google.accounts.id.renderButton(host, { theme: 'filled_black', size: 'large', shape: 'pill', width: 300, text: 'continue_with' });
    };
    if (window.google && window.google.accounts) return render();
    const s = document.createElement('script'); s.src = 'https://accounts.google.com/gsi/client'; s.async = true; s.onload = render;
    document.head.appendChild(s);
  }

  // ---------------- lobby ----------------
  showLobby() {
    this.show('lobby');
    const u = account.user, st = u.stats, r = rankOf(st.xp), m = this.map, t = tierOf(u.tp || 0);
    $('lobby').innerHTML = `
      <div class="lob" dir="rtl">
        <header>
          <button class="me" id="acctBtn"><span class="av">${esc(u.username[0] || '?').toUpperCase()}</span>
            <span><b>${esc(u.username)}</b><small>${RANKS_AR[r]} · Lv ${1 + Math.floor(st.xp / 1000)}${u.guest ? ' · <i class="warn">ضيف</i>' : ''}</small></span></button>
          <div class="xp"><div style="width:${(st.xp % 1000) / 10}%"></div><small>${st.xp % 1000}/1000 XP</small></div>
          <div class="wallet"><span class="tierchip" style="--tc:${t.color}">${t.ar} ${t.roman}</span><span class="coin">🪙 ${u.coins ?? 0}</span></div>
          <div class="icons">
            ${account.offline ? '' : '<button id="lbBtn" title="المتصدرين">🏆</button>'}
            <button id="setBtn" title="الإعدادات">⚙</button>
          </div>
        </header>
        <main>
          <div class="stats">
            <div><b>${st.matches}</b>مباريات</div><div><b>${st.wins}</b>فوز 🧀</div><div><b>${st.kills}</b>قتلات</div>
            <div><b>${st.matches ? (st.kills / st.matches).toFixed(1) : '0'}</b>معدل القتل</div><div><b>${st.top10 || 0}</b>أفضل 10</div>
          </div>
          <div class="hero"><canvas id="heroCv"></canvas><div class="acts"><button id="lookBtn">🧑 الشخصية</button><button id="wardBtn">🪂 المتجر</button></div></div>
          ${u.guest && !account.offline ? '<div class="note link">أنت لاعب ضيف – <a href="#" id="linkNow">اربط حسابك</a> عشان ما يضيع تقدّمك.</div>' : ''}
        </main>
        <aside class="mapcard" id="mapBtn">
          <canvas id="lobbyMap" width="256" height="256"></canvas>
          <div><small>الخريطة</small><b>${esc(m.ar)}</b><span>${esc(m.name)} · ${m.training ? 'تدريب' : m.players + ' لاعب'}</span></div>
          <em>تغيير ◂</em>
        </aside>
        <div style="grid-column:2;display:flex;flex-direction:column;gap:8px">
          ${m.training ? '' : `<div class="modes">${MODES.map(x => `<button data-mode="${x.id}" class="${x.id === this.modeId ? 'on' : ''}">${x.ar}</button>`).join('')}</div>`}
          <button class="start" id="startBtn">${m.training ? 'ادخل التدريب' : 'ابدأ'}</button>
        </div>
      </div>`;
    drawMapPreview($('lobbyMap'), m, { labels: false });
    if (this.hero) this.hero.dispose();
    try { this.hero = new Preview($('heroCv')); this.hero.setLook(u.look || presetLook('saqr')); this.hero.set(u.loadout || {}); } catch { }
    $('startBtn').onclick = () => this.play();
    $('mapBtn').onclick = () => this.mapPicker();
    $('setBtn').onclick = () => this.openSettings();
    $('acctBtn').onclick = () => this.accountPanel();
    $('wardBtn').onclick = () => this.wardrobe('chute');
    $('lookBtn').onclick = () => this.creator();
    $('lobby').querySelectorAll('[data-mode]').forEach(b => b.onclick = () => { this.modeId = b.dataset.mode; try { localStorage.setItem('kryvon.mode', this.modeId); } catch { } this.showLobby(); });
    if ($('lbBtn')) $('lbBtn').onclick = () => this.leaderboard();
    if ($('linkNow')) $('linkNow').onclick = e => { e.preventDefault(); this.accountPanel(); };
  }

  play() {
    try { localStorage.setItem('kryvon.map', this.mapId); localStorage.setItem('kryvon.mode', this.modeId); } catch { }
    if (this.hero) { this.hero.dispose(); this.hero = null; }
    $('auth').innerHTML = ''; // drop forms (and saved passwords) from the DOM
    const m = this.map, go = () => {
      $('queue').style.display = 'none';
      this.show('loading', m.training ? 'تجهيز ساحة التدريب…' : `بناء ${m.ar}…`);
      setTimeout(() => this.game.start({ map: m, settings: this.settings, user: account.user, mode: this.modeId }), 60);
    };
    if (m.training) return go();
    // queue: fills the lobby; empty seats are bots until online play exists
    const mode = MODES.find(x => x.id === this.modeId);
    $('queue').style.display = 'flex';
    $('qtitle').textContent = `جاري البحث عن مباراة – ${m.ar} · ${mode.ar}`;
    $('qnote').textContent = 'الأماكن الفاضية بتتعبّى ببوتات لحد ما يصير اللعب أونلاين.';
    let n = mode.size; const t0 = performance.now();
    const tick = () => {
      const k = Math.min(1, (performance.now() - t0) / 2600);
      n = Math.max(n, Math.round(mode.size + (m.players - mode.size) * k * k));
      $('qcount').textContent = `${n}/${m.players}`;
      if (k < 1) requestAnimationFrame(tick); else setTimeout(go, 250);
    };
    tick();
  }

  // ---------------- wardrobe / shop ----------------
  // ---------------- character creator ----------------
  creator(tab = 'presets', draft) {
    const u = account.user;
    const L = draft || sanitizeLook(u.look || presetLook('saqr'));
    const hex = c => '#' + c.toString(16).padStart(6, '0');
    const chips = (k, label, show = true) => show ? `<div class="grp"><small>${label}</small><div class="chips">${LOOK.options[k].map(([v, t]) => `<button data-k="${k}" data-v='${JSON.stringify(v)}' class="${L[k] === v ? 'on' : ''}">${t}</button>`).join('')}</div></div>` : '';
    const dots = (k, label, list, show = true) => show ? `<div class="grp"><small>${label}</small><div class="dots">${list.map((c, i) => `<button data-k="${k}" data-v="${i}" class="${L[k] === i ? 'on' : ''}" style="background:${hex(c)}" aria-label="${label} ${i + 1}"></button>`).join('')}</div></div>` : '';
    const tog = (k, label, show = true) => show ? `<div class="grp"><small>${label}</small><div class="chips"><button data-k="${k}" data-v="false" class="${!L[k] ? 'on' : ''}">لا</button><button data-k="${k}" data-v="true" class="${L[k] ? 'on' : ''}">نعم</button></div></div>` : '';
    const f = L.sex === 'f';
    const tabs = [['presets', 'شخصيات جاهزة'], ['face', 'الوجه والجسم'], ['hair', f ? 'الشعر والحجاب' : 'الشعر واللحية'], ['clothes', 'الملابس'], ['gear', 'إكسسوارات']];
    const body = {
      presets: `<div class="presets">${PRESET_LOOKS.map(p => `<button data-preset="${p.id}"><b>${p.ar}</b><small>${p.role}</small></button>`).join('')}</div><p class="dim" style="font-size:12px">اختار شخصية وعدّل عليها اللي بدك من باقي الأقسام.</p>`,
      face: chips('sex', 'الجنس') + dots('skin', 'لون البشرة', LOOK.skins) + chips('face', 'شكل الوجه') + dots('eyes', 'لون العيون', LOOK.eyeColors) + chips('build', 'الجسم') + chips('lips', 'الشفايف', f),
      hair: tog('hijab', 'حجاب', f) + dots('hijabColor', 'لون الحجاب', LOOK.clothColors, f && L.hijab) + chips('hair', 'تسريحة الشعر', !L.hijab) + dots('hairColor', 'لون الشعر', LOOK.hairColors, !L.hijab) + chips('beard', 'اللحية', !f),
      clothes: chips('top', 'القطعة العلوية') + dots('topColor', 'لونها', LOOK.clothColors) + chips('topPattern', 'النقشة') + dots('pantsColor', 'لون البنطلون', LOOK.clothColors) + chips('pantsPattern', 'نقشة البنطلون') + chips('boots', 'البوط'),
      gear: chips('vest', 'السترة') + dots('vestColor', 'لون السترة', LOOK.clothColors, L.vest !== 'none') + chips('scarf', 'الشماغ / اللفحة', !L.hijab) + chips('glasses', 'النظارة') + chips('patch', 'رقعة الكتف') + tog('gloves', 'كفوف') + tog('tags', 'قلادة عسكرية'),
    }[tab];
    const m = this.modal(`
      <h2>🧑 شخصيتك</h2>
      <nav class="stabs">${tabs.map(([id, t]) => `<button data-tab="${id}" class="${id === tab ? 'on' : ''}">${t}</button>`).join('')}</nav>
      <div class="cc"><canvas id="ccCv"></canvas><div class="opts2">${body}</div></div>
      <div class="err" id="ccErr"></div>
      <div class="ccfoot"><button class="primary" id="ccSave">حفظ الشخصية</button><button class="ghost small" id="ccRand">🎲 عشوائي</button></div>`, 'wide');
    const pv = new Preview(m.querySelector('#ccCv')); pv.setLook(L); pv.set(u.loadout || {}); pv.zoom(tab === 'face' || tab === 'hair');
    const redo = (t = tab, d = L) => { pv.dispose(); this.onModalClose = null; this.creator(t, d); };
    m.querySelectorAll('[data-tab]').forEach(b => b.onclick = () => redo(b.dataset.tab));
    m.querySelectorAll('[data-preset]').forEach(b => b.onclick = () => redo('presets', presetLook(b.dataset.preset)));
    m.querySelectorAll('[data-k]').forEach(b => b.onclick = () => redo(tab, sanitizeLook({ ...L, [b.dataset.k]: JSON.parse(b.dataset.v) })));
    m.querySelector('#ccRand').onclick = async () => { const { randomLook } = await import('./look.js'); redo(tab, randomLook()); };
    m.querySelector('#ccSave').onclick = async () => {
      try { await account.saveLook(L); pv.dispose(); this.onModalClose = null; this.closeModal(); if (!this.game.world) this.showLobby(); }
      catch (x) { m.querySelector('#ccErr').textContent = translateErr(x.message); }
    };
    this.onModalClose = () => pv.dispose();
  }

  wardrobe(slot = 'chute') {
    const u = account.user;
    const items = COSMETICS.filter(c => c.slot === slot);
    const hex = c => '#' + (c == null ? 0 : c).toString(16).padStart(6, '0');
    const m = this.modal(`
      <h2>👕 الخزانة والمتجر <span class="coin" style="font-size:14px;margin-inline-start:8px">🪙 ${u.coins ?? 0}</span></h2>
      <div class="shop">
        <div class="slots-tabs">${SLOTS.map(x => `<button data-slot="${x.id}" class="${x.id === slot ? 'on' : ''}">${x.ar}</button>`).join('')}</div>
        <div class="shopwrap"><canvas id="shopCv"></canvas>
        <div class="shopgrid">${items.map(c => {
          const own = account.owns(c.id), eq = (u.loadout || {})[slot] === c.id;
          const sw = c.color == null ? 'repeating-linear-gradient(45deg,#333 0 6px,#222 6px 12px)' : c.color2 != null ? `linear-gradient(135deg,${hex(c.color)} 60%,${hex(c.color2)} 60%)` : hex(c.color);
          return `<div class="si ${eq ? 'eq' : ''}" data-id="${c.id}"><div class="sw2" style="background:${sw}"></div><b>${c.ar}</b>
            <span class="pr">${c.price ? (own ? 'مملوك' : '🪙 ' + c.price) : 'مجاني'}</span>
            ${eq ? '<button class="on" disabled>✓ لابسه</button>' : own ? '<button class="eqb">البس</button>' : `<button class="buy" ${(u.coins ?? 0) < c.price ? 'disabled style="opacity:.5"' : ''}>اشتري</button>`}</div>`;
        }).join('')}</div></div>
        <div class="err" id="shopErr"></div>
        <p class="dim" style="font-size:12px">العملات بتجمعها من المباريات: مشاركة، قتلات، أفضل 10، والفوز.</p>
      </div>`, 'wide');
    const pv = new Preview(m.querySelector('#shopCv')); pv.setLook(u.look || presetLook('saqr')); pv.set(u.loadout || {});
    m.querySelectorAll('[data-slot]').forEach(b => b.onclick = () => { pv.dispose(); this.wardrobe(b.dataset.slot); });
    m.querySelectorAll('.si').forEach(card => {
      const id = card.dataset.id;
      card.onmouseenter = () => pv.set({ ...(u.loadout || {}), [slot]: id });
      card.onmouseleave = () => pv.set(account.user.loadout || {});
      const b = card.querySelector('button.buy, button.eqb');
      if (b) b.onclick = async () => {
        try {
          if (b.classList.contains('buy')) await account.buy(id);
          await account.equip(id);
          pv.dispose(); this.onModalClose = () => !this.game.world && this.showLobby(); this.wardrobe(slot);
        } catch (x) { m.querySelector('#shopErr').textContent = x.message === 'Not enough coins' ? 'ما معك عملات كفاية' : translateErr(x.message); }
      };
    });
    this.onModalClose = () => { pv.dispose(); if (!this.game.world) this.showLobby(); };
  }

  // ---------------- modal helper ----------------
  modal(html, cls = '') {
    this.closeModal();
    const m = h(`<div class="modal ${cls}" dir="rtl"><div class="box"><button class="x" aria-label="إغلاق">✕</button>${html}</div></div>`);
    m.querySelector('.x').onclick = () => this.closeModal();
    m.onclick = e => { if (e.target === m) this.closeModal(); };
    document.body.appendChild(m);
    this.modalEl = m;
    if (this.game.input) this.game.input.enabled = false;
    return m;
  }
  closeModal() {
    if (!this.modalEl) return;
    this.modalEl.remove(); this.modalEl = null;
    if (this.onModalClose) { const f = this.onModalClose; this.onModalClose = null; f(); }
    if (this.game.input && !this.pauseOpen) this.game.input.enabled = true;
  }

  // ---------------- map picker ----------------
  mapPicker() {
    const m = this.modal(`<h2>اختار الخريطة</h2><div class="maps">${MAPS.map(mp => `
      <button class="mp ${mp.id === this.mapId ? 'on' : ''}" data-id="${mp.id}">
        <canvas width="200" height="200"></canvas>
        <b>${esc(mp.ar)}</b><span class="en">${esc(mp.name)}</span>
        <p>${esc(mp.desc)}</p>
        <span class="meta">${mp.training ? '🎯 تدريب' : `👥 ${mp.players}`} · 📐 ${mp.size} · ⏱ ${mp.time}</span>
      </button>`).join('')}</div>`, 'wide');
    m.querySelectorAll('.mp').forEach(b => {
      drawMapPreview(b.querySelector('canvas'), MAPS.find(x => x.id === b.dataset.id), { labels: false });
      b.onclick = () => { this.mapId = b.dataset.id; this.closeModal(); this.showLobby(); };
    });
  }

  // ---------------- settings ----------------
  openSettings(tab = 'graphics') {
    const s = this.settings, inGame = !!this.game.world;
    const rows = SCHEMA.filter(r => r[0] === tab).map(([, path, label, type, x = {}]) => {
      if (type === 'header') return `<h3>${label}</h3>`;
      if (type === 'binds') {
        const b = s.controls.binds;
        return `<div class="binds">${ACTIONS.map(([code, name]) => `<div class="bind"><span>${name}</span><button data-bind="${code}">${keyLabel(b[code] || code)}</button></div>`).join('')}
          <button class="ghost small" id="bindReset">رجّع الأزرار الافتراضية</button></div>`;
      }
      const v = getPath(s, path), tag = x.restart && inGame ? '<i class="rs">بالمباراة الجاية</i>' : '';
      if (type === 'range') return `<label class="row"><span>${label}${tag}</span><input type="range" data-p="${path}" min="${x.min}" max="${x.max}" step="${x.step}" value="${v}"><output>${v}${x.unit || ''}</output></label>`;
      if (type === 'toggle') return `<label class="row"><span>${label}${tag}</span><input type="checkbox" class="sw" data-p="${path}" ${v ? 'checked' : ''}></label>`;
      if (type === 'select') return `<label class="row"><span>${label}${tag}</span><select data-p="${path}">${x.options.map(([ov, ol]) => `<option value="${ov}" ${String(ov) === String(v) ? 'selected' : ''}>${ol}${path === 'graphics.preset' && ov === suggestPreset() ? ' (مقترح)' : ''}</option>`).join('')}</select></label>`;
      if (type === 'color') return `<div class="row"><span>${label}</span><div class="swatches">${x.options.map(c => `<button data-p="${path}" data-v="${c}" style="background:${c}" class="${c === v ? 'on' : ''}" aria-label="${c}"></button>`).join('')}</div></div>`;
      return '';
    }).join('');
    const m = this.modal(`
      <h2>الإعدادات</h2>
      <nav class="stabs">${TABS.map(([id, l]) => `<button data-tab="${id}" class="${id === tab ? 'on' : ''}">${l}</button>`).join('')}</nav>
      <div class="srows">${rows}</div>
      <div class="sfoot"><button class="ghost small" id="sReset">رجّع إعدادات هالقسم الافتراضية</button>${tab === 'sens' ? '<span class="hint">جرّب الحساسية بساحة التدريب 🎯</span>' : ''}</div>`, 'wide settings');
    m.querySelectorAll('[data-tab]').forEach(b => b.onclick = () => this.openSettings(b.dataset.tab));
    const unit = p => (SCHEMA.find(r => r[1] === p)[4] || {}).unit || '';
    m.querySelectorAll('input[type=range]').forEach(i => i.oninput = () => { i.nextElementSibling.textContent = i.value + unit(i.dataset.p); this.set(i.dataset.p, +i.value); });
    m.querySelectorAll('input[type=checkbox]').forEach(i => i.onchange = () => this.set(i.dataset.p, i.checked));
    m.querySelectorAll('select').forEach(i => i.onchange = () => this.set(i.dataset.p, isNaN(+i.value) || i.value === '' ? i.value : +i.value));
    m.querySelectorAll('.swatches button').forEach(b => b.onclick = () => { this.set(b.dataset.p, b.dataset.v); this.openSettings(tab); });
    m.querySelector('#sReset').onclick = () => { this.settings[tab] = defaults()[tab]; if (tab === 'graphics') this.settings.graphics.preset = suggestPreset(); this.commit(); this.openSettings(tab); };
    m.querySelectorAll('[data-bind]').forEach(b => b.onclick = () => {
      b.textContent = '…اضغط زر'; b.classList.add('wait');
      const on = e => {
        e.preventDefault(); e.stopPropagation(); removeEventListener('keydown', on, true);
        if (e.code !== 'Escape') {
          const binds = this.settings.controls.binds, act = b.dataset.bind, prev = binds[act] || act;
          for (const [c] of ACTIONS) if (c !== act && (binds[c] || c) === e.code) binds[c] = prev; // swap on conflict
          binds[act] = e.code;
          for (const k in binds) if (binds[k] === k) delete binds[k];
          this.commit();
        }
        this.openSettings('controls');
      };
      addEventListener('keydown', on, true);
    });
    const br = m.querySelector('#bindReset'); if (br) br.onclick = () => { this.settings.controls.binds = {}; this.commit(); this.openSettings('controls'); };
  }
  set(path, v) { setPath(this.settings, path, v); this.commit(); }
  commit() {
    saveLocal(this.settings);
    account.saveSettings(this.settings);
    if (this.game.applySettings) this.game.applySettings(this.settings);
  }

  // ---------------- account ----------------
  accountPanel() {
    const u = account.user;
    const google = account.config && account.config.google;
    const m = this.modal(`
      <h2>الحساب</h2>
      <div class="acct">
        <div class="row"><span>اسم اللاعب</span><form id="nameF" class="inline"><input name="username" value="${esc(u.username)}" maxlength="16" dir="ltr"><button>حفظ</button></form></div>
        ${account.offline ? '<div class="note">هاد بروفايل أوفلاين محفوظ على هالجهاز بس. شغّل السيرفر عشان تعمل حساب حقيقي.</div>' : `
        <h3>ربط الحساب</h3>
        <div class="link ${u.email ? 'ok' : ''}"><span>✉ الإيميل وكلمة السر</span>${u.email ? `<b dir="ltr">${esc(u.email)}</b>` : `
          <form id="linkF"><input name="email" type="email" placeholder="الإيميل" required dir="ltr" autocomplete="email"><input name="password" type="password" placeholder="كلمة السر (6+)" required dir="ltr" autocomplete="new-password"><button>اربط</button></form>`}</div>
        <div class="link ${u.google ? 'ok' : ''}"><span>G حساب Google</span>${u.google ? `<b dir="ltr">${esc(u.google)}</b>${u.email ? '<button class="ghost small" id="unG">فك الربط</button>' : ''}` : google ? '<div id="gLink"></div>' : '<small class="dim">مش مفعّل على هالسيرفر (لازم GOOGLE_CLIENT_ID)</small>'}</div>
        ${u.email ? `<h3>تغيير كلمة السر</h3><form id="pwF" class="col"><input name="old" type="password" placeholder="كلمة السر الحالية" dir="ltr" autocomplete="current-password"><input name="password" type="password" placeholder="كلمة السر الجديدة" dir="ltr" autocomplete="new-password"><button>تغيير</button></form>` : ''}
        `}
        <div class="err" id="acErr"></div><div class="okmsg" id="acOk"></div>
        <button class="danger" id="logoutBtn">تسجيل الخروج</button>
        ${u.guest && !u.email && !u.google && !account.offline ? '<small class="dim">⚠ إذا طلعت من حساب ضيف بدون ما تربطه، بيضيع.</small>' : ''}
      </div>`);
    const err = t => { $('acErr').textContent = t || ''; $('acOk').textContent = ''; };
    const ok = t => { $('acOk').textContent = t; $('acErr').textContent = ''; };
    const refresh = () => { this.accountPanel(); if (!this.game.world) this.onModalClose = () => this.showLobby(); };
    m.querySelector('#nameF').onsubmit = async e => { e.preventDefault(); try { await account.rename(e.target.username.value.trim()); ok('تم الحفظ'); this.onModalClose = () => !this.game.world && this.showLobby(); } catch (x) { err(translateErr(x.message)); } };
    const lf = m.querySelector('#linkF');
    if (lf) lf.onsubmit = async e => { e.preventDefault(); try { await account.linkEmail(lf.email.value.trim(), lf.password.value); refresh(); } catch (x) { err(translateErr(x.message)); } };
    const pf = m.querySelector('#pwF');
    if (pf) pf.onsubmit = async e => { e.preventDefault(); try { await account.changePassword(pf.old.value, pf.password.value); pf.reset(); ok('تغيّرت كلمة السر'); } catch (x) { err(translateErr(x.message)); } };
    const gl = m.querySelector('#gLink');
    if (gl) this.googleButton(gl, async cred => { try { await account.linkGoogle(cred); refresh(); } catch (x) { err(translateErr(x.message)); } });
    const ug = m.querySelector('#unG');
    if (ug) ug.onclick = async () => { try { await account.unlinkGoogle(); refresh(); } catch (x) { err(translateErr(x.message)); } };
    m.querySelector('#logoutBtn').onclick = async () => {
      if (u.guest && !u.email && !u.google && !account.offline && !confirm('حساب الضيف رح يضيع. متأكد؟')) return;
      await account.logout(); this.closeModal();
      if (this.game.world) location.search = ''; else this.showAuth();
    };
  }

  async leaderboard() {
    const m = this.modal('<h2>🏆 المتصدرين</h2><div class="lb">…</div>');
    try {
      const { top } = await account.leaderboard();
      m.querySelector('.lb').innerHTML = top.length ? `<table><tr><th>#</th><th>اللاعب</th><th>التصنيف</th><th>فوز</th><th>قتلات</th></tr>${top.map((r, i) => { const t = tierOf(r.tp || 0); return `<tr class="${r.username === account.user.username ? 'me' : ''}"><td>${i + 1}</td><td dir="ltr">${esc(r.username)}</td><td style="color:${t.color}">${t.ar} ${t.roman} · ${r.tp || 0}</td><td>${r.wins}</td><td>${r.kills}</td></tr>`; }).join('')}</table>` : '<p class="dim">لسا ما حدا لعب. كون الأول!</p>';
    } catch { m.querySelector('.lb').textContent = 'ما قدرنا نجيب القائمة.'; }
  }

  // ---------------- in-game pause ----------------
  togglePause(force) {
    const open = force ?? !this.pauseOpen;
    if (open === !!this.pauseOpen) return;
    this.pauseOpen = open;
    $('pause').style.display = open ? 'flex' : 'none';
    if (this.game.input) this.game.input.enabled = !open;
    if (open) { document.exitPointerLock && document.exitPointerLock(); this.game.input && (this.game.input.fire = false); }
  }
  bindPause() {
    $('pResume').onclick = () => { this.togglePause(false); this.game.input.lock(); };
    $('pSettings').onclick = () => this.openSettings('sens');
    $('pQuit').onclick = () => { if (this.game.map.training || confirm('رح تطلع من المباراة. متأكد؟')) location.search = ''; };
    $('gearBtn').onclick = () => this.togglePause(true);
    $('bagX').onclick = () => this.game.hud.toggleBag(false);
    $('bag').onclick = e => { if (e.target.id === 'bag') this.game.hud.toggleBag(false); };
    addEventListener('keydown', e => {
      if (!this.game.world || this.game.over) return;
      if (e.code === 'Escape' || e.code === 'KeyP') {
        if (this.modalEl) { this.closeModal(); return; }
        if (this.game.hud.bagOpen) { this.game.hud.toggleBag(false); return; }
        this.togglePause();
      }
    });
    // Esc in the browser drops pointer lock without a key event reaching us → open the pause menu
    document.addEventListener('pointerlockchange', () => {
      if (!document.pointerLockElement && this.game.world && !this.game.over && !this.modalEl && !this.game.input.touch && !this.game.hud.mapOpen && !this.game.hud.bagOpen && !this.game.hud.chatKeys) this.togglePause(true);
    });
  }
}

function translateErr(m) {
  const t = {
    'Username: 3–16 letters, numbers, _ . -': 'اسم اللاعب: من 3 لـ 16 حرف أو رقم أو _ . -',
    'Invalid email': 'الإيميل مش صحيح',
    'Password must be 6–128 characters': 'كلمة السر لازم تكون 6 أحرف أو أكثر',
    'Username is taken': 'اسم اللاعب محجوز',
    'An account with this email already exists': 'في حساب على هالإيميل',
    'Wrong username/email or password': 'الاسم/الإيميل أو كلمة السر غلط',
    'Too many attempts – wait a few minutes': 'محاولات كثير – استنى كم دقيقة',
    'That email belongs to another account': 'هالإيميل مربوط بحساب ثاني',
    'That Google account is linked to another player': 'حساب Google هاد مربوط بلاعب ثاني',
    'Add an email + password first, or you would lose access': 'اربط إيميل وكلمة سر أول، وإلا ما رح تقدر تدخل',
    'Current password is wrong': 'كلمة السر الحالية غلط',
    'Please sign in again': 'سجّل دخول مرة ثانية',
    'Failed to fetch': 'ما في اتصال بالسيرفر',
  };
  return t[m] || m;
}
