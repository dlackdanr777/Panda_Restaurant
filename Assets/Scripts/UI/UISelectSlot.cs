using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISelectSlot : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private ButtonPressEffect _button;


    [Space]
    [Header("Images")]
    [SerializeField] private Image _usingImage;
    [SerializeField] private Image _notUsingImage;

    private BasicData _currentData;

    public void OnButtonClicked(Action<BasicData> action)
    {
        _button.RemoveAllListeners();
        _button.AddListener(() =>
        {
            if (_currentData == null)
                return;

            action?.Invoke(_currentData);
        });
    }

    public void SetData(BasicData data, bool isUsing, bool isHave)
    {
        _currentData = data;
        if (data == null)
        {
            _image.gameObject.SetActive(false);
            _nameText.gameObject.SetActive(false);
            _usingImage.gameObject.SetActive(false);
            _notUsingImage.gameObject.SetActive(false);
            _button.Interactable = false;
            return;
        }

        _image.gameObject.SetActive(true);
        _nameText.gameObject.SetActive(true);

        _image.sprite = data.ThumbnailSprite;
        _nameText.text = data.Name;

        if(!isHave)
        {
            _usingImage.gameObject.SetActive(false);
            _notUsingImage.gameObject.SetActive(false);
            _button.Interactable = false;
            return;
        }

        if(isUsing)
        {
            _usingImage.gameObject.SetActive(true);
            _notUsingImage.gameObject.SetActive(false);
            _button.Interactable = false;
        }
        else
        {
            _usingImage.gameObject.SetActive(false);
            _notUsingImage.gameObject.SetActive(true);
            _button.Interactable = true;
        }
    }
}
