
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
}
