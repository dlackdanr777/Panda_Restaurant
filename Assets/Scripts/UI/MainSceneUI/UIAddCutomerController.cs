using Muks.Tween;
using System;
using UnityEngine;

public class UIAddCutomerController : MonoBehaviour
{
    public event Action OnAddCustomerHandelr;

    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private CustomerController _customerController;

    [Space]
    [Header("Buttons")]
    [SerializeField] private FillAmountButton _addCustomerButton;
    [SerializeField] private UIMarketerImage _marketerImage;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _callSound;

    private int _tabCount = 0;
    private Vector3 _addCustomerButtonTmpPos;
    private Vector3 _addCustomerButtonTmpScale;

    private void Awake()
    {
        _marketerImage.Init();
        _addCustomerButtonTmpPos = _addCustomerButton.transform.position;
        _addCustomerButtonTmpScale = _addCustomerButton.transform.localScale;
        _addCustomerButton.AddListener(OnAddCustomerButtonClicked);
        _addCustomerButton.AddListener(_marketerImage.StartAnime);
    }


    private void OnAddCustomerButtonClicked()
    {
        SoundManager.Instance.PlayEffectAudio(_callSound);

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

                PopupManager.Instance.ShowDisplayText("ÁÙÀÌ ²ËÃ¡½À´Ï´Ù.");
                return;
            }

            _customerController.AddCustomer();
            _addCustomerButton.SetFillAmonut(0);
            _tabCount = 0;
            OnAddCustomerHandelr?.Invoke();
            return;
        }

        _tabCount++;

        float fillAmount = GameManager.Instance.TotalTabCount <= 0 ? 0 : 0.275f + ((float)_tabCount / (GameManager.Instance.TotalTabCount - 1)) * 0.725f;
        _addCustomerButton.SetFillAmonut(fillAmount);
    }
}
