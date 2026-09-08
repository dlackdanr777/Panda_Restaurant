#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class StaffGachaAcquisitionCardTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly List<Object> _createdObjects = new List<Object>();
    private readonly Image[] _frames = new Image[4];
    private readonly GameObject[] _stars = new GameObject[5];
    private UIGachaCard _card;
    private Image _image;
    private TextMeshProUGUI _nameText;
    private TextMeshProUGUI _descriptionText;
    private TextMeshProUGUI _effectText;
    private TextMeshProUGUI _typeText;
    private StaffDataManager _originalManager;
    private Random.State _randomState;
    private string _userState;
    private int _mutationEvents;
    private Action _mutationHandler;
    private Action<ERestaurantFloorType, EquipStaffType> _equipHandler;

    [SetUp]
    public void SetUp()
    {
        _userState = ReadUserState();
        _mutationEvents = 0;
        _mutationHandler = () => _mutationEvents++;
        _equipHandler = (floor, type) => _mutationEvents++;
        SubscribeMutationEvents();

        // GetStaffGroupType only checks the real asset's runtime type. An inactive
        // temporary instance avoids Awake/Resources initialization or scene loading.
        FieldInfo managerField = GetManagerInstanceField();
        _originalManager = (StaffDataManager)managerField.GetValue(null);
        if (_originalManager == null)
        {
            GameObject managerObject = Track(new GameObject("Staff card test manager"));
            managerObject.SetActive(false);
            managerField.SetValue(null, managerObject.AddComponent<StaffDataManager>());
        }

        GameObject root = Track(new GameObject("Staff card test", typeof(RectTransform)));
        root.SetActive(false);
        _card = root.AddComponent<UIGachaCard>();
        SetField(_card, "_rectTransform", root.GetComponent<RectTransform>());
        string[] frameFields = { "_star1Frame", "_star3Frame", "_star4Frame", "_star5Frame" };
        for (int i = 0; i < _frames.Length; i++)
        {
            _frames[i] = CreateChild<Image>(root.transform, frameFields[i]);
            SetField(_card, frameFields[i], _frames[i]);
        }
        _image = CreateChild<Image>(root.transform, "Image");
        _nameText = CreateChild<TextMeshProUGUI>(root.transform, "Name");
        _descriptionText = CreateChild<TextMeshProUGUI>(root.transform, "Description");
        _effectText = CreateChild<TextMeshProUGUI>(root.transform, "Effect");
        _typeText = CreateChild<TextMeshProUGUI>(root.transform, "Type");
        SetField(_card, "_skinImage", _image);
        SetField(_card, "_nameText", _nameText);
        SetField(_card, "_descriptionText", _descriptionText);
        SetField(_card, "_effectText", _effectText);
        SetField(_card, "_typeText", _typeText);
        UIItemStar itemStar = CreateChild<UIItemStar>(root.transform, "Stars");
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i] = CreateChild<Image>(itemStar.transform, "Star " + (i + 1)).gameObject;
            SetField(itemStar, "_star" + (i + 1), _stars[i]);
        }
        SetField(_card, "_itemStar", itemStar);
        _randomState = Random.state;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomState), "Card binding consumed randomness");
            Assert.That(ReadUserState(), Is.EqualTo(_userState), "Actual user state changed");
            Assert.That(_mutationEvents, Is.Zero, "User mutation event was raised");
        }
        finally
        {
            UnsubscribeMutationEvents();
            GetManagerInstanceField().SetValue(null, _originalManager);
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            _createdObjects.Clear();
        }
    }

    [Test]
    public void NewAcquisition_BindsRegisteredStaffNameImageRankAndStatus()
    {
        // Registered resources, not fabricated employees or actual account grants.
        GachaStaffData normal = LoadStaff("STAFF01");
        GachaStaffData special = LoadStaff("STAFF25");
        GachaStaffData longDescription = LoadStaff("STAFF28");
        Assert.That(normal.Rank, Is.EqualTo(Rank.Normal2));
        Assert.That(special.Rank, Is.EqualTo(Rank.Special));

        foreach (GachaStaffData staff in new[] { normal, special, longDescription })
        {
            StaffGachaAcquisitionItem item = Calculate(staff, false);
            Assert.That(_card.TrySetStaffAcquisitionResult(staff, item, true), Is.True);
            Assert.That(_nameText.text, Is.EqualTo(staff.Name));
            Assert.That(_image.sprite, Is.SameAs(staff.ThumbnailSprite ?? staff.Sprite));
            Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n신규 획득"));
            Assert.That(_effectText.text, Is.EqualTo(Utility.GetStaffEffectDescription(staff.StaffData, 1)),
                "Acquisition shows existing base ability without reading account-owned levels");
            Assert.That(_effectText.text, Is.Not.Empty.And.Not.Contains("???"));
            Assert.That(_typeText.text, Is.EqualTo(Utility.StaffTypeStringConverter(
                StaffDataManager.Instance.GetStaffGroupType(staff.StaffData))));
            AssertRank(staff == normal ? 0 : staff == special ? 3 : 2,
                staff == normal ? 2 : staff == special ? 5 : 4);
        }
    }

    [Test]
    public void ReusedCard_ReplacesDuplicateNewAndGenericDescriptionsWithoutStaleStatus()
    {
        GachaStaffData staff = LoadStaff("STAFF01");
        StaffGachaAcquisitionItem duplicate = Calculate(staff, true);
        StaffGachaAcquisitionItem acquired = Calculate(staff, false);
        Assert.That(duplicate.PandaTokenReward, Is.EqualTo(5));

        Assert.That(_card.TrySetStaffAcquisitionResult(staff, duplicate, true), Is.True);
        Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n중복 획득\n판다토큰 +5"));
        Assert.That(_card.TrySetStaffAcquisitionResult(staff, acquired, true), Is.True);
        Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n신규 획득"));
        Assert.That(_card.TrySetStaffAcquisitionResult(staff, duplicate), Is.True);
        Assert.That(_descriptionText.text, Is.EqualTo("중복 획득\n판다토큰 +5"));

        // Only a generic UI fixture; this object is never a staff candidate or grant.
        var generic = Track(ScriptableObject.CreateInstance<StaffGachaCardGenericTestData>());
        generic.Initialize(staff.Sprite);
        _card.SetData(generic);
        Assert.That(_descriptionText.text, Is.EqualTo("Original generic description"));
        Assert.That(_nameText.text, Is.EqualTo("Generic card"));
        Assert.That(_image.sprite, Is.SameAs(staff.Sprite));
        Assert.That(_typeText.text, Is.EqualTo("아이템"));
        Assert.That(_effectText.text, Is.Empty);
        AssertRank(1, 3);
    }

    [Test]
    public void InvalidBinding_ClearsPreviousStaffForNullIdAndRankMismatch()
    {
        GachaStaffData staff = LoadStaff("STAFF01");
        StaffGachaAcquisitionItem duplicate = Calculate(staff, true);
        StaffGachaAcquisitionItem otherId = Calculate(LoadStaff("STAFF02"), false);
        GachaStaffData wrongRank = Track(GachaStaffData.Create(staff.StaffData));
        // Mutate only the disposable wrapper. The registered StaffData remains intact.
        typeof(GachaData).GetField("_rank", PrivateInstance).SetValue(wrongRank, Rank.Special);

        var invalidBindings = new[]
        {
            new KeyValuePair<GachaStaffData, StaffGachaAcquisitionItem>(null, duplicate),
            new KeyValuePair<GachaStaffData, StaffGachaAcquisitionItem>(staff, null),
            new KeyValuePair<GachaStaffData, StaffGachaAcquisitionItem>(staff, otherId),
            new KeyValuePair<GachaStaffData, StaffGachaAcquisitionItem>(wrongRank, duplicate)
        };
        foreach (var binding in invalidBindings)
        {
            Assert.That(_card.TrySetStaffAcquisitionResult(staff, duplicate, true), Is.True);
            Assert.That(_card.TrySetStaffAcquisitionResult(binding.Key, binding.Value, true), Is.False);
            Assert.That(_image.sprite, Is.Null);
            Assert.That(_nameText.text, Is.Empty);
            Assert.That(_descriptionText.text, Is.Empty);
            Assert.That(_effectText.text, Is.Empty);
            Assert.That(_typeText.text, Is.Empty);
            AssertRank(0, 1);
        }
        Assert.That(staff.StaffData.Rank, Is.EqualTo(Rank.Normal2));
    }

    [Test]
    public void FixedElevenPreview_PreservesCalculatedOrderAcrossNavigationAndResetsNewSequence()
    {
        GachaStaffData normal = LoadStaff("STAFF01");
        GachaStaffData rare = LoadStaff("STAFF23");
        Assert.That(normal.Rank, Is.EqualTo(Rank.Normal2));
        Assert.That(rare.Rank, Is.EqualTo(Rank.Rare));
        Random.State randomBefore = Random.state;
        // Reverse resource order deliberately; the fixture is B, B, A x 9.
        GachaData[] available = { rare, normal };
        Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateFixedEleven(
            available, out var sequence, out string error), Is.True, error);
        StaffGachaAcquisitionResult result = sequence.Result;
        Assert.That(sequence.Count, Is.EqualTo(11));
        CollectionAssert.AreEqual(new[] { rare.Id, rare.Id }.Concat(Enumerable.Repeat(normal.Id, 9)),
            result.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { Rank.Rare, Rank.Rare }.Concat(Enumerable.Repeat(Rank.Normal2, 9)),
            result.Items.Select(item => item.Rank));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            result.Items.Select(item => item.PandaTokenReward));
        CollectionAssert.AreEqual(new[] { rare.Id }, result.NewStaffIds);
        Assert.That(result.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(result.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        Assert.That(result.TotalPandaTokens, Is.EqualTo(55));

        void AssertPage(StaffGachaAcquisitionPreviewSequence current,
            StaffGachaAcquisitionResult original, string description)
        {
            Assert.That(current.Result, Is.SameAs(original), "Navigation replaced the calculated result");
            Assert.That(current.CurrentItem, Is.SameAs(original.Items[current.Index]));
            Assert.That(current.CurrentStaff.Id, Is.EqualTo(current.CurrentItem.StaffId));
            Assert.That(current.CurrentStaff.Rank, Is.EqualTo(current.CurrentItem.Rank));
            Assert.That(_card.TrySetStaffAcquisitionResult(
                current.CurrentStaff, current.CurrentItem, true), Is.True);
            Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n" + description));
            Assert.That(_effectText.text,
                Is.EqualTo(Utility.GetStaffEffectDescription(current.CurrentStaff.StaffData, 1)));
            Assert.That(_effectText.text, Is.Not.Empty.And.Not.Contains("???"));
            Assert.That(_nameText.text, Is.EqualTo(current.CurrentStaff.Name));
            Assert.That(_image.sprite, Is.SameAs(
                current.CurrentStaff.ThumbnailSprite ?? current.CurrentStaff.Sprite));
            Assert.That(Random.state, Is.EqualTo(randomBefore));
        }

        Assert.That(sequence.Index, Is.Zero);
        Assert.That(sequence.CanMovePrevious, Is.False);
        Assert.That(sequence.CanMoveNext, Is.True);
        Assert.That(sequence.TryMove(-1), Is.False);
        Assert.That(sequence.TryMove(0), Is.False);
        Assert.That(sequence.TryMove(2), Is.False);
        Assert.That(sequence.Index, Is.Zero);
        AssertPage(sequence, result, "신규 획득");
        Assert.That(sequence.TryMove(1), Is.True);
        Assert.That(sequence.Index, Is.EqualTo(1));
        AssertPage(sequence, result, "중복 획득\n판다토큰 +10");
        Assert.That(sequence.TryMove(-1), Is.True);
        Assert.That(sequence.Index, Is.Zero);
        AssertPage(sequence, result, "신규 획득");

        for (int index = 1; index < 11; index++)
        {
            Assert.That(sequence.TryMove(1), Is.True);
            Assert.That(sequence.Index, Is.EqualTo(index));
            Assert.That(sequence.CanMovePrevious, Is.True);
            Assert.That(sequence.CanMoveNext, Is.EqualTo(index < 10));
            AssertPage(sequence, result, "중복 획득\n판다토큰 +" + (index == 1 ? 10 : 5));
        }
        Assert.That(sequence.TryMove(1), Is.False);
        Assert.That(sequence.Index, Is.EqualTo(10));
        AssertPage(sequence, result, "중복 획득\n판다토큰 +5");

        // Invoke the real close cleanup without displaying a window or touching a scene.
        var window = Track(ScriptableObject.CreateInstance<StaffGachaAcquisitionPreviewWindow>());
        SetField(window, "_sequence", sequence);
        typeof(StaffGachaAcquisitionPreviewWindow).GetMethod("ClosePreview", PrivateInstance).Invoke(window, null);
        Assert.That(typeof(StaffGachaAcquisitionPreviewWindow).GetField("_sequence", PrivateInstance)
            .GetValue(window), Is.Null, "Close retained the previous batch/index");
        sequence = null;
        Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateFixedEleven(
            available, out sequence, out error), Is.True, error);
        Assert.That(sequence.Index, Is.Zero);
        Assert.That(sequence.Result, Is.Not.SameAs(result));
        AssertPage(sequence, sequence.Result, "신규 획득");

        foreach (bool duplicate in new[] { false, true })
        {
            Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateSingle(
                normal, duplicate, out var single, out error), Is.True, error);
            Assert.That(single.Count, Is.EqualTo(1));
            Assert.That(single.Index, Is.Zero);
            Assert.That(single.CanMovePrevious, Is.False);
            Assert.That(single.CanMoveNext, Is.False);
            Assert.That(single.TryMove(-1), Is.False);
            Assert.That(single.TryMove(1), Is.False);
            AssertPage(single, single.Result, duplicate ? "중복 획득\n판다토큰 +5" : "신규 획득");
        }
        foreach (GachaData onlyGrade in available)
        {
            Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateFixedEleven(
                new[] { onlyGrade }, out var invalid, out error), Is.False);
            Assert.That(invalid, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
    }

    [Test]
    public void SingleAnimationPreview_CompletesOnceWithOriginalSequenceAndCard()
    {
        MachineAnimationFixture machine = CreateMachineAnimationFixture();
        StaffGachaAcquisitionPreviewSequence sequence = CreateSingleSequence("STAFF01");
        StaffGachaAcquisitionResult result = sequence.Result;
        var preview = new StaffGachaSingleAnimationPreview();
        int callbacks = 0;
        try
        {
            Assert.That(preview.TryStart(machine.Staff, sequence, completed =>
            {
                callbacks++;
                Assert.That(completed, Is.SameAs(sequence));
                Assert.That(completed.Result, Is.SameAs(result));
                BindAnimationResult(completed);
            }, out string error), Is.True, error);
            Assert.That(preview.IsActive, Is.True);
            Assert.That(preview.IsComplete, Is.False);
            Assert.That(machine.Animator.fireEvents, Is.False,
                "Actual UIStaffGacha grant/tutorial AnimationEvents must not run");
            AdvanceAnimationToResult(machine, preview);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(sequence.Index, Is.Zero);
            Assert.That(sequence.Result, Is.SameAs(result));
            Assert.That(_nameText.text, Is.EqualTo(sequence.CurrentStaff.Name));
            Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n신규 획득"));
            Assert.That(_effectText.text, Is.EqualTo(
                Utility.GetStaffEffectDescription(sequence.CurrentStaff.StaffData, 1)));
            AssertRank(0, 2);
            preview.Tick();
            preview.Tick();
            Assert.That(callbacks, Is.EqualTo(1), "Completed animation displayed its result twice");
        }
        finally { preview.Close(); }
        AssertMachineRestored(machine);
    }

    [Test]
    public void SingleAnimationPreview_RejectsDoubleStartWithoutReplacingPendingResult()
    {
        MachineAnimationFixture machine = CreateMachineAnimationFixture();
        StaffGachaAcquisitionPreviewSequence first = CreateSingleSequence("STAFF01");
        StaffGachaAcquisitionPreviewSequence second = CreateSingleSequence("STAFF23");
        var preview = new StaffGachaSingleAnimationPreview();
        int firstCallbacks = 0, secondCallbacks = 0;
        try
        {
            Assert.That(preview.TryStart(machine.Staff, first, completed =>
            {
                firstCallbacks++;
                Assert.That(completed, Is.SameAs(first));
                BindAnimationResult(completed);
            }, out string error), Is.True, error);
            Assert.That(preview.TryStart(machine.Staff, second,
                completed => secondCallbacks++, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            AdvanceAnimationToResult(machine, preview);
            preview.Tick();
            Assert.That(firstCallbacks, Is.EqualTo(1));
            Assert.That(secondCallbacks, Is.Zero);
            Assert.That(_nameText.text, Is.EqualTo(first.CurrentStaff.Name));
            Assert.That(first.Index, Is.Zero);
            Assert.That(second.Index, Is.Zero);
        }
        finally { preview.Close(); }
        AssertMachineRestored(machine);
    }

    [Test]
    public void SingleAnimationPreview_CancelLateTickAndRestartRestoresMachine()
    {
        MachineAnimationFixture machine = CreateMachineAnimationFixture();
        StaffGachaAcquisitionPreviewSequence first = CreateSingleSequence("STAFF01");
        StaffGachaAcquisitionPreviewSequence second = CreateSingleSequence("STAFF23");
        var cancelled = new StaffGachaSingleAnimationPreview();
        var restarted = new StaffGachaSingleAnimationPreview();
        int cancelledCallbacks = 0, restartedCallbacks = 0;
        try
        {
            Assert.That(cancelled.TryStart(machine.Staff, first,
                completed => cancelledCallbacks++, out string error), Is.True, error);
            for (int step = 0; step < 5; step++)
            {
                machine.Animator.Update(0.1f);
                cancelled.Tick();
            }
            Assert.That(cancelled.IsComplete, Is.False);
            cancelled.Close();
            cancelled.Tick();
            cancelled.Close();
            Assert.That(cancelled.IsActive, Is.False);
            Assert.That(cancelledCallbacks, Is.Zero);
            AssertMachineRestored(machine);

            Assert.That(restarted.TryStart(machine.Staff, second, completed =>
            {
                restartedCallbacks++;
                Assert.That(completed, Is.SameAs(second));
                BindAnimationResult(completed);
            }, out error), Is.True, error);
            // A delayed old-session tick must not close, advance or display this session.
            AdvanceAnimationToResult(machine, restarted, cancelled.Tick);
            cancelled.Tick();
            restarted.Tick();
            Assert.That(cancelledCallbacks, Is.Zero);
            Assert.That(restartedCallbacks, Is.EqualTo(1));
            Assert.That(_nameText.text, Is.EqualTo(second.CurrentStaff.Name));
            Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n신규 획득"));
            AssertRank(1, 3);
        }
        finally
        {
            cancelled.Close();
            restarted.Close();
            restarted.Close();
        }
        Assert.That(restarted.IsActive, Is.False);
        AssertMachineRestored(machine);

        // Navigation has already hidden/disabled the source: an old snapshot must not reopen it.
        var departed = new StaffGachaSingleAnimationPreview();
        try
        {
            Assert.That(departed.TryStart(machine.Staff, second,
                completed => restartedCallbacks++, out string error), Is.True, error);
            machine.Animator.enabled = false;
            machine.Staff.gameObject.SetActive(false);
            string hiddenPresentation = ReadMachinePresentation(machine.Staff.transform);
            departed.Close(false);
            departed.Tick();
            Assert.That(departed.IsActive, Is.False);
            Assert.That(machine.Staff.gameObject.activeSelf, Is.False);
            Assert.That(machine.Animator.enabled, Is.False);
            Assert.That(machine.Animator.fireEvents, Is.True);
            Assert.That(ReadMachinePresentation(machine.Staff.transform), Is.EqualTo(hiddenPresentation));
            Assert.That(ReadPaymentState(), Is.EqualTo(machine.PaymentState));
            Assert.That(restartedCallbacks, Is.EqualTo(1));
        }
        finally { departed.Close(false); }
    }

    [Test]
    public void ElevenAnimationPreview_UnlocksSameCalculatedBatchAfterOneAnimation()
    {
        MachineAnimationFixture machine = CreateMachineAnimationFixture();
        StaffGachaAcquisitionPreviewSequence sequence = CreateElevenSequence();
        StaffGachaAcquisitionResult result = sequence.Result;
        var preview = new StaffGachaSingleAnimationPreview();
        int callbacks = 0;
        CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
            result.Items.Select(item => item.StaffId));
        CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)),
            result.Items.Select(item => item.PandaTokenReward));
        Assert.That(result.Items.Count(item => item.IsNew), Is.EqualTo(1));
        Assert.That(result.Items.Count(item => item.IsDuplicate), Is.EqualTo(10));
        Assert.That(result.TotalPandaTokens, Is.EqualTo(55));
        try
        {
            Assert.That(preview.TryStart(machine.Staff, sequence, completed =>
            {
                callbacks++;
                Assert.That(completed, Is.SameAs(sequence));
                Assert.That(completed.Result, Is.SameAs(result));
                Assert.That(completed.IsNavigationLocked, Is.False,
                    "The result callback must receive an unlocked list");
                BindAnimationResult(completed);
            }, out string error), Is.True, error);
            Assert.That(sequence.IsNavigationLocked, Is.True);
            Assert.That(sequence.CanMovePrevious, Is.False);
            Assert.That(sequence.CanMoveNext, Is.False);
            Assert.That(sequence.TryMove(-1), Is.False);
            Assert.That(sequence.TryMove(1), Is.False);
            Assert.That(sequence.Index, Is.Zero);
            AdvanceAnimationToResult(machine, preview, () =>
            {
                Assert.That(sequence.TryMove(1), Is.False, "Navigation changed the running batch");
                Assert.That(sequence.Index, Is.Zero);
            });

            void AssertPage(int index, string description, int frame, int stars)
            {
                Assert.That(sequence.Index, Is.EqualTo(index));
                Assert.That(sequence.Result, Is.SameAs(result));
                Assert.That(sequence.CurrentItem, Is.SameAs(result.Items[index]));
                BindAnimationResult(sequence);
                Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n" + description));
                Assert.That(_nameText.text, Is.EqualTo(sequence.CurrentStaff.Name));
                Assert.That(_image.sprite, Is.SameAs(
                    sequence.CurrentStaff.ThumbnailSprite ?? sequence.CurrentStaff.Sprite));
                AssertRank(frame, stars);
                machine.Animator.Update(0.1f);
                preview.Tick();
                Assert.That(machine.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(Animator.StringToHash("Base Layer.ZoomIn Item")),
                    "List navigation restarted the machine animation");
                Assert.That(machine.Animator.IsInTransition(0), Is.False);
                Assert.That(callbacks, Is.EqualTo(1));
                Assert.That(result.TotalPandaTokens, Is.EqualTo(55));
            }

            Assert.That(sequence.IsNavigationLocked, Is.False);
            Assert.That(sequence.CanMoveNext, Is.True);
            AssertPage(0, "신규 획득", 1, 3);
            Assert.That(sequence.TryMove(1), Is.True);
            AssertPage(1, "중복 획득\n판다토큰 +10", 1, 3);
            Assert.That(sequence.TryMove(-1), Is.True);
            AssertPage(0, "신규 획득", 1, 3);
            for (int index = 1; index < 11; index++)
                Assert.That(sequence.TryMove(1), Is.True);
            AssertPage(10, "중복 획득\n판다토큰 +5", 0, 2);
            Assert.That(sequence.CanMoveNext, Is.False);
            Assert.That(sequence.TryMove(1), Is.False);
            Assert.That(sequence.TryMove(-1), Is.True);
            AssertPage(9, "중복 획득\n판다토큰 +5", 0, 2);
        }
        finally { preview.Close(); }
        Assert.That(sequence.IsNavigationLocked, Is.False);
        AssertMachineRestored(machine);
        StaffGachaAcquisitionPreviewSequence single = CreateSingleSequence("STAFF01");
        Assert.That(single.Count, Is.EqualTo(1));
        Assert.That(single.Index, Is.Zero);
        Assert.That(CreateElevenSequence().Index, Is.Zero);
    }

    [Test]
    public void ElevenAnimationPreview_DoubleStartCancelAndRestartKeepOneFreshList()
    {
        MachineAnimationFixture machine = CreateMachineAnimationFixture();
        machine.Animator.fireEvents = false; // Preserve both original event settings across these two tests.
        StaffGachaAcquisitionPreviewSequence first = CreateElevenSequence();
        StaffGachaAcquisitionPreviewSequence rejected = CreateElevenSequence();
        var cancelled = new StaffGachaSingleAnimationPreview();
        var restarted = new StaffGachaSingleAnimationPreview();
        int cancelledCallbacks = 0, rejectedCallbacks = 0, restartedCallbacks = 0;
        try
        {
            Assert.That(cancelled.TryStart(machine.Staff, first,
                completed => cancelledCallbacks++, out string error), Is.True, error);
            Assert.That(cancelled.TryStart(machine.Staff, rejected,
                completed => rejectedCallbacks++, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(rejected.IsNavigationLocked, Is.False);
            for (int step = 0; step < 5; step++)
            {
                machine.Animator.Update(0.1f);
                cancelled.Tick();
                Assert.That(first.TryMove(1), Is.False);
            }
            Assert.That(cancelled.IsComplete, Is.False);
            cancelled.Close();
            cancelled.Tick();
            cancelled.Close();
            Assert.That(first.IsNavigationLocked, Is.False);
            Assert.That(first.TryMove(1), Is.True, "Cancellation left the old list locked");
            AssertMachineRestored(machine, false);

            // A previously paged list cannot be used as the beginning of a new machine run.
            Assert.That(restarted.TryStart(machine.Staff, first,
                completed => rejectedCallbacks++, out error), Is.False);
            StaffGachaAcquisitionPreviewSequence fresh = CreateElevenSequence();
            StaffGachaAcquisitionResult freshResult = fresh.Result;
            Assert.That(fresh.Index, Is.Zero);
            Assert.That(freshResult, Is.Not.SameAs(first.Result));
            Assert.That(restarted.TryStart(machine.Staff, fresh, completed =>
            {
                restartedCallbacks++;
                Assert.That(completed, Is.SameAs(fresh));
                Assert.That(completed.Result, Is.SameAs(freshResult));
                Assert.That(completed.Index, Is.Zero);
                Assert.That(completed.IsNavigationLocked, Is.False);
                BindAnimationResult(completed);
            }, out error), Is.True, error);
            AdvanceAnimationToResult(machine, restarted, cancelled.Tick);
            cancelled.Tick();
            restarted.Tick();
            Assert.That(cancelledCallbacks, Is.Zero);
            Assert.That(rejectedCallbacks, Is.Zero);
            Assert.That(restartedCallbacks, Is.EqualTo(1));
            Assert.That(_descriptionText.text, Is.EqualTo("[테스트 미리보기]\n신규 획득"));
            Assert.That(fresh.Count, Is.EqualTo(11));
            Assert.That(fresh.Index, Is.Zero);
            Assert.That(fresh.TryMove(1), Is.True);
            Assert.That(fresh.CurrentItem.PandaTokenReward, Is.EqualTo(10));
            Assert.That(fresh.Result, Is.SameAs(freshResult));
            restarted.Close();
            Assert.That(fresh.IsNavigationLocked, Is.False);
        }
        finally
        {
            cancelled.Close();
            restarted.Close();
        }
        AssertMachineRestored(machine, false);
        StaffGachaAcquisitionPreviewSequence next = CreateElevenSequence();
        Assert.That(next.Count, Is.EqualTo(11));
        Assert.That(next.Index, Is.Zero);
        Assert.That(next.CurrentItem.IsNew, Is.True);
    }

    private StaffGachaAcquisitionPreviewSequence CreateElevenSequence()
    {
        Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateFixedEleven(
            new GachaData[] { LoadStaff("STAFF23"), LoadStaff("STAFF01") },
            out var sequence, out string error), Is.True, error);
        return sequence;
    }

    private StaffGachaAcquisitionPreviewSequence CreateSingleSequence(string id)
    {
        Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateSingle(
            LoadStaff(id), false, out var sequence, out string error), Is.True, error);
        return sequence;
    }

    private void BindAnimationResult(StaffGachaAcquisitionPreviewSequence sequence)
    {
        Assert.That(_card.TrySetStaffAcquisitionResult(
            sequence.CurrentStaff, sequence.CurrentItem, true), Is.True);
        _card.gameObject.SetActive(true);
    }

    private static void AdvanceAnimationToResult(MachineAnimationFixture machine,
        StaffGachaSingleAnimationPreview preview, Action lateTick = null)
    {
        var visited = new HashSet<int>();
        for (int step = 0; step < 150 && !preview.IsComplete; step++)
        {
            Assert.That(machine.Animator.fireEvents, Is.False);
            machine.Animator.Update(0.1f);
            visited.Add(machine.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash);
            lateTick?.Invoke();
            preview.Tick();
        }
        Assert.That(preview.IsComplete, Is.True, "Real machine controller never reached its result state");
        foreach (string state in new[] { "Start_Gacha", "Wait_Gacha", "Open_Gacha", "ZoomIn Item" })
            Assert.That(visited, Does.Contain(Animator.StringToHash("Base Layer." + state)), state);
    }

    private MachineAnimationFixture CreateMachineAnimationFixture()
    {
        const string controllerPath = "Assets/Animation/GachaMachine/SkinGacha/Skin Gacha Machine.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        Assert.That(controller, Is.Not.Null, "Missing actual staff-machine controller");
        GameObject root = Track(new GameObject("Staff animation test", typeof(RectTransform)));
        root.SetActive(false);
        UIStaffGacha staff = root.AddComponent<UIStaffGacha>();
        Animator animator = root.AddComponent<Animator>();
        // Idle itself has a SetStep event. Suppress events before initialization too.
        animator.fireEvents = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (AnimationClip clip in controller.animationClips.Distinct())
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)
                     .Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip)))
        {
            Transform target = FindOrCreateAnimationPath(root.transform, binding.path);
            if (binding.type == typeof(Image) && target.GetComponent<Image>() == null)
                target.gameObject.AddComponent<Image>();
            else
                Assert.That(binding.type == typeof(Image) || binding.type == typeof(RectTransform) ||
                    binding.type == typeof(Transform) || binding.type == typeof(GameObject), Is.True,
                    "Unexpected real animation binding: " + binding.type);
        }
        _card.transform.SetParent(root.transform, false);
        SetField(staff, "_gachaMacineAnimator", animator);
        SetField(staff, "_gachaCard", _card);
        Transform staffImageTransform = FindOrCreateAnimationPath(root.transform, "Image");
        Image staffImage = staffImageTransform.GetComponent<Image>() ?? staffImageTransform.gameObject.AddComponent<Image>();
        SetField(staff, "_getStaffImage", staffImage);
        foreach (string field in new[] { "_singleButton", "_tenButton", "_screenButton", "_skipButton" })
        {
            Button button = CreateChild<Button>(root.transform, field);
            button.interactable = false; // The real execution buttons remain unavailable.
            button.gameObject.SetActive(field == "_singleButton" || field == "_tenButton");
            SetField(staff, field, button);
        }
        SetField(staff, "_getStaffSlotFrame", FindOrCreateAnimationPath(root.transform, "Result Slots"));
        SetField(staff, "_capsules", FindOrCreateAnimationPath(root.transform, "Gacha Machine/CapSules"));
        SetField(staff, "_upperCapsule", root.transform.Find("Gacha Machine/CapSules/Upper Capsule").GetComponent<Image>());
        SetField(staff, "_lowerCapsule", root.transform.Find("Gacha Machine/CapSules/Lower Capsule ").GetComponent<Image>());
        AudioSource audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        SetField(staff, "_gachaSound", audio);
        animator.runtimeAnimatorController = controller;
        root.SetActive(true);
        animator.Rebind();
        animator.Play("Base Layer.Idle", 0, 0f);
        animator.Update(0f);
        _card.gameObject.SetActive(false);
        staffImage.gameObject.SetActive(false);
        root.transform.Find("Result Slots").gameObject.SetActive(false);
        animator.fireEvents = true; // TryStart/Close must preserve this real-machine setting.
        return new MachineAnimationFixture
        {
            Staff = staff, Animator = animator, Audio = audio,
            Presentation = ReadMachinePresentation(root.transform), PaymentState = ReadPaymentState()
        };
    }

    private static Transform FindOrCreateAnimationPath(Transform root, string path)
    {
        Transform current = root;
        if (string.IsNullOrEmpty(path)) return current;
        foreach (string part in path.Split('/'))
        {
            Transform child = current.Find(part);
            if (child == null)
            {
                child = new GameObject(part, typeof(RectTransform)).transform;
                child.SetParent(current, false);
            }
            current = child;
        }
        return current;
    }

    private static void AssertMachineRestored(MachineAnimationFixture machine, bool expectedFireEvents = true)
    {
        Assert.That(machine.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
            Is.EqualTo(Animator.StringToHash("Base Layer.Idle")));
        Assert.That(machine.Animator.enabled, Is.True);
        Assert.That(machine.Animator.fireEvents, Is.EqualTo(expectedFireEvents));
        Assert.That(machine.Audio.isPlaying, Is.False);
        Assert.That(ReadMachinePresentation(machine.Staff.transform), Is.EqualTo(machine.Presentation),
            "Close did not restore the original machine presentation");
        Assert.That(ReadPaymentState(), Is.EqualTo(machine.PaymentState), "Payment records changed");
        Assert.That((bool)typeof(UIStaffGacha).GetField("IsGachaExecutionEnabled",
            BindingFlags.NonPublic | BindingFlags.Static).GetValue(null), Is.False);
        foreach (Button button in machine.Staff.GetComponentsInChildren<Button>(true))
            Assert.That(button.interactable, Is.False, "Preview enabled an execution/input button");
    }

    private static string ReadMachinePresentation(Transform root)
    {
        return JsonConvert.SerializeObject(root.GetComponentsInChildren<RectTransform>(true).Select(rect => new
        {
            rect.name, Active = rect.gameObject.activeSelf, Sibling = rect.GetSiblingIndex(),
            Position = new[] { rect.localPosition.x, rect.localPosition.y, rect.localPosition.z },
            Scale = new[] { rect.localScale.x, rect.localScale.y, rect.localScale.z },
            Rotation = new[] { rect.localRotation.x, rect.localRotation.y, rect.localRotation.z, rect.localRotation.w },
            Layout = new[] { rect.anchorMin.x, rect.anchorMin.y, rect.anchorMax.x, rect.anchorMax.y,
                rect.pivot.x, rect.pivot.y, rect.sizeDelta.x, rect.sizeDelta.y },
            Images = rect.GetComponents<Image>().Select(image => new
            {
                Sprite = image.sprite == null ? 0 : image.sprite.GetInstanceID(),
                Color = new[] { image.color.r, image.color.g, image.color.b, image.color.a }
            }).ToArray()
        }).ToArray());
    }

    private static string ReadPaymentState()
    {
        return JsonConvert.SerializeObject(new { PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas });
    }

    private sealed class MachineAnimationFixture
    {
        public UIStaffGacha Staff;
        public Animator Animator;
        public AudioSource Audio;
        public string Presentation;
        public string PaymentState;
    }

    private static StaffGachaAcquisitionItem Calculate(GachaStaffData staff, bool alreadyOwned)
    {
        string[] owned = alreadyOwned ? new[] { staff.Id } : Array.Empty<string>();
        Assert.That(StaffGachaAcquisitionCalculator.TryCalculate(
            owned, new[] { staff }, out StaffGachaAcquisitionResult result, out string error), Is.True, error);
        Assert.That(result.Items.Count, Is.EqualTo(1));
        return result.Items[0];
    }

    private GachaStaffData LoadStaff(string id)
    {
        StaffData staff = Resources.Load<StaffData>("StaffData/" + id);
        Assert.That(staff, Is.Not.Null, "Missing registered resource " + id);
        Assert.That(staff.Id, Is.EqualTo(id));
        Assert.That(staff.ThumbnailSprite ?? staff.Sprite, Is.Not.Null);
        return Track(GachaStaffData.Create(staff));
    }

    private void AssertRank(int frameIndex, int starCount)
    {
        for (int i = 0; i < _frames.Length; i++)
            Assert.That(_frames[i].gameObject.activeSelf, Is.EqualTo(i == frameIndex), "Frame " + i);
        for (int i = 0; i < _stars.Length; i++)
            Assert.That(_stars[i].activeSelf, Is.EqualTo(i < starCount), "Star " + i);
    }

    private static T CreateChild<T>(Transform parent, string name) where T : Component
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.AddComponent<T>();
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }

    private static FieldInfo GetManagerInstanceField()
    {
        return typeof(StaffDataManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
    }

    private void SubscribeMutationEvents()
    {
        UserInfo.OnChangeDiaHandler += _mutationHandler;
        UserInfo.OnChangeMoneyHandler += _mutationHandler;
        UserInfo.OnChangeSkinTokenHandler += _mutationHandler;
        UserInfo.OnGiveStaffHandler += _mutationHandler;
        UserInfo.OnUpgradeStaffHandler += _mutationHandler;
        UserInfo.OnGiveStaffSkinHandler += _mutationHandler;
        UserInfo.OnChangeStaffSkinHandler += _mutationHandler;
        UserInfo.OnUseGachaMachineHandler += _mutationHandler;
        UserInfo.OnChangeStaffHandler += _equipHandler;
    }

    private void UnsubscribeMutationEvents()
    {
        UserInfo.OnChangeDiaHandler -= _mutationHandler;
        UserInfo.OnChangeMoneyHandler -= _mutationHandler;
        UserInfo.OnChangeSkinTokenHandler -= _mutationHandler;
        UserInfo.OnGiveStaffHandler -= _mutationHandler;
        UserInfo.OnUpgradeStaffHandler -= _mutationHandler;
        UserInfo.OnGiveStaffSkinHandler -= _mutationHandler;
        UserInfo.OnChangeStaffSkinHandler -= _mutationHandler;
        UserInfo.OnUseGachaMachineHandler -= _mutationHandler;
        UserInfo.OnChangeStaffHandler -= _equipHandler;
    }

    private static string ReadUserState()
    {
        var stages = (StageInfo[])typeof(UserInfo)
            .GetField("_stageInfos", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var stageState = stages?.Select(stage => stage == null ? null : new
        {
            Owned = typeof(StageInfo).GetField("_giveStaffDic", PrivateInstance).GetValue(stage),
            Equipped = ((Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>)
                typeof(StageInfo).GetField("_equipStaffTypeDic", PrivateInstance).GetValue(stage))
                .Select(floor => new
                {
                    Floor = floor.Key,
                    Staff = floor.Value.Select(slot => new
                    {
                        Slot = slot.Key,
                        Id = slot.Value == null ? null : slot.Value.Id
                    }).ToArray()
                }).ToArray()
        }).ToArray();
        return JsonConvert.SerializeObject(new
        {
            UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken,
            UserInfo.TotalUseGachaMachineCount, UserInfo.CurrentStage,
            UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart,
            Stages = stageState
        });
    }

    private T Track<T>(T createdObject) where T : Object
    {
        _createdObjects.Add(createdObject);
        return createdObject;
    }
}

// In-memory generic-card fixture; no StaffData, Resources or asset creation.
public sealed class StaffGachaCardGenericTestData : GachaData
{
    public void Initialize(Sprite sprite)
    {
        _id = "GENERIC_CARD_TEST";
        _name = "Generic card";
        _description = "Original generic description";
        _sprite = sprite;
        _thumbnailSprite = sprite;
        _rank = Rank.Rare;
    }
}
#endif
