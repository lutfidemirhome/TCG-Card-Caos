using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates only the authored Tab popup. Task, hotbar, scenes and skill progression are untouched.</summary>
public static class SkillPanelDesignBuilder
{
    const string PrefabPath = "Assets/Resources/UI/Skills/SkillUI.prefab";
    const string Art = "Assets/UI/Skills/Panel/";
    static readonly Color Ink = new Color32(17, 29, 60, 255);
    static readonly Color Muted = new Color32(100, 91, 73, 255);
    static readonly Color Gold = new Color32(184, 140, 66, 255);
    static Material textMaterial;
    static TMP_FontAsset font;

    [MenuItem("TCG Card Chaos/UI/Apply Skill Panel Design")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before updating the skill prefab.");
        Directory.CreateDirectory(Art);
        ImportSprite(Art + "ParchmentPanel.png", 96, 2048);
        Skin("Inset", new Color32(249,239,217,235), new Color32(241,224,191,235), new Color32(208,179,129,255), 2);
        Skin("Badge", new Color32(250,240,214,255), new Color32(235,215,174,255), Gold, 2);
        Skin("Level", new Color32(248,224,166,255), new Color32(236,203,131,255), new Color32(206,163,74,255), 1);
        Skin("Tile", new Color32(96,91,79,255), new Color32(73,71,64,255), new Color32(48,47,43,255), 3);
        Skin("Selection", Color.clear, Color.clear, new Color32(234,183,59,255), 3);
        Skin("Upgrade", new Color32(91,126,58,255), new Color32(53,86,37,255), new Color32(221,181,84,255), 3);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("19133410948514086983d0026b2d8795"));
        textMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/UI/Skills/Materials/SkillTextNoOutline.mat");
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform panel = root.transform.Find("Panel"), body = panel.Find("Body");
            panel.GetComponent<Image>().color = new Color(0.008f, 0.012f, 0.03f, 0.98f);
            var bodyRect = (RectTransform)body;
            bodyRect.anchorMin = bodyRect.anchorMax = bodyRect.pivot = new Vector2(.5f,.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = new Vector2(1600,900);
            SetImage(body.GetComponent<Image>(), "ParchmentPanel");
            Text(body.Find("Title"), 100, 64, 900, 76, 64 * .85f, Ink, TextAlignmentOptions.Left);
            Text(body.Find("Instructions"), 104, 142, 932, 66, 26 * .85f, Muted, TextAlignmentOptions.TopLeft);
            Image pointsBg = Decor(body,"PointsFrame",1080,86,418,78,"Badge");
            pointsBg.transform.SetAsFirstSibling();
            Text(body.Find("Points"),1100,103,376,44,28 * .85f,Ink,TextAlignmentOptions.Center);
            Line(body,"HeaderRule",86,222,1428,2);
            Line(body,"Divider",876,258,2,590);

            Transform nodes = body.Find("Nodes");
            Rect(nodes, 86, 318, 770, 226);
            // Reorder visually to match the existing 1–5 hotbar; permanent skill IDs stay unchanged.
            for (int slot = 0; slot < SkillCatalog.Count; slot++)
            {
                int id = (int)SkillCatalog.HotbarSkill(slot);
                Transform node = nodes.Find("Skill" + id);
                Rect(node, slot * 154 + 9, 0, 126, 132);
                SetImage(node.GetComponent<Image>(), "Badge");
                node.GetComponent<Image>().color = new Color32(136,125,98,255);
                SetupButton(node.GetComponent<Button>(), false);
                Rect(node.Find("Icon"),-7.2f,-5.5f,140.4f,143);
                if (id == (int)CardSkill.Sort || id == (int)CardSkill.ShelfGuide)
                {
                    // Both 75x81 sprites have alpha bounds centered at (37.5, 41).
                    // Center the aspect-preserved artwork, not its top-left-pivot rect.
                    var iconRect = (RectTransform)node.Find("Icon");
                    iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(.5f,.5f);
                    iconRect.anchoredPosition = new Vector2(0,143f / 162f);
                }
                var icon = node.Find("Icon").GetComponent<Image>();
                icon.color = Color.white; icon.preserveAspect = true; icon.raycastTarget = false;
                Image selection = Decor(node,"Selection",-6,-6,138,144,"Selection");
                selection.color = new Color(.15f,1f,.5f,1f);
                selection.pixelsPerUnitMultiplier = .7f;
                selection.gameObject.SetActive(id == 0);
                Text(node.Find("Name"),-9,150,144,64,25 * .85f,Ink,TextAlignmentOptions.Top);
                Image levelBg = Decor(node,"LevelFrame",-5,217,136,38,"Badge");
                levelBg.transform.SetAsFirstSibling();
                Text(node.Find("Level"),-2,219,130,34,22 * .85f,Muted,TextAlignmentOptions.Center);
                Rect(node.Find("Check"),103,107,22,22);
            }
            Line(body,"SkillsRule",110,286,718,2);
            Line(body,"ProgressRule",110,610,718,2);

            Transform details = body.Find("Details");
            Rect(details,906,232,602,590);
            SetImage(details.GetComponent<Image>(),"Inset");
            Rect(details.Find("IconFrame"),28,26,128,136);
            SetImage(details.Find("IconFrame").GetComponent<Image>(),"Badge");
            details.Find("IconFrame").GetComponent<Image>().color = new Color32(136,125,98,255);
            Image detailSelection = Decor(details.Find("IconFrame"),"Selection",-4,-4,136,144,"Selection");
            detailSelection.color = new Color(.15f,1f,.5f,1f);
            detailSelection.pixelsPerUnitMultiplier = .7f;
            Rect(details.Find("IconFrame/Icon"),-7.5f,-10,143,156);
            Text(details.Find("Name"),186,30,382,67,39 * .85f,Ink,TextAlignmentOptions.Left);
            Image selectedLevel = Decor(details,"LevelFrame",186,110,260,40,"Level");
            selectedLevel.transform.SetAsFirstSibling();
            Text(details.Find("Level"),198,112,236,36,24 * .85f,Ink,TextAlignmentOptions.Center);
            // Existing divider belongs to the detail panel, if present.
            Transform oldLine = details.Find("DescriptionDivider");
            if (oldLine != null) oldLine.gameObject.SetActive(false);
            Line(details,"TitleRule",30,180,542,2);
            Text(details.Find("Description"),32,198,538,146,25 * .85f,Ink,TextAlignmentOptions.TopLeft);
            Line(details,"StatsRule",30,358,542,2);
            Text(details.Find("Next"),32,376,538,42,29 * .85f,Ink,TextAlignmentOptions.Left);
            Image statsBg = Decor(details,"StatsFrame",26,426,550,92,"Badge");
            statsBg.transform.SetAsFirstSibling();
            Text(details.Find("Stats"),42,436,518,76,27 * .85f,Ink,TextAlignmentOptions.TopLeft);
            details.Find("Stats").GetComponent<TMP_Text>().lineSpacing = 12;
            Transform upgrade = details.Find("Upgrade");
            Rect(upgrade,28,528,546,52);
            SetImage(upgrade.GetComponent<Image>(),"Upgrade");
            SetupButton(upgrade.GetComponent<Button>(),true);
            Text(upgrade.Find("Label"),22,6,502,46,30 * .85f,new Color32(255,246,217,255),TextAlignmentOptions.Center);

            Transform close = panel.Find("Close") ?? body.Find("Close");
            close.SetParent(panel,false);
            Rect(close,40,32,240,56);
            Image closeImage = close.GetComponent<Image>();
            closeImage.sprite = null; closeImage.color = Color.clear; closeImage.raycastTarget = true;
            SetupButton(close.GetComponent<Button>(),false);
            Image key = Decor(close,"Key",0,0,52,56,null);
            key.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/ingame/esc_icon.png");
            key.preserveAspect = true;
            Text(close.Find("Label"),66,4,166,48,30 * .85f,Color.white,TextAlignmentOptions.Left);
            MinorSkillPanelBuilder.ApplyToRoot(root.transform);
            panel.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[SkillPanelDesign] Updated Tab popup, preserved Task/Hotbar and all skill IDs.");
    }

    static void SetupButton(Button button, bool upgrade)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f,1.1f,1.04f,1);
        colors.pressedColor = new Color(.82f,.85f,.78f,1);
        colors.selectedColor = Color.white;
        colors.disabledColor = upgrade ? new Color(.68f,.67f,.62f,.7f) : Color.gray;
        colors.colorMultiplier = 1; colors.fadeDuration = .1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        Navigation nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav;
    }
    static void Rect(Transform transform,float x,float y,float w,float h)
    {
        var r = (RectTransform)transform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0,1);
        r.anchoredPosition = new Vector2(x,-y); r.sizeDelta = new Vector2(w,h);
        r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
    }
    static void Text(Transform transform,float x,float y,float w,float h,float size,Color color,TextAlignmentOptions align)
    {
        Rect(transform,x,y,w,h);
        TMP_Text t = transform.GetComponent<TMP_Text>();
        t.font = font; t.fontSharedMaterial = textMaterial; t.color = color;
        t.fontSize = t.fontSizeMax = size; t.fontSizeMin = Mathf.Min(size,17);
        t.enableAutoSizing = true; t.alignment = align; t.richText = true;
        t.fontStyle = FontStyles.Normal; t.characterSpacing = 0; t.lineSpacing = 0;
        t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
        t.margin = Vector4.zero; t.raycastTarget = false;
    }
    static Transform Label(Transform parent,string name)
    {
        Transform t = parent.Find(name);
        if (t == null) { var go = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.layer=5; t=go.transform; t.SetParent(parent,false); }
        return t;
    }
    static Image Decor(Transform parent,string name,float x,float y,float w,float h,string sprite)
    {
        Transform t = parent.Find(name);
        if (t == null) { var go = new GameObject(name,typeof(RectTransform),typeof(Image)); go.layer=5; t=go.transform; t.SetParent(parent,false); }
        Rect(t,x,y,w,h); Image im=t.GetComponent<Image>();
        if(sprite != null) SetImage(im,sprite); else {im.sprite=null; im.color=Color.white;}
        im.raycastTarget=false; return im;
    }
    static void Line(Transform p,string name,float x,float y,float w,float h)
    {
        Image im=Decor(p,name,x,y,w,h,null); im.color=new Color(Gold.r,Gold.g,Gold.b,.52f);
    }
    static void SetImage(Image image,string sprite)
    {
        image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+sprite+".png");
        if(image.sprite == null) throw new InvalidOperationException("Missing panel sprite: "+sprite);
        image.color=Color.white; image.type=Image.Type.Sliced; image.preserveAspect=false;
        image.raycastTarget=image.GetComponent<Button>() != null;
    }
    static void ImportSprite(string path,int border,int maxSize)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.isReadable=false;
        importer.maxTextureSize=maxSize; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit=100; importer.spriteBorder=new Vector4(border,border,border,border);
        importer.npotScale=TextureImporterNPOTScale.None; importer.wrapMode=TextureWrapMode.Clamp;
        var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect; settings.spriteGenerateFallbackPhysicsShape=false;
        importer.SetTextureSettings(settings); importer.SaveAndReimport();
    }
    // Small nine-slice UI primitives. Painted paper remains the separately generated texture.
    static void Skin(string name,Color top,Color bottom,Color border,float thickness)
    {
        const int n=96; var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);
        var pixels=new Color[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            float qx=Mathf.Abs(x-47.5f)-30, qy=Mathf.Abs(y-47.5f)-30;
            float d=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-15;
            Color c=d > -thickness ? border : Color.Lerp(bottom,top,y/95f);
            c.a*=Mathf.Clamp01(.5f-d); pixels[y*n+x]=c;
        }
        texture.SetPixels(pixels); texture.Apply();
        string path=Art+name+".png"; File.WriteAllBytes(path,texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture); ImportSprite(path,22,128);
    }
}
