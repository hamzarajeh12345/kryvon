// The real loading screen. Boot is the first (tiny) scene: it shows the KRAYVON key art at once, then loads the lobby in
// the background and the red bar follows the ACTUAL work:
//   0–80 %   the lobby scene streaming in (AsyncOperation.progress)
//   80–90 %  the lobby waking up (scene activation)
//   90–100 % the lobby's first frames: shaders, textures and models uploaded to the GPU – it counts smooth frames and
//            only reaches 100 % when the game runs fluidly (or after 10 s on a very slow phone).
// The screen survives the scene change and fades out over the finished lobby, so there is never a black frame.
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kryvon
{
    public class KryvonBoot : MonoBehaviour
    {
        [SerializeField] string next = "Lobby";
        RectTransform fill, spinner; Text pct; float shown;

        void Start()
        {
            Application.targetFrameRate = 60;
            DontDestroyOnLoad(gameObject);
            Build();
            // Unity's own static splash (the same art, set in Player Settings so the very first frame is never black) keeps
            // drawing OVER the game until loading ends – it hid the live bar. Ours is in place now, so hand over at once.
            UnityEngine.Rendering.SplashScreen.Stop(UnityEngine.Rendering.SplashScreen.StopBehavior.StopImmediate);
            StartCoroutine(Run());
        }

        void Build()
        {
            var cv = gameObject.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 1000;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)transform;
            var bg = Img(root, "Black", Color.black); Stretch(bg.rectTransform);

            var art = Resources.Load<Texture2D>("UI/splash");
            if (!art) return;
            // 1) blurred art filling the whole screen, 2) the art itself, whole and undistorted
            var blur = Raw(root, "Blur", Resources.Load<Texture2D>("UI/splash_blur") ?? art);
            var bf = blur.gameObject.AddComponent<AspectRatioFitter>(); bf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; bf.aspectRatio = 1241f / 1268f;
            var ri = Raw(root, "Art", art);
            var fit = ri.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1241f / 1268f;

            // 3) the bar and spinner painted in the art are covered by live ones in exactly the same place
            //    (pixel rects in the 1241 x 1268 art), so what you see moving IS the loading
            RectTransform OnArt(string n, float x0, float y0, float x1, float y1, Color c, Sprite sp)
            {
                var im = Img(ri.rectTransform, n, c, sp); var r = im.rectTransform; const float W = 1241f, H = 1268f;   // the original art size (the import may resize the texture)
                r.anchorMin = new Vector2(x0 / W, 1 - y1 / H); r.anchorMax = new Vector2(x1 / W, 1 - y0 / H); r.offsetMin = r.offsetMax = Vector2.zero; return r;
            }
            var pill = Pill();
            var track = OnArt("Track", 324, 1088, 916, 1123, new Color(0.5f, 0.5f, 0.54f, 1f), pill);     // thin metal rim
            var inner = Img(track, "Inner", new Color(0.05f, 0.05f, 0.06f, 1f), pill); Stretch(inner.rectTransform); inner.rectTransform.offsetMin = new Vector2(2, 2); inner.rectTransform.offsetMax = new Vector2(-2, -2);
            var area = new GameObject("Area", typeof(RectTransform)).GetComponent<RectTransform>(); area.SetParent(track, false); Stretch(area); area.offsetMin = new Vector2(3, 3); area.offsetMax = new Vector2(-3, -3);
            var f = Img(area, "Fill", new Color(0.93f, 0.08f, 0.06f), pill); fill = f.rectTransform; fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var shine = Img(fill, "Shine", new Color(1f, 0.65f, 0.55f, 0.5f), pill).rectTransform; shine.anchorMin = new Vector2(0, 0.55f); shine.anchorMax = new Vector2(1, 0.9f); shine.offsetMin = new Vector2(3, 0); shine.offsetMax = new Vector2(-3, 0);
            var disc = OnArt("SpinnerBg", 584, 1134, 658, 1208, new Color(0.03f, 0.03f, 0.04f, 1f), Disc());
            var sp = Img(disc, "Spinner", new Color(1f, 0.2f, 0.15f), Arc()); spinner = sp.rectTransform; Stretch(spinner); spinner.offsetMin = new Vector2(5, 5); spinner.offsetMax = new Vector2(-5, -5);
            // the real percentage, right of the painted "Loading..."
            var t = new GameObject("Pct", typeof(RectTransform), typeof(Text)).GetComponent<Text>(); t.transform.SetParent(ri.rectTransform, false);
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.fontStyle = FontStyle.Bold; t.color = Color.white; t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 8; t.resizeTextMaxSize = 60; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tr = t.rectTransform; const float AW = 1241f, AH = 1268f;
            tr.anchorMin = new Vector2(926 / AW, 1 - 1126 / AH); tr.anchorMax = new Vector2(1010 / AW, 1 - 1086 / AH); tr.offsetMin = tr.offsetMax = Vector2.zero;
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            pct = t; t.text = "0%";
        }

        void Show(float target, float dt)
        {
            shown = Mathf.Max(shown, Mathf.MoveTowards(shown, target, dt * 1.6f));   // never goes backwards
            if (fill) fill.anchorMax = new Vector2(shown, 1);
            if (pct) pct.text = Mathf.FloorToInt(shown * 100f) + "%";
            if (spinner) spinner.Rotate(0, 0, -360f * dt);
        }

        IEnumerator Run()
        {
            yield return null; yield return null;          // the art is on screen before any heavy work starts
            var op = SceneManager.LoadSceneAsync(next);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) { Show(op.progress / 0.9f * 0.8f, Time.unscaledDeltaTime); yield return null; }
            while (shown < 0.79f) { Show(0.8f, Time.unscaledDeltaTime); yield return null; }
            KryvonLobby.SkipNextSplash();
            op.allowSceneActivation = true;
            while (!op.isDone) { Show(0.88f, Time.unscaledDeltaTime); yield return null; }

            // the lobby is in: wait until it actually runs smoothly (GPU uploads / shader compiles happen on the first frames)
            float t = 0; int smooth = 0;
            while (true)
            {
                float dt = Time.unscaledDeltaTime; t += dt;
                smooth = dt < 0.1f ? smooth + 1 : 0;
                Show(0.9f + 0.1f * Mathf.Clamp01(smooth / 30f), dt);
                if ((smooth >= 30 && shown >= 0.999f) || t > 10f) break;
                yield return null;
            }
            Show(1f, 1f); yield return null;
            var cg = GetComponent<CanvasGroup>();
            for (float f = 0; f < 0.6f; f += Time.unscaledDeltaTime) { cg.alpha = 1f - f / 0.6f; if (spinner) spinner.Rotate(0, 0, -360f * Time.unscaledDeltaTime); yield return null; }
            Destroy(gameObject);
        }

        // ---------- tiny UI helpers ----------
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static Image Img(RectTransform p, string n, Color c, Sprite s = null)
        {
            var i = new GameObject(n, typeof(RectTransform), typeof(Image)).GetComponent<Image>(); i.transform.SetParent(p, false);
            i.color = c; i.sprite = s; i.raycastTarget = false; if (s && s.border != Vector4.zero) i.type = Image.Type.Sliced; return i;
        }
        static RawImage Raw(RectTransform p, string n, Texture t)
        {
            var r = new GameObject(n, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>(); r.transform.SetParent(p, false);
            r.texture = t; r.raycastTarget = false; Stretch(r.rectTransform); return r;
        }
        static Sprite Pill()
        {
            int S = 64, R = 31; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, R, S - R), cy = Mathf.Clamp(y + 0.5f, R, S - R);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(R - d + 0.5f)));
            }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        }
        static Sprite Disc()
        {
            int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S / 2f, S / 2f)); t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(S / 2f - d))); }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        static Sprite Arc()
        {
            int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = x - S / 2f, dy = y - S / 2f, d = Mathf.Sqrt(dx * dx + dy * dy) / (S / 2f), ang = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f;
                float ring = Mathf.Clamp01((0.98f - d) * 30f) * Mathf.Clamp01((d - 0.7f) * 30f);
                t.SetPixel(x, y, new Color(1, 1, 1, ring * Mathf.Pow(ang / 360f, 1.5f)));
            }
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
    }
}
