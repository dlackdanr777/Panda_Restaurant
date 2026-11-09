using System.Globalization;
using Muks.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class PopupStoreResultPanel
{
    private PopupStoreAccountRecoveryPresentation _accountRecovery;
    internal bool BindAccountRecovery(PopupStoreAccountRecoveryPresentation owner)
    {
        if (owner == null || owner.gameObject != gameObject || !owner.IsBound || _shown
            || _accountRecovery != null || GetComponent<PopupStorePhasePreview>() != null || !Refresh()) return false;
        _accountRecovery = owner;
        return true;
    }

    private bool TickAccountRecovery(float delta)
    {
        if (_accountRecovery == null) return false;
        if (!isActiveAndEnabled || !Finite(delta) || delta < 0 || (!_ready && !Refresh())
            || !Validate(out _) || _view.VisibleState != VisibleState.Appeared) return true;
        if (!_accountRecovery.IsCurrent || !_clock.CanAdvanceVisuals)
        { if (_confirmButton != null) _confirmButton.interactable = false; return true; }
        if (Application.isPlaying) delta = _clock.FilterAutomaticVisualDelta(delta, ref _editorPhaseVisualResumeVersion);
        var reward = _accountRecovery.Reward;
        if (reward != null && reward.BeginCount != 0)
        {
            EditorHideSummaryForReward(); reward.ResumePresentation(); reward.Tick(delta, true);
            return true;
        }
        if (!_shown)
        {
            if (!EditorValidatePhaseSummaryLayout(out _)) return true;
            var result = _accountRecovery.Snapshot;
            var box = _rewardBox != null ? _rewardBox.BoxImage : null;
            var hud = GetComponentInChildren<PopupStoreCurrentGradeHud>(true);
            if (box == null || !ContainsResultGraphic(box) || (result.EligibleReward && (hud == null || !hud.ValidateMapping(out _)))) return true;
            var scorePosition = _scoreText.rectTransform.anchoredPosition;
            var scoreSize = _scoreText.rectTransform.sizeDelta;
            var alignment = _scoreText.alignment;
            var boxPosition = box.rectTransform.anchoredPosition;
            var boxSize = box.rectTransform.sizeDelta;
            var boxScale = box.rectTransform.localScale;
            bool boxAspect = box.preserveAspect, boxRaycast = box.raycastTarget;
            bool gradeActive = _gradeText.gameObject.activeSelf, countActive = _countText.gameObject.activeSelf;
            var boxButton = box.GetComponent<Button>();
            bool boxInteractable = boxButton != null && boxButton.interactable;
            _editorPhaseScoreStyle = new PopupStorePreviewTextStyle(_scoreText);
            _editorRestoreSummary = () => {
                _scoreText.rectTransform.anchoredPosition = scorePosition; _scoreText.rectTransform.sizeDelta = scoreSize;
                _scoreText.alignment = alignment; _gradeText.gameObject.SetActive(gradeActive); _countText.gameObject.SetActive(countActive);
                box.rectTransform.anchoredPosition = boxPosition; box.rectTransform.sizeDelta = boxSize;
                box.rectTransform.localScale = boxScale; box.preserveAspect = boxAspect; box.raycastTarget = boxRaycast;
                if (boxButton != null) boxButton.interactable = boxInteractable;
            };
            _titleText.text = "게임 종료";
            _scoreText.text = "기록 점수 " + result.Score.ToString(CultureInfo.InvariantCulture);
            _scoreText.rectTransform.anchoredPosition = result.EligibleReward ? _phaseSummaryScorePosition : _phaseSummaryNoBoxScorePosition;
            _scoreText.rectTransform.sizeDelta = _phaseSummaryScoreSize; _scoreText.alignment = TextAlignmentOptions.Center;
            PopupStorePreviewTextStyle.Apply(_scoreText, _phaseSummaryScoreFontSize, _phaseSummaryMinimumScoreFontSize);
            _gradeText.gameObject.SetActive(false); _countText.gameObject.SetActive(false);
            box.rectTransform.anchoredPosition = _phaseSummaryBoxPosition; box.rectTransform.sizeDelta = _phaseSummaryBoxSize;
            box.rectTransform.localScale = Vector3.one; box.preserveAspect = true; box.raycastTarget = false;
            box.sprite = result.EligibleReward ? hud.SpriteFor(result.Grade) : null; box.enabled = result.EligibleReward;
            if (boxButton != null) boxButton.interactable = false;
            _editorPhaseRewardVisible = true; _confirmText.text = "받기";
            _lockedGroup = _inputGroup; _oldInteractable = _lockedGroup.interactable; _oldRaycasts = _lockedGroup.blocksRaycasts;
            _lockedGroup.interactable = false; _lockedGroup.blocksRaycasts = false;
            _shownOverlay = _overlay; _overlay.transform.SetAsLastSibling(); _overlay.gameObject.SetActive(true);
            _overlay.blocksRaycasts = true; _overlay.interactable = true;
            _shown = true; _elapsed = 0; PresentationCount++;
            _appearing = !_hasAppeared && _showDuration > 0;
            if (!_hasAppeared) { _hasAppeared = true; AppearanceCount++; }
            _confirmButton.interactable = !_appearing;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _overlay.alpha = _appearing ? 0 : 1;
            _panel.localScale = _baseScale * (_appearing ? _startScale : 1f);
        }
        if (_appearing)
        {
            _elapsed = Mathf.Min(_showDuration, _elapsed + delta);
            float t = _showDuration > 0 ? _elapsed / _showDuration : 1f;
            float eased = _showCurve.Evaluate(t);
            if (!Finite(eased)) return true;
            _overlay.alpha = Mathf.Clamp01(eased);
            _panel.localScale = _baseScale * Mathf.LerpUnclamped(_startScale, 1f, Mathf.Clamp01(eased));
            if (t >= 1) { _appearing = false; _overlay.alpha = 1; _panel.localScale = _baseScale; _confirmButton.interactable = true; }
        }
        return true;
    }
}
