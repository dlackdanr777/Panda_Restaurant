using UnityEngine;

public class WorldToSceenPosition : MonoBehaviour
{
    [SerializeField] private Transform _worldTransform;
    [SerializeField] private Vector3 _offset;

    private Camera _camera;

    public void SetWorldTransform(Transform tr)
    {
        _worldTransform = tr;
        UpdateScreenPosition();
    }

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        UpdateScreenPosition();
    }

    private void Update()
    {
        UpdateScreenPosition();
    }

    private void UpdateScreenPosition()
    {
        if (_worldTransform == null || _camera == null)
            return;

        Vector3 screenPosition = _camera.WorldToScreenPoint(_worldTransform.position + _offset);
        if (transform.position != screenPosition)
            transform.position = screenPosition;
    }
}
