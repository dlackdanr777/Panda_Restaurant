using System;
using System.Globalization;
using Muks.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed partial class PopupStoreResultPanel
{
    [Header("Editor phase summary — layout only")]
    [SerializeField] private Vector2 _phaseSummaryBoxPosition = new Vector2(-210, -20);
    [SerializeField] private Vector2 _phaseSummaryBoxSize = new Vector2(300, 300);
    [SerializeField] private Vector2 _phaseSummaryScorePosition = new Vector2(170, -20);
    [SerializeField] private Vector2 _phaseSummaryScoreSize = new Vector2(390, 110);
    [SerializeField] private Vector2 _phaseSummaryNoBoxScorePosition = new Vector2(0, -20);
    [SerializeField, Min(1)] private float _phaseSummaryScoreFontSize = 44;
    [SerializeField, Min(1)] private float _phaseSummaryMinimumScoreFontSize = 30;
    [Header("Abandoned result — presentation only")]
    [SerializeField] private Vector2 _abandonedPanelSize = new Vector2(840, 380);
    [SerializeField] private Vector2 _abandonedTitlePosition = Vector2.zero;
    [SerializeField] private Vector2 _abandonedTitleSize = new Vector2(720, 90);
    [SerializeField] private Vector2 _abandonedButtonPosition = new Vector2(0, -175);
    [SerializeField] private Vector2 _abandonedButtonSize = new Vector2(260, 90);
    [SerializeField, Min(1)] private float _abandonedTitleFontSize = 60;
    [SerializeField] private Image _abandonedInterior;
    [SerializeField] private string _abandonedTitle = "영업 포기";
    [SerializeField] private string _abandonedButtonLabel = "나가기";
    [Tooltip("Original 2162 x 1505 artwork pixels: left, bottom, right, top. Scaled to the imported texture resolution without changing its asset.")]
    [SerializeField] private Vector4 _abandonedFrameBorder = new Vector4(460, 300, 150, 450);
    public PopupStorePhaseResult EditorShownPhaseResult { get; private set; }
    private bool _editorPhaseRewardVisible;
    private PopupStorePreviewTextStyle _editorPhaseGradeStyle, _editorPhaseCountStyle, _editorPhaseScoreStyle;
    private uint _editorPhaseVisualResumeVersion;
    private Action _editorRestoreSummary;

    // Reuses the authored panel and approved sprites. It never constructs a score-grade result,
    // invokes RewardBox.Present/Reveal, decides a reward, or consumes a receipt.
    private bool EditorTickPhasePreview(float delta)
    {
        var preview = GetComponent<PopupStorePhasePreview>();
        if (preview == null || !preview.IsInitialized) return false;
        if (!isActiveAndEnabled || !Finite(delta) || delta < 0 || (!_ready && !Refresh())
            || !Validate(out _) || _view.VisibleState != VisibleState.Appeared || !_clock.CanAdvanceVisuals) return true;
        var result = preview.Session?.FinalResult;
        if (result == null || !preview.IsResultPresentationReady || _presentation.HasPendingVisualWork || HasActiveDeparture()) return true;
        if (EditorShownPhaseResult != null && !ReferenceEquals(EditorShownPhaseResult, result))
        { preview.FailClosed("The local phase result identity changed."); return true; }
        if (Application.isPlaying) delta = _clock.FilterAutomaticVisualDelta(delta, ref _editorPhaseVisualResumeVersion);
        if (preview.RewardPresentation != null && preview.RewardPresentation.BeginCount != 0
            && preview.RewardPresentation.State != PopupStorePhaseRewardVisualState.Confirmed)
        {
            EditorHideSummaryForReward();
            preview.RewardPresentation.ResumePresentation();
            preview.RewardPresentation.transform.SetAsLastSibling();
            preview.RewardPresentation.Tick(delta, true);
            return true;
        }
        if (!_shown)
        {
            if (!EditorValidatePhaseSummaryLayout(out string layoutError))
            { preview.FailClosed(layoutError); return true; }
            var box = _rewardBox != null ? _rewardBox.BoxImage : null;
            var gradeHud = GetComponentInChildren<PopupStoreCurrentGradeHud>(true);
            var securedGrade = result.EndReason == PopupStorePhaseEndReason.Completed || result.EndReason == PopupStorePhaseEndReason.TimedOut
                ? result.HighestClearedGrade : PopupStoreGradeTier.None;
            bool hasBox = securedGrade >= PopupStoreGradeTier.C && securedGrade <= PopupStoreGradeTier.SSS;
            if (box == null || _rewardBox.gameObject != gameObject || !ContainsResultGraphic(box)
                || (hasBox && (gradeHud == null || !gradeHud.ValidateMapping(out _))))
            { preview.FailClosed("Phase summary needs its original local box image and approved grade mapping."); return true; }
            var sprite = hasBox ? gradeHud.SpriteFor(securedGrade) : null;
            EditorShownPhaseResult = result;
            string oldTitle = _titleText.text, oldScore = _scoreText.text, oldConfirm = _confirmText.text;
            bool scoreActive = _scoreText.gameObject.activeSelf;
            Action restoreAbandoned = null;
            _titleText.text = "게임 종료";
            _scoreText.text = "기록 점수 " + result.TotalScore.ToString(CultureInfo.InvariantCulture);
            var position = _scoreText.rectTransform.anchoredPosition;
            var size = _scoreText.rectTransform.sizeDelta;
            var alignment = _scoreText.alignment;
            bool gradeActive = _gradeText.gameObject.activeSelf, countActive = _countText.gameObject.activeSelf;
            var boxPosition = box.rectTransform.anchoredPosition;
            var boxSize = box.rectTransform.sizeDelta;
            var boxScale = box.rectTransform.localScale;
            bool boxAspect = box.preserveAspect, boxRaycast = box.raycastTarget;
            var boxButton = box.GetComponent<Button>();
            bool boxInteractable = boxButton != null && boxButton.interactable;
            _editorPhaseScoreStyle = new PopupStorePreviewTextStyle(_scoreText);
            _editorRestoreSummary = () => {
                restoreAbandoned?.Invoke();
                _titleText.text = oldTitle; _scoreText.text = oldScore; _confirmText.text = oldConfirm;
                _scoreText.gameObject.SetActive(scoreActive);
                _scoreText.rectTransform.anchoredPosition = position; _scoreText.rectTransform.sizeDelta = size;
                _scoreText.alignment = alignment;
                _gradeText.gameObject.SetActive(gradeActive); _countText.gameObject.SetActive(countActive);
                if (box != null)
                {
                    box.rectTransform.anchoredPosition = boxPosition; box.rectTransform.sizeDelta = boxSize;
                    box.rectTransform.localScale = boxScale; box.preserveAspect = boxAspect; box.raycastTarget = boxRaycast;
                }
                if (boxButton != null) boxButton.interactable = boxInteractable;
            };
            _scoreText.rectTransform.anchoredPosition = hasBox ? _phaseSummaryScorePosition : _phaseSummaryNoBoxScorePosition;
            _scoreText.rectTransform.sizeDelta = _phaseSummaryScoreSize;
            _scoreText.alignment = TextAlignmentOptions.Center;
            PopupStorePreviewTextStyle.Apply(_scoreText, _phaseSummaryScoreFontSize, _phaseSummaryMinimumScoreFontSize);
            _gradeText.gameObject.SetActive(false); _countText.gameObject.SetActive(false);
            box.rectTransform.anchoredPosition = _phaseSummaryBoxPosition;
            box.rectTransform.sizeDelta = _phaseSummaryBoxSize;
            box.rectTransform.localScale = Vector3.one;
            box.sprite = sprite; box.enabled = hasBox; box.preserveAspect = true; box.raycastTarget = false;
            if (boxButton != null) boxButton.interactable = false;
            _editorPhaseRewardVisible = true;
            _confirmText.text = "받기";
            if (result.EndReason == PopupStorePhaseEndReason.Abandoned)
                restoreAbandoned = ApplyAbandonedLayout();
            _lockedGroup = _inputGroup;
            _oldInteractable = _lockedGroup.interactable; _oldRaycasts = _lockedGroup.blocksRaycasts;
            _lockedGroup.interactable = false; _lockedGroup.blocksRaycasts = false;
            _shownOverlay = _overlay; _overlay.transform.SetAsLastSibling();
            _overlay.gameObject.SetActive(true); _overlay.blocksRaycasts = true; _overlay.interactable = true;
            _shown = true; _elapsed = 0; _confirming = false; PresentationCount++;
            _appearing = !_hasAppeared && _showDuration > 0;
            if (!_hasAppeared)
            {
                _hasAppeared = true; AppearanceCount++;
                PopupStoreSound.Play(this, PopupStoreSoundCue.ResultShown, result);
            }
            _confirmButton.interactable = !_appearing;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _overlay.alpha = _appearing ? 0 : 1;
            _panel.localScale = _baseScale * (_appearing ? _startScale : 1f);
            if (Application.isPlaying) Debug.Log("POPUP_STORE_PHASE_RESULT session=" + result.BusinessSessionId
                + "; reason=" + result.EndReason + "; highest=" + result.HighestClearedGrade
                + "; displayed=" + preview.SecuredGrade + "; successful=" + result.SuccessfulOrders
                + "; finalized=" + result.FinalizationCount + "; draws=0; unpaid=true", this);
        }
        if (_appearing)
        {
            _elapsed = Mathf.Min(_showDuration, _elapsed + delta);
            float t = _showDuration > 0 ? _elapsed / _showDuration : 1f;
            float eased = _showCurve.Evaluate(t);
            if (!Finite(eased)) { preview.FailClosed("Invalid authored result appearance curve."); return true; }
            _overlay.alpha = Mathf.Clamp01(eased);
            _panel.localScale = _baseScale * Mathf.LerpUnclamped(_startScale, 1f, Mathf.Clamp01(eased));
            if (t >= 1)
            { _appearing = false; _overlay.alpha = 1; _panel.localScale = _baseScale; _confirmButton.interactable = true; }
        }
        return true;
    }

    private void EditorHidePhaseReward()
    {
        GetComponent<PopupStorePhasePreview>()?.RewardPresentation?.HidePresentation();
        _editorRestoreSummary?.Invoke(); _editorRestoreSummary = null;
        _editorPhaseScoreStyle?.Restore(); _editorPhaseScoreStyle = null;
        _editorPhaseGradeStyle?.Restore(); _editorPhaseGradeStyle = null;
        _editorPhaseCountStyle?.Restore(); _editorPhaseCountStyle = null;
        if (!_editorPhaseRewardVisible) return;
        if (_rewardBox != null && _rewardBox.BoxImage != null)
        { _rewardBox.BoxImage.enabled = false; _rewardBox.BoxImage.sprite = null; }
        _editorPhaseRewardVisible = false;
    }

    private Action ApplyAbandonedLayout()
    {
        var title = _titleText.rectTransform;
        var button = (RectTransform)_confirmButton.transform;
        var panelSize = _panel.sizeDelta; var panelPosition = _panel.anchoredPosition;
        var titlePosition = title.anchoredPosition; var titleSize = title.sizeDelta;
        var buttonPosition = button.anchoredPosition;
        var buttonSize = button.sizeDelta;
        var titleStyle = new PopupStorePreviewTextStyle(_titleText);
        bool interiorActive = _abandonedInterior != null && _abandonedInterior.gameObject.activeSelf;
        var source = _frame.sprite; var imageType = _frame.type;
        bool aspect = _frame.preserveAspect;
        float multiplier = _frame.pixelsPerUnitMultiplier;
        Sprite sliced = null;
        // A local Sprite wrapper supplies nine-slice borders; the approved texture and importer stay untouched.
        if (source != null)
        {
            float fit = Mathf.Min(_panel.rect.width / source.rect.width, _panel.rect.height / source.rect.height);
            var border = Vector4.Scale(_abandonedFrameBorder, new Vector4(source.rect.width / 2162f,
                source.rect.height / 1505f, source.rect.width / 2162f, source.rect.height / 1505f));
            sliced = Sprite.Create(source.texture, source.rect, source.pivot / source.rect.size,
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            sliced.name = source.name + " (abandoned layout)";
            sliced.hideFlags = HideFlags.HideAndDontSave;
            _frame.sprite = sliced; _frame.type = Image.Type.Sliced; _frame.preserveAspect = false;
            float reference = _frame.canvas != null ? _frame.canvas.referencePixelsPerUnit : 100f;
            _frame.pixelsPerUnitMultiplier = reference / (source.pixelsPerUnit * Mathf.Max(.001f, fit));
        }
        _panel.sizeDelta = _abandonedPanelSize; _panel.anchoredPosition = Vector2.zero;
        title.anchoredPosition = _abandonedTitlePosition; title.sizeDelta = _abandonedTitleSize;
        button.anchoredPosition = _abandonedButtonPosition;
        button.sizeDelta = _abandonedButtonSize;
        PopupStorePreviewTextStyle.Apply(_titleText, _abandonedTitleFontSize, _abandonedTitleFontSize);
        if (_abandonedInterior != null) _abandonedInterior.gameObject.SetActive(true);
        _titleText.text = _abandonedTitle; _confirmText.text = _abandonedButtonLabel;
        _scoreText.gameObject.SetActive(false);
        return () => {
            _panel.sizeDelta = panelSize; _panel.anchoredPosition = panelPosition;
            title.anchoredPosition = titlePosition; title.sizeDelta = titleSize;
            button.anchoredPosition = buttonPosition;
            button.sizeDelta = buttonSize;
            titleStyle.Restore();
            if (_abandonedInterior != null) _abandonedInterior.gameObject.SetActive(interiorActive);
            _frame.sprite = source; _frame.type = imageType; _frame.preserveAspect = aspect;
            _frame.pixelsPerUnitMultiplier = multiplier;
            if (sliced != null)
            {
                if (Application.isPlaying) Destroy(sliced);
                else DestroyImmediate(sliced);
            }
        };
    }

    internal bool EditorValidatePhaseSummaryLayout(out string error)
    {
        error = null;
        if (!Finite(_phaseSummaryBoxPosition.x) || !Finite(_phaseSummaryBoxPosition.y)
            || !Finite(_phaseSummaryScorePosition.x) || !Finite(_phaseSummaryScorePosition.y)
            || !Finite(_phaseSummaryNoBoxScorePosition.x) || !Finite(_phaseSummaryNoBoxScorePosition.y)
            || !Finite(_phaseSummaryBoxSize.x) || !Finite(_phaseSummaryBoxSize.y)
            || _phaseSummaryBoxSize.x <= 0 || _phaseSummaryBoxSize.y <= 0
            || !Finite(_phaseSummaryScoreSize.x) || !Finite(_phaseSummaryScoreSize.y)
            || _phaseSummaryScoreSize.x <= 0 || _phaseSummaryScoreSize.y <= 0
            || !Finite(_phaseSummaryScoreFontSize) || !Finite(_phaseSummaryMinimumScoreFontSize)
            || _phaseSummaryMinimumScoreFontSize <= 0 || _phaseSummaryScoreFontSize < _phaseSummaryMinimumScoreFontSize)
            error = "Phase summary layout requires finite positions, positive sizes and a valid score font range.";
        if (!Finite(_abandonedPanelSize.x) || !Finite(_abandonedPanelSize.y)
            || _abandonedPanelSize.x <= 0 || _abandonedPanelSize.y <= 0
            || !Finite(_abandonedTitlePosition.x) || !Finite(_abandonedTitlePosition.y)
            || !Finite(_abandonedTitleSize.x) || !Finite(_abandonedTitleSize.y)
            || _abandonedTitleSize.x <= 0 || _abandonedTitleSize.y <= 0
            || !Finite(_abandonedButtonPosition.x) || !Finite(_abandonedButtonPosition.y)
            || !Finite(_abandonedButtonSize.x) || !Finite(_abandonedButtonSize.y)
            || _abandonedButtonSize.x <= 0 || _abandonedButtonSize.y <= 0
            || !Finite(_abandonedTitleFontSize) || _abandonedTitleFontSize <= 0)
            error = "Abandoned result requires finite positions and positive sizes.";
        for (int i = 0; i < 4; i++)
            if (!Finite(_abandonedFrameBorder[i]) || _abandonedFrameBorder[i] < 0)
                error = "Abandoned frame borders must be finite and non-negative.";
        if (_abandonedFrameBorder.x + _abandonedFrameBorder.z >= 2162
                || _abandonedFrameBorder.y + _abandonedFrameBorder.w >= 1505)
            error = "Abandoned frame borders must leave a stretchable center.";
        return error == null;
    }

    internal void EditorHideSummaryForReward()
    {
        if (_lockedGroup == null)
        {
            _lockedGroup = _inputGroup;
            _oldInteractable = _lockedGroup.interactable; _oldRaycasts = _lockedGroup.blocksRaycasts;
        }
        _lockedGroup.interactable = false; _lockedGroup.blocksRaycasts = false;
        _appearing = false;
        _overlay.alpha = 0; _overlay.interactable = false; _overlay.blocksRaycasts = false;
        _overlay.gameObject.SetActive(false);
    }

    internal void SetAccountClaimStatus(string label, bool retry)
    {
        if ((GetComponent<PopupStorePhasePreview>()?.IsAccountMode != true && _accountRecovery == null)
            || !_shown || _confirmText == null) return;
        _confirmText.text = EditorShownPhaseResult?.EndReason == PopupStorePhaseEndReason.Abandoned
            ? _abandonedButtonLabel : label;
        if (_confirmButton != null) _confirmButton.interactable = retry && !_appearing;
    }
}
