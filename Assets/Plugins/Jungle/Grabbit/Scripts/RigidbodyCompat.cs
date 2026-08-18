using UnityEngine;

namespace Grabbit
{
    /// <summary>
    /// Cross-version shim for the Unity 6 Rigidbody property renames so the same source compiles on
    /// 2022.3 LTS through Unity 6+ without relying on the obsolete pre-6 names (which emit deprecation
    /// warnings on 6.x and may be removed later):
    ///   velocity    -> linearVelocity
    ///   drag        -> linearDamping
    ///   angularDrag -> angularDamping
    /// (angularVelocity was NOT renamed, so it is used directly everywhere.)
    /// Usage: body.LinearVelocity() / body.LinearVelocity(v), body.LinearDamping() / body.LinearDamping(v),
    /// body.AngularDamping() / body.AngularDamping(v).
    /// </summary>
    internal static class RigidbodyCompat
    {
        public static Vector3 LinearVelocity(this Rigidbody rb) =>
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity;
#else
            rb.velocity;
#endif

        public static void LinearVelocity(this Rigidbody rb, Vector3 value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = value;
#else
            rb.velocity = value;
#endif
        }

        public static float LinearDamping(this Rigidbody rb) =>
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping;
#else
            rb.drag;
#endif

        public static void LinearDamping(this Rigidbody rb, float value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = value;
#else
            rb.drag = value;
#endif
        }

        public static float AngularDamping(this Rigidbody rb) =>
#if UNITY_6000_0_OR_NEWER
            rb.angularDamping;
#else
            rb.angularDrag;
#endif

        public static void AngularDamping(this Rigidbody rb, float value)
        {
#if UNITY_6000_0_OR_NEWER
            rb.angularDamping = value;
#else
            rb.angularDrag = value;
#endif
        }
    }
}
