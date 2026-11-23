# KRAYVON world generator (stage 1): a 2 km x 2 km island-region of Bilad al-Sham with the 20 regions of the design
# document, eroded terrain (hydraulic + thermal, the same ideas Houdini/Gaea use), a road network between the towns,
# and masks for forests / olive groves / desert / water. Writes:
#   world/height.r16      1025x1025 16-bit heightmap for the Unity terrain (0..HMAX metres)
#   world/regions.json    region names, centres, radii, loot tier, kind
#   world/roads.json      road polylines (metres) with type
#   world/masks.png       R = forest, G = olives/farms, B = desert
#   world/map_preview.png the map you see before anything is built in Unity
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'world'); os.makedirs(OUT, exist_ok=True)
N = 1025; SIZE = 2048.0; HMAX = 260.0; SEA = 6.0           # metres
rng = np.random.default_rng(1977)
xs = np.linspace(-SIZE / 2, SIZE / 2, N); X, Z = np.meshgrid(xs, xs[::-1])   # row 0 = north

# ---------------- regions (x east, z north) ----------------
R = [  # name, arabic, x, z, radius, tier, kind
 ("Beirut Coastal", "بيروت الساحلية", -800, 640, 150, 3, "coast_city"),
 ("Cedar Mountains", "جبال الأرز", -470, 800, 190, 2, "mountain_forest"),
 ("Tal Al-Rumman", "تل الرمان", -150, 690, 110, 2, "hills_ruins"),
 ("Aleppo Citadel", "قلعة حلب", 330, 810, 150, 4, "citadel_city"),
 ("Ein Al-Jouz", "عين الجوز", 720, 640, 110, 1, "village"),
 ("Heliopolis", "هيليوبوليس", -470, 420, 120, 3, "ruins"),
 ("Old Damascus", "دمشق القديمة", 120, 400, 190, 4, "big_city"),
 ("Joash & Alloun", "جوش وعلون", -790, 160, 120, 2, "village_hills"),
 ("Umm Os", "أم القص", 760, 300, 100, 1, "rural"),
 ("Kharbet Saba", "خربة سابا", 440, 120, 100, 2, "village"),
 ("Old Jerusalem", "القدس القديمة", -440, -60, 150, 3, "old_city"),
 ("Olive Plains", "سهول الزيتون", -120, 110, 160, 1, "olives"),
 ("Amman Downtown", "وسط عمان", 230, -140, 170, 4, "big_city"),
 ("Al-Orak", "عمان الشرقية", 600, -200, 130, 3, "industrial"),
 ("Al Salt", "السلط", -60, -300, 120, 3, "hill_town"),
 ("Dead Sea", "البحر الميت", -560, -430, 170, 1, "dead_sea"),
 ("Badia Desert", "بادية الشام", 700, -540, 220, 1, "desert"),
 ("Wadi Musa", "وادي موسى", 130, -640, 140, 3, "canyon_mountain"),
 ("Petra", "البتراء", -170, -780, 130, 4, "petra"),
 ("Wadi Araba", "وادي عربة", -650, -830, 170, 1, "desert_valley"),
]

def blob(cx, cz, r, sharp=2.0):
    d = np.sqrt((X - cx) ** 2 + (Z - cz) ** 2) / r
    return np.clip(1 - d, 0, 1) ** sharp

def fbm(scale, octaves=6, seed=0):
    g = np.random.default_rng(seed); acc = np.zeros((N, N)); amp = 1.0; tot = 0
    for o in range(octaves):
        cells = int(SIZE / scale * 2 ** o) + 2
        grid = g.random((cells, cells))
        img = Image.fromarray((grid * 255).astype(np.uint8)).resize((N, N), Image.BICUBIC)
        acc += amp * (np.asarray(img, dtype=np.float32) / 255.0); tot += amp; amp *= 0.5
    return acc / tot

# ---------------- base landform ----------------
n1 = fbm(700, 7, 1); n2 = fbm(220, 5, 2); ridge = 1 - np.abs(fbm(400, 6, 3) * 2 - 1)
h = 28 + 30 * n1 + 10 * n2                                   # rolling plateau
h += 210 * blob(-470, 800, 420, 1.6) * (0.55 + 0.45 * ridge)    # Cedar range (north-west)
h += 120 * blob(130, -660, 330, 1.5) * (0.5 + 0.5 * ridge)      # Wadi Musa / Petra sandstone mountains (south)
h += 70 * blob(-470, 420, 260, 1.8) * ridge                     # Anti-Lebanon foothills by Heliopolis
h += 55 * blob(-60, -300, 200, 1.5)                             # Al Salt hills
h += 45 * blob(330, 810, 120, 2.5)                              # citadel mound (Aleppo)
h += 35 * blob(-150, 690, 160, 2.0)                             # Tal Al-Rumman
h -= 60 * blob(-560, -430, 330, 1.2)                            # Dead Sea depression
h -= 40 * blob(-650, -830, 300, 1.2)                            # Wadi Araba valley
desert = blob(700, -540, 480, 1.0)
h = h * (1 - 0.65 * desert) + (24 + 6 * n2) * 0.65 * desert       # flat Badia
# west coast: the land falls into the sea
coast = np.clip((X + 1024) / 260.0, 0, 1) ** 1.5
coast = np.maximum(coast, 1 - np.clip((Z - 250) / 300.0, 0, 1) * 0)  # only the north-west has the open sea
west = np.clip((-X - 700) / 324.0, 0, 1) * np.clip((Z + 100) / 400.0, 0, 1)
h = h * (1 - west) + (SEA - 18) * west

# ---------------- erosion ----------------
def thermal(h, it=40, talus=1.1, k=0.25):
    cell = SIZE / (N - 1)
    for _ in range(it):
        for dy, dx in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            nb = np.roll(np.roll(h, dy, 0), dx, 1)
            diff = h - nb; move = np.where(diff > talus * cell, (diff - talus * cell) * k * 0.5, 0)
            h = h - move + np.roll(np.roll(move, -dy, 0), -dx, 1)
    return h

def hydraulic(h, it=160, rain=0.012, cap=1.4, erode=0.25, depos=0.2, evap=0.04):
    w = np.zeros_like(h); s = np.zeros_like(h)
    for i in range(it):
        w += rain
        tot = h + w
        # flow to the lowest of the 4 neighbours
        nbs = [np.roll(tot, 1, 0), np.roll(tot, -1, 0), np.roll(tot, 1, 1), np.roll(tot, -1, 1)]
        drops = np.stack([np.maximum(tot - n, 0) for n in nbs]); total = drops.sum(0) + 1e-6
        out = np.minimum(w, total * 0.5)
        frac = drops / total
        slope = drops.max(0)
        capacity = cap * out * np.minimum(slope, 6) / 6
        dif = capacity - s
        er = np.where(dif > 0, np.minimum(dif * erode, 0.6), dif * depos)
        h = h - er; s = s + er
        # move water and sediment
        sf = np.where(w > 1e-6, s * out / (w + 1e-6), 0)
        w2 = w - out; s2 = s - sf
        for k, (ax, sh) in enumerate(((0, -1), (0, 1), (1, -1), (1, 1))):
            w2 += np.roll(out * frac[k], sh, ax); s2 += np.roll(sf * frac[k], sh, ax)
        w = w2 * (1 - evap); s = s2
    return h

h = thermal(h, 30)
h = hydraulic(h, 140)
h = thermal(h, 20, talus=0.9)
for _ in range(3): h = (h * 4 + np.roll(h, 1, 0) + np.roll(h, -1, 0) + np.roll(h, 1, 1) + np.roll(h, -1, 1)) / 8
h = np.clip(h, 0, HMAX)

# flatten town plateaus a little so buildings sit well
for name, ar, cx, cz, r, tier, kind in R:
    if kind in ("big_city", "old_city", "citadel_city", "coast_city", "industrial", "hill_town", "village", "village_hills", "rural"):
        b = blob(cx, cz, r * 0.9, 1.0); mean = float(h[(b > 0.5)].mean())
        h = h * (1 - 0.8 * b) + mean * 0.8 * b

# ---------------- roads: cheapest paths between neighbouring towns over the terrain ----------------
def cost_path(a, b):
    # A* on a coarse 129x129 grid, cost = distance x (1 + slope^2 x 8) + water penalty
    G = 129; step = SIZE / (G - 1)
    hh = np.asarray(Image.fromarray(h.astype(np.float32)).resize((G, G), Image.BILINEAR))
    def cell(p): return (int(round((SIZE / 2 - p[1]) / step)), int(round((p[0] + SIZE / 2) / step)))
    import heapq
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
                dist = step * math.hypot(dx, dy); slope = abs(hh[v] - hh[u]) / dist
                wet = 50 if hh[v] < SEA + 1 else 1
                nc = c + dist * (1 + slope * slope * 30) * wet
                if nc < best.get(v, 1e18): best[v] = nc; prev[v] = u; heapq.heappush(q, (nc + math.dist(v, g0) * step, nc, v))
    path = [g0]
    while path[-1] in prev: path.append(prev[path[-1]])
    pts = [(c[1] * step - SIZE / 2, SIZE / 2 - c[0] * step) for c in reversed(path)]
    # smooth
    for _ in range(3): pts = [pts[0]] + [((pts[i - 1][0] + 2 * pts[i][0] + pts[i + 1][0]) / 4, (pts[i - 1][1] + 2 * pts[i][1] + pts[i + 1][1]) / 4) for i in range(1, len(pts) - 1)] + [pts[-1]]
    return pts

towns = [r for r in R if r[6] not in ("mountain_forest", "dead_sea", "desert_valley", "olives")]
links = set()
for t in towns:   # each town to its 2 nearest
    near = sorted((math.dist((t[2], t[3]), (u[2], u[3])), u[0]) for u in towns if u is not t)[:2]
    for d, n in near: links.add(tuple(sorted((t[0], n))))
byname = {r[0]: r for r in R}
highways = {tuple(sorted(p)) for p in [("Beirut Coastal", "Heliopolis"), ("Heliopolis", "Old Damascus"), ("Old Damascus", "Aleppo Citadel"),
                                        ("Old Damascus", "Amman Downtown"), ("Amman Downtown", "Wadi Musa"), ("Wadi Musa", "Petra"), ("Old Jerusalem", "Amman Downtown")]}
links |= highways
roads = []
for a, b in sorted(links):
    pts = cost_path((byname[a][2], byname[a][3]), (byname[b][2], byname[b][3]))
    roads.append({"from": a, "to": b, "type": "highway" if (a, b) in highways else "road", "points": [[round(p[0], 1), round(p[1], 1)] for p in pts]})

# ---------------- masks ----------------
slope = np.hypot(*np.gradient(h, SIZE / (N - 1)))
forest = np.clip(blob(-470, 800, 380, 0.8) * 1.4 + blob(-790, 160, 200, 1) + blob(-150, 690, 180, 1) * 0.6 + blob(130, -640, 200, 1) * 0.3, 0, 1) * (fbm(120, 4, 9) > 0.45) * (h > SEA + 2) * (slope < 0.9)
olives = np.clip(blob(-120, 110, 260, 0.8) * 1.3 + blob(720, 640, 160, 1) + blob(760, 300, 150, 1) + blob(440, 120, 150, 1), 0, 1) * (slope < 0.35) * (h > SEA + 2)
desertm = np.clip(desert * 1.2 + blob(-650, -830, 320, 0.8) + blob(-170, -780, 160, 1) * 0.7, 0, 1)
masks = np.stack([forest, olives, desertm], -1)
Image.fromarray((np.clip(masks, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, 'masks.png'))

# ---------------- save ----------------
(np.clip(h / HMAX, 0, 1) * 65535).astype('<u2').tofile(os.path.join(OUT, 'height.r16'))
json.dump({"size": SIZE, "hmax": HMAX, "sea": SEA, "res": N,
           "regions": [{"name": r[0], "arabic": r[1], "x": r[2], "z": r[3], "radius": r[4], "tier": r[5], "kind": r[6]} for r in R]},
          open(os.path.join(OUT, 'regions.json'), 'w', encoding='utf8'), ensure_ascii=False, indent=1)
json.dump(roads, open(os.path.join(OUT, 'roads.json'), 'w'), indent=0)

# ---------------- preview map ----------------
P = 1400; sc = P / SIZE
hn = np.clip(h / HMAX, 0, 1)
gy, gx = np.gradient(h, SIZE / (N - 1)); light = np.clip(0.75 + 1.6 * (-gx * 0.7 + gy * 0.7) / np.sqrt(1 + gx * gx + gy * gy), 0.25, 1.35)
col = np.zeros((N, N, 3))
low = np.array([0.55, 0.62, 0.38]); mid = np.array([0.62, 0.58, 0.42]); high = np.array([0.55, 0.5, 0.45]); sand = np.array([0.86, 0.76, 0.55])
col[:] = low
col = col * (1 - hn[..., None]) + high * hn[..., None]
col = col * (1 - desertm[..., None] * 0.85) + sand * desertm[..., None] * 0.85
col = col * (1 - olives[..., None] * 0.5) + np.array([0.48, 0.58, 0.3]) * olives[..., None] * 0.5
col = col * (1 - forest[..., None] * 0.8) + np.array([0.18, 0.36, 0.16]) * forest[..., None] * 0.8
col *= light[..., None]
water = h < SEA; col[water] = [0.18, 0.38, 0.6]
dead = (blob(-560, -430, 150, 1) > 0.25); col[dead] = [0.35, 0.62, 0.68]
img = Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).resize((P, P), Image.BILINEAR)
d = ImageDraw.Draw(img)
def tp(x, z): return ((x + SIZE / 2) * sc, (SIZE / 2 - z) * sc)
for r in roads:
    pts = [tp(*p) for p in r["points"]]
    d.line(pts, fill=(40, 40, 40) if r["type"] == "highway" else (110, 95, 70), width=6 if r["type"] == "highway" else 3)
try:
    font = ImageFont.truetype("arialbd.ttf", 19); small = ImageFont.truetype("arial.ttf", 14)
except Exception:
    font = small = ImageFont.load_default()
tier_col = {1: (230, 230, 230), 2: (120, 220, 120), 3: (90, 160, 255), 4: (255, 200, 60)}
for name, ar, x, z, r, tier, kind in R:
    cx, cz = tp(x, z); rr = r * sc
    d.ellipse([cx - rr, cz - rr, cx + rr, cz + rr], outline=tier_col[tier], width=2)
    d.text((cx - d.textlength(name, font=font) / 2 + 1, cz - 11 + 1), name, font=font, fill=(0, 0, 0))
    d.text((cx - d.textlength(name, font=font) / 2, cz - 11), name, font=font, fill=(255, 255, 255))
    t = f"Tier {tier}"; d.text((cx - d.textlength(t, font=small) / 2, cz + 10), t, font=small, fill=tier_col[tier])
d.rectangle([10, 10, 420, 128], fill=(0, 0, 0, 160))
d.text((20, 16), "KRAYVON — Bilad al-Sham  (2 km x 2 km)", font=font, fill=(255, 210, 90))
for i, (k, v) in enumerate([("Tier 4 very high", 4), ("Tier 3 high", 3), ("Tier 2 medium", 2), ("Tier 1 low", 1)]):
    d.text((20, 44 + i * 20), "●  " + k, font=small, fill=tier_col[v])
d.text((240, 44), "▬ highway", font=small, fill=(30, 30, 30)); d.text((240, 64), "▬ road", font=small, fill=(110, 95, 70))
d.text((240, 84), "■ forest  ■ olives", font=small, fill=(60, 140, 60))
img.save(os.path.join(OUT, 'map_preview.png'))
print('height range', round(float(h.min()), 1), round(float(h.max()), 1), '| roads', len(roads), '| forest cells', int((forest > 0.5).sum()))
