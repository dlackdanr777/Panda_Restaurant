using UnityEngine;


[CreateAssetMenu(fileName = "MiniGame1 Item Database", menuName = "Scriptable Object/MiniGame/MiniGame1/MiniGame1 Item Database")]
public class Minigame1ItemDatabase : ScriptableObject
{
    [SerializeField] private MiniGame1ItemData[] _itemDataList;
    public MiniGame1ItemData[] ItemDataList => _itemDataList;
    
}


[System.Serializable]
public class MiniGame1ItemData
{
    [SerializeField] private string _id;
    public string Id => _id;

    [SerializeField] private string _itemName;
    public string ItemName => _itemName;

    [SerializeField] private Sprite _sprite;
    public Sprite Sprite => _sprite;
}