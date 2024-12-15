using UnityEngine;
using UnityEngine.UI;

public class UIOpenURLButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private string _url;

    void Awake()
    {
        _button.onClick.AddListener(OnButtonClicked);
    }


    private void OnButtonClicked()
    {
        Application.OpenURL(_url);
    }
}
