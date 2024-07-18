using UnityEngine;

public class SceenToWorldPosition : MonoBehaviour
{
    [SerializeField] private RectTransform _sceenTransform;
    [SerializeField] private Vector3 _offset;

    private Camera _camera;

    public void SetTransform(RectTransform tr)
    {
        _sceenTransform = tr;
    }

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        if (_sceenTransform == null)
            return;

        transform.position = _camera.ScreenToWorldPoint(_sceenTransform.position + _offset);
    }

}
