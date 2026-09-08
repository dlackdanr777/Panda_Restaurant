#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class StaffGachaMockRequestPreviewTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private readonly List<Object> _temporary = new List<Object>();
    private readonly Dictionary<StaffData, string> _staffBefore = new Dictionary<StaffData, string>();
    private Random.State _randomBefore;
    private string _gameBefore;

    [SetUp]
    public void SetUp()
    {
        _randomBefore = Random.state;
        _gameBefore = ReadGameState();
        foreach (string id in new[] { "STAFF01", "STAFF03", "STAFF23" })
        {
            StaffData staff = Resources.Load<StaffData>("StaffData/" + id);
            Assert.That(staff, Is.Not.Null, id);
            Assert.That(staff.Id, Is.EqualTo(id));
            _staffBefore.Add(staff, EditorJsonUtility.ToJson(staff));
        }
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(Random.state, Is.EqualTo(_randomBefore));
            Assert.That(ReadGameState(), Is.EqualTo(_gameBefore), "Actual user/payment state changed");
            foreach (var staff in _staffBefore)
                Assert.That(EditorJsonUtility.ToJson(staff.Key), Is.EqualTo(staff.Value), "Registered staff changed");
        }
        finally
        {
            for (int i = _temporary.Count - 1; i >= 0; i--)
                if (_temporary[i] != null) Object.DestroyImmediate(_temporary[i]);
            _temporary.Clear();
            _staffBefore.Clear();
        }
    }

    [Test]
    public void SingleAndMulti_ApplyOnlyOnceAndReuseExactCompletedResultsForCardsAndNavigation()
    {
        using (var context = new StaffGachaMockRequestContext())
        {
            AssertInitial(context);
            AssertNoCompletedSequence(context);
            WithExistingCardFixture((fixture, card) =>
            {
                StaffAccountSaveData singleInput = context.Account;
                string singleInputBefore = JsonConvert.SerializeObject(singleInput);
                Assert.That(context.TryStart(StaffGachaPurchaseType.Single, out string error), Is.True, error);
                StaffGachaSessionRequest single = context.CurrentRequest;
                AssertInitialBalances(context);
                AssertPurchase(single, StaffGachaPurchaseType.Single, 10, 1, 0);
                Assert.That(context.TryHandleResponse(single.RequestId, StaffGachaResponseKind.SuccessConfirmed,
                    out var completed), Is.True);
                Assert.That(completed, Is.SameAs(single));
                Assert.That(context.LastCompletedRequest, Is.SameAs(single));
                AssertAccount(context, 100, 55, true);
                Assert.That(context.Account, Is.SameAs(single.Plan.AccountResult.UpdatedAccount));
                string singleState = Describe(context);
                AssertIgnored(context, single.RequestId, StaffGachaResponseKind.SuccessConfirmed);
                Assert.That(context.TryCreateCompletedSequence(out var singleSequence, out error), Is.True, error);
                Assert.That(singleSequence.Result, Is.SameAs(single.Plan.AccountResult.Acquisition));
                Assert.That(singleSequence.Count, Is.EqualTo(1));
                AssertPage(fixture, card, singleSequence, singleSequence.Result.Items[0]);
                Assert.That(singleSequence.TryMove(-1), Is.False);
                Assert.That(singleSequence.TryMove(1), Is.False);
                var display = new List<GachaStaffData> { singleSequence.CurrentStaff };
                Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateFromCalculated(singleSequence.Result,
                    display, out var copiedDisplay, out error), Is.True, error);
                display.Clear(); // Deliberate caller edit after binding, not a change made by the adapter.
                Assert.That(copiedDisplay.Result, Is.SameAs(singleSequence.Result));
                Assert.That(copiedDisplay.CurrentStaff, Is.SameAs(singleSequence.CurrentStaff));
                var wrongId = GachaStaffData.Create(Resources.Load<StaffData>("StaffData/STAFF01"));
                var wrongRank = GachaStaffData.Create(singleSequence.CurrentStaff.StaffData);
                _temporary.Add(wrongId);
                _temporary.Add(wrongRank);
                // Only a disposable wrapper is deliberately corrupted; never the registered staff resource.
                typeof(GachaData).GetField("_rank", PrivateInstance).SetValue(wrongRank, Rank.Special);
                foreach (GachaStaffData[] invalidDisplay in new[]
                    { Array.Empty<GachaStaffData>(), new[] { wrongId }, new[] { wrongRank } })
                {
                    Assert.That(StaffGachaAcquisitionPreviewSequence.TryCreateFromCalculated(singleSequence.Result,
                        invalidDisplay, out var rejected, out error), Is.False);
                    Assert.That(rejected, Is.Null, "Invalid display input exposed a partial sequence");
                    Assert.That(error, Is.Not.Null.And.Not.Empty);
                    Assert.That(context.LastCompletedRequest, Is.SameAs(single));
                    Assert.That(Describe(context), Is.EqualTo(singleState));
                }
                Assert.That(Describe(context), Is.EqualTo(singleState));
                Assert.That(JsonConvert.SerializeObject(singleInput), Is.EqualTo(singleInputBefore));

                Assert.That(context.TryReset(out error), Is.True, error);
                AssertInitial(context);
                StaffAccountSaveData multiInput = context.Account;
                string multiInputBefore = JsonConvert.SerializeObject(multiInput);
                Assert.That(context.TryStart(StaffGachaPurchaseType.Multi, out error), Is.True, error);
                StaffGachaSessionRequest multi = context.CurrentRequest;
                Assert.That(multi.RequestId, Is.Not.EqualTo(single.RequestId));
                AssertInitialBalances(context);
                AssertPurchase(multi, StaffGachaPurchaseType.Multi, 100, 11, 55);
                Assert.That(context.TryHandleResponse(multi.RequestId, StaffGachaResponseKind.SuccessConfirmed,
                    out completed), Is.True);
                Assert.That(completed, Is.SameAs(multi));
                Assert.That(context.LastCompletedRequest, Is.SameAs(multi));
                AssertAccount(context, 10, 110, true);
                Assert.That(context.Account, Is.SameAs(multi.Plan.AccountResult.UpdatedAccount));
                string completedState = Describe(context);
                AssertIgnored(context, multi.RequestId, StaffGachaResponseKind.SuccessConfirmed);
                Assert.That(context.TryStart(StaffGachaPurchaseType.Multi, out error), Is.False);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(Describe(context), Is.EqualTo(completedState), "Insufficient balance changed the completed mock state");

                Assert.That(context.TryCreateCompletedSequence(out var sequence, out error), Is.True, error);
                StaffGachaAcquisitionResult result = multi.Plan.AccountResult.Acquisition;
                Assert.That(sequence.Result, Is.SameAs(result), "Display recalculated the completed acquisition");
                CollectionAssert.AreEqual(new[] { "STAFF23", "STAFF23" }.Concat(Enumerable.Repeat("STAFF01", 9)),
                    result.Items.Select(item => item.StaffId));
                CollectionAssert.AreEqual(new[] { true }.Concat(Enumerable.Repeat(false, 10)), result.Items.Select(item => item.IsNew));
                CollectionAssert.AreEqual(new[] { 0, 10 }.Concat(Enumerable.Repeat(5, 9)), result.Items.Select(item => item.PandaTokenReward));
                Assert.That(result.TotalPandaTokens, Is.EqualTo(55));
                CollectionAssert.AreEqual(new[] { "STAFF23" }, result.NewStaffIds);
                Assert.That(sequence.TryMove(-1), Is.False);
                for (int index = 0; index < result.Items.Count; index++)
                {
                    Assert.That(sequence.Index, Is.EqualTo(index));
                    AssertPage(fixture, card, sequence, result.Items[index]);
                    Assert.That(sequence.TryMove(1), Is.EqualTo(index + 1 < result.Items.Count));
                }
                Assert.That(sequence.TryMove(-1), Is.True);
                AssertPage(fixture, card, sequence, result.Items[9]);
                Assert.That(context.TryCreateCompletedSequence(out var reopened, out error), Is.True, error);
                Assert.That(reopened, Is.Not.SameAs(sequence));
                Assert.That(reopened.Index, Is.Zero);
                Assert.That(reopened.Result, Is.SameAs(result));
                AssertPage(fixture, card, reopened, result.Items[0]);
                Assert.That(Describe(context), Is.EqualTo(completedState), "Navigation/reopening reapplied the request");
                Assert.That(JsonConvert.SerializeObject(multiInput), Is.EqualTo(multiInputBefore));
            });
        }
    }

    [Test]
    public void ProcessingAndUnknown_BlockStartAndResetWhileWrongFailedAndOldResponsesNeverApply()
    {
        using (var context = new StaffGachaMockRequestContext())
        {
            AssertInitial(context);
            Assert.That(context.TryStart((StaffGachaPurchaseType)999, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            AssertInitial(context);
            Assert.That(context.TryStart(StaffGachaPurchaseType.Multi, out error), Is.True, error);
            StaffGachaSessionRequest first = context.CurrentRequest;
            AssertInitialBalances(context);
            AssertNoCompletedSequence(context);
            AssertBlocked(context, StaffGachaRequestState.Processing);
            foreach (StaffGachaResponseKind response in Enum.GetValues(typeof(StaffGachaResponseKind)))
                AssertIgnored(context, "not-the-current-request", response);
            AssertIgnored(context, first.RequestId, (StaffGachaResponseKind)999);
            Assert.That(context.TryHandleResponse(first.RequestId, StaffGachaResponseKind.Indeterminate, out var completed), Is.True);
            Assert.That(completed, Is.Null);
            AssertInitialBalances(context);
            AssertBlocked(context, StaffGachaRequestState.Indeterminate);
            AssertIgnored(context, first.RequestId, StaffGachaResponseKind.Indeterminate);
            AssertNoCompletedSequence(context);

            Assert.That(context.TryHandleResponse(first.RequestId, StaffGachaResponseKind.UnappliedFailureConfirmed, out completed), Is.True);
            Assert.That(completed, Is.Null);
            Assert.That(context.State, Is.EqualTo(StaffGachaRequestState.FailedUnapplied));
            Assert.That(context.CanStartNewRequest, Is.True);
            Assert.That(context.CanReset, Is.True);
            Assert.That(context.LastCompletedRequest, Is.Null);
            AssertInitialBalances(context);
            AssertIgnored(context, first.RequestId, StaffGachaResponseKind.SuccessConfirmed);

            Assert.That(context.TryReset(out error), Is.True, error);
            AssertInitial(context);
            Assert.That(context.TryStart(StaffGachaPurchaseType.Single, out error), Is.True, error);
            StaffGachaSessionRequest second = context.CurrentRequest;
            Assert.That(second.RequestId, Is.Not.EqualTo(first.RequestId), "Reset reused an old response ID");
            foreach (StaffGachaResponseKind response in Enum.GetValues(typeof(StaffGachaResponseKind)))
                AssertIgnored(context, first.RequestId, response);
            AssertInitialBalances(context);
            Assert.That(context.TryHandleResponse(second.RequestId, StaffGachaResponseKind.SuccessConfirmed, out completed), Is.True);
            Assert.That(completed, Is.SameAs(second));
            AssertAccount(context, 100, 55, true);
        }
    }

    [Test]
    public void SharedContext_SurvivesWindowAndCardCloseAndRetainsCompletionWithoutPlayModeUi()
    {
        Assert.That(EditorApplication.isPlaying, Is.False, "This is an EditMode lifecycle test, not a UI play test");
        StaffGachaMockRequestContext shared = StaffGachaMockRequestContext.Shared;
        // Do not reset or take ownership of an unrelated in-progress shared mock request.
        AssertInitial(shared);
        try
        {
            WithExistingCardFixture((fixture, card) =>
            {
                var firstWindow = CreateWindow();
                InvokeWindow(firstWindow, "StartMockRequest", StaffGachaPurchaseType.Multi);
                Assert.That(shared.State, Is.EqualTo(StaffGachaRequestState.Processing));
                StaffGachaSessionRequest request = shared.CurrentRequest;
                Assert.That(request, Is.Not.Null);
                InvokeClose(firstWindow);
                Assert.That(shared.CurrentRequest, Is.SameAs(request));
                Assert.That(shared.State, Is.EqualTo(StaffGachaRequestState.Processing));
                Assert.That(shared.TryHandleResponse(request.RequestId, StaffGachaResponseKind.Indeterminate, out var completed), Is.True);
                Assert.That(completed, Is.Null);
                Object.DestroyImmediate(firstWindow);
                Assert.That(shared.State, Is.EqualTo(StaffGachaRequestState.Indeterminate));
                AssertInitialBalances(shared);

                var secondWindow = CreateWindow();
                Assert.That(StaffGachaMockRequestContext.Shared, Is.SameAs(shared));
                Assert.That(shared.CurrentRequest, Is.SameAs(request));
                Assert.That(shared.CanStartNewRequest, Is.False);
                InvokeWindow(secondWindow, "HandleMockResponse", StaffGachaResponseKind.SuccessConfirmed);
                Assert.That(shared.State, Is.EqualTo(StaffGachaRequestState.Succeeded));
                Assert.That(shared.LastCompletedRequest, Is.SameAs(request));
                AssertAccount(shared, 10, 110, true);
                string completedState = Describe(shared);
                Assert.That(secondWindow.TryShowMockCompletedResult(out string error), Is.False);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(Describe(shared), Is.EqualTo(completedState), "No UI context dropped or reapplied the completed request");
                Assert.That(shared.TryCreateCompletedSequence(out var sequence, out error), Is.True, error);
                Assert.That(sequence.Result, Is.SameAs(request.Plan.AccountResult.Acquisition));
                AssertPage(fixture, card, sequence, sequence.Result.Items[0]);
                var overlay = new GameObject("Disposable mock preview overlay", typeof(RectTransform));
                overlay.SetActive(false);
                _temporary.Add(overlay);
                GameObject cardClone = Object.Instantiate(card.gameObject, overlay.transform, false);
                Assert.That(cardClone, Is.Not.SameAs(card.gameObject));
                SetField(secondWindow, "_sequence", sequence);
                SetField(secondWindow, "_overlay", overlay);
                SetField(secondWindow, "_previewCard", cardClone.GetComponent<UIGachaCard>());
                InvokeClose(secondWindow);
                Assert.That(overlay == null, Is.True, "Close did not destroy the disposable overlay");
                Assert.That(cardClone == null, Is.True, "Close did not destroy the disposable card clone");
                Assert.That(card != null, Is.True, "Close destroyed the original fixture card");
                Assert.That(GetField<object>(secondWindow, "_sequence"), Is.Null);
                Assert.That(GetField<object>(secondWindow, "_previewCard"), Is.Null);
                Assert.That(Describe(shared), Is.EqualTo(completedState));
                Object.DestroyImmediate(secondWindow);

                var reopenedWindow = CreateWindow();
                Assert.That(StaffGachaMockRequestContext.Shared, Is.SameAs(shared));
                Assert.That(shared.LastCompletedRequest, Is.SameAs(request));
                Assert.That(reopenedWindow.TryShowMockCompletedResult(out error), Is.False);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(shared.TryCreateCompletedSequence(out var reopened, out error), Is.True, error);
                Assert.That(reopened.Index, Is.Zero);
                Assert.That(reopened.Result, Is.SameAs(sequence.Result));
                AssertPage(fixture, card, reopened, reopened.Result.Items[0]);
                AssertIgnored(shared, request.RequestId, StaffGachaResponseKind.SuccessConfirmed);
                Assert.That(Describe(shared), Is.EqualTo(completedState));
            });
        }
        finally
        {
            // Test cleanup uses the public terminal transition; never dispose/reset an active Shared session.
            if (!shared.CanStartNewRequest && shared.CurrentRequest != null)
                shared.TryHandleResponse(shared.CurrentRequest.RequestId, StaffGachaResponseKind.UnappliedFailureConfirmed, out _);
            Assert.That(shared.TryReset(out string error), Is.True, error);
            AssertInitial(shared);
        }
    }

    private static void WithExistingCardFixture(Action<StaffGachaAcquisitionCardTests, UIGachaCard> action)
    {
        // Reuse setup/cleanup only, not any existing [Test] method or animation/UI fixture.
        var fixture = new StaffGachaAcquisitionCardTests();
        fixture.SetUp();
        try { action(fixture, GetField<UIGachaCard>(fixture, "_card")); }
        finally { fixture.TearDown(); }
    }

    private static void AssertPage(StaffGachaAcquisitionCardTests fixture, UIGachaCard card,
        StaffGachaAcquisitionPreviewSequence sequence, StaffGachaAcquisitionItem expected)
    {
        Assert.That(sequence.CurrentItem, Is.SameAs(expected));
        Assert.That(sequence.CurrentStaff.Id, Is.EqualTo(expected.StaffId));
        Assert.That(sequence.CurrentStaff.Rank, Is.EqualTo(expected.Rank));
        Assert.That(card.TrySetStaffAcquisitionResult(sequence.CurrentStaff, sequence.CurrentItem, true), Is.True);
        Assert.That(GetField<TextMeshProUGUI>(fixture, "_nameText").text, Is.EqualTo(sequence.CurrentStaff.Name));
        Assert.That(GetField<TextMeshProUGUI>(fixture, "_descriptionText").text,
            Is.EqualTo("[테스트 미리보기]\n" + (expected.IsNew ? "신규 획득" : "중복 획득\n판다토큰 +" + expected.PandaTokenReward)));
        Assert.That(GetField<Image>(fixture, "_image").sprite,
            Is.SameAs(sequence.CurrentStaff.ThumbnailSprite ?? sequence.CurrentStaff.Sprite));
        Assert.That(GetField<TextMeshProUGUI>(fixture, "_effectText").text,
            Is.EqualTo(Utility.GetStaffEffectDescription(sequence.CurrentStaff.StaffData, 1)));
        Image[] frames = GetField<Image[]>(fixture, "_frames");
        for (int index = 0; index < frames.Length; index++)
            Assert.That(frames[index].gameObject.activeSelf, Is.EqualTo(index == (expected.Rank == Rank.Rare ? 1 : 0)));
    }

    private static void AssertPurchase(StaffGachaSessionRequest request, StaffGachaPurchaseType type, int price, int count, int reward)
    {
        Assert.That(request.RequestId, Is.Not.Null.And.Not.Empty);
        Assert.That(request.Plan.PurchaseType, Is.EqualTo(type));
        Assert.That(request.Plan.DiamondsBefore, Is.EqualTo(110));
        Assert.That(request.Plan.DiamondCost, Is.EqualTo(price));
        Assert.That(request.Plan.DiamondsAfter, Is.EqualTo(110 - price));
        Assert.That(request.Plan.ResultCount, Is.EqualTo(count));
        Assert.That(request.Plan.AccountResult.Acquisition.Items.Count, Is.EqualTo(count));
        Assert.That(request.Plan.AccountResult.Acquisition.TotalPandaTokens, Is.EqualTo(reward));
    }

    private static void AssertInitial(StaffGachaMockRequestContext context)
    {
        Assert.That(context.State, Is.EqualTo(StaffGachaRequestState.Idle));
        Assert.That(context.CurrentRequest, Is.Null);
        Assert.That(context.LastCompletedRequest, Is.Null);
        Assert.That(context.CanStartNewRequest, Is.True);
        Assert.That(context.CanReset, Is.True);
        AssertInitialBalances(context);
    }

    private static void AssertInitialBalances(StaffGachaMockRequestContext context) => AssertAccount(context, 110, 55, false);

    private static void AssertAccount(StaffGachaMockRequestContext context, int diamonds, long tokens, bool hasRare)
    {
        Assert.That(context.Diamonds, Is.EqualTo(diamonds));
        Assert.That(context.Account.Version, Is.EqualTo(StaffAccountSaveConverter.CurrentVersion));
        Assert.That(context.Account.PandaTokens, Is.EqualTo(tokens));
        CollectionAssert.AreEqual(hasRare ? new[] { "STAFF01", "STAFF03", "STAFF23" } : new[] { "STAFF01", "STAFF03" },
            context.Account.Staff.Select(staff => staff.Id));
        CollectionAssert.AreEqual(hasRare ? new[] { 2, 4, 1 } : new[] { 2, 4 }, context.Account.Staff.Select(staff => staff.Level));
    }

    private static void AssertBlocked(StaffGachaMockRequestContext context, StaffGachaRequestState state)
    {
        string before = Describe(context);
        Assert.That(context.State, Is.EqualTo(state));
        Assert.That(context.CanStartNewRequest, Is.False);
        Assert.That(context.CanReset, Is.False);
        foreach (StaffGachaPurchaseType type in Enum.GetValues(typeof(StaffGachaPurchaseType)))
        {
            Assert.That(context.TryStart(type, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }
        Assert.That(context.TryReset(out string resetError), Is.False);
        Assert.That(resetError, Is.Not.Null.And.Not.Empty);
        Assert.That(Describe(context), Is.EqualTo(before));
    }

    private static void AssertNoCompletedSequence(StaffGachaMockRequestContext context)
    {
        Assert.That(context.TryCreateCompletedSequence(out var sequence, out string error), Is.False);
        Assert.That(sequence, Is.Null);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    private static void AssertIgnored(StaffGachaMockRequestContext context, string id, StaffGachaResponseKind response)
    {
        string before = Describe(context);
        Assert.That(context.TryHandleResponse(id, response, out var completed), Is.False);
        Assert.That(completed, Is.Null);
        Assert.That(Describe(context), Is.EqualTo(before));
    }

    private StaffGachaAcquisitionPreviewWindow CreateWindow()
    {
        var window = ScriptableObject.CreateInstance<StaffGachaAcquisitionPreviewWindow>();
        _temporary.Add(window);
        return window;
    }

    private static void InvokeClose(StaffGachaAcquisitionPreviewWindow window) =>
        InvokeWindow(window, "ClosePreview");
    private static void InvokeWindow(StaffGachaAcquisitionPreviewWindow window, string method, params object[] arguments) =>
        typeof(StaffGachaAcquisitionPreviewWindow).GetMethod(method, PrivateInstance).Invoke(window, arguments);
    private static T GetField<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
    private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, PrivateInstance).SetValue(owner, value);
    private static string Describe(StaffGachaMockRequestContext context) => JsonConvert.SerializeObject(new
    {
        context.Diamonds, context.Account, context.State, context.CurrentRequest, context.LastCompletedRequest,
        context.CanStartNewRequest, context.CanReset
    });

    private static string ReadGameState()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var stages = (StageInfo[])typeof(UserInfo).GetField("_stageInfos", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        var stageState = stages?.Select(stage => stage == null ? null : new
        {
            Owned = typeof(StageInfo).GetField("_giveStaffDic", flags).GetValue(stage),
            Equipped = ((Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>)
                typeof(StageInfo).GetField("_equipStaffTypeDic", flags).GetValue(stage))
                .Select(floor => new { Floor = floor.Key, Staff = floor.Value.Select(slot => new
                { Slot = slot.Key, Id = slot.Value == null ? null : slot.Value.Id }).ToArray() }).ToArray()
        }).ToArray();
        return JsonConvert.SerializeObject(new
        {
            UserInfo.Dia, UserInfo.Money, UserInfo.SkinToken, UserInfo.TotalUseGachaMachineCount,
            UserInfo.CurrentStage, UserInfo.IsFirstTutorialClear, UserInfo.IsTutorialStart, Stages = stageState,
            PaymentInfo.PaymentDatas, PaymentInfo.GachaPaymentDatas
        });
    }
}
#endif
