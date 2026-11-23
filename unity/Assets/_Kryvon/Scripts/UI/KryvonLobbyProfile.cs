// The player card (tap your name in the lobby), laid out like the owner's reference:
//   left  – the player's own photo in a glowing ring (taken with the phone / laptop camera, or an operator portrait),
//           the rank badge (chevrons → stars → crown as you climb), RANK: NAME and three stars = progress to the next rank
//   right – name + UID, LINK ACCOUNT / EDIT PROFILE, CAREER STATS: a K/D gauge, the K/D ratio, DETAILS, and tiles for
//           MATCHES PLAYED · WINS · TOP 10
// Design system: charcoal glass, silver tech edges with corner brackets, gold for the main actions.
// The photo is stored on the device (persistentDataPath/avatar.png) and also shows on the lobby name card.
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Kryvon
{
    /// the player's picture: a photo taken in the game, an operator portrait, or (default) the selected hero
    public static class KryvonAvatar
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "avatar.png");
        static Texture2D cached;
        public static Texture2D Photo
        {
            get
            {
                if (cached) return cached;
                try { if (File.Exists(FilePath)) { var t = new Texture2D(2, 2, TextureFormat.RGB24, false); if (t.LoadImage(File.ReadAllBytes(FilePath))) cached = t; } } catch { }
                return cached;
            }
        }
        public static void SavePhoto(Texture2D t) { try { File.WriteAllBytes(FilePath, t.EncodeToPNG()); } catch { } cached = t; PlayerPrefs.SetString("kryvon.avatar", "photo"); PlayerPrefs.Save(); }
        public static void UseOperator(string id) { PlayerPrefs.SetString("kryvon.avatar", "op:" + id); PlayerPrefs.Save(); }
        public static void Clear() { try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { } cached = null; PlayerPrefs.DeleteKey("kryvon.avatar"); PlayerPrefs.Save(); }
        /// what to draw and which part of it (operator portraits are cropped to the face)
        public static (Texture tex, Rect uv) Current(System.Func<string, Texture2D> portrait, string heroId)
        {
            var a = PlayerPrefs.GetString("kryvon.avatar", "");
            if (a == "photo" && Photo) return (Photo, new Rect(0, 0, 1, 1));
            string id = a.StartsWith("op:") ? a.Substring(3) : heroId;
            return (portrait(id), new Rect(0.34f, 0.76f, 0.32f, 0.22f));        // the face of the outfit card
        }
    }

    public partial class KryvonLobby
    {
        RawImage cardAvatar, lobbyAvatar; Text cardName, cardUid, cardRank, cardStars, kdBig, kdGauge, detailsText; Image kdArc; Text[] tileVals;
        RectTransform rankBadge, details, avatarPicker, camScreen; RawImage camView; WebCamTexture cam; bool camFront;
        Sprite circleSp, ringSp;
        static readonly Color Silver = new(0.72f, 0.76f, 0.82f, 0.9f), Ice = new(0.55f, 0.82f, 1f, 1f);

        RectTransform Glass2(RectTransform parent, string n, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, Color edge)
        {
            var r = Panel(parent, n, fill, sharp); Place(r, anchor, pos, size, anchor); Edge(r, edge); return r;
        }
        void Brackets(RectTransform r, Color c, float len = 34, float th = 4)
        {   // tech corner brackets just outside the card
            foreach (var (ax, ay) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) })
            {
                float sx = ax == 0 ? -1 : 1, sy = ay == 0 ? -1 : 1;
                var h = Panel(r, "Bracket", c); h.anchorMin = h.anchorMax = new Vector2(ax, ay); h.pivot = new Vector2(ax, ay); h.sizeDelta = new Vector2(len, th); h.anchoredPosition = new Vector2(sx * 6, sy * 6);
                var v = Panel(r, "Bracket", c); v.anchorMin = v.anchorMax = new Vector2(ax, ay); v.pivot = new Vector2(ax, ay); v.sizeDelta = new Vector2(th, len); v.anchoredPosition = new Vector2(sx * 6, sy * 6);
            }
        }
        RawImage Avatar(RectTransform parent, float size, Vector2 pos, Vector2 anchor)
        {   // a round photo: circle mask + the picture
            var m = new GameObject("AvatarMask", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>(); m.SetParent(parent, false);
            var mi = m.GetComponent<Image>(); mi.sprite = circleSp; mi.raycastTarget = false; m.GetComponent<Mask>().showMaskGraphic = false;
            Place(m, anchor, pos, new Vector2(size, size), anchor);
            var bg = Panel(m, "Bg", new Color(0.1f, 0.11f, 0.12f)); Fill(bg);
            var img = new GameObject("Photo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); img.transform.SetParent(m, false); img.raycastTarget = false; Fill(img.rectTransform);
            return img;
        }
        void ApplyAvatar()
        {
            var (tex, uv) = KryvonAvatar.Current(Thumb, heroes.Length > 0 ? heroes[hero].id : "hamza");
            foreach (var a in new[] { cardAvatar, lobbyAvatar }) if (a) { a.texture = tex; a.uvRect = uv; }
        }

        // ---------- the card ----------
        void BuildAccount()
        {
            circleSp ??= MakeRounded(128, 63); ringSp ??= MakeFrame(128, 63, 7f);
            account = FullScreen("Account", new Color(0.01f, 0.01f, 0.015f, 0.8f));
            var card = Glass2(account, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 700), new Color(0.07f, 0.075f, 0.08f, 0.96f), Silver);
            Brackets(card, new Color(0.9f, 0.93f, 0.97f, 0.95f));
            var topLine = Panel(card, "TopLine", new Color(Ice.r, Ice.g, Ice.b, 0.35f)); topLine.anchorMin = new Vector2(0.3f, 1); topLine.anchorMax = new Vector2(0.95f, 1); topLine.sizeDelta = new Vector2(0, 2); topLine.anchoredPosition = new Vector2(0, -1);

            // ---- left: photo, rank ----
            var left = Glass2(card, "Left", new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(380, 652), new Color(1, 1, 1, 0.025f), new Color(Silver.r, Silver.g, Silver.b, 0.45f));
            var halo = Panel(left, "Halo", new Color(Ice.r, Ice.g, Ice.b, 0.3f), glow); Place(halo, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(380, 360), new Vector2(0.5f, 1));
            var ring = Panel(left, "Ring", Silver, ringSp); Place(ring, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(286, 286), new Vector2(0.5f, 1)); ring.GetComponent<Image>().type = Image.Type.Simple;
            var ring2 = Panel(left, "RingIce", new Color(Ice.r, Ice.g, Ice.b, 0.85f), ringSp); Place(ring2, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(298, 298), new Vector2(0.5f, 1)); ring2.GetComponent<Image>().type = Image.Type.Simple;
            cardAvatar = Avatar(left, 266, new Vector2(0, -36), new Vector2(0.5f, 1));
            var change = Panel(left, "ChangePhoto", Gold, sharp); Place(change, new Vector2(0.5f, 1), new Vector2(0, -296), new Vector2(210, 40), new Vector2(0.5f, 1));
            Label(change, "CHANGE PHOTO", bodyBold, 17, Dark, TextAnchor.MiddleCenter); Btn(change, OpenAvatarPicker);
            rankBadge = new GameObject("RankBadge", typeof(RectTransform)).GetComponent<RectTransform>(); rankBadge.SetParent(left, false); Place(rankBadge, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(170, 170), new Vector2(0.5f, 0.5f));
            cardRank = Label(left, "", display, 30, Color.white, TextAnchor.MiddleCenter); Place(cardRank.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(360, 40), new Vector2(0.5f, 0.5f));
            cardStars = Label(left, "", display, 30, Color.white, TextAnchor.MiddleCenter); Place(cardStars.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(360, 36), new Vector2(0.5f, 0.5f));
            // KRAYVON monogram in the corner
            var mono = Panel(card, "Mono", new Color(Silver.r, Silver.g, Silver.b, 0.9f), sharp); Place(mono, new Vector2(0, 0), new Vector2(18, 18), new Vector2(64, 64), new Vector2(0, 0)); mono.localRotation = Quaternion.Euler(0, 0, 45); mono.anchoredPosition = new Vector2(40, 0);
            Label(mono, "K", display, 34, Dark, TextAnchor.MiddleCenter).rectTransform.localRotation = Quaternion.Euler(0, 0, -45);

            // ---- right: name, actions ----
            cardName = Label(card, "", display, 76, Color.white, TextAnchor.MiddleLeft); Place(cardName.rectTransform, new Vector2(0, 1), new Vector2(436, -34), new Vector2(560, 84), new Vector2(0, 1));
            cardName.resizeTextForBestFit = true; cardName.resizeTextMinSize = 30; cardName.resizeTextMaxSize = 76;
            cardUid = Label(card, "", bodyBold, 22, Grey, TextAnchor.MiddleLeft); Place(cardUid.rectTransform, new Vector2(0, 1), new Vector2(440, -118), new Vector2(560, 30), new Vector2(0, 1));
            RectTransform Action(string glyph, string text, float x, System.Action a)
            {
                var b = Glass2(card, "Action", new Vector2(1, 1), new Vector2(x, -30), new Vector2(122, 104), new Color(1, 1, 1, 0.04f), new Color(Silver.r, Silver.g, Silver.b, 0.5f));
                var g = Label(b, glyph, display, 40, Color.white, TextAnchor.UpperCenter); g.rectTransform.offsetMax = new Vector2(0, -6);
                var t = Label(b, text, bodyBold, 15, Color.white, TextAnchor.LowerCenter); t.rectTransform.offsetMin = new Vector2(0, 8);
                Btn(b, a); return b;
            }
            Action("⇄", "LINK\nACCOUNT", -184, () => accountMsg.text = LinkSoon);
            Action("✎", "EDIT\nPROFILE", -50, () => OpenProfile(true));
            var close = Glass2(card, "Close", new Vector2(1, 1), new Vector2(26, 26), new Vector2(62, 62), new Color(0.1f, 0.1f, 0.11f, 0.95f), Gold);
            Label(close, "✕", display, 34, Gold, TextAnchor.MiddleCenter); Btn(close, () => account.gameObject.SetActive(false));

            // ---- career stats ----
            var stats = Glass2(card, "Stats", new Vector2(0, 1), new Vector2(436, -160), new Vector2(838, 470), new Color(1, 1, 1, 0.03f), new Color(Silver.r, Silver.g, Silver.b, 0.45f));
            var hdr = Panel(stats, "Header", new Color(1, 1, 1, 0.07f)); hdr.anchorMin = new Vector2(0, 1); hdr.anchorMax = new Vector2(1, 1); hdr.pivot = new Vector2(0.5f, 1); hdr.sizeDelta = new Vector2(-4, 56); hdr.anchoredPosition = new Vector2(0, -2);
            Label(hdr, "CAREER STATS  (SEASON 1)", display, 30, Color.white).rectTransform.offsetMin = new Vector2(22, 0);
            // K/D gauge: a ring that fills with the ratio (5.0 = full)
            var gBack = Panel(stats, "GaugeBack", new Color(1, 1, 1, 0.12f), ringSp); Place(gBack, new Vector2(0, 1), new Vector2(40, -84), new Vector2(150, 150), new Vector2(0, 1)); gBack.GetComponent<Image>().type = Image.Type.Simple;
            var gFill = Panel(gBack, "Arc", Ice, ringSp); Fill(gFill); kdArc = gFill.GetComponent<Image>(); kdArc.type = Image.Type.Filled; kdArc.fillMethod = Image.FillMethod.Radial360; kdArc.fillOrigin = (int)Image.Origin360.Top; kdArc.fillClockwise = true;
            kdGauge = Label(gBack, "", display, 34, Color.white, TextAnchor.MiddleCenter); kdGauge.lineSpacing = 0.8f;
            var kl = Label(stats, "K/D RATIO", bodyBold, 22, Grey); Place(kl.rectTransform, new Vector2(0, 1), new Vector2(228, -98), new Vector2(300, 30), new Vector2(0, 1));
            kdBig = Label(stats, "", display, 86, Color.white); Place(kdBig.rectTransform, new Vector2(0, 1), new Vector2(226, -124), new Vector2(360, 96), new Vector2(0, 1)); kdBig.supportRichText = true;
            var det = Glass2(stats, "Details", new Vector2(1, 1), new Vector2(-34, -132), new Vector2(170, 52), new Color(1, 1, 1, 0.06f), new Color(Silver.r, Silver.g, Silver.b, 0.7f));
            Label(det, "DETAILS", display, 26, Color.white, TextAnchor.MiddleCenter); Btn(det, () => { details.gameObject.SetActive(!details.gameObject.activeSelf); });
            var sep = Panel(stats, "Sep", new Color(1, 1, 1, 0.1f)); sep.anchorMin = new Vector2(0, 1); sep.anchorMax = new Vector2(1, 1); sep.sizeDelta = new Vector2(-50, 2); sep.anchoredPosition = new Vector2(0, -254);
            // tiles
            string[] glyphs = { "⚔", "♛", "✪" }, names = { "MATCHES PLAYED", "WINS", "TOP 10" };
            tileVals = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var t = Glass2(stats, "Tile", new Vector2(0, 0), new Vector2(24 + i * 268, 22), new Vector2(252, 178), new Color(0, 0, 0, 0.25f), new Color(Silver.r, Silver.g, Silver.b, 0.55f));
                var corner = Panel(t, "Corner", new Color(Silver.r, Silver.g, Silver.b, 0.9f)); Place(corner, new Vector2(1, 0), new Vector2(-4, 4), new Vector2(16, 3), new Vector2(1, 0));
                var corner2 = Panel(t, "Corner", new Color(Silver.r, Silver.g, Silver.b, 0.9f)); Place(corner2, new Vector2(1, 0), new Vector2(-4, 4), new Vector2(3, 16), new Vector2(1, 0));
                var g = Label(t, glyphs[i], display, 42, new Color(0.85f, 0.88f, 0.92f), TextAnchor.UpperCenter); g.rectTransform.offsetMax = new Vector2(0, -10);
                var n = Label(t, names[i], bodyBold, 21, new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleCenter); n.rectTransform.offsetMin = new Vector2(0, 30);
                tileVals[i] = Label(t, "", display, 54, Color.white, TextAnchor.LowerCenter); tileVals[i].rectTransform.offsetMin = new Vector2(0, 8);
            }
            // details overlay (kills, deaths, headshots, damage, best place, rank points)
            details = Panel(stats, "DetailsPanel", new Color(0.05f, 0.055f, 0.06f, 0.97f), sharp); details.anchorMin = Vector2.zero; details.anchorMax = Vector2.one; details.offsetMin = new Vector2(4, 4); details.offsetMax = new Vector2(-4, -60);
            Edge(details, new Color(Ice.r, Ice.g, Ice.b, 0.5f));
            detailsText = Label(details, "", bodyBold, 28, Color.white, TextAnchor.MiddleLeft); detailsText.rectTransform.offsetMin = new Vector2(40, 20); detailsText.rectTransform.offsetMax = new Vector2(-40, -20); detailsText.supportRichText = true; detailsText.lineSpacing = 1.2f;
            var back = Glass2(details, "Back", new Vector2(1, 0), new Vector2(-24, 22), new Vector2(150, 50), new Color(1, 1, 1, 0.06f), Silver); Label(back, "BACK", display, 26, Color.white, TextAnchor.MiddleCenter);
            Btn(back, () => details.gameObject.SetActive(false));
            details.gameObject.SetActive(false);

            accountMsg = Label(card, "", body, 19, new Color(1f, 0.82f, 0.5f), TextAnchor.MiddleCenter); Place(accountMsg.rectTransform, new Vector2(0, 0), new Vector2(436, 20), new Vector2(838, 34), new Vector2(0, 0));
            var so = Panel(card, "SignOut", new Color(0, 0, 0, 0.001f)); Place(so, new Vector2(1, 0), new Vector2(-26, 14), new Vector2(150, 34), new Vector2(1, 0));
            Label(so, "SIGN OUT", bodyBold, 18, new Color(0.9f, 0.45f, 0.4f), TextAnchor.MiddleRight); Btn(so, SignOut);
            account.gameObject.SetActive(false);
        }

        void OpenAccount()
        {
            int matches = KryvonWallet.Stat("matches"), wins = KryvonWallet.Stat("wins"), top10 = KryvonWallet.Stat("top10"), kills = KryvonWallet.Stat("kills"), deaths = KryvonWallet.Stat("deaths");
            int hits = KryvonWallet.Stat("hits"), heads = KryvonWallet.Stat("headshots"), dmg = KryvonWallet.Stat("damage"), best = PlayerPrefs.GetInt("s.best", 0);
            float kd = kills / (float)Mathf.Max(1, deaths);
            cardName.text = PlayerName;
            var acc = PlayerPrefs.GetString("kryvon.account", "guest");
            cardUid.text = $"UID: {PlayerId}   ·   {PlayerPrefs.GetString("kryvon.country", "")}   ·   {(acc == "guest" ? "GUEST" : acc.ToUpperInvariant())}";
            kdGauge.text = $"{kd:0.00}\n<size=20>K/D</size>"; kdGauge.supportRichText = true;
            kdArc.fillAmount = Mathf.Clamp01(kd / 5f); kdArc.color = Color.Lerp(Ice, Gold, Mathf.Clamp01((kd - 1f) / 3f));
            kdBig.text = $"{kd:0.00} <size=40>K/D</size>" + (kd >= 1f ? "  <color=#5FD38A><size=40>▲</size></color>" : "");
            tileVals[0].text = matches.ToString("N0"); tileVals[1].text = wins.ToString("N0"); tileVals[2].text = top10.ToString("N0");
            detailsText.text =
                $"<color=#B0B0B0>KILLS</color>   {kills:N0}        <color=#B0B0B0>DEATHS</color>   {deaths:N0}\n" +
                $"<color=#B0B0B0>HEADSHOTS</color>   {(hits > 0 ? Mathf.RoundToInt(100f * heads / hits) : 0)}%        <color=#B0B0B0>DAMAGE</color>   {dmg:N0}\n" +
                $"<color=#B0B0B0>BEST PLACE</color>   {(best > 0 && best < 99 ? "#" + best : "–")}        <color=#B0B0B0>RANK POINTS</color>   {RankPoints():N0}";
            // rank: badge, name, three stars = progress towards the next rank
            int pts = RankPoints(), k = 0; for (int i = 0; i < Ranks.Length; i++) if (pts >= Ranks[i].points) k = i;
            int lo = Ranks[k].points, hi = k + 1 < Ranks.Length ? Ranks[k + 1].points : lo + 1;
            int filled = k + 1 >= Ranks.Length ? 3 : 1 + Mathf.Clamp(Mathf.FloorToInt(3f * (pts - lo) / Mathf.Max(1, hi - lo)), 0, 2);
            cardRank.text = "RANK: " + Ranks[k].name;
            cardStars.text = new string('★', filled) + new string('☆', 3 - filled); cardStars.color = RankColor(k);
            DrawRankBadge(k);
            ApplyAvatar();
            accountMsg.text = ""; details.gameObject.SetActive(false); account.gameObject.SetActive(true);
        }

        static Color RankColor(int k) => k <= 1 ? new Color(0.78f, 0.52f, 0.32f) : k <= 3 ? new Color(0.8f, 0.83f, 0.88f) : k <= 5 ? new Color(0.83f, 0.69f, 0.22f) : new Color(0.68f, 0.5f, 1f);
        /// the badge: a diamond in the rank colour with chevrons (Private … Lieutenant), stars (Captain … Colonel) or a crown (Commander)
        void DrawRankBadge(int k)
        {
            foreach (Transform c in rankBadge) Destroy(c.gameObject);
            var col = RankColor(k);
            var glowB = Panel(rankBadge, "Glow", new Color(col.r, col.g, col.b, 0.35f), glow); Place(glowB, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 240), new Vector2(0.5f, 0.5f));
            var d = Panel(rankBadge, "Diamond", col, sharp); Place(d, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112), new Vector2(0.5f, 0.5f)); d.localRotation = Quaternion.Euler(0, 0, 45);
            var inner = Panel(d, "Inner", new Color(0.08f, 0.085f, 0.1f, 0.95f), sharp); Fill(inner, 8);
            Edge(inner, new Color(col.r, col.g, col.b, 0.8f));
            var holder = new GameObject("Mark", typeof(RectTransform)).GetComponent<RectTransform>(); holder.SetParent(rankBadge, false); Place(holder, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120), new Vector2(0.5f, 0.5f));
            void Bar(Vector2 pos, float w, float h, float rot) { var b = Panel(holder, "Bar", col); Place(b, new Vector2(0.5f, 0.5f), pos, new Vector2(w, h), new Vector2(0.5f, 0.5f)); b.localRotation = Quaternion.Euler(0, 0, rot); }
            void Chevron(float y) { Bar(new Vector2(-15, y), 40, 9, 32); Bar(new Vector2(15, y), 40, 9, -32); }
            if (k <= 3) { int n = Mathf.Min(k + 1, 3); for (int i = 0; i < n; i++) Chevron(14 - i * 16 + (n - 1) * 8); if (k == 3) Bar(new Vector2(0, -34), 52, 7, 0); }
            else if (k <= 6)
            {
                int n = k - 3; var s = Label(holder, new string('★', n), display, n == 1 ? 64 : n == 2 ? 44 : 34, col, TextAnchor.MiddleCenter); s.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else
            {   // commander: a crown
                Bar(new Vector2(0, -12), 64, 12, 0);
                Bar(new Vector2(-24, 6), 12, 30, 12); Bar(new Vector2(24, 6), 12, 30, -12); Bar(new Vector2(0, 12), 12, 38, 0);
                var gem = Panel(holder, "Gem", Color.white, sharp); Place(gem, new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(12, 12), new Vector2(0.5f, 0.5f)); gem.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }

        // ---------- the picture: camera photo or an operator portrait ----------
        void OpenAvatarPicker()
        {
            if (!avatarPicker)
            {
                avatarPicker = FullScreen("AvatarPicker", new Color(0.01f, 0.01f, 0.015f, 0.86f));
                var box = Glass2(avatarPicker, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 560), new Color(0.07f, 0.075f, 0.08f, 0.97f), Silver);
                Brackets(box, new Color(0.9f, 0.93f, 0.97f, 0.95f));
                Label(box, "PROFILE PICTURE", display, 50, Color.white, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -24);
                var take = Panel(box, "Take", Gold, sharp); Place(take, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(460, 80), new Vector2(0.5f, 1));
                Label(take, "◉   TAKE A PHOTO", display, 38, Dark, TextAnchor.MiddleCenter); Btn(take, () => StartCoroutine(OpenCamera()));
                Label(box, "OR USE AN OPERATOR", bodyBold, 20, Grey, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -214);
                string[] ops = { "sara", "hamza", "recon", "medic", "engineer", "heavy" };
                for (int i = 0; i < ops.Length; i++)
                {
                    string id = ops[i]; var holder = new GameObject("Op", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); holder.SetParent(box, false);
                    holder.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f); Place(holder, new Vector2(0.5f, 1), new Vector2((i - 2.5f) * 160, -270), new Vector2(140, 140), new Vector2(0.5f, 1));
                    var rg = Panel(holder, "Ring", Silver, ringSp); Fill(rg); rg.GetComponent<Image>().type = Image.Type.Simple;
                    var face = Avatar(holder, 124, Vector2.zero, new Vector2(0.5f, 0.5f)); face.texture = Thumb(id); face.uvRect = new Rect(0.34f, 0.76f, 0.32f, 0.22f);
                    Btn(holder, () => { KryvonAvatar.UseOperator(id); ApplyAvatar(); avatarPicker.gameObject.SetActive(false); });
                }
                var rm = Glass2(box, "Remove", new Vector2(0, 0), new Vector2(40, 30), new Vector2(240, 56), new Color(1, 1, 1, 0.05f), new Color(0.9f, 0.45f, 0.4f, 0.8f));
                Label(rm, "REMOVE PHOTO", bodyBold, 20, new Color(0.95f, 0.55f, 0.5f), TextAnchor.MiddleCenter); Btn(rm, () => { KryvonAvatar.Clear(); ApplyAvatar(); avatarPicker.gameObject.SetActive(false); });
                var cl = Glass2(box, "Close", new Vector2(1, 0), new Vector2(-40, 30), new Vector2(200, 56), new Color(1, 1, 1, 0.05f), Silver);
                Label(cl, "CLOSE", display, 28, Color.white, TextAnchor.MiddleCenter); Btn(cl, () => avatarPicker.gameObject.SetActive(false));
            }
            avatarPicker.SetAsLastSibling(); avatarPicker.gameObject.SetActive(true);
        }

        IEnumerator OpenCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                for (float t = 0; t < 15f && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera); t += Time.unscaledDeltaTime) yield return null;
            }
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera)) { accountMsg.text = "Camera permission was not given."; yield break; }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { accountMsg.text = "Camera permission was not given."; yield break; }
#endif
            var devs = WebCamTexture.devices;
            if (devs.Length == 0) { accountMsg.text = "No camera found on this device – pick an operator picture instead."; yield break; }
            int pick = 0; for (int i = 0; i < devs.Length; i++) if (devs[i].isFrontFacing) { pick = i; break; }
            camFront = devs[pick].isFrontFacing;
            cam = new WebCamTexture(devs[pick].name, 960, 960, 30); cam.Play();
            if (!camScreen)
            {
                camScreen = FullScreen("Camera", new Color(0, 0, 0, 0.95f));
                var ringC = Panel(camScreen, "Ring", Gold, ringSp); Place(ringC, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(620, 620), new Vector2(0.5f, 0.5f)); ringC.GetComponent<Image>().type = Image.Type.Simple;
                camView = Avatar(camScreen, 600, new Vector2(0, 40), new Vector2(0.5f, 0.5f));
                var shot = Panel(camScreen, "Shoot", Gold, sharp); Place(shot, new Vector2(0.5f, 0), new Vector2(120, 40), new Vector2(320, 80), new Vector2(0.5f, 0));
                Label(shot, "◉  CAPTURE", display, 38, Dark, TextAnchor.MiddleCenter); Btn(shot, Capture);
                var cc = Glass2(camScreen, "Cancel", new Vector2(0.5f, 0), new Vector2(-200, 40), new Vector2(240, 80), new Color(1, 1, 1, 0.06f), Silver);
                Label(cc, "CANCEL", display, 34, Color.white, TextAnchor.MiddleCenter); Btn(cc, StopCamera);
            }
            camView.texture = cam; camScreen.SetAsLastSibling(); camScreen.gameObject.SetActive(true);
            while (cam && cam.isPlaying && camScreen.gameObject.activeSelf)
            {   // keep the preview upright and mirrored like a mirror for the front camera; crop to a square
                camView.rectTransform.localEulerAngles = new Vector3(0, 0, -cam.videoRotationAngle);
                camView.rectTransform.localScale = new Vector3(camFront ? -1 : 1, cam.videoVerticallyMirrored ? -1 : 1, 1);
                if (cam.width > 16) { float a = cam.width / (float)cam.height; camView.uvRect = a >= 1 ? new Rect((1 - 1 / a) / 2, 0, 1 / a, 1) : new Rect(0, (1 - a) / 2, 1, a); }
                yield return null;
            }
        }
        void StopCamera() { if (cam) { cam.Stop(); Destroy(cam); cam = null; } if (camScreen) camScreen.gameObject.SetActive(false); }
        void Capture()
        {
            if (!cam || !cam.isPlaying || cam.width < 16) return;
            int w = cam.width, h = cam.height, s = Mathf.Min(w, h);
            var src = cam.GetPixels((w - s) / 2, (h - s) / 2, s, s);
            // turn the pixels the way the preview showed them (rotation, mirror)
            var dst = new Color[s * s]; int rot = ((cam.videoRotationAngle % 360) + 360) % 360;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                int sx = x, sy = y;
                if (rot == 90) { sx = y; sy = s - 1 - x; } else if (rot == 180) { sx = s - 1 - x; sy = s - 1 - y; } else if (rot == 270) { sx = s - 1 - y; sy = x; }
                if (camFront) sx = s - 1 - sx;
                if (cam.videoVerticallyMirrored) sy = s - 1 - sy;
                dst[y * s + x] = src[Mathf.Clamp(sy, 0, s - 1) * s + Mathf.Clamp(sx, 0, s - 1)];
            }
            var full = new Texture2D(s, s, TextureFormat.RGB24, false); full.SetPixels(dst); full.Apply();
            var rt = RenderTexture.GetTemporary(512, 512); Graphics.Blit(full, rt);
            var outT = new Texture2D(512, 512, TextureFormat.RGB24, false); var prev = RenderTexture.active; RenderTexture.active = rt;
            outT.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); outT.Apply(); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Destroy(full);
            KryvonAvatar.SavePhoto(outT); ApplyAvatar(); StopCamera();
            if (avatarPicker) avatarPicker.gameObject.SetActive(false);
            accountMsg.text = "Profile picture updated.";
        }
    }
}
