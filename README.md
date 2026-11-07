# KRYVON – كرايفون

An original browser battle royale set in Bilad Al-Sham (the Levant): Damascus, Beirut, Aleppo, Al-Quds, Amman, Petra and more,
with landmarks, squads with knock/revive, a character creator, attachments, tiers and a coin shop.

## Run
Needs Node.js 22.5 or newer (uses the built-in `node:sqlite`).

```
npm install
npm start
```
Open http://localhost:8080 (phones on the same Wi-Fi: `http://<your-pc-ip>:8080`).

Optional environment variables: `PORT`, `GOOGLE_CLIENT_ID` (enables Google sign-in), `DB_FILE`.

## Layout
- `index.html` – page, styles and HUD markup
- `src/` – game (three.js ES modules): world, landmarks, actors, bots, UI, character builder
- `server.js` – static files + account / match / shop API (SQLite in `data/`, not committed)
- `replica/` – product map, architecture and database schema

Matches currently run in the browser with bots; online multiplayer is planned (see `replica/architecture.md`).
