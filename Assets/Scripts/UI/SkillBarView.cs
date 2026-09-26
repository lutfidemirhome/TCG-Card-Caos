using UnityEngine;

/// <summary>
/// Hides the legacy jump/hand buttons in existing HUDs. Space always uses the enhanced
/// jump; the five upgradeable card skills are presented by SkillPanelView.
/// </summary>
public class SkillBarView : MonoBehaviour
{
    public static bool HandSkillEnabled => false;

    void Awake()
    {
        HideSlot("Button_DoubleJump");
        HideSlot("Button_Hand");
        enabled = false;
    }

    void HideSlot(string objectName)
    {
        Transform existing = transform.Find(objectName);
        if (existing != null) existing.gameObject.SetActive(false);
    }
}
