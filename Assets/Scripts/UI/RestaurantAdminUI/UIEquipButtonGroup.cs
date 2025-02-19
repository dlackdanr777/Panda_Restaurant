using Muks.Tween;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIEquipButtonGroup : MonoBehaviour
{
    [SerializeField] private UIButtonAndText _floor1Button;
    [SerializeField] private UIButtonAndText _floor2Button;
    [SerializeField] private UIButtonAndText _floor3Button;
    [SerializeField] private UIButtonAndText _cancelButton;
    [SerializeField] private GridLayoutGroup _gridLayoutGroup;
    [SerializeField] private CanvasGroup _canvasGroup;

    public bool ActiveSelf => gameObject.activeSelf;
    private Vector2 _tmpSapcing;
    private Vector2 _startSapcing;
    

    public void Init(UnityAction floor1ButtonClicked, UnityAction floor2ButtonClicked, UnityAction floor3ButtonClicked, UnityAction cancelButtonClicked)
    {
        _floor1Button.RemoveAllListeners();
        _floor2Button.RemoveAllListeners();
        _floor3Button.RemoveAllListeners();
        _cancelButton.RemoveAllListeners();

        _floor1Button.AddListener(floor1ButtonClicked);
        _floor2Button.AddListener(floor2ButtonClicked);
        _floor3Button.AddListener(floor3ButtonClicked);
        _cancelButton.AddListener(cancelButtonClicked);

        _tmpSapcing = _gridLayoutGroup.spacing;
        _startSapcing = new Vector2(0, -_gridLayoutGroup.cellSize.y);
        _gridLayoutGroup.spacing = _startSapcing;

        UserInfo.OnChangeFloorHandler += OnFloorChangeEvent;
        gameObject.SetActive(false);
    }


    public void Show(Action onCompleted = null)
    {
        gameObject.SetActive(true);
        _gridLayoutGroup.TweenStop();
        OnFloorChangeEvent();
        _canvasGroup.interactable = false;
        _gridLayoutGroup.spacing = _startSapcing;
        _gridLayoutGroup.TweenSpacing(_tmpSapcing, 0.25f, Ease.OutBack).OnComplete(() =>
        {
            _canvasGroup.interactable = true;
            onCompleted?.Invoke();
        });
    }


    public void Hide(Action onCompleted = null)
    {
        _gridLayoutGroup.TweenStop();
        gameObject.SetActive(true);
        _canvasGroup.interactable = false;
        _gridLayoutGroup.spacing = _tmpSapcing;
        _gridLayoutGroup.TweenSpacing(_startSapcing, 0.25f, Ease.InBack).OnComplete(() =>
        {
            onCompleted?.Invoke();
            gameObject.SetActive(false);
        });
    }

    public void ShowNoAnime()
    {
        _gridLayoutGroup.TweenStop();
        _canvasGroup.interactable = true;
        _gridLayoutGroup.spacing = _tmpSapcing;
    }

    public void HideNoAnime()
    {
        gameObject.SetActive(false);
    }


    private void OnEnable()
    {
        _gridLayoutGroup.TweenStop();
        _gridLayoutGroup.spacing = _startSapcing;
        _canvasGroup.interactable = false;
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeFloorHandler -= OnFloorChangeEvent;
    }


    private void OnFloorChangeEvent()
    {
        if (!gameObject.activeInHierarchy)
            return;

        ERestaurantFloorType currentFloorType = UserInfo.CurrentFloor;
        if (currentFloorType == ERestaurantFloorType.Floor3)
        {
            _floor3Button.gameObject.SetActive(true);
            _floor2Button.gameObject.SetActive(true);
            _floor1Button.gameObject.SetActive(true);
        }
        else if (currentFloorType == ERestaurantFloorType.Floor2)
        {
            _floor3Button.gameObject.SetActive(false);
            _floor2Button.gameObject.SetActive(true);
            _floor1Button.gameObject.SetActive(true);
        }
        else if (currentFloorType == ERestaurantFloorType.Floor1)
        {
            _floor3Button.gameObject.SetActive(false);
            _floor2Button.gameObject.SetActive(false);
            _floor1Button.gameObject.SetActive(true);
        }
        else
        {
            _floor3Button.gameObject.SetActive(false);
            _floor2Button.gameObject.SetActive(false);
            _floor1Button.gameObject.SetActive(false);
        }
    }
}
