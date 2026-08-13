using System;
using Muks.DataBind;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIFloorButtonGroup : MonoBehaviour
{
    [Header("Button Group Images")]
    [SerializeField] private UnityEngine.UI.Image[] _buttonGroupImages; // 스프라이트가 변경될 이미지들
    [SerializeField] private Sprite _floor1Sprite; // 1층용 스프라이트
    [SerializeField] private Sprite _vipRoomSprite; // VIP룸용 스프라이트
    
    [Space]
    [Header("Floor Text")]
    [SerializeField] private TextMeshProUGUI _floorText; // 층 표시 텍스트
    
    [Space]
    [Header("Toggle Button")]
    [SerializeField] private Button _toggleButton;

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
        UpdateButtonGroupVisibility();
    }


    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }


    public void SetFloorText(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
        UpdateButtonGroupVisibility();
    }
    
    private void OnToggleButtonClicked()
    {
        DataBind.GetUnityActionValue("ButtonClickSound")?.Invoke();
        
        if (!_isVIPUnlocked)
            return;
        
        // 1층 ↔ VIP룸 토글
        if (_currentFloor == ERestaurantFloorType.Floor1)
        {
            _floor2ButtonClicked?.Invoke();
        }
        else
        {
            _floor1ButtonClicked?.Invoke();
        }
    }
    
    private void UpdateButtonGroupVisibility()
    {
        Sprite targetSprite = null;
        string floorText = "";
        
        if (!_isVIPUnlocked)
        {
            // VIP룸 미해금: 1층 스프라이트 표시
            targetSprite = _floor1Sprite;
            floorText = "1층";
        }
        else
        {
            // VIP룸 해금: 현재 층에 따라 스프라이트 전환
            if (_currentFloor == ERestaurantFloorType.Floor1)
            {
                targetSprite = _floor1Sprite;
                floorText = "1층";
            }
            else
            {
                targetSprite = _vipRoomSprite;
                floorText = "VIP룸";
            }
        }
        
        // 모든 이미지에 스프라이트 적용
        if (_buttonGroupImages != null && targetSprite != null)
        {
            foreach (var image in _buttonGroupImages)
            {
                if (image != null)
                {
                    image.sprite = targetSprite;
                }
            }
        }
        
        // 텍스트 업데이트
        if (_floorText != null)
        {
            _floorText.text = floorText;
        }
    }

    private void OnChangeUnlockFloorEvent()
    {
        ERestaurantFloorType unlockedFloor = UserInfo.GetUnlockFloor(UserInfo.CurrentStage);
        
        // VIP룸(2층 이상) 해금 여부 확인
        _isVIPUnlocked = unlockedFloor == ERestaurantFloorType.Floor2 || 
                         unlockedFloor == ERestaurantFloorType.Floor3;
        
        UpdateButtonGroupVisibility();
    }


    private void OnEnable()
    {
        OnChangeUnlockFloorEvent();
    }
}
