using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Read-only view of the grade already calculated for this popup. It never settles a transaction.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreCurrentGradeHud : MonoBehaviour
{
    [SerializeField] private PopupStoreGradeSession _gradeSession;
    [SerializeField] private PopupStoreScore _score;
    [SerializeField] private PopupStoreBusinessClock _clock;
    [SerializeField] private Image _rewardImage;
    [SerializeField] private TMP_Text _nextGradeText;
    [SerializeField] private TMP_Text _phaseProgressText;
    [SerializeField] private bool _showSecuredGradeLabel;
    [SerializeField] private Sprite _cSprite, _bSprite, _aSprite, _sSprite, _ssSprite, _sssSprite;
    [SerializeField] private bool _showImage = true;
    [SerializeField] private bool _showNextGrade = true;
    [SerializeField] private string _nextGradeFormat = "다음 {0}까지 {1}점";
    [SerializeField] private string _highestGradeText = "최고 등급";
    [SerializeField] private string _noneText = "보상 없음";

    private PopupStoreGradeSession _boundGrade;
    private PopupStoreScore _boundScore;
    private PopupStoreBusinessClock _boundClock;
    private Image _displayedImage;
    private TMP_Text _displayedText;
    private string _ownerSessionId;
    private bool _dirty;
    private bool _viewValidated, _mappingChecked;
    private Image _validatedImage;
    private TMP_Text _validatedText;
    private Transform _validatedParent;
    private PopupStorePhasePreview _boundPreview;
    private PopupStoreSessionLifetime _lifetime;
    private UIPopupStore _validatedPopup;
    private readonly Sprite[] _mappedSprites = new Sprite[6];
    private string _mappingError;
    private static readonly string[] ApprovedNames =
    {
        "PopupRewardBox_C_APPROVED", "PopupRewardBox_B_APPROVED", "PopupRewardBox_A_APPROVED",
        "PopupRewardBox_S_APPROVED", "PopupRewardBox_SS_APPROVED", "PopupRewardBox_SSS_APPROVED"
    };
    private PopupStorePreviewTextStyle _editorNextGradeStyle;
    private TMP_Text _editorPhaseProgressText;
    internal TMP_Text EditorPhaseProgressText => _editorPhaseProgressText;

    public Image RewardImage => _rewardImage;
    public TMP_Text NextGradeText => _nextGradeText;
    public PopupStoreGradeTier DisplayedGrade { get; private set; }
    public Sprite DisplayedSprite => IsVisible ? _displayedImage.sprite : null;
    public bool IsVisible => _displayedImage != null && _displayedImage.enabled
        && _displayedImage.gameObject.activeInHierarchy;
    public string LastError { get; private set; }
#if UNITY_EDITOR
    internal int DisplayRefreshCount { get; private set; }
    internal int MappingValidationCount { get; private set; }
    internal int BindingValidationCount { get; private set; }
#endif

    public bool ValidateMapping(out string error)
    {
        if (!MappingChanged()) { error = _mappingError; return error == null; }
#if UNITY_EDITOR
        MappingValidationCount++;
#endif
        _mappingChecked = true;
        for (int i = 0; i < _mappedSprites.Length; i++)
            _mappedSprites[i] = SpriteFor((PopupStoreGradeTier)(i + 1));
        _mappingError = null;
        for (int i = 0; i < _mappedSprites.Length; i++)
        {
            var sprite = _mappedSprites[i];
            var grade = (PopupStoreGradeTier)(i + 1);
            if (sprite == null || sprite.texture == null || sprite.rect.width <= 0 || sprite.rect.height <= 0
                || !string.Equals(sprite.name, ApprovedNames[i], StringComparison.Ordinal))
            { _mappingError = "Missing or incorrect approved current-grade sprite for " + grade + "."; break; }
            for (int j = 0; j < i; j++)
                if (_mappedSprites[j] == sprite || _mappedSprites[j].texture == sprite.texture)
                { _mappingError = "Current-grade sprites must be six distinct approved images."; break; }
            if (_mappingError != null) break;
        }
        error = _mappingError;
        return error == null;
    }

    private bool MappingChanged()
    {
        if (!_mappingChecked) return true;
        for (int i = 0; i < _mappedSprites.Length; i++)
        {
            var sprite = SpriteFor((PopupStoreGradeTier)(i + 1));
            if (_mappedSprites[i] != sprite || (sprite == null && _mappingError == null)) return true;
        }
        return false;
    }

    private bool BindingChanged() => !_viewValidated || _boundGrade == null || _boundScore == null
        || _boundClock == null || _rewardImage == null || _boundGrade != _gradeSession
        || _boundScore != _score || _boundClock != _clock || _validatedImage != _rewardImage
        || _validatedText != _nextGradeText || _validatedParent != transform.parent
        || _boundPreview != (_clock.PhasePreviewAuthority as PopupStorePhasePreview)
        || !string.Equals(_ownerSessionId, _gradeSession.SessionId, StringComparison.Ordinal);

    internal void RefreshIfDirty()
    {
        if (_dirty || BindingChanged() || MappingChanged()) RefreshDisplay();
    }

    /// <summary>Reads Current only. Safe for view refreshes and Editor display diagnostics, never calls Grade.Refresh.</summary>
    public bool RefreshDisplay()
    {
#if UNITY_EDITOR
        DisplayRefreshCount++;
#endif
        _dirty = false;
        if (!isActiveAndEnabled) { HideDisplay(); return false; }
        if ((BindingChanged() || !BindingsOwned()) && !BindView(out string bindingError)) return Reject(bindingError);
        if (!ValidateMapping(out string error)) return Reject(error);
        var preview = _boundPreview;
        if (preview != null)
        {
            if (_displayedImage != _rewardImage || _displayedText != _nextGradeText) HideDisplay();
            _displayedImage = _rewardImage; _displayedText = _nextGradeText;
            DisplayedGrade = preview.SecuredGrade;
            _displayedImage.sprite = SpriteFor(DisplayedGrade);
            _displayedImage.enabled = _showImage && _displayedImage.sprite != null;
            _displayedImage.preserveAspect = true; _displayedImage.raycastTarget = false;
            if (_displayedText != null)
            {
                if (_editorNextGradeStyle == null)
                {
                    _editorNextGradeStyle = new PopupStorePreviewTextStyle(_displayedText);
                    PopupStorePreviewTextStyle.Apply(_displayedText, 24, 22);
                }
                _displayedText.enabled = _showNextGrade && _showSecuredGradeLabel; _displayedText.raycastTarget = false;
                _displayedText.text = "확보 " + (DisplayedGrade == PopupStoreGradeTier.None ? "없음" : DisplayedGrade + "상자");
                RenderEditorPhaseProgress(preview);
            }
            LastError = null;
            return true;
        }
        HideEditorPhaseTypography();
        var current = _gradeSession.Current;
        bool hasGrade = current.IsValid && current.SuccessfulOrders > 0
            && current.Grade >= PopupStoreGradeTier.C && current.Grade <= PopupStoreGradeTier.SSS
            && _clock.State != PopupStoreBusinessState.NotStarted
            && _clock.State != PopupStoreBusinessState.Abandoned
            && _lifetime?.IsRetired != true;
        string nextLabel;
        try
        {
            nextLabel = !hasGrade ? _noneText : current.Grade == PopupStoreGradeTier.SSS ? _highestGradeText
                : string.Format(CultureInfo.InvariantCulture, _nextGradeFormat,
                    (PopupStoreGradeTier)((int)current.Grade + 1), Math.Max(0, current.NextTargetScore - current.Score));
        }
        catch (FormatException exception) { return Reject("Invalid next-grade display format: " + exception.Message); }

        if (_displayedImage != _rewardImage || _displayedText != _nextGradeText) HideDisplay();
        _displayedImage = _rewardImage;
        _displayedText = _nextGradeText;
        _displayedImage.raycastTarget = false;
        _displayedImage.preserveAspect = true;
        _displayedImage.sprite = hasGrade && _showImage ? SpriteFor(current.Grade) : null;
        _displayedImage.enabled = hasGrade && _showImage;
        DisplayedGrade = hasGrade ? current.Grade : PopupStoreGradeTier.None;
        if (_displayedText != null)
        {
            _displayedText.raycastTarget = false;
            _displayedText.enabled = _showNextGrade;
            if (_displayedText.text != nextLabel) _displayedText.text = nextLabel;
        }
        LastError = null;
        return true;
    }

    private bool BindView(out string error)
    {
#if UNITY_EDITOR
        BindingValidationCount++;
#endif
        error = null;
        var popup = GetComponentInParent<UIPopupStore>(true);
        if (popup == null || _gradeSession == null || _score == null || _clock == null || _rewardImage == null
            || _gradeSession.gameObject != popup.gameObject || _clock.gameObject != popup.gameObject
            || _score.GetComponentInParent<UIPopupStore>(true) != popup
            || _rewardImage.GetComponentInParent<UIPopupStore>(true) != popup
            || _rewardImage.gameObject == popup.gameObject
            || (_nextGradeText != null && _nextGradeText.GetComponentInParent<UIPopupStore>(true) != popup))
            error = "Current-grade sources and dedicated graphics must belong to this popup.";
        else if (IsResultChild(popup, _rewardImage) || IsResultChild(popup, _nextGradeText)
            || popup.GetComponent<PopupStoreRewardBox>()?.BoxImage == _rewardImage
            || popup.GetComponent<PopupStoreResultPanel>()?.ContainsResultGraphic(_rewardImage) == true)
            error = "The current-grade HUD cannot reuse a result-panel graphic.";
        else if (_ownerSessionId != null && !string.Equals(_ownerSessionId, _gradeSession.SessionId, StringComparison.Ordinal))
            error = "The current-grade HUD cannot be rebound to another business session.";
        if (error != null) { Unbind(); return false; }
        if (_ownerSessionId == null) _ownerSessionId = _gradeSession.SessionId;
        Unbind();
        _boundGrade = _gradeSession;
        _boundScore = _score;
        _boundClock = _clock;
        _boundScore.Changed += MarkDirty;
        _boundClock.StateChanged += MarkDirty;
        _boundGrade.Finalized += OnFinalized;
        _boundPreview = _clock.PhasePreviewAuthority as PopupStorePhasePreview;
        if (_boundPreview != null) _boundPreview.HudChanged += MarkDirty;
        _lifetime = GetComponentInParent<PopupStoreSessionLifetime>(true);
        _validatedPopup = popup;
        _validatedImage = _rewardImage; _validatedText = _nextGradeText;
        _validatedParent = transform.parent;
        _viewValidated = true;
        return true;
    }

    private bool BindingsOwned() => _validatedPopup != null && transform.IsChildOf(_validatedPopup.transform)
        && _gradeSession.gameObject == _validatedPopup.gameObject && _clock.gameObject == _validatedPopup.gameObject
        && _score.transform.IsChildOf(_validatedPopup.transform)
        && _rewardImage.transform.IsChildOf(_validatedPopup.transform)
        && (_nextGradeText == null || _nextGradeText.transform.IsChildOf(_validatedPopup.transform));

    private static bool IsResultChild(UIPopupStore popup, Component graphic)
    {
        // The existing authored result box lives directly in the result panel. Reject that complete
        // subtree, including its frame and confirm graphic, not only the optional result-image slot.
        var resultImage = popup.GetComponent<PopupStoreRewardBox>()?.BoxImage;
        var resultPanel = resultImage != null ? resultImage.transform.parent : null;
        return graphic != null && resultPanel != null && resultPanel != popup.transform
            && graphic.transform.IsChildOf(resultPanel);
    }

    internal Sprite SpriteFor(PopupStoreGradeTier grade)
    {
        switch (grade)
        {
            case PopupStoreGradeTier.C: return _cSprite;
            case PopupStoreGradeTier.B: return _bSprite;
            case PopupStoreGradeTier.A: return _aSprite;
            case PopupStoreGradeTier.S: return _sSprite;
            case PopupStoreGradeTier.SS: return _ssSprite;
            case PopupStoreGradeTier.SSS: return _sssSprite;
            default: return null;
        }
    }

    private void HideDisplay()
    {
        HideEditorPhaseTypography();
        if (_displayedImage != null) { _displayedImage.enabled = false; _displayedImage.sprite = null; }
        if (_displayedText != null) _displayedText.enabled = false;
        _displayedImage = null;
        _displayedText = null;
        DisplayedGrade = PopupStoreGradeTier.None;
    }

    private void RenderEditorPhaseProgress(PopupStorePhasePreview preview)
    {
        if (_editorPhaseProgressText == null && _phaseProgressText != null)
            _editorPhaseProgressText = _phaseProgressText;
        if (_editorPhaseProgressText == null)
        {
            // The authored stage TMP is inactive and belongs to an older layout. Keep every
            // authored rectangle intact; add only a memory label within the existing track.
            var track = _nextGradeText.transform.parent.Find("Grade Progress Track") as RectTransform;
            if (track == null) return;
            var label = new GameObject("Editor Phase Progress Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.hideFlags = HideFlags.DontSave;
            var rect = (RectTransform)label.transform;
            rect.SetParent(track, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 2); rect.offsetMax = new Vector2(-8, -2);
            _editorPhaseProgressText = label.GetComponent<TextMeshProUGUI>();
            _editorPhaseProgressText.font = _nextGradeText.font;
            _editorPhaseProgressText.fontSharedMaterial = _nextGradeText.fontSharedMaterial;
            _editorPhaseProgressText.color = _nextGradeText.color;
            _editorPhaseProgressText.raycastTarget = false;
            _editorPhaseProgressText.alignment = TextAlignmentOptions.Center;
            _editorPhaseProgressText.margin = Vector4.zero;
            PopupStorePreviewTextStyle.Apply(_editorPhaseProgressText, 22, 20);
            rect.SetAsLastSibling();
        }
        var phase = preview.Session;
        _editorPhaseProgressText.text = phase != null && phase.State == PopupStorePhaseState.Playing
            ? preview.GoalText
            : phase != null && phase.State == PopupStorePhaseState.Countdown
                ? "다음 단계 · " + phase.CountdownValue : preview.TransitionText;
        _editorPhaseProgressText.enabled = _showNextGrade;
    }

    private void HideEditorPhaseTypography()
    {
        _editorNextGradeStyle?.Restore();
        _editorNextGradeStyle = null;
        if (_editorPhaseProgressText != null) _editorPhaseProgressText.enabled = false;
    }

    private bool Reject(string error)
    {
        HideDisplay();
        if (LastError != error) Debug.LogError("PopupStoreCurrentGradeHud: " + error, this);
        LastError = error;
        return false;
    }

    private void MarkDirty() => _dirty = true;
    private void OnFinalized(PopupStoreGradeResult _) => MarkDirty();
    private void Unbind()
    {
        _viewValidated = false;
        if (_boundScore != null) _boundScore.Changed -= MarkDirty;
        if (_boundClock != null) _boundClock.StateChanged -= MarkDirty;
        if (_boundGrade != null) _boundGrade.Finalized -= OnFinalized;
        if (_boundPreview != null) _boundPreview.HudChanged -= MarkDirty;
        _boundPreview = null;
        _boundScore = null;
        _boundClock = null;
        _boundGrade = null;
    }
    private void OnEnable() { _dirty = true; _mappingChecked = false; RefreshDisplay(); }
    // Score observers are synchronous; defer reading until every observer has completed the grade calculation.
    // Idle frames perform no refresh and this presenter never invokes accounting or reward APIs.
    private void LateUpdate() { if (_boundPreview == null || !_boundPreview.isActiveAndEnabled) RefreshIfDirty(); }
    private void OnValidate() { _viewValidated = false; _mappingChecked = false; MarkDirty(); }
    private void OnTransformParentChanged() { _viewValidated = false; MarkDirty(); }
    private void OnDisable() { Unbind(); HideDisplay(); _dirty = false; }
    private void OnDestroy() { Unbind(); HideDisplay(); }
}

/// <summary>Memory-only typography overrides, preserving authored fonts and RectTransforms.</summary>
internal sealed class PopupStorePreviewTextStyle
{
    private readonly TMP_Text _text;
    private readonly float _size, _min, _max;
    private readonly bool _auto;
    private readonly TextWrappingModes _wrapping;
    private readonly TextOverflowModes _overflow;

    internal PopupStorePreviewTextStyle(TMP_Text text)
    {
        _text = text; _size = text.fontSize; _min = text.fontSizeMin; _max = text.fontSizeMax;
        _auto = text.enableAutoSizing; _wrapping = text.textWrappingMode; _overflow = text.overflowMode;
    }

    internal static void Apply(TMP_Text text, float maximum, float minimum)
    {
        text.fontSize = maximum; text.fontSizeMax = maximum; text.fontSizeMin = minimum;
        text.enableAutoSizing = true; text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    internal void Restore()
    {
        if (_text == null) return;
        _text.fontSize = _size; _text.fontSizeMin = _min; _text.fontSizeMax = _max;
        _text.enableAutoSizing = _auto; _text.textWrappingMode = _wrapping; _text.overflowMode = _overflow;
    }
}
