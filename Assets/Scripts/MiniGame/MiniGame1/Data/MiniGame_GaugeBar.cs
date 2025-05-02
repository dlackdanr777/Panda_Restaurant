using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame_GaugeBar : MonoBehaviour
{
    [Header("Components")]
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
        _gaugeBar.SetFillAmonut(score / (float)clearScore);
        _scoreText.SetText(score.ToString());
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

    public void SetStage(int stage)
    {
        _roundText.SetText(stage.ToString());
    }
}
