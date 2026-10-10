using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Replaces only authored key visuals; identity, pickup collider and scene positions stay intact.</summary>
public static class MinorKeyModelBuilder
{
    const string Folder="Assets/Prefabs/MinorSkills/KeyModels";
    static readonly string[] Shapes={"club","diamond","heart","spade"};
    [MenuItem("TCG Card Chaos/Minor Skills/Apply Imported Key Models")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
        GameObject source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/keys.fbx");
        if(source==null)throw new InvalidOperationException("Key FBX missing.");
        var report=new StringBuilder();
        for(int index=0;index<Shapes.Length;index++)
        {
            string shape=Shapes[index];Transform part=source.transform.Find("key_"+shape);
            if(part==null)throw new InvalidOperationException("Missing key "+shape);
            Mesh mesh=part.GetComponent<MeshFilter>().sharedMesh;
            var visual=new GameObject("Key_"+shape);
            try
            {
                var geometry=new GameObject("Model",typeof(MeshFilter),typeof(MeshRenderer));geometry.transform.SetParent(visual.transform,false);
                geometry.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=geometry.GetComponent<MeshRenderer>();renderer.sharedMaterial=Material(shape);
                renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                // Same 0.262m world length as the small placeholder (.69 × .38).
                float scale=.69f/mesh.bounds.size.x;geometry.transform.localScale=Vector3.one*scale;
                geometry.transform.localPosition=new Vector3(.005f-mesh.bounds.center.x*scale,.0225f-mesh.bounds.min.y*scale,-mesh.bounds.center.z*scale);
                GameObject prefab=PrefabUtility.SaveAsPrefabAsset(visual,Folder+"/Key_"+shape+".prefab");
                string path="Assets/Prefabs/MinorSkills/MinorKey"+(index+1)+".prefab";
                GameObject key=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var behavior=key.GetComponent<WorldMinorSkillKey>();
                    if(behavior.SkillIndex!=index)throw new InvalidOperationException("Key identity mismatch.");
                    var old=key.transform.Find("Visual");if(old!=null)Object.DestroyImmediate(old.gameObject);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,key.transform);instance.name="Visual";
                    behavior.Configure(index,instance.transform);
                    var collider=key.GetComponent<BoxCollider>();
                    Bounds localBounds=new Bounds(geometry.transform.localPosition+mesh.bounds.center*scale,mesh.bounds.size*scale);
                    if(!new Bounds(collider.center,collider.size).Contains(localBounds.min)||!new Bounds(collider.center,collider.size).Contains(localBounds.max))throw new InvalidOperationException("Key does not fit existing collider: "+shape);
                    PrefabUtility.SaveAsPrefabAsset(key,path);
                    report.AppendLine($"Key {index+1}: {shape}, {mesh.triangles.Length/3} triangles, world length={mesh.bounds.size.x*scale*key.transform.localScale.x:F4}m; identity/collider/root retained.");
                }
                finally{PrefabUtility.UnloadPrefabContents(key);}
            }
            finally{Object.DestroyImmediate(visual);}
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Temp/key-model-build.txt",report.ToString());
    }
    static Texture2D Texture(string shape,string suffix,bool normal=false)
    {
        string path=Folder+"/key_"+shape+"_mat_"+suffix+".png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.maxTextureSize=1024;importer.isReadable=false;importer.mipmapEnabled=true;
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.flipGreenChannel=normal; // Source normal maps are explicitly DirectX.
        importer.sRGBTexture=suffix=="Base_Color";
        importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Material(string shape)
    {
        string path=Folder+"/Key_"+shape+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Key_"+shape};AssetDatabase.CreateAsset(mat,path);}
        mat.SetTexture("_BaseMap",Texture(shape,"Base_Color"));mat.SetColor("_BaseColor",Color.white);
        // Keep the colored inlays legible under the shop's dim floor lighting.
        // Reuse the existing albedo map; no new texture or runtime material is needed.
        mat.SetTexture("_MetallicGlossMap",null);mat.SetFloat("_Metallic",.15f);mat.SetFloat("_Smoothness",.25f);mat.DisableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_EmissionMap",mat.GetTexture("_BaseMap"));mat.SetColor("_EmissionColor",new Color(.27f,.27f,.27f,1f));mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        mat.SetTexture("_BumpMap",Texture(shape,"Normal_DirectX",true));mat.EnableKeyword("_NORMALMAP");
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
    }
}
