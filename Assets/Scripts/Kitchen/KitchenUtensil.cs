using UnityEngine;

public class KitchenUtensil : MonoBehaviour
{
    private enum KitchenUtensilBatchType
    {
        Lower,
        Upper,
        Center
    }
    [SerializeField] private KitchenUtensilType _type;
    public KitchenUtensilType Type => _type;

    [SerializeField] private KitchenUtensilBatchType _batchType;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _defalutSprite;

    private float _initialSpriteHeight; // 초기 스프라이트 높이 저장용

    public void Init()
    {
        if (_spriteRenderer.sprite != null)
        {
            _initialSpriteHeight = _spriteRenderer.sprite.bounds.size.y;
        }
        else
        {
            _initialSpriteHeight = 1f; // 기본값 설정
        }
    }

    public void SetData(KitchenUtensilData data)
    {
        if (data == null)
        {
            if (_defalutSprite == null)
            {
                _spriteRenderer.gameObject.SetActive(false);
            }
            else
            {
                _spriteRenderer.sprite = _defalutSprite;
            }
            return;
        }

        _spriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = data.Sprite;

        // 현재 스프라이트 높이 계산
        float newSpriteHeight = _spriteRenderer.sprite.bounds.size.y;

        if (_initialSpriteHeight > 0 && newSpriteHeight > 0)
        {
            // 높이를 기준으로 스케일 비율 계산
            float heightRatio = _initialSpriteHeight / newSpriteHeight;

            // 새로운 스케일 설정 (SizeMul 적용)
            Vector3 newScale = _spriteRenderer.transform.localScale;
            newScale.x = heightRatio * data.SizeMul;
            newScale.y = heightRatio * data.SizeMul;
            _spriteRenderer.transform.localScale = newScale;
        }

        // 배치 타입에 따른 위치 설정
        float heightAdjustment = (_spriteRenderer.sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
        if (_batchType == KitchenUtensilBatchType.Lower)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, heightAdjustment, 0);
        }
        else if (_batchType == KitchenUtensilBatchType.Upper)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, -heightAdjustment, 0);
        }
        else if (_batchType == KitchenUtensilBatchType.Center)
        {
            _spriteRenderer.transform.localPosition = Vector3.zero;
        }
    }
}
