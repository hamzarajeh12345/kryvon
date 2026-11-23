// Store + inventory pages for cosmetics (KryvonSkins): weapon skins per gun (gun picker on top), parachutes,
// backpacks, helmets, vehicles. The preview on the right is the REAL model, rendered live from the lobby's Showcase
// rig with the skin on it, turning slowly; the evolving M4A1 skin shows its level and an UPGRADE button.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kryvon
{
    public partial class KryvonLobby
    {
        int gunPick;                                   // which gun's skins the WEAPON SKINS page shows
        Transform showRoot; Camera showCam; RenderTexture showRT; GameObject shown;

        static string KindModel(Cos c) => c.kind switch
        {
            CosKind.Weapon => KryvonSkins.GunModel(c.target), CosKind.Parachute => "p_chute", CosKind.Backpack => "p_bag",
            CosKind.Helmet => "p_helmet", _ => "v_pickup",
        };
        static string GunIcon(string model) => model switch
        {
            "w_sniper" or "w_bigsniper" => "w_sniper", "w_dmr" => "w_dmr", "w_lmg" or "w_dp" => "w_lmg", "w_shotgun" or "w_autoshot" or "w_dbshot" => "w_shotgun",
            "w_smg" or "w_vector" or "w_uzi" or "w_drum" => "w_smg", "w_pistol" => "w_pistol", _ => "w_ar",
        };

        List<Item> CosItems(CosKind k, string target, bool ownedOnly)
        {
            var list = new List<Item>();
            foreach (var c in KryvonSkins.All)
            {
                if (c.kind != k || (target != null && c.target != target)) continue;
                bool own = KryvonSkins.Owned(c); if (ownedOnly && !own) continue;
                var eq = KryvonSkins.Equipped(c.kind, c.target);
                var cc = c;
                list.Add(new Item { id = c.id, kind = "cos", cos = c, name = c.name, rarity = c.rarity, price = c.price, gems = c.gems, owned = own, equipped = eq == c,
                    desc = c.desc.Length > 0 ? c.desc : c.kind == CosKind.Weapon ? "Weapon skin – shows on this gun in your hands." : "Shows on your next match.",
                    buy = () => KryvonSkins.Buy(cc), equip = () => KryvonSkins.Equip(cc) });
            }
            return list;
        }

        // ---------- the live 3D preview: an armory pedestal ----------
        // The real model on a holographic pedestal (a glowing ring in the skin's rarity colour over a soft shadow), lit by a
        // studio rim light from behind so the silhouette stands out, turning slowly. Max-level evolving skins shed ice sparkles.
        Transform pedestal; MeshRenderer pedRing, pedShadow; Light rimLight; Cos shownCos; int previewLv; float nextSparkle; Vector3 shownCenter; float shownRadius;
        static MaterialPropertyBlock pedMpb;
        MeshRenderer Quad(string n, Material m, Transform parent)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = n; Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false); q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; return r;
        }
        void Stage(Cos c)
        {
            if (!shown) { if (pedestal) pedestal.gameObject.SetActive(false); return; }
            if (!pedestal)
            {
                pedestal = new GameObject("Pedestal").transform; pedestal.SetParent(showRoot, false);
                pedShadow = Quad("Shadow", KryvonFX.SoftMat, pedestal); pedRing = Quad("Ring", KryvonFX.RingMat, pedestal);
                pedRing.transform.localPosition = Vector3.up * 0.004f;
                rimLight = new GameObject("Rim").AddComponent<Light>(); rimLight.transform.SetParent(pedestal, false);
                rimLight.type = LightType.Spot; rimLight.spotAngle = 70; rimLight.range = 6; rimLight.shadows = LightShadows.None;
            }
            pedestal.gameObject.SetActive(true);
            foreach (var t in pedestal.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = shown.layer;
            // frame the model: bounds of its visible meshes
            var rs = shown.GetComponentsInChildren<Renderer>(); var bb = new Bounds(shown.transform.position, Vector3.zero); bool any = false;
            foreach (var r in rs) { if (r is ParticleSystemRenderer) continue; if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
            shownCenter = bb.center; shownRadius = Mathf.Max(0.25f, Mathf.Max(bb.extents.x, bb.extents.z));
            pedestal.position = new Vector3(bb.center.x, bb.min.y - 0.03f, bb.center.z);
            float d = shownRadius * 2.5f; pedRing.transform.localScale = new Vector3(d, d, 1); pedShadow.transform.localScale = new Vector3(d * 1.15f, d * 1.15f, 1);
            var col = RarityCol(c.rarity); if (c.source == "evolve") col = new Color(0.45f, 0.8f, 1f);
            pedMpb ??= new MaterialPropertyBlock();
            pedMpb.SetColor("_BaseColor", new Color(col.r, col.g, col.b, 0.85f)); pedRing.SetPropertyBlock(pedMpb);
            pedMpb.SetColor("_BaseColor", new Color(0, 0, 0, 0.75f)); pedShadow.SetPropertyBlock(pedMpb);
            // rim light: behind the model as seen from the preview camera, tinted with the rarity
            if (showCam)
            {
                var cf = showCam.transform.forward;
                rimLight.transform.position = bb.center + cf * (shownRadius * 3f) + Vector3.up * shownRadius * 1.5f;
                rimLight.transform.rotation = Quaternion.LookRotation(bb.center - rimLight.transform.position);
            }
            rimLight.color = Color.Lerp(Color.white, col, 0.45f); rimLight.intensity = 6f;
        }
        void ShowModel(Cos c, int level = 0)
        {
            if (!showRoot) { var g = GameObject.Find("Showcase"); if (!g) return; showRoot = g.transform; showCam = showRoot.GetComponentInChildren<Camera>(true); }
            string want = KindModel(c);
            if (c.kind == CosKind.Backpack && !showRoot.Find("p_bag")) want = "l_backpack";
            shown = null; shownCos = c; previewLv = level;
            foreach (Transform t in showRoot) { if (t.GetComponent<Camera>() || t.GetComponent<Light>() || t == pedestal) continue; bool on = t.name == want; t.gameObject.SetActive(on); if (on) shown = t.gameObject; }
            if (shown) { shown.transform.localRotation = Quaternion.Euler(0, 200, 0); KryvonSkins.Apply(shown, c.tex == null && c.glow == Color.clear && c.source != "evolve" ? null : c, level); }
            Stage(c);
            // the live preview: lighter on integrated / mobile GPUs (900 px with 4x MSAA hung the Intel UHD driver)
            if (!showRT) { bool weak = KryvonGraphics.WeakGpu; showRT = new RenderTexture(weak ? 640 : 900, weak ? 640 : 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = weak ? 1 : 4 }; }
            if (showCam) { showCam.targetTexture = showRT; showCam.enabled = shown; }
        }
        void CosArt(RectTransform parent, Cos c, float x, float y, float w, float h)
        {
            // card art: the skin's pattern with the gun / item silhouette on top
            var sw = new GameObject("Swatch", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); sw.transform.SetParent(parent, false); sw.raycastTarget = false;
            var tex = KryvonSkins.Tex(c.source == "evolve" ? "digital" : c.tex); sw.texture = tex; sw.color = tex ? (c.source == "evolve" ? new Color(0.72f, 0.86f, 1f) : Color.white) : new Color(0.2f, 0.22f, 0.25f);
            sw.uvRect = new Rect(0, 0, w / 220f, h / 220f);
            Place(sw.rectTransform, new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), new Vector2(0, 1));
            var shade = Panel(sw.rectTransform, "Shade", new Color(0, 0, 0, 0.35f)); Fill(shade);
            string icon = c.kind == CosKind.Weapon ? GunIcon(KryvonSkins.GunModel(c.target)) : c.kind == CosKind.Parachute ? "c_chute" : c.kind == CosKind.Backpack ? "c_bag" : c.kind == CosKind.Helmet ? "c_helmet" : c.kind == CosKind.Jet ? "c_chute" : c.kind == CosKind.Emote ? "sprint" : "c_car";
            var t = Resources.Load<Texture2D>("UI/Icons/" + icon);
            if (t)
            {
                var ic = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); ic.transform.SetParent(parent, false); ic.raycastTarget = false; ic.texture = t;
                bool wide = icon.StartsWith("w_"); if (wide) ic.uvRect = new Rect(0, 0.3f, 1, 0.4f);
                float iw = w * 0.86f, ih = wide ? iw * 0.4f : Mathf.Min(h * 0.6f, iw);
                if (!wide) iw = ih;
                Place(ic.rectTransform, new Vector2(0, 1), new Vector2(x + (w - iw) / 2, -(y + (h - ih) / 2)), new Vector2(iw, ih), new Vector2(0, 1));
            }
            if (c.source == "evolve" || c.source == "pass")
            {
                var b = Box(parent, x + 8, y + 8, 120, 30, c.source == "pass" ? new Color(0.85f, 0.55f, 0.1f) : new Color(0.3f, 0.6f, 1f));
                TextAt(b, c.source == "pass" ? "PASS" : "EVOLVING", 0, 0, 120, 30, 16, Color.white, TextAnchor.MiddleCenter, display);
            }
        }

        // ---------- pages ----------
        void CosPage(RectTransform b, CosKind k, bool store)
        {
            float y0 = 150; string target = null;
            if (k == CosKind.Weapon)
            {
                gunPick = Mathf.Clamp(gunPick, 0, KryvonSkins.Guns.Length - 1);
                for (int g = 0; g < KryvonSkins.Guns.Length; g++)
                {
                    int gi = g; bool on = g == gunPick;
                    var chip = Box(b, 380 + (g % 8) * 124, 150 + (g / 8) * 46, 118, 40, on ? GoldDeep : CardBg);
                    TextAt(chip, KryvonSkins.Guns[g].name, 0, 0, 118, 40, 17, on ? Dark : Color.white, TextAnchor.MiddleCenter);
                    Btn(chip, () => { gunPick = gi; selItem = 0; FillSheet(store ? 1 : 0); });
                }
                y0 = 300; target = KryvonSkins.Guns[gunPick].id;
            }
            var items = CosItems(k, target, !store);
            Grid(b, items, store, y0);
        }

        /// right-hand preview for a cosmetic: live model on the pedestal; for the evolving skin, one button per level that
        /// shows that level on the model (level 7 = ABSOLUTE ZERO with its kill effect), the level owned and UPGRADE
        void CosPreview(RectTransform p, Item it, bool store)
        {
            int show = it.cos.source == "evolve" ? (previewLv > 0 && shownCos == it.cos ? previewLv : KryvonSkins.Level(it.cos)) : 0;
            ShowModel(it.cos, show);
            var img = new GameObject("Model", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); img.transform.SetParent(p, false); img.raycastTarget = false;
            img.texture = showRT; Place(img.rectTransform, new Vector2(0, 1), new Vector2(10, -10), new Vector2(440, 440), new Vector2(0, 1));
            if (it.cos.source == "evolve")
            {
                int lv = KryvonSkins.Level(it.cos);
                var ice = new Color(0.45f, 0.75f, 1f);
                TextAt(p, $"LEVEL {show} · {KryvonSkins.GlacierLevels[show - 1]}", 30, 400, 400, 30, 22, ice, TextAnchor.MiddleLeft, display);
                if (show >= KryvonSkins.MaxLevel) TextAt(p, "KILL EFFECT: ICE BURST", 30, 428, 400, 24, 17, new Color(0.8f, 0.93f, 1f));
                // one chip per level: tap to preview it; owned levels are solid, locked ones outlined
                for (int s = 1; s <= KryvonSkins.MaxLevel; s++)
                {
                    int ls = s; bool own = s <= lv, on = s == show;
                    var chip = Box(p, 30 + (s - 1) * 57, 452, 52, 40, own ? new Color(ice.r, ice.g, ice.b, on ? 0.95f : 0.45f) : new Color(1, 1, 1, on ? 0.18f : 0.06f));
                    if (sharp) { chip.GetComponent<Image>().sprite = sharp; Edge(chip, on ? Amber : new Color(ice.r, ice.g, ice.b, 0.5f)); }
                    TextAt(chip, s.ToString(), 0, 0, 52, 40, 20, own ? Dark : Color.white, TextAnchor.MiddleCenter, display);
                    Btn(chip, () => { previewLv = ls; FillSheet(store ? 1 : 0); });
                }
                if (it.owned && lv < KryvonSkins.MaxLevel)
                {
                    var up = Box(p, 250, 700, 180, 54, new Color(0.3f, 0.6f, 1f));
                    TextAt(up, $"UPGRADE ● {KryvonSkins.UpgradeCost:N0}", 0, 0, 180, 54, 18, Color.white, TextAnchor.MiddleCenter, display);
                    Btn(up, () => { toast = KryvonSkins.Upgrade(it.cos) ? $"{it.name} reached level {KryvonSkins.Level(it.cos)}!" : "Not enough coins – finish missions to earn more."; previewLv = 0; RefreshWallet(); FillSheet(store ? 1 : 0); });
                }
            }
            if (it.cos.source == "pass" && !it.owned) TextAt(p, PassWhere(it.cos.id), 30, 700, 400, 40, 20, Gold);
        }

        void Update2()
        {
            if (shown && shown.activeInHierarchy) shown.transform.Rotate(0, 28f * Time.deltaTime, 0, Space.World);
            // max-level evolving skin (shown on the pedestal): ice sparkles keep drifting up around it
            if (shown && shown.activeInHierarchy && showCam && showCam.enabled && shownCos != null && shownCos.source == "evolve" && Time.time >= nextSparkle
                && (previewLv > 0 ? previewLv : KryvonSkins.Level(shownCos)) >= KryvonSkins.MaxLevel)
            { nextSparkle = Time.time + 0.5f; KryvonFX.Sparkle(shownCenter, shownRadius * 0.9f, new Color(0.5f, 0.85f, 1f), shown.layer); }
            if (showCam && showCam.enabled) { bool any = false; foreach (var s in sheets) if (s && s.gameObject.activeSelf) any = true; if (!any) showCam.enabled = false; }
        }
    }
}
