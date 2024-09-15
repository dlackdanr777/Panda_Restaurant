using TMPro;
using UnityEngine;

public class UIUpgradeAreaGroup : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _text1;
    [SerializeField] private TextMeshProUGUI _text2;

    public void SetData(int level, string text1, string text2)
    {
        _levelText.text = "Lv." + level;
        _text1.text = text1;
        _text2.text = text2;
    }

}
