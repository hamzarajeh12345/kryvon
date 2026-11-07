// KRYVON – game data. All names are original/fictional (see GDD §7 licensing note).
// Map is scaled down for the browser prototype: 1 unit = 1 m, 1.6 x 1.6 km (GDD target 6 x 6 km).

export const MAP_SCALE = 1.5;     // the design below is laid out on ±800 m and stretched by this factor
export const HALF = 800 * MAP_SCALE; // map spans -HALF..HALF on X (east) and Z (south)
export const CELL = 5;            // heightfield spacing (m)
export const GRID_N = HALF * 2 / CELL + 1;
export const TOTAL_PLAYERS = 100;
export const MAP_SEED = 1946;

// Zones placed roughly by real geography (north = -Z, Mediterranean on the west)
// fr = flatten radius (0 = keep natural terrain), br = building radius
export const ZONES = [
  { id: 'cedars',    name: 'Cedar Mountains', ar: 'جبال الأرز',    x: -400, z: -560, r: 110, tier: 2, style: 'mountain',  buildings: 10, fr: 55, br: 60 },
  { id: 'aleppo',    name: 'Aleppo Citadel',  ar: 'قلعة حلب',      x: 80,   z: -600, r: 75,  tier: 3, style: 'citadel',   buildings: 12, fr: 70, br: 27 },
  { id: 'badia',     name: 'Badia Desert',    ar: 'البادية',       x: 470,  z: -460, r: 150, tier: 1, style: 'desert',    buildings: 7,  fr: 40, br: 120 },
  { id: 'beirut',    name: 'Coastal Beirut',  ar: 'بيروت',         x: -440, z: -250, r: 95,  tier: 2, style: 'beirut',    buildings: 22, fr: 60, br: 70 },
  { id: 'damascus',  name: 'Old Damascus',    ar: 'دمشق القديمة',  x: -60,  z: -240, r: 95,  tier: 3, style: 'damascus',  buildings: 30, fr: 85, br: 80 },
  { id: 'jerash',    name: 'Jerash & Ajloun', ar: 'جرش وعجلون',    x: 310,  z: -170, r: 110, tier: 1, style: 'jerash',    buildings: 12, fr: 90, br: 80 },
  { id: 'jerusalem', name: 'Old Jerusalem',   ar: 'القدس القديمة', x: -300, z: 140,  r: 90,  tier: 3, style: 'jerusalem', buildings: 26, fr: 90, br: 66 },
  { id: 'deadsea',   name: 'Dead Sea',        ar: 'البحر الميت',   x: 55,   z: 230,  r: 50,  tier: 1, style: 'resort',    buildings: 7,  fr: 30, br: 40 },
  { id: 'amman',     name: 'Downtown Amman',  ar: 'وسط البلد',     x: 270,  z: 130,  r: 100, tier: 3, style: 'amman',     buildings: 32, fr: 90, br: 85 },
  { id: 'olives',    name: 'Olive Plains',    ar: 'سهول الزيتون',  x: -380, z: 470,  r: 120, tier: 1, style: 'village',   buildings: 9,  fr: 40, br: 70 },
  { id: 'araba',     name: 'Wadi Araba',      ar: 'وادي عربة',     x: -60,  z: 580,  r: 110, tier: 1, style: 'desert',    buildings: 5,  fr: 35, br: 60 },
  { id: 'petra',     name: 'Petra',           ar: 'البتراء',       x: 330,  z: 560,  r: 70,  tier: 3, style: 'petra',     buildings: 6,  fr: 0,  br: 26 },
];

// Low-loot "safe start" villages
export const VILLAGES = [
  { id: 'v1', name: 'Tal Al-Rumman', ar: 'تل الرمان',   x: -200, z: -430 },
  { id: 'v2', name: 'Ein Al-Jouz',   ar: 'عين الجوز',   x: 230,  z: -390 },
  { id: 'v3', name: 'Deir Al-Teen',  ar: 'دير التين',   x: -230, z: -40 },
  { id: 'v4', name: 'Al-Salt',       ar: 'السلط',       x: 120,  z: -10 },
  { id: 'v5', name: 'Umm Qais',      ar: 'أم قيس',      x: 520,  z: -80 },
  { id: 'v6', name: 'Kharbet Saba',  ar: 'خربة سبع',    x: -200, z: 380 },
  { id: 'v7', name: 'Ras Al-Ein',    ar: 'رأس العين',   x: 140,  z: 390 },
  { id: 'v8', name: 'Al-Karak',      ar: 'الكرك',       x: 540,  z: 330 },
  { id: 'v9', name: 'Wadi Musa',     ar: 'وادي موسى',   x: 115,  z: 560 },
  { id: 'v10', name: 'Homs',         ar: 'حمص',         x: -170, z: -650 },
  { id: 'v11', name: 'Daraa',        ar: 'درعا',        x: 110,  z: -300 },
  { id: 'v12', name: 'Mafraq',       ar: 'المفرق',      x: 420,  z: -300 },
  { id: 'v13', name: 'Irbid',        ar: 'إربد',        x: 420,  z: -40 },
  { id: 'v14', name: 'Madaba',       ar: 'مادبا',       x: 170,  z: 290 },
  { id: 'v15', name: 'Hebron',       ar: 'الخليل',      x: -200, z: 260 },
  { id: 'v16', name: "Ma'an",        ar: 'معان',        x: 560,  z: 500 },
  { id: 'v17', name: 'Tyre',         ar: 'صور',         x: -450, z: -60 },
].map(v => ({ ...v, r: 40, tier: 1, style: 'village', buildings: 5, fr: 30, br: 30, minor: true }));

// stretch the layout (town sizes stay the same, distances grow); more buildings per town
for (const z of [...ZONES, ...VILLAGES]) { z.x *= MAP_SCALE; z.z *= MAP_SCALE; z.buildings = Math.round(z.buildings * 2.2); z.br = Math.round(z.br * 1.3); }

export const STYLES = {
  beirut:    { wall: [0xe8e4dc, 0xd9cfc0, 0xc9d3d8, 0xf0e6d2], roof: 0x9a4b36, h: [5, 9],   size: [8, 13], gap: 4 },
  damascus:  { wall: [0xcdb892, 0xbfa57a, 0xd8c8a5],           roof: 0x7a6a55, h: [4, 6],   size: [7, 11], gap: 2.6 },
  jerusalem: { wall: [0xe6d6b0, 0xdccb9f, 0xd2c093],           roof: 0xcfc09a, h: [4, 7],   size: [7, 11], gap: 2.6 },
  amman:     { wall: [0xeae3d3, 0xdedad0, 0xf2ecdf],           roof: 0xbdb6a8, h: [6, 10],  size: [8, 12], gap: 3 },
  village:   { wall: [0xd6c6a3, 0xc8b48c],                     roof: 0xa08c6a, h: [3.5, 4.5], size: [7, 10], gap: 5 },
  desert:    { wall: [0xc8a87a, 0xb89a70],                     roof: 0x8a7458, h: [3.5, 4.5], size: [6, 9], gap: 8 },
  resort:    { wall: [0xf4f1ea, 0xe6e0d0],                     roof: 0x6aa0b0, h: [4, 6],   size: [10, 14], gap: 5 },
  jerash:    { wall: [0xd8c49c, 0xc9b48a],                     roof: 0x9a8566, h: [4, 5],   size: [7, 10], gap: 5 },
  citadel:   { wall: [0xc2ab84, 0xb59d77],                     roof: 0x8c7a5c, h: [4, 6],   size: [7, 10], gap: 2.5 },
  mountain:  { wall: [0x8b6b4a, 0x7a5c3e],                     roof: 0x5a4030, h: [3.5, 4.5], size: [6, 9], gap: 4 },
  petra:     { wall: [0xc98a72, 0xb87a62],                     roof: 0xa06a55, h: [3.5, 5], size: [6, 9], gap: 3 },
};

export const RARITY_COLORS = ['#e8e8e8', '#5ed36a', '#4aa3ff', '#b46cff', '#ffc531'];
export const RARITY_NAMES = ['Common', 'Uncommon', 'Rare', 'Epic', 'Airdrop'];

export const AMMO = {
  '556': { name: '5.56', w: 0.5, pack: 30 },
  '762': { name: '7.62', w: 0.7, pack: 30 },
  '9mm': { name: '9mm', w: 0.375, pack: 30 },
  '45':  { name: '.45', w: 0.4, pack: 30 },
  '12g': { name: '12 Gauge', w: 1.25, pack: 10 },
  '300': { name: '.300', w: 1, pack: 10 },
  'mtn': { name: 'Mountain rounds', w: 0, pack: 0 },   // airdrop only
  'belt': { name: 'Haddar belt', w: 0, pack: 0 },      // airdrop only
};

// dmg per bullet, rpm, spread (deg hip), adsSpread, recoil (deg pitch kick), range (m)
export const WEAPONS = {
  saqr:     { name: 'Al-Saqr',      ar: 'الصقر', cls: 'AR',     ammo: '556', dmg: 40, rpm: 660, mag: 30, reload: 2.1, spread: 1.7, ads: 0.35, recoil: 0.5,  range: 350, auto: true,  rarity: 1 },
  nimr:     { name: 'Nimr-47',      ar: 'النمر', cls: 'AR',     ammo: '762', dmg: 48, rpm: 600, mag: 30, reload: 2.4, spread: 2.1, ads: 0.5,  recoil: 0.9,  range: 350, auto: true,  rarity: 2 },
  zalzal:   { name: 'Zalzal SMG',   ar: 'زلزال', cls: 'SMG',    ammo: '9mm', dmg: 25, rpm: 900, mag: 33, reload: 1.8, spread: 1.8, ads: 0.7,  recoil: 0.32, range: 150, auto: true,  rarity: 1 },
  shabah:   { name: 'Shabah .45',   ar: 'شبح',   cls: 'SMG',    ammo: '45',  dmg: 31, rpm: 700, mag: 35, reload: 2.0, spread: 1.6, ads: 0.6,  recoil: 0.4,  range: 160, auto: true,  rarity: 2 },
  rami:     { name: 'Rami DMR',     ar: 'الرامي', cls: 'DMR',   ammo: '762', dmg: 58, rpm: 260, mag: 10, reload: 2.6, spread: 1.4, ads: 0.1,  recoil: 1.3,  range: 550, auto: false, rarity: 2 },
  qannas:   { name: 'Al-Qannas',    ar: 'القنّاص', cls: 'SNIPER', ammo: '300', dmg: 78, head: 2.5, rpm: 48, mag: 5, reload: 3.2, spread: 3.5, ads: 0.02, recoil: 3, range: 750, auto: false, rarity: 3 },
  dabbal:   { name: 'Al-Dabbal',    ar: 'الدبّل', cls: 'SG',    ammo: '12g', dmg: 22, pellets: 9, rpm: 300, mag: 2, reload: 2.3, spread: 5, ads: 4, recoil: 2.5, range: 45, auto: false, rarity: 1 },
  sahm:     { name: 'Sahm P9',      ar: 'سهم',   cls: 'PISTOL', ammo: '9mm', dmg: 23, rpm: 420, mag: 15, reload: 1.4, spread: 1.6, ads: 0.8,  recoil: 0.6,  range: 100, auto: false, rarity: 0 },
  // Airdrop-exclusive (GDD §5): never spawn on the ground, out-damage every ground weapon
  jabaliya: { name: 'Mountain Rifle', ar: 'البندقية الجبلية', cls: 'SNIPER', ammo: 'mtn', dmg: 130, head: 2.5, rpm: 42, mag: 5, reload: 3.4, spread: 3.5, ads: 0.01, recoil: 3, range: 900, auto: false, rarity: 4, exclusive: true, ammoWith: 20 },
  haddar:   { name: 'Al-Haddar',      ar: 'الهدّار',           cls: 'LMG',    ammo: 'belt', dmg: 45, rpm: 900, mag: 100, reload: 4.2, spread: 2.2, ads: 0.75, recoil: 0.55, range: 400, auto: true, rarity: 4, exclusive: true, ammoWith: 200 },
};

export const ITEMS = {
  gauze:    { name: 'Gauze',          kind: 'heal',  time: 4, heal: 10,  cap: 75,  w: 2,  rarity: 0 },
  firstaid: { name: 'First Aid Bag',  kind: 'heal',  time: 6, heal: 75,  cap: 75,  w: 10, rarity: 1 },
  hospital: { name: 'Hospital Kit',   kind: 'heal',  time: 8, heal: 100, cap: 100, w: 20, rarity: 2 },
  tea:      { name: 'Sage Tea',       ar: 'شاي ميرمية', kind: 'boost', time: 4, boost: 40,  w: 4,  rarity: 1 },
  coffee:   { name: 'Black Coffee',   ar: 'قهوة سادة',  kind: 'boost', time: 6, boost: 60,  w: 6,  rarity: 2 },
  syringe:  { name: 'Syringe',        kind: 'boost', time: 6, boost: 100, w: 10, rarity: 3 },
  frag:     { name: 'Frag Grenade',   kind: 'throw', w: 12, rarity: 1 },
  slipper:  { name: 'Flying Slipper', ar: 'الشحاطة الطائرة', kind: 'throw', w: 3, rarity: 0 },
  flare:    { name: 'Flare Gun',      kind: 'throw', w: 8, rarity: 4 },
};

export const SCOPES = { 1.3: 'Red Dot', 2: '2x Scope', 4: '4x Scope', 8: '8x Scope' };

export const ARMOR = {
  helmet: { names: ['', 'Construction Helmet', 'Motorbike Helmet', 'Tactical Helmet'], reduce: [0, 0.3, 0.4, 0.55], dur: [0, 80, 150, 230] },
  vest:   { names: ['', 'Cloth Vest', 'Padded Vest', 'Hard Armor'],                     reduce: [0, 0.3, 0.4, 0.55], dur: [0, 200, 220, 250] },
  pack:   { names: ['', 'Cloth Pouch', 'School Bag', 'Hiking Pack'], capacity: [50, 150, 200, 250] },
};

// Al-Tawq (safe zone). Accelerated for the prototype; tune here.
// water = max fraction of the circle that may be sea (GDD §4A: ≤30% early, 0 at the end)
export const RING = [
  { wait: 75, shrink: 45, r: 560, dps: 0.6, water: 0.30 },
  { wait: 55, shrink: 40, r: 360, dps: 1.0, water: 0.30 },
  { wait: 45, shrink: 35, r: 220, dps: 2.0, water: 0.30 },
  { wait: 40, shrink: 30, r: 130, dps: 3.5, water: 0.15 },
  { wait: 35, shrink: 25, r: 75,  dps: 5,   water: 0.08 },
  { wait: 30, shrink: 20, r: 40,  dps: 7.5, water: 0 },
  { wait: 25, shrink: 20, r: 18,  dps: 10,  water: 0 },
  { wait: 20, shrink: 25, r: 2,   dps: 14,  water: 0 },
];

for (const p of RING) p.r *= MAP_SCALE;

export const AIRDROP_INTERVAL = [100, 140]; // GDD says 180–240 s; shortened for the shorter prototype match

export const WEATHER = [
  { id: 'clear',     name: 'Clear',      w: 40 },
  { id: 'sunset',    name: 'Sunset',     w: 20 },
  { id: 'fog',       name: 'Fog',        w: 15 },
  { id: 'rain',      name: 'Rain',       w: 10 },
  { id: 'snow',      name: 'Snow',       w: 10 },
  { id: 'khamaseen', name: 'Khamaseen',  w: 5 },
];

export const PRESETS = {
  low:    { label: 'Low',       pixel: 0.6,  shadows: 0,    far: 420,  trees: 0.35, aa: false },
  medium: { label: 'Medium',    pixel: 0.75, shadows: 1024, far: 600,  trees: 0.6,  aa: false },
  high:   { label: 'High',      pixel: 1,    shadows: 2048, far: 850,  trees: 1,    aa: true },
  ultra:  { label: 'Ultra',     pixel: 1.5,  shadows: 4096, far: 1200, trees: 1.3,  aa: true },
};

export const RANKS = ['Recruit', 'Soldier', 'Nashmi', 'Faris', 'Batal', 'Commander', 'KRYVON Legend'];

export const BOT_NAMES = [
  'AbuSaif', 'Nashmi_77', 'FalconOfPetra', 'ShawarmaKing', 'Zaatar99', 'DesertFox_JO', 'BeirutNights', 'Shami_Sniper',
  'MansafLord', 'Dabke_Master', 'OliveBranch', 'CedarGhost', 'Qudsi_Lion', 'Wadi_Runner', 'Habibi_GG', 'Knafeh4Life',
  'Ya_Salaam', 'JabalAmman', 'Hummus_Hero', 'Sheikh_Snipe', 'AbuShakra', 'ZarqaZero', 'IrbidBoy', 'AqabaWave',
  'SoukRaider', 'Mujaddara', 'TabboulehTank', 'Fattoush_FPS', 'MaqloubaFlip', 'Arak_Bear', 'Dalla_Smash', 'Mihbash_Pro',
  'NoorAlHuda', 'LaylaStorm', 'Rania_R', 'Yasmin_Shot', 'Farah_TTV', 'Dina_Dunes', 'Salma_Scope', 'Huda_Headshot',
  'Omar_Oud', 'Khaled_K', 'Tamer.exe', 'Ziad_Zone', 'Basel_Bomb', 'Fadi_Flank', 'Rami_Rush', 'Sami_Smoke', 'Wael_Wall',
  'KaakSeller', 'GasTruckFan', 'SlipperThrower', 'TeaWithSage', 'BlackCoffee', 'Abu_Jamal', 'Um_Ali', 'Nashama_JO',
  'Siq_Shadow', 'Treasury_TTV', 'Citadel_King', 'Hamidiyah', 'Jerash_Column', 'Ajloun_Archer', 'RedShemagh', 'Keffiyeh_K',
  'Tarboosh_T', 'Sherwal_Speed', 'Qumbaz_Q', 'Abaya_Ace', 'Thobe_Tactic', 'Wadi_Rum', 'Badia_Bedouin', 'Shams_Sun',
  'Qamar_Moon', 'Najm_Star', 'Raad_Thunder', 'Barq_Flash', 'Asifa_Storm', 'Saqr_Hawk', 'Nimr_Tiger', 'Fahd_Lynx',
  'Dhib_Wolf', 'Asad_Lion', 'Jamal_Camel', 'Ghazal_Gazelle', 'Hudhud', 'Bulbul_99', 'Zeytoun', 'Rumman', 'Teen_Fig',
  'Inab_Grape', 'Loz_Almond', 'Joz_Walnut', 'Fustuq', 'Simsim', 'Halawa', 'Maamoul', 'Qatayef', 'Awameh',
];

// ---------------- Maps (areas of the Levant world) ----------------
// cx/cz/radius = playable area; the first Al-Tawq circle covers it. players includes you.
export const MAPS = [
  { id: 'sham',     name: 'Bilad Al-Sham',        ar: 'بلاد الشام',          desc: 'الخريطة الكاملة: من جبال الأرز للبتراء. 100 لاعب، 8 مراحل للطوق.', cx: 0,    cz: 0,    radius: 1150, players: 100, size: '1.6 × 1.6 كم', time: '~20 د' },
  { id: 'north',    name: 'The North',            ar: 'الشمال: دمشق وبيروت وحلب', desc: 'مدن قديمة وقتال شوارع: الجامع الأموي، قلعة حلب، الروشة، بعلبك.', cx: -170, cz: -420, radius: 430,  players: 60,  size: '0.9 كم', time: '~14 د' },
  { id: 'heart',    name: 'Al-Quds & Amman',      ar: 'القدس وعمّان',        desc: 'قبة الصخرة، المدرج الروماني، البحر الميت وجرش. لوت عالي.', cx: 60,   cz: 60,   radius: 420,  players: 60,  size: '0.85 كم', time: '~14 د' },
  { id: 'south',    name: 'Petra & Wadi Araba',   ar: 'البتراء ووادي عربة',   desc: 'صحراء مفتوحة وصخور وردية: الخزنة، الدير، وأعمدة الرمل. للقناصين.', cx: 160,  cz: 480,  radius: 380,  players: 50,  size: '0.75 كم', time: '~12 د' },
  { id: 'training', name: 'Training Grounds',     ar: 'ساحة التدريب',        desc: 'كل الأسلحة والسكوبات وأهداف ثابتة. جرّب الحساسية بدون طوق.', cx: 120,  cz: -10,  radius: 0,    players: 1,   size: '—', time: 'بلا وقت', training: true },
];
for (const m of MAPS) { m.cx *= MAP_SCALE; m.cz *= MAP_SCALE; m.radius *= MAP_SCALE; }

// ---------------- Landmarks (آثار) ----------------
export const LANDMARKS = [
  { id: 'umayyad',   zone: 'damascus',  name: 'Umayyad Mosque',     ar: 'الجامع الأموي',      dx: 0,   dz: 0,   info: 'من أقدم وأكبر جوامع العالم، بمئذنة العروس ومئذنة عيسى وقبة النسر.' },
  { id: 'hamidiyah', zone: 'damascus',  name: 'Al-Hamidiyah Souq',  ar: 'سوق الحميدية',       dx: -2,  dz: 46,  info: 'السوق المسقوف الشهير اللي بيوصل لباب الجامع الأموي.' },
  { id: 'citadel',   zone: 'aleppo',    name: 'Citadel Gate',       ar: 'بوابة قلعة حلب',     dx: 0,   dz: 52,  info: 'جسر حجري مرتفع وبرج بوابة ضخم فوق التل، من أعظم قلاع العصور الوسطى.' },
  { id: 'raouche',   zone: 'beirut',    name: 'Raouché Rocks',      ar: 'صخرة الروشة',        dx: 0,   dz: 0,   info: 'صخرتين بالبحر قبالة بيروت، وحدة منهن فيها قوس طبيعي.' },
  { id: 'baalbek',   zone: 'cedars',    name: 'Baalbek Columns',    ar: 'أعمدة بعلبك',        dx: 150, dz: 70,  info: 'الأعمدة الستة الباقية من معبد جوبيتر، من أطول أعمدة العالم القديم.' },
  { id: 'cedarsgod', zone: 'cedars',    name: 'Cedars of God',      ar: 'أرز الرب',           dx: 0,   dz: 0,   info: 'غابة أرز معمّر بجبال لبنان، رمز البلد.' },
  { id: 'palmyra',   zone: 'badia',     name: 'Palmyra',            ar: 'تدمر',               dx: 0,   dz: 0,   info: 'مدينة القوافل بالبادية: الشارع المعمّد ومعبد بل وقوس النصر.' },
  { id: 'hadrian',   zone: 'jerash',    name: "Hadrian's Arch",     ar: 'قوس هادريان',        dx: -70, dz: -10, info: 'البوابة الجنوبية لجرش بثلاث فتحات، والساحة البيضاوية المحاطة بالأعمدة.' },
  { id: 'ajloun',    zone: 'jerash',    name: 'Ajloun Castle',      ar: 'قلعة عجلون',         dx: 50,  dz: -45, info: 'قلعة أيوبية على قمة جبل، كانت تراقب وادي الأردن.' },
  { id: 'dome',      zone: 'jerusalem', name: 'Dome of the Rock',   ar: 'قبة الصخرة',         dx: 18,  dz: -8,  info: 'المبنى المثمّن بالقبة الذهبية فوق ساحة الحرم.' },
  { id: 'aqsa',      zone: 'jerusalem', name: 'Al-Aqsa Mosque',     ar: 'المسجد الأقصى',      dx: 18,  dz: 34,  info: 'المصلى الكبير بالقبة الرمادية جنوب ساحة الحرم.' },
  { id: 'theatre',   zone: 'amman',     name: 'Roman Theatre',      ar: 'المدرج الروماني',    dx: -10, dz: 40,  info: 'مدرج محفور بسفح الجبل بوسط البلد، كان يتسع لآلاف المتفرجين.' },
  { id: 'hercules',  zone: 'amman',     name: 'Temple of Hercules', ar: 'معبد هرقل',          dx: 30,  dz: -45, info: 'أعمدة ضخمة على جبل القلعة بتطل على عمّان.' },
  { id: 'salt',      zone: 'deadsea',   name: 'Salt Pillars',       ar: 'أعمدة الملح',        dx: -25, dz: 0,   info: 'تكوينات ملحية بيضاء على شط أخفض نقطة على الأرض.' },
  { id: 'olivepress',zone: 'olives',    name: 'Old Olive Press',    ar: 'المعصرة القديمة',    dx: 0,   dz: 0,   info: 'معصرة حجرية وزيتونة معمّرة عمرها مئات السنين.' },
  { id: 'rum',       zone: 'araba',     name: 'Sandstone Pillars',  ar: 'أعمدة الرمل والقوس', dx: 0,   dz: 0,   info: 'جبال رملية حمراء وقوس صخري طبيعي، على طريقة وادي رم.' },
  { id: 'treasury',  zone: 'petra',     name: 'The Treasury',       ar: 'الخزنة',             dx: 40,  dz: 0,   info: 'واجهة منحوتة بالصخر الوردي بنهاية السيق.' },
  { id: 'monastery', zone: 'petra',     name: 'The Monastery',      ar: 'الدير',              dx: 30,  dz: -125, info: 'أضخم واجهة بالبتراء، فوق الجبل بعد مئات الدرجات.' },
  { id: 'karak',     zone: 'v8',        name: 'Karak Castle',       ar: 'قلعة الكرك',         dx: 0,   dz: 0,   info: 'قلعة صليبية ضخمة على تلّة، حصنها الأيوبيون بعدين.' },
  { id: 'gadara',    zone: 'v5',        name: 'Gadara Ruins',       ar: 'آثار أم قيس',        dx: 22,  dz: 0,   info: 'أعمدة بازلت سوداء ومدرج صغير بيطل على طبريا والجولان.' },
];

// ---------------- Attachments ----------------
const GUNS = ['AR', 'SMG', 'DMR', 'SNIPER', 'LMG', 'PISTOL'];
export const ATTACHMENTS = {
  suppressor:  { name: 'Suppressor',     ar: 'كاتم صوت',     slot: 'muzzle', fits: ['AR', 'SMG', 'DMR', 'SNIPER', 'PISTOL'], rarity: 2 },
  compensator: { name: 'Compensator',    ar: 'معوّض ارتداد',  slot: 'muzzle', fits: ['AR', 'SMG', 'DMR', 'SNIPER', 'LMG'], rarity: 1, recoil: 0.7 },
  extmag:      { name: 'Extended Mag',   ar: 'مخزن ممدود',    slot: 'mag',    fits: GUNS.filter(c => c !== 'LMG'), rarity: 1, mag: 1.5 },
  quickmag:    { name: 'Quick Mag',      ar: 'مخزن سريع',     slot: 'mag',    fits: GUNS.filter(c => c !== 'LMG'), rarity: 1, reload: 0.7 },
  stock:       { name: 'Tactical Stock', ar: 'أخمص تكتيكي',   slot: 'stock',  fits: ['AR', 'SMG', 'DMR', 'LMG'], rarity: 1, spread: 0.8, recoil: 0.85 },
};
export const ATT_SLOTS = [['muzzle', 'فوهة'], ['mag', 'مخزن'], ['stock', 'أخمص']];

export const QUICK_CHAT = ['عدو قدّامي!', 'محتاج علاج', 'تجمّعوا عندي', 'تمام 👍', 'في لوت هون', 'انتبهوا، الطوق!', 'غطّوني', 'لحقوني!'];

// ---------------- Roads (dirt/asphalt tracks between towns) ----------------
export const ROADS = [
  ['beirut', 'damascus'], ['beirut', 'cedars'], ['cedars', 'v1'], ['v1', 'aleppo'], ['aleppo', 'v2'], ['v2', 'badia'],
  ['damascus', 'v1'], ['damascus', 'v3'], ['v3', 'jerusalem'], ['damascus', 'jerash'], ['jerash', 'v5'], ['v2', 'jerash'],
  ['jerash', 'v4'], ['v4', 'amman'], ['amman', 'deadsea'], ['deadsea', 'jerusalem'], ['jerusalem', 'olives'], ['olives', 'v6'],
['v10', 'aleppo'], ['v10', 'cedars'], ['v11', 'damascus'], ['v11', 'v2'], ['v12', 'badia'], ['v12', 'jerash'], ['v13', 'jerash'], ['v13', 'v5'], ['v14', 'amman'], ['v14', 'deadsea'], ['v15', 'jerusalem'], ['v15', 'v6'], ['v16', 'petra'], ['v16', 'v8'], ['v17', 'beirut'], ['v17', 'v3'],
    ['v6', 'araba'], ['amman', 'v8'], ['v8', 'petra'], ['petra', 'v9'], ['v9', 'araba'], ['v7', 'v9'], ['deadsea', 'v7'], ['v5', 'badia'],
];
