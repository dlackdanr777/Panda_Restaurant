using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Inspector-authored, instance-only presentation overrides for the isolated phase preview.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePhasePreviewPolish : MonoBehaviour
{
    [Header("Preview exit modal only")]
    [SerializeField] private string _exitTitle = "도전을 포기하시겠습니까?";
    [SerializeField, TextArea] private string _exitBody = "영업을 포기하면 사용한 영업권은 반환되지 않으며\n보상을 받을 수 없습니다.";
    [SerializeField] private string _continueLabel = "계속하기";
    [SerializeField] private string _abandonLabel = "포기하기";
    [SerializeField] private Color _continueTint = Color.white;
    [SerializeField] private Color _abandonTint = new Color(1f, .45f, 1f, 1f);
    [SerializeField] private float _bodyVerticalOffset = -35;
    [SerializeField] private float _buttonsVerticalOffset = -55;
    [SerializeField] private float _panelExtraHeight = 90;
    [Header("Remaining-time warning — visuals only")]
    [SerializeField, Min(.1f)] private float _warningSeconds = 10;
    [SerializeField, Min(.2f)] private float _warningPeriod = 1;
    [SerializeField] private Color _warningColor = new Color(.9f, .08f, .08f, 1);

    private PopupStorePhasePreview _owner;
    private PopupStoreBusinessClock _clock;
    private TMP_Text _time;
    private Color _normalTimeColor;
    private double _warningElapsed;
    private readonly List<Action> _restore = new List<Action>();
    public bool IsWarning { get; private set; }
    public bool IsBound => _owner != null;

    public bool Bind(PopupStorePhasePreview owner, TMP_Text time, TMP_Text title, TMP_Text body,
        Button continueButton, Button abandonButton, RectTransform panel)
    {
        if (IsBound || owner == null || !owner.IsInitialized || time == null || title == null || body == null
            || continueButton == null || abandonButton == null || panel == null || !transform.IsChildOf(owner.transform)
            || !panel.IsChildOf(owner.transform) || panel == owner.transform
            || time == title || time == body || title == body || continueButton == abandonButton
            || !time.transform.IsChildOf(owner.transform) || !title.transform.IsChildOf(panel)
            || !body.transform.IsChildOf(panel) || !continueButton.transform.IsChildOf(panel)
            || !abandonButton.transform.IsChildOf(panel) || !Finite(_warningPeriod) || _warningPeriod < .2f
            || !Finite(_warningSeconds) || _warningSeconds <= 0
            || !Finite(_bodyVerticalOffset) || !Finite(_buttonsVerticalOffset) || !Finite(_panelExtraHeight)
            || !Finite(_continueTint) || !Finite(_abandonTint) || !Finite(_warningColor)) return false;
        var continueText = continueButton.GetComponentInChildren<TMP_Text>(true);
        var abandonText = abandonButton.GetComponentInChildren<TMP_Text>(true);
        if (continueText == null || abandonText == null || continueButton.image == null || abandonButton.image == null) return false;
        _owner = owner; _clock = owner.GetComponent<PopupStoreBusinessClock>(); _time = time; _normalTimeColor = time.color;
        SetText(title, _exitTitle); SetText(body, _exitBody);
        SetText(continueText, _continueLabel); SetText(abandonText, _abandonLabel);
        SetColor(continueButton.image, _continueTint); SetColor(abandonButton.image, _abandonTint);
        Move(body.rectTransform, _bodyVerticalOffset);
        Move(continueButton.transform as RectTransform, _buttonsVerticalOffset);
        Move(abandonButton.transform as RectTransform, _buttonsVerticalOffset);
        var oldSize = panel.sizeDelta;
        _restore.Add(() => { if (panel != null) panel.sizeDelta = oldSize; });
        panel.sizeDelta = oldSize + Vector2.up * _panelExtraHeight;
        return true;
    }

    /// <summary>Called with the BusinessClock's accepted phase delta, never wall time or an independent Update.</summary>
    public void TickWarning(double delta)
    {
        if (!IsBound || _time == null || double.IsNaN(delta) || double.IsInfinity(delta) || delta < 0) return;
        bool eligible = _owner.Session != null && _owner.Session.State == PopupStorePhaseState.Playing
            && string.IsNullOrEmpty(_owner.PresentationCue) && _owner.RemainingSeconds > 0
            && _owner.RemainingSeconds <= _warningSeconds;
        if (!eligible) { ResetWarning(); return; }
        if (!isActiveAndEnabled || !_owner.CanPlay || _clock == null || !_clock.CanAdvanceVisuals) return;
        IsWarning = true;
        _warningElapsed += delta;
        float cycle = (float)(_warningElapsed % _warningPeriod / _warningPeriod);
        float pulse = .5f + .5f * Mathf.Cos(cycle * Mathf.PI * 2);
        _time.color = Color.Lerp(_normalTimeColor, _warningColor, pulse);
    }

    public void ResetWarning()
    {
        IsWarning = false; _warningElapsed = 0;
        if (_time != null) _time.color = _normalTimeColor;
    }
    private void SetText(TMP_Text target, string value)
    { string old = target.text; _restore.Add(() => { if (target != null) target.text = old; }); target.text = value; }
    private void SetColor(Image target, Color value)
    { var old = target.color; _restore.Add(() => { if (target != null) target.color = old; }); target.color = value; }
    private void Move(RectTransform target, float offset)
    { var old = target.anchoredPosition; _restore.Add(() => { if (target != null) target.anchoredPosition = old; }); target.anchoredPosition = old + Vector2.up * offset; }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b) && Finite(value.a);
    private void OnDisable() => ResetWarning();
    private void OnDestroy()
    {
        ResetWarning();
        for (int i = _restore.Count - 1; i >= 0; i--) _restore[i]();
        _restore.Clear();
    }
}
