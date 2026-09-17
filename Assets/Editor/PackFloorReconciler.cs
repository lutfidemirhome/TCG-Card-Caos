using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves ownership between authored floor cards and fixed pack contents.
/// The caller owns the Undo group, including assignment changes.
/// Existing saves retain their own pack/card snapshot and are not rewritten here.
/// </summary>
public static class PackFloorReconciler
{
    const string UndoLabel = "Pack kartlarini ve zemini esitle";

    sealed class Plan
    {
        public readonly List<WorldCard> Remove = new List<WorldCard>();
        public readonly List<CardDefinition> Restore = new List<CardDefinition>();
        public readonly List<WorldCard> Ground = new List<WorldCard>();
    }

    struct FloorPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Transform Parent;
        public int Batch;
        public bool FaceDown;
    }

    public static string Describe(
        PhysicsLevelLayout layout,
        IReadOnlyCollection<CardDefinition> before,
        IReadOnlyCollection<CardDefinition> after)
    {
        return Report(BuildPlan(layout, before, after));
    }

    public static string Apply(
        PhysicsLevelLayout layout,
        IReadOnlyCollection<CardDefinition> before,
        IReadOnlyCollection<CardDefinition> after)
    {
        Plan plan = BuildPlan(layout, before, after);
        // Use vacated positions first. A fixed-size unique pack assignment swaps
        // ownership with floor cards, so every returned card has a safe old seat.
        var englishPoses = new Queue<FloorPose>();
        var japanesePoses = new Queue<FloorPose>();
        for (int i = 0; i < plan.Remove.Count; i++)
        {
            WorldCard card = plan.Remove[i];
            if (PrefabUtility.IsPartOfPrefabInstance(card.gameObject)
                && PrefabUtility.GetOutermostPrefabInstanceRoot(card.gameObject) != card.gameObject)
            {
                throw new InvalidOperationException(
                    card.name + " bir prefab'in alt nesnesi; pack atamasindan once sahnede ayri bir kart olmali.");
            }

            (card.Definition.IsJapanese ? japanesePoses : englishPoses).Enqueue(CapturePose(card));
        }

        // Resolve every destination before deleting any object. If authoring has
        // no suitable floor space, the caller can cancel the complete transaction.
        var destinations = new List<FloorPose>(plan.Restore.Count);
        for (int i = 0; i < plan.Restore.Count; i++)
        {
            CardDefinition definition = plan.Restore[i];
            Queue<FloorPose> available = definition.IsJapanese ? japanesePoses : englishPoses;
            if (available.Count > 0)
                destinations.Add(available.Dequeue());
            else
                destinations.Add(FindNearbyFloorPose(layout, definition, plan.Ground, destinations));
        }

        for (int i = 0; i < plan.Remove.Count; i++)
            Undo.DestroyObjectImmediate(plan.Remove[i].gameObject);

        for (int i = 0; i < plan.Restore.Count; i++)
        {
            CardDefinition definition = plan.Restore[i];
            FloorPose pose = destinations[i];
            WorldCard card = CardFactory.CreateWorldCard(
                pose.Position,
                pose.Rotation,
                definition,
                paletteIndex: 0,
                cardName: (definition.IsJapanese ? "JP_Card_" : "Card_") + definition.DefinitionId);
            // Register immediately so a later initialization exception can also
            // be rolled back by the enclosing assignment transaction.
            Undo.RegisterCreatedObjectUndo(card.gameObject, UndoLabel);
            SceneManager.MoveGameObjectToScene(card.gameObject, layout.gameObject.scene);
            Undo.SetTransformParent(card.transform, pose.Parent, UndoLabel);
            card.SetGroundShowsBack(pose.FaceDown);
            card.PrepareEditorPhysicsPlacement();
            PhysicsLevelItem item = Undo.AddComponent<PhysicsLevelItem>(card.gameObject);
            item.Configure(PhysicsLevelItem.AreaKind.Main, pose.Batch, isBaked: true);
            EditorUtility.SetDirty(card);
            EditorUtility.SetDirty(item);
        }

        if (plan.Remove.Count > 0 || plan.Restore.Count > 0)
            EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
        return Report(plan);
    }

    static Plan BuildPlan(
        PhysicsLevelLayout layout,
        IReadOnlyCollection<CardDefinition> before,
        IReadOnlyCollection<CardDefinition> after)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Pack atamasi icin Play Mode'u durdur.");
        if (layout == null || !layout.gameObject.scene.IsValid()
            || !layout.gameObject.scene.isLoaded || EditorUtility.IsPersistent(layout)
            || PrefabStageUtility.GetPrefabStage(layout.gameObject) != null
            || layout.MainLevelRoot == null || !layout.MainLevelRoot.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Aktif MainScene ve Physics_Card_Level/Main_Level gerekli.");

        var plan = new Plan();
        var nextIds = DefinitionIds(after);
        var presentIds = new HashSet<string>(StringComparer.Ordinal);
        GameObject[] roots = layout.gameObject.scene.GetRootGameObjects();
        for (int r = 0; r < roots.Length; r++)
        {
            WorldCard[] cards = roots[r].GetComponentsInChildren<WorldCard>(true);
            for (int i = 0; i < cards.Length; i++)
            {
                WorldCard card = cards[i];
                if (card == null || !card.gameObject.activeInHierarchy || card.UsesPsaSlab
                    || card.Definition == null || EditorUtility.IsPersistent(card))
                    continue;

                presentIds.Add(card.Definition.DefinitionId);
                if (!card.transform.IsChildOf(layout.MainLevelRoot) || card.IsInHand
                    || card.IsFlyingToShelf || !PhysicsLevelItem.IsMixStoreItem(card)
                    || card.GetComponentInParent<CardShelfSlot>() != null
                    || card.GetComponentInParent<PsaCabinetSlot>() != null)
                    continue;

                plan.Ground.Add(card);
                if (nextIds.Contains(card.Definition.DefinitionId))
                    plan.Remove.Add(card);
            }
        }

        var returnedIds = new HashSet<string>(StringComparer.Ordinal);
        if (before != null)
        {
            foreach (CardDefinition definition in before)
            {
                if (definition == null || nextIds.Contains(definition.DefinitionId)
                    || presentIds.Contains(definition.DefinitionId)
                    || !returnedIds.Add(definition.DefinitionId))
                    continue;
                plan.Restore.Add(definition);
            }
        }
        plan.Restore.Sort((a, b) => string.CompareOrdinal(a.DefinitionId, b.DefinitionId));
        // Stable ordering keeps each author's changes deterministic.
        plan.Remove.Sort((a, b) => string.CompareOrdinal(a.Definition.DefinitionId, b.Definition.DefinitionId));
        return plan;
    }

    static HashSet<string> DefinitionIds(IReadOnlyCollection<CardDefinition> definitions)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (definitions == null)
            return result;
        foreach (CardDefinition definition in definitions)
        {
            if (definition != null)
                result.Add(definition.DefinitionId);
        }
        return result;
    }

    static FloorPose CapturePose(WorldCard card)
    {
        SerializedProperty faceDown = new SerializedObject(card).FindProperty("groundShowsBack");
        return new FloorPose
        {
            Position = card.transform.position,
            Rotation = card.transform.rotation,
            Parent = card.transform.parent,
            Batch = card.GetComponent<PhysicsLevelItem>().BatchIndex,
            FaceDown = faceDown != null && faceDown.boolValue,
        };
    }

    static FloorPose FindNearbyFloorPose(
        PhysicsLevelLayout layout,
        CardDefinition definition,
        List<WorldCard> ground,
        List<FloorPose> planned)
    {
        float width = CardDimensions.Width * CardDimensions.GroundCardScale;
        float length = CardDimensions.Height * CardDimensions.GroundCardScale;
        float spacing = Mathf.Sqrt(width * width + length * length) + 0.04f;
        float groundY = CardFactory.GroundHeightOffset();
        var overlaps = new Collider[32];
        Physics.SyncTransforms();
        for (int i = 0; i < ground.Count; i++)
        {
            WorldCard anchor = ground[i];
            if (anchor.Definition.IsJapanese != definition.IsJapanese
                || Mathf.Abs(anchor.transform.position.y - groundY) > 0.2f)
                continue;
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI * 0.25f;
                Vector3 position = anchor.transform.position
                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spacing;
                position.y = groundY;
                bool occupied = false;
                // Flat editor cards may sit below the environment clearance box.
                // Check their footprints even when their colliders are disabled.
                for (int j = 0; j < ground.Count; j++)
                {
                    Vector3 delta = ground[j].transform.position - position;
                    if (Mathf.Abs(delta.y) < 0.2f
                        && delta.x * delta.x + delta.z * delta.z < spacing * spacing)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (occupied)
                    continue;
                for (int j = 0; j < planned.Count; j++)
                {
                    Vector3 delta = planned[j].Position - position;
                    if (delta.x * delta.x + delta.z * delta.z < spacing * spacing)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (occupied)
                    continue;

                // Keep clear of furniture, walls and other floor items. The box
                // starts above the floor surface rather than intersecting it.
                int hits = Physics.OverlapBoxNonAlloc(
                    position + Vector3.up * 0.045f,
                    new Vector3(spacing * 0.5f, 0.025f, spacing * 0.5f),
                    overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (hits > 0)
                    continue;
                bool supported = Physics.Raycast(
                    position + Vector3.up * 0.25f, Vector3.down,
                    out RaycastHit floor, 0.5f, ~0, QueryTriggerInteraction.Ignore)
                    && floor.collider.gameObject.scene == layout.gameObject.scene
                    && floor.normal.y > 0.9f && Mathf.Abs(floor.point.y - groundY) < 0.06f;
                if (!supported)
                    continue;

                FloorPose pose = CapturePose(anchor);
                pose.Position = position;
                pose.Rotation = Quaternion.Euler(0f, anchor.transform.eulerAngles.y, 0f);
                pose.FaceDown = false;
                return pose;
            }
        }

        throw new InvalidOperationException(
            definition.DisplayName + " icin guvenli zemin konumu bulunamadi. "
            + "Ayni dilde yerde bulunan baska bir karti pakete atayarak yer ac.");
    }

    static string Report(Plan plan)
    {
        return plan.Remove.Count + " kart yerden kaldirilacak; "
            + plan.Restore.Count + " eski pack karti zemine geri konacak.";
    }
}
