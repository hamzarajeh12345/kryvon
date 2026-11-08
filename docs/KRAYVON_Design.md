# KRAYVON – Design Document and Build Status

The owner's design for KRAYVON, collected from the planning messages, with what is built in the Unity version.
Status: ✅ built · 🟡 partly built · ⏳ next · 🔒 needs servers / accounts / money

Original-content rule: KRAYVON uses its own names, art and branding. Real-world weapon types are kept, but every weapon has
an original KRAYVON name. Local names (Jawad, Shas, Nashama, Petra, Amman…) are used freely.

---

## 1. Match flow and zone (Al-Tawq)

| Phase | Wait | Shrink | Damage / s | Status |
|---|---|---|---|---|
| Landing | 120 s (circle shows at 60 s) | – | – | ✅ |
| Zone 1 | 4 min | 3 min | 1 % | ✅ |
| Zone 2 | 3 min | 2 min | 3 % | ✅ |
| Zone 3 | 2 min | 90 s | 6 % | ✅ |
| Zone 4 | 90 s | 60 s | 10 % | ✅ |
| Final | 30 s | 45 s | 18–25 % (22) | ✅ |

Plane, parachute, win screen, results: ⏳ (built in the web version, not yet in Unity)

## 2. Map and POIs

| Item | Status |
|---|---|
| High tier (downtown cores, Petra, Jerash): armour 2–3, advanced ARs, snipers; vehicles 90 % | ✅ |
| Mid tier (town edges): armour 1–2, SMG + basic AR; vehicles 60 % | ✅ |
| Low tier (farms, lone houses): basic guns, light meds; vehicles 30 % | ✅ |
| 3 dense towns × 30 buildings (2–4 floors + 6–9 floor towers with stairs) | ✅ |
| Enterable houses: rooms, doors that open (F), breakable windows, balconies | ✅ |
| Petra (Siq + Al-Khazneh), Jerash (Cardo, theatre, arch) | ✅ |
| Full Bilad al-Sham map in Unity (Damascus, Jerusalem, Amman, Beirut, Dead Sea…) | ⏳ |
| Military base, comms towers (recall) | ⏳ |

## 3. Arsenal (22 weapons, original names)

| Class | Weapons | Ammo |
|---|---|---|
| AR | Zawba'a (airdrop), Shahin (airdrop), Saqr-4, Nimr-47, Burj-16 (burst), Jabal-36, Barq-762 | 5.56 green / 7.62 yellow |
| SMG | Zalzal-45, Asifa-V, Asifa, Tabl (50 drum) | .45 / 9mm blue |
| Sniper | Al-Malik .300 (airdrop, ignores armour), Al-Qannas 24, Al-Qannas 98 | .300 / 7.62 |
| DMR | Rami-14, Rami SKR, Sahm-14 | 7.62 / 5.56 |
| Shotgun | Ra'd 97 (pump), Ra'd S12 (auto), Ra'd DB (double) | 12G red |
| LMG | Jabal-249 (100), Sahn-28 (47 disc) | 5.56 / 7.62 |
| Pistol | Saif-19 | 9mm |

Status: ✅ stats, recoil character, ammo colours, models, airdrop-only flags · Attachments ⏳ · Weapon inspect ⏳

### Attachments (⏳ next)
Scopes Red Dot/Holo, 2x/3x, 4x/6x, 8x · Mags: extended, quickdraw, extended quickdraw · Muzzles: suppressor, compensator,
flash hider · Grips: vertical (vertical recoil), angled (ADS speed).

## 4. Gear

| Item | Spec | Status |
|---|---|---|
| Helmet 1/2/3 | −30 / 40 / 55 % head damage | ✅ |
| Vest 1/2/3 | −30 / 40 / 55 % body damage + storage | ✅ (storage ⏳) |
| Backpack 1/2/3 | capacity | 🟡 picked up, capacity ⏳ |

## 5. Healing and boost

| Key | Item | Effect | Time | Status |
|---|---|---|---|---|
| 4 | Bandage | +10 %, cap 75 % | 2 s | ✅ |
| 5 | First Aid | to 75 % | 6 s | ✅ |
| 6 | Med Kit | to 100 % | 8 s | ✅ |
| 7 | Energy Drink | +30 boost | 2 s | ✅ |
| 8 | Painkillers | +60 boost | 4 s | ✅ |
| 9 | Adrenaline | boost 100 | 6 s | ✅ |

Boost: heals over time, extra run speed above 60 %, drains in ~3 min. ✅

## 6. Throwables and deployables

| Item | Spec | Status |
|---|---|---|
| Frag | AoE, cook up to 5 s (G hold), walls block | ✅ |
| Molotov | fire area 8 s, damage over time | ✅ |
| Smoke | thick cover 25 s | ✅ |
| Flashbang | white-out + deafen 3–5 s | ✅ (player) |
| Spike strip (Y) | blows tyres, vehicle slows and wobbles | ✅ |
| Deployable shield (U) | steel cover with HP | ✅ |
| Flare gun | 5 per map, calls a supply drop | ✅ |

## 7. Stealth

| Item | Spec | Status |
|---|---|---|
| Levant ghillie suit (airdrop) | prone/crouched off-road: 85 % hidden | ✅ |
| Mud camouflage (H, 3 s) | earth colour 90 s, −30 % near cover | ✅ |
| Shadow cloak (X, device, 3 charges) | 70 % invisible 7 s, hum, breaks on fire/sprint | ✅ |
| Shadow masking | indoors + out of sun: −50 %, darker | ✅ |
| Weapon flashlight reveals | | ⏳ |
| Bots that use visibility | | ⏳ |

## 8. Vehicles

| Vehicle | Seats | Status |
|---|---|---|
| Jawad Armored | 4 | ✅ driveable, resists 80 % bullets |
| Shas Pickup | 4 (2 in the bed can fire) | ✅ driveable (bed shooting ⏳) |
| Buggy | 2 | ✅ |
| Tactical Dirt Bike | 2 | ✅ |

HP + explosions ✅ · spike tyres ✅ · shooting tyres ⏳ · drive-by ⏳ · fuel + gas cans ⏳ · passengers ⏳

## 9. Movement

Smooth stances, lean (Q/E) ✅ · Vault/mantle, slide ⏳ · Ragdoll + hit reaction ⏳ · Screen shake on explosions ✅

## 10. Death and recovery

Death animation, blood, kill feed, hit marker, grave with lootable crate ✅ · Knocked 60 s crawl + 10 s revive ⏳ ·
ID tags + recall towers ⏳

## 11. HUD

Health, boost, armour, consumables, weapon + ammo, zone timer, alive count, loot labels, visibility meter ✅ ·
Minimap with zone, plane path, pings, footstep/gunfire markers ⏳ · Squad panel ⏳ · Purple neon restyle per mock-up ⏳

## 12. Front end

Splash + lobby with hero turntable, modes, map, settings, loading ✅ ·
Tactical Command Center lobby (purple neon, ruined Amman skyline, holographic panels) ⏳ ·
Master Pass screen ⏳ · Armory (inspect + attach) ⏳ · Workshop (paint, decals, turret) ⏳ · Store ⏳ · Profile & career ⏳

## 13. Master Pass – Season 1 "Nashama – Tactical Origins"

- 100 tiers × 200 XP, about 90 days of moderate play. Free and Master tracks.
- Daily missions (10 XP, +25 for three): play a match downtown, kill with a DMR, drive 1,500 m in the Shas, use 3 energy drinks.
- Weekly (50 XP, +100 for five, don't expire): 10 kills in a high-tier zone, run over 3 with the Jawad, win as a squad,
  blow 5 tyres, play 2 h with a friend.
- Season (200 XP): 100 heavy-sniper kills, 30 matches, 15 revives, destroy 10 falling airdrops, 10 headshots past 150 m.
- Free track rewards: T10 desert cap · T20 SMG "Jabal Amman" · T30 Petra gloves · T40 Wadi Rum backpack · T50 mountain trousers ·
  T60 AR "old heritage" · T70 Citadel badge · T80 Dead Sea buggy · T90 "Nashama" dance · T100 Nashama core outfit.
- Master track: T1 commander "Ahmad" · T10 Saqr-4 Nashama · T20 Jawad elite · T30 Jabal-36 neon · T40 Nashama backpack L3 ·
  T50 Al-Qannas 24 Nashama · T60 Shas mountain recon · T70 victory emote · T80 Al-Malik .300 Nashama · T90 elite mountain outfit ·
  T100 legendary "The Origin" outfit.
- Evolving weapon skin "Burial Ground" (Saqr-4): Base → Initial Engraving → Purple Rage → Elite Origins → Finale with kill effect.
- Premium currency refunded around tier 90–100.

Status: ⏳ (UI, missions tracking, XP and rewards are buildable offline; real-money purchases 🔒)

## 14. Economy, ranked, live ops

Soft + hard currency (display ✅, earning/spending ⏳) · Ranked tiers Bronze → Legendary ⏳ · Daily login calendar ⏳ ·
Friends / synergy titles 🔒 · Reports 🔒 · Analytics 🔒

## 15. Audio

Footsteps by surface ✅ · 3D positional sound ✅ (Unity default) · Occlusion through floors/walls ⏳ · Loud vehicle engines ✅ ·
Announcer (zone, airdrop, teammate down) ⏳ · Kill-streak callouts ⏳ · Quick chat in local dialect ⏳ · Voice chat 🔒

## 16. Mobile and accessibility

Touch layout editor (2/3/4 finger) ⏳ · Gyro aiming (code exists in the camera) 🟡 · Per-scope gyro sensitivity 🟡 ·
Auto-loot ⏳ · Aim assist levels ⏳ · Haptics ⏳

## 17. Performance

LOD + dynamic resolution ⏳ · Streaming ⏳ · Shader pre-warm ⏳ — measure on target devices first.

## 18. Esports and custom rooms

Observer camera ⏳ · Custom rooms with codes 🔒 (needs servers)

## 19. Weather and time

Dust storm, overcast, sunset during the final zone ⏳ (exists in the web version)

## 20. Online

Matchmaking, dedicated servers, anti-cheat, proximity voice: 🔒 a separate project with hosting costs.
