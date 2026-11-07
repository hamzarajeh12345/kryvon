// KRYVON server: static files + account / progression API. No npm dependencies:
// SQLite comes from node:sqlite (Node 22.5+), passwords use scrypt, sessions are random tokens.
// `npm start`, then open http://localhost:8080 (phones on the same Wi-Fi: http://<pc-ip>:8080)
// Optional env: PORT, GOOGLE_CLIENT_ID (enables Google sign-in / linking), DB_FILE.
process.removeAllListeners('warning'); // hide the node:sqlite "experimental" banner
const http = require('http'), fs = require('fs'), path = require('path'), crypto = require('crypto');
const { DatabaseSync } = require('node:sqlite');

const ROOT = __dirname, PORT = process.env.PORT || 8080;
const DATA_DIR = path.join(ROOT, 'data');
const DB_FILE = process.env.DB_FILE || path.join(DATA_DIR, 'kryvon.db');
const GOOGLE_CLIENT_ID = process.env.GOOGLE_CLIENT_ID || '';
const SESSION_DAYS = 60;
const TYPES = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon' };
const STATIC_BLOCK = [/^\/data\//, /^\/server\.js$/, /^\/\.git/, /^\/package/, /^\/replica\//];

let P, LK; // shared progression + look rules (ESM modules, loaded at startup)

// ---------------- database ----------------
fs.mkdirSync(path.dirname(DB_FILE), { recursive: true });
const db = new DatabaseSync(DB_FILE);
db.exec('pragma journal_mode = wal; pragma foreign_keys = on;');
db.exec(fs.readFileSync(path.join(ROOT, 'replica', 'schema.sql'), 'utf8'));
if (!db.prepare("select 1 from pragma_table_info('users') where name = 'look'").get()) db.exec('alter table users add column look text');
const now = () => new Date().toISOString();
const q = (sql) => { const s = db.prepare(sql); return { get: (...a) => s.get(...a), all: (...a) => s.all(...a), run: (...a) => s.run(...a) }; };
const tx = (f) => { db.exec('begin'); try { const r = f(); db.exec('commit'); return r; } catch (e) { db.exec('rollback'); throw e; } };

// one-time import from the earlier JSON store
(function migrateJson() {
  const old = path.join(DATA_DIR, 'db.json');
  if (!fs.existsSync(old) || q('select count(*) n from users').get().n > 0) return;
  try {
    const j = JSON.parse(fs.readFileSync(old, 'utf8'));
    tx(() => { for (const u of Object.values(j.users || {})) {
      q(`insert or ignore into users (id, username, email, pass_hash, pass_salt, google_sub, google_email, xp, settings, created_at)
         values (?,?,?,?,?,?,?,?,?,?)`).run(u.id, u.username, u.email || null, u.hash || null, u.salt || null,
        u.google ? u.google.sub : null, u.google ? u.google.email : null, (u.stats && u.stats.xp) || 0,
        u.settings ? JSON.stringify(u.settings) : null, new Date(u.created || Date.now()).toISOString());
    } });
    fs.renameSync(old, old + '.imported');
    console.log('Imported accounts from db.json');
  } catch (e) { console.error('db.json import failed:', e.message); }
})();

const newId = () => crypto.randomUUID();
function hashPass(pw, salt = crypto.randomBytes(16).toString('hex')) {
  return { pass_salt: salt, pass_hash: crypto.scryptSync(pw, salt, 64).toString('hex') };
}
function checkPass(pw, u) {
  if (!u.pass_hash) return false;
  return crypto.timingSafeEqual(crypto.scryptSync(pw, u.pass_salt, 64), Buffer.from(u.pass_hash, 'hex'));
}
const byId = id => q('select * from users where id = ?').get(id);
const byLogin = s => q('select * from users where email = ? or username = ?').get(String(s), String(s));

function stats(uid) {
  return q(`select count(*) matches, coalesce(sum(case when place = 1 then 1 else 0 end),0) wins,
            coalesce(sum(kills),0) kills, coalesce(sum(damage),0) damage,
            coalesce(sum(case when place <= 10 then 1 else 0 end),0) top10, coalesce(min(place),0) best
            from match_players where user_id = ?`).get(uid);
}
function owned(uid) { return q('select item_id from user_items where user_id = ?').all(uid).map(r => r.item_id); }
function loadout(uid) {
  const out = { ...P.DEFAULT_LOADOUT };
  for (const r of q('select slot, item_id from user_loadout where user_id = ?').all(uid)) out[r.slot] = r.item_id;
  return out;
}
function publicUser(u) {
  const st = stats(u.id);
  return {
    id: u.id, username: u.username, email: u.email || null, guest: !u.email && !u.google_sub,
    google: u.google_email || null, created: u.created_at, settings: u.settings ? JSON.parse(u.settings) : null,
    stats: { ...st, xp: u.xp }, tp: u.tier_points, coins: u.coins, look: u.look ? JSON.parse(u.look) : null,
    owned: owned(u.id), loadout: loadout(u.id),
  };
}
function newSession(u) {
  const token = crypto.randomBytes(32).toString('base64url');
  q('insert into sessions (token, user_id, expires_at) values (?,?,?)').run(token, u.id, new Date(Date.now() + SESSION_DAYS * 864e5).toISOString());
  return token;
}
function createUser(f) {
  const id = newId();
  q(`insert into users (id, username, email, pass_hash, pass_salt, google_sub, google_email) values (?,?,?,?,?,?,?)`)
    .run(id, f.username, f.email || null, f.pass_hash || null, f.pass_salt || null, f.google_sub || null, f.google_email || null);
  return byId(id);
}
const touch = id => q('update users set updated_at = ? where id = ?').run(now(), id);

const USER_RE = /^[\p{L}\p{N}_.-]{3,16}$/u;
const EMAIL_RE = /^[^\s@]{1,64}@[^\s@]{1,190}\.[^\s@]{2,}$/;
function validate({ username, email, password }, need) {
  if (need.includes('username') && !USER_RE.test(username || '')) return 'Username: 3–16 letters, numbers, _ . -';
  if (need.includes('email') && !EMAIL_RE.test(email || '')) return 'Invalid email';
  if (need.includes('password') && (typeof password !== 'string' || password.length < 6 || password.length > 128)) return 'Password must be 6–128 characters';
  return null;
}

const attempts = new Map(); // login rate limit: 10 / 10 min per IP
function limited(ip) {
  const t = Date.now(), a = (attempts.get(ip) || []).filter(x => t - x < 600000);
  a.push(t); attempts.set(ip, a);
  return a.length > 10;
}

async function verifyGoogle(credential) {
  if (!GOOGLE_CLIENT_ID) throw new Error('Google sign-in is not configured on this server');
  const r = await fetch('https://oauth2.googleapis.com/tokeninfo?id_token=' + encodeURIComponent(credential));
  const t = await r.json();
  if (!r.ok || t.aud !== GOOGLE_CLIENT_ID || !['accounts.google.com', 'https://accounts.google.com'].includes(t.iss)) throw new Error('Google token rejected');
  return { sub: t.sub, email: t.email, name: t.given_name || t.name || '' };
}
function uniqueName(base) {
  base = (base || 'Nashmi').replace(/[^\p{L}\p{N}_.-]/gu, '').slice(0, 12) || 'Nashmi';
  if (base.length < 3) base += '_JO';
  let n = base;
  while (byLogin(n)) n = base.slice(0, 11) + Math.floor(1000 + Math.random() * 9000);
  return n;
}

// ---------------- API ----------------
async function api(req, route, body) {
  const ip = req.socket.remoteAddress || '';
  const auth = (req.headers.authorization || '').replace(/^Bearer /, '');
  const sess = auth ? q('select * from sessions where token = ?').get(auth) : null;
  const me = sess && sess.expires_at > now() ? byId(sess.user_id) : null;
  const need = () => { if (!me) throw { code: 401, msg: 'Please sign in again' }; };
  const done = u => ({ token: newSession(u), user: publicUser(u) });

  switch (route) {
    case 'GET /api/config': return { google: GOOGLE_CLIENT_ID || null };

    case 'POST /api/register': {
      const err = validate(body, ['username', 'email', 'password']); if (err) throw { msg: err };
      if (q('select 1 from users where username = ?').get(body.username)) throw { msg: 'Username is taken' };
      if (q('select 1 from users where email = ?').get(body.email)) throw { msg: 'An account with this email already exists' };
      return done(createUser({ username: body.username, email: body.email.toLowerCase(), ...hashPass(body.password) }));
    }
    case 'POST /api/login': {
      if (limited(ip)) throw { code: 429, msg: 'Too many attempts – wait a few minutes' };
      const u = byLogin(body.login || '');
      if (!u || !checkPass(String(body.password || ''), u)) throw { msg: 'Wrong username/email or password' };
      return done(u);
    }
    case 'POST /api/guest': return done(createUser({ username: uniqueName('Guest' + Math.floor(Math.random() * 1e4)) }));
    case 'POST /api/google': {
      const g = await verifyGoogle(body.credential);
      const u = q('select * from users where google_sub = ?').get(g.sub) || createUser({ username: uniqueName(g.name), google_sub: g.sub, google_email: g.email });
      return done(u);
    }
    case 'GET /api/me': need(); return { user: publicUser(me) };
    case 'POST /api/logout': if (auth) q('delete from sessions where token = ?').run(auth); return { ok: true };

    case 'POST /api/link/email': {
      need();
      if (me.email) throw { msg: 'This account already has an email' };
      const err = validate(body, ['email', 'password']); if (err) throw { msg: err };
      if (q('select 1 from users where email = ?').get(body.email)) throw { msg: 'That email belongs to another account' };
      const h = hashPass(body.password);
      q('update users set email = ?, pass_hash = ?, pass_salt = ?, updated_at = ? where id = ?').run(body.email.toLowerCase(), h.pass_hash, h.pass_salt, now(), me.id);
      return { user: publicUser(byId(me.id)) };
    }
    case 'POST /api/link/google': {
      need();
      const g = await verifyGoogle(body.credential);
      const other = q('select id from users where google_sub = ?').get(g.sub);
      if (other && other.id !== me.id) throw { msg: 'That Google account is linked to another player' };
      q('update users set google_sub = ?, google_email = ?, updated_at = ? where id = ?').run(g.sub, g.email, now(), me.id);
      return { user: publicUser(byId(me.id)) };
    }
    case 'POST /api/unlink/google': {
      need();
      if (!me.email) throw { msg: 'Add an email + password first, or you would lose access' };
      q('update users set google_sub = null, google_email = null, updated_at = ? where id = ?').run(now(), me.id);
      return { user: publicUser(byId(me.id)) };
    }
    case 'POST /api/account/name': {
      need();
      const err = validate(body, ['username']); if (err) throw { msg: err };
      const o = q('select id from users where username = ?').get(body.username);
      if (o && o.id !== me.id) throw { msg: 'Username is taken' };
      q('update users set username = ?, updated_at = ? where id = ?').run(body.username, now(), me.id);
      return { user: publicUser(byId(me.id)) };
    }
    case 'POST /api/account/password': {
      need();
      if (me.pass_hash && !checkPass(String(body.old || ''), me)) throw { msg: 'Current password is wrong' };
      const err = validate({ password: body.password }, ['password']); if (err) throw { msg: err };
      const h = hashPass(body.password);
      q('update users set pass_hash = ?, pass_salt = ?, updated_at = ? where id = ?').run(h.pass_hash, h.pass_salt, now(), me.id);
      q('delete from sessions where user_id = ? and token != ?').run(me.id, auth); // sign out other devices
      return { ok: true };
    }
    case 'PUT /api/settings': {
      need();
      const s = JSON.stringify(body.settings || {});
      if (s.length > 8000) throw { msg: 'Settings too large' };
      q('update users set settings = ?, updated_at = ? where id = ?').run(s, now(), me.id);
      return { ok: true };
    }

    case 'PUT /api/look': {
      need();
      const l = JSON.stringify(LK.sanitizeLook(body.look));
      q('update users set look = ?, updated_at = ? where id = ?').run(l, now(), me.id);
      return { user: publicUser(byId(me.id)) };
    }
    case 'POST /api/match': {
      need();
      const n = (v, lo, hi) => Math.max(lo, Math.min(hi, Math.round(Number(v) || 0)));
      if (!['sham', 'north', 'heart', 'south'].includes(body.map)) return { user: publicUser(me), xp: 0, tp: 0, coins: 0 }; // training etc.
      const mode = ['solo', 'duo', 'squad'].includes(body.mode) ? body.mode : 'solo';
      const total = n(body.total, 2, 100), place = n(body.place, 1, 100), kills = n(body.kills, 0, 99);
      const r = P.rewards({ place, total, kills, mode });
      const mid = newId();
      const newTp = Math.max(0, me.tier_points + r.tp);
      tx(() => {
        q('insert into matches (id, map_id, mode, player_count, winner_team, duration_s) values (?,?,?,?,?,?)')
          .run(mid, body.map, mode, total, r.win ? n(body.team, 0, 100) : null, n(body.time, 0, 7200));
        q(`insert into match_players (match_id, user_id, team, place, kills, damage, survived_s, xp_gain, tp_delta, coins_gain)
           values (?,?,?,?,?,?,?,?,?,?)`).run(mid, me.id, n(body.team, 0, 100), place, kills, n(body.damage, 0, 20000), n(body.time, 0, 7200), r.xp, newTp - me.tier_points, r.coins);
        q('update users set xp = xp + ?, tier_points = ?, coins = coins + ?, updated_at = ? where id = ?').run(r.xp, newTp, r.coins, now(), me.id);
      });
      return { user: publicUser(byId(me.id)), xp: r.xp, tp: newTp - me.tier_points, coins: r.coins };
    }
    case 'GET /api/history': {
      need();
      return { matches: q(`select m.map_id map, m.mode, m.player_count total, mp.place, mp.kills, mp.damage, mp.tp_delta tp, m.created_at at
        from match_players mp join matches m on m.id = mp.match_id where mp.user_id = ? order by m.created_at desc limit 20`).all(me.id) };
    }
    case 'GET /api/leaderboard': {
      const top = q(`select u.username, u.tier_points tp, u.xp, count(mp.match_id) matches,
          coalesce(sum(case when mp.place = 1 then 1 else 0 end),0) wins, coalesce(sum(mp.kills),0) kills
        from users u join match_players mp on mp.user_id = u.id group by u.id order by u.tier_points desc, u.xp desc limit 20`).all();
      return { top };
    }

    case 'POST /api/shop/buy': {
      need();
      const it = P.itemById(body.item);
      if (!it) throw { msg: 'Unknown item' };
      if (it.price === 0 || owned(me.id).includes(it.id)) return { user: publicUser(me) };
      tx(() => {
        const c = q('update users set coins = coins - ?, updated_at = ? where id = ? and coins >= ?').run(it.price, now(), me.id, it.price);
        if (!c.changes) throw { msg: 'Not enough coins' };
        q('insert into user_items (user_id, item_id) values (?,?)').run(me.id, it.id);
      });
      return { user: publicUser(byId(me.id)) };
    }
    case 'POST /api/shop/equip': {
      need();
      const it = P.itemById(body.item);
      if (!it) throw { msg: 'Unknown item' };
      if (it.price > 0 && !owned(me.id).includes(it.id)) throw { msg: 'You do not own this item' };
      q('insert into user_loadout (user_id, slot, item_id) values (?,?,?) on conflict (user_id, slot) do update set item_id = excluded.item_id').run(me.id, it.slot, it.id);
      return { user: publicUser(byId(me.id)) };
    }
  }
  throw { code: 404, msg: 'Unknown endpoint' };
}

function readBody(req) {
  return new Promise((ok, fail) => {
    let s = '';
    req.on('data', c => { s += c; if (s.length > 20000) { fail({ code: 413, msg: 'Body too large' }); req.destroy(); } });
    req.on('end', () => { try { ok(s ? JSON.parse(s) : {}); } catch { fail({ msg: 'Invalid JSON' }); } });
  });
}

const server = http.createServer(async (req, res) => {
  const url = req.url.split('?')[0];
  if (url.startsWith('/api/')) {
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Cache-Control', 'no-store');
    try {
      const body = req.method === 'GET' ? {} : await readBody(req);
      const out = await api(req, `${req.method} ${url}`, body);
      res.writeHead(200); res.end(JSON.stringify(out));
    } catch (e) {
      if (!e || !e.msg) console.error(e);
      res.writeHead((e && e.code) || 400); res.end(JSON.stringify({ error: (e && (e.msg || e.message)) || 'Server error' }));
    }
    return;
  }
  let p;
  try { p = decodeURIComponent(url); } catch { res.writeHead(400); return res.end(); }
  if (p === '/') p = '/index.html';
  if (STATIC_BLOCK.some(r => r.test(p))) { res.writeHead(404); return res.end('Not found'); }
  const file = path.normalize(path.join(ROOT, p));
  if (!file.startsWith(ROOT + path.sep)) { res.writeHead(403); return res.end(); }
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404); return res.end('Not found'); }
    res.writeHead(200, { 'Content-Type': TYPES[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
    res.end(data);
  });
});

Promise.all([import('./src/progression.js'), import('./src/look.js')]).then(([m, l]) => {
  P = m; LK = l;
  server.listen(PORT, '0.0.0.0', () => console.log(`KRYVON running at http://localhost:${PORT}` + (GOOGLE_CLIENT_ID ? ' (Google sign-in on)' : '')));
});

setInterval(() => q('delete from sessions where expires_at < ?').run(now()), 3600e3).unref();
