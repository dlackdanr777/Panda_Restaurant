using UnityEngine;

public class SpriteFillAmount : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Range(0f, 1f)][SerializeField] private float _value;

    private Vector2 _tmpSize;


    public void Awake()
    {
        _spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        _tmpSize = _spriteRenderer.sprite.bounds.size;
        _spriteRenderer.size = _tmpSize;
        SetFillAmount(_value);
    }



    public void SetFillAmount(float amount)
    {
        _value = Mathf.Clamp(amount, 0f, 1f);
        _spriteRenderer.size = new Vector2(_tmpSize.x * _value, _tmpSize.y);
    }


    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }
}
