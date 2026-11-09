using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PopupStorePhaseRewardVisualState { Hidden, Rising, AwaitingTap, Opening, Revealed, Confirming, Confirmed }

/// <summary>A local presentation receipt. It does not calculate, draw, grant or persist any reward.</summary>
public sealed class PopupStorePhaseRewardVisualSession
{
    public object ResultIdentity { get; private set; }
    public int Amount { get; private set; }
    public PopupStorePhaseRewardVisualState State { get; private set; }
    public double Elapsed { get; private set; }
    public int BeginCount { get; private set; }
    public int OpenCount { get; private set; }
    public int ConfirmCount { get; private set; }

    public bool TryBegin(object resultIdentity, int amount)
    {
        if (resultIdentity == null || amount < 0) return false;
        if (ResultIdentity != null)
            return ReferenceEquals(ResultIdentity, resultIdentity) && Amount == amount
                && State != PopupStorePhaseRewardVisualState.Confirmed;
        ResultIdentity = resultIdentity; Amount = amount; BeginCount++;
        State = PopupStorePhaseRewardVisualState.Rising; Elapsed = 0;
        return true;
    }

    public bool Advance(double delta, double riseDuration, double openingDuration)
    {
        if (!Finite(delta) || delta < 0 || !Finite(riseDuration) || riseDuration <= 0
            || !Finite(openingDuration) || openingDuration <= 0) return false;
        if (State == PopupStorePhaseRewardVisualState.Rising)
        {
            Elapsed = Math.Min(riseDuration, Elapsed + delta);
            if (Elapsed >= riseDuration) { State = PopupStorePhaseRewardVisualState.AwaitingTap; Elapsed = 0; }
        }
        else if (State == PopupStorePhaseRewardVisualState.AwaitingTap)
            Elapsed = (Elapsed + delta) % 1024d;
        else if (State == PopupStorePhaseRewardVisualState.Opening)
        {
            Elapsed = Math.Min(openingDuration, Elapsed + delta);
            if (Elapsed >= openingDuration) { State = PopupStorePhaseRewardVisualState.Revealed; Elapsed = openingDuration; }
        }
        return true;
    }

    public bool TryOpen()
    {
        if (State != PopupStorePhaseRewardVisualState.AwaitingTap) return false;
        State = PopupStorePhaseRewardVisualState.Opening; Elapsed = 0; OpenCount++;
        return true;
    }

    public bool TryReserveConfirmation()
    {
        if (State != PopupStorePhaseRewardVisualState.Revealed) return false;
        State = PopupStorePhaseRewardVisualState.Confirming;
        return true;
    }

    public void CompleteConfirmation(bool accepted)
    {
        if (State != PopupStorePhaseRewardVisualState.Confirming) return;
        State = accepted ? PopupStorePhaseRewardVisualState.Confirmed : PopupStorePhaseRewardVisualState.Revealed;
        if (accepted) ConfirmCount++;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

/// <summary>Editor-only authored overlay. Its owner supplies a fixed local amount and the shared visual delta.</summary>
[DisallowMultipleComponent]
public sealed class PopupStorePhaseRewardPresentation : MonoBehaviour
{
    [Header("Dedicated reward controls")]
    [SerializeField] private CanvasGroup _overlay;
    [SerializeField] private Image _boxImage;
    [SerializeField] private Button _boxButton;
    [SerializeField] private Image _boomImage;
    [SerializeField] private AnimationClip _boomClip;
    [SerializeField] private Sprite[] _boomFrames = Array.Empty<Sprite>();
    [SerializeField] private float[] _boomFrameTimes = Array.Empty<float>();
    [SerializeField] private AnimationCurve _boomAlphaCurve;
    [SerializeField] private Image[] _diamondImages = Array.Empty<Image>();
    [SerializeField] private Image _centralDiamond;
    [SerializeField] private TMP_Text _tapText;
    [SerializeField] private TMP_Text _amountText;
    [SerializeField] private TMP_Text _disclaimerText;
    [SerializeField] private Button _confirmButton;
    [Header("Authored layout and timing")]
    [SerializeField] private Vector2 _riseFrom = new Vector2(0, -780);
    [SerializeField] private Vector2 _restPosition = new Vector2(0, 45);
    [SerializeField, Min(.01f)] private float _riseDuration = .7f;
    [SerializeField, Min(.01f)] private float _openingDuration = .85f;
    [SerializeField, Min(.01f)] private float _pulsePeriod = 1.3f;
    [SerializeField, Range(0, .15f)] private float _pulseScale = .045f;
    [SerializeField, Range(0, 10)] private float _idleTilt = 2.5f;
    [SerializeField, Min(1)] private float _burstRadius = 300;
    [SerializeField] private AnimationCurve _riseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private readonly PopupStorePhaseRewardVisualSession _session = new PopupStorePhaseRewardVisualSession();
    private PopupStoreBusinessClock _clock;
    private Func<bool> _close;
    private Sprite _shownSprite;
    private string _disclaimer;
    private bool _visible;
    private bool _listenersBound;
    private uint _resumeVersion;
    public PopupStorePhaseRewardVisualSession Session => _session;
    public PopupStorePhaseRewardVisualState State => _session.State;
    public int BeginCount => _session.BeginCount;
    public int OpenCount => _session.OpenCount;
    public int ConfirmCount => _session.ConfirmCount;
    public bool IsVisible => _visible && isActiveAndEnabled && _overlay != null && _overlay.alpha > 0;
    public Sprite DisplayedSprite => _shownSprite;
    public int Amount => _session.Amount;
    public string LastError { get; private set; }
    public float RiseDuration => _riseDuration;
    public float OpeningDuration => _openingDuration;

    public bool BindClock(PopupStoreBusinessClock clock)
    {
        if (clock == null || !transform.IsChildOf(clock.transform) || (_clock != null && _clock != clock))
            return Reject("Reward presentation needs its original popup-local clock.");
        _clock = clock; _resumeVersion = clock.VisualResumeVersion;
        return true;
    }

    public bool Begin(object resultIdentity, Sprite gradeSprite, int amount, string disclaimer, Func<bool> closeCallback)
        => BeginCore(resultIdentity, gradeSprite, amount, disclaimer, closeCallback, false);

    internal bool BeginAccountConfirmed(PopupStorePhasePreview owner, PopupStorePhaseResult result,
        Sprite gradeSprite, int amount, Func<bool> closeCallback)
    {
        if (owner == null || !transform.IsChildOf(owner.transform) || !owner.AccountClaimConfirmed
            || !ReferenceEquals(owner.Session?.FinalResult, result) || owner.AccountConfirmedDiamonds != amount)
            return Reject("Account reward visuals require this owner's confirmed saved result.");
        return BeginCore(result, gradeSprite, amount, "보상 수령 완료", closeCallback, true);
    }

    internal bool BeginAccountRecovery(PopupStoreAccountRecoveryPresentation owner, Sprite gradeSprite, Func<bool> closeCallback)
    {
        if (owner == null || !transform.IsChildOf(owner.transform) || !owner.IsConfirmed
            || !owner.Snapshot.EligibleReward || owner.Reward != this) return Reject("Saved reward confirmation is required.");
        return BeginCore(owner.Snapshot, gradeSprite, owner.Snapshot.Diamonds, "보상 수령 완료", closeCallback, true);
    }

    private bool BeginCore(object resultIdentity, Sprite gradeSprite, int amount, string disclaimer,
        Func<bool> closeCallback, bool accountConfirmed)
    {
        if (!ValidateConfiguration(out string error)) return Reject(error);
        if (_clock == null || !isActiveAndEnabled || resultIdentity == null || gradeSprite == null
            || amount < 0 || string.IsNullOrWhiteSpace(disclaimer) || (!accountConfirmed && !disclaimer.Contains("미지급")) || closeCallback == null
            || !IsApprovedBox(gradeSprite)) return Reject("An owned final result, approved box and explicit unpaid amount are required.");
        if (_session.ResultIdentity != null && (!ReferenceEquals(_session.ResultIdentity, resultIdentity)
            || _shownSprite != gradeSprite || _session.Amount != amount || _disclaimer != disclaimer))
            return Reject("An observed reward presentation cannot be rebound to another result or payload.");
        if (!_session.TryBegin(resultIdentity, amount)) return false;
        if (_shownSprite == null)
        {
            _shownSprite = gradeSprite; _disclaimer = disclaimer; _close = closeCallback;
            _amountText.text = "+" + amount.ToString(CultureInfo.InvariantCulture) + "D";
            _disclaimerText.text = disclaimer;
        }
        BindListeners(); _visible = true; LastError = null; Render();
        return true;
    }

    public void Tick(float delta, bool contextAlreadyFiltered = false)
    {
        if (!Finite(delta) || delta < 0) { Reject("Reward presentation delta must be finite and non-negative."); return; }
        if (!_visible || !isActiveAndEnabled || _clock == null || !_clock.CanAdvanceVisuals) return;
        if (!ValidateConfiguration(out string error)) { Reject(error); HidePresentation(); return; }
        if (!contextAlreadyFiltered && Application.isPlaying)
            delta = _clock.FilterAutomaticVisualDelta(delta, ref _resumeVersion);
        _session.Advance(delta, _riseDuration, _openingDuration);
        Render();
    }

    public bool TryOpen()
    {
        if (!CanReceiveInput() || !_session.TryOpen()) return false;
        Render(); Log("OPEN"); return true;
    }

    public bool TryConfirm()
    {
        if (!CanReceiveInput() || _close == null || !_session.TryReserveConfirmation()) return false;
        Render();
        bool accepted = false;
        try { accepted = _close(); }
        catch (Exception exception) { LastError = "Reward confirmation failed: " + exception.Message; }
        _session.CompleteConfirmation(accepted);
        if (accepted) { Log("CONFIRMED"); HidePresentation(); }
        else Render();
        return accepted;
    }

    public void HidePresentation()
    {
        _visible = false;
        if (Own(_overlay)) { _overlay.alpha = 0; _overlay.interactable = false; _overlay.blocksRaycasts = false; }
        if (Own(_boxButton)) _boxButton.interactable = false;
        if (Own(_confirmButton)) _confirmButton.interactable = false;
    }

    public bool ResumePresentation()
    {
        if (_session.ResultIdentity == null || _session.State == PopupStorePhaseRewardVisualState.Confirmed
            || !isActiveAndEnabled || !ValidateConfiguration(out _)) return false;
        _visible = true; Render(); return true;
    }

    public bool ValidateConfiguration(out string error)
    {
        error = null;
        if (!Own(_overlay) || _overlay.gameObject != gameObject || !Own(_boxImage) || !Own(_boxButton) || !Own(_boomImage)
            || !Own(_centralDiamond) || !Own(_tapText) || !Own(_amountText) || !Own(_disclaimerText)
            || !Own(_confirmButton) || _boxButton.gameObject != _boxImage.gameObject
            || _boxButton.targetGraphic != _boxImage || _boxButton == _confirmButton
            || _boxButton.onClick.GetPersistentEventCount() != 0 || _confirmButton.onClick.GetPersistentEventCount() != 0
            || _amountText == _disclaimerText || _amountText == _tapText || _tapText == _disclaimerText
            || _boxImage == _boomImage || _boxImage == _centralDiamond || _boomImage == _centralDiamond)
            error = "Reward controls must be separate local authored references without persistent callbacks.";
        else if (_boomClip == null || _boomClip.length <= 0 || _boomClip.events.Length != 0 || _centralDiamond.sprite == null
            || _diamondImages == null || _diamondImages.Length < 6 || _diamondImages.Length > 24)
            error = "Original boom and bounded authored diamond visuals are required.";
        else if (!ValidateBoomFrames())
            error = "Original boom sprite keys, times and alpha curve are required.";
        else if (!Finite(_riseDuration) || _riseDuration <= 0 || !Finite(_openingDuration) || _openingDuration <= 0
            || !Finite(_pulsePeriod) || _pulsePeriod <= 0 || !Finite(_pulseScale) || _pulseScale < 0 || _pulseScale > .15f
            || !Finite(_idleTilt) || _idleTilt < 0 || _idleTilt > 10 || !Finite(_burstRadius) || _burstRadius <= 0
            || !Finite(_riseFrom.x) || !Finite(_riseFrom.y) || !Finite(_restPosition.x) || !Finite(_restPosition.y)
            || _riseCurve == null || _riseCurve.length < 2)
            error = "Reward visual settings must be finite with positive durations.";
        else
        {
            var images = new HashSet<Image> { _boxImage, _boomImage, _centralDiamond };
            foreach (Image image in _diamondImages)
                if (!Own(image) || image.sprite == null || !images.Add(image))
                { error = "Each authored diamond needs a distinct local image and original sprite."; break; }
            foreach (var key in _riseCurve.keys)
                if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent))
                { error = "Reward rise curve contains a non-finite key."; break; }
        }
        return error == null;
    }

    private void Render()
    {
        if (!_visible || _overlay == null) return;
        _overlay.alpha = 1; _overlay.ignoreParentGroups = true;
        _overlay.interactable = true; _overlay.blocksRaycasts = true;
        transform.SetAsLastSibling();
        bool awaiting = State == PopupStorePhaseRewardVisualState.AwaitingTap;
        bool opening = State == PopupStorePhaseRewardVisualState.Opening;
        bool revealed = State == PopupStorePhaseRewardVisualState.Revealed || State == PopupStorePhaseRewardVisualState.Confirming;
        float t = Mathf.Clamp01((float)(_session.Elapsed / _openingDuration));
        var boxRect = _boxImage.rectTransform;
        _boxImage.sprite = _shownSprite; _boxImage.preserveAspect = true;
        _boxImage.enabled = !revealed && (!opening || t < .23f);
        _boxImage.raycastTarget = awaiting;
        _boxButton.interactable = awaiting && _clock != null && _clock.CanAdvanceVisuals;
        boxRect.anchoredPosition = _restPosition;
        boxRect.localScale = Vector3.one; boxRect.localRotation = Quaternion.identity;
        if (State == PopupStorePhaseRewardVisualState.Rising)
        {
            float progress = Mathf.Clamp01((float)(_session.Elapsed / _riseDuration));
            float eased = _riseCurve.Evaluate(progress);
            if (!Finite(eased)) { Reject("Reward rise curve returned a non-finite value."); HidePresentation(); return; }
            boxRect.anchoredPosition = Vector2.LerpUnclamped(_riseFrom, _restPosition, Mathf.Clamp01(eased));
            boxRect.localScale = Vector3.one * Mathf.Lerp(.72f, 1, Mathf.Clamp01(eased));
        }
        else if (awaiting)
        {
            float wave = Mathf.Sin((float)_session.Elapsed * Mathf.PI * 2 / _pulsePeriod);
            boxRect.localScale = Vector3.one * (1 + _pulseScale * wave);
            boxRect.localRotation = Quaternion.Euler(0, 0, _idleTilt * wave);
        }
        else if (opening) boxRect.localScale = Vector3.one * Mathf.Lerp(1, 1.15f, Mathf.Min(1, t * 5));
        _tapText.gameObject.SetActive(awaiting);
        _amountText.gameObject.SetActive(revealed);
        _disclaimerText.gameObject.SetActive(revealed);
        _confirmButton.gameObject.SetActive(revealed);
        _confirmButton.interactable = State == PopupStorePhaseRewardVisualState.Revealed && _clock != null && _clock.CanAdvanceVisuals;
        _centralDiamond.gameObject.SetActive(revealed || (opening && t >= .23f));
        _centralDiamond.rectTransform.localScale = Vector3.one * (revealed ? 1 : Mathf.Lerp(.3f, 1, t));
        _boomImage.gameObject.SetActive(opening && _session.Elapsed <= _boomClip.length);
        if (_boomImage.gameObject.activeSelf) RenderOriginalBoom((float)_session.Elapsed);
        for (int i = 0; i < _diamondImages.Length; i++)
        {
            Image diamond = _diamondImages[i];
            diamond.gameObject.SetActive(opening || revealed);
            float travel = revealed ? 1 : Mathf.Sin(t * Mathf.PI * .5f);
            float angle = (i * 137.50776f + 12f) * Mathf.Deg2Rad;
            float radius = _burstRadius * (.63f + (i % 4) * .12f) * travel;
            diamond.rectTransform.anchoredPosition = _restPosition + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .70f + 40 * travel);
            diamond.rectTransform.localScale = Vector3.one * (.58f + (i % 3) * .14f) * Mathf.Lerp(.3f, 1, travel);
            diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * travel * (12 + i * 2));
        }
    }

    // The original clip's Image keys are serialized by the Editor authoring tool.
    // SampleAnimation does not reliably bind its object-reference track to a detached
    // UI Image. Assign through Image.sprite so its texture and vertices are dirtied,
    // while retaining the original frame timestamps and alpha curve exactly.
    private void RenderOriginalBoom(float elapsed)
    {
        int index = 0;
        for (int i = 1; i < _boomFrameTimes.Length && _boomFrameTimes[i] <= elapsed; i++) index = i;
        _boomImage.sprite = _boomFrames[index];
        _boomImage.preserveAspect = true;
        _boomImage.color = new Color(1, 1, 1, Mathf.Clamp01(_boomAlphaCurve.Evaluate(elapsed)));
    }

    private bool ValidateBoomFrames()
    {
        if (_boomFrames == null || _boomFrameTimes == null || _boomFrames.Length == 0
            || _boomFrames.Length != _boomFrameTimes.Length || _boomFrameTimes[0] != 0
            || _boomAlphaCurve == null || _boomAlphaCurve.length < 2) return false;
        for (int i = 0; i < _boomFrames.Length; i++)
            if (_boomFrames[i] == null || !Finite(_boomFrameTimes[i]) || _boomFrameTimes[i] < 0
                || _boomFrameTimes[i] > _boomClip.length || (i > 0 && _boomFrameTimes[i] <= _boomFrameTimes[i - 1]))
                return false;
        foreach (var key in _boomAlphaCurve.keys)
            if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent)) return false;
        return true;
    }

    private bool CanReceiveInput() => _visible && isActiveAndEnabled && _clock != null
        && _clock.CanAdvanceVisuals && ValidateConfiguration(out _);
    private void BindListeners()
    {
        if (_listenersBound) return;
        _boxButton.onClick.AddListener(OnBoxClicked); _confirmButton.onClick.AddListener(OnConfirmClicked);
        _listenersBound = true;
    }
    private void OnBoxClicked() => TryOpen();
    private void OnConfirmClicked() => TryConfirm();
    private bool Own(Component value) => value != null && (value.transform == transform || value.transform.IsChildOf(transform));
    private bool Reject(string error) { LastError = error; return false; }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsApprovedBox(Sprite value)
    {
        foreach (var grade in new[] { "C", "B", "A", "S", "SS", "SSS" })
            if (value.name == "PopupRewardBox_" + grade + "_APPROVED") return true;
        return false;
    }
    private void Log(string action)
    {
        var result = _session.ResultIdentity as PopupStorePhaseResult;
        if (Application.isPlaying) Debug.Log("POPUP_STORE_PHASE_REWARD_" + action
            + " session=" + (result != null ? result.BusinessSessionId : "local-visual")
            + "; sprite=" + (_shownSprite != null ? _shownSprite.name : "")
            + "; amount=" + Amount + "; begin=" + BeginCount + "; open=" + OpenCount
            + "; confirmed=" + ConfirmCount + "; unpaid=True", this);
    }
    private void Awake() => HidePresentation();
    private void OnDisable() => HidePresentation();
    private void OnDestroy()
    {
        if (!_listenersBound) return;
        if (_boxButton != null) _boxButton.onClick.RemoveListener(OnBoxClicked);
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(OnConfirmClicked);
        _listenersBound = false;
    }
}
