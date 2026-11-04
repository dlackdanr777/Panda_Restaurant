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

    public Image RewardImage => _rewardImage;
    public TMP_Text NextGradeText => _nextGradeText;
    public PopupStoreGradeTier DisplayedGrade { get; private set; }
    public Sprite DisplayedSprite => IsVisible ? _displayedImage.sprite : null;
    public bool IsVisible => _displayedImage != null && _displayedImage.enabled
        && _displayedImage.gameObject.activeInHierarchy;
    public string LastError { get; private set; }

    public bool ValidateMapping(out string error)
    {
        var sprites = new[] { _cSprite, _bSprite, _aSprite, _sSprite, _ssSprite, _sssSprite };
        for (int i = 0; i < sprites.Length; i++)
        {
            var sprite = sprites[i];
            var grade = (PopupStoreGradeTier)(i + 1);
            if (sprite == null || sprite.texture == null || sprite.rect.width <= 0 || sprite.rect.height <= 0
                || !string.Equals(sprite.name, "PopupRewardBox_" + grade + "_APPROVED", StringComparison.Ordinal))
            { error = "Missing or incorrect approved current-grade sprite for " + grade + "."; return false; }
            for (int j = 0; j < i; j++)
                if (sprites[j] == sprite || sprites[j].texture == sprite.texture)
                { error = "Current-grade sprites must be six distinct approved images."; return false; }
        }
        error = null;
        return true;
    }

    /// <summary>Reads Current only. Safe for view refreshes and Editor display diagnostics, never calls Grade.Refresh.</summary>
    public bool RefreshDisplay()
    {
        _dirty = false;
        if (!isActiveAndEnabled) { HideDisplay(); return false; }
        if (!BindView(out string error) || !ValidateMapping(out error)) return Reject(error);
        var current = _gradeSession.Current;
        bool hasGrade = current.IsValid && current.SuccessfulOrders > 0
            && current.Grade >= PopupStoreGradeTier.C && current.Grade <= PopupStoreGradeTier.SSS
            && _clock.State != PopupStoreBusinessState.NotStarted
            && _clock.State != PopupStoreBusinessState.Abandoned
            && GetComponentInParent<PopupStoreSessionLifetime>(true)?.IsRetired != true;
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
        if (_boundGrade == _gradeSession && _boundScore == _score && _boundClock == _clock) return true;
        Unbind();
        _boundGrade = _gradeSession;
        _boundScore = _score;
        _boundClock = _clock;
        _boundScore.Changed += MarkDirty;
        _boundClock.StateChanged += MarkDirty;
        _boundGrade.Finalized += OnFinalized;
        return true;
    }

    private static bool IsResultChild(UIPopupStore popup, Component graphic)
    {
        // The existing authored result box lives directly in the result panel. Reject that complete
        // subtree, including its frame and confirm graphic, not only the optional result-image slot.
        var resultImage = popup.GetComponent<PopupStoreRewardBox>()?.BoxImage;
        var resultPanel = resultImage != null ? resultImage.transform.parent : null;
        return graphic != null && resultPanel != null && resultPanel != popup.transform
            && graphic.transform.IsChildOf(resultPanel);
    }

    private Sprite SpriteFor(PopupStoreGradeTier grade)
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
        if (_displayedImage != null) { _displayedImage.enabled = false; _displayedImage.sprite = null; }
        if (_displayedText != null) _displayedText.enabled = false;
        _displayedImage = null;
        _displayedText = null;
        DisplayedGrade = PopupStoreGradeTier.None;
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
        if (_boundScore != null) _boundScore.Changed -= MarkDirty;
        if (_boundClock != null) _boundClock.StateChanged -= MarkDirty;
        if (_boundGrade != null) _boundGrade.Finalized -= OnFinalized;
        _boundScore = null;
        _boundClock = null;
        _boundGrade = null;
    }
    private void OnEnable() { _dirty = true; RefreshDisplay(); }
    // Score observers are synchronous; defer reading until every observer has completed the grade calculation.
    // Idle frames perform no refresh and this presenter never invokes accounting or reward APIs.
    private void LateUpdate() { if (_dirty) RefreshDisplay(); }
    private void OnDisable() { Unbind(); HideDisplay(); _dirty = false; }
    private void OnDestroy() { Unbind(); HideDisplay(); }
}
