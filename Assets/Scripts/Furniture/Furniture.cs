using UnityEngine;

public class Furniture : MonoBehaviour
{
    protected enum FurnitureBatchTypeY
    {
        Lower,
        Upper,
        Center,
        None,
    }

    protected enum FurnitureBatchTypeX
    {
        Left,
        Right,
        Center,
        None,
    }

    [SerializeField] protected FurnitureType _type;
    public FurnitureType Type => _type;

    [SerializeField] protected bool _thumbnailSpriteEnabled = false;
    [SerializeField] protected FurnitureBatchTypeY _batchType;
    [SerializeField] protected FurnitureBatchTypeX _batchTypeX;
    [SerializeField] protected SpriteRenderer _spriteRenderer;
    [SerializeField] protected Sprite _defalutSprite;

    protected TableManager _tableManager;
    protected Vector3 _tmpScale;
    protected ERestaurantFloorType _floor;

    public virtual void Init(TableManager tableManager, ERestaurantFloorType floor)
    {
        _tableManager = tableManager;
        _tmpScale = _spriteRenderer.transform.localScale;
        _floor = floor;
    }


    public virtual void SetFurnitureData(FurnitureData data)
    {
        if(data == null)
        {
            if(_defalutSprite == null)
                _spriteRenderer.gameObject.SetActive(false);

            else
            {
                _spriteRenderer.sprite = _defalutSprite;
                SetRendererScale();
            }

            return;
        }

        _spriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = _thumbnailSpriteEnabled ? data.ThumbnailSprite : data.Sprite;
        SetRendererScale();
    }

    private void SetRendererScale()
    {
        float y = 0;
        float x = 0;

        if (_batchType == FurnitureBatchTypeY.Lower)
            y = _spriteRenderer.sprite.bounds.size.y * 0.5f * _spriteRenderer.transform.lossyScale.y;

        else if (_batchType == FurnitureBatchTypeY.Upper)
            y = -(_spriteRenderer.sprite.bounds.size.y * 0.5f * _spriteRenderer.transform.lossyScale.y);

        else if (_batchType == FurnitureBatchTypeY.Center)
            y = 0;

        else if (_batchType == FurnitureBatchTypeY.None)
            y = _spriteRenderer.transform.localPosition.y;


        if (_batchTypeX == FurnitureBatchTypeX.Left)
            x = _spriteRenderer.sprite.bounds.size.x * 0.5f * _spriteRenderer.transform.lossyScale.x;

        else if (_batchTypeX == FurnitureBatchTypeX.Right)
            x = -(_spriteRenderer.sprite.bounds.size.x * 0.5f * _spriteRenderer.transform.lossyScale.x);

        else if (_batchTypeX == FurnitureBatchTypeX.Center)
            x = 0;

        else if (_batchType == FurnitureBatchTypeY.None)
            x = _spriteRenderer.transform.localPosition.x;

        _spriteRenderer.transform.localPosition = new Vector3(x, y, 0);
    }
}
