using TMPro;
using UnityEngine;

public class UITextAndText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text1;
    [SerializeField] private TextMeshProUGUI _text2;
    public Color TextColor1
    {
        get { return _text1.color;}
        set { _text1.color = value; }
    }

    public Color TextColor2
    {
        get { return _text2.color; }
        set { _text2.color = value; }
    }

    public void SetText1(string text)
    {
        _text1.text = text;
    }

    public void SetText2(string text)
    {
        _text2.text = text;
    }
}
