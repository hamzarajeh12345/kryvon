// A hinged door: F opens/closes it, swinging away from the player. Blocks movement and bullets when closed.
using System.Collections.Generic;
using UnityEngine;

namespace Kryvon
{
    public class KryvonDoor : MonoBehaviour
    {
        public static readonly List<KryvonDoor> All = new();
        [SerializeField] float openAngle = 100f, speed = 300f;
        [SerializeField] AudioClip openSound, closeSound;
        float target, angle; bool open;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool IsOpen => open;
        public void Toggle(Vector3 from)
        {
            open = !open;
            if (open)
            {
                var n = transform.parent ? transform.parent.forward : Vector3.forward;
                target = Vector3.Dot(from - transform.position, n) > 0 ? -openAngle : openAngle;
            }
            else target = 0;
            var clip = open ? openSound : closeSound;
            if (clip) AudioSource.PlayClipAtPoint(clip, transform.position, 0.7f);
        }
        void Update()
        {
            if (Mathf.Approximately(angle, target)) return;
            angle = Mathf.MoveTowards(angle, target, speed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0, angle, 0);
        }
    }
}
