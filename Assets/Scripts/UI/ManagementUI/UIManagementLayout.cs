using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagementLayout : MonoBehaviour
{
    [Serializable]
    public struct LayoutGroupData
    {
        [SerializeField] private string _name;
        public string Name => _name;

        [SerializeField] private UIManagementLayoutGroup _layoutGroup;
        public UIManagementLayoutGroup LayoutGroup => _layoutGroup;
    }

    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _layoutTitle;
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;
    [SerializeField] private UIManagementLayoutGroup[] _layouts;
    [SerializeField] private LayoutGroupData[] _layoutGroups;


    private int _currentIndex = 0;
    private ERestaurantFloorType _currentFloor;

    public void Init()
    {
        _leftButton.onClick.AddListener(() => OnButtonClicked(-1));
        _rightButton.onClick.AddListener(() => OnButtonClicked(1));

        for (int i = 0; i < _layoutGroups.Length; i++)
        {
            _layoutGroups[i].LayoutGroup.Init();
        }
        SelectLayout();
    }


    public void ResetLayout(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
        _currentIndex = 0;
        SelectLayout();
    }

    public void UpdateLayout(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
        SelectLayout();
    }


    private void OnButtonClicked(int dir)
    {
        _currentIndex = (_currentIndex + dir + _layouts.Length) % _layouts.Length;
        SelectLayout();
    }
    

    private void SelectLayout()
    {
        for (int i = 0; i < _layoutGroups.Length; i++)
        {
            if (i == _currentIndex)
            {
                _layoutGroups[i].LayoutGroup.gameObject.SetActive(true);
                _layoutGroups[i].LayoutGroup.EnableLayout(_currentFloor);
                _layoutTitle.SetText(_layoutGroups[i].Name);    
            }
            else
            {
                _layoutGroups[i].LayoutGroup.gameObject.SetActive(false);
            }
        }
    }


}
