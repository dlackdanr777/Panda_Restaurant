#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>현재 직원머신의 카드 복사본에만 계산 결과를 표시하는 Editor 전용 도구.</summary>
public sealed class StaffGachaAcquisitionPreviewWindow : EditorWindow
{
    private const double SettleSeconds = 0.2;
    private static readonly FieldInfo CurrentMachineField = typeof(UIGacha)
        .GetField("_currentGachaMachine", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ExecutionEnabledField = typeof(UIStaffGacha)
        .GetField("IsGachaExecutionEnabled", BindingFlags.Static | BindingFlags.NonPublic);

    private UIGacha _observedGacha;
    private UIStaffGacha _observedStaff;
    private Vector3 _lastMachineScale;
    private Vector2 _lastParentPosition;
    private double _stableSince;
    private UIGacha _gacha;
    private UIStaffGacha _staff;
    private CanvasGroup _parentInput;
    private bool _previousInteractable;
    private bool _previousPopEnabled;
    private bool _inputCaptured;
    private EventSystem _eventSystem;
    private GameObject _previousSelection;
    private GameObject _overlay;
    private UIGachaCard _previewCard;
    private StaffGachaAcquisitionPreviewSequence _sequence;
    private StaffGachaSingleAnimationPreview _animationPreview;
    private string _singleStaffId;
    private string _message = "Play Mode에서 직원머신을 열고 전환이 끝난 뒤 사용하세요.";

    [MenuItem("Tools/Panda Restaurant/Staff Gacha/Acquisition Result Preview")]
    private static void OpenWindow()
    {
        GetWindow<StaffGachaAcquisitionPreviewWindow>("Staff Result Preview");
    }

    private void OnEnable()
    {
        EditorApplication.update += Observe;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += ClosePreview;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Observe;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= ClosePreview;
        ClosePreview();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            ClosePreview();
    }

    private void OnGUI()
    {
        // Escape must not unlock the gacha during the same input frame.
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            Event.current.Use();

        EditorGUILayout.HelpBox("테스트 미리보기입니다. 실제 보유·재화·지급·저장을 변경하지 않습니다.", MessageType.Info);
        bool available = TryGetContext(out UIGacha gacha, out UIStaffGacha staff,
            out _, out _, out string reason) && IsSettled(gacha, staff);
        if (!available && _animationPreview == null)
            EditorGUILayout.HelpBox(reason ?? "직원머신 전환이 끝날 때까지 기다려 주세요.", MessageType.None);
        using (new EditorGUI.DisabledScope(!available || _animationPreview != null))
        {
            // Select existing staff for long-description checks; the fixed eleven fixture is unchanged.
            GachaStaffData[] singleCandidates = StaffGachaAcquisitionPreviewSequence
                .GetDisplayCandidates(staff == null ? null : staff.ItemDataList).ToArray();
            if (singleCandidates.Length > 0)
            {
                int selected = Mathf.Max(0, Array.FindIndex(singleCandidates, data => data.Id == _singleStaffId));
                selected = EditorGUILayout.Popup("단일 미리보기 직원", selected,
                    singleCandidates.Select(data => data.Id + " · " + data.Name).ToArray());
                _singleStaffId = singleCandidates[selected].Id;
            }
            if (GUILayout.Button("신규 획득 미리보기")) ShowResult(false);
            if (GUILayout.Button("중복 획득 미리보기")) ShowResult(true);
            if (GUILayout.Button("11회 결과 미리보기")) ShowResult(false, true);
            if (GUILayout.Button("1회 연출 미리보기")) ShowAnimationPreview();
            if (GUILayout.Button("11회 연출 미리보기")) ShowAnimationPreview(true);
        }
        if (_sequence != null)
        {
            EditorGUILayout.LabelField($"{_sequence.Index + 1}/{_sequence.Count}", EditorStyles.boldLabel);
            int newCount = _sequence.Result.NewStaffIds.Count;
            EditorGUILayout.LabelField($"신규 {newCount}명 / 중복 {_sequence.Count - newCount}명 / " +
                $"판다토큰 {_sequence.Result.TotalPandaTokens}", EditorStyles.wordWrappedLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool canNavigate = _animationPreview == null ? available :
                    _animationPreview.IsComplete && HasAnimationContext() &&
                    EditorApplication.isPlaying && !EditorApplication.isPaused;
                using (new EditorGUI.DisabledScope(!canNavigate || !_sequence.CanMovePrevious))
                    if (GUILayout.Button("이전")) MoveResult(-1);
                using (new EditorGUI.DisabledScope(!canNavigate || _sequence == null || !_sequence.CanMoveNext))
                    if (GUILayout.Button("다음")) MoveResult(1);
            }
        }
        using (new EditorGUI.DisabledScope(_overlay == null))
            if (GUILayout.Button("미리보기 닫기")) ClosePreview();
        EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
    }

    private void Observe()
    {
        if (_animationPreview != null)
        {
            if (!EditorApplication.isPlaying || !HasAnimationContext())
                ClosePreview();
            else if (!EditorApplication.isPaused)
            {
                try { _animationPreview.Tick(); }
                catch (Exception exception) { ClosePreview(); _message = exception.Message; }
            }
            Repaint();
            return;
        }
        if (!TryGetContext(out UIGacha gacha, out UIStaffGacha staff,
            out _, out RectTransform machineParent, out _))
        {
            _observedGacha = null;
            _observedStaff = null;
            if (_inputCaptured || _overlay != null) ClosePreview();
            Repaint();
            return;
        }
        Vector3 scale = staff.transform.localScale;
        Vector2 position = machineParent.anchoredPosition;
        if (_observedGacha != gacha || _observedStaff != staff ||
            (scale - _lastMachineScale).sqrMagnitude > 0.000001f ||
            (position - _lastParentPosition).sqrMagnitude > 0.000001f)
        {
            _observedGacha = gacha;
            _observedStaff = staff;
            _lastMachineScale = scale;
            _lastParentPosition = position;
            _stableSince = EditorApplication.timeSinceStartup;
        }
        if (_inputCaptured && (_overlay == null || gacha != _gacha || staff != _staff))
            ClosePreview();
        Repaint();
    }

    private bool IsSettled(UIGacha gacha, UIStaffGacha staff)
    {
        return gacha != null && staff != null && gacha == _observedGacha && staff == _observedStaff &&
            (staff.transform.localScale - Vector3.one).sqrMagnitude < 0.000001f &&
            EditorApplication.timeSinceStartup - _stableSince >= SettleSeconds;
    }

    private bool HasAnimationContext()
    {
        // The real machine buttons remain hidden until close. Their visibility is not a
        // result-navigation condition for the session that already owns this machine.
        return _animationPreview != null && _animationPreview.IsActive &&
            _gacha != null && _staff != null && _overlay != null && _previewCard != null &&
            _inputCaptured && _gacha.VisibleState == VisibleState.Appeared &&
            _staff.gameObject.activeInHierarchy && CurrentMachineField.GetValue(_gacha) == _staff;
    }

    private bool TryGetContext(out UIGacha gacha, out UIStaffGacha staff,
        out UIGachaCard sourceCard, out RectTransform machineParent, out string reason)
    {
        gacha = null;
        staff = null;
        sourceCard = null;
        machineParent = null;
        reason = "Play Mode의 열린 직원머신에서만 사용할 수 있습니다.";
        if (!EditorApplication.isPlaying || EditorApplication.isPaused ||
            CurrentMachineField == null || ExecutionEnabledField == null ||
            !(ExecutionEnabledField.GetValue(null) is bool enabled) || enabled)
            return false;

        foreach (UIGacha view in Object.FindObjectsByType<UIGacha>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (view.VisibleState != VisibleState.Appeared || view.IsStartGacha) continue;
            if (gacha != null) return false;
            gacha = view;
        }
        if (gacha == null || !(CurrentMachineField.GetValue(gacha) is UIStaffGacha selected) ||
            !selected.gameObject.activeInHierarchy)
            return false;
        staff = selected;
        sourceCard = ReadReference<UIGachaCard>(staff, "_gachaCard");
        machineParent = ReadReference<RectTransform>(gacha, "_machineParent");
        CanvasGroup input = ReadReference<CanvasGroup>(gacha, "_canvasGroup");
        Button single = ReadReference<Button>(staff, "_singleButton");
        if (sourceCard == null || sourceCard.gameObject.activeSelf || machineParent == null || input == null ||
            single == null || !single.gameObject.activeInHierarchy ||
            (!input.interactable && !(_inputCaptured && _gacha == gacha)))
            return false;
        reason = null;
        return true;
    }

    private void ShowResult(bool duplicate, bool eleven = false)
    {
        if (_animationPreview != null) return;
        if (!TryGetContext(out UIGacha gacha, out UIStaffGacha staff,
            out UIGachaCard sourceCard, out _, out string reason) || !IsSettled(gacha, staff))
        {
            _message = reason ?? "직원머신 전환 중에는 미리보기를 열 수 없습니다.";
            return;
        }
        try
        {
            // Each open calculates once; navigation below only reads this stored sequence.
            StaffGachaAcquisitionPreviewSequence sequence;
            string error;
            bool calculated = eleven
                ? StaffGachaAcquisitionPreviewSequence.TryCreateFixedEleven(staff.ItemDataList, out sequence, out error)
                : StaffGachaAcquisitionPreviewSequence.TryCreateSingle(
                    StaffGachaAcquisitionPreviewSequence.GetDisplayCandidates(staff.ItemDataList)
                        .FirstOrDefault(data => data.Id == _singleStaffId),
                    duplicate, out sequence, out error);
            if (!calculated)
                throw new InvalidOperationException(error);
            if (_overlay == null) CreateOverlay(gacha, staff, sourceCard);
            _sequence = sequence;
            DisplayCurrentResult();
        }
        catch (Exception exception)
        {
            ClosePreview();
            _message = exception.Message;
        }
    }

    private void ShowAnimationPreview(bool eleven = false)
    {
        // A session owns the machine until close, including the displayed result.
        if (_animationPreview != null) return;
        if (!TryGetContext(out UIGacha gacha, out UIStaffGacha staff,
            out UIGachaCard sourceCard, out _, out string reason) || !IsSettled(gacha, staff))
        {
            _message = reason ?? "직원머신 전환이 끝난 뒤 사용하세요.";
            return;
        }
        try
        {
            StaffGachaAcquisitionPreviewSequence sequence;
            string error;
            bool calculated = eleven
                ? StaffGachaAcquisitionPreviewSequence.TryCreateFixedEleven(staff.ItemDataList, out sequence, out error)
                : StaffGachaAcquisitionPreviewSequence.TryCreateSingle(
                    StaffGachaAcquisitionPreviewSequence.GetDisplayCandidates(staff.ItemDataList)
                        .FirstOrDefault(data => data.Id == _singleStaffId), false, out sequence, out error);
            if (!calculated)
                throw new InvalidOperationException(error);
            if (_overlay == null) CreateOverlay(gacha, staff, sourceCard);
            _sequence = sequence;
            _previewCard.gameObject.SetActive(false);
            _overlay.GetComponent<Image>().color = Color.clear;
            _overlay.SetActive(true); // Transparent input blocker; the original machine stays visible.
            var session = new StaffGachaSingleAnimationPreview();
            _animationPreview = session;
            if (!session.TryStart(staff, sequence, completed =>
            {
                // Closed/replaced sessions cannot reopen a card, even through a retained delegate.
                if (_animationPreview != session || _overlay == null || _sequence != completed) return;
                DisplayCurrentResult();
            }, out error))
                throw new InvalidOperationException(error);
            _message = "[테스트 미리보기] " + sequence.CurrentItem.StaffId + " · 머신 연출 중";
        }
        catch (Exception exception)
        {
            ClosePreview();
            _message = exception.Message;
        }
    }

    private void MoveResult(int offset)
    {
        if (_sequence == null || _previewCard == null || _overlay == null) return;
        if (_animationPreview != null)
        {
            if (!_animationPreview.IsComplete || !HasAnimationContext() ||
                !EditorApplication.isPlaying || EditorApplication.isPaused) return;
        }
        else if (!TryGetContext(out UIGacha gacha, out UIStaffGacha staff, out _, out _, out _) ||
            !IsSettled(gacha, staff)) return;
        if (!_sequence.TryMove(offset)) return;
        try
        {
            DisplayCurrentResult();
        }
        catch (Exception exception)
        {
            ClosePreview();
            _message = exception.Message;
        }
    }

    private void DisplayCurrentResult()
    {
        if (!_previewCard.TrySetStaffAcquisitionResult(_sequence.CurrentStaff, _sequence.CurrentItem, true))
            throw new InvalidOperationException("카드가 획득 계산 결과를 표시하지 못했습니다.");
        _previewCard.gameObject.SetActive(true);
        _overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        _overlay.SetActive(true);
        _message = "[테스트 미리보기] " + _sequence.CurrentItem.StaffId +
            (_sequence.CurrentItem.IsNew ? " · 신규 획득" : " · 중복 획득");
    }

    private void CreateOverlay(UIGacha gacha, UIStaffGacha staff, UIGachaCard sourceCard)
    {
        Canvas canvas = gacha.GetComponentInParent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("가챠 Canvas 참조가 없습니다.");
        canvas = canvas.rootCanvas;
        int highestOrder = canvas.sortingOrder;
        foreach (Canvas childCanvas in gacha.GetComponentsInChildren<Canvas>(true))
            highestOrder = Mathf.Max(highestOrder, childCanvas.sortingOrder);
        if (highestOrder >= short.MaxValue)
            throw new InvalidOperationException("미리보기를 올릴 Canvas 정렬 여유가 없습니다.");
        _gacha = gacha;
        _staff = staff;
        _overlay = new GameObject("Staff Acquisition Test Preview", typeof(RectTransform));
        _overlay.hideFlags = HideFlags.DontSave;
        _overlay.SetActive(false);
        _overlay.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)_overlay.transform);
        Canvas overlayCanvas = _overlay.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingLayerID = canvas.sortingLayerID;
        overlayCanvas.sortingOrder = highestOrder + 1;
        _overlay.AddComponent<GraphicRaycaster>();
        CanvasGroup overlayInput = _overlay.AddComponent<CanvasGroup>();
        overlayInput.ignoreParentGroups = true;
        overlayInput.interactable = true;
        overlayInput.blocksRaycasts = true;
        Image blocker = _overlay.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.7f);
        blocker.raycastTarget = true;

        _previewCard = Instantiate(sourceCard, _overlay.transform, false);
        _previewCard.name = "Staff Result Card (Test Preview)";
        foreach (MonoBehaviour component in _previewCard.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component is RotationGameObject rotation) rotation.enabled = false;
            else if (!(component is UIGachaCard) && !(component is UIItemStar) &&
                !(component is Graphic) && !(component is LayoutGroup))
                throw new InvalidOperationException("미리보기 카드에 예상하지 못한 스크립트가 있습니다: " + component.GetType().Name);
        }
        foreach (Animator animator in _previewCard.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (AudioSource audio in _previewCard.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (Graphic graphic in _previewCard.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        if (ReadReference<Button>(_previewCard, "_closeButton") != null)
            throw new InvalidOperationException("원본 닫기 연결이 있는 카드는 미리보기에 사용할 수 없습니다.");
        RectTransform cardRect = (RectTransform)_previewCard.transform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        // Instantiate preserves the source card size and scale (currently 465 x 617, scale 1.3).
        TextMeshProUGUI description = ReadReference<TextMeshProUGUI>(_previewCard, "_descriptionText");
        if (description == null) throw new InvalidOperationException("설명 텍스트 참조가 없습니다.");

        Button close = CreateCloseButton(description);
        _parentInput = ReadReference<CanvasGroup>(gacha, "_canvasGroup");
        _previousInteractable = _parentInput.interactable;
        _previousPopEnabled = gacha.PopEnabled;
        _eventSystem = EventSystem.current;
        _previousSelection = _eventSystem == null ? null : _eventSystem.currentSelectedGameObject;
        _inputCaptured = true;
        _parentInput.interactable = false;
        gacha.PopEnabled = false;
        if (_eventSystem != null) _eventSystem.SetSelectedGameObject(close.gameObject);
    }

    private Button CreateCloseButton(TextMeshProUGUI sourceText)
    {
        var buttonObject = new GameObject("Close Test Preview", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(_overlay.transform, false);
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300f, 64f);
        rect.anchoredPosition = new Vector2(0f, -470f);
        buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.2f, 0.27f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.onClick.AddListener(ClosePreview);
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        Stretch((RectTransform)labelObject.transform);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = sourceText.font;
        label.fontSharedMaterial = sourceText.fontSharedMaterial;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "미리보기 닫기";
        return button;
    }

    private void ClosePreview()
    {
        var animation = _animationPreview;
        _animationPreview = null; // Invalidate the completion callback before restoring the machine.
        bool sameVisibleMachine = _gacha != null && _staff != null &&
            _gacha.VisibleState == VisibleState.Appeared && _staff.gameObject.activeInHierarchy &&
            CurrentMachineField.GetValue(_gacha) == _staff;
        animation?.Close(sameVisibleMachine);
        if (_overlay != null) _overlay.SetActive(false);
        if (_inputCaptured)
        {
            _inputCaptured = false;
            if (_parentInput != null && (animation == null || sameVisibleMachine))
                _parentInput.interactable = _previousInteractable;
            if (_gacha != null) _gacha.PopEnabled = _previousPopEnabled;
            if (_eventSystem != null)
                _eventSystem.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy
                    ? _previousSelection : null);
        }
        if (_overlay != null) DestroyImmediate(_overlay);
        _overlay = null;
        _previewCard = null;
        _gacha = null;
        _staff = null;
        _parentInput = null;
        _eventSystem = null;
        _previousSelection = null;
        _sequence = null;
        _message = "미리보기를 닫았습니다. 다시 열면 첫 결과부터 표시합니다.";
    }

    private static T ReadReference<T>(Object owner, string field) where T : Object
    {
        using (var serialized = new SerializedObject(owner))
            return serialized.FindProperty(field)?.objectReferenceValue as T;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

/// <summary>
/// One existing machine animation for a stored single/eleven result list, without gameplay AnimationEvents.
/// No coroutines/delayed delegates are scheduled: close invalidates the owner and stops the Animator.
/// </summary>
internal sealed class StaffGachaSingleAnimationPreview
{
    private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
    private static readonly int Start = Animator.StringToHash("Base Layer.Start_Gacha");
    private static readonly int Wait = Animator.StringToHash("Base Layer.Wait_Gacha");
    private static readonly int Open = Animator.StringToHash("Base Layer.Open_Gacha");
    private static readonly int Result = Animator.StringToHash("Base Layer.ZoomIn Item");
    private readonly List<Action> _restore = new List<Action>();
    private Animator _animator;
    private AudioSource _audio;
    private AudioClip _boom;
    private AudioClip _resultSound;
    private RectTransform _capsules;
    private Image _staffImage;
    private StaffGachaAcquisitionPreviewSequence _sequence;
    private Action<StaffGachaAcquisitionPreviewSequence> _completed;
    private bool _fireEvents, _enabled, _raised, _opening, _boomPlayed;
    private int _originalState;
    private float _originalTime, _raiseTime, _startLength;
    public bool IsActive { get; private set; }
    public bool IsComplete { get; private set; }

    public bool TryStart(UIStaffGacha staff, StaffGachaAcquisitionPreviewSequence sequence,
        Action<StaffGachaAcquisitionPreviewSequence> completed, out string error)
    {
        error = "현재 직원머신에서 보관된 결과의 연출을 시작할 수 없습니다.";
        if (IsActive || staff == null || !staff.gameObject.activeInHierarchy || sequence == null ||
            (sequence.Count != 1 && sequence.Count != 11) || sequence.Index != 0 ||
            sequence.IsNavigationLocked || !sequence.CurrentItem.IsNew || completed == null)
            return false;
        Animator animator = Read<Animator>(staff, "_gachaMacineAnimator");
        UIGachaCard card = Read<UIGachaCard>(staff, "_gachaCard");
        Image staffImage = Read<Image>(staff, "_getStaffImage");
        RectTransform capsules = Read<RectTransform>(staff, "_capsules");
        Button single = Read<Button>(staff, "_singleButton");
        AudioSource audio = Read<AudioSource>(staff, "_gachaSound");
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
        AnimationEvent raise = startClip == null ? null : AnimationUtility.GetAnimationEvents(startClip)
            .FirstOrDefault(evt => evt.functionName == "CapsuleSetSibilingIndex" && evt.intParameter == 6);
        if (raise == null) { error = "기존 머신 캡슐 연출 이벤트 정보를 찾지 못했습니다."; return false; }

        _animator = animator;
        _audio = audio;
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
        _boom = Read<AudioClip>(staff, "_boomSound");
        _resultSound = Read<AudioClip>(staff, sequence.CurrentStaff.Rank == Rank.Unique ||
            sequence.CurrentStaff.Rank == Rank.Special ? "_getSpecialStaffSound" : "_getNormalStaffSound");
        CapturePresentation(staff.transform);
        IsActive = true;
        IsComplete = _raised = _opening = _boomPlayed = false;
        sequence.LockNavigation();
        try
        {
            // Never enter UIStaffGacha.SetStep/GetStaff/StartAddStaff or their tutorial/grant hooks.
            animator.fireEvents = false;
            ResetTriggers();
            foreach (string field in new[] { "_singleButton", "_tenButton", "_screenButton", "_skipButton" })
            {
                Button button = Read<Button>(staff, field);
                if (button != null) button.gameObject.SetActive(false);
            }
            card.gameObject.SetActive(false);
            Transform slots = Read<Transform>(staff, "_getStaffSlotFrame");
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
        if (_audio != null) _audio.Stop();
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

    private static T Read<T>(Object owner, string field) where T : Object
    {
        using (var serialized = new SerializedObject(owner))
            return serialized.FindProperty(field)?.objectReferenceValue as T;
    }
}

/// <summary>Editor 표시용 고정 입력과 계산 결과. 이동은 인덱스만 바꾸며 다시 계산하지 않는다.</summary>
internal sealed class StaffGachaAcquisitionPreviewSequence
{
    private readonly GachaStaffData[] _staff;
    public StaffGachaAcquisitionResult Result { get; }
    public int Index { get; private set; }
    public int Count => Result.Items.Count;
    public GachaStaffData CurrentStaff => _staff[Index];
    public StaffGachaAcquisitionItem CurrentItem => Result.Items[Index];
    internal bool IsNavigationLocked { get; private set; }
    public bool CanMovePrevious => !IsNavigationLocked && Index > 0;
    public bool CanMoveNext => !IsNavigationLocked && Index + 1 < Count;

    internal void LockNavigation() => IsNavigationLocked = true;
    internal void UnlockNavigation() => IsNavigationLocked = false;

    private StaffGachaAcquisitionPreviewSequence(GachaStaffData[] staff, StaffGachaAcquisitionResult result)
    {
        _staff = staff;
        Result = result;
    }

    internal static IEnumerable<GachaStaffData> GetDisplayCandidates(IEnumerable<GachaData> candidates)
    {
        return (candidates ?? Enumerable.Empty<GachaData>()).OfType<GachaStaffData>()
            .Where(data => data != null && data.StaffData != null &&
                !string.IsNullOrWhiteSpace(data.Id) && !data.Id.Any(char.IsWhiteSpace) &&
                string.Equals(data.Id, data.StaffData.Id, StringComparison.Ordinal) &&
                data.Rank == data.StaffData.Rank &&
                (data.Rank == Rank.Normal1 || data.Rank == Rank.Normal2 || data.Rank == Rank.Rare ||
                    data.Rank == Rank.Unique || data.Rank == Rank.Special) &&
                (data.ThumbnailSprite != null || data.Sprite != null))
            .OrderBy(data => data.Id, StringComparer.Ordinal);
    }

    public static bool TryCreateFixedEleven(IEnumerable<GachaData> candidates,
        out StaffGachaAcquisitionPreviewSequence sequence, out string error)
    {
        sequence = null;
        GachaStaffData[] valid = GetDisplayCandidates(candidates).ToArray();
        GachaStaffData a = valid.FirstOrDefault(data => data.Rank == Rank.Normal1 || data.Rank == Rank.Normal2);
        GachaStaffData b = valid.FirstOrDefault(data => data.Rank == Rank.Rare && data.Id != a?.Id);
        if (a == null || b == null)
        {
            error = "11회 미리보기에는 서로 다른 실제 ID의 유효한 노멀·레어 직원이 필요합니다.";
            return false;
        }

        // Display fixture only, not a roll or rarity guarantee: B, B, A x 9; owns A only.
        var staff = new GachaStaffData[11];
        staff[0] = staff[1] = b;
        for (int i = 2; i < staff.Length; i++) staff[i] = a;
        return TryCreate(new[] { a.Id }, staff, out sequence, out error);
    }

    public static bool TryCreateSingle(GachaStaffData staff, bool duplicate,
        out StaffGachaAcquisitionPreviewSequence sequence, out string error)
    {
        return TryCreate(duplicate && staff != null ? new[] { staff.Id } : Array.Empty<string>(),
            new[] { staff }, out sequence, out error);
    }

    private static bool TryCreate(IReadOnlyCollection<string> owned, GachaStaffData[] staff,
        out StaffGachaAcquisitionPreviewSequence sequence, out string error)
    {
        sequence = null;
        if (!StaffGachaAcquisitionCalculator.TryCalculate(owned, staff, out var result, out error))
            return false;
        sequence = new StaffGachaAcquisitionPreviewSequence(staff, result);
        return true;
    }

    public bool TryMove(int offset)
    {
        if (IsNavigationLocked || (offset != -1 && offset != 1) || (offset == -1 && !CanMovePrevious) ||
            (offset == 1 && !CanMoveNext))
            return false;
        Index += offset;
        return true;
    }
}
#endif
