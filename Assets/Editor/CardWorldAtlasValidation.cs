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
                Check(page.enableInstancing && page.shader.name == CardWorldAtlasRenderer.ShaderName, page.name + " keeps instanced atlas variant in builds");
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
                    var source = cards.First(c => c.Definition && !c.UsesPsaSlab && c.Definition.IsJapanese == japanese && !PhysicsLevelItem.IsMixStoreDisplayOnly(c));
                    CheckLifecycle(source, player);
                }
                CheckDemoBoundaries(cards, player);
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
            Check(IsAtlas(renderer), language + " distant card uses atlas");
            Check(renderer.sharedMaterials.Length == 1 && mesh.subMeshCount == 1, language + " near/far card has one draw submission");
            var detail = CardArtLibrary.GetCardMaterials(clone.Definition, CardTextureQuality.Detail)[0];
            Check(renderer.sharedMaterial.GetTexture("_CardBackMap") == CardArtLibrary.GetCardMaterials(clone.Definition, CardTextureQuality.Detail)[1].GetTexture("_BaseMap"), language + " back is unchanged");
            clone.transform.position = Camera.main.transform.position + Vector3.up * 2; clone.RefreshAuthoredVisual();
            Check(!IsAtlas(renderer) && renderer.sharedMaterial.GetTexture("_BaseMap") == clone.Definition.FrontTexture, language + " close view uses original art");
            Check(renderer.sharedMaterial.shader.name == CardWorldAtlasRenderer.ShaderName && renderer.sharedMaterial.IsKeywordEnabled("_CARD_SINGLE_PASS"), language + " close card keeps demo lighting");
            Check(renderer.GetComponent<MeshFilter>().sharedMesh == mesh, language + " distance transition preserves authored mesh");
            clone.SetInteractionHighlight(true);
            var outline = renderer.transform.Find("InteractionOutline");
            Check(outline && outline.gameObject.activeInHierarchy, language + " aim outline remains visible");
            clone.SetInteractionHighlight(false);
            clone.SetInteractionHighlight(true);
            Check(outline && renderer.transform.Find("InteractionOutline") == outline && outline.gameObject.activeInHierarchy,
                language + " aim outline is reused when selecting again");
            clone.SetInteractionHighlight(false);
            clone.transform.position = far; clone.RefreshAuthoredVisual();
            Check(renderer.sharedMaterial.GetFloat("_Smoothness") == detail.GetFloat("_Smoothness")
                && renderer.sharedMaterial.GetColor("_BaseColor") == detail.GetColor("_BaseColor")
                && renderer.sharedMaterial.GetFloat("_Metallic") == detail.GetFloat("_Metallic")
                && renderer.sharedMaterial.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF") == detail.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF"), language + " atlas preserves lit response and tint");
            clone.BeginPickupFlight(player.transform, 1, .3f, .1f);
            Check(clone.IsInHand && !IsAtlas(renderer) && !renderer.HasPropertyBlock(), language + " pickup immediately restores detail and clears atlas UVs");
            Check(CardSinglePassMesh.GetSourceOrSelf(renderer.GetComponent<MeshFilter>().sharedMesh) == CardArtLibrary.CardMesh || CardSinglePassMesh.GetSourceOrSelf(renderer.GetComponent<MeshFilter>().sharedMesh) == CardArtLibrary.HandCardMesh, language + " pickup keeps the existing detailed hand mesh");
            clone.DropWithPhysics(Vector3.zero);
            Check(clone.IsPhysicsSimulating && !IsAtlas(renderer), language + " thrown card stays detailed");
            slot.transform.position = far; clone.PlaceOnShelfSlot(slot.transform, .003f);
            Check(!IsAtlas(renderer), language + " shelf card retains original effect-compatible material");
            clone.gameObject.SetActive(false);
            Check(!renderer.HasPropertyBlock(), language + " disabling clears atlas properties");
        }
        finally { Object.DestroyImmediate(clone.gameObject); Object.DestroyImmediate(slot); }
    }

    static bool IsAtlas(MeshRenderer renderer) => renderer.sharedMaterial.shader.name == CardWorldAtlasRenderer.ShaderName && renderer.HasPropertyBlock();

    static void CheckDemoBoundaries(WorldCard[] cards, FirstPersonController player)
    {
        Check(GameBuildVariant.IsDemo, "Demo build variant remains enabled");
        Check(GameBuildVariant.FolderName == GameBuildVariant.DemoFolderName, "Demo save folder stays separate");
        var layout = PhysicsLevelLayout.FindExisting();
        Check(layout && layout.DemoCardsRoot && layout.MainLevelRoot, "Authored demo and display-only areas still separate");
        Check(DemoShelfTargets.ObjectNames.Length == 10, "All ten demo completion targets retained");
        var display = cards.First(c => c.Definition && PhysicsLevelItem.IsMixStoreDisplayOnly(c));
        var demo = cards.First(c => c.Definition && c.GetComponent<PhysicsLevelItem>() && c.GetComponent<PhysicsLevelItem>().Area == PhysicsLevelItem.AreaKind.Demo);
        Check(!PhysicsLevelItem.IsMixStoreDisplayOnly(demo), "Demo cards remain interactable");
        var pose = display.transform.position; var parent = display.transform.parent;
        display.Interact(player.gameObject);
        Check(!display.IsInHand && display.transform.position == pose && display.transform.parent == parent
            && display.GetPromptText() == string.Empty, "Main-area decorative cards remain unpickable in demo");
        var packs = Object.FindObjectsByType<WorldBoosterPack>(FindObjectsSortMode.None);
        Check(packs.Any(p => PhysicsLevelItem.IsMixStoreDisplayOnly(p)), "Decorative pack restrictions retained");
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
