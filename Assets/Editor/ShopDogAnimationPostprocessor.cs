using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Closes only the two French Dog locomotion clips during their normal import.</summary>
public sealed class ShopDogAnimationPostprocessor : AssetPostprocessor
{
    private const string WalkPath = "Assets/French Dog/Animations/Dog_Walk.fbx";
    private const string RunPath = "Assets/French Dog/Animations/Dog_Run.fbx";
    private const float SourceFrameRate = 30f;

    private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
    {
        int poseCount;
        if (assetPath == WalkPath) poseCount = 18;
        else if (assetPath == RunPath) poseCount = 8;
        else return;

        if (!(assetImporter is ModelImporter importer)
            || importer.animationType != ModelImporterAnimationType.Legacy
            || Mathf.Abs(clip.frameRate - SourceFrameRate) > 0.01f)
        {
            Debug.LogError("[Shop Dog] Yürüme/koşma klibinin formatı değişmiş; döngü düzeltmesi uygulanmadı.", clip);
            return;
        }

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        var curves = new List<CurveSamples>(bindings.Length);
        float sourceStart = float.PositiveInfinity;
        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.type != typeof(Transform)) continue;
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null || curve.length == 0) continue;
            curves.Add(new CurveSamples(binding, curve, poseCount));
            // Read the moving curves' actual timebase: FBX frame 1 need not remain
            // time 1/30, and constant filler curves can start earlier than motion.
            if (HasChangingValues(curve)) sourceStart = Mathf.Min(sourceStart, curve[0].time);
        }
        if (curves.Count == 0 || float.IsInfinity(sourceStart))
        {
            Debug.LogError("[Shop Dog] Yürüme/koşma klibinde değişen animasyon eğrisi bulunamadı; döngü düzeltmesi uygulanmadı.", clip);
            return;
        }

        foreach (CurveSamples curve in curves)
        {
            for (int frame = 0; frame < poseCount; frame++)
                curve.values[frame] = curve.source.Evaluate(sourceStart + frame / SourceFrameRate);
            if (curve.isEuler)
            {
                for (int frame = 1; frame < poseCount; frame++)
                    curve.values[frame] = curve.values[frame - 1]
                        + Mathf.DeltaAngle(curve.values[frame - 1], curve.values[frame]);
            }
        }
        NormalizeRotations(curves, poseCount);

        var repairedBindings = new EditorCurveBinding[curves.Count];
        var repairedCurves = new AnimationCurve[curves.Count];
        for (int i = 0; i < curves.Count; i++)
        {
            repairedBindings[i] = curves[i].binding;
            repairedCurves[i] = MakeClosedCurve(curves[i], poseCount);
        }

        // Edit the transient imported clip, preserving its name, GUID and file ID.
        // This work runs in the Editor, with no clip construction during gameplay.
        AnimationUtility.SetEditorCurves(clip, repairedBindings, repairedCurves);
        clip.EnsureQuaternionContinuity();
        clip.wrapMode = WrapMode.Loop;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f;
        settings.stopTime = poseCount / SourceFrameRate;
        settings.loopTime = true;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static bool HasChangingValues(AnimationCurve curve)
    {
        float firstValue = curve[0].value;
        for (int i = 1; i < curve.length; i++)
        {
            if (Mathf.Abs(curve[i].value - firstValue) > 0.0000001f) return true;
        }
        return false;
    }

    private static void NormalizeRotations(List<CurveSamples> curves, int poseCount)
    {
        var rotations = new Dictionary<string, CurveSamples[]>(StringComparer.Ordinal);
        foreach (CurveSamples curve in curves)
        {
            string property = curve.binding.propertyName;
            if (!property.StartsWith("m_LocalRotation.", StringComparison.Ordinal)) continue;
            int axis = "xyzw".IndexOf(property[property.Length - 1]);
            if (axis < 0) continue;
            if (!rotations.TryGetValue(curve.binding.path, out CurveSamples[] components))
            {
                components = new CurveSamples[4];
                rotations.Add(curve.binding.path, components);
            }
            components[axis] = curve;
        }

        foreach (CurveSamples[] components in rotations.Values)
        {
            if (components[0] == null || components[1] == null
                || components[2] == null || components[3] == null) continue;
            Quaternion previous = Quaternion.identity;
            for (int frame = 0; frame < poseCount; frame++)
            {
                var rotation = new Quaternion(components[0].values[frame], components[1].values[frame],
                    components[2].values[frame], components[3].values[frame]);
                rotation = Quaternion.Normalize(rotation);
                if (frame > 0 && Quaternion.Dot(previous, rotation) < 0f)
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                components[0].values[frame] = rotation.x;
                components[1].values[frame] = rotation.y;
                components[2].values[frame] = rotation.z;
                components[3].values[frame] = rotation.w;
                previous = rotation;
            }
        }
    }

    private static AnimationCurve MakeClosedCurve(CurveSamples curve, int poseCount)
    {
        float[] values = curve.values;
        float duration = poseCount / SourceFrameRate;
        bool constant = true;
        for (int i = 1; i < poseCount; i++)
        {
            if (Mathf.Abs(values[i] - values[0]) > 0.0000001f)
            {
                constant = false;
                break;
            }
        }
        if (constant)
            return new AnimationCurve(new Keyframe(0f, values[0], 0f, 0f),
                new Keyframe(duration, values[0], 0f, 0f));

        var keys = new Keyframe[poseCount + 1];
        for (int i = 0; i < poseCount; i++)
        {
            float before = values[(i + poseCount - 1) % poseCount];
            float after = values[(i + 1) % poseCount];
            float incoming = Difference(before, values[i], curve.isEuler) * SourceFrameRate;
            float outgoing = Difference(values[i], after, curve.isEuler) * SourceFrameRate;
            // Periodic, finite tangents: match velocity at the seam and avoid overshoot.
            float tangent = 0f;
            if (incoming * outgoing > 0f)
            {
                float limit = 3f * Mathf.Min(Mathf.Abs(incoming), Mathf.Abs(outgoing));
                tangent = Mathf.Clamp((incoming + outgoing) * 0.5f, -limit, limit);
            }
            keys[i] = new Keyframe(i / SourceFrameRate, values[i], tangent, tangent);
        }

        float closingValue = curve.isEuler
            ? values[poseCount - 1] + Mathf.DeltaAngle(values[poseCount - 1], values[0])
            : values[0];
        keys[poseCount] = new Keyframe(duration, closingValue, keys[0].inTangent, keys[0].outTangent);
        var result = new AnimationCurve(keys)
        {
            preWrapMode = WrapMode.Loop,
            postWrapMode = WrapMode.Loop
        };
        return result;
    }

    private static float Difference(float from, float to, bool euler)
    {
        return euler ? Mathf.DeltaAngle(from, to) : to - from;
    }

    private sealed class CurveSamples
    {
        public readonly EditorCurveBinding binding;
        public readonly AnimationCurve source;
        public readonly float[] values;
        public readonly bool isEuler;

        public CurveSamples(EditorCurveBinding binding, AnimationCurve source, int poseCount)
        {
            this.binding = binding;
            this.source = source;
            values = new float[poseCount];
            isEuler = binding.propertyName.IndexOf("localEulerAngles", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
