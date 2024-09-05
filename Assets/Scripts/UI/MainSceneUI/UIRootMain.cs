using Muks.Tween;
using UnityEngine;

public class UIRootMain : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private CustomerController _customerController;

    [Header("Buttons")]
    [SerializeField] private FillAmountButton _addCustomerButton;


    private int _tabCount = 0;
    private Vector3 _addCustomerButtonTmpPos;
    private Vector3 _addCustomerButtonTmpScale;

    private void Awake()
    {
        _addCustomerButtonTmpPos = _addCustomerButton.transform.position;
        _addCustomerButtonTmpScale = _addCustomerButton.transform.localScale;
        _addCustomerButton.AddListener(OnAddCustomerButtonClicked);
    }


    private void OnAddCustomerButtonClicked()
    {

        if(GameManager.Instance.TotalTabCount - 1 <= _tabCount)
        {
            if(_customerController.IsMaxCount)
            {
                _addCustomerButton.TweenStop();
                _addCustomerButton.transform.position = _addCustomerButtonTmpPos;
                _addCustomerButton.transform.localScale = _addCustomerButtonTmpScale;
                _addCustomerButton.TweenMoveX(_addCustomerButtonTmpPos.x + 10, 0.05f);
                _addCustomerButton.TweenMoveX(_addCustomerButtonTmpPos.x - 10, 0.05f);
                _addCustomerButton.TweenMoveX(_addCustomerButtonTmpPos.x + 8, 0.03f);
                _addCustomerButton.TweenMoveX(_addCustomerButtonTmpPos.x - 7, 0.02f);
                _addCustomerButton.TweenMoveX(_addCustomerButtonTmpPos.x + 3, 0.02f);
                _addCustomerButton.TweenMoveX(_addCustomerButtonTmpPos.x, 0.1f);

                TimedDisplayManager.Instance.ShowText("ÁÙÀÌ ²ËÃ¡½À´Ï´Ù.");
                return;
            }

            _customerController.AddCustomer();
            _addCustomerButton.SetFillAmonut(0);
            _tabCount = 0;
            return;
        }

        _tabCount++;
        _addCustomerButton.SetFillAmonut((float)_tabCount / (GameManager.Instance.TotalTabCount - 1));
    }
}
