using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CabinetTutorialBuilder
{
    public const string Path = "Assets/Resources/UI/CabinetTutorial/CabinetTutorial.prefab";
    const string Art = "Assets/UI/CabinetTutorial/";
    [MenuItem("TCG Card Chaos/UI/Rebuild Cabinet Tutorial")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Resources/UI/CabinetTutorial");
        var root = Rect("CabinetTutorial", null, Vector2.zero, Vector2.zero);
        Stretch(root);
        var panel = Rect("Panel", root, Vector2.zero, Vector2.zero); Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = new Color(.015f,.025f,.08f,.86f);
        var body = Rect("Body", panel, new Vector2(0,20), new Vector2(1348,888));
        var bg = body.gameObject.AddComponent<Image>(); bg.sprite = Sprite("TutorialPanel.png"); bg.type = Image.Type.Sliced;
        var title = Text(body,"Title",new Vector2(0,398),new Vector2(1200,70),48,"tutorial.cabinet.title");
        title.color = new Color32(255,210,0,255);
        MakeImage(body,"HorizontalDivider",new Vector2(0,269),new Vector2(1316,9),"Divider.png");
        var vertical = MakeImage(body,"VerticalDivider",new Vector2(0,0),new Vector2(751,9),"Divider.png");
        vertical.rectTransform.localRotation = Quaternion.Euler(0,0,90);
        Text(body,"NormalTitle",new Vector2(-330,302),new Vector2(620,72),42,"tutorial.cabinet.normal.title");
        Text(body,"GradedTitle",new Vector2(330,302),new Vector2(620,72),42,"tutorial.cabinet.graded.title");
        var normal = Text(body,"NormalText",new Vector2(-330,139),new Vector2(620,240),25,"tutorial.cabinet.normal.text");
        normal.alignment = TextAlignmentOptions.TopLeft;
        var graded = Text(body,"GradedText",new Vector2(330,173),new Vector2(620,160),25,"tutorial.cabinet.graded.text");
        graded.alignment = TextAlignmentOptions.TopLeft;
        MakeImage(body,"NormalCardsGuide",new Vector2(-330,-198),new Vector2(546,367),"NormalCardsGuide.png",true);
        MakeImage(body,"GradedCardsGuide",new Vector2(330,-150),new Vector2(604,406),"GradedCardsGuide.png",true);
        var button = Rect("Continue",body,new Vector2(0,-444),new Vector2(240,88));
        var buttonImage = button.gameObject.AddComponent<Image>(); buttonImage.sprite = Sprite("Continue.png"); buttonImage.type = Image.Type.Sliced;
        var click = button.gameObject.AddComponent<Button>(); click.targetGraphic = buttonImage;
        SkillPanelButtonFeedback.Ensure(click);
        var label = Text(button,"Label",Vector2.zero,new Vector2(212,66),38,"tutorial.cabinet.close");
        label.fontSharedMaterial = label.font.material; // Same preset as the pause menu Resume label.
        root.gameObject.AddComponent<CabinetTutorialView>();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,Path);
        Object.DestroyImmediate(root.gameObject);
        Debug.Log("[Cabinet Tutorial] Two-column illustrated tutorial rebuilt.");
    }
    static Sprite Sprite(string file) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + file);
    static Image MakeImage(Transform parent,string name,Vector2 pos,Vector2 size,string file,bool aspect=false)
    {
        var image=Rect(name,parent,pos,size).gameObject.AddComponent<Image>();
        image.sprite=Sprite(file);image.preserveAspect=aspect;image.raycastTarget=false;return image;
    }
    static RectTransform Rect(string name,Transform parent,Vector2 pos,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
    static TMP_Text Text(Transform parent,string name,Vector2 pos,Vector2 size,float fontSize,string key)
    {
        var text=Rect(name,parent,pos,size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font=UiMenuFont.Font;text.color=Color.white;text.fontSize=fontSize;
        text.fontSharedMaterial=UiMenuFont.OutlineThin;
        text.enableAutoSizing=true;text.fontSizeMax=fontSize;text.fontSizeMin=18;
        text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        text.gameObject.AddComponent<LocalizedText>().SetKey(key);return text;
    }
}
