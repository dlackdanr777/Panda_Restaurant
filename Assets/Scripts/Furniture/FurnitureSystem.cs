using System.Collections.Generic;
using UnityEngine;

public class FurnitureSystem : MonoBehaviour
{
    [SerializeField] private Furniture[] _furniture;
    private Dictionary<FurnitureType, List<Furniture>> _furnitureDic = new Dictionary<FurnitureType, List<Furniture>>();

    private void Awake()
    {
        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            _furnitureDic.Add((FurnitureType)i, new List<Furniture>());
        }

        for (int i = 0, cnt = _furniture.Length; i < cnt; ++i)
        {
            _furnitureDic[_furniture[i].Type].Add(_furniture[i]);     
        }

        for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
        {
            OnChangeFurnitureEvent((FurnitureType)i);
        }
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
    }


    private void OnChangeFurnitureEvent(FurnitureType type)
    {
        FurnitureData equipFurniture = UserInfo.GetEquipFurniture(type);

        foreach(Furniture data in _furnitureDic[type])
        {
            data.SetFurnitureData(equipFurniture);
        }
    }
}
