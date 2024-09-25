using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ScrollingImage : MonoBehaviour
{
    [SerializeField] private Vector2 _dir;
    private Image _image;
    private Material _material;

    public Vector2 Offset => _material.mainTextureOffset;


    public void SetOffset(Vector2 offset)
    {
        _material.mainTextureOffset = offset;
    }


    public void Init()
    {
        _image = GetComponent<Image>();
        _material = Instantiate(_image.material);
        _image.material = _material;
    }


    private void Update()
    {
        _material.mainTextureOffset += _dir * Time.deltaTime;
    }
}
