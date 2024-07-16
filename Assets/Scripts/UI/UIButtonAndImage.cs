using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIButtonAndImage : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _image;

    public void AddListener(UnityAction action) 
    {
        _button.onClick.AddListener(action);
    }

    public void RemoveAllListeners()
    {
        _button.onClick.RemoveAllListeners();
    }

    public void Interactable(bool value)
    {
        _button.interactable = value;        
    }

    public void SetImage(Sprite sprite)
    {
        if(sprite == null)
        {
            _image.gameObject.SetActive(false);
            return;
        }

        _image.gameObject.SetActive(true);
        _image.sprite = sprite;
    }
}
