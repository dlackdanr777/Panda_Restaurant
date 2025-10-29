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
    [SerializeField] private GachaMachineParent[] _gachaMachines;
    [SerializeField] private GameObject _uiComponents;
    [SerializeField] private UIGachaSlotList _gachaItemList;
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _backgroundAudio;

    private GachaMachineParent _currentGachaMachine;


    public override void Init()
    {
        for(int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Init(this);
            _gachaMachines[i].Hide();
        }
        _gachaItemList.Init(_gachaMachines[0].ItemDataList);
        SetMachine(_gachaMachines[0]);
        _leftButton.onClick.AddListener(() => SetMachine(-1));
        _rightButton.onClick.AddListener(() => SetMachine(1));
        gameObject.SetActive(false);
    }


    public override void Show()
    {
        VisibleState = VisibleState.Appearing;
        SoundManager.Instance.PlayBackgroundAudio(_backgroundAudio, 0.5f);
        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = false;
        _animeUI.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        SetMachine(_gachaMachines[0]);
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
        gameObject.SetActive(false);
    }

    public void SetActiveUIComponents(bool isActive)
    {
        _uiComponents.SetActive(isActive);
    }

    private void SetMachine(int dir)
    {
        int currentIndex = Array.IndexOf(_gachaMachines, _currentGachaMachine);
        int nextIndex = currentIndex + dir;

        if (nextIndex < 0)
            nextIndex = _gachaMachines.Length - 1;
        else if (nextIndex >= _gachaMachines.Length)
            nextIndex = 0;

        SetMachine(_gachaMachines[nextIndex]);
    }

    public void SetMachine(GachaMachineParent gachaMachine)
    {
        for(int i = 0; i < _gachaMachines.Length; i++)
        {
            _gachaMachines[i].Hide();
        }
        _currentGachaMachine = gachaMachine;
        _currentGachaMachine.transform.SetAsLastSibling();
        _gachaItemList.UpdateData(gachaMachine.ItemDataList);
        gachaMachine.Show();
    }
}
