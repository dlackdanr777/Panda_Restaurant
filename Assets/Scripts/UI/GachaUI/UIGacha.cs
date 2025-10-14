using UnityEngine;
using Muks.MobileUI;
using Muks.Tween;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

public class UIGacha : MobileUIView
{
    public event Action<int> GachaStepHandler;

    [Header("Components")]
    [SerializeField] private MainScene _mainScene;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UIItemGacha _itemGacha;


    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _backgroundAudio;


    
    public override void Init()
    {
        _itemGacha.Init();
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_backgroundAudio, 0.5f);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        _itemGacha.Show();
        TweenData tween = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            _canvasGroup.blocksRaycasts = true;
        });
    }


    public override void Hide()
    {
        VisibleState = VisibleState.Disappeared;
        _mainScene.PlayMainMusic();
        _itemGacha.Hide();
        gameObject.SetActive(false);
    }
}
