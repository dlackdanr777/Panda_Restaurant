using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldToSceenPosition : MonoBehaviour
{
    [SerializeField] private Transform _worldTransform;
    [SerializeField] private Vector3 _offset;

    private Camera _camera;

    public void SetWorldTransform(Transform tr)
    {
        _worldTransform = tr;
    }

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void OnEnable()
    {
        if (_worldTransform == null || _camera == null)
            return;

        transform.position = _camera.WorldToScreenPoint(_worldTransform.position + _offset);
    }

    private void OnDisable()
    {
        if (_worldTransform == null || _camera == null)
            return;

        transform.position = _camera.WorldToScreenPoint(_worldTransform.position + _offset);
    }

    private void Update()
    {
        if (_worldTransform == null)
            return;

        transform.position = _camera.WorldToScreenPoint(_worldTransform.position + _offset);
    }
}
