using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ScrollingImage : MonoBehaviour
{
    [SerializeField] private Vector2 _dir;
    private Image _image;
    private Material _material;
    private RectTransform _rectTransform;
    public RectTransform RectTransform => _rectTransform;

    public Vector2 Offset => _material.mainTextureOffset;


    public void SetOffset(Vector2 offset)
    {
        _material.mainTextureOffset = offset;
    }


    public void Init()
    {
        _image = GetComponent<Image>();
        _rectTransform = GetComponent<RectTransform>();
        _material = Instantiate(_image.material);
        _image.material = _material;
    }


    private void Update()
    {
        if(_image == null)
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();
            _material = Instantiate(_image.material);
            _image.material = _material;
        }

        _material.mainTextureOffset += _dir * Time.deltaTime;
    }
}
