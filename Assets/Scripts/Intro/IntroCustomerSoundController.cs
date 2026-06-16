using UnityEngine;

public class IntroCustomerSoundController : MonoBehaviour
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _jumpSound;

    public void PlayJumpSound()
    {
        _audioSource.PlayOneShot(_jumpSound);
    }
}
