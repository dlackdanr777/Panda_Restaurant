#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Approved gauge artwork follows the existing timer; display updates do not advance gameplay.</summary>
public sealed class PopupStorePatienceSkinTests
{
    private const string PrefabPath = "Assets/Resources/UI/PopupStore/UIPopupStore.prefab";
    private const string ArtPath = "Assets/Sprites/PopupStore/CleanPartsV4/";

    [Test]
    public void AuthoredPatienceSkinUsesTheThreeApprovedDistinctFillSpritesAndExistingThresholds()
    {
        var patience = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<PopupStoreCustomerPatience>();
        string[] fields = { "_normalSprite", "_cautionSprite", "_urgentSprite" };
        // The v4 approval changes artwork, never timer policy or its colour thresholds.
        string[] files = { "V4_Patience_Fill_Green.png", "V4_Patience_Fill_Yellow.png", "V4_Patience_Fill_Red.png" };
        var ids = fields.Select(name => (Sprite)Read(patience, name)).ToArray();
        Assert.That(ids.All(sprite => sprite != null), Is.True);
        CollectionAssert.AllItemsAreUnique(ids.Select(sprite => sprite.GetInstanceID()).ToArray());
        for (int i = 0; i < fields.Length; i++) Assert.That(AssetDatabase.GetAssetPath(ids[i]), Is.EqualTo(ArtPath + files[i]));
        Assert.That(Read(patience, "_cautionThreshold"), Is.EqualTo(.5f));
        Assert.That(Read(patience, "_urgentThreshold"), Is.EqualTo(.2f));
        Assert.That(Read(patience, "_onePlushSeconds"), Is.EqualTo(18d));
        Assert.That(Read(patience, "_twoPlushSeconds"), Is.EqualTo(22d));
        Assert.That(Read(patience, "_threePlushSeconds"), Is.EqualTo(26d));
    }

    [TestCase(0d, 1f, "_normalSprite")]
    [TestCase(9d, .5f, "_cautionSprite")]
    [TestCase(14.4d, .2f, "_urgentSprite")]
    [TestCase(18d, 0f, null)]
    public void ActualElapsedTimeSelectsApprovedArtworkAndZeroHidesWithoutAnyDisplaySideEffects(
        double elapsed, float expectedRatio, string spriteField)
    {
        using (var h = new Host())
        {
            h.StartThree();
            var timers = Enumerable.Range(0, 3).Select(h.Patience.GetTimer).ToArray();
            Assert.That(h.Clock.AdvanceTime(elapsed), Is.True);
            Assert.That(timers[0].RemainingPatience, Is.EqualTo(18d - elapsed).Within(.000001));
            Assert.That(timers[1].RemainingPatience, Is.EqualTo(22d - elapsed).Within(.000001));
            Assert.That(timers[2].RemainingPatience, Is.EqualTo(26d - elapsed).Within(.000001));
            if (expectedRatio > 0)
            {
                Assert.That(h.Fill(0).sprite, Is.SameAs(Read(h.Patience, spriteField)));
                Assert.That(h.Fill(0).color, Is.EqualTo(Color.white));
                Assert.That(h.Fill(0).fillAmount, Is.EqualTo(expectedRatio).Within(.000001));
                Assert.That(h.Gauge(0).activeInHierarchy && h.Fill(0).isActiveAndEnabled, Is.True);
            }
            else
            {
                Assert.That(timers[0].IsExpired, Is.True);
                Assert.That(h.Gauge(0).activeInHierarchy || h.Fill(0).isActiveAndEnabled, Is.False,
                    "A terminal zero gauge is hidden; no stale colored geometry remains visible.");
                Assert.That(h.Patience.ExpiredCount, Is.EqualTo(1));
                Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(1));
            }
            var remaining = timers.Select(t => t.RemainingPatience).ToArray();
            var expired = timers.Select(t => t.IsExpired).ToArray();
            int expirations = h.Patience.ExpiredCount, penalties = h.Clock.ComplaintPenaltyCount;
            double businessRemaining = h.Clock.RemainingSeconds, businessElapsed = h.Clock.ElapsedSeconds;
            for (int i = 0; i < 10; i++) Invoke(h.Patience, "UpdateViews");
            CollectionAssert.AreEqual(remaining, timers.Select(t => t.RemainingPatience).ToArray());
            CollectionAssert.AreEqual(expired, timers.Select(t => t.IsExpired).ToArray());
            Assert.That(h.Clock.RemainingSeconds, Is.EqualTo(businessRemaining));
            Assert.That(h.Clock.ElapsedSeconds, Is.EqualTo(businessElapsed));
            Assert.That(h.Clock.ComplaintPenaltyCount, Is.EqualTo(penalties));
            Assert.That(h.Patience.ExpiredCount, Is.EqualTo(expirations));
            Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
            Assert.That(h.Root.GetComponent<PopupStoreScore>().TotalScore, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreRewardSession>().RandomDrawCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStoreGradeSession>().FinalizationCount, Is.Zero);
        }
    }

    [Test]
    public void OptionalArtworkCanBeOmittedWhileTheOriginalColorThresholdsAndTimerRemainUsable()
    {
        using (var h = new Host())
        {
            Write(h.Patience, "_normalSprite", null); Write(h.Patience, "_cautionSprite", null); Write(h.Patience, "_urgentSprite", null);
            h.StartThree();
            var timer = h.Patience.GetTimer(0);
            Assert.That(h.Fill(0).color, Is.EqualTo(Read(h.Patience, "_normalColor")));
            h.Clock.AdvanceTime(9);
            Assert.That(h.Fill(0).color, Is.EqualTo(Read(h.Patience, "_cautionColor")));
            h.Clock.AdvanceTime(6);
            Assert.That(h.Fill(0).color, Is.EqualTo(Read(h.Patience, "_urgentColor")));
            Assert.That(h.Patience.GetTimer(0), Is.SameAs(timer));
            Assert.That(timer.RemainingPatience, Is.EqualTo(3d));
            Assert.That(h.Patience.ExpiredCount, Is.Zero); Assert.That(h.Clock.ComplaintPenaltyCount, Is.Zero);
            Assert.That(h.Root.GetComponent<PopupStorePacking>().CommittedReceipts, Is.Empty);
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly object _inner;
        private readonly long _money = UserInfo.Money, _context = UserInfo.CustomerEncounterContextGeneration;
        private readonly int _dia = UserInfo.Dia;
        private readonly bool _backend = BackEnd.Backend.IsInitialized;
        private readonly float _timeScale = Time.timeScale;
        internal Host()
        {
            var type = typeof(PopupStoreCustomerPatienceTests).GetNestedType("Host", BindingFlags.NonPublic);
            _inner = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { true, null, null }, CultureInfo.InvariantCulture);
        }
        internal GameObject Root => (GameObject)Read(_inner, "Root");
        internal PopupStoreCustomerPatience Patience => Root.GetComponent<PopupStoreCustomerPatience>();
        internal PopupStoreBusinessClock Clock => Root.GetComponent<PopupStoreBusinessClock>();
        internal Image Fill(int index) => (Image)Invoke(_inner, "FillImage", index);
        internal GameObject Gauge(int index) => (GameObject)Invoke(_inner, "GaugeRoot", index);
        internal void StartThree() => Invoke(_inner, "StartThree");
        public void Dispose()
        {
            ((IDisposable)_inner).Dispose();
            Assert.That(UserInfo.Money, Is.EqualTo(_money)); Assert.That(UserInfo.Dia, Is.EqualTo(_dia));
            Assert.That(UserInfo.CustomerEncounterContextGeneration, Is.EqualTo(_context));
            Assert.That(BackEnd.Backend.IsInitialized, Is.EqualTo(_backend)); Assert.That(Time.timeScale, Is.EqualTo(_timeScale));
        }
    }
    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
