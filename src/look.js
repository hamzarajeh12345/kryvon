// Character looks: face, hair, body and clothes. Shared by the browser and server.js.
// All presets are original KRYVON characters.

export const LOOK = {
  skins: [0xf2d2b6, 0xe0b48f, 0xc99a74, 0xb07f5c, 0x8a5c42, 0x5e3b29],
  hairColors: [0x16110d, 0x3b2416, 0x6a4024, 0xa8783e, 0x8a8a86, 0x7a2a1a],
  eyeColors: [0x3a2414, 0x5a3a1a, 0x3a6a3a, 0x3a5a8a, 0x6a6a6a],
  clothColors: [0x161616, 0x2a2d30, 0x4a4f3a, 0x6b6f4a, 0x8a7a5a, 0xc2a374, 0x1f2a48, 0x34507a, 0x5a3a24, 0x6a1f2a, 0xeeeae0, 0x6e6e6a],
  options: {
    sex: [['m', 'رجل'], ['f', 'امرأة']],
    build: [['slim', 'نحيف'], ['normal', 'عادي'], ['heavy', 'عضلي']],
    face: [[0, 'بيضوي'], [1, 'عريض'], [2, 'طويل']],
    hair: [['curly', 'كيرلي'], ['short', 'قصير'], ['buzz', 'محلوق'], ['long', 'طويل'], ['bun', 'كعكة'], ['bald', 'أصلع']],
    beard: [['none', 'بدون'], ['stubble', 'خفيفة'], ['full', 'كاملة'], ['goatee', 'سكسوكة']],
    glasses: [['none', 'بدون'], ['aviator', 'شمسية'], ['clear', 'طبية']],
    lips: [['natural', 'طبيعي'], ['red', 'أحمر']],
    top: [['tee', 'تيشيرت'], ['tank', 'بدون أكمام'], ['longsleeve', 'كم طويل'], ['hoodie', 'هودي'], ['jacket', 'جاكيت'], ['fur', 'جاكيت فرو'], ['suit', 'بدلة رسمية']],
    topPattern: [['plain', 'سادة'], ['camo', 'تمويه'], ['digital', 'رقمي'], ['tiger', 'مخطط']],
    pantsPattern: [['plain', 'سادة'], ['camo', 'تمويه'], ['digital', 'رقمي']],
    vest: [['none', 'بدون'], ['plate', 'درع تكتيكي'], ['harness', 'أحزمة']],
    scarf: [['none', 'بدون'], ['shemagh_bw', 'كوفية سودا وبيضا'], ['shemagh_rw', 'شماغ أحمر'], ['olive', 'لفحة زيتي'], ['black', 'لفحة سودا']],
    patch: [['none', 'بدون'], ['jo', 'علم الأردن'], ['crown', 'تاج'], ['olive', 'غصن زيتون']],
    boots: [['black', 'سود'], ['tan', 'بيج']],
  },
  defaults: {
    sex: 'm', skin: 2, face: 0, build: 'normal', eyes: 0, hair: 'curly', hairColor: 0, beard: 'full', glasses: 'aviator', lips: 'natural',
    hijab: false, hijabColor: 0, top: 'tee', topColor: 0, topPattern: 'plain', vest: 'plate', vestColor: 0, pantsColor: 0, pantsPattern: 'plain',
    scarf: 'shemagh_bw', gloves: true, patch: 'none', tags: false, boots: 'black',
  },
};

// ready-made characters – pick one, then change anything
export const PRESET_LOOKS = [
  { id: 'saqr',     ar: 'صقر',   role: 'هجوم',        look: {} },
  { id: 'nashmiya', ar: 'نشمية', role: 'قنّاصة',       look: { sex: 'f', skin: 1, eyes: 1, hijab: true, hijabColor: 0, beard: 'none', glasses: 'none', lips: 'red', top: 'longsleeve', topPattern: 'digital', pantsPattern: 'digital', scarf: 'none', patch: 'jo', build: 'slim' } },
  { id: 'jabbar',   ar: 'جبّار', role: 'ثقيل',         look: { build: 'heavy', top: 'tank', vest: 'harness', scarf: 'none', tags: true } },
  { id: 'shabah',   ar: 'شبح',   role: 'عمليات خاصة',  look: { top: 'hoodie', patch: 'crown', scarf: 'none', hair: 'short' } },
  { id: 'safir',    ar: 'سفير',  role: 'تكتيكي',       look: { top: 'suit', vest: 'harness', scarf: 'none', hair: 'short', beard: 'stubble' } },
  { id: 'rassad',   ar: 'رصّاد', role: 'استطلاع',      look: { top: 'fur', topColor: 8, topPattern: 'camo', glasses: 'none', vest: 'harness', vestColor: 8 } },
  { id: 'qannas',   ar: 'قنّاص', role: 'قنص',          look: { top: 'tank', vestColor: 2, glasses: 'none', scarf: 'none', build: 'heavy', pantsColor: 3, pantsPattern: 'camo' } },
  { id: 'tayf',     ar: 'طيف',   role: 'تخفّي',        look: { top: 'jacket', topColor: 6, topPattern: 'digital', vestColor: 6, pantsColor: 6 } },
];
export function presetLook(id) { const p = PRESET_LOOKS.find(x => x.id === id) || PRESET_LOOKS[0]; return { ...LOOK.defaults, ...p.look }; }

// keep only known keys and values (used by the server before saving)
export function sanitizeLook(l) {
  const d = LOOK.defaults, out = {};
  if (!l || typeof l !== 'object') return { ...d };
  const list = k => k === 'skin' ? LOOK.skins : k === 'eyes' ? LOOK.eyeColors : k === 'hairColor' ? LOOK.hairColors : LOOK.clothColors;
  for (const k in d) {
    const v = l[k], opts = LOOK.options[k];
    if (opts) out[k] = opts.some(o => o[0] === v) ? v : d[k];
    else if (typeof d[k] === 'boolean') out[k] = typeof v === 'boolean' ? v : d[k];
    else out[k] = Number.isInteger(v) && v >= 0 && v < list(k).length ? v : d[k];
  }
  if (out.sex === 'm') out.hijab = false;
  if (out.sex === 'f') out.beard = 'none';
  return out;
}

// varied looks for bots – nobody is dressed the same
export function randomLook(rng = Math.random) {
  const pick = a => a[(rng() * a.length) | 0], o = LOOK.options;
  const base = rng() < 0.35 ? presetLook(pick(PRESET_LOOKS).id) : { ...LOOK.defaults };
  const f = rng() < 0.28;
  return sanitizeLook({
    ...base, sex: f ? 'f' : 'm', skin: (rng() * LOOK.skins.length) | 0, face: (rng() * 3) | 0, eyes: (rng() * LOOK.eyeColors.length) | 0,
    build: pick(o.build)[0], hairColor: (rng() * 5) | 0,
    hair: f ? pick(['long', 'bun', 'short']) : pick(['curly', 'curly', 'short', 'buzz', 'bald']),
    beard: f ? 'none' : pick(['none', 'stubble', 'full', 'full', 'goatee']),
    glasses: pick(['none', 'none', 'aviator', 'aviator', 'clear']), lips: f && rng() < 0.4 ? 'red' : 'natural',
    hijab: f && rng() < 0.55, hijabColor: pick([0, 0, 1, 6, 10, 2]),
    top: pick(o.top)[0], topColor: (rng() * LOOK.clothColors.length) | 0, topPattern: pick(o.topPattern)[0],
    vest: pick(o.vest)[0], vestColor: pick([0, 1, 2, 3, 4, 5, 8]), pantsColor: pick([0, 1, 2, 3, 4, 6, 7, 11]), pantsPattern: pick(o.pantsPattern)[0],
    scarf: rng() < 0.4 ? pick(o.scarf)[0] : 'none', gloves: rng() < 0.6, patch: rng() < 0.25 ? pick(o.patch)[0] : 'none', tags: rng() < 0.2, boots: pick(o.boots)[0],
  });
}
