using Muks.WeightedRandom;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CustomerData", menuName = "Scriptable Object/CustomerData")]
public class CustomerData : ScriptableObject
{
    [Header("Status")]
    [SerializeField] protected Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] protected string _name;
    public string Name => _name;

    [SerializeField] protected string _id;
    public string Id => _id;

    [TextArea][SerializeField] protected string _description;
    public string Description => _description;

    [SerializeField] protected float _moveSpeed = 6f;
    public float MoveSpeed => _moveSpeed;



    [Space]
    [Header("殿厘 可记")]

    [SerializeField] protected int _minScore;
    public int MinScore => _minScore;

    [SerializeField] protected string _requiredDish;
    public string RequiredDish => _requiredDish;

    [SerializeField] protected string _requiredItem;
    public string RequiredItem => _requiredItem;



    public CustomerData(Sprite sprite, string id, string name, string description, float moveSpeed, int minScore, string requiredDish, string requiredItem)
    {
        _sprite = sprite;
        _id = id;
        _name = name;
        _description = description;
        _moveSpeed = moveSpeed;
        _minScore = minScore;
        _requiredDish = requiredDish;
        _requiredItem = requiredItem;

        DebugLog.Log($"CustomerData 积己凳: {id} {name} {moveSpeed} {minScore} {requiredItem}");
    }
}
