using SnQuaternion = System.Numerics.Quaternion;
using SnVector3 = System.Numerics.Vector3;
using UVector3 = UnityEngine.Vector3;
using UQuaternion = UnityEngine.Quaternion;

namespace GorillaAntiCheat.Unity
{
    /// <summary>
    /// Bridges UnityEngine math types to the engine-agnostic System.Numerics types
    /// used by GorillaAntiCheat.Core. Keeping the conversion here means the core
    /// never takes a Unity dependency.
    /// </summary>
    internal static class UnityMath
    {
        public static SnVector3 ToNumerics(this UVector3 v) => new SnVector3(v.x, v.y, v.z);

        public static SnQuaternion ToNumerics(this UQuaternion q) => new SnQuaternion(q.x, q.y, q.z, q.w);

        public static UVector3 ToUnity(this SnVector3 v) => new UVector3(v.X, v.Y, v.Z);
    }
}
