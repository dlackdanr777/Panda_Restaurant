using UnityEngine;

public class TableFurniture : Furniture
{
    [Space]
    [Header("Table Furniture")]
    [SerializeField] private Sprite _chairDefalutSprite;
    [SerializeField] private SpriteRenderer _leftChairSpriteRenderer;
    [SerializeField] private SpriteRenderer _rightChairSpriteRenderer;
    [SerializeField] private PointerDownSpriteRenderer _pointerDownSpriteRenderer;
    [SerializeField] private SpriteRenderer _bowlRenderer;

    private TableFurnitureData _tableFurnitureData;
    private TableData _tableData;
    private SaveTableData _saveTableData;

    public override void Init(TableManager manager, ERestaurantFloorType floor)
    {
        base.Init(manager, floor);
        manager.OnTableUpdateHandler += OnTableUpdateEvent;
        _pointerDownSpriteRenderer.AddEvent(OnTouchEvent);
    }


    public void SetTableData(TableData data, SaveTableData saveData)
    {
        _tableData = data;
        _saveTableData = saveData;
        _tableData.TableFurniture = this;
        _saveTableData.SetNeedCleaning(_tableData.TableState == ETableState.NeedCleaning);
    }


    public void OnCleanAction()
    {
        if (_tableData.TableState != ETableState.NeedCleaning)
        {
            DebugLog.Log("더러운 상태가 아닙니다: " + _tableData.TableType);
            return;
        }

        _tableData.TableState = ETableState.Empty;
        _tableManager.UpdateTable();
    }


    private void OnTableUpdateEvent()
    {
        if (_tableFurnitureData == null || _tableData == null)
        {
            DebugLog.LogError("테이블 데이터가 없습니다.");
            return;
        }



        if (_tableData.TableState == ETableState.NeedCleaning)
        {
            _bowlRenderer.gameObject.SetActive(true);
            _saveTableData.SetNeedCleaning(true);
            //_spriteRenderer.sprite = _tableFurnitureData.DirtyTableSprite;
        }

        else
        {
            _bowlRenderer.gameObject.SetActive(false);
            _saveTableData.SetNeedCleaning(false);
            //_spriteRenderer.sprite = _tableFurnitureData.Sprite;
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

        float mainY = GetBatchYOffset(_batchType, _spriteRenderer);
        float leftY = GetBatchYOffset(_batchType, _leftChairSpriteRenderer);
        float rightY = GetBatchYOffset(_batchType, _rightChairSpriteRenderer);

        _spriteRenderer.transform.localPosition = new Vector3(0, mainY, 0);
        _leftChairSpriteRenderer.transform.localPosition = new Vector3(
            _leftChairSpriteRenderer.transform.localPosition.x, leftY, 0);
        _rightChairSpriteRenderer.transform.localPosition = new Vector3(
            _rightChairSpriteRenderer.transform.localPosition.x, rightY, 0);

        // 그릇 위치 = 테이블 위치에 그대로 맞춤
        if (_bowlRenderer != null && _spriteRenderer.sprite != null)
        {
            _bowlRenderer.transform.localPosition = _spriteRenderer.transform.localPosition;
        }
    }



    private void OnTouchEvent()
    {
        if (_tableData.TableState != ETableState.NeedCleaning)
        {
            DebugLog.Log("더러운 상태가 아닙니다: " + _tableData.TableType);
            return;
        }

        if(!UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, _floor))
        {
            PopupManager.Instance.ShowDisplayText("싱크대가 꽉 차, 그릇을 정리할 수 없습니다.");
            return;
        }

        _tableData.TableState = ETableState.Empty;
        UserInfo.AddSinkBowlCount(UserInfo.CurrentStage, _floor);
        _tableManager.UpdateTable();
    }


    private float GetBatchYOffset(FurnitureBatchTypeY typeY, SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return 0f;

        Sprite sprite = renderer.sprite;
        float height = sprite.bounds.size.y;
        float scaleY = renderer.transform.lossyScale.y;

        float pivotY = sprite.pivot.y / sprite.rect.height;

        switch (typeY)
        {
            case FurnitureBatchTypeY.Lower:
                // 바닥 정렬: 피벗이 0일 때 기준 → 현재 피벗만큼 위로 올려야 함
                return height * pivotY * scaleY;

            case FurnitureBatchTypeY.Upper:
                // 천장 정렬: 피벗이 1일 때 기준 → (1 - pivotY) 만큼 내려야 함
                return -height * (1f - pivotY) * scaleY;

            case FurnitureBatchTypeY.Center:
                // 중앙 정렬: 피벗이 0.5일 때 기준 → (0.5 - pivotY) 보정
                return (0.5f - pivotY) * height * scaleY;

            case FurnitureBatchTypeY.None:
            default:
                return renderer.transform.localPosition.y;
        }
    }


}
