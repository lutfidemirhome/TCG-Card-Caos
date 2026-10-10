using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Rebuilds one shared chest presentation without changing any gameplay identity or scene placement.</summary>
public static class MinorChestModelBuilder
{
    const string Package = "Assets/Animated PBR Chest Demo";
    const string Folder = "Assets/Prefabs/MinorSkills/Models";
    const string ModelPath = Folder + "/ChestModel.prefab";

    [MenuItem("TCG Card Chaos/Minor Skills/Apply Imported Chest Model")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/MinorSkills", "Models");
        var report = new StringBuilder();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Package + "/Fbx/Animated PBR Chest _Wood_Demo.fbx");
        if (source == null) throw new InvalidOperationException("Imported chest FBX missing.");
        AnimationClip clip = CopyClip();
        Material mat = Material();
        GameObject root = new GameObject("ChestModel");
        try
        {
            var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
            GameObject rig = Object.Instantiate(source, model); rig.name = "Animated";
            AnimatorUtility.DeoptimizeTransformHierarchy(rig);
            foreach (var animator in rig.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            var renderers = rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length != 1) throw new InvalidOperationException("Expected one chest mesh.");
            var skin = renderers[0]; skin.sharedMaterial = mat; skin.updateWhenOffscreen = false;
            skin.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            skin.skinnedMotionVectors = false;
            var closed = new Mesh { name = "ChestClosed" }; var open = new Mesh { name = "ChestOpen" };
            clip.SampleAnimation(rig, 0); skin.BakeMesh(closed);
            clip.SampleAnimation(rig, clip.length); skin.BakeMesh(open);
            clip.SampleAnimation(rig, 0);
            closed.RecalculateBounds(); open.RecalculateBounds();
            Mesh closedAsset = StoreMesh(closed); Mesh openAsset = StoreMesh(open);
            float scale = .835f / closedAsset.bounds.size.x;
            model.localScale = Vector3.one * scale;
            model.localRotation = Quaternion.Euler(0,180,0);
            model.localPosition = new Vector3(closedAsset.bounds.center.x * scale, -closedAsset.bounds.min.y * scale, closedAsset.bounds.center.z * scale);
            var fixedObject = new GameObject("StaticPose", typeof(MeshFilter), typeof(MeshRenderer));
            fixedObject.transform.SetParent(model, false);
            fixedObject.GetComponent<MeshRenderer>().sharedMaterial = mat;
            fixedObject.GetComponent<MeshRenderer>().motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            var view = root.AddComponent<MinorChestModelView>();
            view.Configure(rig, fixedObject.GetComponent<MeshFilter>(), closedAsset, openAsset, clip);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ModelPath);
            for(int i=0;i<4;i++)
            {
                string path = "Assets/Prefabs/MinorSkills/MinorChest"+(i+1)+".prefab";
                GameObject chest = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var behavior = chest.GetComponent<WorldMinorSkillChest>();
                    if(behavior.SkillIndex!=i) throw new InvalidOperationException("Chest identity mismatch.");
                    Transform old = chest.transform.Find("Visual"); if(old!=null) Object.DestroyImmediate(old.gameObject);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,chest.transform); instance.name="Visual";
                    behavior.Configure(i,null); behavior.ConfigureModel(instance.GetComponent<MinorChestModelView>());
                    // Keep the existing solid collider and world footprint; the fitted model stays inside it.
                    PrefabUtility.SaveAsPrefabAsset(chest,path);
                    report.AppendLine($"Chest {i+1}: same root/ID/collider; shared nested model, scale={chest.transform.localScale}");
                }
                finally { PrefabUtility.UnloadPrefabContents(chest); }
            }
            report.AppendLine($"Per chest: {skin.sharedMesh.vertexCount} vertices; {skin.sharedMesh.triangles.Length/3} triangles; one shared material; clip {clip.length:F3}s.");
            report.AppendLine($"Closed normalized bounds={closedAsset.bounds.size*scale}; open bounds={openAsset.bounds.size*scale}; world scale follows each gameplay prefab root.");
            report.AppendLine("No Animator/controller, lights, particles, physics simulation or idle skinning; one static renderer per idle chest.");
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets(); Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/chest-model-build.txt",report.ToString());
    }

    static AnimationClip CopyClip()
    {
        string path=Folder+"/ChestOpening.anim";
        var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(Package+"/Animation/Animated PBR Chest _Opening_UnCommon.anim");
        var copy=Object.Instantiate(source);copy.name="ChestOpening";
        foreach(var binding in AnimationUtility.GetCurveBindings(copy))
            if(binding.type!=typeof(Transform))AnimationUtility.SetEditorCurve(copy,binding,null);
        foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(copy))AnimationUtility.SetObjectReferenceCurve(copy,binding,null);
        AnimationUtility.SetAnimationEvents(copy,Array.Empty<AnimationEvent>());
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing!=null){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(copy,path);return copy;
    }
    static Mesh StoreMesh(Mesh mesh)
    {
        string path=Folder+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old!=null){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static Texture2D Texture(string suffix, bool normal=false)
    {
        string name="WoodChest_Wood_Chest_"+suffix+".png";string target=Folder+"/"+name;
        if(!File.Exists(target)){if(!AssetDatabase.CopyAsset(Package+"/Textures/"+name,target))throw new InvalidOperationException("Cannot copy texture "+name);}
        var importer=(TextureImporter)AssetImporter.GetAtPath(target);
        importer.maxTextureSize=1024;importer.isReadable=false;importer.mipmapEnabled=true;
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=!normal&&suffix=="AlbedoTransparency";
        importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(target);
    }
    static Material Material()
    {
        string path=Folder+"/ChestPBR.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ChestPBR"};AssetDatabase.CreateAsset(mat,path);}
        mat.SetTexture("_BaseMap",Texture("AlbedoTransparency"));mat.SetColor("_BaseColor",Color.white);
        mat.SetTexture("_BumpMap",Texture("Normal",true));mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap",Texture("MetallicSmoothness"));mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness",.838f);mat.SetFloat("_Metallic",1f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
}
