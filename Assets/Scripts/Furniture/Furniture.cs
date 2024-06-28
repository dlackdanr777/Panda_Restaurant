using Muks.PathFinding.AStar;
using UnityEngine;

public class Furniture : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Sprite _defalutSprite;

    public void SetFurnitureData(FurnitureData data)
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
        float heightMul = (data.Sprite.bounds.size.y * 0.5f) * _spriteRenderer.transform.lossyScale.y;
        _spriteRenderer.transform.localPosition = new Vector3(0, heightMul, 0);
    }
}
