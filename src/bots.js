import { HALF } from './config.js';

const wrapA = a => { while (a > Math.PI) a -= Math.PI * 2; while (a < -Math.PI) a += Math.PI * 2; return a; };
const gaussR = () => (Math.random() + Math.random() + Math.random() - 1.5) / 1.5;

export function initBot(b, skillScale) {
  b.ai = {
    think: Math.random(), state: 'loot', target: null, goal: null, goalLoot: null, goalCrate: null,
    react: 0, burst: 0, pause: 0, strafe: Math.random() < 0.5 ? -1 : 1, strafeT: 0,
    stuckT: 0, lastX: 0, lastZ: 0, detour: null, detourT: 0, distract: null, distractT: 0, heardT: 0, heardFrom: null,
    skill: (0.25 + Math.random() * 0.5) * skillScale,
  };
}

function canSee(g, b, t) {
  const e = b.eye(), te = t.eye();
  const dx = te.x - e.x, dy = te.y - e.y, dz = te.z - e.z, d = Math.hypot(dx, dy, dz);
  if (d < 0.5) return true;
  return g.world.raycast(e.x, e.y, e.z, dx / d, dy / d, dz / d, d, 3) === Infinity;
}

function think(g, b) {
  const ai = b.ai, ring = g.ring, w = b.weapon;
  // --- target acquisition ---
  const range = w ? Math.min(w.def.range, w.def.cls === 'SNIPER' || w.def.cls === 'DMR' ? 260 : 170) : 25;
  let best = null, bd = range;
  for (const o of g.actors) {
    if (o === b || !o.alive || o.state !== 'ground' || o.team === b.team) continue;
    const dx = o.pos.x - b.pos.x, dz = o.pos.z - b.pos.z;
    if (Math.abs(dx) > bd || Math.abs(dz) > bd) continue;
    const d = Math.hypot(dx, dz);
    if (d >= bd) continue;
    const ang = Math.abs(wrapA(Math.atan2(-dx, -dz) - b.yaw));
    const aware = ang < 1.1 || d < 12 || o === ai.target || (ai.heardT > 0 && ai.heardFrom === o);
    if (!aware && Math.random() < 0.75) continue;
    if (o.isPlayer && g.playerHidden && d > 8) continue;
    if (!canSee(g, b, o)) continue;
    best = o; bd = d;
  }
  if (best && best !== ai.target) {
    ai.react = 0.35 + (1 - ai.skill) * 0.7;
    if (ai.mate && g.callout) g.callout(b, best);
  }
  ai.target = best;

  const hasAmmo = w && (w.mag > 0 || b.ammo[w.def.ammo] > 0);
  const nextR = ring.next || ring.cur;
  const outsideCur = !ring.inside(b.pos.x, b.pos.z);
  const outsideNext = Math.hypot(b.pos.x - nextR.x, b.pos.z - nextR.z) > nextR.r * 0.92;
  const mustRotate = outsideCur || (outsideNext && ring.state === 'wait' && ring.timer < 30) || (outsideNext && ring.state === 'shrink');

  if (ai.target && hasAmmo && !(outsideCur && b.hp < 50)) { ai.state = 'fight'; return; }
  // squad play: revive a knocked teammate, otherwise stay with the leader
  if (g.teamSize > 1 && !ai.target) {
    const mates = g.teams[b.team] || [];
    const down = mates.find(m => m !== b && m.alive && m.knocked && Math.hypot(m.pos.x - b.pos.x, m.pos.z - b.pos.z) < 90 && !mates.some(o => o !== b && o.action && o.action.type === 'revive' && o.action.target === m));
    if (down) { ai.state = 'revive'; ai.goal = { x: down.pos.x, z: down.pos.z }; ai.goalType = 'revive'; ai.reviveT = down; return; }
    const L = ai.order && ai.orderT > g.time ? ai.order : null;
    if (L) { ai.state = 'follow'; ai.goal = { x: L.x, z: L.z }; ai.goalType = 'order'; if (Math.hypot(L.x - b.pos.x, L.z - b.pos.z) < 6) ai.order = null; return; }
    const lead = mates.find(m => m.alive && m !== b && (m.isPlayer || m === mates.find(x => x.alive && !x.isPlayer)));
    if (lead && lead !== b && !mustRotate) {
      const d = Math.hypot(lead.pos.x - b.pos.x, lead.pos.z - b.pos.z), far = ai.mate ? 22 : 45;
      if (d > far) {
        const i = mates.indexOf(b), a = i * 2.1;
        ai.state = 'follow'; ai.goal = { x: lead.pos.x + Math.cos(a) * 5, z: lead.pos.z + Math.sin(a) * 5 }; ai.goalType = 'follow'; return;
      }
    }
  }
  // switch to the other weapon if this one is dry
  if (w && !hasAmmo) { const o = b.weapons[1 - b.cur]; if (o && (o.mag > 0 || b.ammo[o.def.ammo] > 0)) { b.cur = 1 - b.cur; b.action = null; b.refreshLook(); } }
  if (ai.target && !hasAmmo) {
    ai.state = 'flee';
    const dx = b.pos.x - ai.target.pos.x, dz = b.pos.z - ai.target.pos.z, d = Math.hypot(dx, dz) || 1;
    ai.goal = { x: b.pos.x + dx / d * 30, z: b.pos.z + dz / d * 30 };
    return;
  }
  if (mustRotate) {
    ai.state = 'rotate';
    if (!ai.goal || ai.goalType !== 'rotate' || Math.hypot(ai.goal.x - nextR.x, ai.goal.z - nextR.z) > nextR.r) {
      const p = g.world.randomLandPoint(Math.random, nextR.x, nextR.z, Math.max(3, nextR.r * 0.6));
      ai.goal = p; ai.goalType = 'rotate';
    }
    return;
  }
  // heal up when safe
  if (!b.action) {
    const h = b.hp < 70 ? b.bestHeal() : null;
    if (h) { b.startUse(h); return; }
    if (b.boost < 40 && b.hp < 95) { const k = b.bestBoost(); if (k) { b.startUse(k); return; } }
    if (w && w.mag < w.def.mag * 0.5) b.startReload();
  }
  if (ai.distractT > 0 && ai.distract) { ai.state = 'investigate'; ai.goal = ai.distract; ai.goalType = 'distract'; return; }
  // loot
  if (ai.goalLoot && !ai.goalLoot.alive) ai.goalLoot = null;
  if (!ai.goalLoot) ai.goalLoot = g.loot.near(b.pos.x, b.pos.z, w ? 45 : 90, e => b.wants(e.it));
  if (ai.goalLoot) { ai.state = 'loot'; ai.goal = { x: ai.goalLoot.x, z: ai.goalLoot.z }; ai.goalType = 'loot'; return; }
  // crates: death boxes nearby, airdrops from further away
  if (ai.goalCrate && !g.loot.crates.includes(ai.goalCrate)) ai.goalCrate = null;
  if (!ai.goalCrate) for (const c of g.loot.crates) {
    if (!c.landed) continue;
    const d = Math.hypot(c.x - b.pos.x, c.z - b.pos.z);
    if ((c.kind === 'death' && d < 50 && c.items.some(it => b.wants(it))) || (c.kind !== 'death' && d < 260 && w && Math.random() < 0.4)) { ai.goalCrate = c; break; }
  }
  if (ai.goalCrate) { ai.state = 'crate'; ai.goal = { x: ai.goalCrate.x, z: ai.goalCrate.z }; ai.goalType = 'crate'; return; }
  // roam inside the next circle
  ai.state = 'roam';
  if (!ai.goal || ai.goalType !== 'roam' || Math.hypot(ai.goal.x - b.pos.x, ai.goal.z - b.pos.z) < 3) {
    const c = ring.next || ring.cur;
    ai.goal = g.world.randomLandPoint(Math.random, c.x, c.z, Math.max(3, Math.min(c.r * 0.8, 400)));
    ai.goalType = 'roam';
  }
}

export function updateBot(g, b, dt) {
  const ai = b.ai;
  if (b.knocked) { // crawl toward the closest standing teammate
    const m = (g.teams[b.team] || []).find(o => o !== b && o.alive && !o.knocked);
    let mx = 0, mz = 0;
    if (m) { const dx = m.pos.x - b.pos.x, dz = m.pos.z - b.pos.z, d = Math.hypot(dx, dz); if (d > 2) { mx = dx / d; mz = dz / d; b.yaw = Math.atan2(-mx, -mz); } }
    b.move(mx, mz, dt, false, false);
    return;
  }
  if (b.action && b.action.type === 'revive') { b.move(0, 0, dt, false, false); return; }
  if (ai.distractT > 0) ai.distractT -= dt;
  if (ai.heardT > 0) ai.heardT -= dt;
  const dP = Math.hypot(b.pos.x - g.player.pos.x, b.pos.z - g.player.pos.z);
  ai.think -= dt;
  if (ai.think <= 0) { ai.think = (dP < 250 ? 0.25 : 0.7) + Math.random() * 0.2; think(g, b); }

  let mx = 0, mz = 0, sprint = false, jump = false;
  const t = ai.target && ai.target.alive ? ai.target : null;
  if (ai.state === 'fight' && t) {
    const dx = t.pos.x - b.pos.x, dz = t.pos.z - b.pos.z, d = Math.hypot(dx, dz) || 1;
    const wantYaw = Math.atan2(-dx, -dz);
    b.yaw += wrapA(wantYaw - b.yaw) * Math.min(1, dt * 8);
    b.pitch = Math.atan2(t.pos.y - b.pos.y, d);
    const w = b.weapon;
    const pref = !w ? 5 : w.def.cls === 'SG' ? 8 : w.def.cls === 'SMG' || w.def.cls === 'PISTOL' ? 15 : w.def.cls === 'SNIPER' ? 90 : 35;
    let fwd = d > pref * 1.4 ? 1 : d < pref * 0.6 ? -0.6 : 0;
    ai.strafeT -= dt; if (ai.strafeT <= 0) { ai.strafeT = 0.6 + Math.random() * 1.4; ai.strafe = Math.random() < 0.15 ? 0 : (Math.random() < 0.5 ? -1 : 1); }
    const fx = dx / d, fz = dz / d;
    mx = fx * fwd + -fz * ai.strafe * 0.8; mz = fz * fwd + fx * ai.strafe * 0.8;
    if (Math.random() < 0.004) jump = true;
    if (ai.react > 0) ai.react -= dt;
    else if (w) {
      if (w.mag <= 0) b.startReload();
      else if (b.canFire()) {
        if (ai.burst > 0) {
          const head = Math.random() < 0.18 + ai.skill * 0.15;
          const aimY = t.pos.y + (head ? 1.72 : 1.15);
          const e = b.eye();
          let ax = t.pos.x - e.x, ay = aimY - e.y, az = t.pos.z - e.z;
          const dd = Math.hypot(ax, ay, az);
          // error grows with distance, shrinks with skill; moving targets are harder
          const err = (1.4 + dd * 0.022 + (t.moving ? 1.0 : 0)) * (1.35 - ai.skill) * Math.PI / 180;
          const yaw = Math.atan2(ax, az) + gaussR() * err * 2, pitch = Math.asin(ay / dd) + gaussR() * err * 2;
          g.fire(b, { x: Math.sin(yaw) * Math.cos(pitch), y: Math.sin(pitch), z: Math.cos(yaw) * Math.cos(pitch) });
          ai.burst--;
          if (!w.def.auto) ai.pause = 0.25 + Math.random() * 0.6;
        } else {
          ai.pause -= dt;
          if (ai.pause <= 0) { ai.burst = w.def.auto ? 3 + ((Math.random() * 5) | 0) : 1; ai.pause = 0.35 + Math.random() * 0.7; }
        }
      }
    }
  } else if (ai.goal) {
    let dx = ai.goal.x - b.pos.x, dz = ai.goal.z - b.pos.z;
    const d = Math.hypot(dx, dz);
    if (ai.detourT > 0) { ai.detourT -= dt; dx = ai.detour.x; dz = ai.detour.z; }
    if (d < 1.6) {
      if (ai.goalType === 'loot' && ai.goalLoot) { g.loot.take(b, ai.goalLoot); ai.goalLoot = null; }
      if (ai.goalType === 'crate' && ai.goalCrate) { g.loot.lootAll(b, ai.goalCrate); ai.goalCrate = null; }
      if (ai.goalType === 'distract') ai.distractT = 0;
      if (ai.goalType === 'revive' && ai.reviveT && ai.reviveT.knocked && ai.reviveT.alive) b.action = { type: 'revive', target: ai.reviveT, t: 7, total: 7 };
      ai.goal = null;
    } else {
      const l = Math.hypot(dx, dz) || 1;
      mx = dx / l; mz = dz / l;
      sprint = d > 25 && !b.action;
      b.yaw += wrapA(Math.atan2(-mx, -mz) - b.yaw) * Math.min(1, dt * 6);
      b.pitch *= 0.9;
    }
  }
  // stuck detection → random detour
  ai.stuckT += dt;
  if (ai.stuckT > 1.2) {
    const moved = Math.hypot(b.pos.x - ai.lastX, b.pos.z - ai.lastZ);
    if (moved < 0.8 && (mx || mz) && ai.detourT <= 0) {
      const a = Math.atan2(mz, mx) + (Math.random() < 0.5 ? 1 : -1) * (1.2 + Math.random());
      ai.detour = { x: Math.cos(a), z: Math.sin(a) }; ai.detourT = 1 + Math.random() * 1.5;
      if (Math.random() < 0.3) jump = true;
    }
    ai.stuckT = 0; ai.lastX = b.pos.x; ai.lastZ = b.pos.z;
  }
  b.move(mx, mz, dt, sprint, jump);
}

// called by the game when a shot is fired: nearby bots "hear" it
export function hearShot(g, shooter) {
  const range = shooter.weapon && shooter.weapon.att && shooter.weapon.att.muzzle === 'suppressor' ? 35 : 110;
  for (const b of g.actors) {
    if (b === shooter || b.isPlayer || !b.alive || b.state !== 'ground' || b.team === shooter.team || !b.ai || b.ai.dummy) continue;
    const d = Math.abs(b.pos.x - shooter.pos.x) + Math.abs(b.pos.z - shooter.pos.z);
    if (d < range) {
      b.ai.heardT = 3; b.ai.heardFrom = shooter;
      if (!b.ai.target && Math.random() < 0.5) b.yaw = Math.atan2(-(shooter.pos.x - b.pos.x), -(shooter.pos.z - b.pos.z));
    }
  }
}

export function distract(g, x, z) {
  for (const b of g.actors) {
    if (b.isPlayer || !b.alive || b.state !== 'ground' || !b.ai || b.ai.dummy) continue;
    if (Math.hypot(b.pos.x - x, b.pos.z - z) < 28 && !b.ai.target) {
      b.ai.distract = { x: x + (Math.random() - 0.5) * 2, z: z + (Math.random() - 0.5) * 2 }; b.ai.distractT = 5;
      b.yaw = Math.atan2(-(x - b.pos.x), -(z - b.pos.z));
    }
  }
}

export { HALF };
