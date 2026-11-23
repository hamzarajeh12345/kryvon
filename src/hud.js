import * as THREE from 'three';
import { HALF, WEAPONS, ITEMS, AMMO, ARMOR, RARITY_COLORS, RING, ATTACHMENTS, ATT_SLOTS, QUICK_CHAT } from './config.js';
import { itemName, itemRarity, magCap } from './actor.js';
import { clamp } from './world.js';

const $ = id => document.getElementById(id);
const fmt = s => { s = Math.max(0, Math.ceil(s)); return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`; };

export class HUD {
  constructor(game) {
    this.g = game;
    this.el = $('hud');
    this.mini = $('minimap'); this.mctx = this.mini.getContext('2d');
    this.big = $('bigmap'); this.bctx = this.big.getContext('2d');
    this.comp = $('compass'); this.cctx = this.comp.getContext('2d');
    this.feed = $('feed');
    this.miniT = 0; this.mapOpen = false;
    this.bakeMap();
  }

  // bake terrain + buildings into a 1024px image once
  bakeMap() {
    const w = this.g.world, S = 1024;
    const c = document.createElement('canvas'); c.width = c.height = S;
    const ctx = c.getContext('2d'), img = ctx.createImageData(S, S);
    const col = new THREE.Color();
    for (let j = 0; j < S; j++) for (let i = 0; i < S; i++) {
      const x = -HALF + (i + 0.5) / S * HALF * 2, z = -HALF + (j + 0.5) / S * HALF * 2;
      const h = w.getHeight(x, z), lvl = w.waterLevel(x, z);
      const k = (j * S + i) * 4;
      if (h < lvl) { const d = Math.min(1, (lvl - h) / 10), foam = h > lvl - 0.8 ? 40 : 0; img.data[k] = 34 - d * 18 + foam; img.data[k + 1] = 128 - d * 50 + foam; img.data[k + 2] = 168 - d * 40 + foam; }
      else {
        const hx = w.getHeight(x + 3, z) - h, hz = w.getHeight(x, z + 3) - h;
        const ny = 1 / Math.hypot(hx / 3, hz / 3, 1);
        w.biomeColor(x, z, h, ny, col);
        const shade = clamp(1 + (-hx * 1.2 - hz) * 0.11, 0.55, 1.35);                // hillshade, light from the north-west
        img.data[k] = Math.min(255, col.r * 255 * shade); img.data[k + 1] = Math.min(255, col.g * 255 * shade); img.data[k + 2] = Math.min(255, col.b * 255 * shade);
      }
      img.data[k + 3] = 255;
    }
    ctx.putImageData(img, 0, 0);
    ctx.fillStyle = 'rgba(70,60,50,0.85)';
    const p = v => (v + HALF) / (HALF * 2) * S;
    // roads on top of the terrain
    ctx.strokeStyle = 'rgba(92,78,58,.55)'; ctx.lineWidth = 2.2; ctx.lineJoin = ctx.lineCap = 'round';
    for (const road of w.roadLines || []) { ctx.lineWidth = road.major ? 3.2 : 1.8; ctx.strokeStyle = road.major ? 'rgba(60,60,64,.8)' : 'rgba(92,78,58,.55)'; ctx.beginPath(); road.pts.forEach(([x, z], i) => i ? ctx.lineTo(p(x), p(z)) : ctx.moveTo(p(x), p(z))); ctx.stroke(); }
    for (const road of w.roadLines || []) { if (!road.major) continue; ctx.strokeStyle = 'rgba(240,226,180,.6)'; ctx.lineWidth = 0.8; ctx.beginPath(); road.pts.forEach(([x, z], i) => i ? ctx.lineTo(p(x), p(z)) : ctx.moveTo(p(x), p(z))); ctx.stroke(); }
    ctx.fillStyle = 'rgba(80,80,84,.75)'; for (const s of w.streets || []) ctx.fillRect(p(s.minX), p(s.minZ), Math.max(1, p(s.maxX) - p(s.minX)), Math.max(1, p(s.maxZ) - p(s.minZ)));
    ctx.fillStyle = 'rgba(70,60,50,0.85)';
    for (const b of w.buildings) ctx.fillRect(p(b.minX), p(b.minZ), Math.max(1.5, p(b.maxX) - p(b.minX)), Math.max(1.5, p(b.maxZ) - p(b.minZ)));
    this.base = c;
  }

  toPx(v, size) { return (v + HALF) / (HALF * 2) * size; }

  drawOverlays(ctx, size, view) {
    // view: {x0,z0,span} world window mapped onto [0,size]
    const g = this.g, sc = size / view.span;
    const X = x => (x - view.x0) * sc, Z = z => (z - view.z0) * sc;
    // Black Zone heatmap
    const hm = g.heat, N = hm.N, cell = HALF * 2 / N;
    for (let j = 0; j < N; j++) for (let i = 0; i < N; i++) {
      const v = hm.grid[j * N + i]; if (v <= 0) continue;
      ctx.fillStyle = `rgba(0,0,0,${Math.min(0.82, 0.18 + v / 30)})`;
      ctx.fillRect(X(-HALF + i * cell), Z(-HALF + j * cell), cell * sc + 1, cell * sc + 1);
    }
    // ring outside tint
    const r = g.ring;
    ctx.save();
    ctx.beginPath(); ctx.rect(0, 0, size, size); ctx.arc(X(r.cur.x), Z(r.cur.z), r.cur.r * sc, 0, Math.PI * 2, true);
    ctx.fillStyle = 'rgba(40,90,220,0.28)'; ctx.fill('evenodd');
    ctx.restore();
    ctx.lineWidth = 2; ctx.strokeStyle = '#3b8bff';
    ctx.beginPath(); ctx.arc(X(r.cur.x), Z(r.cur.z), r.cur.r * sc, 0, Math.PI * 2); ctx.stroke();
    if (r.next) { ctx.strokeStyle = '#fff'; ctx.setLineDash([6, 4]); ctx.beginPath(); ctx.arc(X(r.next.x), Z(r.next.z), Math.max(2, r.next.r * sc), 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([]); }
    // red zone
    const rz = g.redzone;
    if (rz.visible) { ctx.fillStyle = rz.state === 'warn' ? 'rgba(255,40,40,0.35)' : 'rgba(255,20,20,0.55)'; ctx.beginPath(); ctx.arc(X(rz.x), Z(rz.z), rz.r * sc, 0, Math.PI * 2); ctx.fill(); }
    // landmarks
    const big = view.span > 1000;
    for (const l of g.world.landmarks) {
      const x = X(l.x), y = Z(l.z); if (x < -10 || y < -10 || x > size + 10 || y > size + 10) continue;
      const r = big ? 5 : 4;
      ctx.fillStyle = g.seenLandmarks && g.seenLandmarks.has(l.id) ? '#8a6bb0' : '#c99bff'; ctx.strokeStyle = '#000'; ctx.lineWidth = 1.2;
      ctx.beginPath(); ctx.moveTo(x, y - r); ctx.lineTo(x + r, y); ctx.lineTo(x, y + r); ctx.lineTo(x - r, y); ctx.closePath(); ctx.fill(); ctx.stroke();
      if (big) {
        ctx.font = '10px system-ui, Tahoma, sans-serif'; ctx.textAlign = 'center'; ctx.lineWidth = 3; ctx.strokeStyle = 'rgba(0,0,0,.7)'; ctx.fillStyle = '#e6d2ff';
        ctx.strokeText(l.ar, x, y + 15); ctx.fillText(l.ar, x, y + 15);
      }
    }
    // crates
    for (const c of g.loot.crates) {
      if (c.kind === 'death') continue;
      ctx.fillStyle = c.kind === 'flare' ? '#ff4040' : '#3aa0ff'; ctx.strokeStyle = '#ffc531'; ctx.lineWidth = 2;
      ctx.fillRect(X(c.x) - 5, Z(c.z) - 5, 10, 10); ctx.strokeRect(X(c.x) - 5, Z(c.z) - 5, 10, 10);
    }
    // Sara's mark
    if (g.saraMark && g.saraMark.alive) { ctx.fillStyle = '#b46cff'; ctx.beginPath(); ctx.arc(X(g.saraMark.x), Z(g.saraMark.z), 5, 0, 7); ctx.fill(); }
    // plane path
    if (g.plane && g.plane.active) {
      ctx.strokeStyle = 'rgba(255,255,255,0.7)'; ctx.lineWidth = 2; ctx.setLineDash([10, 6]);
      ctx.beginPath(); ctx.moveTo(X(g.plane.a.x), Z(g.plane.a.z)); ctx.lineTo(X(g.plane.b.x), Z(g.plane.b.z)); ctx.stroke(); ctx.setLineDash([]);
      const pp = g.plane.pos; ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(X(pp.x), Z(pp.z), 5, 0, 7); ctx.fill();
    }
    // vehicles
    for (const v of g.vehicles ? g.vehicles.list : []) {
      const x = X(v.pos.x), y = Z(v.pos.z); if (x < -5 || y < -5 || x > size + 5 || y > size + 5) continue;
      ctx.fillStyle = v.dead ? '#555' : '#e8e8e8'; ctx.strokeStyle = '#000'; ctx.lineWidth = 1;
      ctx.save(); ctx.translate(x, y); ctx.rotate(-v.yaw); ctx.fillRect(-2, -3.5, 4, 7); ctx.strokeRect(-2, -3.5, 4, 7); ctx.restore();
    }
    // pings
    for (const pg of g.pings) {
      ctx.fillStyle = pg.kind === 'enemy' ? '#ff3b3b' : '#ffd34d'; ctx.strokeStyle = '#000'; ctx.lineWidth = 1.5;
      const x = X(pg.x), y = Z(pg.z); ctx.beginPath(); ctx.moveTo(x, y - 7); ctx.lineTo(x + 5, y); ctx.lineTo(x, y + 7); ctx.lineTo(x - 5, y); ctx.closePath(); ctx.fill(); ctx.stroke();
    }
    // teammates
    const MATE_COL = ['#ffd34d', '#4ade80', '#60a5fa', '#f472b6'];
    (g.teams[g.player.team] || []).forEach((m, i) => {
      if (m.isPlayer || !m.alive) return;
      ctx.fillStyle = m.knocked ? '#ff4d4d' : MATE_COL[i % 4]; ctx.strokeStyle = '#000'; ctx.lineWidth = 1.5;
      ctx.beginPath(); ctx.arc(X(m.pos.x), Z(m.pos.z), 4.5, 0, 7); ctx.fill(); ctx.stroke();
    });
    // player arrow
    const p = g.player;
    ctx.save(); ctx.translate(X(p.pos.x), Z(p.pos.z)); ctx.rotate(-g.camYaw);
    ctx.fillStyle = '#ffd34d'; ctx.strokeStyle = '#000'; ctx.lineWidth = 1.5;
    ctx.beginPath(); ctx.moveTo(0, -8); ctx.lineTo(5.5, 6); ctx.lineTo(0, 3); ctx.lineTo(-5.5, 6); ctx.closePath(); ctx.fill(); ctx.stroke();
    ctx.restore();
  }

  drawMinimap() {
    const ctx = this.mctx, S = this.mini.width, p = this.g.player.pos;
    const span = 360, x0 = p.x - span / 2, z0 = p.z - span / 2;
    ctx.fillStyle = '#1f6f8b'; ctx.fillRect(0, 0, S, S);
    const B = this.base.width, k = B / (HALF * 2);
    ctx.drawImage(this.base, (x0 + HALF) * k, (z0 + HALF) * k, span * k, span * k, 0, 0, S, S);
    this.drawOverlays(ctx, S, { x0, z0, span });
    // direction to ring when outside
    const r = this.g.ring, d = Math.hypot(p.x - r.cur.x, p.z - r.cur.z);
    if (d > r.cur.r) {
      const a = Math.atan2(r.cur.z - p.z, r.cur.x - p.x);
      ctx.strokeStyle = '#fff'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(S / 2, S / 2); ctx.lineTo(S / 2 + Math.cos(a) * S * 0.45, S / 2 + Math.sin(a) * S * 0.45); ctx.stroke();
    }
  }

  drawBigMap() {
    const ctx = this.bctx, S = this.big.width;
    ctx.drawImage(this.base, 0, 0, S, S);
    // zone labels
    ctx.textAlign = 'center';
    for (const z of this.g.world.zones) {
      const x = this.toPx(z.x, S), y = this.toPx(z.z, S);
      const major = !z.minor, gold = z.tier === 3;
      ctx.lineJoin = 'round'; ctx.strokeStyle = 'rgba(20,14,8,0.75)';
      ctx.font = `bold ${major ? 17 : 12}px Tahoma, system-ui, sans-serif`; ctx.lineWidth = 4;
      ctx.fillStyle = gold ? '#ffd27a' : '#fff';
      ctx.strokeText(z.ar, x, y - 4); ctx.fillText(z.ar, x, y - 4);
      ctx.font = `800 ${major ? 11 : 9}px system-ui, sans-serif`; ctx.lineWidth = 3; ctx.fillStyle = gold ? '#ffe7a8' : '#e9e2d0';
      ctx.strokeText(z.name.toUpperCase(), x, y + 9); ctx.fillText(z.name.toUpperCase(), x, y + 9);
    }
    ctx.fillStyle = 'rgba(255,255,255,0.85)'; ctx.font = 'bold 16px Tahoma, system-ui'; ctx.fillText('البحر الأبيض المتوسط', this.toPx(-690, S), this.toPx(-10, S));
    ctx.font = 'italic 600 11px system-ui'; ctx.fillStyle = 'rgba(190,230,255,0.85)'; ctx.fillText('MEDITERRANEAN SEA', this.toPx(-690, S), this.toPx(-10, S) + 15);
    this.drawOverlays(ctx, S, { x0: -HALF, z0: -HALF, span: HALF * 2 });
  }

  drawCompass() {
    const ctx = this.cctx, W = this.comp.width, H = this.comp.height;
    ctx.clearRect(0, 0, W, H);
    const yawDeg = ((-this.g.camYaw * 180 / Math.PI) % 360 + 360) % 360; // 0 = north
    ctx.textAlign = 'center'; ctx.fillStyle = '#fff';
    for (let d = -60; d <= 60; d += 5) {
      const a = Math.round(yawDeg / 5) * 5 + d, aa = ((a % 360) + 360) % 360;
      const x = W / 2 + (a - yawDeg) * (W / 120);
      const lab = { 0: 'N', 45: 'NE', 90: 'E', 135: 'SE', 180: 'S', 225: 'SW', 270: 'W', 315: 'NW' }[aa];
      ctx.globalAlpha = 1 - Math.abs(x - W / 2) / (W / 2) * 0.8;
      if (lab) { ctx.font = 'bold 14px system-ui'; ctx.fillStyle = lab === 'N' ? '#ffd34d' : '#fff'; ctx.fillText(lab, x, 18); }
      else if (aa % 15 === 0) { ctx.font = '10px system-ui'; ctx.fillStyle = '#ddd'; ctx.fillText(aa, x, 17); }
      else { ctx.fillStyle = '#fff'; ctx.fillRect(x - 0.5, 8, 1, 6); }
    }
    ctx.globalAlpha = 1; ctx.fillStyle = '#ffd34d'; ctx.fillRect(W / 2 - 1, 22, 2, 6);
  }

  killfeed(html) {
    const d = document.createElement('div'); d.className = 'kf'; d.innerHTML = html;
    this.feed.prepend(d);
    while (this.feed.children.length > 6) this.feed.lastChild.remove();
    setTimeout(() => d.classList.add('fade'), 6000); setTimeout(() => d.remove(), 7000);
  }

  update(dt) {
    const g = this.g, p = g.player;
    this.miniT -= dt;
    if (this.miniT <= 0) { this.miniT = 0.1; this.drawMinimap(); if (this.mapOpen) this.drawBigMap(); }
    this.drawCompass();
    $('hpfill').style.width = p.hp + '%';
    $('hpfill').style.background = p.hp > 60 ? '#e8e8e8' : p.hp > 30 ? '#ffb347' : '#ff4d4d';
    $('boostfill').style.width = p.boost + '%';
    const stamina = $('staminafill'); if (stamina) stamina.style.width = p.stamina + '%';
    $('alive').textContent = g.aliveCount;
    $('teamsleft').parentElement.style.display = g.teamSize > 1 ? '' : 'none';
    $('teamsleft').textContent = g.teamsLeft;
    this.updateTeam();
    document.body.classList.toggle('knocked', !!p.knocked);
    const tb = $('ctxE');
    if (tb) { const pr = g.prompt || ''; tb.style.display = /<b>E<\/b>/.test(pr) ? '' : 'none'; tb.textContent = /إنعاش/.test(pr) ? 'إنعاش' : /Loot|crate/i.test(pr) ? 'افتح' : 'التقط'; }
    const vh = $('vehud');
    if (vh) {
      const v = p.state === 'vehicle' ? p.vehicle : null;
      vh.style.display = v ? 'flex' : 'none';
      if (v) vh.innerHTML = `<b>${Math.round(Math.abs(v.speed) * 3.6)}</b><small>km/h</small><span>${v.def.ar}</span><i><u style="width:${Math.max(0, v.hp / v.def.hp * 100)}%;background:${v.hp / v.def.hp > 0.3 ? '#e8e8e8' : '#ff4d4d'}"></u></i>`;
    }
    const fb = $('ctxF');
    if (fb) fb.style.display = /<b>F<\/b>/.test(g.prompt || '') ? '' : 'none';
    if (this.bagOpen) { this.bagT = (this.bagT || 0) - dt; if (this.bagT <= 0) { this.bagT = 0.4; this.renderBag(); } }
    $('kills').textContent = p.kills;
    // ring info
    const r = g.ring;
    let rt = '';
    if (g.map.training) rt = '🎯 ساحة التدريب · <b>U</b> تعبئة الرف · <b>F</b> سيارة · <b>Esc</b> الإعدادات';
    else if (g.phase === 'island') rt = `✈ الطيارة بتقلع بعد <b>${fmt(g.island.t)}</b> · ${g.mode.ar} · ${g.map.ar}`;
    else if (r.state === 'wait') rt = `Al-Tawq closes in <b>${fmt(r.timer)}</b>`;
    else if (r.state === 'shrink') rt = `<span class="warn">Al-Tawq is closing</span> <b>${fmt(r.timer)}</b>`;
    else rt = 'Final circle';
    $('ringinfo').innerHTML = g.map.training || g.phase === 'island' ? rt : `${rt} · Phase ${Math.min(r.phase + 1, RING.length)}/8`;
    const out = !r.inside(p.pos.x, p.pos.z) && p.state === 'ground';
    $('outzone').style.opacity = out ? 1 : 0;
    // weapons
    for (let i = 0; i < 2; i++) {
      const w = p.weapons[i], el = $('w' + i);
      el.classList.toggle('sel', i === p.cur);
      if (w) {
        const at = Object.values(w.att || {}).filter(Boolean).length;
        el.innerHTML = `<span class="wn" style="color:${RARITY_COLORS[w.def.rarity]}">${i + 1} · ${w.def.name}</span><span class="wa">${w.mag}<small>/${p.ammo[w.def.ammo]}</small></span>${w.zoom > 1 ? `<span class="wz">${w.zoom === 1.3 ? 'RD' : w.zoom + 'x'}</span>` : ''}${at ? `<span class="wz">+${at}</span>` : ''}`;
      } else el.innerHTML = `<span class="wn dim">${i + 1} · Empty</span>`;
    }
    $('armor').innerHTML =
      `<span class="${p.helmet.lvl ? '' : 'dim'}">⛑ ${p.helmet.lvl ? 'Lv' + p.helmet.lvl + ' ' + Math.max(0, Math.round(p.helmet.dur / ARMOR.helmet.dur[p.helmet.lvl] * 100)) + '%' : '—'}</span>` +
      `<span class="${p.vest.lvl ? '' : 'dim'}">🦺 ${p.vest.lvl ? 'Lv' + p.vest.lvl + ' ' + Math.max(0, Math.round(p.vest.dur / ARMOR.vest.dur[p.vest.lvl] * 100)) + '%' : '—'}</span>` +
      `<span class="${p.pack ? '' : 'dim'}">🎒 ${Math.round(p.load())}/${p.capacity()}</span>`;
    const it = p.items;
    $('items').innerHTML =
      `<span title="H">🩹${it.gauze} 🧰${it.firstaid} 🏥${it.hospital}</span><span title="B">🌿${it.tea} ☕${it.coffee} 💉${it.syringe}</span>` +
      `<span class="thr" title="G throw / T switch">${['frag', 'slipper', 'flare'].map(k => `<i class="${p.throwSel === k ? 'sel' : ''}">${{ frag: '💣', slipper: '🩴', flare: '🎆' }[k]}${it[k]}</i>`).join('')}</span>`;
    // action bar
    if (p.action) {
      $('action').style.display = 'block';
      $('actfill').style.width = (1 - p.action.t / p.action.total) * 100 + '%';
      $('actlabel').textContent = p.action.type === 'reload' ? 'Reloading' : p.action.type === 'revive' ? `إنعاش ${p.action.target.name}` : `Using ${ITEMS[p.action.key].name}`;
    } else $('action').style.display = 'none';
    // prompt
    $('prompt').innerHTML = g.prompt || '';
    $('prompt').style.display = g.prompt ? 'block' : 'none';
    // crosshair
    const spread = g.currentSpread();
    $('cross').style.setProperty('--gap', `${8 + spread * 9}px`);
    $('cross').style.display = p.state === 'ground' && !g.scoped ? 'block' : 'none';
    $('scope').style.display = g.scoped ? 'block' : 'none';
    $('sara').style.opacity = g.saraUsed ? 0.35 : 1;
  }

  // ---------- team panel ----------
  buildTeam() {
    const g = this.g, el = $('team');
    const t = (g.teams[g.player.team] || []);
    el.style.display = t.length > 1 ? '' : 'none';
    el.innerHTML = t.map((m, i) => `<div class="tm" data-i="${i}"><span class="dot c${i % 4}">${i + 1}</span><span class="nm">${m.name}</span><div class="hb"><div></div></div></div>`).join('');
  }
  updateTeam() {
    const g = this.g, t = g.teams[g.player.team] || [], rows = $('team').children;
    t.forEach((m, i) => {
      const r = rows[i]; if (!r) return;
      r.classList.toggle('dead', !m.alive); r.classList.toggle('down', !!m.knocked);
      r.querySelector('.hb div').style.width = (m.alive ? m.hp : 0) + '%';
    });
  }

  // ---------- team chat + quick chat ----------
  chat(html) {
    const d = document.createElement('div'); d.className = 'cm'; d.innerHTML = html;
    $('chatlog').appendChild(d);
    while ($('chatlog').children.length > 5) $('chatlog').firstChild.remove();
    setTimeout(() => d.classList.add('fade'), 7000); setTimeout(() => d.remove(), 8000);
  }
  toggleChat(force) {
    const el = $('quick'), open = force ?? el.style.display !== 'grid';
    el.style.display = open ? 'grid' : 'none';
    if (open) {
      el.innerHTML = QUICK_CHAT.map((t, i) => `<button data-q="${i}"><kbd>${i + 1}</kbd>${t}</button>`).join('') + '<button data-q="ping">📍 علامة (V)</button>';
      el.querySelectorAll('button').forEach(b => b.onclick = e => { e.stopPropagation(); if (b.dataset.q === 'ping') this.g.ping(); else this.g.quickChat(+b.dataset.q); this.toggleChat(false); });
      this.chatKeys = e => { const n = +e.key; if (n >= 1 && n <= QUICK_CHAT.length) { e.preventDefault(); e.stopPropagation(); this.g.quickChat(n - 1); this.toggleChat(false); } else if (e.code === 'Escape' || e.code === 'KeyZ') { e.stopPropagation(); this.toggleChat(false); } };
      addEventListener('keydown', this.chatKeys, true);
    } else if (this.chatKeys) { removeEventListener('keydown', this.chatKeys, true); this.chatKeys = null; }
  }

  // ---------- backpack ----------
  toggleBag(force) {
    const open = force ?? !this.bagOpen;
    this.bagOpen = open;
    $('bag').style.display = open ? 'flex' : 'none';
    this.g.input.enabled = !open;
    if (open) { document.exitPointerLock && document.exitPointerLock(); this.g.input.fire = false; this.renderBag(); }
    else if (!this.g.input.touch) this.g.input.lock();
  }
  closePanels() { if (this.bagOpen) this.toggleBag(false); this.toggleChat(false); }
  renderBag() {
    const g = this.g, p = g.player, el = $('bagbody');
    const btn = (kind, key, label = 'رمي') => `<button class="drop" data-kind="${kind}" data-key='${JSON.stringify(key)}'>${label}</button>`;
    const weapon = i => {
      const w = p.weapons[i];
      if (!w) return `<div class="wcard empty"><b>${i + 1}</b> فاضي</div>`;
      return `<div class="wcard ${i === p.cur ? 'sel' : ''}">
        <div class="wh"><b style="color:${RARITY_COLORS[w.def.rarity]}">${w.def.name}</b><small>${w.def.ar || ''} · ${w.def.cls}</small>${btn('weapon', i)}</div>
        <div class="wstat">${w.mag}/${magCap(w)} · ذخيرة ${AMMO[w.def.ammo].name}: ${p.ammo[w.def.ammo]}</div>
        <div class="slots">
          <div class="slot ${w.zoom > 1 ? 'on' : ''}"><small>سكوب</small>${w.zoom > 1 ? (w.zoom === 1.3 ? 'Red Dot' : w.zoom + 'x') + btn('scope', i, '✕') : '—'}</div>
          ${ATT_SLOTS.map(([sl, ar]) => { const k = w.att && w.att[sl]; const ok = Object.values(ATTACHMENTS).some(a => a.slot === sl && a.fits.includes(w.def.cls));
            return `<div class="slot ${k ? 'on' : ''} ${ok ? '' : 'na'}"><small>${ar}</small>${k ? ATTACHMENTS[k].ar + btn('attach', [i, sl], '✕') : ok ? '—' : 'ما بركب'}</div>`; }).join('')}
        </div></div>`;
    };
    const gear = ['helmet', 'vest'].map(k => p[k].lvl ? `<div class="gi"><span>${ARMOR[k].names[p[k].lvl]} <small>Lv${p[k].lvl} · ${Math.max(0, Math.round(p[k].dur / ARMOR[k].dur[p[k].lvl] * 100))}%</small></span>${btn(k, 0)}</div>` : '').join('')
      + (p.pack ? `<div class="gi"><span>${ARMOR.pack.names[p.pack]} <small>Lv${p.pack}</small></span>${btn('pack', 0)}</div>` : '');
    const items = Object.keys(p.items).filter(k => p.items[k] > 0).map(k => {
      const d = ITEMS[k], use = d.kind === 'heal' || d.kind === 'boost';
      return `<div class="gi"><span>${d.ar || d.name} <b>×${p.items[k]}</b></span>${use ? `<button class="use" data-use="${k}">استعمل</button>` : ''}${btn('item', k)}</div>`;
    }).join('');
    const ammo = Object.keys(p.ammo).filter(k => p.ammo[k] > 0 && AMMO[k].w > 0).map(k => `<div class="gi"><span>${AMMO[k].name} <b>×${p.ammo[k]}</b></span>${btn('ammo', k)}</div>`).join('');
    el.innerHTML = `
      <section class="wcol">${weapon(0)}${weapon(1)}</section>
      <section class="icol">
        <h4>العتاد</h4>${gear || '<p class="dim">ما في عتاد</p>'}
        <h4>المواد</h4>${items || '<p class="dim">فاضية</p>'}
        <h4>الذخيرة</h4>${ammo || '<p class="dim">ما في ذخيرة</p>'}
        <div class="cap"><span>الحمل</span><i><u style="width:${Math.min(100, p.load() / p.capacity() * 100)}%"></u></i><span>${Math.round(p.load())}/${p.capacity()}</span></div>
      </section>`;
    el.querySelectorAll('.drop').forEach(b => b.onclick = () => { g.dropFromBag(b.dataset.kind, JSON.parse(b.dataset.key)); this.renderBag(); });
    el.querySelectorAll('.use').forEach(b => b.onclick = () => { if (!p.startUse(b.dataset.use)) g.toast('ما بتقدر تستعمله هلّق'); this.toggleBag(false); });
  }
}
