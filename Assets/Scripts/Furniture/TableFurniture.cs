using UnityEngine;

public class TableFurniture : Furniture
{
    [Space]
    [Header("Table Furniture")]
    [SerializeField] private Sprite _chairDefalutSprite;
    [SerializeField] private SpriteRenderer _leftChairSpriteRenderer;
    [SerializeField] private SpriteRenderer _rightChairSpriteRenderer;
    [SerializeField] private PointerDownSpriteRenderer _pointerDownSpriteRenderer;


    private TableFurnitureData _tableFurnitureData;
    private TableData _tableData;


    public override void Init(TableManager manager)
    {
        base.Init(manager);
        manager.OnTableUpdateHandler += OnTableUpdateEvent;
        _pointerDownSpriteRenderer.AddEvent(OnTouchEvent);
    }


    public void SetTableData(TableData data)
    {
        _tableData = data;
        OnTableUpdateEvent();
    }


    private void OnTableUpdateEvent()
    {
        if (_tableFurnitureData == null)
        {
            DebugLog.LogError("테이블 데이터가 없습니다.");
            return;
        }

        if (_tableData.TableState == ETableState.NeedCleaning)
        {
            _spriteRenderer.sprite = _tableFurnitureData.DirtyTableSprite;
        }

        else
        {
            _spriteRenderer.sprite = _tableFurnitureData.Sprite;
        }
    }


    public override void SetFurnitureData(FurnitureData data)
    {
        if (data == null)
        {
            _tableFurnitureData = null;
            if (_defalutSprite == null)
            {
                _spriteRenderer.gameObject.SetActive(false);
                _leftChairSpriteRenderer.gameObject.SetActive(false);
                _rightChairSpriteRenderer.gameObject.SetActive(false);
            }


            else
            {
                _spriteRenderer.sprite = _defalutSprite;
                _leftChairSpriteRenderer.sprite = _chairDefalutSprite;
                _rightChairSpriteRenderer.sprite = _chairDefalutSprite;
                _rightChairSpriteRenderer.flipX = true;
                SetRendererScale(null);
            }

            return;
        }

        if(!(data is TableFurnitureData))
        {
            DebugLog.LogError("TableFurniture 컴포넌트에선 TableFurnitureData만 사용할 수 있습니다.");
            return;
        }

        _tableFurnitureData = (TableFurnitureData)data;

        _spriteRenderer.gameObject.SetActive(true);
        _leftChairSpriteRenderer.gameObject.SetActive(true);
        _rightChairSpriteRenderer.gameObject.SetActive(true);
        _spriteRenderer.sprite = data.Sprite;
        _leftChairSpriteRenderer.sprite = _tableFurnitureData.ChairSprite;
        _rightChairSpriteRenderer.sprite = _tableFurnitureData.RightChairSprite == null ? _tableFurnitureData.ChairSprite : _tableFurnitureData.RightChairSprite;
        _rightChairSpriteRenderer.flipX = _tableFurnitureData.RightChairSprite == null ? true : false;
        OnTableUpdateEvent();
        SetRendererScale(_tableFurnitureData);


    }

    private void SetRendererScale(TableFurnitureData tableData)
    {
        Vector3 scale = tableData == null || tableData.Scale <= 0 ? _tmpScale : tableData.Scale * _tmpScale;
        _spriteRenderer.transform.localScale = scale;
        _leftChairSpriteRenderer.transform.localScale = scale;
        _rightChairSpriteRenderer.transform.localScale = scale;

        float heightMul = (_spriteRenderer.sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
        float leftChairHeightMul = (_leftChairSpriteRenderer.sprite.bounds.size.y * 0.5f) * _leftChairSpriteRenderer.transform.lossyScale.y;
        float rightChairHeightMul = (_rightChairSpriteRenderer.sprite.bounds.size.y * 0.5f) * _rightChairSpriteRenderer.transform.lossyScale.y;

        if (_batchType == FurnitureBatchTypeY.Lower)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);
            _leftChairSpriteRenderer.transform.localPosition = new Vector3(_leftChairSpriteRenderer.transform.localPosition.x, leftChairHeightMul, 0);
            _rightChairSpriteRenderer.transform.localPosition = new Vector3(_rightChairSpriteRenderer.transform.localPosition.x, rightChairHeightMul, 0);
        }
        else if (_batchType == FurnitureBatchTypeY.Upper)
        {
            _spriteRenderer.transform.localPosition = new Vector3(0, -heightMul, 0);
            _leftChairSpriteRenderer.transform.localPosition = new Vector3(_leftChairSpriteRenderer.transform.localPosition.x, -leftChairHeightMul, 0);
            _rightChairSpriteRenderer.transform.localPosition = new Vector3(_rightChairSpriteRenderer.transform.localPosition.x, -rightChairHeightMul, 0);
        }
        else if (_batchType == FurnitureBatchTypeY.Center)
        {
            _spriteRenderer.transform.localPosition = Vector3.zero;
            _leftChairSpriteRenderer.transform.localPosition = new Vector3(_leftChairSpriteRenderer.transform.localPosition.x, 0, 0);
            _rightChairSpriteRenderer.transform.localPosition = new Vector3(_rightChairSpriteRenderer.transform.position.x, 0, 0);
        }
    }

    private void OnTouchEvent()
    {
        if (_tableData.TableState != ETableState.NeedCleaning)
        {
            DebugLog.Log("더러운 상태가 아닙니다: " + _tableData.TableType);
            return;
        }


        _tableData.TableState = ETableState.None;
        _tableManager.UpdateTable();
    }
}
