// Player settings: defaults, schema for the settings screen, load/save (server account + local cache).

export const ACTIONS = [
  ['KeyW', 'تقدّم'], ['KeyS', 'رجوع'], ['KeyA', 'يسار'], ['KeyD', 'يمين'], ['ShiftLeft', 'ركض'], ['Space', 'قفز / مظلة'],
  ['KeyE', 'التقاط / فتح صندوق'], ['KeyR', 'تعبئة'], ['Digit1', 'السلاح 1'], ['Digit2', 'السلاح 2'], ['KeyX', 'تبديل السلاح'],
  ['KeyH', 'علاج'], ['KeyB', 'منشّط'], ['KeyG', 'رمي'], ['KeyT', 'تبديل المرمى'], ['KeyM', 'الخريطة'], ['KeyQ', 'عين سارة'],
  ['KeyF', 'ركوب / نزول السيارة'], ['KeyI', 'الحقيبة'], ['KeyZ', 'رسائل سريعة'], ['KeyV', 'علامة (Ping)'],
];

export const DEFAULTS = {
  graphics: { preset: '', fps: 60, fov: 80, brightness: 100, view: 100, shadows: true, showFps: false },
  sens: {
    camera: 100, ads: 100, reddot: 100, x2: 100, x4: 100, x8: 100, para: 100, vehicle: 100, // % of base
    touch: 100, invertY: false, gyro: 'off', gyroSens: 100,
  },
  controls: {
    aim: 'toggle', sprint: 'hold', autoPickup: true, autoReload: true, autoOpenChute: true,
    crossColor: '#ffffff', crossStyle: 'cross', hudScale: 100, btnOpacity: 70, leftFire: true, binds: {},
  },
  audio: { master: 70, sfx: 100, ui: 80, voice: true },
};

// [tab, path, label, type, extra]
export const SCHEMA = [
  ['graphics', 'graphics.preset', 'جودة الرسوميات', 'select', { options: [['low', 'منخفضة'], ['medium', 'متوسطة'], ['high', 'عالية'], ['ultra', 'فائقة']], restart: true }],
  ['graphics', 'graphics.fps', 'حد الإطارات (FPS)', 'select', { options: [[30, '30'], [60, '60'], [90, '90'], [120, '120'], [0, 'بلا حد']] }],
  ['graphics', 'graphics.fov', 'زاوية الرؤية (FOV)', 'range', { min: 60, max: 100, step: 1, unit: '°' }],
  ['graphics', 'graphics.brightness', 'السطوع', 'range', { min: 60, max: 150, step: 1, unit: '%' }],
  ['graphics', 'graphics.view', 'مدى الرؤية', 'range', { min: 50, max: 150, step: 5, unit: '%', restart: true }],
  ['graphics', 'graphics.shadows', 'الظلال', 'toggle', { restart: true }],
  ['graphics', 'graphics.showFps', 'إظهار عدّاد FPS', 'toggle'],

  ['sens', '#', 'حساسية الكاميرا', 'header'],
  ['sens', 'sens.camera', 'الكاميرا (منظور ثالث)', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.para', 'الطيارة والمظلة', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.vehicle', 'السيارة', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', '#', 'حساسية التصويب (ADS)', 'header'],
  ['sens', 'sens.ads', 'بدون سكوب', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.reddot', 'ريد دوت', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.x2', 'سكوب 2x', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.x4', 'سكوب 4x', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.x8', 'سكوب 8x', 'range', { min: 10, max: 300, step: 1, unit: '%' }],
  ['sens', '#', 'اللمس والجايرو', 'header'],
  ['sens', 'sens.touch', 'مضاعف حساسية اللمس', 'range', { min: 20, max: 300, step: 1, unit: '%' }],
  ['sens', 'sens.gyro', 'الجايروسكوب', 'select', { options: [['off', 'مطفي'], ['scope', 'بالسكوب بس'], ['always', 'دايماً']] }],
  ['sens', 'sens.gyroSens', 'حساسية الجايرو', 'range', { min: 10, max: 400, step: 1, unit: '%' }],
  ['sens', 'sens.invertY', 'عكس المحور العمودي', 'toggle'],

  ['controls', 'controls.aim', 'وضع التصويب', 'select', { options: [['toggle', 'ضغطة (تشغيل/إطفاء)'], ['hold', 'ضغط مستمر']] }],
  ['controls', 'controls.sprint', 'الركض', 'select', { options: [['hold', 'ضغط مستمر'], ['toggle', 'ضغطة (تشغيل/إطفاء)']] }],
  ['controls', 'controls.autoPickup', 'التقاط تلقائي (ذخيرة، علاج، رمّانات)', 'toggle'],
  ['controls', 'controls.autoReload', 'تعبئة تلقائية لما يفضى المخزن', 'toggle'],
  ['controls', 'controls.autoOpenChute', 'فتح المظلة تلقائياً', 'toggle'],
  ['controls', 'controls.leftFire', 'زر إطلاق يسار (موبايل)', 'toggle'],
  ['controls', 'controls.btnOpacity', 'شفافية الأزرار', 'range', { min: 20, max: 100, step: 5, unit: '%' }],
  ['controls', 'controls.hudScale', 'حجم الواجهة', 'range', { min: 70, max: 140, step: 5, unit: '%' }],
  ['controls', 'controls.crossStyle', 'شكل التصويبة', 'select', { options: [['cross', 'علامة +'], ['dot', 'نقطة'], ['circle', 'دائرة']] }],
  ['controls', 'controls.crossColor', 'لون التصويبة', 'color', { options: ['#ffffff', '#4dff6a', '#ffd34d', '#ff4d4d', '#3be3ff', '#ff6bd5'] }],
  ['controls', '#', 'أزرار الكيبورد', 'header'],
  ['controls', 'controls.binds', '', 'binds'],

  ['audio', 'audio.master', 'الصوت العام', 'range', { min: 0, max: 100, step: 1, unit: '%' }],
  ['audio', 'audio.sfx', 'المؤثرات (رصاص، انفجارات)', 'range', { min: 0, max: 100, step: 1, unit: '%' }],
  ['audio', 'audio.ui', 'أصوات الواجهة', 'range', { min: 0, max: 100, step: 1, unit: '%' }],
  ['audio', 'audio.voice', 'رسائل سارة', 'toggle'],
];

export const TABS = [['graphics', 'الرسوميات'], ['sens', 'الحساسية'], ['controls', 'التحكم'], ['audio', 'الصوت']];

const clone = o => JSON.parse(JSON.stringify(o));
function merge(base, over) {
  if (!over || typeof over !== 'object') return base;
  for (const k in base) {
    if (!(k in over)) continue;
    if (base[k] && typeof base[k] === 'object' && !Array.isArray(base[k])) base[k] = k === 'binds' ? { ...over[k] } : merge(base[k], over[k]);
    else if (typeof over[k] === typeof base[k]) base[k] = over[k];
  }
  return base;
}

export const getPath = (o, p) => p.split('.').reduce((a, k) => a[k], o);
export function setPath(o, p, v) { const ks = p.split('.'), last = ks.pop(); ks.reduce((a, k) => a[k], o)[last] = v; }

export function loadLocal() {
  let s = null;
  try { s = JSON.parse(localStorage.getItem('kryvon.settings')); } catch { }
  const out = merge(clone(DEFAULTS), s);
  // migrate the old single sensitivity slider
  try {
    const old = JSON.parse(localStorage.getItem('kryvon.sens'));
    if (!s && old) out.sens.camera = Math.round(old * 100);
    const pr = JSON.parse(localStorage.getItem('kryvon.preset'));
    if (!s && pr) out.graphics.preset = pr;
  } catch { }
  return out;
}
export function fromAccount(remote) { return merge(clone(DEFAULTS), remote); }
export function saveLocal(s) { try { localStorage.setItem('kryvon.settings', JSON.stringify(s)); } catch { } }
export const defaults = () => clone(DEFAULTS);

export function keyLabel(code) {
  if (!code) return '—';
  const named = { ShiftLeft: 'Shift', ShiftRight: 'R-Shift', ControlLeft: 'Ctrl', ControlRight: 'R-Ctrl', AltLeft: 'Alt', AltRight: 'R-Alt', ArrowUp: '↑', ArrowDown: '↓', ArrowLeft: '←', ArrowRight: '→', CapsLock: 'Caps', Backquote: '`' };
  return named[code] || code.replace(/^Key/, '').replace(/^Digit/, '').replace(/^Numpad/, 'Num');
}
