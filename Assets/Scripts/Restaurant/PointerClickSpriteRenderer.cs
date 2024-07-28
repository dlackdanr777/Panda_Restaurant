using UnityEngine;

public class PointerClickSpriteRenderer : PointerClick
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
}
