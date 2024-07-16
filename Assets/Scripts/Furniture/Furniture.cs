using UnityEngine;

public class Furniture : MonoBehaviour
{
    protected enum FurnitureBatchType
    {
        Lower,
        Upper,
        Center
    }
    [SerializeField] protected FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] protected FurnitureBatchType _batchType;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    [SerializeField] protected Sprite _defalutSprite;
    
    public virtual void SetFurnitureData(FurnitureData data)
    {
        if(data == null)
        {
            if(_defalutSprite == null)
                _spriteRenderer.gameObject.SetActive(false);

            else
                _spriteRenderer.sprite = _defalutSprite;

            return;
        }

        _spriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = data.Sprite;


        if(_batchType == FurnitureBatchType.Lower)
        {
            float heightMul = (data.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
            _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);
        }
        else if(_batchType == FurnitureBatchType.Upper)
        {
            float heightMul = (data.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
            _spriteRenderer.transform.localPosition = new Vector3(0, -heightMul, 0);
        }
        else if(_batchType == FurnitureBatchType.Center)
        {
            _spriteRenderer.transform.localPosition = Vector3.zero;
        }

    }
}
