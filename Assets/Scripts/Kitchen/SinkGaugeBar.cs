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

    public void SetGauge(int currentBowlCount, int maxBowlCount, float gauge)
    {
        _spriteFillAmount.SetFillAmount(gauge);
        _tmp.SetText(currentBowlCount + "/" + maxBowlCount);
        SetChangeRenderer(currentBowlCount, maxBowlCount);
    }

    private void SetChangeRenderer(int currentBowlCount, int maxBowlCount)
    {
        bool isFull = maxBowlCount <= currentBowlCount;
        if (isFull)
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
