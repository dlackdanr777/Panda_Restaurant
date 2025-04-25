using System;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1_ButtonSlot : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _image;

    private MiniGame1ItemData _currentData;
    private Action<MiniGame1ItemData> _onButtonClicked;


    public void Init(Action<MiniGame1ItemData> onButtonClicked)
    {
        _onButtonClicked = onButtonClicked;
        _button.onClick.AddListener(OnButtonClicked);
    }

    public void SetData(MiniGame1ItemData data)
    {
        _currentData = data;
        if (data == null)
        {
            _image.sprite = null;
            return;
        }

        _image.sprite = data.Sprite;
    }


    public void StopButtonAction()
    {
        _button.interactable = false;
    }

    public void StartButtonAction()
    {
        _button.interactable = true;
    }

    private void OnButtonClicked()
    {
        _onButtonClicked?.Invoke(_currentData);
    }
}
