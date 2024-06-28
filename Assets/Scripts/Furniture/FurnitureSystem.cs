using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FurnitureSystem : MonoBehaviour
{
    [SerializeField] private Furniture[] _furniture = new Furniture[(int)FurnitureType.Length];


    private void Awake()
    {
        UserInfo.OnChangeFurnitureHandler += OnChangeFurnitureEvent;
    }

    private void Start()
    {
        for(int i = 0, cnt = (int)FurnitureType.Length; i < cnt; i++)
        {
            _furniture[i].SetFurnitureData(UserInfo.GetEquipFurniture((FurnitureType)i));
        }
    }


    public void OnChangeFurnitureEvent(FurnitureType type)
    {
        FurnitureData equipFurniture = UserInfo.GetEquipFurniture(type);
        _furniture[(int)type].SetFurnitureData(equipFurniture);
    }
}
