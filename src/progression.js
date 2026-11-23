// Shared by the browser and server.js: tiers, match rewards and the cosmetics catalogue.
// All items are original KRYVON designs (colours/patterns), priced in coins (soft currency, earned by playing).

export const TIERS = [
  { id: 'stone',   ar: 'حجر',        en: 'Stone',      min: 0,    color: '#9a948a' },
  { id: 'copper',  ar: 'نحاس',       en: 'Copper',     min: 300,  color: '#c07a4a' },
  { id: 'silver',  ar: 'فضة',        en: 'Silver',     min: 800,  color: '#c9d0d8' },
  { id: 'gold',    ar: 'ذهب',        en: 'Gold',       min: 1500, color: '#ffc531' },
  { id: 'ruby',    ar: 'ياقوت',      en: 'Ruby',       min: 2400, color: '#ff4d6a' },
  { id: 'emerald', ar: 'زمرّد',      en: 'Emerald',    min: 3500, color: '#3fd28a' },
  { id: 'crown',   ar: 'تاج الشام',  en: 'Sham Crown', min: 5000, color: '#c99bff' },
];

export const MASTER_PASS_LEVELS = 100;
export const MASTER_PASS_SKINS = [
  { level: 1,  ar: 'وشاح البتراء', en: 'Petra Shemagh', slot: 'scarf', item: 'scarf_green' },
  { level: 2,  ar: 'حزام شامي', en: 'Sham Belt', slot: 'top', item: 'top_desert' },
  { level: 3,  ar: 'عباءة الليم', en: 'Shadow Abaya', slot: 'top', item: 'top_track' },
  { level: 4,  ar: 'سلاح نحاسي', en: 'Copper Arsenal', slot: 'gun', item: 'gun_copper' },
  { level: 5,  ar: 'دبكة النصر', en: 'Victory Dabke', slot: 'dance', item: 'dabke' },
  { level: 6,  ar: 'تمويه صخري', en: 'Rose-Rock Camo', slot: 'gun', item: 'gun_rose' },
  { level: 7,  ar: 'عرضة الذهب', en: 'Golden Ardah', slot: 'dance', item: 'ardah' },
];
export function tierOf(tp) {
  let i = 0;
  while (i + 1 < TIERS.length && tp >= TIERS[i + 1].min) i++;
  const t = TIERS[i], next = TIERS[i + 1];
  const span = next ? next.min - t.min : 1000, into = tp - t.min;
  const sub = next ? 5 - Math.min(4, Math.floor(into / span * 5)) : 0;          // V → I inside a tier
  return { ...t, index: i, sub, roman: sub ? ['', 'I', 'II', 'III', 'IV', 'V'][sub] : '', progress: next ? into / span : 1, next };
}

export const MODES = [
  { id: 'solo',  ar: 'فردي', size: 1 },
  { id: 'duo',   ar: 'ثنائي', size: 2 },
  { id: 'squad', ar: 'فريق', size: 4 },
];

// match rewards – the server recomputes these, the client only displays them
export function rewards({ place, total, kills, mode }) {
  const teams = Math.max(1, Math.ceil(total / (MODES.find(m => m.id === mode) || MODES[0]).size));
  const frac = teams > 1 ? (place - 1) / (teams - 1) : 0;
  kills = Math.max(0, Math.min(30, kills | 0));
  const win = place === 1;
  const tp = Math.round(34 * Math.pow(1 - frac, 1.6) - 9) + Math.min(40, kills * 4) + (win ? 15 : 0);
  const xp = kills * 60 + Math.round((1 - frac) * 400) + (win ? 400 : 0);
  const coins = 15 + kills * 8 + (frac <= 0.1 ? 20 : 0) + (win ? 60 : 0);
  return { tp, xp, coins, win };
}

export const SLOTS = [
  { id: 'chute', ar: 'المظلة' },
  { id: 'gun',   ar: 'سكن السلاح' },
];

// color = main, color2 = accent/pattern (for chutes and guns)
export const COSMETICS = [
  // tops
  { id: 'top_olive',   slot: 'top', ar: 'جاكيت زيتي',        price: 0,    color: 0x8a7c55 },
  { id: 'top_thobe',   slot: 'top', ar: 'ثوب أبيض',          price: 400,  color: 0xeeeae0 },
  { id: 'top_leather', slot: 'top', ar: 'جاكيت جلد بني',      price: 700,  color: 0x5a3a24 },
  { id: 'top_linen',   slot: 'top', ar: 'قميص كتان أزرق',     price: 350,  color: 0x4a76a8 },
  { id: 'top_track',   slot: 'top', ar: 'بدلة رياضة كحلي',    price: 500,  color: 0x1f2a48 },
  { id: 'top_desert',  slot: 'top', ar: 'سترة البادية',       price: 900,  color: 0xc9a46a },
  { id: 'top_maroon',  slot: 'top', ar: 'كنزة خمري',          price: 450,  color: 0x6a1f2a },
  // pants
  { id: 'pants_dark',  slot: 'pants', ar: 'بنطلون غامق',      price: 0,    color: 0x3a3a34 },
  { id: 'pants_jeans', slot: 'pants', ar: 'جينز',             price: 300,  color: 0x34507a },
  { id: 'pants_khaki', slot: 'pants', ar: 'كاكي',             price: 300,  color: 0xa89468 },
  { id: 'pants_sirwal',slot: 'pants', ar: 'شروال رمادي',       price: 450,  color: 0x6e6e6a },
  { id: 'pants_black', slot: 'pants', ar: 'أسود تكتيكي',       price: 600,  color: 0x16171a },
  // scarves
  { id: 'scarf_red',   slot: 'scarf', ar: 'شماغ أحمر',         price: 0,    color: 0xc0262d },
  { id: 'scarf_black', slot: 'scarf', ar: 'كوفية سوداء وبيضاء', price: 350,  color: 0x1c1c1c },
  { id: 'scarf_white', slot: 'scarf', ar: 'غترة بيضاء',        price: 250,  color: 0xf2f2ee },
  { id: 'scarf_green', slot: 'scarf', ar: 'لفحة زيتونية',      price: 400,  color: 0x4f6b3a },
  { id: 'scarf_none',  slot: 'scarf', ar: 'بدون',              price: 0,    color: null },
  // parachutes
  { id: 'chute_red',    slot: 'chute', ar: 'مظلة حمراء',       price: 0,    color: 0xc0262d, color2: 0xf0e6d2 },
  { id: 'chute_sky',    slot: 'chute', ar: 'سماء عمّان',        price: 500,  color: 0x4aa3ff, color2: 0xffffff },
  { id: 'chute_petra',  slot: 'chute', ar: 'وردي البتراء',      price: 800,  color: 0xc98a6a, color2: 0xf2d0b8 },
  { id: 'chute_night',  slot: 'chute', ar: 'ليل دمشق',          price: 900,  color: 0x14183a, color2: 0xffc531 },
  { id: 'chute_olive',  slot: 'chute', ar: 'غصن زيتون',         price: 600,  color: 0x5a7a3a, color2: 0xe8e2c8 },
  // weapon skins
  { id: 'gun_default', slot: 'gun', ar: 'افتراضي',             price: 0,    color: 0x202224 },
  { id: 'gun_copper',  slot: 'gun', ar: 'نحاس دمشقي',          price: 600,  color: 0xb06a3a },
  { id: 'gun_tile',    slot: 'gun', ar: 'أزرق الخزف',          price: 700,  color: 0x2f6fae },
  { id: 'gun_sand',    slot: 'gun', ar: 'تمويه رملي',          price: 500,  color: 0xc2a374 },
  { id: 'gun_rose',    slot: 'gun', ar: 'صخر وردي',            price: 750,  color: 0xc9806a },
  { id: 'gun_gold',    slot: 'gun', ar: 'ذهب كرايفون',          price: 2000, color: 0xd9a520 },
];
export const DEFAULT_LOADOUT = { top: 'top_olive', pants: 'pants_dark', scarf: 'scarf_red', chute: 'chute_red', gun: 'gun_default' };
export const itemById = id => COSMETICS.find(c => c.id === id);
export function randomLoadout(rng = Math.random) {
  const out = {};
  for (const s of SLOTS) { const opts = COSMETICS.filter(c => c.slot === s.id); out[s.id] = opts[(rng() * opts.length) | 0].id; }
  return out;
}
