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

    public Texture GetTexture()
    {
        // Material이 없으면 초기화 시도
        if (_material == null)
        {
            if (_image == null)
                _image = GetComponent<Image>();
            
            if (_image != null && _image.material != null)
            {
                _material = _image.material;
            }
        }
        
        // Material이 있으면 mainTexture 반환
        if (_material != null && _material.mainTexture != null)
        {
            return _material.mainTexture;
        }
        
        // Material이 없으면 Image의 sprite texture 반환
        if (_image != null && _image.sprite != null)
        {
            return _image.sprite.texture;
        }
        
        Debug.LogError($"GetTexture failed for {gameObject.name}. Material: {_material != null}, Image: {_image != null}");
        return null;
    }

    public Vector2 GetTextureScale()
    {
        if (_material != null)
        {
            return _material.mainTextureScale;
        }
        return Vector2.one;
    }


    public void Init()
    {
        if (_image == null)
            _image = GetComponent<Image>();
        
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();
        
        // Material이 이미 있으면 재사용
        if (_material == null && _image != null && _image.material != null)
        {
            _material = Instantiate(_image.material);
            _image.material = _material;
        }
        
        Debug.Log($"ScrollingImage Init: {gameObject.name}, Material: {_material != null}, Texture: {GetTexture() != null}");
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
