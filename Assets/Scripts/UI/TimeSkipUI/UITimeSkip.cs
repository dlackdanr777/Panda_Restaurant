using Muks.MobileUI;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UITimeSkip : MobileUIView
{
    [Header("Components")]
    [SerializeField] private GameObject _dontTouchArea;
    [SerializeField] private Button _okButton;

    [Space]
    [Header("Animations")]
    [SerializeField] private GameObject _animeUI;
    [SerializeField] private float _showDuration;
    [SerializeField] private Ease _showTweenMode;

    [Space]
    [SerializeField] private float _hideDuration;
    [SerializeField] private Ease _hideTweenMode;


    private string _timeSkipId;

    public override void Init()
    {
        _okButton.onClick.AddListener(OnOkButtonClicked);
        gameObject.SetActive(false);
    }

    public override void Show()
    {
        VisibleState = VisibleState.Appearing;

        gameObject.SetActive(true);
        _animeUI.SetActive(true);
        _dontTouchArea.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        TweenData tween2 = _animeUI.TweenScale(new Vector3(1, 1, 1), _showDuration, _showTweenMode);
        tween2.OnComplete(() =>
        {
            VisibleState = VisibleState.Appeared;
            gameObject.SetActive(true);
            _dontTouchArea.gameObject.SetActive(false);
        });
        
    }

    public override void Hide()
    {
        VisibleState = VisibleState.Disappearing;

        gameObject.SetActive(true);
        _animeUI.SetActive(true);
        _dontTouchArea.gameObject.SetActive(true);
        _animeUI.transform.localScale = new Vector3(1f, 1f, 1f);
        TweenData tween2 = _animeUI.TweenScale(new Vector3(0.3f, 0.3f, 0.3f), _hideDuration, _hideTweenMode);
        tween2.OnComplete(() =>
        {
            VisibleState = VisibleState.Disappeared;
            gameObject.SetActive(false);
            _dontTouchArea.gameObject.SetActive(false);
        });
    }

    public void ShowTimeSkipUI(string id)
    {
        _timeSkipId  = id;
        _uiNav.Push("UITimeSkip");
    }

    private void OnOkButtonClicked()
    {
        TimeManager.Instance.RemoveTime(_timeSkipId);
        //여기서 광고
        _uiNav.Pop("UITimeSkip");
    }
}
