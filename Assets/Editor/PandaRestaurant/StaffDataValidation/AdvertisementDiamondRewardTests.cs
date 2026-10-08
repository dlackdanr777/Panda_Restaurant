#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using LitJson;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed partial class AdvertisementDiamondRewardTests
{
    [Test]
    public void Rewarded_ThreeSuccessfulAdsGrantNineAndSaveTheExistingDailyLimit()
    {
        using (var scope = new Scope())
        {
            Assert.That(ConstValue.DAILY_AD_DIA_REWARD_COUNT, Is.EqualTo(3));
            Assert.That(scope.Label.text, Is.EqualTo("3"));
            for (int i = 1; i <= 3; i++)
            {
                scope.Fire("OnAdButtonClicked"); scope.Fire("OnAdRewarded");
                Assert.That(UserInfo.Dia, Is.EqualTo(100 + 3 * i));
                Assert.That(UserInfo.DailyAdDiaRewardCount, Is.EqualTo(i));
                Assert.That(scope.Slot.AnimationCount, Is.EqualTo(3));
                var restored = new LoadUserData(JsonMapper.ToObject("[" + scope.Slot.SavedJson + "]"));
                Assert.That(restored.IsValid, Is.True);
                Assert.That(restored.Dia, Is.EqualTo(UserInfo.Dia));
                Assert.That(restored.DailyAdDiaRewardCount, Is.EqualTo(i));
            }
            scope.Fire("OnAdButtonClicked"); scope.Fire("OnAdRewarded");
            Assert.That(UserInfo.Dia, Is.EqualTo(109));
            Assert.That(scope.Slot.Saves, Is.EqualTo(3));
            Assert.That(scope.Count.text, Is.EqualTo("0/3"));
        }
    }

    [TestCase("failure")]
    [TestCase("close-only")]
    [TestCase("unrequested")]
    [TestCase("duplicate")]
    [TestCase("closed-then-rewarded")]
    [TestCase("reentrant")]
    public void RewardCallbacks_GrantOnlyOnceForOneSuccessfulRequest(string scenario)
    {
        using (var scope = new Scope())
        {
            if (scenario != "unrequested") scope.Fire("OnAdButtonClicked");
            if (scenario == "failure") scope.Fire("OnAdDisplayFailed");
            if (scenario == "close-only" || scenario == "closed-then-rewarded") scope.Fire("OnAdClosed");
            if (scenario == "reentrant") scope.Slot.Reenter = () => scope.Fire("OnAdRewarded");
            if (scenario != "close-only") scope.Fire("OnAdRewarded");
            if (scenario == "duplicate") scope.Fire("OnAdRewarded");
            bool success = scenario == "duplicate" || scenario == "closed-then-rewarded" || scenario == "reentrant";
            Assert.That(UserInfo.Dia, Is.EqualTo(success ? 103 : 100));
            Assert.That(UserInfo.DailyAdDiaRewardCount, Is.EqualTo(success ? 1 : 0));
            Assert.That(scope.Slot.Saves, Is.EqualTo(success ? 1 : 0));
        }
    }

    [Test]
    public void ReinitializingSlot_DoesNotDuplicateTheProductionRewardListener()
    {
        using (var scope = new Scope())
        {
            scope.Slot.Init(null, MoneyType.Dia);
            scope.Slot.Init(null, MoneyType.Dia);
            Assert.That(scope.Listeners("OnAdRewarded"), Is.EqualTo(1));
            scope.Fire("OnAdButtonClicked"); scope.Fire("OnAdRewarded");
            Assert.That(UserInfo.Dia, Is.EqualTo(103));
            Assert.That(scope.Slot.Saves, Is.EqualTo(1));
        }
    }

    public sealed class IsolatedDiamondSlot : UIPaymentAdSlot
    {
        public int Saves, AnimationCount;
        public string SavedJson;
        public Action Reenter;
        protected override void CompleteDiamondReward(int amount)
        {
            AnimationCount = amount;
            SavedJson = UserInfo.GetSaveUserData().GetJson();
            Saves++;
            Reenter?.Invoke();
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly Dictionary<FieldInfo, object> _originals = new Dictionary<FieldInfo, object>();
        private readonly GameObject _root;
        private readonly WatchAdButton _watch;
        internal readonly IsolatedDiamondSlot Slot;
        internal readonly TextMeshProUGUI Label, Count;
        internal Scope()
        {
            Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
            _root = new GameObject("SDK-free diamond reward callbacks"); _root.SetActive(false);
            Replace(typeof(UserInfo), "_dia", 100);
            Replace(typeof(UserInfo), "_dailyAdDiaRewardCount", 0);
            Replace(typeof(UserInfo), "_staffCostCommitDepth", 1);
            Replace(typeof(UserInfo), "_diamondDataGeneration", 0L);
            Replace(typeof(UserInfo), "_userId", "offline-phase4-advertisement");
            Replace(typeof(UserInfo), "_firstAccessTime", "2026-10-02T00:00:00Z");
            Replace(typeof(UserInfo), "_lastAttendanceTime", "2026-10-02T00:00:00Z");
            Replace(typeof(TimeManager), "_instance", _root.AddComponent<TimeManager>());
            var backend = _root.AddComponent<Muks.BackEnd.BackendManager>();
            Set(backend, "_cachedServerTime", new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc));
            Set(backend, "_serverTimeCachedAt", float.PositiveInfinity);
            Replace(typeof(Muks.BackEnd.BackendManager), "_instance", backend);
            _watch = _root.AddComponent<WatchAdButton>();
            Slot = _root.AddComponent<IsolatedDiamondSlot>();
            Label = Text("Reward"); Count = Text("Daily count");
            Set(Slot, "_watchAdButton", _watch); Set(Slot, "_valueText", Label); Set(Slot, "_countText", Count);
            Slot.Init(null, MoneyType.Dia);
        }
        private TextMeshProUGUI Text(string name)
        {
            var child = new GameObject(name, typeof(RectTransform)); child.transform.SetParent(_root.transform, false);
            return child.AddComponent<TextMeshProUGUI>();
        }
        internal void Fire(string name) => ((Action)Field(typeof(WatchAdButton), name).GetValue(_watch))?.Invoke();
        internal int Listeners(string name) => ((Action)Field(typeof(WatchAdButton), name).GetValue(_watch))?.GetInvocationList().Length ?? 0;
        private void Replace(Type type, string name, object value)
        { var f = Field(type, name); _originals.Add(f, f.GetValue(null)); f.SetValue(null, value); }
        private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        private static FieldInfo Field(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(name);
        }
        public void Dispose()
        {
            Object.DestroyImmediate(_root);
            foreach (var pair in _originals) pair.Key.SetValue(null, pair.Value);
        }
    }
}
#endif
