using UnityEngine;
using UnityEngine.UI;

public class UIManagementLayout : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Button _leftButton;
    [SerializeField] private Button _rightButton;
    [SerializeField] private UIManagementLayoutGroup[] _layouts;


    private int _currentIndex = 0;
    private ERestaurantFloorType _currentFloor;

    public void Init()
    {
        _leftButton.onClick.AddListener(() => OnButtonClicked(-1));
        _rightButton.onClick.AddListener(() => OnButtonClicked(1));

        for (int i = 0; i < _layouts.Length; i++)
        {
            _layouts[i].Init();
        }
        SelectLayout();
    }


    public void ResetLayout(ERestaurantFloorType floor)
    {
        _currentFloor = floor;
        _currentIndex = 0;
        SelectLayout();
    }


    private void OnButtonClicked(int dir)
    {
        _currentIndex = (_currentIndex + dir + _layouts.Length) % _layouts.Length;
        SelectLayout();
    }
    

    private void SelectLayout()
    {
        for (int i = 0; i < _layouts.Length; i++)
        {
            if (i == _currentIndex)
            {
                _layouts[i].gameObject.SetActive(true);
                _layouts[i].EnableLayout(_currentFloor);
            }
            else
            {
                _layouts[i].gameObject.SetActive(false);
            }
        }
    }


}
