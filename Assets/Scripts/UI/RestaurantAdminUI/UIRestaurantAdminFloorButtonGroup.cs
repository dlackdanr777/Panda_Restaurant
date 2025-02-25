using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIRestaurantAdminFloorButtonGroup : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button _openButton;
    [SerializeField] private Button[] _closeButtons;
    [SerializeField] private UIButtonAndPressEffect _floor1Button;
    [SerializeField] private UIButtonAndPressEffect _floor2Button;
    [SerializeField] private UIButtonAndPressEffect _floor3Button;

    [Space]
    [Header("Components")]
    [SerializeField] private Image _closeImage;
    [SerializeField] private Image _openImage;
    [SerializeField] private TextMeshProUGUI _floorText;


    public void Init(UnityAction floor1ButtonClicked, UnityAction floor2ButtonClicked, UnityAction floor3ButtonClicked)
    {
        _floor1Button.AddListener(floor1ButtonClicked);
        _floor2Button.AddListener(floor2ButtonClicked);
        _floor3Button.AddListener(floor3ButtonClicked);

        _floor1Button.AddListener(Hide);
        _floor2Button.AddListener(Hide);
        _floor3Button.AddListener(Hide);

        _openButton.onClick.AddListener(Show);
        for(int i = 0, cnt = _closeButtons.Length; i < cnt; ++i)
        {
            _closeButtons[i].onClick.AddListener(Hide);
        }
    }


    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }


    public void SetFloorText(ERestaurantFloorType floor)
    {
        string text = floor switch
        {
            ERestaurantFloorType.Floor1 => "1Ãþ",
            ERestaurantFloorType.Floor2 => "2Ãþ",
            ERestaurantFloorType.Floor3 => "3Ãþ",
            _ => "Error"
        };

        _floorText.SetText(text);
    }

    public void Show()
    {
        OnChangeUnlockFloorEvent();
        _closeImage.gameObject.SetActive(false);
        _openImage.gameObject.SetActive(true);
    }


    public void Hide()
    {
        _closeImage.gameObject.SetActive(true);
        _openImage.gameObject.SetActive(false);
    }


    private void OnChangeUnlockFloorEvent()
    {
        if (!_openImage.gameObject.activeInHierarchy)
            return;

        ERestaurantFloorType currentFloorType = UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        if (currentFloorType == ERestaurantFloorType.Floor3)
        {
            _floor3Button.interactable = true;
            _floor2Button.interactable = true;
            _floor1Button.interactable = true;
        }
        else if (currentFloorType == ERestaurantFloorType.Floor2)
        {
            _floor3Button.interactable = false;
            _floor2Button.interactable = true;
            _floor1Button.interactable = true;
        }
        else if (currentFloorType == ERestaurantFloorType.Floor1)
        {
            _floor3Button.interactable = false;
            _floor2Button.interactable = false;
            _floor1Button.interactable = true;
        }
        else
        {
            _floor3Button.interactable = false;
            _floor2Button.interactable = false;
            _floor1Button.interactable = false;
        }
    }


    private void OnEnable()
    {
        OnChangeUnlockFloorEvent();
    }
}
