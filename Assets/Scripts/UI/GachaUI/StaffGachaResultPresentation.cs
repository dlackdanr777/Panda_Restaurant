using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Runtime.CompilerServices;
using TMPro;
using Muks.BackEnd;
using Muks.MobileUI;

/// <summary>
/// Scene-bound presentation only. Financial requests/results are owned by BackendManager.
/// A weak display marker avoids replaying a completed purchase's animation after scene recreation.
/// </summary>
public sealed class StaffGachaPurchaseDisplay
{
    private static readonly ConditionalWeakTable<object, object> Shown
        = new ConditionalWeakTable<object, object>();
    // Explicit result acknowledgement survives scene-view recreation, not account/request lifetime.
    // Hidden or interrupted results are not acknowledged by presentation cleanup.
    private static readonly ConditionalWeakTable<object, object> Acknowledged
        = new ConditionalWeakTable<object, object>();
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
    private GameObject _overlay;
    private UIGachaCard _card;
    private Button _replay, _previous, _next;
    private TextMeshProUGUI _position;
    private bool _captured, _previousStarted;
    private EventSystem _eventSystem;
    private GameObject _previousSelection;

#if UNITY_EDITOR
    public bool EditorIsAnimating => _animation != null && _animation.IsActive && !_animation.IsComplete;
    public bool EditorIsResultVisible => _overlay != null && _overlay.activeInHierarchy;
    public int EditorResultIndex => _sequence?.Index ?? -1;
    public int EditorResultCount => _sequence?.Count ?? 0;
    public StaffGachaAcquisitionItem EditorCurrentItem => _sequence?.CurrentItem;
    public int EditorAnimationStartCount { get; private set; }
    public string EditorAnimationError { get; private set; }
#endif

    public StaffGachaPurchaseDisplay(UIStaffGacha staff, UIGacha view, BackendManager backend)
        : this(staff, view,
            () => backend == null ? null : backend.LastCompletedStaffPurchaseExecution,
            value => backend != null && backend.CanPresentStaffPurchase(value as StaffGachaPurchaseExecution),
            value => ((StaffGachaPurchaseExecution)value).Plan.AccountResult.Acquisition,
            value => ((StaffGachaPurchaseExecution)value).DrawnStaff, null)
    {
    }

    public static StaffGachaPurchaseDisplay ForQuestGrant(UIStaffGacha staff, UIGacha view,
        BackendManager backend, string questId, Action resultClosed)
        => new StaffGachaPurchaseDisplay(staff, view,
            () => backend == null ? null : backend.LastCompletedQuestStaffGrant,
            value => value is QuestStaffGrantExecution grant && grant.QuestId == questId &&
                backend != null && backend.CanPresentQuestStaffGrant(grant),
            value => ((QuestStaffGrantExecution)value).Acquisition,
            value => ((QuestStaffGrantExecution)value).DisplayStaff, resultClosed);

    private StaffGachaPurchaseDisplay(UIStaffGacha staff, UIGacha view,
        Func<object> getCompleted, Func<object, bool> canPresent,
        Func<object, StaffGachaAcquisitionResult> getAcquisition,
        Func<object, IReadOnlyList<GachaStaffData>> getDisplayStaff, Action resultClosed)
    {
        _staff = staff;
        _view = view;
        _getCompleted = getCompleted;
        _canPresent = canPresent;
        _getAcquisition = getAcquisition;
        _getDisplayStaff = getDisplayStaff;
        _resultClosed = resultClosed;
    }

    private bool IsVisible => _staff != null && _view != null &&
        _staff.gameObject.activeInHierarchy && _staff.ResultEntryButton != null &&
        _staff.ResultAnimator != null && _staff.ResultAnimator.enabled &&
        _view.VisibleState == VisibleState.Appeared;

    public void Tick()
    {
        if (!IsVisible) { Suspend(); return; }
        if (_displayed != null && !_canPresent(_displayed))
            Close();
        if (_animation != null)
        {
            try { _animation.Tick(); }
            catch (Exception exception)
            {
                DebugLog.LogError(exception.ToString());
                Close(); // Saved result stays in BackendManager, independently of this display.
            }
        }
        object completed = _getCompleted();
        bool canShow = _canPresent(completed);
        if (canShow && _replay == null)
        {
            TextMeshProUGUI source = _staff.ResultCard.GetComponentInChildren<TextMeshProUGUI>(true);
            if (source != null)
                _replay = CreateReplayButton(_staff.transform, source);
        }
        if (_replay != null) _replay.gameObject.SetActive(canShow && _displayed == null);
        if (!canShow || ReferenceEquals(_observed, completed)) return;
        _observed = completed; // Display failure is not a purchase failure/retry.
        if (Acknowledged.TryGetValue(completed, out _)) return; // Only explicit replay may reopen a closed result.
        // A view recreated/reopened after an interrupted animation reads its fixed card;
        // only the first observation may run the machine animation.
        TryShowCompleted(!Shown.TryGetValue(completed, out _), out _);
    }

    public bool TryShowCompleted(bool animate, out string error)
    {
        error = "획득 결과를 표시할 수 없습니다. 다시 열어 주세요.";
        object completed = _getCompleted();
        if (!IsVisible || !_canPresent(completed) ||
            (_animation != null && !_animation.IsComplete)) return false;
        if (!StaffGachaResultSequence.TryCreateFromCalculated(
            _getAcquisition(completed), _getDisplayStaff(completed), out var sequence, out error))
            return false;
        Close();
        _observed = _displayed = completed;
        _sequence = sequence;
        _previousStarted = _view.IsStartGacha;
        _captured = true;
        _view.SetStartGacha(true); // Existing navigation lock only; X/Back still close the view.
        if (_replay != null) _replay.gameObject.SetActive(false);
        try
        {
            if (animate && !Shown.TryGetValue(completed, out _))
            {
#if UNITY_EDITOR
                EditorAnimationError = null;
#endif
                var animation = new StaffGachaResultAnimation();
                _animation = animation;
                if (animation.TryStart(_staff, sequence, finished =>
                    {
                        if (ReferenceEquals(_animation, animation) &&
                            ReferenceEquals(_sequence, finished) && _canPresent(completed))
                            ShowCards();
                    }, out error))
                {
#if UNITY_EDITOR
                    EditorAnimationStartCount++;
#endif
                    Shown.Add(completed, new object());
                    return true;
                }
#if UNITY_EDITOR
                EditorAnimationError = error;
#endif
                _animation = null;
            }
            ShowCards(); // An unavailable machine animation never discards or re-executes a purchase.
            if (!Shown.TryGetValue(completed, out _)) Shown.Add(completed, new object());
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
#if UNITY_EDITOR
            EditorAnimationError = error;
#endif
            DebugLog.LogError(exception.ToString());
            Close();
            return false;
        }
    }

    private void ShowCards()
    {
        if (_sequence == null || !_canPresent(_displayed)) return;
        if (_overlay == null)
        {
            Canvas canvas = _view.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("획득 카드 Canvas 참조가 없습니다.");
            int order = canvas.rootCanvas.sortingOrder;
            foreach (Canvas child in _view.GetComponentsInChildren<Canvas>(true))
                order = Math.Max(order, child.sortingOrder);
            if (order >= short.MaxValue) throw new InvalidOperationException("획득 카드 정렬 공간이 없습니다.");
            _overlay = new GameObject("Staff Acquisition Results", typeof(RectTransform));
            _overlay.SetActive(false);
            _overlay.transform.SetParent(_view.transform, false);
            Stretch((RectTransform)_overlay.transform);
            Canvas overlayCanvas = _overlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingLayerID = canvas.sortingLayerID;
            overlayCanvas.sortingOrder = order + 1;
            _overlay.AddComponent<GraphicRaycaster>();
            CanvasGroup group = _overlay.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            group.interactable = group.blocksRaycasts = true;
            Image blocker = _overlay.AddComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.7f);
            blocker.raycastTarget = true;

            _card = UnityEngine.Object.Instantiate(_staff.ResultCard, _overlay.transform, false);
            foreach (Animator animator in _card.GetComponentsInChildren<Animator>(true))
            { animator.fireEvents = false; animator.enabled = false; }
            foreach (AudioSource audio in _card.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
            foreach (MonoBehaviour component in _card.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(component is UIGachaCard) && !(component is UIItemStar) &&
                    !(component is Graphic) && !(component is LayoutGroup)) component.enabled = false;
            foreach (Graphic graphic in _card.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            RectTransform rect = (RectTransform)_card.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            TextMeshProUGUI source = _card.GetComponentInChildren<TextMeshProUGUI>(true);
            if (source == null) throw new InvalidOperationException("획득 카드 글꼴 참조가 없습니다.");
            Button close = CreateResultControls(_overlay.transform, source);
            _eventSystem = EventSystem.current;
            _previousSelection = _eventSystem == null ? null : _eventSystem.currentSelectedGameObject;
            if (_eventSystem != null) _eventSystem.SetSelectedGameObject(close.gameObject);
        }
        if (!_card.TrySetStaffAcquisitionResult(_sequence.CurrentStaff, _sequence.CurrentItem))
            throw new InvalidOperationException("직원 획득 결과와 카드 자료가 일치하지 않습니다.");
        _position.text = (_sequence.Index + 1) + "/" + _sequence.Count;
        _previous.interactable = _sequence.CanMovePrevious;
        _next.interactable = _sequence.CanMoveNext;
        _previous.gameObject.SetActive(_sequence.Count > 1);
        _next.gameObject.SetActive(_sequence.Count > 1);
        _card.gameObject.SetActive(true);
        _overlay.SetActive(true);
    }

    public bool Move(int direction)
    {
        if (_sequence == null || !_canPresent(_displayed) ||
            !_sequence.TryMove(direction)) return false;
        ShowCards();
        return true;
    }

    private Button CreateReplayButton(Transform parent, TextMeshProUGUI source)
        => CreateButton(parent, source, "획득 결과", new Vector2(0, -490),
            () => TryShowCompleted(false, out _));

    private Button CreateResultControls(Transform parent, TextMeshProUGUI source)
    {
        _previous = CreateButton(parent, source, "이전", new Vector2(-250, -500), () => Move(-1));
        _next = CreateButton(parent, source, "다음", new Vector2(250, -500), () => Move(1));
        Button close = CreateButton(parent, source, "닫기", new Vector2(0, -500), AcknowledgeResult);
        _position = CreateLabel(parent, source, new Vector2(0, -440), new Vector2(200, 42));
        return close;
    }

    private void AcknowledgeResult()
    {
        // Mark before cleanup/callbacks: a navigation callback may immediately reopen this machine.
        // Do not mark Close/Suspend/Dispose; an unseen or interrupted completion must remain available.
        if (_displayed == null || _overlay == null || !_overlay.activeSelf) return;
        if (!Acknowledged.TryGetValue(_displayed, out _)) Acknowledged.Add(_displayed, new object());
        Close();
        _resultClosed?.Invoke();
    }

    public void Close()
    {
        var animation = _animation;
        _animation = null; // Invalidate completion callbacks before restoring any component.
        bool sameVisible = IsVisible;
        animation?.Close(sameVisible);
        if (_overlay != null)
        {
            _overlay.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(_overlay);
            else UnityEngine.Object.DestroyImmediate(_overlay);
        }
        _overlay = null;
        _card = null;
        _sequence = null;
        _displayed = null;
        if (_captured && sameVisible) _view.SetStartGacha(_previousStarted);
        _captured = false;
        if (_eventSystem != null)
            _eventSystem.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy
                ? _previousSelection : null);
        _eventSystem = null;
        _previousSelection = null;
    }

    public void Suspend()
    {
        Close();
        _observed = null; // A hidden view may observe the same retained completion on its next opening.
    }

    public void Dispose()
    {
        Close();
        if (_replay != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(_replay.gameObject);
            else UnityEngine.Object.DestroyImmediate(_replay.gameObject);
        }
        _replay = null;
    }

    private static Button CreateButton(Transform parent, TextMeshProUGUI source, string text,
        Vector2 position, UnityEngine.Events.UnityAction clicked)
    {
        var obj = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(200, 64);
        obj.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.27f, 1);
        TextMeshProUGUI label = CreateLabel(obj.transform, source, Vector2.zero, rect.sizeDelta);
        label.text = text;
        Button button = obj.GetComponent<Button>();
        button.onClick.AddListener(clicked);
        return button;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, TextMeshProUGUI source,
        Vector2 position, Vector2 size)
    {
        var obj = new GameObject("Label", typeof(RectTransform));
        // Bind the supplied font before TMP Awake can resolve a global/default resource.
        obj.SetActive(false);
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        obj.SetActive(true);
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}


/// <summary>Stored acquisition output plus display bindings. Never rolls or calculates rewards.</summary>
public class StaffGachaResultSequence
{
    private readonly GachaStaffData[] _staff;
    public StaffGachaAcquisitionResult Result { get; }
    public int Index { get; private set; }
    public int Count => Result.Items.Count;
    public GachaStaffData CurrentStaff => _staff[Index];
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
    private AudioClip _resultSound;
    private RectTransform _capsules;
    private Image _staffImage;
    private StaffGachaResultSequence _sequence;
    private Action<StaffGachaResultSequence> _completed;
    private bool _fireEvents, _enabled, _raised, _opening, _boomPlayed;
    private int _originalState;
    private float _originalTime, _raiseTime, _startLength;
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }

    public bool TryStart(UIStaffGacha staff, StaffGachaResultSequence sequence,
        Action<StaffGachaResultSequence> completed, out string error)
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
        _sequence = sequence;
        _completed = completed;
        _fireEvents = animator.fireEvents;
        _enabled = animator.enabled;
        _originalState = original.fullPathHash;
        _originalTime = original.normalizedTime;
        _raiseTime = raise.time;
        _startLength = startClip.length;
        _boom = staff.ResultBoom;
        _resultSound = staff.GetResultSound(sequence.CurrentStaff.Rank);
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
            animator.SetTrigger("Start");
            if (Application.isPlaying) _audio.Play();
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
        if (!_opening && state.fullPathHash == Wait && !_animator.IsInTransition(0))
        {
            _opening = true;
            _audio.Stop();
            _staffImage.gameObject.SetActive(true);
            _capsules.SetSiblingIndex(11);
            _animator.SetTrigger("CapsuleOpen");
        }
        if (!_boomPlayed && state.fullPathHash == Open)
        {
            _boomPlayed = true;
            if (Application.isPlaying && _boom != null) _audio.PlayOneShot(_boom);
        }
        if (state.fullPathHash != Result) return;
        IsComplete = true; // Latch before calling the owner; repeated ticks cannot display again.
        _sequence.UnlockNavigation();
        _staffImage.gameObject.SetActive(false);
        if (Application.isPlaying && _resultSound != null) _audio.PlayOneShot(_resultSound);
        var completed = _completed;
        _completed = null;
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
            Vector3 position = transform.localPosition, scale = transform.localScale;
            Quaternion rotation = transform.localRotation;
            int sibling = transform.GetSiblingIndex();
            bool active = transform.gameObject.activeSelf;
            _restore.Add(() =>
            {
                if (transform == null) return;
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
                    _restore.Add(() => { if (image != null) image.sprite = sprite; });
                }
            }
            _restore.Add(() => { if (transform != null && transform.gameObject.activeSelf != active)
                transform.gameObject.SetActive(active); });
        }
    }

}
