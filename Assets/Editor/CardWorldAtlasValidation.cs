using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CardWorldAtlasValidation
{
    static readonly List<string> Results = new();
    static int passed, failed;
    static void Check(bool condition, string label)
    {
        Results.Add((condition ? "PASS " : "FAIL ") + label);
        if (condition) passed++; else failed++;
    }

    [MenuItem("TCG Card Chaos/Diagnostics/Validate Distant Cards And Movement")]
    public static void Validate()
    {
        Results.Clear(); passed = failed = 0;
        try
        {
            var atlas = Resources.Load<CardWorldAtlas>("Cards/WorldAtlas/Catalog");
            Check(atlas != null, "Atlas catalog exists");
            if (!atlas) return;
            var definitions = CardCatalog.All.Where(d => d && d.FrontTexture).ToArray();
            Check(atlas.entries.Length == definitions.Length, "Every card definition has world art");
            Check(atlas.entries.Select(e => e.definitionId).Distinct().Count() == atlas.entries.Length, "Unique definition mappings");
            Check(atlas.entries.All(e => e.page >= 0 && e.page < atlas.pages.Length && e.uvRect.x > 0 && e.uvRect.y > 0
                && e.uvRect.z >= 0 && e.uvRect.w >= 0 && e.uvRect.x + e.uvRect.z <= 1 && e.uvRect.y + e.uvRect.w <= 1), "UVs stay within their pages");
            foreach (var page in atlas.pages)
            {
                var texture = page.GetTexture("_BaseMap") as Texture2D;
                Check(texture && texture.width == 4096 && texture.height == 4096 && !texture.isReadable, page.name + " imported at full page size without CPU copy");
                Check(page.enableInstancing && page.IsKeywordEnabled("_CARD_SINGLE_PASS") && page.IsKeywordEnabled("_CARD_WORLD_ATLAS"), page.name + " keeps instanced atlas variant in builds");
            }
            Check(!ShaderUtil.ShaderHasError(atlas.pages[0].shader), "Card shader compiled without errors");
            for (int i = 0; i < definitions.Length; i += 700)
            {
                var definition = definitions[i]; atlas.TryGet(definition.DefinitionId, out var entry);
                var a = Read(definition.FrontTexture, Vector2.one, Vector2.zero);
                var b = Read(atlas.pages[entry.page].GetTexture("_BaseMap"), new Vector2(entry.uvRect.x, entry.uvRect.y), new Vector2(entry.uvRect.z, entry.uvRect.w));
                double error = 0;
                for (int p = 0; p < a.Length; p++) error += Math.Abs(a[p].r - b[p].r) + Math.Abs(a[p].g - b[p].g) + Math.Abs(a[p].b - b[p].b);
                error /= a.Length * 3 * 255d;
                Check(error < .055, $"Art/color sample {definition.DefinitionId}: mean channel error {error:F4}");
            }
            if (Application.isPlaying && CardInstancedRenderManager.IsGameplayReady)
            {
                var player = Object.FindFirstObjectByType<FirstPersonController>();
                var controller = player.GetComponent<CharacterController>();
                int layer = LayerMask.NameToLayer(CardLayers.WorldCardLayerName);
                Check((controller.excludeLayers.value & (1 << layer)) != 0, "Player permanently excludes loose card/pack layer");
                var cards = Object.FindObjectsByType<WorldCard>(FindObjectsSortMode.None);
                Check(cards.All(c => !c.GetComponent<Collider>() || c.gameObject.layer == layer), "All card root colliders use the excluded item layer");
                var packs = Object.FindObjectsByType<WorldBoosterPack>(FindObjectsSortMode.None);
                Check(packs.All(c => !c.GetComponent<Collider>() || c.gameObject.layer == layer), "All pack root colliders use the excluded item layer");
                foreach (bool japanese in new[] { false, true })
                {
                    var source = cards.First(c => c.Definition && !c.UsesPsaSlab && c.Definition.IsJapanese == japanese);
                    CheckLifecycle(source, player);
                }
                CheckPlayerCollisionPersistence();
            }
            else Check(false, "Runtime lifecycle checks require a ready Play-mode scene");
        }
        catch (Exception e) { Check(false, e.ToString()); }
        finally
        {
            string report = $"{passed} passed, {failed} failed\n" + string.Join("\n", Results);
            File.WriteAllText("Temp/card-world-atlas-validation.txt", report);
            Debug.Log(report);
        }
    }

    static Color32[] Read(Texture texture, Vector2 scale, Vector2 offset)
    {
        var previous = RenderTexture.active; bool srgb = GL.sRGBWrite;
        var rt = RenderTexture.GetTemporary(96, 224, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var result = new Texture2D(96, 224, TextureFormat.RGB24, false, false);
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(texture, rt, scale, offset); RenderTexture.active = rt;
            result.ReadPixels(new Rect(0, 0, 96, 224), 0, 0, false); return result.GetPixels32();
        }
        finally { RenderTexture.active = previous; GL.sRGBWrite = srgb; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(result); }
    }

    static void CheckLifecycle(WorldCard source, FirstPersonController player)
    {
        var clone = Object.Instantiate(source.gameObject).GetComponent<WorldCard>();
        var slot = new GameObject("Atlas validation slot"); slot.AddComponent<CardShelfSlot>();
        string language = source.Definition.IsJapanese ? "Japanese" : "English";
        try
        {
            Vector3 far = Camera.main.transform.position + Vector3.up * 20;
            clone.transform.position = far; clone.RefreshAuthoredVisual();
            var renderer = clone.transform.Find("CardVisual").GetComponent<MeshRenderer>();
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            Check(renderer.sharedMaterial.IsKeywordEnabled("_CARD_WORLD_ATLAS"), language + " distant card uses atlas");
            var detail = CardArtLibrary.GetCardMaterials(clone.Definition, CardTextureQuality.Detail)[0];
            Check(renderer.sharedMaterial.GetTexture("_CardBackMap") == detail.GetTexture("_CardBackMap"), language + " back is unchanged");
            clone.transform.position = Camera.main.transform.position + Vector3.up * 2; clone.RefreshAuthoredVisual();
            Check(!renderer.sharedMaterial.IsKeywordEnabled("_CARD_WORLD_ATLAS") && renderer.sharedMaterial.GetTexture("_BaseMap") == clone.Definition.FrontTexture, language + " close view uses original art");
            Check(renderer.GetComponent<MeshFilter>().sharedMesh == mesh, language + " distance transition preserves authored mesh");
            clone.transform.position = far; clone.RefreshAuthoredVisual();
            clone.BeginPickupFlight(player.transform, 1, .3f, .1f);
            Check(clone.IsInHand && !renderer.sharedMaterial.IsKeywordEnabled("_CARD_WORLD_ATLAS") && !renderer.HasPropertyBlock(), language + " pickup immediately restores detail and clears atlas UVs");
            Check(CardSinglePassMesh.GetSourceOrSelf(renderer.GetComponent<MeshFilter>().sharedMesh) == CardArtLibrary.HandCardMesh, language + " pickup keeps the existing detailed hand mesh");
            clone.DropWithPhysics(Vector3.zero);
            Check(clone.IsPhysicsSimulating && !renderer.sharedMaterial.IsKeywordEnabled("_CARD_WORLD_ATLAS"), language + " thrown card stays detailed");
            slot.transform.position = far; clone.PlaceOnShelfSlot(slot.transform, .003f);
            Check(!renderer.sharedMaterial.IsKeywordEnabled("_CARD_WORLD_ATLAS"), language + " shelf card retains original effect-compatible material");
            clone.gameObject.SetActive(false);
            Check(!renderer.HasPropertyBlock(), language + " disabling clears atlas properties");
        }
        finally { Object.DestroyImmediate(clone.gameObject); Object.DestroyImmediate(slot); }
    }

    static void CheckPlayerCollisionPersistence()
    {
        var root = new GameObject("Movement collision validation");
        root.SetActive(false);
        root.transform.position = new Vector3(1000, .1f, 0);
        var controller = root.AddComponent<CharacterController>();
        controller.height = 2; controller.center = Vector3.up; controller.radius = .3f;
        var view = new GameObject("Test view"); view.transform.SetParent(root.transform, false); view.transform.localPosition = Vector3.up * 1.8f;
        var player = root.AddComponent<FirstPersonController>(); player.enabled = false;
        var serialized = new SerializedObject(player);
        serialized.FindProperty("cameraTransform").objectReferenceValue = view.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(1000, -.05f, 0); floor.transform.localScale = new Vector3(10, .1f, 10);
        var card = GameObject.CreatePrimitive(PrimitiveType.Cube); card.layer = LayerMask.NameToLayer(CardLayers.WorldCardLayerName);
        card.transform.position = new Vector3(1000, .05f, 1); card.transform.localScale = new Vector3(2, .1f, 1);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(1000, 1, 3); wall.transform.localScale = new Vector3(4, 2, .2f);
        try
        {
            var pose = player.CaptureSaveState(); player.RestoreSaveState(pose);
            Physics.SyncTransforms();
            float maxHeight = float.MinValue, minHeight = float.MaxValue;
            for (int i = 0; i < 160; i++)
            {
                controller.Move(new Vector3(0, -.04f, .03f));
                if (i > 20) { maxHeight = Mathf.Max(maxHeight, root.transform.position.y); minHeight = Mathf.Min(minHeight, root.transform.position.y); }
            }
            Check(maxHeight - minHeight < .005f, $"No floor-card step after controller restore (vertical range {maxHeight - minHeight:F4} m)");
            Check(root.transform.position.z < 2.8f, "Solid wall still blocks walking");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(floor); Object.DestroyImmediate(card); Object.DestroyImmediate(wall); }
    }
}
