// Account client. Talks to server.js; if the server is unreachable (e.g. the game is hosted as
// static files) it falls back to an offline profile kept in this browser only.
import { rewards, itemById, DEFAULT_LOADOUT } from './progression.js';
const TOKEN_KEY = 'kryvon.token', OFFLINE_KEY = 'kryvon.offline';
export const RANKS = ['Recruit', 'Soldier', 'Nashmi', 'Faris', 'Batal', 'Commander', 'Sham Crown', 'KRYVON Legend'];
export const RANKS_AR = ['مستجد', 'جندي', 'نشمي', 'فارس', 'بطل', 'قائد', 'تاج الشام', 'أسطورة كرايفون'];
export const rankOf = xp => Math.min(RANKS.length - 1, Math.floor(xp / 1000));

const ls = {
  get(k) { try { return localStorage.getItem(k); } catch { return null; } },
  set(k, v) { try { v == null ? localStorage.removeItem(k) : localStorage.setItem(k, v); } catch { } },
};

class Account {
  constructor() { this.user = null; this.token = ls.get(TOKEN_KEY); this.offline = false; this.config = {}; }

  async call(method, path, body) {
    const r = await fetch('/api/' + path, {
      method, headers: { 'Content-Type': 'application/json', ...(this.token ? { Authorization: 'Bearer ' + this.token } : {}) },
      body: method === 'GET' ? undefined : JSON.stringify(body || {}),
    });
    let j = {}; try { j = await r.json(); } catch { }
    if (!r.ok) { const e = new Error(j.error || 'خطأ بالسيرفر'); e.status = r.status; throw e; }
    return j;
  }

  async serverUp() {
    try { this.config = await this.call('GET', 'config'); return true; } catch (e) { return !!e.status && e.status !== 404; }
  }

  // restore a saved session; returns the user or null
  async restore() {
    if (ls.get(OFFLINE_KEY) && !this.token) { this.useOffline(); return this.user; }
    if (!this.token) return null;
    try { this.user = (await this.call('GET', 'me')).user; return this.user; }
    catch (e) { if (e.status === 401) this.setToken(null); return null; }
  }
  setToken(t) { this.token = t; ls.set(TOKEN_KEY, t); }
  done(j) { this.setToken(j.token); this.user = j.user; this.offline = false; ls.set(OFFLINE_KEY, null); return this.user; }

  login(login, password) { return this.call('POST', 'login', { login, password }).then(j => this.done(j)); }
  register(username, email, password) { return this.call('POST', 'register', { username, email, password }).then(j => this.done(j)); }
  guest() { return this.call('POST', 'guest').then(j => this.done(j)); }
  google(credential) { return this.call('POST', 'google', { credential }).then(j => this.done(j)); }
  async logout() {
    if (!this.offline) try { await this.call('POST', 'logout'); } catch { }
    this.setToken(null); ls.set(OFFLINE_KEY, null); this.user = null; this.offline = false;
  }

  async linkEmail(email, password) { this.user = (await this.call('POST', 'link/email', { email, password })).user; }
  async linkGoogle(credential) { this.user = (await this.call('POST', 'link/google', { credential })).user; }
  async unlinkGoogle() { this.user = (await this.call('POST', 'unlink/google')).user; }
  async rename(username) {
    if (this.offline) { this.user.username = username; this.saveOffline(); return; }
    this.user = (await this.call('POST', 'account/name', { username })).user;
  }
  changePassword(old, password) { return this.call('POST', 'account/password', { old, password }); }
  leaderboard() { return this.call('GET', 'leaderboard'); }

  async saveSettings(settings) {
    if (!this.user) return;
    this.user.settings = settings;
    if (this.offline) return this.saveOffline();
    clearTimeout(this.saveT);
    this.saveT = setTimeout(() => this.call('PUT', 'settings', { settings }).catch(() => { }), 600);
  }

  async reportMatch(m) {
    if (!this.user) return null;
    if (this.offline) {
      if (m.map === 'training') return { xp: 0, tp: 0, coins: 0 };
      const u = this.user, st = u.stats, r = rewards(m);
      st.matches++; st.kills += m.kills; st.damage += Math.round(m.damage); st.xp += r.xp;
      if (m.place === 1) st.wins++; if (m.place <= 10) st.top10++;
      st.best = st.best ? Math.min(st.best, m.place) : m.place;
      const before = u.tp; u.tp = Math.max(0, u.tp + r.tp); u.coins += r.coins;
      this.saveOffline();
      return { xp: r.xp, tp: u.tp - before, coins: r.coins };
    }
    try { const j = await this.call('POST', 'match', m); this.user = j.user; return j; } catch { return null; }
  }

  async buy(id) {
    const it = itemById(id);
    if (this.offline) {
      if (this.user.coins < it.price) throw new Error('Not enough coins');
      this.user.coins -= it.price; this.user.owned.push(id); this.saveOffline(); return;
    }
    this.user = (await this.call('POST', 'shop/buy', { item: id })).user;
  }
  async equip(id) {
    const it = itemById(id);
    if (this.offline) { this.user.loadout[it.slot] = id; this.saveOffline(); return; }
    this.user = (await this.call('POST', 'shop/equip', { item: id })).user;
  }
  async saveLook(look) {
    if (this.offline) { this.user.look = look; this.saveOffline(); return; }
    this.user = (await this.call('PUT', 'look', { look })).user;
  }
  owns(id) { const it = itemById(id); return it && (it.price === 0 || this.user.owned.includes(id)); }

  // ---- offline profile ----
  useOffline(name) {
    let u = null; try { u = JSON.parse(ls.get(OFFLINE_KEY)); } catch { }
    if (!u) {
      const old = k => { try { return JSON.parse(ls.get('kryvon.' + k)) || 0; } catch { return 0; } };
      u = { id: 'local', username: name || 'Nashmi', guest: true, offline: true, settings: null,
        stats: { matches: old('matches'), wins: old('wins'), kills: old('kills'), xp: old('xp'), damage: 0, top10: 0, best: 0 } };
    }
    u.tp ??= 0; u.coins ??= 500; u.owned ??= []; u.loadout = { ...DEFAULT_LOADOUT, ...(u.loadout || {}) };
    this.user = u; this.offline = true; this.setToken(null); this.saveOffline();
    return u;
  }
  saveOffline() { ls.set(OFFLINE_KEY, JSON.stringify(this.user)); }
}

export const account = new Account();
