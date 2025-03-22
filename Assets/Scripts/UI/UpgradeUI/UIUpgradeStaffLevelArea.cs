using TMPro;
using UnityEngine;

public class UIUpgradeStaffLevelArea : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text1;
    [SerializeField] private TextMeshProUGUI _text2;
    public Color TextColor1
    {
        get { return _text1.color; }
        set { _text1.color = value; }
    }

    public Color TextColor2
    {
        get { return _text2.color; }
        set { _text2.color = value; }
    }

    public void SetLevelText(int level)
    {
        _text1.text = "Lv."+ level;
    }

    public void SetEffectText(string text)
    {
        _text2.text = text.Replace("(", "\n(");
    }
}
