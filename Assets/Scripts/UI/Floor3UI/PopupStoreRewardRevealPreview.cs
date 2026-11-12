using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Editor-only presentation of an already decided local preview. Never decides or grants a reward.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreRewardRevealPreview : MonoBehaviour
{
    [SerializeField] private PopupStoreResultPanel _panel;
    [SerializeField] private PopupStoreRewardBox _box;
    [SerializeField] private PopupStoreRewardSession _reward;
    [SerializeField] private PopupStoreGradeSession _grade;
    [SerializeField] private Button _openButton;
    [SerializeField] private TMP_Text _valueText, _noticeText;
    [SerializeField, Range(.3f, .6f)] private float _duration = .45f;
    [SerializeField, Range(.01f, 1f)] private float _shrinkScale = .92f;
    [SerializeField, Range(0f, 1f)] private float _minimumAlpha = .65f;
    [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsReady { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool IsRevealed => OpenedResult != null;
    public bool IsPreviewVisible => IsRevealed && _valueText != null && _noticeText != null
        && _valueText.gameObject.activeInHierarchy && _noticeText.gameObject.activeInHierarchy;
    public int RevealCount { get; private set; }
    public PopupStoreRewardDecision ShownDecision { get; private set; }
    public PopupStoreGradeResult OpenedResult { get; private set; }
    public string LastError { get; private set; }
    public static bool IsSupported
    {
        get
        {
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

#if UNITY_EDITOR
    private PopupStoreGradeResult _observedResult;
    private Button _boundButton;
    private Image _animatedImage;
    private TMP_Text _shownValueText, _shownNoticeText;
    private Vector3 _shownScale;
    private Color _shownColor;
    private float _elapsed;
    private bool _textShown;

    public bool Refresh()
    {
        if (!Validate(out string error)) { Reject(error); return false; }
        if (_observedResult == null)
        { _observedResult = _panel.ShownResult; ShownDecision = _reward.Result; }
        if (_boundButton != _openButton)
        {
            Unbind(); _boundButton = _openButton;
            _boundButton.onClick.AddListener(OnClicked);
            PopupStoreSound.BindButton(_boundButton);
        }
        LastError = null;
        IsReady = !IsRevealed && !IsAnimating;
        _openButton.interactable = IsReady;
        _box.BoxImage.raycastTarget = IsReady;
        if (IsRevealed && !IsAnimating) ShowText();
        return true;
    }

    public bool TryReveal()
    {
        if (!Refresh() || !IsReady) return false;
        // Reserve before any UI/log callback. Closing mid-animation cannot grant a replay on reentry.
        OpenedResult = _observedResult;
        RevealCount++;
        IsReady = false; IsAnimating = true; _elapsed = 0;
        _openButton.interactable = false;
        _box.BoxImage.raycastTarget = false;
        _animatedImage = _box.BoxImage;
        _shownScale = _animatedImage.rectTransform.localScale;
        _shownColor = _animatedImage.color;
        PopupStoreSound.Play(this, PopupStoreSoundCue.RewardOpen, OpenedResult);
        Log("STARTED");
        return true;
    }

    public void TickPreview(float unscaledDelta)
    {
        if (!Finite(unscaledDelta) || unscaledDelta < 0)
        { Reject("Preview delta must be finite and non-negative."); return; }
        if (!Refresh() || !IsAnimating) return;
        _elapsed = Mathf.Min(_duration, _elapsed + unscaledDelta);
        float t = Mathf.Clamp01(_elapsed / _duration);
        float eased = _curve.Evaluate(t);
        if (!Finite(eased)) { Reject("Preview curve returned an invalid value."); return; }
        float pulse = 1f - Mathf.Abs(2f * Mathf.Clamp01(eased) - 1f);
        _animatedImage.rectTransform.localScale = _shownScale * Mathf.Lerp(1f, _shrinkScale, pulse);
        var color = _shownColor;
        color.a *= Mathf.Lerp(1f, _minimumAlpha, pulse);
        _animatedImage.color = color;
        if (t < 1f) return;
        RestoreAnimatedImage(); IsAnimating = false;
        ShowText(); Log("COMPLETED");
        if (ShownDecision.Diamonds > 0)
            PopupStoreSound.Play(this, PopupStoreSoundCue.DiamondsShown, OpenedResult);
    }

    private bool Validate(out string error)
    {
        error = null;
        if (!isActiveAndEnabled || !Local(_panel) || !Local(_box) || !Local(_reward) || !Local(_grade)
            || !OwnButton() || !OwnTexts())
            error = "Preview needs dedicated local result controls.";
        else if ((_shownValueText != null && _shownValueText != _valueText)
            || (_shownNoticeText != null && _shownNoticeText != _noticeText))
            error = "A visible preview cannot be rebound to different text controls.";
        else if (!_panel.CanRevealPreview || !_box.IsVisible || _box.IsAnimating)
            error = "Result and reward appearance must finish before preview input.";
        else if (!_grade.IsFinalized || !_reward.IsResolved || _reward.Result == null
            || _panel.ShownResult == null || !_panel.ShownResult.IsFinalized
            || !ReferenceEquals(_grade.FinalResult, _panel.ShownResult)
            || !ReferenceEquals(_box.ShownResult, _panel.ShownResult)
            || string.IsNullOrEmpty(_grade.SessionId)
            || _panel.ShownResult.SessionId != _grade.SessionId
            || _reward.ResolvedSessionId != _grade.SessionId || _reward.Result.SessionId != _grade.SessionId
            || _reward.Result.Grade != _panel.ShownResult.Grade
            || _reward.Result.Grade <= PopupStoreGradeTier.None || _reward.Result.Grade > PopupStoreGradeTier.SSS
            || _panel.ShownResult.SuccessfulOrders <= 0 || !_reward.Result.IsLocalPreview
            || _reward.Result.Diamonds < 0 || string.IsNullOrEmpty(_reward.Result.DecisionId)
            || (_reward.Result.PolicyId != PopupStoreRewardTable.LegacyPolicyId
                && _reward.Result.PolicyId != PopupStoreRewardTable.R1PolicyId)
            || !Enum.IsDefined(typeof(PopupStoreRewardKind), _reward.Result.Kind)
            || (_observedResult != null && (!ReferenceEquals(_observedResult, _panel.ShownResult)
                || !ReferenceEquals(ShownDecision, _reward.Result))))
            error = "Preview requires this original final result and its already decided local reward.";
        else if (!_box.ValidateMapping(out error)) { }
        else if (_box.DisplayedSprite == null
            || _box.DisplayedSprite.name != "PopupRewardBox_" + _panel.ShownResult.Grade + "_APPROVED")
            error = "Preview needs the approved image of this final grade.";
        else if (!Finite(_duration) || _duration < .3f || _duration > .6f
            || !Finite(_shrinkScale) || _shrinkScale <= 0 || _shrinkScale > 1
            || !Finite(_minimumAlpha) || _minimumAlpha < 0 || _minimumAlpha > 1
            || _curve == null || _curve.length < 2)
            error = "Preview duration, scale, opacity and curve must be finite and valid.";
        else
        {
            foreach (var key in _curve.keys)
                if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent)
                    || !Finite(key.inWeight) || !Finite(key.outWeight))
                { error = "Preview curve keys must be finite."; break; }
        }
        return error == null;
    }

    private void ShowText()
    {
        if (_textShown) return;
        string label = ShownDecision.PolicyId == PopupStoreRewardTable.R1PolicyId ? "R1 검수 보상 +" : "R0 테스트 보상 +";
        _valueText.text = label + ShownDecision.Diamonds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "D";
        _noticeText.text = "미지급 · 실제 계정에 반영되지 않음";
        _shownValueText = _valueText; _shownNoticeText = _noticeText;
        _valueText.gameObject.SetActive(true); _noticeText.gameObject.SetActive(true);
        _textShown = true;
        Log("VISIBLE");
    }

    private void RestoreAnimatedImage()
    {
        if (_animatedImage == null) return;
        // Box.OnDisable may already have restored the authored scale. Never overwrite that restoration.
        if (Local(_box) && _box.IsVisible && _box.BoxImage == _animatedImage)
            _animatedImage.rectTransform.localScale = _shownScale;
        _animatedImage.color = _shownColor;
        _animatedImage = null;
    }
    private void Unbind()
    {
        if (_boundButton != null)
        {
            _boundButton.onClick.RemoveListener(OnClicked); _boundButton.interactable = false;
            var image = _boundButton.targetGraphic as Image;
            var localPanel = GetComponent<PopupStoreResultPanel>();
            if (localPanel != null && localPanel.ContainsResultGraphic(image)) image.raycastTarget = false;
        }
        _boundButton = null;
    }
    private void Reject(string error)
    {
        HidePreview(); LastError = error;
        // A malformed policy must be visible without presenting a reward or touching result confirmation.
        if (!Local(_panel) || !Local(_grade) || !Local(_reward) || !OwnTexts()
            || !_panel.CanRevealPreview || !_grade.IsFinalized || _panel.ShownResult == null
            || !ReferenceEquals(_panel.ShownResult, _grade.FinalResult)
            || _panel.ShownResult.SuccessfulOrders <= 0 || _reward.Result != null
            || string.IsNullOrEmpty(_reward.LastError)) return;
        _valueText.text = "보상 정보 오류 · 미결정";
        _noticeText.text = "미지급 · 실제 계정에 반영되지 않음";
        _shownValueText = _valueText; _shownNoticeText = _noticeText;
        _valueText.gameObject.SetActive(true); _noticeText.gameObject.SetActive(true);
    }
    private void OnClicked() => TryReveal();
    private void Log(string action)
    {
        if (Application.isPlaying && ShownDecision != null)
            Debug.Log("POPUP_STORE_REWARD_REVEAL_" + action + " session=" + ShownDecision.SessionId
                + " decision=" + ShownDecision.DecisionId + " grade=" + ShownDecision.Grade
                + " policy=" + ShownDecision.PolicyId
                + " diamonds=" + ShownDecision.Diamonds + " reveals=" + RevealCount
                + " finalizations=" + _grade.FinalizationCount + " decisions=" + _reward.DecisionCount
                + " draws=" + _reward.RandomDrawCount + " score=" + OpenedResult.Score
                + " successful=" + OpenedResult.SuccessfulOrders + " unpaid=True", this);
    }
#else
    public bool Refresh() { DisablePreviewForPlayer(); return false; }
    public bool TryReveal() { DisablePreviewForPlayer(); return false; }
    public void TickPreview(float unscaledDelta) => DisablePreviewForPlayer();
#endif

    public void HidePreview()
    {
#if UNITY_EDITOR
        RestoreAnimatedImage(); Unbind(); _textShown = false;
        HideText(_shownValueText); HideText(_shownNoticeText);
        _shownValueText = null; _shownNoticeText = null;
#endif
        IsReady = false; IsAnimating = false;
        DisableOwnedControls();
    }

    private void DisablePreviewForPlayer() => HidePreview();
    private void DisableOwnedControls()
    {
        // Bad Inspector assignments must never erase a result label or disable its confirmation button.
        if (OwnButton()) { _openButton.interactable = false; _box.BoxImage.raycastTarget = false; }
        HideText(_valueText); HideText(_noticeText);
    }
    private void HideText(TMP_Text text)
    {
        var localPanel = GetComponent<PopupStoreResultPanel>();
        if (localPanel == null || !localPanel.ContainsPreviewText(text)) return;
        text.text = string.Empty; text.gameObject.SetActive(false);
    }
    private bool OwnButton() => Local(_box) && Local(_panel) && _box.BoxImage != null && _openButton != null
        && _panel.ContainsResultGraphic(_box.BoxImage) && _openButton.gameObject == _box.BoxImage.gameObject
        && _openButton.targetGraphic == _box.BoxImage && _openButton.onClick.GetPersistentEventCount() == 0;
    private bool OwnTexts() => Local(_panel) && _valueText != _noticeText
        && _panel.ContainsPreviewText(_valueText) && _panel.ContainsPreviewText(_noticeText);
    private bool Local(Component component) => component != null && component.gameObject == gameObject;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void OnEnable() => HidePreview();
    private void OnDisable() => HidePreview();
    private void OnDestroy() => HidePreview();
}
