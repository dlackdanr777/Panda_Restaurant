using TMPro;
using UnityEngine;

public class UIManagementSetCount : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _setNameText;
    [SerializeField] private TextMeshProUGUI _setCountValueText;


    public void SetData(string setName, int count, int totalCount)
    {
        _setNameText.text = setName;
        _setCountValueText.text = count + "/" + totalCount;
    }

}
