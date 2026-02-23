using UnityEngine;

public class FollowPosition : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private Transform _followObject;
    [SerializeField] private Vector3 _offset;

    private void LateUpdate()
    {
        if (_target == null)
            return;

        _followObject.position = _target.position + _offset; 
    }
}
