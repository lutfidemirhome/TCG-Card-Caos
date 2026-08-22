using System;
using UnityEngine;

[Serializable]
public class CardSaveRecord
{
    public string id = string.Empty;
    public string definitionId = string.Empty;
    public int psaSlot;
    public int psaVariant;
    public int palette;
    public CardRuntimeLocation location;
    public float px;
    public float py;
    public float pz;
    public float rx;
    public float ry;
    public float rz;
    public float rw = 1f;
    public bool faceDown;
    public int stackLayer;
    public string shelfId = string.Empty;
    public int slotRow;
    public int slotColumn;
    public string psaCabinetId = string.Empty;
    public int psaCabinetSlot;

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
