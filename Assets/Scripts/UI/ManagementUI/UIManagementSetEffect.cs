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
    [SerializeField] private RectTransform _furnitureRenderGroup;
    [SerializeField] private RectTransform _kitchenUtensilRenderGroup;

    [SerializeField] private Camera _furnitureCamera;
    [SerializeField] private Camera _kitchenUtensilCamera;

    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;

    [SerializeField] private GameobjectsSetActive _pageSetActiveGroup;

    [Header("SetDescroption Components")]
    [SerializeField] private UIHorizontalScrollSwipe _scrollSwipe;
    [SerializeField] private TextMeshProUGUI _setTitleText;
    [SerializeField] private TextMeshProUGUI _setDescriptionText;

    [Header("Slot Option")]
    [SerializeField] private RectTransform _slotParent1;
    [SerializeField] private RectTransform _slotParent2;
    [SerializeField] private UIManagementEquipSlot _equipSlotPrefab;


    [Space]
    [Header("Options")]
    [SerializeField] private float[] _floorHeights = new float[(int)ERestaurantFloorType.Length];


    private SetEffectType _currentSetEffectType;
    private ERestaurantFloorType _currentFloorType;
    private List<UIManagementEquipSlot> _eqipCountList1 = new List<UIManagementEquipSlot>();
    private List<UIManagementEquipSlot> _eqipCountList2 = new List<UIManagementEquipSlot>();

    public void Init()
    {
        for (int i = 0; i < 8; ++i)
        {
            UIManagementEquipSlot setCount = Instantiate(_equipSlotPrefab, _slotParent1);
            _eqipCountList1.Add(setCount);
            setCount.gameObject.SetActive(false);

            setCount = Instantiate(_equipSlotPrefab, _slotParent2);
            _eqipCountList2.Add(setCount);
            setCount.gameObject.SetActive(false);
        }


        _leftArrowButton.onClick.AddListener(() => OnChangeSetEffectType(-1));
        _rightArrowButton.onClick.AddListener(() => OnChangeSetEffectType(1));

        OnChangeSetEffectType(SetEffectType.Furniture);
        OnChangeSetEffect(0);
        _scrollSwipe.OnChangePageHandler += OnChangeSetEffect;
    }

    public void UpdateUI(ERestaurantFloorType floorType)
    {
        _currentFloorType = floorType;
        OnChangeSetEffectType(SetEffectType.Furniture);
        OnChangeFloorType(floorType);
        OnChangeSetEffect(0);
    }


    private void UpdateSlot()
    {
        for (int i = 0, cnt = _eqipCountList1.Count; i < cnt; ++i)
        {
            _eqipCountList1[i].gameObject.SetActive(false);
        }
        for (int i = 0, cnt = _eqipCountList2.Count; i < cnt; ++i)
        {
            _eqipCountList2[i].gameObject.SetActive(false);
        }


        if (_currentSetEffectType == SetEffectType.Furniture)
        {
            for (int i = 0, cnt = ConstValue.SET_EFFECT_ENABLE_FURNITURE_COUNT; i < cnt; ++i)
            {
                FurnitureData furnitureData = UserInfo.GetEquipFurniture(UserInfo.CurrentStage, _currentFloorType, (FurnitureType)i);
                if (furnitureData == null)
                    continue;

                bool isEnd = false;
                for (int j = 0, cntJ = _eqipCountList1.Count; j < cntJ; ++j)
                {
                    if (!_eqipCountList1[j].gameObject.activeSelf)
                    {
                        _eqipCountList1[j].SetData(furnitureData.Name, furnitureData.FoodType);
                        _eqipCountList1[j].gameObject.SetActive(true);
                        isEnd = true;
                        break;
                    }
                }

                if (isEnd)
                    continue;

                for (int j = 0, cntJ = _eqipCountList2.Count; j < cntJ; ++j)
                {
                    if (!_eqipCountList2[j].gameObject.activeSelf)
                    {
                        _eqipCountList2[j].SetData(furnitureData.Name, furnitureData.FoodType);
                        _eqipCountList2[j].gameObject.SetActive(true);
                        break;
                    }
                }
            }
        }
        else if (_currentSetEffectType == SetEffectType.KitchenUntensils)
        {

            for (int i = 0, cnt = ConstValue.SET_EFFECT_ENABLE_KITCHEN_UTENSIL_COUNT; i < cnt; ++i)
            {
                KitchenUtensilData data = UserInfo.GetEquipKitchenUtensil(UserInfo.CurrentStage, _currentFloorType, (KitchenUtensilType)i);
                if (data == null)
                    continue;

                bool isEnd = false;
                for (int j = 0, cntJ = _eqipCountList1.Count; j < cntJ; ++j)
                {
                    if (!_eqipCountList1[j].gameObject.activeSelf)
                    {
                        _eqipCountList1[j].SetData(data.Name, data.FoodType);
                        _eqipCountList1[j].gameObject.SetActive(true);
                        isEnd = true;
                        break;
                    }
                }

                if (isEnd)
                    continue;

                for (int j = 0, cntJ = _eqipCountList2.Count; j < cntJ; ++j)
                {
                    if (!_eqipCountList2[j].gameObject.activeSelf)
                    {
                        _eqipCountList2[j].SetData(data.Name, data.FoodType);
                        _eqipCountList2[j].gameObject.SetActive(true);
                        break;
                    }
                }
            }
        }
        _pageSetActiveGroup.SetActiveAll(_eqipCountList2.Find(x => x.gameObject.activeSelf) != null);
        _scrollSwipe.RefreshPages();
        UpdateSetEffect();
    }


    private void OnChangeSetEffect(int page)
    {
        bool isSlot2Active = _eqipCountList2.Find(x => x.gameObject.activeSelf) != null;

        if (page == 0 || (isSlot2Active && page == 1))
        {
            _setTitleText.text = _currentSetEffectType == SetEffectType.Furniture ? "적용 가구 목록" : "적용 기구 목록";
            return;
        }

        else if ((!isSlot2Active && page == 1) || (isSlot2Active && page == 2))
        {
            FoodType foodType = _currentSetEffectType == SetEffectType.Furniture ? UserInfo.GetEquipFurnitureFoodType(UserInfo.CurrentStage, _currentFloorType) : UserInfo.GetEquipKitchenUtensilFoodType(UserInfo.CurrentStage, _currentFloorType);
            Debug.Log(foodType);
            bool setEnabled = foodType != FoodType.None;
            _setTitleText.text = setEnabled ? Utility.FoodTypeStringConverter(foodType) + " 적용중" : "비활성화";
            _setDescriptionText.text = !setEnabled ? "적용중인 효과 없음" : _currentSetEffectType == SetEffectType.Furniture ? Utility.GetFurnitureFoodTypeSetEffectDescription(foodType) : Utility.GetKitchenFoodTypeSetEffectDescription(foodType);
        }
    }


    private void OnChangeSetEffectType(SetEffectType setEffectType)
    {
        _currentSetEffectType = setEffectType;

        if (_currentSetEffectType == SetEffectType.Furniture)
        {
            _furnitureRenderGroup.gameObject.SetActive(true);
            _kitchenUtensilRenderGroup.gameObject.SetActive(false);

        }
        else if (_currentSetEffectType == SetEffectType.KitchenUntensils)
        {
            _furnitureRenderGroup.gameObject.SetActive(false);
            _kitchenUtensilRenderGroup.gameObject.SetActive(true);
        }

        _scrollSwipe.ChangeIndex(0);
        OnChangeSetEffect(0);
        UpdateSlot();
    }


    private void OnChangeSetEffectType(int dir)
    {
        int currentIndex = (int)_currentSetEffectType;
        int maxIndex = (int)SetEffectType.Length;
        int newIndex = ((currentIndex + dir) % maxIndex + maxIndex) % maxIndex;

        _currentSetEffectType = (SetEffectType)newIndex;
        OnChangeSetEffectType(_currentSetEffectType);
    }


    private void OnChangeFloorType(ERestaurantFloorType floorType)
    {
        _currentFloorType = floorType;

        // Set the camera height based on the current floor type
        if (_furnitureCamera != null)
        {
            _furnitureCamera.transform.position = new Vector3(_furnitureCamera.transform.position.x, _floorHeights[(int)_currentFloorType], _furnitureCamera.transform.position.z);
        }

        if (_kitchenUtensilCamera != null)
        {
            _kitchenUtensilCamera.transform.position = new Vector3(_kitchenUtensilCamera.transform.position.x, _floorHeights[(int)_currentFloorType], _kitchenUtensilCamera.transform.position.z);
        }
    }


    private void UpdateSetEffect()
    {
        FoodType foodType = _currentSetEffectType == SetEffectType.Furniture ? UserInfo.GetEquipFurnitureFoodType(UserInfo.CurrentStage, _currentFloorType) : UserInfo.GetEquipKitchenUtensilFoodType(UserInfo.CurrentStage, _currentFloorType);
        bool setEnabled = foodType != FoodType.None;
        _setDescriptionText.text = !setEnabled ? "적용중인 효과 없음" : _currentSetEffectType == SetEffectType.Furniture ? Utility.GetFurnitureFoodTypeSetEffectDescription(foodType) : Utility.GetKitchenFoodTypeSetEffectDescription(foodType);
    }
}
