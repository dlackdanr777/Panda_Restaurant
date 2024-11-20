using BackEnd;
using Muks.BackEnd;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GetHashScene : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_InputField _inputField;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Init();
    }

    private async void Init()
    {
        await BackendManager.Instance.GuestLogin(10);

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnButtonClicked);
    }



    private void OnButtonClicked()
    {

        _inputField.text = Backend.Utils.GetGoogleHash();
    }
}
