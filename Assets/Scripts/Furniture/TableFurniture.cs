using UnityEngine;

public class TableFurniture : Furniture
{
    [SerializeField] private SpriteRenderer _leftChairSpriteRenderer;
    [SerializeField] private SpriteRenderer _rightChairSpriteRenderer;

    public override void SetFurnitureData(FurnitureData data)
    {
        if (data == null)
        {
            if (_defalutSprite == null)
                _spriteRenderer.gameObject.SetActive(false);

            else
                _spriteRenderer.sprite = _defalutSprite;

            _leftChairSpriteRenderer.gameObject.SetActive(false);
            _rightChairSpriteRenderer.gameObject.SetActive(false);
            return;
        }

        if(!(data is TableFurnitureData))
        {
            DebugLog.LogError("TableFurniture 컴포넌트에선 TableFurnitureData만 사용할 수 있습니다.");
            return;
        }

        TableFurnitureData tableData = (TableFurnitureData)data;

        _spriteRenderer.gameObject.SetActive(true);
        _leftChairSpriteRenderer.gameObject.SetActive(true);
        _rightChairSpriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = data.Sprite;
        _leftChairSpriteRenderer.sprite = tableData.ChairSprite;
        _rightChairSpriteRenderer.sprite = tableData.ChairSprite;
        _rightChairSpriteRenderer.flipX = true;

        float heightMul = (data.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
        float chairHeightMul = (tableData.ChairSprite.bounds.size.y * 0.5f) * _leftChairSpriteRenderer.transform.lossyScale.y;

        if (_batchType == FurnitureBatchType.Lower)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);
            _leftChairSpriteRenderer.transform.localPosition = new Vector3(_leftChairSpriteRenderer.transform.localPosition.x, chairHeightMul, 0);
            _rightChairSpriteRenderer.transform.localPosition = new Vector3(_rightChairSpriteRenderer.transform.localPosition.x, chairHeightMul, 0);
        }
        else if (_batchType == FurnitureBatchType.Upper)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, -heightMul, 0);
            _leftChairSpriteRenderer.transform.localPosition = new Vector3(_leftChairSpriteRenderer.transform.localPosition.x, -chairHeightMul, 0);
            _rightChairSpriteRenderer.transform.localPosition = new Vector3(_rightChairSpriteRenderer.transform.localPosition.x, -chairHeightMul, 0);
        }
        else if (_batchType == FurnitureBatchType.Center)
        {
            _spriteRenderer.transform.localPosition = Vector3.zero;
            _leftChairSpriteRenderer.transform.localPosition = new Vector3(_leftChairSpriteRenderer.transform.localPosition.x, 0, 0);
            _rightChairSpriteRenderer.transform.localPosition = new Vector3(_rightChairSpriteRenderer.transform.position.x, 0, 0);
        }

    }
}
