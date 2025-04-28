using System;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1_ButtonSlot : MonoBehaviour
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private Button _button;
    [SerializeField] private Image _itemImage;
    [SerializeField] private Image _backImage;

    [Header("Option")]
    [SerializeField] private float _flipSpeed = 0.3f;
    [SerializeField] private float _flipScale = 1.2f;

    private MiniGame1ItemData _currentData;
    public MiniGame1ItemData CurrentData => _currentData;
    private Action<MiniGame1ItemData> _onButtonClicked;
    private bool _isFrontFlipped = false;
    private bool _isAnimatingToFront = false; // 앞면으로 애니메이션 중인지 여부
    private bool _passedMidPoint = false;    // 90도 지점을 지났는지 여부

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
            _itemImage.sprite = null;
            return;
        }

        _itemImage.sprite = data.Sprite;
    }

    public void FlipBack()
    {
        _rectTransform.TweenStop();
        _isFrontFlipped = false;
        _itemImage.gameObject.SetActive(false);
        _backImage.gameObject.SetActive(true);
        _rectTransform.localRotation = Quaternion.Euler(0, 180, 0);
    }

    public void FlipBackAnimation()
    {
        _isAnimatingToFront = false;
        _passedMidPoint = false;
        FlipForward();
        _rectTransform.TweenRotate(new Vector3(0, 180, 0), _flipSpeed, Ease.OutBack).OnUpdate(CheckFlipImage);
    }

    public void FlipForward()
    {
        _rectTransform.TweenStop();
        _isFrontFlipped = true;
        _itemImage.gameObject.SetActive(true);
        _backImage.gameObject.SetActive(false);
        _rectTransform.localRotation = Quaternion.Euler(0, 0, 0);
    }

    public void FlipForwardAnimation()
    {
        _isAnimatingToFront = true;
        _passedMidPoint = false;      
        FlipBack();        
        _rectTransform.TweenRotate(new Vector3(0, 0, 0), _flipSpeed, Ease.InOutBack).OnUpdate(CheckFlipImage);
    }

    private void CheckFlipImage()
    {
        float currentYRotation = _rectTransform.localRotation.eulerAngles.y;
        float normalizedAngle;
        
        if (currentYRotation > 180)
            normalizedAngle = (360 - currentYRotation) / 90f;
        else
            normalizedAngle = currentYRotation / 90f;
        
        float scaleMultiplier = 0;
        if (normalizedAngle <= 1.0f)
            scaleMultiplier = normalizedAngle; // 0~90도 또는 270~360도
        else if (normalizedAngle <= 2.0f)
            scaleMultiplier = 2.0f - normalizedAngle; // 90~180도 또는 180~270도
        
        float targetScale = Mathf.Lerp(1.0f, _flipScale, scaleMultiplier);     
        _rectTransform.localScale = new Vector3(targetScale, targetScale, targetScale);
        
        // 기존 이미지 전환 로직
        if (_isAnimatingToFront)
        {
            bool isNearMidpoint = (currentYRotation < 90 || currentYRotation > 270);
            
            if (isNearMidpoint && !_passedMidPoint)
            {
                _itemImage.gameObject.SetActive(true);
                _backImage.gameObject.SetActive(false);
                _isFrontFlipped = true;
                _passedMidPoint = true;
            }
        }
        else
        {
            bool isNearMidpoint = (currentYRotation > 90 && currentYRotation < 270);
            
            if (isNearMidpoint && !_passedMidPoint)
            {
                _itemImage.gameObject.SetActive(false);
                _backImage.gameObject.SetActive(true);
                _isFrontFlipped = false;
                _passedMidPoint = true;
            }
        }
    }

    public void ScaleAnimation(float startScale, float targetScale, float duration)
    {
        _rectTransform.TweenStop();
        _rectTransform.localScale = new Vector3(startScale, startScale, startScale);
        _rectTransform.TweenScale(new Vector3(targetScale, targetScale, targetScale), duration, Ease.InOutBack);
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
