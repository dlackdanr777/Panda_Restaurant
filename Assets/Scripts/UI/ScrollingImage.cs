using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ScrollingImage : MonoBehaviour
{
    [SerializeField] private Vector2 _dir;
    private Image _image;
    private Material _material;
    

    private void Awake()
    {
        _image = GetComponent<Image>();
        _material = Instantiate(_image.material);
        _image.material = _material;
    }


    private void OnDisable()
    {
        _material.mainTextureOffset = Vector2.zero;
    }


    void Update()
    {
        _material.mainTextureOffset += _dir * Time.deltaTime;
    }
}
