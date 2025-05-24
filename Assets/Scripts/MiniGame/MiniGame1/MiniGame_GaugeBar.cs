using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame_GaugeBar : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform[] _gaugeFallowObjs;
    [SerializeField] private UITweenFillAmountImage _gaugeBar;
    [SerializeField] private Image _frameImage;
    [SerializeField] private Image _gaugeBarImage;
    [SerializeField] private Image _normalStageImage;
    [SerializeField] private Image _clearStageImage;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _roundText;

    [Space]
    [Header("Sprites")]
    [SerializeField] private Sprite _normalFrameSprite;
    [SerializeField] private Sprite _clearFrameSprite;

    [Space]
    [Header("Gauge Bar Color")]
    [SerializeField] private Color _normalColor;
    [SerializeField] private Color _clearColor;


    public void Init()
    {
        ResetScore();
        SetNormalSprite();
    }

    public void ResetScore()
    {
        _gaugeBar.SetFillAmountNoAnime(0);
        _scoreText.SetText("0");

        _gaugeBarImage.color = _normalColor;
        _frameImage.sprite = _normalFrameSprite;
        _normalStageImage.gameObject.SetActive(true);
        _clearStageImage.gameObject.SetActive(false);
    }

    public void SetScore(int score, int clearScore)
    {
        if (clearScore <= 0)
        {
            _gaugeBar.SetFillAmonut(1);
            _scoreText.SetText(score.ToString());
            UpdateFallowObjectPosition(1);
            return;
        }

        _gaugeBar.SetFillAmonut(score / (float)clearScore);
        _scoreText.SetText(score.ToString());
        UpdateFallowObjectPosition(score / (float)clearScore);
    }

    public void SetNormalSprite()
    {
        _gaugeBarImage.color = _normalColor;
        _frameImage.sprite = _normalFrameSprite;
        _normalStageImage.gameObject.SetActive(true);
        _clearStageImage.gameObject.SetActive(false);
    }

    public void SetClearSprite()
    {
        _gaugeBarImage.color = _clearColor;
        _frameImage.sprite = _clearFrameSprite;
        _clearStageImage.gameObject.SetActive(true);
        _normalStageImage.gameObject.SetActive(false);
    }

    public void SetStageText(string text)
    {
        _roundText.SetText(text);
    }
    
private void UpdateFallowObjectPosition(float fillAmount)
{
    if (_gaugeBarImage == null) return;

    // 게이지바 너비 계산
    float gaugeWidth = _gaugeBarImage.rectTransform.rect.width;

    // 수평 방향으로 변경: 왼쪽(0)에서 오른쪽으로 채워지는 방향
    // 게이지바의 왼쪽이 0, 오른쪽이 gaugeWidth라고 가정
    
    // 그림의 중앙이 게이지바 채워진 부분 끝에 위치하도록 함
    float xPosition = fillAmount * gaugeWidth - (gaugeWidth / 2f);

    // 위치 설정 (y 위치는 유지, x 위치만 변경)
    Vector2 targetPos = new Vector2(
        xPosition,
        _gaugeBarImage.rectTransform.anchoredPosition.y
    );
    for(int i = 0; i < _gaugeFallowObjs.Length; i++)
    {
        _gaugeFallowObjs[i].anchoredPosition = targetPos;
    }
}
}
