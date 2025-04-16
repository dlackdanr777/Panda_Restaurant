using UnityEngine;

public class TableFurniture : Furniture
{
    [Space]
    [Header("Table Furniture")]
    [SerializeField] private Sprite _defalutLeftChairSprite;
    [SerializeField] private Sprite _defalutRightChairSprite;
    [SerializeField] private SpriteRenderer _leftChairSpriteRenderer;
    [SerializeField] private SpriteRenderer _rightChairSpriteRenderer;
    [SerializeField] private SpriteRenderer _leftChairArmrestSpriteRenderer;
    [SerializeField] private SpriteRenderer _rightChairArmrestSpriteRenderer;
    [SerializeField] private PointerDownSpriteRenderer _pointerDownSpriteRenderer;
    [SerializeField] private SpriteRenderer _bowlRenderer;

    [Space]
    [Header("Sounds")]
    [SerializeField] private AudioClip _cleanSound;

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


    private void OnTableUpdateEvent()
    {
        if (_tableData == null)
        {
            DebugLog.LogError("테이블 데이터가 없습니다.");
            return;
        }

        if(_tableFurnitureData == null && _type != FurnitureType.Table1)
        {
            DebugLog.Log("현재 장착되어있는 가구 데이터가 없습니다.");
            _bowlRenderer.gameObject.SetActive(false);
            return;
        }

        if (_tableData.TableState == ETableState.NeedCleaning)
        {
            _bowlRenderer.gameObject.SetActive(true);
            _saveTableData.SetNeedCleaning(true);
        }

        else
        {
            _bowlRenderer.gameObject.SetActive(false);
            _saveTableData.SetNeedCleaning(false);
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
                _leftChairArmrestSpriteRenderer.gameObject.SetActive(false);
                _rightChairArmrestSpriteRenderer.gameObject.SetActive(false);
            }

            else
            {
                _spriteRenderer.sprite = _defalutSprite;
                _leftChairSpriteRenderer.sprite = _defalutLeftChairSprite;
                _rightChairSpriteRenderer.sprite = _defalutRightChairSprite;
                _leftChairArmrestSpriteRenderer.sprite = null;
                _rightChairArmrestSpriteRenderer.sprite = null;
                SetRendererScale();
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

        _leftChairSpriteRenderer.sortingLayerID = _tableFurnitureData.IsChairForward ?
            SortingLayer.NameToID("ForwardChair") : SortingLayer.NameToID("Chair");
        _rightChairSpriteRenderer.sortingLayerID = _tableFurnitureData.IsChairForward ?
            SortingLayer.NameToID("ForwardChair") : SortingLayer.NameToID("Chair");

        _leftChairArmrestSpriteRenderer.sprite = _tableFurnitureData.LeftChairArmrestSprite;
        _rightChairArmrestSpriteRenderer.sprite = _tableFurnitureData.RightChairArmrestSprite;
        
        OnTableUpdateEvent();
        SetRendererScale();
    }

    private void SetRendererScale()
    {
        // Vector3 scale = tableData == null || tableData.Scale <= 0 ? _tmpScale : tableData.Scale * _tmpScale;

        // _spriteRenderer.transform.localScale = scale;
        // _leftChairSpriteRenderer.transform.localScale = scale;
        // _rightChairSpriteRenderer.transform.localScale = scale;

        float mainY = GetBatchYOffset(_batchType, _spriteRenderer);
        float leftY = GetBatchYOffset(_batchType, _leftChairSpriteRenderer);
        float rightY = GetBatchYOffset(_batchType, _rightChairSpriteRenderer);

        float mainX = GetBatchXOffset(_spriteRenderer);
        float leftX = GetBatchXOffset(_leftChairSpriteRenderer);
        float rightX = GetBatchXOffset(_rightChairSpriteRenderer);

        _spriteRenderer.transform.localPosition = new Vector3(mainX, mainY, 0);
        _leftChairSpriteRenderer.transform.localPosition = new Vector3(leftX, leftY, 0);
        _rightChairSpriteRenderer.transform.localPosition = new Vector3(rightX, rightY, 0);

        if(_leftChairArmrestSpriteRenderer != null && _leftChairArmrestSpriteRenderer.sprite != null)
        {
            _leftChairArmrestSpriteRenderer.gameObject.SetActive(true);
            _leftChairArmrestSpriteRenderer.transform.localPosition = new Vector3(leftX, leftY, 0);
        }
        else
        {
            _leftChairArmrestSpriteRenderer.gameObject.SetActive(false);
        }

        if(_rightChairArmrestSpriteRenderer != null && _rightChairArmrestSpriteRenderer.sprite != null)
        {
            _rightChairArmrestSpriteRenderer.gameObject.SetActive(true);
            _rightChairArmrestSpriteRenderer.transform.localPosition = new Vector3(rightX, rightY, 0);
        }
        else
        {
            _rightChairArmrestSpriteRenderer.gameObject.SetActive(false);
        }

        // 그릇 위치 = 테이블 위치에 그대로 맞춤
        if (_bowlRenderer != null && _spriteRenderer.sprite != null)
        {
            _bowlRenderer.transform.localPosition = _spriteRenderer.transform.localPosition;
        }

        if(_tableData != null)
        {
            _tableData.SetLeftChairTrPos(_leftChairSpriteRenderer.transform.position);
            _tableData.SetRightChairTrPos(_rightChairSpriteRenderer.transform.position);
        }
    }


    public void OnCleanAction()
    {
        if (_tableData.TableState != ETableState.NeedCleaning)
        {
            DebugLog.Log("더러운 상태가 아닙니다: " + _tableData.TableType);
            return;
        }

        if (!UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, _floor))
        {   
            return;
        }

        _tableData.TableState = ETableState.Empty;
        UserInfo.AddSinkBowlCount(UserInfo.CurrentStage, _floor);
        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _cleanSound);
        _tableManager.UpdateTable();
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
            PopupManager.Instance.ShowDisplayText("싱크대가 꽉 차 정리할 수 없습니다.");
            return;
        }

        _tableData.TableState = ETableState.Empty;
        UserInfo.AddSinkBowlCount(UserInfo.CurrentStage, _floor);
        SoundManager.Instance.PlayEffectAudio(EffectType.Hall, _cleanSound);
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
                return height * pivotY * scaleY - 1.4f;

            case FurnitureBatchTypeY.Upper:
                // 천장 정렬: 피벗이 1일 때 기준 → (1 - pivotY) 만큼 내려야 함
                return -height * (1f - pivotY) * scaleY - 1.4f;

            case FurnitureBatchTypeY.Center:
                // 중앙 정렬: 피벗이 0.5일 때 기준 → (0.5 - pivotY) 보정
                return (0.5f - pivotY) * height * scaleY - 1.4f;

            case FurnitureBatchTypeY.None:
            default:
                return renderer.transform.localPosition.y;
        }
    }

/// <summary> 스프라이트의 피벗을 고려하여 X 좌표 오프셋을 계산하고 반환하는 함수 </summary>>
    private float GetBatchXOffset(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return 0f;

        Sprite sprite = renderer.sprite;
        float width = sprite.bounds.size.x;
        float scaleX = renderer.transform.lossyScale.x;

        // 실제 피벗 X 비율 (0~1 사이 값)
        float pivotX = sprite.pivot.x / sprite.rect.width;
    
        // 피벗이 0.5(중앙)이 아닌 경우 보정
        // 피벗이 0.5보다 작으면 오른쪽으로, 크면 왼쪽으로 이동
        return -(0.5f - pivotX) * width * scaleX;
    }
}
