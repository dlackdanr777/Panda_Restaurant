using UnityEngine;

public class RotationGameObject : MonoBehaviour
{
    [SerializeField] private bool _isStart = true;
    [SerializeField] private GameObject _gameObject;
    [SerializeField] private float _rotatePerMinute;
    public void Update()
    {
        if (!_isStart)
            return;

        _gameObject.transform.Rotate(0, 0, _rotatePerMinute * Time.deltaTime);
    }

    public void Play()
    {
        _isStart = true;
    }

    public void Stop()
    {
        _isStart = false;
    }
}
