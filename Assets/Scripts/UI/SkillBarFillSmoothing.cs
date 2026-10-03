using UnityEngine;
using UnityEngine.UI;

/// <summary>Softens the moving edge of the small HUD timers without extra materials.</summary>
[RequireComponent(typeof(Image))]
public sealed class SkillBarFillSmoothing : BaseMeshEffect
{
    Image _image;

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount != 4) return;
        if (!_image) _image = GetComponent<Image>();
        if (_image.type != Image.Type.Filled || _image.fillMethod != Image.FillMethod.Horizontal
            || _image.fillOrigin != (int)Image.OriginHorizontal.Left || _image.fillAmount >= 1f) return;

        UIVertex bottomLeft = default, topLeft = default, topRight = default, bottomRight = default;
        vertices.PopulateUIVertex(ref bottomLeft, 0);
        vertices.PopulateUIVertex(ref topLeft, 1);
        vertices.PopulateUIVertex(ref topRight, 2);
        vertices.PopulateUIVertex(ref bottomRight, 3);
        float width = bottomRight.position.x - bottomLeft.position.x;
        if (width <= 0f) return;

        // A roughly one-screen-pixel alpha ramp lets fractional pixel movement show.
        // The timer value, original sprite UVs and the surrounding bar remain unchanged.
        float scale = _image.canvas ? _image.canvas.scaleFactor : 1f;
        float feather = Mathf.Min(width, 1.25f / Mathf.Max(0.01f, scale));
        float t = 1f - feather / width;
        UIVertex innerTop = topRight, innerBottom = bottomRight;
        innerTop.position = Vector3.Lerp(topLeft.position, topRight.position, t);
        innerTop.uv0 = Vector4.Lerp(topLeft.uv0, topRight.uv0, t);
        innerTop.color = Color32.Lerp(topLeft.color, topRight.color, t);
        innerBottom.position = Vector3.Lerp(bottomLeft.position, bottomRight.position, t);
        innerBottom.uv0 = Vector4.Lerp(bottomLeft.uv0, bottomRight.uv0, t);
        innerBottom.color = Color32.Lerp(bottomLeft.color, bottomRight.color, t);
        topRight.color.a = 0;
        bottomRight.color.a = 0;

        vertices.Clear();
        vertices.AddVert(bottomLeft);
        vertices.AddVert(topLeft);
        vertices.AddVert(innerTop);
        vertices.AddVert(innerBottom);
        vertices.AddVert(topRight);
        vertices.AddVert(bottomRight);
        vertices.AddTriangle(0, 1, 2);
        vertices.AddTriangle(2, 3, 0);
        vertices.AddTriangle(3, 2, 4);
        vertices.AddTriangle(4, 5, 3);
    }
}
