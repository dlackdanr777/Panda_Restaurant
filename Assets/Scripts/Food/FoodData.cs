using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FoodData", menuName = "Scriptable Object/FoodData")]
public class FoodData : ScriptableObject
{
    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;

    [SerializeField] private string _name;
    public string Name => _name;

    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _description;
    public string Description => _description;

    [SerializeField] private float _cookingTime;
    public float CookingTime => _cookingTime;   

    [SerializeField] private int _minScore;
    public int MinScore => _minScore;

    [SerializeField] private int _buyPrice;
    public int BuyPrice => _buyPrice;

    [SerializeField] private int _sellPrice;
    public int SellPrice => _sellPrice;

    [SerializeField] private bool _miniGameNeeded;
    public bool MiniGameNeeded => _miniGameNeeded;
}
