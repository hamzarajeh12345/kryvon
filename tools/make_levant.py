# KRAYVON – The Levant map, built from the owner's layout image (assets/refs/levant_map.jpg, 1408x768).
# World = 2560 m x 1396 m (same aspect as the image). Every region / gate / lake is placed at its pixel position.
# Writes world/levant_height.r16 (1025x1025, 16-bit), levant_regions.json, levant_roads.json, levant_masks.png,
# and levant_compare.png (the owner's map next to the generated terrain) to check the layout before building it.
import json, math, os, heapq
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__)); OUT = os.path.join(HERE, '..', 'world'); os.makedirs(OUT, exist_ok=True)
REF = os.path.join(HERE, '..', 'assets', 'refs', 'levant_map.jpg')
IW, IH = 1408, 768
W, H = 2560.0, 2560.0 * IH / IW            # metres
N = 1025; HMAX = 280.0; SEA = 8.0
xs = np.linspace(-W / 2, W / 2, N); zs = np.linspace(H / 2, -H / 2, N)   # row 0 = north
X, Z = np.meshgrid(xs, zs)
def P(px, py): return ((px / IW - 0.5) * W, (0.5 - py / IH) * H)          # image pixel -> world metres (x east, z north)
def mpx(m): return m * IW / W                                            # metres -> pixels (for radii)

# ---------------- regions, at their place in the image ----------------
R = [  # name, arabic, px, py, radius(m), tier, kind
 ("Beirut Coastal", "بيروت الساحلية", 115, 105, 150, 3, "coast_city"),
 ("Beirut Port", "ميناء بيروت", 345, 135, 190, 3, "port"),
 ("Heliopolis", "هيليوبوليس", 300, 245, 110, 3, "ruins"),
 ("Aleppo Citadel", "قلعة حلب", 770, 62, 140, 4, "citadel_city"),
 ("Damascus Quarter", "حارة دمشق", 680, 240, 110, 4, "old_city"),
 ("Old Damascus", "دمشق القديمة", 680, 312, 170, 4, "big_city"),
 ("Tal Al-Rumman", "تل الرمان", 530, 290, 100, 2, "hills_ruins"),
 ("Old Jerusalem", "القدس القديمة", 150, 462, 150, 3, "old_city"),
 ("Joash & Alloun", "جوش وعلون", 300, 450, 100, 2, "village_hills"),
 ("Olive Plains", "سهول الزيتون", 455, 440, 150, 1, "olives"),
 ("The Outskirts", "الأطراف", 410, 528, 130, 2, "village"),
 ("Kharbet Saba", "خربة سابا", 400, 640, 100, 2, "village"),
 ("Wadi Araba", "وادي عربة", 470, 700, 180, 1, "desert_valley"),
 ("Dead Sea", "البحر الميت", 190, 640, 120, 1, "dead_sea"),
 ("Waiting Area", "ساحة الانتظار", 715, 735, 120, 0, "base"),
 ("Al Salt", "السلط", 960, 340, 130, 3, "hill_town"),
 ("Amman Downtown", "وسط عمان", 1120, 255, 170, 4, "big_city"),
 ("Al-Orak", "عمان الشرقية", 1290, 340, 130, 3, "industrial"),
 ("Wadi Musa", "وادي موسى", 1000, 490, 150, 3, "canyon_mountain"),
 ("Petra", "البتراء", 1060, 640, 140, 4, "petra"),
 ("Badia Desert", "بادية الشام", 1250, 580, 220, 1, "desert"),
 ("Cedar Hills", "جبال الأرز", 1080, 80, 200, 2, "mountain_forest"),
 ("QAIA", "مطار الملكة علياء الدولي", 1125, 530, 230, 4, "airport"),
]
GATES = {"B1": (530, 458), "B2": (840, 500), "B3": (795, 585)}            # the Great Levant Tunnel
SEA_POLY = [(0, 0), (625, 0), (600, 55), (470, 110), (330, 190), (250, 230), (170, 262), (110, 330), (75, 420), (45, 520), (0, 560)]
PORT_PIER = [(250, 40), (455, 40), (455, 270), (250, 270)]                 # the container terminal juts into the sea
LAKES = [((190, 640), (40, 125)), ((1052, 165), (14, 32)), ((1040, 385), (12, 45))]   # Dead Sea + two lakes (px centre, px radii)

def blob(cx, cz, r, sharp=2.0):
    d = np.sqrt((X - cx) ** 2 + (Z - cz) ** 2) / r
    return np.clip(1 - d, 0, 1) ** sharp
def fbm(scale, octaves=6, seed=0):
    g = np.random.default_rng(seed); acc = np.zeros((N, N)); amp = 1.0; tot = 0
    for o in range(octaves):
        cells = int(W / scale * 2 ** o) + 2
        img = Image.fromarray((g.random((cells, cells)) * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)
        acc += amp * (np.asarray(img, dtype=np.float32) / 255.0); tot += amp; amp *= 0.5
    return acc / tot
def poly_mask(poly):
    img = Image.new('L', (N, N), 0); d = ImageDraw.Draw(img)
    d.polygon([(px / IW * (N - 1), py / IH * (N - 1)) for px, py in poly], fill=255)
    return np.asarray(img, dtype=np.float32) / 255.0
def soften(m, r=6):
    img = Image.fromarray((m * 255).astype(np.uint8)).resize((N // r, N // r), Image.BILINEAR).resize((N, N), Image.BILINEAR)
    return np.asarray(img, dtype=np.float32) / 255.0

n1 = fbm(800, 7, 1); n2 = fbm(240, 5, 2); ridge = 1 - np.abs(fbm(420, 6, 3) * 2 - 1)
h = 34 + 26 * n1 + 9 * n2                                                          # plateau
def at(px, py): return P(px, py)
# mountains & hills (from the image: green hills NE, rocky massif in the middle with the tunnel, Wadi Musa / Petra range SE)
h += 150 * blob(*at(1080, 90), 520, 1.4) * (0.5 + 0.5 * ridge)                   # Cedar hills (north-east, forested)
h += 175 * blob(*at(690, 480), 420, 1.5) * (0.45 + 0.55 * ridge)                  # central massif (tunnel runs under it)
h += 190 * blob(*at(1030, 560), 380, 1.5) * (0.5 + 0.5 * ridge)                   # Wadi Musa + Petra sandstone range
h += 60 * blob(*at(960, 340), 200, 1.6)                                            # Al Salt hills
h += 50 * blob(*at(770, 62), 130, 2.5)                                             # Aleppo citadel mound
h += 40 * blob(*at(530, 290), 150, 2.0)                                            # Tal Al-Rumman
h += 70 * blob(*at(300, 420), 260, 1.6) * ridge                                    # hills behind Jerusalem / Joash
h -= 55 * blob(*at(190, 640), 300, 1.1)                                            # Dead Sea depression
h -= 30 * blob(*at(470, 700), 360, 1.1)                                            # Wadi Araba valley
desert = np.clip(blob(*at(1250, 600), 600, 0.9) * 1.1 + blob(*at(470, 700), 380, 1) * 0.8, 0, 1)
h = h * (1 - 0.55 * desert) + (30 + 8 * n2) * 0.55 * desert

# the sea: everything inside the coastline drops below sea level; the port terminal stays as a flat quay
sea = soften(poly_mask(SEA_POLY), 5); pier = poly_mask(PORT_PIER)
h = h * (1 - sea) + (SEA - 14) * sea

# ---------------- erosion (hydraulic + thermal) ----------------
CELL = W / (N - 1)
def thermal(h, it=30, talus=1.0, k=0.25):
    for _ in range(it):
        for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            nb = np.roll(np.roll(h, dy, 0), dx, 1); diff = h - nb
            move = np.where(diff > talus * CELL, (diff - talus * CELL) * k * 0.5, 0)
            h = h - move + np.roll(np.roll(move, -dy, 0), -dx, 1)
    return h
def hydraulic(h, it=140, rain=0.012, cap=1.4, erode=0.25, depos=0.2, evap=0.04):
    w = np.zeros_like(h); s = np.zeros_like(h)
    for _ in range(it):
        w += rain; tot = h + w
        nbs = [np.roll(tot, 1, 0), np.roll(tot, -1, 0), np.roll(tot, 1, 1), np.roll(tot, -1, 1)]
        drops = np.stack([np.maximum(tot - n, 0) for n in nbs]); total = drops.sum(0) + 1e-6
        out = np.minimum(w, total * 0.5); frac = drops / total
        capacity = cap * out * np.minimum(drops.max(0), 6) / 6
        dif = capacity - s; er = np.where(dif > 0, np.minimum(dif * erode, 0.6), dif * depos)
        h = h - er; s = s + er
        sf = np.where(w > 1e-6, s * out / (w + 1e-6), 0); w2 = w - out; s2 = s - sf
        for k, (ax, sh) in enumerate(((0, -1), (0, 1), (1, -1), (1, 1))):
            w2 += np.roll(out * frac[k], sh, ax); s2 += np.roll(sf * frac[k], sh, ax)
        w = w2 * (1 - evap); s = s2
    return h
h = thermal(h, 30); h = hydraulic(h, 140); h = thermal(h, 20, talus=0.85)
for _ in range(7): h = (h * 4 + np.roll(h, 1, 0) + np.roll(h, -1, 0) + np.roll(h, 1, 1) + np.roll(h, -1, 1)) / 8   # no erosion speckles

# flat ground for towns, the port quay and the base; water for the lakes
for name, ar, px, py, r, tier, kind in R:
    if kind in ("dead_sea", "desert", "desert_valley", "mountain_forest", "olives", "canyon_mountain", "petra"): continue
    b = blob(*at(px, py), r * 0.95, 1.0); m = float(h[b > 0.5].mean()); h = h * (1 - 0.85 * b) + m * 0.85 * b
quay = soften(pier, 2); h = h * (1 - quay) + (SEA + 3) * quay
for (cx, cy), (rx, ry) in LAKES:
    cxm, czm = at(cx, cy); e = ((X - cxm) / (rx * W / IW)) ** 2 + ((Z - czm) / (ry * H / IH)) ** 2
    lake = np.clip(1.6 - e, 0, 1); h = np.minimum(h, h * (1 - lake) + (float(h[e < 1].mean()) - 10) * lake)
h = np.clip(h, 0, HMAX)

# ---------------- roads ----------------
G = 161; gstep_x = W / (G - 1); gstep_z = H / (G - 1)
hh = np.asarray(Image.fromarray(h.astype(np.float32)).resize((G, G), Image.BILINEAR))
def cell(p): return (int(round((H / 2 - p[1]) / gstep_z)), int(round((p[0] + W / 2) / gstep_x)))
def cost_path(a, b, tunnel=False):
    s0, g0 = cell(a), cell(b); best = {s0: 0}; prev = {}; q = [(0, 0, s0)]
    while q:
        f, c, u = heapq.heappop(q)
        if u == g0: break
        if c > best.get(u, 1e18): continue
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if not dy and not dx: continue
                v = (u[0] + dy, u[1] + dx)
                if not (0 <= v[0] < G and 0 <= v[1] < G): continue
                dist = math.hypot(dx * gstep_x, dy * gstep_z); slope = abs(hh[v] - hh[u]) / dist
                wet = 60 if hh[v] < SEA + 1 else 1
                nc = c + dist * (1 + (0 if tunnel else slope * slope * 30)) * wet
                if nc < best.get(v, 1e18): best[v] = nc; prev[v] = u; heapq.heappush(q, (nc + math.dist((v[0] * gstep_z, v[1] * gstep_x), (g0[0] * gstep_z, g0[1] * gstep_x)), nc, v))
    path = [g0]
    while path[-1] in prev: path.append(prev[path[-1]])
    pts = [(c[1] * gstep_x - W / 2, H / 2 - c[0] * gstep_z) for c in reversed(path)]
    for _ in range(3): pts = [pts[0]] + [((pts[i - 1][0] + 2 * pts[i][0] + pts[i + 1][0]) / 4, (pts[i - 1][1] + 2 * pts[i][1] + pts[i + 1][1]) / 4) for i in range(1, len(pts) - 1)] + [pts[-1]]
    return pts
byname = {r[0]: r for r in R}
def C(n): r = byname[n]; return at(r[2], r[3])
HIGHWAYS = [("Beirut Coastal", "Beirut Port"), ("Beirut Port", "Heliopolis"), ("Heliopolis", "Damascus Quarter"), ("Damascus Quarter", "Aleppo Citadel"),
            ("Damascus Quarter", "Amman Downtown"), ("Amman Downtown", "Al-Orak"), ("Amman Downtown", "Wadi Musa"), ("Wadi Musa", "Petra"),
            ("Old Jerusalem", "Old Damascus"), ("Old Damascus", "Damascus Quarter")]
ROADS = [("Old Jerusalem", "Joash & Alloun"), ("Joash & Alloun", "Olive Plains"), ("Olive Plains", "The Outskirts"), ("The Outskirts", "Kharbet Saba"),
         ("Kharbet Saba", "Wadi Araba"), ("Wadi Araba", "Waiting Area"), ("Waiting Area", "Petra"), ("Tal Al-Rumman", "Heliopolis"), ("Tal Al-Rumman", "Old Damascus"),
         ("Al Salt", "Amman Downtown"), ("Al Salt", "Old Damascus"), ("Al-Orak", "Badia Desert"), ("Badia Desert", "Petra"), ("Old Jerusalem", "Dead Sea"),
         ("Aleppo Citadel", "Cedar Hills"), ("Cedar Hills", "Amman Downtown")]
roads = []
for a, b in HIGHWAYS: roads.append({"from": a, "to": b, "type": "highway", "points": [[round(x, 1), round(z, 1)] for x, z in cost_path(C(a), C(b))]})
for a, b in ROADS: roads.append({"from": a, "to": b, "type": "road", "points": [[round(x, 1), round(z, 1)] for x, z in cost_path(C(a), C(b))]})
# the tunnel: straight under the massif, B1 -> B2, with the B3 branch
gates = {k: at(*v) for k, v in GATES.items()}
roads.append({"from": "B1", "to": "B2", "type": "tunnel", "points": [list(map(lambda v: round(v, 1), gates["B1"])), list(map(lambda v: round(v, 1), gates["B2"]))]})
mid = ((gates["B1"][0] + gates["B2"][0]) / 2, (gates["B1"][1] + gates["B2"][1]) / 2)
roads.append({"from": "B3", "to": "tunnel", "type": "tunnel", "points": [list(map(lambda v: round(v, 1), gates["B3"])), [round(mid[0], 1), round(mid[1], 1)]]})

# ---------------- masks ----------------
slope = np.hypot(*np.gradient(h, CELL))
forest = np.clip(blob(*at(1080, 90), 560, 0.8) * 1.4 + blob(*at(300, 430), 240, 1) * 0.8 + blob(*at(1000, 250), 260, 1) * 0.5, 0, 1) * (fbm(130, 4, 9) > 0.46) * (h > SEA + 2) * (slope < 0.9)
olives = np.clip(blob(*at(455, 440), 280, 0.8) * 1.3 + blob(*at(270, 740), 160, 1) + blob(*at(400, 640), 170, 1) * 0.8, 0, 1) * (slope < 0.35) * (h > SEA + 2)
masks = np.stack([forest, olives, desert], -1)
Image.fromarray((np.clip(masks, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, 'levant_masks.png'))
(np.clip(h / HMAX, 0, 1) * 65535).astype('<u2').tofile(os.path.join(OUT, 'levant_height.r16'))
json.dump({"width": W, "length": H, "hmax": HMAX, "sea": SEA, "res": N,
           "regions": [{"name": r[0], "arabic": r[1], "x": round(at(r[2], r[3])[0], 1), "z": round(at(r[2], r[3])[1], 1), "radius": r[4], "tier": r[5], "kind": r[6]} for r in R],
           "gates": {k: [round(v[0], 1), round(v[1], 1)] for k, v in gates.items()}},
          open(os.path.join(OUT, 'levant_regions.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
json.dump(roads, open(os.path.join(OUT, 'levant_roads.json'), 'w'), indent=0)

# ---------------- comparison image ----------------
gy, gx = np.gradient(h, CELL); light = np.clip(0.78 + 1.7 * (-gx * 0.7 + gy * 0.7) / np.sqrt(1 + gx * gx + gy * gy), 0.25, 1.35)
hn = np.clip(h / HMAX, 0, 1)
col = np.array([0.6, 0.6, 0.42]) * (1 - hn[..., None]) + np.array([0.55, 0.48, 0.42]) * hn[..., None]
col = col * (1 - desert[..., None] * 0.8) + np.array([0.86, 0.74, 0.52]) * desert[..., None] * 0.8
col = col * (1 - olives[..., None] * 0.5) + np.array([0.5, 0.58, 0.32]) * olives[..., None] * 0.5
col = col * (1 - forest[..., None] * 0.8) + np.array([0.2, 0.38, 0.18]) * forest[..., None] * 0.8
col *= light[..., None]; col[h < SEA] = [0.16, 0.36, 0.58]
gen = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).resize((IW, IH), Image.BILINEAR)
d = ImageDraw.Draw(gen)
def tp(x, z): return ((x + W / 2) / W * IW, (H / 2 - z) / H * IH)
for r in roads:
    pts = [tp(*p) for p in r["points"]]
    d.line(pts, fill=(40, 90, 200) if r["type"] == "highway" else (240, 140, 30) if r["type"] == "tunnel" else (120, 105, 80), width=5 if r["type"] != "road" else 3)
try: font = ImageFont.truetype("arialbd.ttf", 18)
except Exception: font = ImageFont.load_default()
for name, ar, px, py, r, tier, kind in R:
    d.text((px - d.textlength(name, font=font) / 2 + 1, py - 9 + 1), name, font=font, fill=(0, 0, 0)); d.text((px - d.textlength(name, font=font) / 2, py - 9), name, font=font, fill=(255, 255, 255))
for k, (px, py) in GATES.items(): d.rectangle([px - 14, py - 10, px + 14, py + 10], fill=(30, 30, 30)); d.text((px - 10, py - 9), k, font=font, fill=(255, 210, 90))
ref = Image.open(REF).convert('RGB')
both = Image.new('RGB', (IW, IH * 2)); both.paste(ref, (0, 0)); both.paste(gen, (0, IH))
both.resize((IW // 2 * 2 // 2 * 1, IH)).save(os.path.join(OUT, 'levant_compare.png')) if False else both.save(os.path.join(OUT, 'levant_compare.png'))
print('height', round(float(h.min()), 1), '..', round(float(h.max()), 1), 'm | roads', len(roads), '| world', int(W), 'x', int(H), 'm')
