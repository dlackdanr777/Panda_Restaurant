using UnityEngine;

public class PointerDownSpriteRenderer : PointerDown
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
}
