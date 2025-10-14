using UnityEngine;

[System.Serializable]
public class Capsule
{
    [SerializeField] private Sprite _upperCapsule;
    public Sprite UpperCapsule => _upperCapsule;

    [SerializeField] private Sprite _lowerCapsule;
    public Sprite LowerCapsule => _lowerCapsule;
}