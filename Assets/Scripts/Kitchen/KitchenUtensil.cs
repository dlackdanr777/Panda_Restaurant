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

    protected float _initialSpriteHeight; // �ʱ� ��������Ʈ ���� �����

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
            _initialSpriteHeight = 1f; // �⺻�� ����
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
            // ���̸� �������� ������ ���� ���
            float heightRatio = _initialSpriteHeight / newSpriteHeight;

            // ���ο� ������ ���� (SizeMul ����)
            Vector3 newScale = _spriteRenderer.transform.localScale;
            newScale.x = heightRatio * sizeMul;
            newScale.y = heightRatio * sizeMul;
            _spriteRenderer.transform.localScale = newScale;
        }

        // ��ġ Ÿ�Կ� ���� ��ġ ����
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
