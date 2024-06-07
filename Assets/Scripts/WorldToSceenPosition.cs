using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldToSceenPosition : MonoBehaviour
{
    [SerializeField] private Transform _worldTransform;

    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        transform.position = _camera.WorldToScreenPoint(_worldTransform.position);
    }

}
