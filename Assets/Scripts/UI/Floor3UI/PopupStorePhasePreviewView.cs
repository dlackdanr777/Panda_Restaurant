using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored, popup-local preview visuals. Its owner supplies every state and time sample.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePhasePreviewView : MonoBehaviour, ICanvasRaycastFilter
{
    [Serializable]
    private sealed class TutorialPage
    {
        public Sprite Screenshot;
        public string Title;
        [TextArea(2, 3)] public string Body;
        public bool ShowScreenshot = true;
        public Rect Crop = new Rect(0, 0, 1, 1);
        public bool ShowSecondCrop;
        public Rect SecondCrop = new Rect(0, 0, 1, 1);
        public GameObject Illustration;
        [Tooltip("Page-local offset for screenshots and composed examples; title and controls stay fixed.")]
        public Vector2 VisualOffset;
        public Vector2 ScreenshotPosition = new Vector2(0, 35);
        public Vector2 ScreenshotSize = new Vector2(1140, 380);
        public Vector2 BodyPosition = new Vector2(0, -225);
        [Min(1)] public float BodyFontSize = 40;
    }

    [Header("Tutorial pages — current gameplay captures, displayed crops only")]
    [SerializeField] private TutorialPage[] _pages;
    [SerializeField] private CanvasGroup _inputGroup;
    [SerializeField] private GameObject _tutorialGroup;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private TMP_Text _pageText;
    [SerializeField] private RawImage _screenshot;
    [SerializeField] private RawImage _secondScreenshot;
    [SerializeField] private AspectRatioFitter _screenshotAspect;
    [SerializeField] private AspectRatioFitter _secondScreenshotAspect;
    [SerializeField] private RectTransform _screenshotRegion;
    [SerializeField] private RectTransform _secondScreenshotRegion;
    [SerializeField] private Button _previousButton;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Image _ticketImage;
    [SerializeField] private TMP_Text _ticketStatusText;

    [Header("Final page — minigame ticket and play layout")]
    [SerializeField] private Vector2 _ticketPosition = new Vector2(-310, -300);
    [SerializeField] private Vector2 _ticketSize = new Vector2(170, 100);
    [SerializeField] private Vector2 _ticketStatusPosition = new Vector2(-80, -300);
    [SerializeField] private Vector2 _ticketStatusSize = new Vector2(230, 60);
    [SerializeField] private Vector2 _playPosition = new Vector2(265, -300);
    [SerializeField] private Vector2 _playSize = new Vector2(300, 103);

    [Header("Recipe minigame original presentation sprites")]
    [SerializeField] private GameObject _presentationGroup;
    [SerializeField] private Image _cueImage;
    [SerializeField] private TMP_Text _stageText;
    [SerializeField] private Sprite _three;
    [SerializeField] private Sprite _two;
    [SerializeField] private Sprite _one;
    [SerializeField] private Sprite _gameStart;
    [SerializeField] private Sprite _success;
    [SerializeField] private Sprite _gameOver;
    [SerializeField, Min(0.01f)] private float _stageNoticeSeconds = 1.5f;
    [SerializeField, Min(0.01f)] private float _gameStartSeconds = .7f;
    [SerializeField, Min(0.01f)] private float _gameOverSeconds = 1.1f;
    [SerializeField, Range(.5f, 1f)] private float _cueStartScale = .86f;

    private Func<bool> _play, _close;
    private RectTransform _originalExit;
    private bool _bound, _canPlay, _playRequested, _closeRequested, _invoking;
    private int _currentPage;
    private int _lastLoggedPage = -1;
    public int CurrentPage => _currentPage;
    public int PageCount => _pages?.Length ?? 0;
    public bool IsTutorialVisible => isActiveAndEnabled && _tutorialGroup != null && _tutorialGroup.activeInHierarchy;
    public bool IsCueVisible => isActiveAndEnabled && _presentationGroup != null && _presentationGroup.activeInHierarchy;
    public string LastCue { get; private set; }
    public int CurrentStage { get; private set; }
    public int PlayRequestCount { get; private set; }
    public int CloseRequestCount { get; private set; }
    public int CueShowCount { get; private set; }
    public float StageNoticeSeconds => _stageNoticeSeconds;
    public float GameStartSeconds => _gameStartSeconds;
    public float GameOverSeconds => _gameOverSeconds;

    public void Bind(Func<bool> play, Func<bool> close)
    {
        if (play == null || close == null) throw new ArgumentNullException(play == null ? nameof(play) : nameof(close));
        if (!ValidateConfiguration(out string error)) throw new InvalidOperationException(error);
        if (_bound)
        {
            if (_play != play || _close != close) throw new InvalidOperationException("A preview view cannot change its owner.");
            return;
        }
        _play = play; _close = close; _bound = true;
        _previousButton.onClick.AddListener(OnPrevious);
        _nextButton.onClick.AddListener(OnNext);
        _playButton.onClick.AddListener(OnPlay);
        _closeButton.onClick.AddListener(OnClose);
        PopupStoreSound.BindButton(_previousButton);
        PopupStoreSound.BindButton(_nextButton);
        PopupStoreSound.BindButton(_playButton);
        PopupStoreSound.BindButton(_closeButton, SoundEffectType.ButtonExitSound);
        HideAll();
    }

    public void RenderTutorial(bool visible, bool canPlay, string ticketStatus)
    {
        if (!_bound) return;
        if (!visible)
        {
            _tutorialGroup.SetActive(false);
            _inputGroup.blocksRaycasts = IsCueVisible;
            return;
        }
        bool wasVisible = IsTutorialVisible;
        _presentationGroup.SetActive(false); LastCue = null;
        _tutorialGroup.SetActive(true);
        _inputGroup.interactable = true; _inputGroup.blocksRaycasts = true;
        _canPlay = canPlay;
        if (!wasVisible && !_invoking) _closeRequested = false;
        _ticketStatusText.text = ticketStatus ?? string.Empty;
        RenderPage();
        PopupStoreSound.BeginRecipeBackground(this);
    }

    /// <summary>Account requests are asynchronous. Only release the local click latch once the host allows a retry.</summary>
    internal void RenderAccountAdmission(bool pending, bool canRetry, string status, bool confirmedTicketExhaustion = false)
    {
        if (!_bound || !IsTutorialVisible) return;
        _canPlay = canRetry && !pending;
        if (!_invoking && !pending) _playRequested = false;
        if (pending) _ticketStatusText.text = "시작 확인 중";
        // A confirmed 0/1 is ordinary exhaustion, not a missing approval response.
        else if (!confirmedTicketExhaustion && !canRetry && !string.IsNullOrEmpty(status)) _ticketStatusText.text = "시작 확인 필요";
        RenderButtons();
    }

    public bool TryPrevious()
    {
        if (!CanNavigate() || _currentPage == 0) return false;
        _currentPage--; RenderPage(); return true;
    }
    public bool TryNext()
    {
        if (!CanNavigate() || _currentPage >= PageCount - 1) return false;
        _currentPage++; RenderPage(); return true;
    }
    public bool TryPlay()
    {
        if (!CanNavigate() || !_canPlay || _currentPage != PageCount - 1) return false;
        _invoking = true; _playRequested = true; RenderButtons();
        bool accepted = false;
        try
        {
            accepted = _play.Invoke();
            if (accepted) PlayRequestCount++;
            return accepted;
        }
        finally { _invoking = false; if (!accepted) _playRequested = false; RenderButtons(); }
    }
    public bool TryClose()
    {
        if (!_bound || !IsTutorialVisible || _invoking || _closeRequested || _playRequested) return false;
        _invoking = true; _closeRequested = true; RenderButtons();
        bool accepted = false;
        try
        {
            accepted = _close.Invoke();
            if (accepted)
            {
                CloseRequestCount++;
                // Navigation has accepted this close. Explicitly retire the visible tutorial
                // so reentry starts a fresh visible interval even when EditMode does not run
                // MonoBehaviour.OnDisable. Keep its page and admission untouched.
                HideAll();
            }
            return accepted;
        }
        finally { _invoking = false; if (!accepted) _closeRequested = false; RenderButtons(); }
    }
    private bool CanNavigate() => _bound && IsTutorialVisible && !_invoking && !_closeRequested && !_playRequested;

    public void SetExitControl(RectTransform originalX)
    {
        var popup = GetComponentInParent<UIPopupStore>(true);
        if (originalX == null || popup == null || !originalX.IsChildOf(popup.transform) || originalX.IsChildOf(transform))
            throw new ArgumentException("Presentation exit must be the original popup's separate X control.", nameof(originalX));
        _originalExit = originalX;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        => !(IsCueVisible && _originalExit != null && _originalExit.gameObject.activeInHierarchy
            && RectTransformUtility.RectangleContainsScreenPoint(_originalExit, screenPoint, eventCamera));

    // Supported cues: Stage, 3, 2, 1, GameStart, Success, GameOver. Empty hides presentation only.
    public void RenderCue(string cue, int stage, float progress)
    {
        if (!_bound) return;
        if (string.IsNullOrEmpty(cue))
        {
            _presentationGroup.SetActive(false); LastCue = null;
            _inputGroup.blocksRaycasts = IsTutorialVisible;
            return;
        }
        if (float.IsNaN(progress) || float.IsInfinity(progress) || stage < 1 || stage > 6)
            throw new ArgumentOutOfRangeException(nameof(progress), "A preview cue requires finite progress and stage 1–6.");
        Sprite sprite;
        switch (cue)
        {
            case "Stage": sprite = null; break;
            case "3": sprite = _three; break;
            case "2": sprite = _two; break;
            case "1": sprite = _one; break;
            case "GameStart": sprite = _gameStart; break;
            case "Success": sprite = _success; break;
            case "GameOver": sprite = _gameOver; break;
            default: throw new ArgumentException("Unknown preview cue: " + cue, nameof(cue));
        }
        if (!IsCueVisible || LastCue != cue || CurrentStage != stage)
            CueShowCount++;
        LastCue = cue; CurrentStage = stage;
        _tutorialGroup.SetActive(false); _presentationGroup.SetActive(true);
        _inputGroup.interactable = true; _inputGroup.blocksRaycasts = true;
        _cueImage.sprite = sprite; _cueImage.gameObject.SetActive(sprite != null);
        _cueImage.preserveAspect = true;
        bool digit = cue == "1" || cue == "2" || cue == "3";
        _cueImage.rectTransform.sizeDelta = digit ? new Vector2(420, 430) : new Vector2(1250, 310);
        _stageText.gameObject.SetActive(cue == "Stage");
        _stageText.text = "STAGE " + stage;
        float t = Mathf.Clamp01(progress * 5f);
        float scale = Mathf.Lerp(_cueStartScale, 1f, t * t * (3f - 2f * t));
        _cueImage.rectTransform.localScale = Vector3.one * scale;
        _stageText.rectTransform.localScale = Vector3.one * scale;
        // The first Stage render occurs before the clock commits Running. Repeated
        // renders may retry that unavailable context; the sound owner dispatches this
        // stable stage/cue identity at most once and never advances the presentation.
        string marker = stage + ":" + cue;
        switch (cue)
        {
            case "Stage": PopupStoreSound.Play(this, PopupStoreSoundCue.StageNotice, this, marker); break;
            case "3": case "2": case "1":
                PopupStoreSound.Play(this, PopupStoreSoundCue.Countdown, this, marker); break;
            case "GameStart": PopupStoreSound.Play(this, PopupStoreSoundCue.GameStart, this, marker); break;
            case "Success": PopupStoreSound.Play(this, PopupStoreSoundCue.StageSuccess, this, marker); break;
            case "GameOver": PopupStoreSound.Play(this, PopupStoreSoundCue.GameOver, this, marker); break;
        }
    }

    public void HideAll()
    {
        if (_tutorialGroup != null) _tutorialGroup.SetActive(false);
        if (_presentationGroup != null) _presentationGroup.SetActive(false);
        if (_inputGroup != null) { _inputGroup.interactable = false; _inputGroup.blocksRaycasts = false; }
        LastCue = null;
    }

    private void RenderPage()
    {
        var page = _pages[_currentPage];
        if (Application.isPlaying && _lastLoggedPage != _currentPage)
        {
            _lastLoggedPage = _currentPage;
            Debug.Log("POPUP_STORE_PHASE_TUTORIAL page=" + (_currentPage + 1), this);
        }
        _titleText.text = page.Title; _bodyText.text = page.Body;
        _pageText.text = (_currentPage + 1) + " / " + PageCount;
        foreach (var candidate in _pages)
            if (candidate.Illustration != null)
            {
                ((RectTransform)candidate.Illustration.transform).anchoredPosition = candidate.VisualOffset;
                candidate.Illustration.SetActive(ReferenceEquals(page, candidate));
            }
        _screenshotRegion.gameObject.SetActive(page.ShowScreenshot);
        _secondScreenshotRegion.gameObject.SetActive(page.ShowScreenshot && page.ShowSecondCrop);
        if (page.ShowScreenshot)
        {
            _screenshotRegion.anchoredPosition = page.ScreenshotPosition + page.VisualOffset;
            _screenshotRegion.sizeDelta = page.ScreenshotSize;
            ApplyScreenshot(_screenshot, _screenshotAspect, page.Screenshot, page.Crop);
            if (page.ShowSecondCrop) ApplyScreenshot(_secondScreenshot, _secondScreenshotAspect, page.Screenshot, page.SecondCrop);
        }
        bool last = _currentPage == PageCount - 1;
        _bodyText.rectTransform.anchoredPosition = page.BodyPosition;
        _bodyText.fontSize = page.BodyFontSize;
        SetRect(_ticketImage.rectTransform, _ticketPosition, _ticketSize);
        SetRect(_ticketStatusText.rectTransform, _ticketStatusPosition, _ticketStatusSize);
        SetRect(_playButton.GetComponent<RectTransform>(), _playPosition, _playSize);
        _ticketImage.gameObject.SetActive(last); _ticketStatusText.gameObject.SetActive(last);
        RenderButtons();
    }
    private static void SetRect(RectTransform target, Vector2 position, Vector2 size)
    { target.anchoredPosition = position; target.sizeDelta = size; }
    private void RenderButtons()
    {
        bool canNavigate = CanNavigate();
        _previousButton.gameObject.SetActive(_currentPage > 0);
        _nextButton.gameObject.SetActive(_currentPage < PageCount - 1);
        _previousButton.interactable = canNavigate; _nextButton.interactable = canNavigate;
        _playButton.gameObject.SetActive(_currentPage == PageCount - 1);
        _playButton.interactable = canNavigate && _canPlay;
        _closeButton.interactable = !_closeRequested && !_playRequested;
    }
    private static void ApplyScreenshot(RawImage image, AspectRatioFitter aspect, Sprite sprite, Rect crop)
    {
        var rect = sprite.rect; var texture = sprite.texture;
        image.texture = texture;
        image.uvRect = new Rect((rect.x + crop.x * rect.width) / texture.width,
            (rect.y + crop.y * rect.height) / texture.height,
            crop.width * rect.width / texture.width, crop.height * rect.height / texture.height);
        aspect.aspectRatio = rect.width * crop.width / (rect.height * crop.height);
    }

    public bool ValidateConfiguration(out string error)
    {
        error = null;
        if (_pages == null || _pages.Length != 5 || _inputGroup == null || _inputGroup.gameObject != gameObject
            || !_inputGroup.ignoreParentGroups || !Owned(_tutorialGroup) || !Owned(_presentationGroup)
            || _tutorialGroup == _presentationGroup || !Owned(_titleText) || !Owned(_bodyText) || !Owned(_pageText)
            || !Owned(_screenshot) || !Owned(_secondScreenshot) || !Owned(_screenshotAspect) || !Owned(_secondScreenshotAspect)
            || !Owned(_screenshotRegion) || !Owned(_secondScreenshotRegion) || !Owned(_ticketImage)
            || _ticketImage.sprite == null || !Owned(_ticketStatusText) || !Owned(_cueImage) || !Owned(_stageText)
            || !ValidButton(_previousButton) || !ValidButton(_nextButton) || !ValidButton(_playButton) || !ValidButton(_closeButton)
            || _three == null || _two == null || _one == null || _gameStart == null || _success == null || _gameOver == null
            || !Positive(_stageNoticeSeconds) || !Positive(_gameStartSeconds) || !Positive(_gameOverSeconds)
            || float.IsNaN(_cueStartScale) || _cueStartScale < .5f || _cueStartScale > 1f
            || !Finite(_ticketPosition) || !ValidSize(_ticketSize)
            || !Finite(_ticketStatusPosition) || !ValidSize(_ticketStatusSize)
            || !Finite(_playPosition) || !ValidSize(_playSize))
        { error = "The authored phase preview view has a missing, foreign or invalid reference."; return false; }
        var buttons = new[] { _previousButton, _nextButton, _playButton, _closeButton };
        for (int i = 0; i < buttons.Length; i++)
            for (int j = i + 1; j < buttons.Length; j++)
                if (buttons[i] == buttons[j]) { error = "Tutorial actions require distinct buttons."; return false; }
        foreach (var page in _pages)
            if (page == null || page.Screenshot == null || string.IsNullOrWhiteSpace(page.Title)
                || string.IsNullOrWhiteSpace(page.Body) || !ValidCrop(page.Crop)
                || (page.ShowSecondCrop && !ValidCrop(page.SecondCrop))
                || (page.Illustration != null && (!Owned(page.Illustration) || !(page.Illustration.transform is RectTransform)
                    || !page.Illustration.transform.IsChildOf(_tutorialGroup.transform)))
                || (!page.ShowScreenshot && page.Illustration == null)
                || !Finite(page.VisualOffset) || !Finite(page.ScreenshotPosition) || !Finite(page.ScreenshotSize)
                || page.ScreenshotSize.x <= 0 || page.ScreenshotSize.y <= 0
                || !Finite(page.BodyPosition) || !Positive(page.BodyFontSize))
            { error = "Each tutorial page requires its current gameplay sprite, text and valid displayed crop."; return false; }
        for (int i = 0; i < _pages.Length; i++)
        {
            var illustration = _pages[i].Illustration;
            if (illustration == null) continue;
            if (illustration.GetComponentsInChildren<Button>(true).Length != 0)
            { error = "Tutorial illustrations cannot contain interactive gameplay controls."; return false; }
            for (int j = i + 1; j < _pages.Length; j++)
                if (illustration == _pages[j].Illustration)
                { error = "Each tutorial illustration must belong to one page."; return false; }
        }
        return true;
    }
    private static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    private static bool ValidSize(Vector2 value) => Finite(value) && value.x > 0 && value.y > 0;
    private bool Owned(Component component) => component != null && component.transform.IsChildOf(transform);
    private bool Owned(GameObject obj) => obj != null && obj != gameObject && obj.transform.IsChildOf(transform);
    private bool ValidButton(Button button) => Owned(button) && button.onClick.GetPersistentEventCount() == 0;
    private static bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
    private static bool ValidCrop(Rect crop) => Positive(crop.width) && Positive(crop.height)
        && !float.IsNaN(crop.x) && !float.IsNaN(crop.y) && crop.x >= 0 && crop.y >= 0
        && crop.xMax <= 1 && crop.yMax <= 1;
    private void OnPrevious() => TryPrevious();
    private void OnNext() => TryNext();
    private void OnPlay() => TryPlay();
    private void OnClose() => TryClose();
    private void OnDisable() => HideAll();
    private void OnDestroy()
    {
        if (_previousButton != null) _previousButton.onClick.RemoveListener(OnPrevious);
        if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNext);
        if (_playButton != null) _playButton.onClick.RemoveListener(OnPlay);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(OnClose);
        _play = null; _close = null;
    }
}
