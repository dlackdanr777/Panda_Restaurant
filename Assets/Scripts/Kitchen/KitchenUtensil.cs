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

    private float _initialSpriteHeight; // �ʱ� ��������Ʈ ���� �����

    public void Init()
    {
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

        // ���� ��������Ʈ ���� ���
        float newSpriteHeight = _spriteRenderer.sprite.bounds.size.y;

        if (_initialSpriteHeight > 0 && newSpriteHeight > 0)
        {
            // ���̸� �������� ������ ���� ���
            float heightRatio = _initialSpriteHeight / newSpriteHeight;

            // ���ο� ������ ���� (SizeMul ����)
            Vector3 newScale = _spriteRenderer.transform.localScale;
            newScale.x = heightRatio * data.SizeMul;
            newScale.y = heightRatio * data.SizeMul;
            _spriteRenderer.transform.localScale = newScale;
        }

        // ��ġ Ÿ�Կ� ���� ��ġ ����
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
