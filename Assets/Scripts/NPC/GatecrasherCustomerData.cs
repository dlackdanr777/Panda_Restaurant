
using UnityEngine;

public abstract class GatecrasherCustomerData : CustomerData
{
    [Space]
    [Header("Gatecrasher Option")]
    [Range(0f, 10f)] [SerializeField] private float _spawnChance;
    public float SpawnChance => _spawnChance;

    [Range(0, 100)][SerializeField] private int _activeDuration;
    public int ActiveDuration => _activeDuration;

    [Range(0, 100)][SerializeField] private int _touchCount;
    public int TouchCount => _touchCount;

    [SerializeField] private RuntimeAnimatorController _controller;
    public RuntimeAnimatorController Controller => _controller;

    public GatecrasherCustomerData(Sprite sprite, string id, string name, string description, float moveSpeed, int minScore, string requiredDish, string requiredItem, int activeDuration, int touchCount, float spawnChance, RuntimeAnimatorController controller) : base(sprite, id, name, description, moveSpeed, minScore, requiredDish, requiredItem)
    {
        _activeDuration = activeDuration;
        _touchCount = touchCount;
        _spawnChance = spawnChance;
        _controller = controller;
    }
}
