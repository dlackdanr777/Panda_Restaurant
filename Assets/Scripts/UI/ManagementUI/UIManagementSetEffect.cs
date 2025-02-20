using Muks.Tween;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SetEffectType
{
    Furniture,
    KitchenUntensils,
    Length,
}

public class UIManagementSetEffect : MonoBehaviour
{
    [Header("Set Camera Components")]
    [SerializeField] private RectTransform _setCameraGroup;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;

    [Header("SetDescroption Components")]
    [SerializeField] private UIHorizontalScrollSwipe _scrollSwipe;
    [SerializeField] private TextMeshProUGUI _setTitleText;
    [SerializeField] private TextMeshProUGUI _setDescriptionText;

    [Header("Slot Option")]
    [SerializeField] private RectTransform _slotParent;
    [SerializeField] private UIManagementSetCount _setCountPrefab;

    private SetEffectType _currentSetEffectType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIManagementSetCount> _setCountList = new List<UIManagementSetCount>();


    public void Init()
    {
        for(int i = 0; i < 4; ++i)
        {
            UIManagementSetCount setCount = Instantiate(_setCountPrefab, _slotParent);
            _setCountList.Add(setCount);
            setCount.gameObject.SetActive(false);
        }

        _leftArrowButton.onClick.AddListener(() => OnChagneSetEffectType(SetEffectType.Furniture));
        _rightArrowButton.onClick.AddListener(() => OnChagneSetEffectType(SetEffectType.KitchenUntensils));

        OnChagneSetEffectType(_currentSetEffectType);
        OnChangeSetEffect(0);
        OnChangeSlotData();
        _scrollSwipe.OnChangePageHandler += OnChangeSetEffect;
    }

    public void UpdateUI()
    {
        OnChangeSetEffect(_scrollSwipe.CurrentPage);
        OnChangeSlotData();
    }


    private void OnChangeSlotData()
    {
        for (int i = 0, cnt = _setCountList.Count; i < cnt; ++i)
        {
            _setCountList[i].gameObject.SetActive(false);
        }

        Dictionary<string, int> equipSetDataCountDic = new Dictionary<string, int>();
        List<KeyValuePair<string, int>> sortList = new List<KeyValuePair<string, int>>();

        switch (_currentSetEffectType)
        {
            case SetEffectType.Furniture:
                FurnitureData furniutreData;
                for (int i = 0, cnt = (int)FurnitureType.Length; i < cnt; ++i)
                {
                    furniutreData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _currentFloorType, (FurnitureType)i);
                    if (furniutreData == null)
                        continue;

                    if (equipSetDataCountDic.ContainsKey(furniutreData.SetId))
                    {
                        equipSetDataCountDic[furniutreData.SetId] += 1;
                        continue;
                    }
                    equipSetDataCountDic.Add(furniutreData.SetId, 1);
                }

                foreach (var setData in equipSetDataCountDic)
                {
                    sortList.Add(new KeyValuePair<string, int>(setData.Key, setData.Value));
                }
                sortList.Sort((x, y) => y.Value.CompareTo(x.Value));

                for(int i = 0, cnt = _setCountList.Count; i < cnt; ++i)
                {
                    if (sortList.Count <= i)
                        break;

                    _setCountList[i].gameObject.SetActive(true);
                    _setCountList[i].SetData(SetDataManager.Instance.GetSetData(sortList[i].Key), sortList[i].Value, ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT);
                }
                return;


            case SetEffectType.KitchenUntensils:
                KitchenUtensilData kitchenData;
                for (int i = 0, cnt = (int)KitchenUtensilType.Length; i < cnt; ++i)
                {
                    kitchenData = UserInfo.GetEquipKitchenUtensil(_currentFloorType, (KitchenUtensilType)i);
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
                    sortList.Add(new KeyValuePair<string, int>(setData.Key, setData.Value));
                }
                sortList.Sort((x, y) => y.Value.CompareTo(x.Value));

                for (int i = 0, cnt = _setCountList.Count; i < cnt; ++i)
                {
                    if (sortList.Count <= i)
                        break;

                    _setCountList[i].gameObject.SetActive(true);
                    _setCountList[i].SetData(SetDataManager.Instance.GetSetData(sortList[i].Key), sortList[i].Value, ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT);
                }
                return;
        }
    }


    private void OnChangeSetEffect(int page)
    {
        if(page == 1)
        {
            _setTitleText.text = _currentSetEffectType == SetEffectType.Furniture ? "적용 가구 목록" : "적용 기구 목록";
            return;
        }

        else if(page == 0)
        {
            SetData setData = _currentSetEffectType == SetEffectType.Furniture ? UserInfo.GetEquipFurnitureSetData(UserInfo.CurrentStage, _currentFloorType) : UserInfo.GetEquipKitchenUntensilSetData(_currentFloorType);

            bool setEnabled = setData != null;
            _setTitleText.text = setEnabled ? setData.Name : "비활성화";
            _setDescriptionText.text = setEnabled ? Utility.GetSetEffectDescription(setData) : "적용중인 효과 없음";
        }

    }


    private void OnChagneSetEffectType(SetEffectType type)
    {
        if (type == SetEffectType.Furniture)
        {
            _rightArrowButton.gameObject.SetActive(true);
            _leftArrowButton.gameObject.SetActive(false);
        }
        else
        {
            _leftArrowButton.gameObject.SetActive(true);
            _rightArrowButton.gameObject.SetActive(false);
        }

        if (_currentSetEffectType == type)
            return;

        _currentSetEffectType = type;
        _setCameraGroup.TweenStop();
        if (type == SetEffectType.Furniture)
        {
            _setCameraGroup.TweenAnchoredPosition(new Vector2(0, -6), 0.3f, Ease.Smoothstep);
            _scrollSwipe.ChagneIndex(0);
            OnChangeSetEffect(0);
            OnChangeSlotData();
            return;
        }

        if (type == SetEffectType.KitchenUntensils)
        {
            _setCameraGroup.TweenAnchoredPosition(new Vector2(-546, -6), 0.3f, Ease.Smoothstep);
            _scrollSwipe.ChagneIndex(0);
            OnChangeSetEffect(0);
            OnChangeSlotData();
            return;
        }
    }
}
