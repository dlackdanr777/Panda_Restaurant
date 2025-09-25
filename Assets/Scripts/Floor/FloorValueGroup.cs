using TMPro;
using UnityEngine;

public class FloorValueGroup : MonoBehaviour
{
    [SerializeField] private TextMeshPro _valueText;
    [SerializeField] private GameObject _okImage;
    [SerializeField] private GameObject _lockImage;

    public void SetValue(int value, bool isUnlock)
    {
        _valueText.SetText(Utility.ConvertToMoney(value));
        _okImage.SetActive(isUnlock);
        _lockImage.SetActive(!isUnlock);
    }
}
