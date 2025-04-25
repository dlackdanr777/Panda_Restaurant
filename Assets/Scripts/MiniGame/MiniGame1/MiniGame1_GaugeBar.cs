using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MiniGame1_GaugeBar : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UITweenFillAmountImage _gaugeBar;
    [SerializeField] private Image _gaugeBarImage;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _roundText;

    [Space]
    [Header("Gauge Bar Color")]
    [SerializeField] private Color _normalColor;
    [SerializeField] private Color _clearColor;



    public void Init()
    {
        _gaugeBarImage.color = _normalColor;
        _scoreText.SetText("0");
    }

    public void SetScore(int score, int clearScore)
    {
        _gaugeBar.SetFillAmonut(score / (float)clearScore);
        _scoreText.SetText(score.ToString());
        _gaugeBarImage.color = score < clearScore ? _normalColor : _clearColor;
    }
}
