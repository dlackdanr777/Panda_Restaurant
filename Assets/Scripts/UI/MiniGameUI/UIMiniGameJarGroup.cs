using UnityEngine;

public class UIMiniGameJarGroup : MonoBehaviour
{
    [SerializeField] private Transform _stick;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _shakeSound;

    private int _lastIndex;
    private int _firstIndex;

    public void Init()
    {
        _lastIndex = transform.childCount;
        _firstIndex = 0;
    }

    public void StickSetSiblingIndex(int index)
    {
        SetSiblingIndex(_stick, index);
    }


    public void PlayShakeSound()
    {
        _audioSource.PlayOneShot(_shakeSound);
    }


    private void SetSiblingIndex(Transform tr, int index)
    {
        index = Mathf.Clamp(index, _firstIndex, _lastIndex);
        tr.SetSiblingIndex(index);
    }
}
