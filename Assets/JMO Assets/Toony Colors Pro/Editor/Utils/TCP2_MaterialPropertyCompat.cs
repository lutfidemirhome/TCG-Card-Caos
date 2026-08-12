// Compatibility shim for Unity 6000.0.x (propertyType added in 6000.2+).
using UnityEditor;
using UnityEngine.Rendering;

namespace ToonyColorsPro.Utilities
{
    internal static class TCP2_MaterialPropertyCompat
    {
        internal static ShaderPropertyType GetPropertyType(MaterialProperty property)
        {
#if UNITY_6000_2_OR_NEWER
            return property.propertyType;
#else
            return (ShaderPropertyType)(int)property.type;
#endif
        }

        internal static ShaderPropertyFlags GetPropertyFlags(MaterialProperty property)
        {
#if UNITY_6000_2_OR_NEWER
            return property.propertyFlags;
#else
            return (ShaderPropertyFlags)(int)property.flags;
#endif
        }
    }
}
