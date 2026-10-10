using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Author key outlines and split only the chest's lock plate into a tinted material.</summary>
public static class MinorKeyChestPresentationBuilder
{
    const string Folder = "Assets/Prefabs/MinorSkills/Models";
    [MenuItem("TCG Card Chaos/Minor Skills/Build Key And Lock Presentation")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        var closed = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/ChestClosed.asset");
        var open = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/ChestOpen.asset");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ChestModel.prefab");
        var skin = template.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var vertices = closed.vertices; var triangles = closed.triangles;
        var ordinary = new List<int>(); var lockPlate = new List<int>();
        for (int i=0; i<triangles.Length; i+=3)
        {
            Vector3 center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3f;
            bool isLock=Mathf.Abs(center.x)<.405f && center.y>.79f && center.y<1.375f && center.z>.745f;
            var target=isLock?lockPlate:ordinary;
            target.Add(triangles[i]);target.Add(triangles[i+1]);target.Add(triangles[i+2]);
        }
        if(lockPlate.Count<30) throw new InvalidOperationException("Lock plate geometry was not found.");
        Mesh closedTint=Split(closed,"ChestClosedLockTint",ordinary,lockPlate);
        Mesh openTint=Split(open,"ChestOpenLockTint",ordinary,lockPlate);
        Mesh skinTint=Split(skin.sharedMesh,"ChestRigLockTint",ordinary,lockPlate);
        Color[] colors={new Color(.22f,.85f,.2f),new Color(.68f,.18f,.95f),new Color(1f,.15f,.18f),new Color(.16f,.42f,1f)};
        for(int i=0;i<4;i++)
        {
            string keyPath="Assets/Prefabs/MinorSkills/MinorKey"+(i+1)+".prefab";
            var key=PrefabUtility.LoadPrefabContents(keyPath);
            try
            {
                var visual=key.transform.Find("Visual");
                var outline=visual.GetComponent<Outline>(); if(outline==null)outline=visual.gameObject.AddComponent<Outline>();
                outline.OutlineMode=Outline.Mode.OutlineVisible;outline.OutlineWidth=3;outline.OutlineColor=Color.yellow;
                var so=new SerializedObject(outline);so.FindProperty("precomputeOutline").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                outline.SendMessage("OnValidate",SendMessageOptions.DontRequireReceiver);outline.enabled=false;
                PrefabUtility.SaveAsPrefabAsset(key,keyPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(key);}
            string materialPath=Folder+"/ChestLock"+(i+1)+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var baseMat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ChestPBR.mat");
            if(mat==null){mat=new Material(baseMat);AssetDatabase.CreateAsset(mat,materialPath);}
            else mat.CopyPropertiesFromMaterial(baseMat);
            mat.name="ChestLock"+(i+1);mat.SetColor("_BaseColor",colors[i]);EditorUtility.SetDirty(mat);
            string chestPath="Assets/Prefabs/MinorSkills/MinorChest"+(i+1)+".prefab";
            var chest=PrefabUtility.LoadPrefabContents(chestPath);
            try
            {
                var view=chest.GetComponentInChildren<MinorChestModelView>(true);
                var sm=view.GetComponentInChildren<SkinnedMeshRenderer>(true);sm.sharedMesh=skinTint;sm.sharedMaterials=new[]{baseMat,mat};
                var mf=view.GetComponentInChildren<MeshFilter>(true);mf.GetComponent<MeshRenderer>().sharedMaterials=new[]{baseMat,mat};
                var so=new SerializedObject(view);so.FindProperty("closedMesh").objectReferenceValue=closedTint;so.FindProperty("openMesh").objectReferenceValue=openTint;so.ApplyModifiedPropertiesWithoutUndo();view.SetOpen(false);
                Transform socket=chest.transform.Find("KeySocket");if(socket==null){socket=new GameObject("KeySocket").transform;socket.SetParent(chest.transform,false);}
                socket.position=mf.transform.TransformPoint(new Vector3(0,1.12f,.782f));
                // Original front is +Z; the normalized presentation rotates it toward -Z.
                socket.rotation=Quaternion.LookRotation(-mf.transform.forward,chest.transform.up);
                chest.GetComponent<WorldMinorSkillChest>().ConfigureSocket(socket);
                PrefabUtility.SaveAsPrefabAsset(chest,chestPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(chest);}
        }
        const string buttonPath = "Assets/UI/Skills/Minor/PopupConfirm.png";
        if (!System.IO.File.Exists(buttonPath)) AssetDatabase.CopyAsset("Assets/UI/Skills/Art/skill_upgrade_button.png", buttonPath);
        const string popupPath = "Assets/Resources/UI/Skills/MinorSkillUnlock.prefab";
        var popup = PrefabUtility.LoadPrefabContents(popupPath);
        try
        {
            var button = popup.transform.Find("Panel/Body/Close");
            button.GetComponent<UnityEngine.UI.Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(buttonPath);
            button.Find("Label").GetComponent<TMPro.TMP_Text>().color = new Color(.04f,.07f,.01f);
            PrefabUtility.SaveAsPrefabAsset(popup, popupPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(popup); }
        AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText("Temp/key-presentation-build.txt","Lock triangles: "+lockPlate.Count/3+"; four shared-texture tint materials; four baked key outlines.");
    }
    static Mesh Split(Mesh source,string name,List<int> ordinary,List<int> plate)
    {
        var copy=Object.Instantiate(source);copy.name=name;copy.subMeshCount=2;copy.SetTriangles(ordinary,0);copy.SetTriangles(plate,1);
        string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing!=null){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(copy,path);return copy;
    }
}
