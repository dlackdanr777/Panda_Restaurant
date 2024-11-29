using UnityEngine;

public class UIMiniGameJarGroup : MonoBehaviour
{
    [SerializeField] private Transform _stick;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _shakeSound;

    private UIMiniGame _uiMiniGame;
    private int _lastIndex;
    private int _firstIndex;

    public void Init(UIMiniGame uiMiniGame)
    {
        _uiMiniGame = uiMiniGame;
        _lastIndex = transform.childCount;
        _firstIndex = _lastIndex - 5;
    }

    public void StickSetSiblingIndex(int index)
    {
        SetSiblingIndex(_stick, index);
    }


    public void PlayShakeSound()
    {
        _audioSource.pitch = Mathf.Clamp(0.5f + _uiMiniGame.CurrentPower * 0.02f, 0.5f, 2);
        _audioSource.PlayOneShot(_shakeSound);
    }


    private void SetSiblingIndex(Transform tr, int index)
    {
        index = Mathf.Clamp(index, _firstIndex, _lastIndex);
        tr.SetSiblingIndex(index);
    }
}
