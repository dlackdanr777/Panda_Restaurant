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

    
    public void SetData(KitchenUtensilData data)
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


        if(_batchType == KitchenUtensilBatchType.Lower)
        {
            float heightMul = (data.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
            _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);
        }
        else if(_batchType == KitchenUtensilBatchType.Upper)
        {
            float heightMul = (data.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
            _spriteRenderer.transform.localPosition = new Vector3(0, -heightMul, 0);
        }
        else if(_batchType == KitchenUtensilBatchType.Center)
        {
            _spriteRenderer.transform.localPosition = Vector3.zero;
        }

    }
}
