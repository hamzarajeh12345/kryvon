// KRAYVON sign-in and player profile: the first launch shows a sign-in screen (guest now; Google, Facebook, Apple and
// e-mail linking switch on with the online server), then "create your profile" (nickname, country, gender).
// Tapping the player card in the lobby opens the profile: ID, link status, edit, sign out.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kryvon
{
    public partial class KryvonLobby
    {
        public static bool HasProfile => PlayerPrefs.HasKey("kryvon.name");
        public static string PlayerName => PlayerPrefs.GetString("kryvon.name", "PLAYER");
        public static string PlayerId
        {
            get
            {
                var id = PlayerPrefs.GetString("kryvon.uid", "");
                if (id == "") { id = Random.Range(100000000, 999999999).ToString(); PlayerPrefs.SetString("kryvon.uid", id); PlayerPrefs.Save(); }
                return id;
            }
        }
        static bool skipSplash;
        /// the Boot scene already showed the real loading screen: the lobby skips its own
        public static void SkipNextSplash() => skipSplash = true;
        static readonly string[] Countries =
        {
            "JORDAN", "PALESTINE", "SYRIA", "LEBANON", "IRAQ", "SAUDI ARABIA",
            "EGYPT", "UAE", "KUWAIT", "QATAR", "BAHRAIN", "OMAN",
            "YEMEN", "MOROCCO", "ALGERIA", "TUNISIA", "LIBYA", "SUDAN",
            "TÜRKIYE", "USA", "UK", "GERMANY", "FRANCE", "OTHER",
        };
        static readonly string[] Genders = { "MALE", "FEMALE" };
        const string LinkSoon = "Account linking turns on with the KRAYVON online server.\nPlay as a guest now – you can link this profile later from your player card.";

        RectTransform login, profile, account; InputField nameField; Text loginMsg, profileMsg, accountInfo, accountMsg;
        readonly List<Image> countryChips = new(), genderChips = new(); int pickCountry = -1, pickGender = -1;

        void BuildAccountScreens() { BuildLogin(); BuildProfile(); BuildAccount(); }

        RectTransform FullScreen(string name, Color c)
        {
            var r = Panel(canvas, name, c); Fill(r); r.GetComponent<Image>().raycastTarget = true; r.gameObject.SetActive(false); return r;
        }
        RectTransform WideBtn(RectTransform parent, string text, Vector2 pos, Vector2 size, Color bg, Color fg, System.Action onClick, int fs = 26)
        {
            var b = Panel(parent, text, bg, round); Place(b, new Vector2(0.5f, 0.5f), pos, size); Label(b, text, display, fs, fg, TextAnchor.MiddleCenter); Btn(b, onClick); return b;
        }

        // ---------- sign-in ----------
        void BuildLogin()
        {
            login = FullScreen("Login", new Color(0.02f, 0.02f, 0.03f, 0.55f));
            var side = Panel(login, "Shade", new Color(0, 0, 0, 0.75f), gradH); side.anchorMin = new Vector2(0.45f, 0); side.anchorMax = Vector2.one; side.offsetMin = side.offsetMax = Vector2.zero; side.localScale = new Vector3(-1, 1, 1);
            var logo = Label(login, "KRAYVON", display, 150, Gold, TextAnchor.MiddleLeft); Place(logo.rectTransform, new Vector2(0, 0.5f), new Vector2(90, 120), new Vector2(900, 170), new Vector2(0, 0.5f));
            logo.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(5, -5);
            var tag = Label(login, "BILAD AL-SHAM  ·  BATTLE ROYALE", bodyBold, 30, new Color(1, 1, 1, 0.85f)); Place(tag.rectTransform, new Vector2(0, 0.5f), new Vector2(98, 20), new Vector2(900, 40), new Vector2(0, 0.5f));

            var card = Panel(login, "Card", new Color(0.05f, 0.06f, 0.08f, 0.94f), round); Place(card, new Vector2(1, 0.5f), new Vector2(-110, 0), new Vector2(600, 880), new Vector2(1, 0.5f));
            var fr = Panel(card, "Gold", new Color(1f, 0.74f, 0.25f, 0.4f), round); Fill(fr); fr.GetComponent<Image>().fillCenter = false;
            Label(card, "SIGN IN", display, 56, Color.white, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -26);
            var sub = Label(card, "Choose how to play. Your progress is saved to your account.", body, 20, Dim, TextAnchor.UpperCenter); sub.rectTransform.offsetMax = new Vector2(-30, -100); sub.rectTransform.offsetMin = new Vector2(30, 0);
            var sz = new Vector2(500, 70);
            WideBtn(card, "G    CONTINUE WITH GOOGLE", new Vector2(0, 250), sz, Color.white, new Color(0.12f, 0.12f, 0.14f), () => loginMsg.text = LinkSoon);
            WideBtn(card, "f    CONTINUE WITH FACEBOOK", new Vector2(0, 168), sz, new Color(0.23f, 0.35f, 0.62f), Color.white, () => loginMsg.text = LinkSoon);
            WideBtn(card, "CONTINUE WITH APPLE", new Vector2(0, 86), sz, Color.black, Color.white, () => loginMsg.text = LinkSoon);
            WideBtn(card, "☏    CONTINUE WITH PHONE NUMBER", new Vector2(0, 4), sz, new Color(0.29f, 0.365f, 0.306f), Color.white, () => loginMsg.text = LinkSoon);
            WideBtn(card, "SIGN IN WITH E-MAIL", new Vector2(0, -78), sz, new Color(1, 1, 1, 0.1f), Color.white, () => loginMsg.text = LinkSoon);
            var or = Label(card, "———————   OR   ———————", bodyBold, 20, new Color(1, 1, 1, 0.4f), TextAnchor.MiddleCenter); Place(or.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -140), new Vector2(500, 30));
            // a returning player continues with the saved profile; a new one makes a guest profile
            if (HasProfile) WideBtn(card, "CONTINUE AS " + PlayerName, new Vector2(0, -206), new Vector2(500, 80), Gold, Dark, ContinueSaved, 30);
            else WideBtn(card, "PLAY AS GUEST", new Vector2(0, -206), new Vector2(500, 80), Gold, Dark, () => OpenProfile(false), 34);
            loginMsg = Label(card, "", body, 19, new Color(1f, 0.82f, 0.5f), TextAnchor.MiddleCenter); Place(loginMsg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(540, 70));
            var terms = Label(card, "By continuing you agree to the Terms of Service and the Privacy Policy.", body, 16, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter); Place(terms.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(540, 50));
            var ver = Label(login, "v" + Application.version + "  ·  MIDDLE EAST", body, 18, new Color(1, 1, 1, 0.4f)); Place(ver.rectTransform, new Vector2(0, 0), new Vector2(30, 20), new Vector2(500, 30), new Vector2(0, 0));
        }
        void ShowLogin() { loginMsg.text = ""; login.gameObject.SetActive(true); loginShown = true; }
        /// the login screen comes once per launch, right after the loading screen (not again after each match)
        static bool loginShown;
        public static bool LoginDue => !loginShown;
        void ContinueSaved() { login.gameObject.SetActive(false); StartCoroutine(FadeInLobby()); }
        System.Collections.IEnumerator FadeInLobby() { var lg = lobby.GetComponent<CanvasGroup>(); for (float f = 0; f < 0.5f; f += Time.unscaledDeltaTime) { lg.alpha = f / 0.5f; yield return null; } lg.alpha = 1; }
        void AutoContinue() { if (login.gameObject.activeSelf && HasProfile) ContinueSaved(); }

        // ---------- create / edit profile ----------
        void BuildProfile()
        {
            profile = FullScreen("Profile", new Color(0.02f, 0.02f, 0.03f, 0.75f));
            var card = Panel(profile, "Card", new Color(0.05f, 0.06f, 0.08f, 0.96f), round); Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1120, 800));
            var fr = Panel(card, "Gold", new Color(1f, 0.74f, 0.25f, 0.4f), round); Fill(fr); fr.GetComponent<Image>().fillCenter = false;
            Label(card, "CREATE YOUR PROFILE", display, 54, Gold, TextAnchor.UpperCenter).rectTransform.offsetMax = new Vector2(0, -22);
            Text H(string s, float y) { var l = Label(card, s, bodyBold, 22, Dim); Place(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-510, y), new Vector2(700, 30), new Vector2(0, 0.5f)); return l; }

            H("NICKNAME", 268);
            var f = Panel(card, "Name", new Color(1, 1, 1, 0.08f), round); Place(f, new Vector2(0.5f, 0.5f), new Vector2(-510, 214), new Vector2(660, 66), new Vector2(0, 0.5f));
            var img = f.GetComponent<Image>(); img.raycastTarget = true;
            var txt = Label(f, "", bodyBold, 32, Color.white); txt.rectTransform.offsetMin = new Vector2(22, 0); txt.rectTransform.offsetMax = new Vector2(-22, 0); txt.supportRichText = false; txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Label(f, "ENTER A NICKNAME", bodyBold, 30, new Color(1, 1, 1, 0.28f)); ph.rectTransform.offsetMin = new Vector2(22, 0);
            nameField = f.gameObject.AddComponent<InputField>(); nameField.textComponent = txt; nameField.placeholder = ph; nameField.targetGraphic = img; nameField.characterLimit = 14;
            nameField.onValidateInput = (s, i, c) => char.IsLetterOrDigit(c) && c < 128 || c == '_' || c == '.' ? c : '\0';
            WideBtn(card, "RANDOM", new Vector2(250, 214), new Vector2(200, 66), new Color(1, 1, 1, 0.1f), Color.white, () =>
            {
                string[] first = { "Nashama", "Falcon", "Shaheen", "Saqr", "Layth", "Najm", "Barq", "Raad", "Sahm", "Wadi" };
                nameField.text = first[Random.Range(0, first.Length)] + Random.Range(10, 9999);
            });
            var hint = Label(card, "3–14 characters: English letters, numbers, _ and .", body, 18, new Color(1, 1, 1, 0.45f)); Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-510, 166), new Vector2(700, 26), new Vector2(0, 0.5f));

            H("COUNTRY / REGION", 120);
            countryChips.Clear();
            for (int i = 0; i < Countries.Length; i++)
            {
                int idx = i;
                var c = Panel(card, "Country", Ink, round); Place(c, new Vector2(0.5f, 0.5f), new Vector2(-510 + (i % 6) * 172, 70 - (i / 6) * 54), new Vector2(164, 46), new Vector2(0, 0.5f));
                Label(c, Countries[i], bodyBold, 18, Color.white, TextAnchor.MiddleCenter);
                countryChips.Add(c.GetComponent<Image>()); Btn(c, () => { pickCountry = idx; Paint(); });
            }

            H("GENDER", -150);
            genderChips.Clear();
            for (int i = 0; i < Genders.Length; i++)
            {
                int idx = i;
                var c = Panel(card, "Gender", Ink, round); Place(c, new Vector2(0.5f, 0.5f), new Vector2(-510 + i * 232, -200), new Vector2(220, 56), new Vector2(0, 0.5f));
                Label(c, Genders[i], display, 26, Color.white, TextAnchor.MiddleCenter);
                genderChips.Add(c.GetComponent<Image>()); Btn(c, () => { pickGender = idx; Paint(); });
            }
            profileMsg = Label(card, "", bodyBold, 20, new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter); Place(profileMsg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 128), new Vector2(1000, 30));
            WideBtn(card, "CONFIRM", new Vector2(130, -330), new Vector2(360, 82), Gold, Dark, ConfirmProfile, 38);
            WideBtn(card, "BACK", new Vector2(-180, -330), new Vector2(220, 82), new Color(1, 1, 1, 0.1f), Color.white, () => { profile.gameObject.SetActive(false); if (!HasProfile) ShowLogin(); }, 30);
        }
        void Paint()
        {
            for (int i = 0; i < countryChips.Count; i++) countryChips[i].color = i == pickCountry ? GoldDeep : Ink;
            for (int i = 0; i < genderChips.Count; i++) genderChips[i].color = i == pickGender ? GoldDeep : Ink;
        }
        void OpenProfile(bool edit)
        {
            login.gameObject.SetActive(false); account.gameObject.SetActive(false);
            nameField.text = edit ? PlayerName : "";
            pickCountry = edit ? System.Array.IndexOf(Countries, PlayerPrefs.GetString("kryvon.country", "")) : -1;
            pickGender = edit ? System.Array.IndexOf(Genders, PlayerPrefs.GetString("kryvon.gender", "")) : -1;
            profileMsg.text = ""; Paint(); profile.gameObject.SetActive(true);
        }
        void ConfirmProfile()
        {
            var n = (nameField.text ?? "").Trim();
            if (n.Length < 3) { profileMsg.text = "The nickname needs at least 3 characters."; return; }
            if (!System.Linq.Enumerable.Any(n, ch => char.IsLetter(ch))) { profileMsg.text = "The nickname needs at least one letter."; return; }
            if (pickCountry < 0) { profileMsg.text = "Pick your country or region."; return; }
            if (pickGender < 0) { profileMsg.text = "Pick a gender."; return; }
            bool first = !HasProfile;
            PlayerPrefs.SetString("kryvon.name", n.ToUpperInvariant()); PlayerPrefs.SetString("kryvon.country", Countries[pickCountry]); PlayerPrefs.SetString("kryvon.gender", Genders[pickGender]);
            if (!PlayerPrefs.HasKey("kryvon.account")) PlayerPrefs.SetString("kryvon.account", "guest");
            if (first && !PlayerPrefs.HasKey(KryvonHeroSwitch.Pref)) PlayerPrefs.SetString(KryvonHeroSwitch.Pref, pickGender == 1 ? "sara" : "hamza");
            _ = PlayerId; PlayerPrefs.Save();
            skipSplash = true; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);     // rebuild the lobby with the new name
        }

        // the player card (BuildAccount / OpenAccount) lives in KryvonLobbyProfile.cs
        void SignOut()
        {
            foreach (var k in new[] { "kryvon.name", "kryvon.country", "kryvon.gender", "kryvon.account" }) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save(); skipSplash = true; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // autotest hooks (SendMessage)
        void AutoLogin() { if (login && login.gameObject.activeSelf) OpenProfile(false); }
        void AutoProfile() { nameField.text = "Tester"; pickCountry = 0; pickGender = 0; Paint(); }
        void AutoConfirm() => ConfirmProfile();
        void AutoLayout() => KryvonTouchControls.OpenEditor(bodyBold, null);
        void AutoLayoutClose() { var e = GameObject.Find("TouchLayoutEditor"); if (e) Destroy(e); }
        void AutoCloseAccount() => account.gameObject.SetActive(false);
    }
}
