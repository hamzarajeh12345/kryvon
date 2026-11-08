// Flashbang effect on the local player: white-out that fades over 3–5 s, muffled sound and a ringing tone.
// Also hosts the screen shake used by explosions and hits.
using UnityEngine;

namespace Kryvon
{
    public class KryvonFlashOverlay : MonoBehaviour
    {
        static KryvonFlashOverlay inst;
        float t, dur, strength; AudioSource ring;
        public static void Blind(float seconds, float s, AudioClip ringClip)
        {
            if (!inst) inst = new GameObject("FlashOverlay").AddComponent<KryvonFlashOverlay>();
            inst.t = 0; inst.dur = seconds; inst.strength = Mathf.Max(inst.strength, s);
            if (ringClip) { inst.ring ??= inst.gameObject.AddComponent<AudioSource>(); inst.ring.clip = ringClip; inst.ring.loop = true; inst.ring.ignoreListenerVolume = true; inst.ring.volume = 0.5f * s; inst.ring.Play(); }
        }
        void Update()
        {
            if (dur <= 0) return;
            t += Time.deltaTime; float k = 1f - t / dur;
            AudioListener.volume = Mathf.Lerp(PlayerPrefs.GetFloat("kryvon.volume", 0.8f), 0.08f, Mathf.Clamp01(k * strength * 1.3f));
            if (ring) ring.volume = 0.5f * strength * Mathf.Clamp01(k);
            if (t >= dur) { dur = 0; strength = 0; if (ring) ring.Stop(); AudioListener.volume = PlayerPrefs.GetFloat("kryvon.volume", 0.8f); }
        }
        void OnGUI()
        {
            if (dur <= 0) return;
            float k = Mathf.Clamp01(1f - t / dur); float a = Mathf.Clamp01(strength * (k < 0.5f ? k * 2f : 1f));
            GUI.color = new Color(1, 1, 1, a); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture); GUI.color = Color.white;
        }
    }

    /// cheap camera shake: offsets the main camera for a moment after Kick()
    public class CameraShake : MonoBehaviour
    {
        static CameraShake inst; float amt;
        public static void Kick(float a) { if (a <= 0) return; if (!inst && Camera.main) inst = Camera.main.gameObject.AddComponent<CameraShake>(); if (inst) inst.amt = Mathf.Max(inst.amt, a); }
        void LateUpdate()
        {
            if (amt <= 0.001f) return;
            transform.position += Random.insideUnitSphere * amt * 0.25f;
            transform.rotation *= Quaternion.Euler(Random.Range(-1f, 1f) * amt * 2f, Random.Range(-1f, 1f) * amt * 2f, 0);
            amt = Mathf.MoveTowards(amt, 0, Time.deltaTime * 2.5f);
        }
    }
}
