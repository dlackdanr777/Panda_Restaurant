using UnityEngine;
using Muks.RecyclableScrollView;
using TMPro;

public class TestScrollSlot : RecyclableScrollSlot<TestData>
{
    [SerializeField] private TextMeshProUGUI _strText;
    [SerializeField] private TextMeshProUGUI _intText;

    public override void UpdateSlot(TestData data)
    {
        _strText.text = data.Str;
        _intText.text = data.Int.ToString();
    }
}
