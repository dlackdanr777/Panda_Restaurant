using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UISatisfaction : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RectTransform _uiSatisfaction;
    [SerializeField] private Image _satisfactionImage;

    [Space]
    [Header("Sprites")]

    [SerializeField] private Sprite _satisfactorySprite;
    [SerializeField] private Sprite _averageSprite;
    [SerializeField] private Sprite _dissatisfactorySprite;
    [SerializeField] private Sprite _veryDissatisfactorySprite;


    private SatisfactionSystem _satisfactionSystem;
    private float _maxLength;
    private SatisfactionType _satisfactionType;


    public void Init(SatisfactionSystem satisfactionSystem)
    {
        _satisfactionSystem = satisfactionSystem;

        _satisfactionImage.rectTransform.anchorMax = new Vector2(0, 0.5f);
        _satisfactionImage.rectTransform.anchorMin = new Vector2(0, 0.5f);
        
        _maxLength = _uiSatisfaction.sizeDelta.x;

        _satisfactionType = _satisfactionSystem.GetSatisfactionType();
        SetSatisfactionImage();

        _satisfactionImage.rectTransform.anchoredPosition = SatisfactionImagePosCaculator();

        _satisfactionSystem.OnChangeSatisfactionHandler += OnChagneSatisfactionEvent;
    }


    private void OnChagneSatisfactionEvent()
    {
        _satisfactionImage.rectTransform.TweenStop();
        //_satisfactionImage.rectTransform.anchoredPosition = new Vector2(xPosition, currentPos.y);
        _satisfactionImage.rectTransform.TweenAnchoredPosition(SatisfactionImagePosCaculator(), 0.1f, Ease.Constant);
        TweenSatisfactionImage();
    }


    private Vector2 SatisfactionImagePosCaculator()
    {
        float currentSatisfaction = _satisfactionSystem.Satisfaction;

        // 만족도 범위를 UI 위치 범위로 매핑
        // MIN_SATISFACTION(-50)일 때 위치 = 0
        // MAX_SATISFACTION(50)일 때 위치 = _maxLength
        float normalizedPosition = Mathf.InverseLerp(
            ConstValue.MIN_SATISFACTION,
            ConstValue.MAX_SATISFACTION,
            currentSatisfaction
        );

        float xPosition = normalizedPosition * _maxLength;
        return new Vector2(xPosition, 0);
    }

    private void TweenSatisfactionImage()
    {
        SatisfactionType satisfactionType = _satisfactionSystem.GetSatisfactionType();
        if(satisfactionType != _satisfactionType)
        {
            _satisfactionType = satisfactionType;
            _satisfactionImage.transform.localScale = Vector3.one;
            _satisfactionImage.TweenScale(Vector3.one * 1.2f, 0.05f, Ease.OutBack).OnComplete(() =>
            {
                _satisfactionImage.TweenScale(Vector3.one, 0.08f, Ease.InBack);
            });

            SetSatisfactionImage();
        }
    }

    private void SetSatisfactionImage()
    {
        switch (_satisfactionType)
        {
            case SatisfactionType.Satisfactory:
                _satisfactionImage.sprite = _satisfactorySprite;
                break;
            case SatisfactionType.Average:
                _satisfactionImage.sprite = _averageSprite;
                break;
            case SatisfactionType.Dissatisfactory:
                _satisfactionImage.sprite = _dissatisfactorySprite;
                break;
            case SatisfactionType.VeryDissatisfactory:
                _satisfactionImage.sprite = _veryDissatisfactorySprite;
                break;
        }
    }
}
