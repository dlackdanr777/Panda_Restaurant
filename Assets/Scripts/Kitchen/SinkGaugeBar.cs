using TMPro;
using UnityEngine;

public class SinkGaugeBar : MonoBehaviour
{
    [SerializeField] private SpriteFillAmount _spriteFillAmount;
    [SerializeField] private TextMeshPro _tmp;
    [SerializeField] private SpriteRenderer _emptyRenderer;
    [SerializeField] private SpriteRenderer _fullRenderer;

    [Space]
    [Header("Option")]
    [SerializeField] private Color _emptyColor;
    [SerializeField] private Color _fullColor;


    public void Init()
    {
    }

    public void SetGauge(int currentBowlCount, int maxBowlCount)
    {
        float gauge = Mathf.Clamp((float)currentBowlCount / maxBowlCount, 0, 1);
        _spriteFillAmount.TweenFillAmount(gauge, 0.05f);
        _tmp.SetText(currentBowlCount + "/" + maxBowlCount);
        SetChangeRenderer(gauge);
    }

    private void SetChangeRenderer(float gauge)
    {
        if (1 <= gauge)
        {
            _emptyRenderer.gameObject.SetActive(false);
            _fullRenderer.gameObject.SetActive(true);
            _spriteFillAmount.SetColor(_fullColor);
        }
        else
        {
            _emptyRenderer.gameObject.SetActive(true);
            _fullRenderer.gameObject.SetActive(false);
            _spriteFillAmount.SetColor(_emptyColor);
        }
    }
}
