using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.CompilerServices;
using TMPro;
using Muks.BackEnd;
using Muks.MobileUI;
using Muks.Tween;

/// <summary>
/// Presentation of a retained, confirmed result in the original machine/card/ten-slot layout.
/// No grants, selection, account mutation or transmission occurs in this view.
/// </summary>
public sealed class StaffGachaPurchaseDisplay
{
    private static readonly ConditionalWeakTable<object, object> Shown = new ConditionalWeakTable<object, object>();
    private static readonly ConditionalWeakTable<object, object> Acknowledged = new ConditionalWeakTable<object, object>();
    internal static bool IsAcknowledged(object result) => result != null && Acknowledged.TryGetValue(result, out _);
    private readonly UIStaffGacha _staff;
    private readonly UIGacha _view;
    private readonly Func<object> _getCompleted;
    private readonly Func<object, bool> _canPresent;
    private readonly Func<object, StaffGachaAcquisitionResult> _getAcquisition;
    private readonly Func<object, IReadOnlyList<GachaStaffData>> _getDisplayStaff;
    private readonly Action _resultClosed;
    private object _observed, _displayed;
    private StaffGachaResultSequence _sequence;
    private StaffGachaResultAnimation _animation;
    private bool _previousStarted, _previousComponents, _captured;
    private enum Phase { Closed, Machine, Card, Accumulating, CapsuleMoving, CapsuleOpening, Summary }
    private Phase _phase;
    private int _revealed, _selected;
    private float _nextInput, _nextSlot;
    private bool _cardTextAcknowledged;
    private Transform _slotParent;
    private int _slotSibling;
    private Action _restoreMachineArt;
    private readonly List<UnityEngine.Events.UnityAction> _slotActions = new List<UnityEngine.Events.UnityAction>();
    private readonly List<Button> _slotButtons = new List<Button>();
    private readonly Vector3[] _buttonCorners = new Vector3[4];
    private GachaResultCardPopup _popup;
    private Button _bonusButton;
    private UnityEngine.Events.UnityAction _bonusAction;
    private bool _bonusButtonWasEnabled;
    private GachaResultCardHitArea _bonusHitArea;

#if UNITY_EDITOR
    public bool EditorIsAnimating => _phase == Phase.Machine || _phase == Phase.Accumulating ||
        _phase == Phase.CapsuleMoving || _phase == Phase.CapsuleOpening;
    public bool EditorIsResultVisible => _displayed != null && _staff != null &&
        _staff.ResultCard != null && _staff.ResultCard.gameObject.activeInHierarchy;
    public int EditorResultIndex => _sequence == null ? -1 : _selected;
    public int EditorResultCount => _sequence?.Count ?? 0;
    public StaffGachaAcquisitionItem EditorCurrentItem => _sequence?.Result.Items[_selected];
    public int EditorAnimationStartCount { get; private set; }
    public string EditorAnimationError { get; private set; }
    public string EditorPresentationPhase => _phase.ToString();
    public int EditorVisibleSlotCount => _staff.ResultCardSlots.Count(slot => slot.gameObject.activeInHierarchy);
#endif

    public StaffGachaPurchaseDisplay(UIStaffGacha staff, UIGacha view, BackendManager backend)
        : this(staff, view,
            () => backend == null ? null : backend.LastCompletedStaffPurchaseExecution,
            value => backend != null && backend.CanPresentStaffPurchase(value as StaffGachaPurchaseExecution),
            value => ((StaffGachaPurchaseExecution)value).Plan.AccountResult.Acquisition,
            value => ((StaffGachaPurchaseExecution)value).DrawnStaff, null) { }

    public static StaffGachaPurchaseDisplay ForQuestGrant(UIStaffGacha staff, UIGacha view,
        BackendManager backend, string questId, Action resultClosed)
        => new StaffGachaPurchaseDisplay(staff, view,
            () => backend == null ? null : backend.LastCompletedQuestStaffGrant,
            value => value is QuestStaffGrantExecution grant && grant.QuestId == questId &&
                backend != null && backend.CanPresentQuestStaffGrant(grant),
            value => ((QuestStaffGrantExecution)value).Acquisition,
            value => ((QuestStaffGrantExecution)value).DisplayStaff, resultClosed);

    private StaffGachaPurchaseDisplay(UIStaffGacha staff, UIGacha view, Func<object> getCompleted,
        Func<object, bool> canPresent, Func<object, StaffGachaAcquisitionResult> getAcquisition,
        Func<object, IReadOnlyList<GachaStaffData>> getDisplayStaff, Action resultClosed)
    {
        _staff = staff; _view = view; _getCompleted = getCompleted; _canPresent = canPresent;
        _getAcquisition = getAcquisition; _getDisplayStaff = getDisplayStaff; _resultClosed = resultClosed;
    }

    private bool IsVisible => _staff != null && _view != null && _staff.gameObject.activeInHierarchy &&
        _view.IsCurrentMachine(_staff) && _view.VisibleState == VisibleState.Appeared;

    public void Tick()
    {
        if (!IsVisible) { Suspend(); return; }
        if (_displayed != null && !_canPresent(_displayed)) Close();
        try
        {
            if (_phase == Phase.Machine) _animation?.Tick();
            else if (_phase == Phase.Accumulating && Time.unscaledTime >= _nextSlot)
            {
                if (_revealed < 10)
                {
                    RevealSlot(_revealed, true);
                    _revealed++;
                    // Match UIItemGacha's ten-card cascade and its final pause.
                    _nextSlot = Time.unscaledTime + (_revealed == 10 ? 0.2f : 0.1f);
                }
                else BeginCapsule(10);
            }
            else if (_phase == Phase.CapsuleMoving && !_staff.ResultFinalCapsule.IsMoving)
            {
                _staff.ResultFinalCapsule.StartOpen();
                PlaySound(_staff.ResultBoom);
                _phase = Phase.CapsuleOpening;
            }
            else if (_phase == Phase.CapsuleOpening && _staff.ResultFinalCapsule.IsOpenComplete)
            {
                _staff.ResultFinalCapsule.CancelPresentation();
                ShowCurrentCard(true);
            }
        }
        catch (Exception exception)
        {
#if UNITY_EDITOR
            EditorAnimationError = exception.Message;
#endif
            DebugLog.LogError(exception.ToString());
            Close(); // The owner retains the confirmed result; a display error never retries a purchase.
        }
        object completed = _getCompleted();
        bool canShow = completed != null && _canPresent(completed);
        UpdateNativeResultButton(canShow);
        _popup?.BringToFront();
        if (!canShow || ReferenceEquals(_observed, completed)) return;
        _observed = completed;
        if (Acknowledged.TryGetValue(completed, out _)) return;
        TryShowCompleted(!Shown.TryGetValue(completed, out _), out _);
    }

    public bool TryShowCompleted(bool animate, out string error)
    {
        error = "획득 결과를 표시할 수 없습니다. 다시 열어 주세요.";
        object completed = _getCompleted();
        if (!IsVisible || completed == null || !_canPresent(completed) || EditorAnimationInProgress()) return false;
        if (!StaffGachaResultSequence.TryCreateFromCalculated(_getAcquisition(completed),
            _getDisplayStaff(completed), out var sequence, out error)) return false;
        if (_staff.ResultCard == null || _staff.ResultSlots == null || _staff.ResultCardSlots.Count != 10 ||
            _staff.ResultFinalCapsule == null || _staff.ResultCapsuleColor == null) return false;
        Close();
        _observed = _displayed = completed;
        _sequence = sequence;
        _selected = _revealed = 0;
        _previousStarted = _view.IsStartGacha;
        _previousComponents = _view.AreUIComponentsActive;
        _captured = true;
        _restoreMachineArt = _view.IsolateMachineArt(_staff);
        var animation = new StaffGachaResultAnimation();
        _animation = animation;
        _phase = Phase.Machine;
        bool play = animate && !Shown.TryGetValue(completed, out _);
        if (!animation.TryStart(_staff, sequence, finished =>
        {
            if (!ReferenceEquals(_animation, animation) || !ReferenceEquals(_sequence, finished) || !_canPresent(completed)) return;
            _view.SetActiveUIComponents(false);
            BindSlots();
            if (play) ShowCurrentCard();
            else ShowSummary();
        }, out error, play, true))
        {
            Close();
#if UNITY_EDITOR
            EditorAnimationError = error;
#endif
            return false;
        }
        _view.SetStartGacha(true);
        _view.SetActiveUIComponents(false);
        if (play)
        {
            _staff.ResultScreenButton.gameObject.SetActive(true);
            _staff.ResultScreenButton.transform.SetAsLastSibling();
        }
        if (!Shown.TryGetValue(completed, out _)) Shown.Add(completed, new object());
#if UNITY_EDITOR
        EditorAnimationError = null;
        if (play) EditorAnimationStartCount++;
#endif
        UpdateNativeResultButton(true);
        return true;
    }

    private bool EditorAnimationInProgress() => _phase == Phase.Machine || _phase == Phase.Accumulating ||
        _phase == Phase.CapsuleMoving || _phase == Phase.CapsuleOpening;

    private void BindSlots()
    {
        if (_slotButtons.Count != 0) return;
        // The authored grid is nested below the machine art, while the touch catcher is
        // its later sibling. Lift the same grid into the presentation layer (world rect
        // unchanged); the captured hierarchy is restored on close/cancel.
        _slotParent = _staff.ResultSlots.parent;
        _slotSibling = _staff.ResultSlots.GetSiblingIndex();
        _staff.ResultSlots.SetParent(_staff.transform, true);
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            var slot = _staff.ResultCardSlots[i];
            Button button = slot.GetComponent<Button>() ?? slot.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            UnityEngine.Events.UnityAction action = () => SelectCard(index);
            button.onClick.AddListener(action);
            _slotButtons.Add(button);
            _slotActions.Add(action);
        }
    }

    private void RevealSlot(int index, bool animate = false)
    {
        if (index < 0 || index >= 10 || _sequence.Count != 11) return;
        UIGachaCardSlot slot = _staff.ResultCardSlots[index];
        if (!slot.TrySetStaffAcquisitionResult(_sequence.StaffAt(index), _sequence.Result.Items[index]))
            throw new InvalidOperationException("직원 결과와 누적 카드가 일치하지 않습니다.");
        slot.gameObject.SetActive(true);
        slot.TweenStop();
        slot.transform.localScale = Vector3.one * (animate ? 1.2f : 1f);
        if (animate) slot.TweenScale(Vector3.one, 0.2f, Ease.OutBack);
        slot.ChangeImagePivot();
        _staff.ResultSlots.gameObject.SetActive(true);
    }

    private void ShowCurrentCard(bool animateFinal = false)
    {
        _selected = _sequence.Index;
        if (!_staff.ResultCard.TrySetStaffAcquisitionResult(_sequence.CurrentStaff, _sequence.CurrentItem))
            throw new InvalidOperationException("직원 결과와 카드가 일치하지 않습니다.");
        _staff.ResultImage.gameObject.SetActive(false);
        _staff.ResultCard.TweenStop();
        bool summary = _sequence.Count == 11 && _sequence.Index == 10;
        if (summary) _staff.ResultCard.SetScale(animateFinal ? 1.3f : 1f);
        else _staff.ResultCard.ResetScale();
        _staff.ResultCard.SetPosition(summary ? new Vector3(600, 0, 0) : Vector3.zero);
        _staff.ResultCard.gameObject.SetActive(true);
        if (animateFinal) _staff.ResultCard.TweenScale(Vector3.one, 0.2f, Ease.OutBack);
        // Like the item machine, individual reveals stay centred and do not build
        // a background grid. Only the completed ten-card cascade owns that grid.
        _phase = _sequence.Count == 1 || summary ? Phase.Summary : Phase.Card;
        if (summary) BindBonusCard();
        _cardTextAcknowledged = false;
        _staff.ResultScreenButton.gameObject.SetActive(true);
        // The full-screen touch surface belongs behind the real cards; cards remain selectable.
        _staff.ResultScreenButton.transform.SetAsLastSibling();
        _staff.ResultSlots.SetAsLastSibling();
        _staff.ResultCard.transform.SetAsLastSibling();
        _nextInput = Time.unscaledTime + 0.2f;
        PlaySound(_staff.GetResultSound(_sequence.CurrentStaff.Rank));
        UpdateNativeResultButton(true);
    }

    private void ShowSummary()
    {
        if (_sequence.Count == 11)
            for (int i = 0; i < 10; i++) RevealSlot(i);
        _revealed = _sequence.Count == 11 ? 10 : 0;
        _sequence.TrySelect(_sequence.Count - 1);
        ShowCurrentCard();
    }

    private void BeginCapsule(int index)
    {
        if (_sequence.Count != 11 || index != 10 || _revealed != 10)
            throw new InvalidOperationException("마지막 +1 캡슐은 10개 결과 목록 뒤에만 표시합니다.");
        _sequence.TrySelect(index);
        _selected = index;
        _staff.ResultCard.gameObject.SetActive(false);
        _staff.ResultImage.gameObject.SetActive(false);
        _staff.ResultScreenButton.gameObject.SetActive(false);
        _staff.ResultFinalCapsule.PreparePresentation(_sequence.CurrentStaff.ThumbnailSprite ?? _sequence.CurrentStaff.Sprite,
            _staff.ResultCapsuleColor, new Vector2(600, -2000));
        StaffCapsuleContentLayout.Fit(_staff.ResultFinalCapsule.PresentationImage,
            _staff.ResultFinalCapsule.UpperCapsuleImage, _staff.ResultFinalCapsule.LowerCapsuleImage);
        _staff.ResultFinalCapsule.TweenAnchoredPosition(new Vector2(600, 0), 1f, Ease.Smoothstep);
        _phase = Phase.CapsuleMoving;
        UpdateNativeResultButton(true);
    }

    public bool HandleScreenInput()
    {
        if (_displayed == null) return false;
        if (_popup != null && _popup.IsOpen) { _popup.Hide(); return true; }
        if (!IsVisible || !_canPresent(_displayed) || Time.unscaledTime < _nextInput) return true;
        if (_phase == Phase.Machine)
        {
            _animation?.AdvanceMachine();
            _nextInput = Time.unscaledTime + 0.2f;
        }
        else if (_phase == Phase.Card)
        {
            // Preserve the item machine's text-confirm touch before advancing.
            if (!_cardTextAcknowledged)
            {
                _cardTextAcknowledged = true;
                _nextInput = Time.unscaledTime + 0.5f;
            }
            else if (_sequence.Index == 9) HandleResultButton();
            else if (_animation != null && _animation.BeginNextReveal())
            {
                _selected = _sequence.Index;
                _staff.ResultCard.gameObject.SetActive(false);
                _staff.ResultSlots.gameObject.SetActive(false);
                _phase = Phase.Machine;
                _nextInput = Time.unscaledTime + 0.2f;
            }
        }
        else if (_phase == Phase.Summary) AcknowledgeResult();
        return true;
    }

    public void HandleResultButton()
    {
        if (!IsVisible) return;
        if (_displayed == null) { TryShowCompleted(false, out _); return; }
        if (!_canPresent(_displayed)) return;
        if (_phase == Phase.Summary) { AcknowledgeResult(); return; }
        if (_sequence.Count != 11 || _phase == Phase.Accumulating || _phase == Phase.CapsuleMoving ||
            _phase == Phase.CapsuleOpening) return;
        _animation?.FinishMachineForSummary();
        BindSlots();
        if (_revealed >= 10) { BeginCapsule(10); return; }
        _staff.ResultCard.gameObject.SetActive(false);
        _staff.ResultImage.gameObject.SetActive(false);
        _staff.ResultScreenButton.gameObject.SetActive(false);
        _phase = Phase.Accumulating;
        _nextSlot = Time.unscaledTime;
        UpdateNativeResultButton(true);
    }

    public bool SelectCard(int index)
    {
        if (!IsVisible || _sequence == null || !_canPresent(_displayed) ||
            _phase != Phase.Summary || _sequence.Count != 11 || _revealed != 10 || index < 0 || index >= _sequence.Count) return false;
        if (_popup == null) _popup = new GachaResultCardPopup(_view.transform, _staff.ResultCard);
        if (!_popup.ShowStaff(_sequence.StaffAt(index), _sequence.Result.Items[index])) return false;
        _selected = index;
        return true;
    }

    private void BindBonusCard()
    {
        if (_bonusButton != null) return;
        _bonusButton = _staff.ResultCard.GetComponent<Button>();
        _bonusButtonWasEnabled = _bonusButton != null && _bonusButton.enabled;
        if (_bonusButton == null) _bonusButton = _staff.ResultCard.gameObject.AddComponent<Button>();
        _bonusButton.enabled = true;
        _bonusButton.transition = Selectable.Transition.None;
        _bonusAction = () => SelectCard(10);
        _bonusButton.onClick.AddListener(_bonusAction);
        _bonusHitArea = new GachaResultCardHitArea(_staff.ResultCard.transform);
    }

    // Compatibility for the Editor inspector only; no paged runtime controls are created.
    public bool Move(int direction) => SelectCard(_selected + direction);

    private void UpdateNativeResultButton(bool canShow)
    {
        Button button = _staff == null ? null : _staff.ResultSkipButton;
        if (button == null) return;
        bool show = IsVisible && canShow && (_phase == Phase.Closed ||
            (_phase == Phase.Summary && _sequence?.Count == 1) ||
            ((_phase == Phase.Machine || _phase == Phase.Card) && _sequence?.Count == 11));
        button.gameObject.SetActive(show);
        button.interactable = show;
        if (show && button.transform is RectTransform rect)
        {
            GachaMachineParent.AlignResultButton(rect, (RectTransform)_view.transform, _buttonCorners);
        }
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = _phase == Phase.Closed ? "결과 확인" : _phase == Phase.Summary ? "닫기" : "건너뛰기";
        if (show) button.transform.SetAsLastSibling();
    }

    public void AcknowledgeResult(bool notify = true)
    {
        if (_displayed == null || (_phase != Phase.Card && _phase != Phase.Summary)) return;
        if (!Acknowledged.TryGetValue(_displayed, out _)) Acknowledged.Add(_displayed, new object());
        Close();
        if (notify) _resultClosed?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (Application.isPlaying && clip != null && _staff.ResultAudio != null) _staff.ResultAudio.PlayOneShot(clip);
    }

    public void Close()
    {
        _popup?.Dispose();
        _popup = null;
        _bonusHitArea?.Dispose();
        _bonusHitArea = null;
        if (_bonusButton != null)
        {
            _bonusButton.onClick.RemoveListener(_bonusAction);
            _bonusButton.enabled = _bonusButtonWasEnabled;
        }
        _bonusButton = null;
        _bonusAction = null;
        bool visible = IsVisible;
        var animation = _animation;
        _animation = null;
        _staff?.ResultFinalCapsule?.CancelPresentation();
        if (_staff != null)
        {
            _staff.ResultCard?.TweenStop();
            if (_staff.ResultCard != null) _staff.ResultCard.gameObject.SetActive(false);
            foreach (var slot in _staff.ResultCardSlots) { slot.TweenStop(); slot.gameObject.SetActive(false); }
            if (_staff.ResultSlots != null) _staff.ResultSlots.gameObject.SetActive(false);
            for (int i = 0; i < _slotButtons.Count; i++)
                if (_slotButtons[i] != null) _slotButtons[i].onClick.RemoveListener(_slotActions[i]);
        }
        _slotButtons.Clear(); _slotActions.Clear();
        if (_slotParent != null && _staff != null && _staff.ResultSlots != null)
        {
            _staff.ResultSlots.SetParent(_slotParent, true);
            _staff.ResultSlots.SetSiblingIndex(_slotSibling);
        }
        _slotParent = null;
        animation?.Close(visible);
        _restoreMachineArt?.Invoke();
        _restoreMachineArt = null;
        if (_captured && visible)
        {
            _view.SetStartGacha(_previousStarted);
            _view.SetActiveUIComponents(_previousComponents);
        }
        _captured = false; _phase = Phase.Closed;
        _sequence = null; _displayed = null;
        if (_staff != null && _staff.ResultSkipButton != null) _staff.ResultSkipButton.gameObject.SetActive(false);
    }

    public void Suspend() { Close(); _observed = null; }
    public void Dispose() => Close();
}


/// <summary>Stored acquisition output plus display bindings. Never rolls or calculates rewards.</summary>
public class StaffGachaResultSequence
{
    private readonly GachaStaffData[] _staff;
    public StaffGachaAcquisitionResult Result { get; }
    public int Index { get; private set; }
    public int Count => Result.Items.Count;
    public GachaStaffData CurrentStaff => _staff[Index];
    public GachaStaffData StaffAt(int index) => _staff[index];
    public StaffGachaAcquisitionItem CurrentItem => Result.Items[Index];
    public bool IsNavigationLocked { get; private set; }
    public bool CanMovePrevious => !IsNavigationLocked && Index > 0;
    public bool CanMoveNext => !IsNavigationLocked && Index + 1 < Count;
    public void LockNavigation() => IsNavigationLocked = true;
    public void UnlockNavigation() => IsNavigationLocked = false;

    protected StaffGachaResultSequence(GachaStaffData[] staff, StaffGachaAcquisitionResult result)
    {
        _staff = (GachaStaffData[])staff.Clone();
        Result = result;
    }

    public static bool TryCreateFromCalculated(StaffGachaAcquisitionResult result,
        IReadOnlyList<GachaStaffData> displayStaff, out StaffGachaResultSequence sequence, out string error)
    {
        sequence = null;
        error = "완료 결과와 표시 직원의 개수·ID·등급이 일치해야 합니다.";
        if (result == null || displayStaff == null || (result.Items.Count != 1 && result.Items.Count != 11) ||
            displayStaff.Count != result.Items.Count) return false;
        var copy = displayStaff.ToArray();
        for (int i = 0; i < copy.Length; i++)
        {
            GachaStaffData staff = copy[i];
            StaffGachaAcquisitionItem item = result.Items[i];
            if (staff == null || staff.StaffData == null || item == null ||
                string.IsNullOrWhiteSpace(staff.Id) || staff.Id.Any(char.IsWhiteSpace) ||
                !string.Equals(staff.Id, staff.StaffData.Id, StringComparison.Ordinal) ||
                !string.Equals(staff.Id, item.StaffId, StringComparison.Ordinal) ||
                staff.Rank != staff.StaffData.Rank || staff.Rank != item.Rank ||
                (staff.Rank != Rank.Normal1 && staff.Rank != Rank.Normal2 && staff.Rank != Rank.Rare &&
                    staff.Rank != Rank.Unique && staff.Rank != Rank.Special) ||
                (staff.ThumbnailSprite == null && staff.Sprite == null)) return false;
        }
        sequence = new StaffGachaResultSequence(copy, result);
        error = null;
        return true;
    }

    public bool TryMove(int offset)
    {
        if (IsNavigationLocked || (offset != -1 && offset != 1) ||
            (offset == -1 && !CanMovePrevious) || (offset == 1 && !CanMoveNext)) return false;
        Index += offset;
        return true;
    }

    public bool TrySelect(int index)
    {
        if (IsNavigationLocked || index < 0 || index >= Count) return false;
        Index = index;
        return true;
    }
}

/// <summary>Display-only driver for the existing staff machine; gameplay AnimationEvents remain suppressed until close.</summary>
public sealed class StaffGachaResultAnimation
{
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Start = Animator.StringToHash("Base Layer.Start_Gacha");
    private static readonly int Wait = Animator.StringToHash("Base Layer.Wait_Gacha");
    private static readonly int Open = Animator.StringToHash("Base Layer.Open_Gacha");
    private static readonly int Result = Animator.StringToHash("Base Layer.ZoomIn Item");
    private readonly List<Action> _restore = new List<Action>();
    private Animator _animator;
    private AudioSource _audio;
    private AudioClip _originalAudioClip;
    private int _originalAudioSamples;
    private AudioClip _boom;
    private RectTransform _capsules;
    private Image _staffImage;
    private Image _upperCapsuleImage, _lowerCapsuleImage;
    private StaffGachaResultSequence _sequence;
    private Action<StaffGachaResultSequence> _completed;
    private bool _fireEvents, _enabled, _raised, _opening, _boomPlayed;
    private bool _waitForInput, _preparedCapsule, _awaitingNextCapsule;
    private int _originalState;
    private float _originalTime, _raiseTime, _startLength;
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }

    public bool TryStart(UIStaffGacha staff, StaffGachaResultSequence sequence,
        Action<StaffGachaResultSequence> completed, out string error, bool animate = true, bool waitForInput = false)
    {
        error = "현재 직원머신에서 보관된 결과의 연출을 시작할 수 없습니다.";
        if (IsActive || staff == null || !staff.gameObject.activeInHierarchy || sequence == null ||
            (sequence.Count != 1 && sequence.Count != 11) || sequence.Index != 0 ||
            sequence.IsNavigationLocked || completed == null)
            return false;
        Animator animator = staff.ResultAnimator;
        UIGachaCard card = staff.ResultCard;
        Image staffImage = staff.ResultImage;
        RectTransform capsules = staff.ResultCapsules;
        Button single = staff.ResultEntryButton;
        AudioSource audio = staff.ResultAudio;
        if (animator == null || animator.runtimeAnimatorController == null || card == null ||
            card.gameObject.activeSelf || staffImage == null || capsules == null || single == null ||
            !single.gameObject.activeInHierarchy || audio == null || audio.isPlaying ||
            !animator.enabled || animator.IsInTransition(0) ||
            !new[] { Idle, Start, Wait, Open, Result }.All(hash => animator.HasState(0, hash)))
            return false;
        var original = animator.GetCurrentAnimatorStateInfo(0);
        if (original.fullPathHash != Idle) return false;
        AnimationClip startClip = animator.runtimeAnimatorController.animationClips
            .FirstOrDefault(clip => clip.name == "Start_Gacha");
        AnimationEvent raise = startClip == null ? null : startClip.events
            .FirstOrDefault(evt => evt.functionName == "CapsuleSetSibilingIndex" && evt.intParameter == 6);
        if (raise == null) { error = "기존 머신 캡슐 연출 이벤트 정보를 찾지 못했습니다."; return false; }

        _animator = animator;
        _audio = audio;
        _originalAudioClip = audio.clip;
        _originalAudioSamples = audio.clip == null ? 0 : audio.timeSamples;
        _capsules = capsules;
        _staffImage = staffImage;
        _upperCapsuleImage = staff.ResultUpperCapsule;
        _lowerCapsuleImage = staff.ResultLowerCapsule;
        _sequence = sequence;
        _completed = completed;
        _waitForInput = waitForInput;
        _preparedCapsule = _awaitingNextCapsule = false;
        _fireEvents = animator.fireEvents;
        _enabled = animator.enabled;
        _originalState = original.fullPathHash;
        _originalTime = original.normalizedTime;
        _raiseTime = raise.time;
        _startLength = startClip.length;
        _boom = staff.ResultBoom;
        CapturePresentation(staff.transform);
        IsActive = true;
        IsComplete = _raised = _opening = _boomPlayed = false;
        sequence.LockNavigation();
        try
        {
            // Never enter UIStaffGacha.SetStep/GetStaff/StartAddStaff or their tutorial/grant hooks.
            animator.fireEvents = false;
            ResetTriggers();
            foreach (Button button in staff.ResultControlButtons)
            {
                if (button != null) button.gameObject.SetActive(false);
            }
            card.gameObject.SetActive(false);
            Transform slots = staff.ResultSlots;
            if (slots != null) slots.gameObject.SetActive(false);
            _staffImage.sprite = sequence.CurrentStaff.ThumbnailSprite ?? sequence.CurrentStaff.Sprite;
            _staffImage.gameObject.SetActive(false);
            // Preserve the current capsule sprites. Cosmetic randomness is not needed for a fixed preview.
            _capsules.SetSiblingIndex(1);
            if (animate)
            {
                animator.SetTrigger("Start");
                if (Application.isPlaying) _audio.Play();
            }
            else
            {
                animator.Play(Result, 0, 0f);
                animator.Update(0f);
                Complete();
            }
            error = null;
            return true;
        }
        catch
        {
            Close();
            throw;
        }
    }

    public void Tick()
    {
        if (!IsActive || IsComplete || _animator == null) return;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        if (!_raised && state.fullPathHash == Start && state.normalizedTime * _startLength >= _raiseTime)
        {
            _raised = true;
            _capsules.SetSiblingIndex(6);
        }
        if (!_preparedCapsule && state.fullPathHash == Wait && !_animator.IsInTransition(0))
        {
            // Read the closed native capsule after its Wait clip has supplied its
            // geometry. Fit the visible sprite, not its gameplay feet pivot.
            StaffCapsuleContentLayout.Fit(_staffImage, _upperCapsuleImage, _lowerCapsuleImage);
            _preparedCapsule = true;
            _awaitingNextCapsule = false;
            _audio.Stop();
            _staffImage.gameObject.SetActive(true);
            _capsules.SetSiblingIndex(11);
            if (!_waitForInput) AdvanceMachine();
        }
        if (!_boomPlayed && state.fullPathHash == Open)
        {
            _boomPlayed = true;
            if (Application.isPlaying && _boom != null) _audio.PlayOneShot(_boom);
        }
        if (state.fullPathHash != Result || _awaitingNextCapsule) return;
        Complete();
    }

    public void AdvanceMachine()
    {
        if (!IsActive || IsComplete || _animator == null) return;
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash == Start) _animator.SetTrigger("Step2Skip");
        else if (state.fullPathHash == Wait && !_animator.IsInTransition(0) && _preparedCapsule && !_opening)
        {
            _opening = true;
            _animator.SetTrigger("CapsuleOpen");
        }
        // Open_Gacha and its real puff clip are never bypassed by a second click.
    }

    public bool BeginNextReveal()
    {
        if (!IsActive || !IsComplete || _animator == null || _completed == null ||
            _sequence.Count != 11 || _sequence.Index >= 9 || !_sequence.TryMove(1)) return false;
        // Reuse the native central Wait/Open/ZoomIn sequence, exactly as the item
        // machine does with Step2Skip. The right capsule is exclusively the +1.
        IsComplete = _opening = _boomPlayed = _preparedCapsule = false;
        _awaitingNextCapsule = true; // Ignore the outgoing ZoomIn state until Wait is entered.
        _sequence.LockNavigation();
        _staffImage.sprite = _sequence.CurrentStaff.ThumbnailSprite ?? _sequence.CurrentStaff.Sprite;
        _staffImage.gameObject.SetActive(false);
        ResetTriggers();
        _animator.SetTrigger("Step2Skip");
        return true;
    }

    public void FinishMachineForSummary()
    {
        if (!IsActive || _animator == null) return;
        IsComplete = true;
        _completed = null;
        _sequence.UnlockNavigation();
        _audio.Stop();
        ResetTriggers();
        _animator.Play(Result, 0, 0f);
        _animator.Update(0f);
        _staffImage.gameObject.SetActive(false);
    }

    private void Complete()
    {
        if (IsComplete) return;
        IsComplete = true; // Latch before calling the owner; repeated ticks cannot display again.
        _sequence.UnlockNavigation();
        _staffImage.gameObject.SetActive(false);
        var completed = _completed;
        completed?.Invoke(_sequence);
    }

    public void Close(bool restorePresentation = true)
    {
        if (!IsActive) return;
        IsActive = false;
        _completed = null;
        _sequence?.UnlockNavigation();
        _sequence = null;
        if (_audio != null)
        {
            _audio.Stop();
            // A stopped/paused staff source can still retain a seek position. Restore the
            // playback state we changed, without touching unrelated AudioSources or mixing settings.
            if (_originalAudioClip != null && _audio.clip == _originalAudioClip &&
                _originalAudioSamples >= 0 && _originalAudioSamples < _originalAudioClip.samples)
                _audio.timeSamples = _originalAudioSamples;
        }
        if (_animator != null)
        {
            _animator.fireEvents = false;
            ResetTriggers();
            _animator.Play(_originalState, 0, _originalTime);
            if (restorePresentation && _animator.gameObject.activeInHierarchy)
            {
                _animator.Update(0f);
            }
            if (restorePresentation) _animator.enabled = false;
        }
        // A navigation Hide wins over our old visible snapshot; never reactivate a departed machine.
        if (restorePresentation)
            foreach (Action restore in _restore) restore();
        _restore.Clear();
        if (_animator != null)
        {
            _animator.fireEvents = _fireEvents;
            if (restorePresentation) _animator.enabled = _enabled;
        }
    }

    private void ResetTriggers()
    {
        foreach (AnimatorControllerParameter parameter in _animator.parameters)
            if (parameter.type == AnimatorControllerParameterType.Trigger)
                _animator.ResetTrigger(parameter.nameHash);
    }

    private void CapturePresentation(Transform root)
    {
        // The existing clips animate transforms, active flags, Image sprites and alpha.
        // Restore exact pre-preview values, not an assumed default Idle layout.
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            Transform parent = transform.parent;
            Vector3 position = transform.localPosition, scale = transform.localScale;
            Quaternion rotation = transform.localRotation;
            int sibling = transform.GetSiblingIndex();
            bool active = transform.gameObject.activeSelf;
            _restore.Add(() =>
            {
                if (transform == null) return;
                if (transform.parent != parent) transform.SetParent(parent, false);
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
                transform.SetSiblingIndex(sibling);
            });
            if (transform is RectTransform rect)
            {
                Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta;
                Vector3 anchored = rect.anchoredPosition3D;
                _restore.Add(() =>
                {
                    if (rect == null) return;
                    rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                    rect.sizeDelta = size; rect.anchoredPosition3D = anchored;
                });
            }
            foreach (Graphic graphic in transform.GetComponents<Graphic>())
            {
                Color color = graphic.color;
                _restore.Add(() => { if (graphic != null) graphic.color = color; });
                if (graphic is Image image)
                {
                    Sprite sprite = image.sprite;
                    bool preserveAspect = image.preserveAspect;
                    _restore.Add(() =>
                    {
                        if (image == null) return;
                        image.sprite = sprite;
                        image.preserveAspect = preserveAspect;
                    });
                }
            }
            _restore.Add(() => { if (transform != null && transform.gameObject.activeSelf != active)
                transform.gameObject.SetActive(active); });
        }
    }

}

/// <summary>Staff-only geometry inside the existing closed capsule; no asset or item-machine changes.</summary>
internal static class StaffCapsuleContentLayout
{
    private const float ContentRatio = 0.88f;

    internal static void Fit(Image content, Image upper, Image lower)
    {
        if (content == null || content.sprite == null || upper == null || lower == null ||
            content.transform.parent == null)
            throw new InvalidOperationException("직원 캡슐 표시 영역을 확인할 수 없습니다.");
        Transform parent = content.transform.parent;
        Rect top = VisualBounds(upper, parent), bottom = VisualBounds(lower, parent);
        Rect capsule = Rect.MinMaxRect(Mathf.Min(top.xMin, bottom.xMin), Mathf.Min(top.yMin, bottom.yMin),
            Mathf.Max(top.xMax, bottom.xMax), Mathf.Max(top.yMax, bottom.yMax));
        Sprite sprite = content.sprite;
        Vector2 visibleSize = (Vector2)sprite.bounds.size * sprite.pixelsPerUnit;
        Vector2 sourceSize = sprite.rect.size;
        if (visibleSize.x <= 0f || visibleSize.y <= 0f || capsule.width <= 0f || capsule.height <= 0f)
            throw new InvalidOperationException("직원 또는 캡슐 스프라이트 크기가 유효하지 않습니다.");
        float scale = Mathf.Min(capsule.width / visibleSize.x, capsule.height / visibleSize.y) * ContentRatio;
        Vector2 size = sourceSize * scale;
        Vector2 spriteCenter = (Vector2)sprite.bounds.center * sprite.pixelsPerUnit + sprite.pivot;
        RectTransform rect = content.rectTransform;
        // Capsule_Open writes the original anchored Y. Compensate with the unanimated
        // pivot rather than fighting that shared animation or moving the capsule itself.
        Vector2 anchor = Vector2.Lerp(((RectTransform)parent).rect.min, ((RectTransform)parent).rect.max, 0.5f);
        Vector2 position = anchor + rect.anchoredPosition;
        rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
        rect.localScale = Vector3.one;
        content.preserveAspect = false; // The source aspect is already preserved by the fitted size.
        rect.sizeDelta = size;
        rect.pivot = new Vector2(spriteCenter.x / sourceSize.x + (position.x - capsule.center.x) / size.x,
            spriteCenter.y / sourceSize.y + (position.y - capsule.center.y) / size.y);
    }

    private static Rect VisualBounds(Image image, Transform parent)
    {
        Sprite sprite = image.sprite;
        if (sprite == null) throw new InvalidOperationException("캡슐 스프라이트가 없습니다.");
        Rect rect = image.rectTransform.rect;
        Vector2 size = sprite.rect.size;
        if (image.preserveAspect)
        {
            float ratio = size.x / size.y;
            if (ratio > rect.width / rect.height)
            {
                float height = rect.width / ratio;
                rect.y += (rect.height - height) * image.rectTransform.pivot.y;
                rect.height = height;
            }
            else
            {
                float width = rect.height * ratio;
                rect.x += (rect.width - width) * image.rectTransform.pivot.x;
                rect.width = width;
            }
        }
        Vector2 normalizedMin = ((Vector2)sprite.bounds.min * sprite.pixelsPerUnit + sprite.pivot) / size;
        Vector2 normalizedMax = ((Vector2)sprite.bounds.max * sprite.pixelsPerUnit + sprite.pivot) / size;
        Vector2 min = rect.min + Vector2.Scale(normalizedMin, rect.size);
        Vector2 max = rect.min + Vector2.Scale(normalizedMax, rect.size);
        Vector2 first = parent.InverseTransformPoint(image.rectTransform.TransformPoint(min));
        Vector2 last = parent.InverseTransformPoint(image.rectTransform.TransformPoint(max));
        return Rect.MinMaxRect(Mathf.Min(first.x, last.x), Mathf.Min(first.y, last.y),
            Mathf.Max(first.x, last.x), Mathf.Max(first.y, last.y));
    }
}
