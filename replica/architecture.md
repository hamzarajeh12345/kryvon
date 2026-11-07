# KRYVON – architecture

**Stack in one line:** browser game in three.js (ES modules, no build step) + one Node server (`server.js`, no npm
dependencies) + SQLite via `node:sqlite`; realtime multiplayer later through a WebSocket game server written in the same JS.

## 1. Stack
| layer | choice | why |
|---|---|---|
| client / engine | three.js 0.160 in the browser | already built; runs on phones and PCs from one URL, no installs, no store review |
| mobile app | wrap the same page with Capacitor when we ship to stores | one codebase; store builds later |
| server | Node 24, plain `http` | one process serves files + API; nothing to install |
| database | SQLite (`node:sqlite`), schema in `schema.sql` | zero setup, single file, transactions; the schema is Postgres-compatible for when we host it |
| auth | own email+password (scrypt) and Google ID tokens; bearer session tokens in `sessions` | no third-party account vendor; guests can link later |
| hosting | any VM / Fly / Render with a persistent disk | SQLite needs a disk; move to Postgres (Neon/Supabase) if we run more than one server |
| realtime (later) | WebSocket server in Node (`ws` package) | browsers can't do raw UDP; WebSocket is the practical choice for a web game |

**Why not Unreal Engine 5:** it would mean throwing away the working game and rebuilding from zero in C++/Blueprints,
plus native builds per platform. Worth it only for a studio-sized team and budget. three.js keeps us shipping weekly.

## 2. Schema
`replica/schema.sql` – 8 tables: users, sessions, matches, match_players, user_items, user_loadout, parties, party_members.
- Ownership: every user-owned row has `user_id` with `on delete cascade`; every query is filtered by the session user in `server.js`.
- Stats (matches, wins, kills, top-10, best) are **computed** from `match_players`, never stored twice.
- Coins/tier points change only inside one transaction with the match row; purchases use `update … where coins >= price` so a double-click can't overspend.
- Cosmetics catalogue + tier/reward rules live in `src/progression.js`, shared by client and server so both compute the same numbers; the server's result is the one saved.

## 3. API (all JSON, `Authorization: Bearer <token>`)
| route | does | who |
|---|---|---|
| GET /api/config | is Google sign-in on | anyone |
| POST /api/register, /login, /guest, /google | create session | anyone (login rate-limited 10/10 min/IP) |
| GET /api/me · POST /api/logout | profile · end session | signed in |
| POST /api/link/email, /link/google, /unlink/google | attach sign-in methods to the same account | signed in |
| POST /api/account/name, /account/password | rename · change password (signs out other devices) | signed in |
| PUT /api/settings | save settings JSON (≤ 8 KB) | signed in |
| POST /api/match | record a finished match → xp, tier points, coins | signed in |
| GET /api/history · GET /api/leaderboard | last 20 matches · top 20 by tier points | signed in · anyone |
| POST /api/shop/buy, /shop/equip | buy with coins · equip per slot | signed in |

## 4. Realtime multiplayer (milestone 4 – not built yet)
Today every match runs **in the player's browser with bots**; teammates are bots. To put real people in one match:
1. **Matchmaker** (in `server.js`): parties → queue per (map, mode) → fill a match up to N humans, top up with server-side bots.
2. **Match server**: one Node worker per match, authoritative, 20–30 Hz tick. Clients send *inputs* (move axes, yaw/pitch, fire, use), never positions or damage.
3. **Client prediction + reconciliation** for the local player; other players are interpolated ~100 ms behind; hits use server-side lag compensation (rewind positions by the shooter's ping).
4. **Transport**: WebSocket with binary messages (DataView), delta-compressed snapshots, area-of-interest culling (only send actors within ~400 m).
5. **Voice**: WebRTC peer-to-peer inside a squad, signalling over the same WebSocket.
6. **Anti-cheat basics**: server validates fire rate, ammo, line of sight and movement speed; results are written by the match server, not the client.
Hosting: one VM handles several matches (bots are cheap); regions added when players exist outside one area.

## 5. Parts that bite
- **Trusting the client:** until §4 exists, `POST /api/match` trusts the browser – fine for a prototype, not for ranked rewards with real money. Never sell coins before the match server exists.
- **SQLite + many servers:** one writer only; switch to Postgres before running more than one server process.
- **Phones:** GPU budget – keep the "Low" preset honest; test on a mid-range Android every milestone.
- **GDPR:** deleting a user cascades to sessions, matches rows, items and loadout (add a delete-account button before launch).

## 6. Build order
1. ✅ Vertical slice: sign in → lobby → match → results saved.
2. ▶ This milestone: modes + bot squads with knock/revive, queue + waiting area, backpack + attachments, quick chat & pings, tiers/coins/results, wardrobe/shop with character preview.
3. Vehicles (original designs), smoke/stun throwables.
4. Realtime multiplayer (§4), parties, voice.
5. Store builds (Capacitor) and launch.
