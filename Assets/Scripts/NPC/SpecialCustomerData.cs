using UnityEngine;

[CreateAssetMenu(fileName = " SpecialCustomerData", menuName = "Scriptable Object/SpecialCustomerData")]
public class SpecialCustomerData : CustomerData
{
    [Space]
    [Header("SpecialCustomer Option")]

    [SerializeField] private Sprite _touchSprite;
    public Sprite TouchSprite => _touchSprite;

    [Range(0f, 10f)] [SerializeField] private float _spawnChance;
    public float SpawnChance => _spawnChance;

    [Range(0, 100)] [SerializeField] private int _activeDuration;
    public int ActiveDuration => _activeDuration;

    [Range(0, 100)][SerializeField] private int _touchCount;
    public int TouchCount => _touchCount;

    [Range(0, 10000)][SerializeField] private int _touchAddMoney;
    public int TouchAddMoney => _touchAddMoney;
}
