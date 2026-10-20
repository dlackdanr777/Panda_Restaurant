#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Muks.Tween;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Flow = CustomerTableUseRegressionTests.Flow;
using static CustomerTableUseRegressionTests;

// Direct callback regression tests, not PlayerLoop/AI timing evidence. The original
// Guard/Gatecrasher prefabs and STAFF26/30/CUSTOMER81 data are only read/instantiated.
// Only the normal decision cooldown, movement arrival and saved tween callbacks are
// advanced explicitly. Assignment, target selection and attacks run production code.
public sealed class StaffAiGuardLifecycleTests
{
    [TestCase("departure", "same-assignment")]
    [TestCase("departure", "same-id-reuse")]
    [TestCase("departure", "replacement")]
    [TestCase("attack", "same-assignment")]
    [TestCase("attack", "same-id-reuse")]
    [TestCase("attack", "replacement")]
    public void PriorGuardCallback_CannotMoveOrAttackAfterAssignment(string boundary, string replacement)
    {
        using (var f = new GuardFixture())
        {
            var delayed = f.CaptureWork(boundary);
            long lifetime = f.Staff.WorkLifetime;
            f.Reassign(replacement);
            Assert.That(f.Staff.WorkLifetime, Is.GreaterThan(lifetime));
            var position = f.Staff.transform.position;
            var scale = f.Staff.SpriteRenderer.transform.localScale;
            var color = f.Staff.SpriteRenderer.color;
            float touches = f.TouchCount;
            var currentAction = Get<IStaffAction>(f.Staff, "_staffAction");

            delayed();
            // Advance any visual continuation incorrectly created by the stale wait.
            f.CompleteVisualIfPresent();
            f.CompleteVisualIfPresent();
            delayed();

            Assert.That(f.TouchCount, Is.EqualTo(touches), "An old action attacked after reassignment");
            Assert.That(f.Staff.transform.position, Is.EqualTo(position), "An old action moved the replacement employee");
            Assert.That(f.Staff.SpriteRenderer.transform.localScale, Is.EqualTo(scale));
            Assert.That(f.Staff.SpriteRenderer.color, Is.EqualTo(color));
            Assert.That(Get<IStaffAction>(f.Staff, "_staffAction"), Is.SameAs(currentAction));
            Assert.That(f.Gate.IsEndEvent, Is.False);
            Assert.That(f.S.Stage.Satisfaction, Is.EqualTo(-5), "Only the original gatecrasher arrival penalty is present");

            // A cancelled operation must also allow a fresh job on the current action.
            var next = f.CaptureWork("departure");
            Assert.That(next, Is.Not.Null);
        }
    }

    [Test]
    public void CurrentGuardWork_UsesAuthoredRatesAndActualTouchAction()
    {
        using (var f = new GuardFixture())
        {
            Assert.That(f.Original.GetActionValue(1), Is.EqualTo(5f));
            Assert.That(f.Replacement.GetActionValue(1), Is.EqualTo(6f));
            Assert.That(f.Staff.SpeedMul, Is.EqualTo(1f));
            Assert.That(f.Gate.TotalTouchCount, Is.EqualTo(30f));
            var attack = f.CaptureWork("attack");
            Assert.That(f.Staff.transform.position,
                Is.EqualTo(f.Gate.transform.position + new Vector3(.2f, 0)));
            attack();
            Assert.That(f.TouchCount, Is.EqualTo(1f));
            Assert.That(f.Gate.IsEndEvent, Is.False);
            Assert.That(f.S.Stage.Satisfaction, Is.EqualTo(-5));
            Assert.That(f.S.TakeWait(5f / 30f), Is.Not.Null,
                "The next attack retains the original per-touch interval");
        }
    }

    private sealed class GuardFixture : IDisposable
    {
        internal readonly Flow S;
        internal readonly StaffGuard Staff;
        internal readonly GatecrasherCustomer Gate;
        internal readonly GuardData Original;
        internal readonly GuardData Replacement;
        internal float TouchCount => Get<float>(Gate, "_currentTouchCount");

        internal GuardFixture()
        {
            S = new Flow();
            try
            {
                Original = AssetDatabase.LoadAssetAtPath<GuardData>("Assets/Resources/StaffData/STAFF26.asset");
                Replacement = AssetDatabase.LoadAssetAtPath<GuardData>("Assets/Resources/StaffData/STAFF30.asset");
                Assert.That(Original, Is.Not.Null); Assert.That(Replacement, Is.Not.Null);
                var owned = Get<Dictionary<string, SaveStaffData>>(S.Stage, "_giveStaffDic");
                owned[Original.Id] = new SaveStaffData(Original.Id, 1);
                owned[Replacement.Id] = new SaveStaffData(Replacement.Id, 1);
                Get<Dictionary<StaffGroupType, float>>(GameManager.Instance, "_addGachaItemStaffSpeedMulDic")[StaffGroupType.Guard] = 0;
                S.Swap(typeof(StaffDataManager), "_instance", S.Create<StaffDataManager>());

                var furniture = Get<FurnitureSystem>(S.Manager, "_furnitureSystem");
                var group = Get<Dictionary<ERestaurantFloorType, FurnitureGroup>>(furniture, "_furnitureGroupDic")[S.Table.FloorType];
                var home = S.Create<Transform>(); home.position = new Vector3(-4f, 0);
                Set(group, "_guardTr", home);
                Staff = S.Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/ObjectPool/Staff/Guard.prefab"))).GetComponent<StaffGuard>();
                Staff.Init(EquipStaffType.Guard, S.Manager, Get<KitchenSystem>(S.Manager, "_kitchenSystem"),
                    S.Controller, Get<FeverSystem>(S.Manager, "_feverSystem"));
                Assign(Original);

                var smokeParent = S.Track(new GameObject("Owned guard smoke"));
                S.Swap(typeof(ObjectPoolManager), "_particleParent", smokeParent);
                S.Swap(typeof(ObjectPoolManager), "_smokeParitclePool", new Queue<SmokeParticle>());
                S.Swap(typeof(ObjectPoolManager), "_smokeParticlePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/ObjectPool/SmokeParticle.prefab").GetComponent<SmokeParticle>());
                var gateData = AssetDatabase.LoadAssetAtPath<GatecrasherCustomer2Data>("Assets/Resources/CustomerData/CUSTOMER81.asset");
                Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic")[gateData.Id] = gateData;
                var records = (Dictionary<string, SaveCustomerData>)typeof(UserInfo).GetField("_enabledCustomerDic",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                records[gateData.Id] = new SaveCustomerData(gateData.Id, "", 0);
                Gate = S.Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/ObjectPool/GatecrasherCustomer.prefab"))).GetComponent<GatecrasherCustomer>();
                Gate.transform.position = Vector3.zero; // The fixture's floor-one door region.
                Gate.SetData(gateData, S.Controller, S.Manager);
                Gate.SetVisitFloor(S.Table.FloorType);
                S.Controller.GatecrasherCustomer[(int)S.Table.FloorType] = Gate;
                var target = new Vector3(7f, 0);
                Gate.StartGatecreasherCustomer2Event(target, S.Manager, null);
                var arrived = Get<Action>(Gate, "_moveCompleted");
                Gate.StopMove(); Gate.transform.position = target; arrived();
                S.TakeWait(1f)(); // Original arrival-to-action delay, not a changed rate.
                Assert.That(Gate.IsInDisguise, Is.False);
            }
            catch { S.Dispose(); throw; }
        }

        private void Assign(GuardData data)
        {
            Get<Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>>(S.Stage,
                "_equipStaffTypeDic")[S.Table.FloorType][EquipStaffType.Guard] = data;
            Staff.SetStaffData(data, S.Table.FloorType);
        }

        internal void Reassign(string mode)
        {
            if (mode == "same-id-reuse") Assign(null);
            Assign(mode == "replacement" ? Replacement : Original);
        }

        internal Action CaptureWork(string boundary)
        {
            var action = (GuardAction)Get<IStaffAction>(Staff, "_staffAction");
            Set(action, "_actionTimer", 0f); // Advance the 1-second decision timer only.
            Staff.StaffAction();
            var departure = S.TakeWait(1f / Staff.SpeedMul);
            if (boundary == "departure") return departure;
            departure();
            Assert.That(CompleteVisualIfPresent(), Is.True);
            Assert.That(CompleteVisualIfPresent(), Is.True);
            return S.TakeWait(Staff.GetActionValue() / Staff.SpeedMul / Gate.TotalTouchCount);
        }

        internal bool CompleteVisualIfPresent()
        {
            foreach (var tween in Staff.SpriteRenderer.GetComponents<TweenData>())
            {
                var callbacks = Get<Dictionary<int, Action>>(tween, "_onCompletedDic");
                var callback = callbacks.OrderBy(pair => pair.Key).Select(pair => pair.Value).FirstOrDefault();
                if (callback == null) continue;
                tween.Clear();
                callback();
                return true;
            }
            return false;
        }

        public void Dispose() => S.Dispose();
    }
}
#endif
