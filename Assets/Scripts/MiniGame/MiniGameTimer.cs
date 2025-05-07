
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class MiniGameTimer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UITweenFillAmountImage _timerBar;
    [SerializeField] private Image _gaugeBarImage;
    [SerializeField] private Image _footImage;
    [SerializeField] private RectTransform _gaugeBarRect;
    [SerializeField] private RectTransform _footRect;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _normalGaugeSprite;
    [SerializeField] private Sprite _clearGaugeSprite;
    [SerializeField] private Sprite _normalFootSprite;
    [SerializeField] private Sprite _clearFootSprite;


    public void ResetTimer()
    {
        _timerBar.SetFillAmountNoAnime(1);
        UpdateFootImagePosition();
    }

    public void SetNormalSprite()
    {
        _gaugeBarImage.sprite = _normalGaugeSprite;
        _footImage.sprite = _normalFootSprite;
    }

    public void SetClearSprite()
    {
        DebugLog.Log("클리어");
        _gaugeBarImage.sprite = _clearGaugeSprite;
        _footImage.sprite = _clearFootSprite;
    }


    public void StartAnimation()
    {
        float loopTime = 1f;
        _footRect.TweenStop();
        _footRect.transform.rotation = Quaternion.Euler(0, 0, -3);
        _footRect.TweenRotate(new Vector3(0, 0, 3), loopTime, Ease.Smoothstep).Loop(LoopType.Yoyo);
    }

    public void StopAnimation()
    {
        _footRect.TweenStop();
        _footRect.transform.rotation = Quaternion.Euler(0, 0, 0);
    }


    public void SetTimer(float time, float maxTime)
    {
        float value = Mathf.Clamp01(time / maxTime);
        _timerBar.SetFillAmountNoAnime(value);
        UpdateFootImagePosition();
    }

    public void SetTimer(float value)
    {
        value = Mathf.Clamp01(value);
        _timerBar.SetFillAmountNoAnime(value);
        DebugLog.Log($"SetTimer: {value}");
        UpdateFootImagePosition();
    }


    private void UpdateFootImagePosition()
    {
        if (_footRect == null || _gaugeBarRect == null || _timerBar == null) return;

        // fillAmount 값 가져오기 (0~1 사이)
        float fillAmount = _timerBar.FillAmount;

        // 게이지바 높이 계산
        float gaugeHeight = _gaugeBarRect.rect.height;

        // 발 이미지 위치 계산: 게이지바 하단(0)에서 채워진 높이까지

        // footImage의 로컬 위치 계산
        // 게이지바의 로컬 좌표계에서 fillAmount에 따른 높이 계산
        // 게이지바 아래쪽이 0, 위쪽이 gaugeHeight라고 가정

        // 그림의 중앙이 게이지바 채워진 부분 상단에 위치하도록 함
        float footHeight = _footRect.rect.height;
        float yPosition = fillAmount * gaugeHeight - (gaugeHeight / 2f);

        // 위치 설정 (x 위치는 유지, y 위치만 변경)
        _footRect.anchoredPosition = new Vector2(
            _footRect.anchoredPosition.x,
            yPosition
        );
    }
}
