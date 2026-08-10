using System;
using Muks.DataBind;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIFloorButtonGroup : MonoBehaviour
{
    [Header("Toggle Button")]
    [SerializeField] private Button _toggleButton;
    [SerializeField] private Image _buttonImage;
    
    [Space]
    [Header("Floor Images")]
    [SerializeField] private Sprite _floor1Sprite;
    [SerializeField] private Sprite _vipRoomSprite;
    
    [Space]
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _floorText;

    private UnityAction _floor1ButtonClicked;
    private UnityAction _floor2ButtonClicked;
    private ERestaurantFloorType _currentFloor = ERestaurantFloorType.Floor1;
    private bool _isVIPUnlocked = false;


    public void Init(UnityAction floor1ButtonClicked, UnityAction floor2ButtonClicked, UnityAction floor3ButtonClicked)
    {
        _floor1ButtonClicked = floor1ButtonClicked;
        _floor2ButtonClicked = floor2ButtonClicked;
        
        _toggleButton.onClick.AddListener(OnToggleButtonClicked);

        UserInfo.OnChangeFloorHandler += OnChangeUnlockFloorEvent;
        OnChangeUnlockFloorEvent();
        UpdateButtonVisual();
    }


    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }


    public void SetFloorText(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
        UpdateButtonVisual();
    }
    
    private void OnToggleButtonClicked()
    {
        DataBind.GetUnityActionValue("ButtonClickSound")?.Invoke();
        
        if (!_isVIPUnlocked)
            return;
        
        // 1? ? VIP? ??
        if (_currentFloor == ERestaurantFloorType.Floor1)
        {
            _floor2ButtonClicked?.Invoke();
        }
        else
        {
            _floor1ButtonClicked?.Invoke();
        }
    }
    
    private void UpdateButtonVisual()
    {
        if (!_isVIPUnlocked)
        {
            // VIP? ???: 1? ???? ??
            if (_buttonImage != null && _floor1Sprite != null)
                _buttonImage.sprite = _floor1Sprite;
            
            if (_floorText != null)
                _floorText.SetText("1?");
        }
        else
        {
            // VIP? ??: ?? ?? ?? ??? ??
            if (_buttonImage != null)
            {
                _buttonImage.sprite = _currentFloor == ERestaurantFloorType.Floor1 
                    ? _floor1Sprite 
                    : _vipRoomSprite;
            }
            
            if (_floorText != null)
            {
                string text = _currentFloor == ERestaurantFloorType.Floor1 ? "1?" : "VIP?";
                _floorText.SetText(text);
            }
        }
    }

    private void OnChangeUnlockFloorEvent()
    {
        ERestaurantFloorType unlockedFloor = UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        
        // VIP?(2? ??) ?? ?? ??
        _isVIPUnlocked = unlockedFloor == ERestaurantFloorType.Floor2 || 
                         unlockedFloor == ERestaurantFloorType.Floor3;
        
        UpdateButtonVisual();
    }


    private void OnEnable()
    {
        OnChangeUnlockFloorEvent();
    }
}
