// Looping 2D ambience beds (wind, birds) with slow random swells.
using UnityEngine;

namespace Kryvon
{
    public class KryvonAmbience : MonoBehaviour
    {
        [SerializeField] AudioClip[] beds;
        [SerializeField, Range(0, 1)] float volume = 0.35f;
        AudioSource[] src; float[] seed;

        void Start()
        {
            src = new AudioSource[beds.Length]; seed = new float[beds.Length];
            for (int i = 0; i < beds.Length; i++)
            {
                var s = src[i] = gameObject.AddComponent<AudioSource>();
                s.clip = beds[i]; s.loop = true; s.spatialBlend = 0; s.volume = volume; s.time = Random.Range(0f, beds[i].length * 0.9f); s.Play();
                seed[i] = Random.value * 100f;
            }
        }

        void Update()
        {
            for (int i = 0; i < src.Length; i++)
                src[i].volume = volume * Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(seed[i], Time.time * 0.08f));
        }
    }
}
