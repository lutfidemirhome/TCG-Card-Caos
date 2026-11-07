using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent white paw, using the same UI material as the HUD.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PawIconGraphic : MaskableGraphic
{
    const int Segments = 32;
    static readonly Vector2[] Pad = MakePad();

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width, rect.height) / 100f;
        float edge = .65f / Mathf.Max(.01f, canvas != null ? canvas.scaleFactor : 1f);
        Ellipse(mesh, rect.center, scale, edge, new Vector2(18, 62), new Vector2(9, 13), 28);
        Ellipse(mesh, rect.center, scale, edge, new Vector2(39, 79), new Vector2(10, 14), 10);
        Ellipse(mesh, rect.center, scale, edge, new Vector2(61, 79), new Vector2(10, 14), -10);
        Ellipse(mesh, rect.center, scale, edge, new Vector2(82, 62), new Vector2(9, 13), -28);
        Polygon(mesh, rect.center, scale, edge, Pad, new Vector2(50, 33));
    }

    void Ellipse(VertexHelper mesh, Vector2 origin, float scale, float edge, Vector2 center, Vector2 radii, float angle)
    {
        int first = mesh.currentVertCount;
        Vector2 position = origin + (center - Vector2.one * 50) * scale;
        mesh.AddVert(position, color, Vector2.zero);
        float rotation = angle * Mathf.Deg2Rad;
        Color transparent = color; transparent.a = 0;
        for (int i = 0; i < Segments; i++)
        {
            float t = i * Mathf.PI * 2 / Segments;
            Vector2 point = new Vector2(Mathf.Cos(t) * radii.x, Mathf.Sin(t) * radii.y);
            point = new Vector2(point.x * Mathf.Cos(rotation) - point.y * Mathf.Sin(rotation),
                point.x * Mathf.Sin(rotation) + point.y * Mathf.Cos(rotation));
            mesh.AddVert(position + point * scale, color, Vector2.zero);
            mesh.AddVert(position + point * scale + point.normalized * edge, transparent, Vector2.zero);
        }
        Triangles(mesh, first, Segments);
    }

    void Polygon(VertexHelper mesh, Vector2 origin, float scale, float edge, Vector2[] points, Vector2 center)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(origin + (center - Vector2.one * 50) * scale, color, Vector2.zero);
        Color transparent = color; transparent.a = 0;
        foreach (Vector2 point in points)
        {
            Vector2 position = origin + (point - Vector2.one * 50) * scale;
            mesh.AddVert(position, color, Vector2.zero);
            mesh.AddVert(position + (point - center).normalized * edge, transparent, Vector2.zero);
        }
        Triangles(mesh, first, points.Length);
    }

    static void Triangles(VertexHelper mesh, int first, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int a = first + 1 + i * 2, b = first + 1 + ((i + 1) % count) * 2;
            mesh.AddTriangle(first, a, b);
            mesh.AddTriangle(a, a + 1, b + 1);
            mesh.AddTriangle(a, b + 1, b);
        }
    }

    static Vector2[] MakePad()
    {
        // Rounded central pad: broad base, softly notched lower edge, four separate toes.
        Vector2[] controls = {
            new Vector2(50, 60), new Vector2(38, 60), new Vector2(34, 47), new Vector2(25, 37),
            new Vector2(16, 27), new Vector2(20, 14), new Vector2(31, 14),
            new Vector2(39, 14), new Vector2(44, 20), new Vector2(50, 20),
            new Vector2(56, 20), new Vector2(61, 14), new Vector2(69, 14),
            new Vector2(80, 14), new Vector2(84, 27), new Vector2(75, 37),
            new Vector2(66, 47), new Vector2(62, 60), new Vector2(50, 60)
        };
        const int steps = 12;
        var points = new Vector2[6 * steps];
        for (int curve = 0; curve < 6; curve++)
            for (int i = 0; i < steps; i++)
            {
                float t = (float)i / steps, u = 1 - t;
                int p = curve * 3;
                points[curve * steps + i] = u * u * u * controls[p] + 3 * u * u * t * controls[p + 1]
                    + 3 * u * t * t * controls[p + 2] + t * t * t * controls[p + 3];
            }
        return points;
    }
}
