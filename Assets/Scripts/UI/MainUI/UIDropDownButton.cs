using UnityEngine;
using UnityEngine.UI;

public class UIDropDownButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _dropDownMenu;

    private void Awake()
    {
        _button.onClick.AddListener(OnButtonClicked);
        _dropDownMenu.gameObject.SetActive(false);
    }

    private void OnButtonClicked()
    {
        _dropDownMenu.SetActive(!_dropDownMenu.activeSelf);
    }
}
