#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using Muks.MobileUI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authored HUD mapping and read-only behavior against the existing local sales/session graph.</summary>
public sealed class PopupStoreCurrentGradeHudTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private static readonly string[] SpriteFields = { "_cSprite", "_bSprite", "_aSprite", "_sSprite", "_ssSprite", "_sssSprite" };

    [Test]
    public void AuthoredHudUsesExactExistingSixSpritesAndItsOwnNonInteractiveImage()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var hud = root.GetComponent<PopupStoreCurrentGradeHud>();
        var box = root.GetComponent<PopupStoreRewardBox>();
        Assert.That(hud, Is.Not.Null);
        Assert.That(root.GetComponentsInChildren<PopupStoreRewardBox>(true).Length, Is.EqualTo(1));
        Assert.That(root.GetComponentsInChildren<PopupStoreCurrentGradeHud>(true).Length, Is.EqualTo(1));
        Assert.That(hud.ValidateMapping(out var error), Is.True, error);
        Assert.That(hud.RewardImage, Is.Not.Null.And.Not.SameAs(box.BoxImage));
        Assert.That(hud.RewardImage.raycastTarget, Is.False);
        Assert.That(hud.RewardImage.preserveAspect, Is.True);
        Assert.That(hud.NextGradeText, Is.Not.Null.And.Not.SameAs(Read(root.GetComponent<PopupStoreGradeSession>(), "_gradeText")));
        Assert.That(hud.NextGradeText.raycastTarget, Is.False);
        Assert.That(hud.NextGradeText.font, Is.SameAs(((TMP_Text)Read(root.GetComponent<PopupStoreScore>(), "_scoreText")).font));
        for (int i = 0; i < SpriteFields.Length; i++)
        {
            var grade = (PopupStoreGradeTier)(i + 1);
            var sprite = (Sprite)Read(hud, SpriteFields[i]);
            Assert.That(sprite, Is.SameAs(Read(box, SpriteFields[i])));
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(
                "Assets/Sprites/PopupStore/RewardBoxes/PopupRewardBox_" + grade + "_APPROVED.png"));
        }
    }

    [TestCase(PopupStoreGradeTier.C)]
    [TestCase(PopupStoreGradeTier.B)]
    [TestCase(PopupStoreGradeTier.A)]
    [TestCase(PopupStoreGradeTier.S)]
    [TestCase(PopupStoreGradeTier.SS)]
    [TestCase(PopupStoreGradeTier.SSS)]
    public void RealApprovedSalesShowTheExistingCurrentGradeWithoutFinalizingOrDrawing(PopupStoreGradeTier tier)
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", tier);
            var protectedState = new TransactionSnapshot(h);
            Invoke(h.Hud, "LateUpdate");
            Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(tier));
            Assert.That(h.Hud.DisplayedSprite, Is.SameAs(Read(h.Box, SpriteFields[(int)tier - 1])));
            Assert.That(h.Hud.IsVisible, Is.True);
            Assert.That(h.Grade.FinalResult, Is.Null); Assert.That(h.Reward.RandomDrawCount, Is.Zero);
            Assert.That(h.Reward.DecisionCount, Is.Zero); Assert.That(h.Box.AnimationCount, Is.Zero);
            for (int i = 0; i < 5; i++) Assert.That(h.Hud.RefreshDisplay(), Is.True);
            protectedState.AssertUnchanged(h);
        }
    }

    [TestCase(100, 1, PopupStoreGradeTier.C, 600, 100f / 700)]
    [TestCase(699, 4, PopupStoreGradeTier.C, 1, 699f / 700)]
    [TestCase(700, 4, PopupStoreGradeTier.B, 500, 0)]
    [TestCase(1199, 7, PopupStoreGradeTier.B, 1, 499f / 500)]
    [TestCase(1200, 7, PopupStoreGradeTier.A, 500, 0)]
    [TestCase(1260, 7, PopupStoreGradeTier.A, 440, .12f)]
    [TestCase(1699, 10, PopupStoreGradeTier.A, 1, 499f / 500)]
    [TestCase(1700, 10, PopupStoreGradeTier.S, 400, 0)]
    [TestCase(2099, 12, PopupStoreGradeTier.S, 1, 399f / 400)]
    [TestCase(2100, 12, PopupStoreGradeTier.SS, 300, 0)]
    [TestCase(2399, 14, PopupStoreGradeTier.SS, 1, 299f / 300)]
    [TestCase(2400, 14, PopupStoreGradeTier.SSS, 0, 1)]
    public void CachedCalculatorBoundariesUseActualNextThresholdAndProgress(
        int score, int count, PopupStoreGradeTier tier, int remaining, float progress)
    {
        using (var h = new Host())
        {
            h.Start();
            Assert.That(PopupStoreGradeCalculator.TryEvaluate(score, count, out var evaluation, out var error), Is.True, error);
            Assert.That(evaluation.Progress, Is.EqualTo(progress).Within(.00001f));
            // A display-only boundary fixture; this never creates a receipt or asserts a synthetic sale happened.
            var original = h.Grade.Current;
            Write(h.Grade, "_current", evaluation);
            try
            {
                var protectedState = new TransactionSnapshot(h);
                Assert.That(h.Hud.RefreshDisplay(), Is.True);
                Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(tier));
                Assert.That(h.Hud.DisplayedSprite, Is.SameAs(Read(h.Box, SpriteFields[(int)tier - 1])));
                string expected = tier == PopupStoreGradeTier.SSS ? "최고 등급"
                    : "다음 " + (PopupStoreGradeTier)((int)tier + 1) + "까지 " + remaining + "점";
                Assert.That(h.Hud.NextGradeText.text, Is.EqualTo(expected));
                protectedState.AssertUnchanged(h);
            }
            finally { Write(h.Grade, "_current", original); h.Hud.RefreshDisplay(); }
        }
    }

    [TestCase(PopupStoreBusinessState.NotStarted)]
    [TestCase(PopupStoreBusinessState.Running)]
    [TestCase(PopupStoreBusinessState.Paused)]
    [TestCase(PopupStoreBusinessState.Ended)]
    public void ZeroSuccessAndNoneNeverDisplayABox(PopupStoreBusinessState state)
    {
        using (var h = new Host())
        {
            if (state != PopupStoreBusinessState.NotStarted) h.Start(state == PopupStoreBusinessState.Ended ? 0 : 90);
            if (state == PopupStoreBusinessState.Paused) h.Clock.SetApplicationFocused(false);
            var snapshot = new TransactionSnapshot(h);
            Assert.That(h.Hud.RefreshDisplay(), Is.True);
            Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            Assert.That(h.Hud.RewardImage.enabled, Is.False); Assert.That(h.Hud.RewardImage.sprite, Is.Null);
            Assert.That(h.Hud.NextGradeText.text, Is.EqualTo("보상 없음"));
            snapshot.AssertUnchanged(h);
        }
    }

    [TestCase("missing")]
    [TestCase("swapped")]
    [TestCase("duplicate")]
    public void InvalidApprovedMappingFailsClosedWithoutChangingTransactions(string defect)
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C); h.Hud.RefreshDisplay();
            object original = Read(h.Hud, "_cSprite");
            if (defect == "missing") Write(h.Hud, "_cSprite", null);
            else if (defect == "swapped")
            { Write(h.Hud, "_cSprite", Read(h.Hud, "_bSprite")); Write(h.Hud, "_bSprite", original); }
            else Write(h.Hud, "_cSprite", Read(h.Hud, "_sssSprite"));
            var snapshot = new TransactionSnapshot(h);
            Assert.That(h.Hud.ValidateMapping(out _), Is.False);
            ExpectHudError(); Assert.That(h.Hud.RefreshDisplay(), Is.False);
            Assert.That(h.Hud.IsVisible, Is.False); snapshot.AssertUnchanged(h);
            Write(h.Hud, "_cSprite", original); Write(h.Hud, "_bSprite", Read(h.Box, "_bSprite"));
        }
    }

    [Test]
    public void ScoreObserverOrderCannotReadThePreviousGradeDuringTheSaleCallback()
    {
        using (var h = new Host())
        {
            h.Call("StartCustomers", 3, 90d); h.Hud.RefreshDisplay();
            var original = (Action)Read(h.Score, "Changed");
            var observers = original.GetInvocationList();
            Action reordered = null;
            foreach (var callback in observers.Where(d => ReferenceEquals(d.Target, h.Hud))) reordered += (Action)callback;
            bool beforeGradeWasObserved = false;
            reordered += () =>
            {
                beforeGradeWasObserved = true;
                Assert.That(h.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.None));
                Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(PopupStoreGradeTier.None));
            };
            foreach (var callback in observers.Where(d => !ReferenceEquals(d.Target, h.Hud))) reordered += (Action)callback;
            Write(h.Score, "Changed", reordered);
            try
            {
                h.Call("Pack", 0);
                Assert.That(beforeGradeWasObserved, Is.True);
                Assert.That(h.Grade.Current.Grade, Is.EqualTo(PopupStoreGradeTier.C));
                Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(PopupStoreGradeTier.None));
                Invoke(h.Hud, "LateUpdate");
                Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(PopupStoreGradeTier.C));
                h.Call("CompleteVisual", true); h.Call("FinishDepartures");
            }
            finally { Write(h.Score, "Changed", original); }
        }
    }

    [Test]
    public void RepeatedRefreshAndIdleLateUpdatesDoNotDuplicateSubscriptionsOrRewardWork()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C);
            var snapshot = new TransactionSnapshot(h);
            for (int i = 0; i < 20; i++) { h.Hud.RefreshDisplay(); Invoke(h.Hud, "LateUpdate"); }
            Assert.That(CountObservers(h.Score, "Changed", h.Hud), Is.EqualTo(1));
            Assert.That(CountObservers(h.Clock, "StateChanged", h.Hud), Is.EqualTo(1));
            Assert.That(CountObservers(h.Grade, "Finalized", h.Hud), Is.EqualTo(1));
            Assert.That(Read(h.Hud, "_dirty"), Is.False);
            snapshot.AssertUnchanged(h);
        }
    }

    [Test]
    public void HiddenViewUnsubscribesAndReopensWithTheSameCachedGradeOnlyOnce()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C); h.Hud.RefreshDisplay();
            var sprite = h.Hud.DisplayedSprite;
            h.Call("Close");
            Assert.That(h.Hud.IsVisible, Is.False);
            Assert.That(CountObservers(h.Score, "Changed", h.Hud), Is.Zero);
            Assert.That(CountObservers(h.Clock, "StateChanged", h.Hud), Is.Zero);
            Assert.That(CountObservers(h.Grade, "Finalized", h.Hud), Is.Zero);
            Assert.That(h.Hud.RefreshDisplay(), Is.False);
            h.Call("Reopen"); h.Hud.RefreshDisplay();
            Assert.That(h.Hud.DisplayedSprite, Is.SameAs(sprite));
            Assert.That(CountObservers(h.Score, "Changed", h.Hud), Is.EqualTo(1));
            Assert.That(h.Reward.RandomDrawCount, Is.Zero); Assert.That(h.Grade.FinalizationCount, Is.Zero);
        }
    }

    [TestCase("foreign")]
    [TestCase("result")]
    [TestCase("result-frame")]
    [TestCase("result-confirm")]
    public void SubstitutedForeignOrResultGraphicIsNotMutated(string source)
    {
        using (var h = new Host())
        using (var other = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C); h.Hud.RefreshDisplay();
            var original = h.Hud.RewardImage;
            var replacement = source == "foreign" ? other.Hud.RewardImage : source == "result-frame"
                ? (Image)Read(h.Panel, "_frame") : source == "result-confirm"
                ? ((Button)Read(h.Panel, "_confirmButton")).targetGraphic as Image : h.Box.BoxImage;
            Assert.That(replacement, Is.Not.Null);
            var sprite = replacement.sprite; bool enabled = replacement.enabled;
            Write(h.Hud, "_rewardImage", replacement);
            ExpectHudError(); Assert.That(h.Hud.RefreshDisplay(), Is.False);
            Assert.That(replacement.sprite, Is.SameAs(sprite)); Assert.That(replacement.enabled, Is.EqualTo(enabled));
            Assert.That(original.enabled, Is.False); Write(h.Hud, "_rewardImage", original);
        }
    }

    [Test]
    public void DirectExitHidesCurrentGradeWithoutInventingAFinalResultOrReward()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C); h.Hud.RefreshDisplay();
            Assert.That((bool)Invoke(h.Clock, "TryAbandonSession"), Is.True);
            var snapshot = new TransactionSnapshot(h);
            Assert.That(h.Hud.RefreshDisplay(), Is.True);
            Assert.That(h.Hud.IsVisible, Is.False);
            Assert.That(h.Grade.FinalResult, Is.Null); Assert.That(h.Reward.Result, Is.Null);
            snapshot.AssertUnchanged(h);
        }
    }

    [Test]
    public void FinalResultConfirmationAndRealViewReplacementStartWithoutThePreviousHud()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C); h.Call("End"); h.Call("ShowResult");
            h.Hud.RefreshDisplay();
            string oldId = h.Grade.SessionId;
            var previousScoreEvents = (Action)Read(h.Score, "Changed");
            Assert.That(h.Panel.TryConfirm(), Is.True);
            h.Entry.onClick.Invoke();
            var next = (UIPopupStore)h.Navigation.FirstView;
            var nextGrade = next.GetComponent<PopupStoreGradeSession>();
            var nextHud = next.GetComponent<PopupStoreCurrentGradeHud>();
            Assert.That(nextGrade.SessionId, Is.Not.EqualTo(oldId));
            Assert.That(nextGrade.FinalResult, Is.Null); Assert.That(nextGrade.Current.Score, Is.Zero);
            Assert.That(next.GetComponent<PopupStoreBusinessClock>().State, Is.EqualTo(PopupStoreBusinessState.NotStarted));
            Assert.That(nextHud.RefreshDisplay(), Is.True); Assert.That(nextHud.IsVisible, Is.False);
            // Late callbacks retain old objects; they must never carry an old grade into the replacement.
            foreach (var callback in previousScoreEvents.GetInvocationList().Where(d => d.Target is PopupStoreCurrentGradeHud)) callback.DynamicInvoke();
            Invoke(nextHud, "LateUpdate"); Assert.That(nextHud.IsVisible, Is.False);
            Assert.That(next.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            Assert.That(nextGrade.FinalizationCount, Is.Zero);
        }
    }

    [Test]
    public void InspectorVisibilityAndFormatChangeOnlyTheDedicatedGraphics()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C);
            var position = h.Hud.RewardImage.rectTransform.anchoredPosition;
            var size = h.Hud.RewardImage.rectTransform.sizeDelta;
            var scale = h.Hud.RewardImage.rectTransform.localScale;
            var snapshot = new TransactionSnapshot(h);
            Write(h.Hud, "_showImage", false); Write(h.Hud, "_nextGradeFormat", "{0}: {1}");
            Assert.That(h.Hud.RefreshDisplay(), Is.True); Assert.That(h.Hud.IsVisible, Is.False);
            Assert.That(h.Hud.NextGradeText.text, Is.EqualTo("B: 600"));
            Write(h.Hud, "_showImage", true); Write(h.Hud, "_showNextGrade", false);
            Assert.That(h.Hud.RefreshDisplay(), Is.True); Assert.That(h.Hud.IsVisible, Is.True);
            Assert.That(h.Hud.NextGradeText.enabled, Is.False);
            Assert.That(h.Hud.RewardImage.rectTransform.anchoredPosition, Is.EqualTo(position));
            Assert.That(h.Hud.RewardImage.rectTransform.sizeDelta, Is.EqualTo(size));
            Assert.That(h.Hud.RewardImage.rectTransform.localScale, Is.EqualTo(scale));
            snapshot.AssertUnchanged(h);
        }
    }

    [Test]
    public void InvalidTextFormatDoesNotAffectTheReceiptOrFinalResult()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.C);
            var snapshot = new TransactionSnapshot(h); Write(h.Hud, "_nextGradeFormat", "{missing}");
            ExpectHudError(); Assert.That(h.Hud.RefreshDisplay(), Is.False);
            snapshot.AssertUnchanged(h); Write(h.Hud, "_nextGradeFormat", "다음 {0}까지 {1}점");
            Assert.That(h.Hud.RefreshDisplay(), Is.True);
        }
    }

    [Test]
    public void NormalCloseReadsTheSameFinalGradeWithoutChangingResultBoxAnimationOrR1Decision()
    {
        using (var h = new Host())
        {
            h.Start(); h.Call("SellUntil", PopupStoreGradeTier.S); h.Call("End"); h.Call("ShowResult");
            var snapshot = new TransactionSnapshot(h); int animations = h.Box.AnimationCount;
            for (int i = 0; i < 5; i++) Assert.That(h.Hud.RefreshDisplay(), Is.True);
            Assert.That(h.Hud.DisplayedGrade, Is.EqualTo(h.Grade.FinalResult.Grade));
            Assert.That(h.Hud.DisplayedSprite, Is.SameAs(h.Box.DisplayedSprite));
            Assert.That(h.Box.AnimationCount, Is.EqualTo(animations));
            Assert.That(h.Grade.FinalizationCount, Is.EqualTo(1)); Assert.That(h.Reward.RandomDrawCount, Is.EqualTo(1));
            snapshot.AssertUnchanged(h);
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        internal Host()
        {
            var type = typeof(PopupStoreResultTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { null }, CultureInfo.InvariantCulture);
            // The older EditMode host explicitly invokes component lifecycles; these two observers
            // were added after that host and must be bound here just as they are by OnEnable in Play.
            try
            {
                Assert.That(Hud, Is.Not.Null); Assert.That(Reward.Refresh(), Is.True);
                Assert.That(Hud.RefreshDisplay(), Is.True);
            }
            catch { Dispose(); throw; }
        }
        internal GameObject Root => (GameObject)Read(_inner, "Root");
        internal PopupStoreCurrentGradeHud Hud => Root.GetComponent<PopupStoreCurrentGradeHud>();
        internal PopupStoreRewardBox Box => Root.GetComponent<PopupStoreRewardBox>();
        internal PopupStoreGradeSession Grade => Root.GetComponent<PopupStoreGradeSession>();
        internal PopupStoreScore Score => Root.GetComponent<PopupStoreScore>();
        internal PopupStorePacking Packing => Root.GetComponent<PopupStorePacking>();
        internal PopupStoreRewardSession Reward => Root.GetComponent<PopupStoreRewardSession>();
        internal PopupStoreResultPanel Panel => Root.GetComponent<PopupStoreResultPanel>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal MobileUINavigation Navigation => (MobileUINavigation)Read(_inner, "Navigation");
        internal Button Entry => (Button)Read(_inner, "Entry");
        internal object Call(string name, params object[] args)
        {
            object result = Invoke(_inner, name, args);
            if (name == "Close") Invoke(Hud, "OnDisable");
            else if (name == "Reopen") Invoke(Hud, "OnEnable");
            return result;
        }
        internal void Start(double seconds = 90) => Call("Start", seconds);
        public void Dispose()
        {
            if (Root != null)
            {
                if (Hud != null) Invoke(Hud, "OnDestroy");
                if (Reward != null) Invoke(Reward, "OnDestroy");
            }
            ((IDisposable)_inner).Dispose();
        }
    }

    private sealed class TransactionSnapshot
    {
        private readonly object _final, _reward;
        private readonly int _score, _orders, _packs, _finalizations, _draws, _decisions;
        private readonly PopupStorePackingReceipt[] _committed, _queued;
        internal TransactionSnapshot(Host h)
        {
            _final = h.Grade.FinalResult; _reward = h.Reward.Result;
            _score = h.Score.TotalScore; _orders = h.Score.SuccessfulOrderCount;
            _packs = h.Packing.SuccessfulPackCount; _finalizations = h.Grade.FinalizationCount;
            _draws = h.Reward.RandomDrawCount; _decisions = h.Reward.DecisionCount;
            _committed = h.Packing.CommittedReceipts.ToArray();
            _queued = ((Queue<PopupStorePackingReceipt>)Read(h.Packing, "_receipts")).ToArray();
        }
        internal void AssertUnchanged(Host h)
        {
            Assert.That(h.Grade.FinalResult, Is.SameAs(_final)); Assert.That(h.Reward.Result, Is.SameAs(_reward));
            Assert.That(h.Score.TotalScore, Is.EqualTo(_score)); Assert.That(h.Score.SuccessfulOrderCount, Is.EqualTo(_orders));
            Assert.That(h.Packing.SuccessfulPackCount, Is.EqualTo(_packs)); Assert.That(h.Grade.FinalizationCount, Is.EqualTo(_finalizations));
            Assert.That(h.Reward.RandomDrawCount, Is.EqualTo(_draws)); Assert.That(h.Reward.DecisionCount, Is.EqualTo(_decisions));
            CollectionAssert.AreEqual(_committed, h.Packing.CommittedReceipts.ToArray());
            CollectionAssert.AreEqual(_queued, ((Queue<PopupStorePackingReceipt>)Read(h.Packing, "_receipts")).ToArray());
        }
    }
    private static int CountObservers(object target, string name, object observer)
        => ((Delegate)Read(target, name))?.GetInvocationList().Count(d => ReferenceEquals(d.Target, observer)) ?? 0;
    private static void ExpectHudError() => LogAssert.Expect(LogType.Error, new Regex("^PopupStoreCurrentGradeHud:"));
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    private static object Invoke(object target, string name, params object[] args)
    {
        try { return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
}
#endif
