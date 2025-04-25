using Muks.Tween;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UIAddCutomerController : MonoBehaviour
{
    public event Action OnAddCustomerHandelr;

    public Image MarketerSkillEffect => _marketerImage.MarketerSkillEffect;

    [Header("Components")]
    [SerializeField] private TableManager _tableManager;
    [SerializeField] private CustomerController _customerController;

    [Space]
    [Header("Buttons")]
    [SerializeField] private FillAmountButton _addCustomerButton;
    [SerializeField] private UIMarketerImage _marketerImage;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _callSound;

    private Vector3 _addCustomerButtonTmpPos;
    private Vector3 _addCustomerButtonTmpScale;

    private void Awake()
    {
        _marketerImage.Init();
        _addCustomerButtonTmpPos = _addCustomerButton.transform.position;
        _addCustomerButtonTmpScale = _addCustomerButton.transform.localScale;
        _addCustomerButton.AddListener(() => OnAddCustomerButtonClicked(true));

        _customerController.OnAddCustomerHandler += OnAddCustomerEvent;
        _customerController.OnMaxCustomerHandler += OnMaxCustomerEvent;
        _customerController.OnAddTabCountHandler += OnAddTabCountEvent;
    }


    public void OnAddCustomerButtonClicked(bool isSoundPlay)
    {
        _customerController.AddTabCount();

        if (isSoundPlay)
            _audioSource.PlayOneShot(_callSound);
    }

    private void OnMaxCustomerEvent()
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
    }

    private void OnAddCustomerEvent()
    {
        _addCustomerButton.SetFillAmonut(0);
        _marketerImage.StartAnime();
        OnAddCustomerHandelr?.Invoke();
    }

    private void OnAddTabCountEvent()
    {
        int tabCount = _customerController.TabCount;
        int beforeTabCount = tabCount - 1;
        float cntFillAmount = GameManager.Instance.TotalTabCount <= 0 ? 0 : 0.275f + (beforeTabCount <= 0 ? 0 : ((float)beforeTabCount / (GameManager.Instance.TotalTabCount - 1))) * 0.725f;
        _addCustomerButton.SetFillAmountNoAnime(cntFillAmount);
        float nextFillAmount = GameManager.Instance.TotalTabCount <= 0 ? 0 : 0.275f + ((float)tabCount / (GameManager.Instance.TotalTabCount - 1)) * 0.725f;
        _addCustomerButton.SetFillAmonut(nextFillAmount);
        _marketerImage.StartAnime();
    }
}
