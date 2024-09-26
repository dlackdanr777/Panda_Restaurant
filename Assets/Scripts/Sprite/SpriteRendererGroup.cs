using UnityEngine;


public class SpriteRendererGroup : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] _spriteRenderers;

    public void SetAlpha(float alpha)
    {
        for(int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            Color tmpColor = _spriteRenderers[i].color;
            _spriteRenderers[i].color = new Color(tmpColor.r, tmpColor.g, tmpColor.b, alpha);
        }
    }

    public void SetColor(Color color)
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].color = color;
        }
    }

    public void SetAcive(bool acive)
    {
        for (int i = 0, cnt = _spriteRenderers.Length; i < cnt; ++i)
        {
            _spriteRenderers[i].gameObject.SetActive(acive);
        }
    }
}
