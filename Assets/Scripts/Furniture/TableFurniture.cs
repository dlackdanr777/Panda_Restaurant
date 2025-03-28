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
            DebugLog.Log("������ ���°� �ƴմϴ�: " + _tableData.TableType);
            return;
        }

        _tableData.TableState = ETableState.Empty;
        _tableManager.UpdateTable();
    }


    private void OnTableUpdateEvent()
    {
        if (_tableFurnitureData == null || _tableData == null)
        {
            DebugLog.LogError("���̺� �����Ͱ� �����ϴ�.");
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
            DebugLog.LogError("TableFurniture ������Ʈ���� TableFurnitureData�� ����� �� �ֽ��ϴ�.");
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

        // �׸� ��ġ = ���̺� ��ġ�� �״�� ����
        if (_bowlRenderer != null && _spriteRenderer.sprite != null)
        {
            _bowlRenderer.transform.localPosition = _spriteRenderer.transform.localPosition;
        }
    }



    private void OnTouchEvent()
    {
        if (_tableData.TableState != ETableState.NeedCleaning)
        {
            DebugLog.Log("������ ���°� �ƴմϴ�: " + _tableData.TableType);
            return;
        }

        if(!UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, _floor))
        {
            PopupManager.Instance.ShowDisplayText("��ũ�밡 �� ��, �׸��� ������ �� �����ϴ�.");
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
                // �ٴ� ����: �ǹ��� 0�� �� ���� �� ���� �ǹ���ŭ ���� �÷��� ��
                return height * pivotY * scaleY;

            case FurnitureBatchTypeY.Upper:
                // õ�� ����: �ǹ��� 1�� �� ���� �� (1 - pivotY) ��ŭ ������ ��
                return -height * (1f - pivotY) * scaleY;

            case FurnitureBatchTypeY.Center:
                // �߾� ����: �ǹ��� 0.5�� �� ���� �� (0.5 - pivotY) ����
                return (0.5f - pivotY) * height * scaleY;

            case FurnitureBatchTypeY.None:
            default:
                return renderer.transform.localPosition.y;
        }
    }


}
