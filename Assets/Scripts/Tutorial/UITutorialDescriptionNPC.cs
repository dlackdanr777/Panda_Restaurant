using Muks.MobileUI;
using Muks.Tween;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UITutorialDescriptionNPC : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UITutorialSkip _tutorialSkip;
    [SerializeField] private Button _skipButton;
    [SerializeField] private UIImageAndText _descriptionText1;
    [SerializeField] private UIImageAndText _descriptionText2;
    [SerializeField] private Button _screenButton;
    [SerializeField] private GameObject[] _cursorObjs;

    private Coroutine _textCoroutine;
    private bool _isScreenClicked = false;

    private Action _onSkipOkButtonClicked;

    public override void Init()
    {
        _skipButton.onClick.AddListener(OnSkipButtonClicked);
        _descriptionText1.gameObject.SetActive(false);
        _descriptionText2.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        _isScreenClicked = false;
        _screenButton.onClick.AddListener(() => _isScreenClicked = true);
        gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
    }


    public override void Show()
    {
        _descriptionText1.gameObject.SetActive(false);
        _descriptionText2.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
        for (int i = 0, cnt = _cursorObjs.Length; i < cnt; ++i)
            _cursorObjs[i].gameObject.SetActive(false);
        _isScreenClicked = false;

        if (_textCoroutine != null)
            StopCoroutine(_textCoroutine);

        VisibleState = VisibleState.Appeared;
        gameObject.SetActive(true);
        _skipButton.gameObject.SetActive(true);
    }


    public void OnSkipOkButtonClicked(Action action)
    {
        _onSkipOkButtonClicked = action;
    }


    public override void Hide()
    {
        if (_textCoroutine != null)
            StopCoroutine(_textCoroutine);

        VisibleState = VisibleState.Disappeared;
        gameObject.SetActive(false);
        _skipButton.gameObject.SetActive(false);
    }


    public Coroutine ShowDescription1Text(string str)
    {
        _descriptionText1.gameObject.SetActive(true);
        _descriptionText2.gameObject.SetActive(false);
        if (_textCoroutine != null)
            StopCoroutine(_textCoroutine);
        _textCoroutine = StartCoroutine(ShowDescriptionTextRoutine(_descriptionText1, str, 0.05f));
        return _textCoroutine;
    }

    public Coroutine ShowDescription2Text(string str)
    {
        _descriptionText2.gameObject.SetActive(true);
        _descriptionText1.gameObject.SetActive(false);
        if (_textCoroutine != null)
            StopCoroutine(_textCoroutine);
        _textCoroutine = StartCoroutine(ShowDescriptionTextRoutine(_descriptionText2, str, 0.05f));
        return _textCoroutine;
    }


    private void OnSkipButtonClicked()
    {
        _tutorialSkip.ShowSkipUI(_onSkipOkButtonClicked);
    }


    private IEnumerator ShowDescriptionTextRoutine(UIImageAndText text, string str, float duration)
    {
        text.gameObject.SetActive(true);
        _screenButton.gameObject.SetActive(false);
        for(int i = 0, cnt = _cursorObjs.Length; i < cnt; ++i)
            _cursorObjs[i].gameObject.SetActive(false);

        text.Text.text = string.Empty;
        TweenData tween = Tween.Wait(0.2f, () => _screenButton.gameObject.SetActive(true));
        _isScreenClicked = false;

        yield return YieldCache.WaitForSeconds(duration);
        for(int i = 0, cnt = str.Length; i < cnt; ++i)
        {
            text.Text.text += str[i];
            yield return YieldCache.WaitForSeconds(duration);

            if(_isScreenClicked)
                break;
        }

        text.Text.text = str;
        _screenButton.gameObject.SetActive(false);
        _isScreenClicked = false;
        tween.Clear();
        yield return YieldCache.WaitForSeconds(0.2f);
        _screenButton.gameObject.SetActive(true);
        for (int i = 0, cnt = _cursorObjs.Length; i < cnt; ++i)
            _cursorObjs[i].gameObject.SetActive(true);

        while (!_isScreenClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        text.gameObject.SetActive(false);
        _screenButton.gameObject.SetActive(false);
    }
}
