# KRYVON – product map (recon)

KRYVON is an original battle royale set in Bilad Al-Sham. This map lists **our** screens, flows and data,
written from the genre checklist the owner supplied and the code already in this repo. It does not describe
or copy any other game's internals, art, names or content.

## Screens
| id | screen | purpose | status |
|----|--------|---------|--------|
| S01 | Auth | sign in, sign up, play as guest, Google sign-in | built |
| S02 | Lobby | character preview, stats, coins, tier, mode (solo/duo/squad), map card, Start | partial → this milestone |
| S03 | Map picker | 4 Levant areas + training grounds | built |
| S04 | Wardrobe / Shop | buy & equip outfits, scarves, parachute and weapon skins with coins | this milestone |
| S05 | Settings | graphics, per-scope sensitivity, gyro, controls, key binds, audio | built |
| S06 | Account | link email / Google, rename, password, sign out | built |
| S07 | Leaderboard | top players by tier points | built (switch to tier points) |
| S08 | Queue | "finding players" while the match is assembled | this milestone (bots until netcode) |
| S09 | Waiting area | free-roam staging ground with countdown before the plane | this milestone |
| S10 | In-game HUD | minimap, compass, ring timer, team bars, touch controls, contextual buttons | built + team bars |
| S11 | Backpack | weapons with attachment slots, armor, consumables; use / drop | this milestone |
| S12 | Quick chat & pings | canned team messages and world markers | this milestone |
| S13 | Results | place, kills, damage, time, XP, tier points ±, coins earned, team table | this milestone |
| S14 | Pause | resume, settings, leave | built |

## Flows
- **F1 Matchmaking:** pick mode + map → Start → queue (S08) → waiting area (S09, 20 s) → plane.
- **F2 Survival loop:** jump → land → loot (S11) → stay inside Al-Tawq → fight → knock / revive (team modes) → last team standing → results (S13).
- **F3 Progression:** match result → XP, tier points, coins → spend coins in S04 → equipped look shows in lobby and in matches.
- **F4 Account:** guest → link email or Google (S06) without losing progress.

## Data model (inferred from the flows)
users (level/xp, tier points, coins) · sessions · matches · match_players · cosmetics catalogue (static, in code) ·
user_items (owned) · user_loadout (equipped per slot) · parties + party_members (for online squads, later).

## Must-haves
Smooth touch controls with contextual buttons · shrinking zone with timer · loot + backpack management ·
team play with knock/revive · quick chat & pings · tiers and results · cosmetics with soft currency.

## Explicitly later
Real online multiplayer (players from different devices in one match) and voice chat need a realtime game
server – see architecture.md §4. Vehicles are the next gameplay milestone.
