using UnityEngine;
using UnityEngine.UI;

public class AddItemButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private string _itemId;

    private void Awake()
    {
        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        UserInfo.GiveGachaItem(_itemId);
    }
}
