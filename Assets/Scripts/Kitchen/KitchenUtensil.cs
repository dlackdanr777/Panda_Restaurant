using Muks.PathFinding.AStar;
using UnityEngine;

public enum KitchenUtensilBatchType
{
    Lower,
    Upper,
    Center
}

public class KitchenUtensil : MonoBehaviour
{

    [SerializeField] private KitchenUtensilType _type;
    public KitchenUtensilType Type => _type;

    [SerializeField] protected KitchenUtensilBatchType _batchType;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    [SerializeField] protected Sprite _defalutSprite;

    protected float _initialSpriteHeight; // 초기 스프라이트 높이 저장용

    protected ERestaurantFloorType _floorType;
    protected KitchenUtensilData _data;
    public virtual void Init(ERestaurantFloorType floor)
    {
        _floorType = floor;
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
            _data = null;
            if (_defalutSprite == null)
            {
                _spriteRenderer.gameObject.SetActive(false);
            }
            else
            {
                _spriteRenderer.sprite = _defalutSprite;
                SetRendererScale(null);
            }
            return;
        }
        _data = data;
        _spriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = data.Sprite;
        SetRendererScale(data);
    }

    protected virtual void SetRendererScale(KitchenUtensilData data)
    {
        float newSpriteHeight = _spriteRenderer.sprite.bounds.size.y;
        float sizeMul = 1;
        if (_initialSpriteHeight > 0 && newSpriteHeight > 0)
        {
            // 높이를 기준으로 스케일 비율 계산
            float heightRatio = _initialSpriteHeight / newSpriteHeight;

            // 새로운 스케일 설정 (SizeMul 적용)
            Vector3 newScale = _spriteRenderer.transform.localScale;
            newScale.x = heightRatio * sizeMul;
            newScale.y = heightRatio * sizeMul;
            _spriteRenderer.transform.localScale = newScale;
        }

        // 배치 타입에 따른 위치 설정
        float heightAdjustment = (_spriteRenderer.sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
        if (_batchType == KitchenUtensilBatchType.Lower)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, heightAdjustment - AStar.Instance.NodeSize * 0.5f, 0);
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
