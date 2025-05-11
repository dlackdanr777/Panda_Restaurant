using System;
using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMiniGameResultGroup : MonoBehaviour
{
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _text;

    [Header("UI Animation")]
    [SerializeField] private float _showDuration = 0.3f;
    [SerializeField] private Ease _showEase = Ease.OutBack;
    [SerializeField] private float _showScale = 0.3f;

    [Header("Audio")]
    [SerializeField] private AudioClip _audioClip;



    public void SetResult(Sprite sprite, string text)
    {
        _image.sprite = sprite;
        SoundManager.Instance.PlayEffectAudio(EffectType.UI, _audioClip);
        _text.SetText(text);
        Show();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        _animeUI.transform.localScale = Vector3.one * _showScale;
        _animeUI.TweenScale(Vector3.one, _showDuration, _showEase);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
