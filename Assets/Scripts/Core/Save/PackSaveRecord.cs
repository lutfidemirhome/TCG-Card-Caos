using System;
using UnityEngine;

[Serializable]
public class PackSaveRecord
{
    public string id = string.Empty;
    public string assignmentLabel = string.Empty;
    public int variant = 1;
    public int packSet;
    public bool held;
    public float px;
    public float py;
    public float pz;
    public float rx;
    public float ry;
    public float rz;
    public float rw = 1f;
    public bool faceDown;
    public int stackLayer;
    public ThrownPhysicsSaveState physics;
    public PackWorldVisualPose worldVisualPose;
    public string[] contents;

    public void SetPosition(Vector3 position)
    {
        px = position.x;
        py = position.y;
        pz = position.z;
    }

    public void SetRotation(Quaternion rotation)
    {
        rx = rotation.x;
        ry = rotation.y;
        rz = rotation.z;
        rw = rotation.w;
    }

    public Vector3 Position => new Vector3(px, py, pz);

    public Quaternion Rotation
    {
        get
        {
            float magSq = rx * rx + ry * ry + rz * rz + rw * rw;
            if (magSq < 0.0001f)
                return Quaternion.identity;

            return new Quaternion(rx, ry, rz, rw).normalized;
        }
    }
}

/// <summary>
/// Optional in version-1 saves. Authored packs and thrown packs can use different
/// child poses even when their root position/rotation and faceDown flag match.
/// </summary>
[Serializable]
public sealed class PackWorldVisualPose
{
    public bool captured;
    public Vector3 worldScale;
    public Vector3 proxyPosition;
    public Quaternion proxyRotation;
    public Vector3 proxyScale;
    public Vector3 modelPosition;
    public Quaternion modelRotation;
    public Vector3 modelScale;

    public bool IsValid => captured && ValidScale(worldScale) && ValidScale(proxyScale)
        && ValidScale(modelScale) && Finite(proxyPosition) && Finite(modelPosition)
        && ValidRotation(proxyRotation) && ValidRotation(modelRotation);

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    static bool ValidScale(Vector3 value) => Finite(value)
        && Mathf.Abs(value.x) > 0.000001f && Mathf.Abs(value.y) > 0.000001f && Mathf.Abs(value.z) > 0.000001f;
    static bool ValidRotation(Quaternion value) => Finite(value.x) && Finite(value.y)
        && Finite(value.z) && Finite(value.w) && Quaternion.Dot(value, value) > 0.0001f;
}
