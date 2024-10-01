using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GatechrasherSpawnButton : MonoBehaviour
{
    [SerializeField] private CustomerController _customerController;
    [SerializeField] private string _testId;
    private Button _button;
    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnButtonClickEvent);
    }

    private void OnButtonClickEvent()
    {
        _customerController.GatecrasherTest(_testId);
    }
}
