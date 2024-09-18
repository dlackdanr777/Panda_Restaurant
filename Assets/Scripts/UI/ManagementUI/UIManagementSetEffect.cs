using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum SetEffectType
{
    Furniture,
    KitchenUntensils,
    Length,
}

public class UIManagementSetEffect : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _setName;
    [SerializeField] private TextMeshProUGUI _setDescription;

    [Header("Slot Options")]
    [SerializeField] private Transform _setCountParent;
    [SerializeField] private UIManagementSetCount _setCountPrefab;

    private UIManagementSetCount[] _setCounts;
    private SetEffectType _setEffectType;

    public void Init(SetEffectType type)
    {
        _setEffectType = type;
        _setCounts = new UIManagementSetCount[SetDataManager.Count];

        for(int i = 0, cnt = _setCounts.Length; i < cnt; ++i)
        {
            UIManagementSetCount setCount = Instantiate(_setCountPrefab, _setCountParent);
            _setCounts[i] = setCount;
            setCount.gameObject.SetActive(false);
        }
    }


    public void SetData(SetData data)
    {
        if (data == null)
        {
            _setDescription.gameObject.SetActive(false);
            _setCountParent.gameObject.SetActive(true);
            _setName.text = "��ġ ��Ȳ";

            for (int i = 0, cnt = _setCounts.Length; i < cnt; ++i)
            {
                _setCounts[i].gameObject.SetActive(false);
            }

            Dictionary<string, int> equipSetDataCountDic = new Dictionary<string, int>();
            int index = 0;
            switch (_setEffectType)
            {
                case SetEffectType.Furniture:
                    FurnitureData furniutreData;
                    for(int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
                    {
                        furniutreData = UserInfo.GetEquipFurniture((FurnitureType)i);
                        if (furniutreData == null)
                            continue;

                        if (equipSetDataCountDic.ContainsKey(furniutreData.SetId))
                        {
                            equipSetDataCountDic[furniutreData.SetId] += 1;
                            continue;
                        }
                        equipSetDataCountDic.Add(furniutreData.SetId, 1);
                    }

                    foreach(var setData in equipSetDataCountDic)
                    {
                        _setCounts[index].gameObject.SetActive(true);
                        _setCounts[index].SetData(SetDataManager.Instance.GetSetData(setData.Key).Name, setData.Value, ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT);
                        index++;
                    }
                    return;


                case SetEffectType.KitchenUntensils:
                    KitchenUtensilData kitchenData;
                    for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
                    {
                        kitchenData = UserInfo.GetEquipKitchenUtensil((KitchenUtensilType)i);
                        if (kitchenData == null)
                            continue;

                        if (equipSetDataCountDic.ContainsKey(kitchenData.SetId))
                        {
                            equipSetDataCountDic[kitchenData.SetId] += 1;
                            continue;
                        }
                        equipSetDataCountDic.Add(kitchenData.SetId, 1);
                    }

                    foreach (var setData in equipSetDataCountDic)
                    {
                        _setCounts[index].gameObject.SetActive(true);
                        _setCounts[index].SetData(SetDataManager.Instance.GetSetData(setData.Key).Name, setData.Value, ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT);
                        index++;
                    }
                    return;
            }

            return;
        }


        _setDescription.gameObject.SetActive(true);
        _setCountParent.gameObject.SetActive(false);
        _setName.text = data.Name;
        _setDescription.text = Utility.GetSetEffectDescription(data);
    }
}
