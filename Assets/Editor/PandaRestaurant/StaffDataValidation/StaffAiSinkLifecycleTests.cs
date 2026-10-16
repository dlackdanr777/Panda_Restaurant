#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;
using Object = UnityEngine.Object;

// Production ChefAction chooses the burner and reacts to the real full-sink state.
// Only decision time, arrival and already-scheduled waits are advanced explicitly.
// These direct callback regressions do not claim PlayerLoop or path verification.
public sealed class StaffAiSinkLifecycleTests
{
    [Test]
    public void FullSinkDuringCooking_NextDecisionPreservesPendingSinkArrival()
        => RunFullSinkTransition(true);

    [Test]
    public void FullSinkDuringCooking_ArrivalKeepsWashingDespitePendingCooking()
        => RunFullSinkTransition(false);

    private static void RunFullSinkTransition(bool decideDuringTravel)
    {
        using (var s = new Flow())
        {
            var speeds = new Dictionary<StaffGroupType, float>();
            foreach (StaffGroupType role in Enum.GetValues(typeof(StaffGroupType)))
                if (role != StaffGroupType.Length) speeds.Add(role, 0f);
            Set(GameManager.Instance, "_addGachaItemStaffSpeedMulDic", speeds);

            Set(s.Kitchen, "_defaultChef1Pos", s.Create<Transform>());
            var door1 = s.Create<Transform>(); door1.position = new Vector3(1f, 0f, 0f);
            var door2 = s.Create<Transform>(); door2.position = new Vector3(1f, 7.64f, 0f);
            Set(s.Kitchen, "_door1", door1); Set(s.Kitchen, "_door2", door2);
            var sink = s.Create<SinkKitchenUtensil>(); sink.transform.position = new Vector3(-4f, 0f, 0f);
            Set(sink, "_washingEffect", s.Create<Transform>().gameObject);
            Set(sink, "_washingSound", s.Create<AudioSource>());
            Set(sink, "_chefEffect", s.Create<ParticleSystem>());
            Set(s.Kitchen, "_sinkKitchenUtensil", sink);
            var burner = Get<KitchenBurnerData[]>(s.Kitchen, "_burnerDatas")[0];
            var utensil = s.Create<BurnerKitchenUtensil>(); utensil.transform.position = new Vector3(4f, 0f, 0f);
            burner.SetKitchenUtensil(utensil);

            s.GuideAndSeat(); s.Manager.OnCustomerOrder(s.Table); Invoke(s.Kitchen, "DequeueFood", 0);
            Assert.That(burner.CookingData.IsDefault(), Is.False);
            var originalOrder = Get<object>(burner.CookingData, "_identity");
            var staff = CreateChef(s);
            try
            {
                var action = Get<IStaffAction>(staff, "_staffAction");
                Set(action, "_time", 2f); staff.StaffAction();
                Assert.That(burner.UseStaff, Is.SameAs(staff), "The actual decision must select existing cooking work.");
                s.Arrive(staff); s.TakeWait(1f)();
                Assert.That(utensil.IsStaffWorking, Is.True);
                Assert.That(burner.AddCookSpeedMul, Is.EqualTo(staff.GetActionValue()));

                // Fill only this memory account through the existing bowl API while
                // the chef is working. Do not force a busy flag or choose a sink job.
                int capacity = s.Stage.GetMaxSinkBowlCount(s.Table.FloorType);
                for (int i = s.Stage.GetSinkBowlCount(s.Table.FloorType); i < capacity; i++)
                    UserInfo.AddSinkBowlCount(UserInfo.CurrentStage, s.Table.FloorType);
                Assert.That(UserInfo.GetBowlAddEnabled(UserInfo.CurrentStage, s.Table.FloorType), Is.False);
                s.TakeWait(.5f)();
                Assert.That(burner.UseStaff, Is.Null, "A full sink must release the cooking assignment.");
                Assert.That(utensil.IsStaffWorking, Is.False);
                Assert.That(sink.UseStaff, Is.SameAs(staff));
                var sinkArrival = Get<Action>(staff, "_moveCompleted");
                Assert.That(sinkArrival, Is.Not.Null);
                Assert.That(Get<Vector2>(staff, "_targetPos"), Is.EqualTo((Vector2)sink.transform.position));

                if (decideDuringTravel)
                {
                    Set(action, "_time", 2f); staff.StaffAction();
                    Assert.That(Get<Action>(staff, "_moveCompleted"), Is.SameAs(sinkArrival),
                        "A new decision replaced the pending full-sink arrival with cooking travel before any dishes could be washed.");
                    Assert.That(burner.UseStaff, Is.Null, "The same chef must not reserve a burner while travelling to its owned full sink.");
                }

                s.Arrive(staff); s.TakeWait(1f)();
                Assert.That(sink.UseStaff, Is.SameAs(staff),
                    "The full-sink arrival released ownership immediately because the interrupted burner was still available.");
                Assert.That(sink.IsStaffWashing, Is.True,
                    "Existing pending cooking must not end full-sink washing before it can make any capacity available.");
                Assert.That(s.Stage.GetSinkBowlCount(s.Table.FloorType), Is.EqualTo(capacity));
                Assert.That(burner.CookingData.IsDefault(), Is.False, "Switching to dishes must preserve the unfinished food.");
                Assert.That(UserInfo.TotalCookCount, Is.Zero, "Neither reassignment nor sink work is a completed meal.");

                // Advance only the memory bowl counter through its production API.
                // This checks priority/ownership after washing, not washing duration.
                for (int i = 0; i < capacity; i++) UserInfo.SubSinkBowlCount(UserInfo.CurrentStage, s.Table.FloorType);
                s.TakeWait(.1f)();
                Assert.That(sink.UseStaff, Is.Null);
                Assert.That(sink.IsStaffWashing, Is.False);
                Set(action, "_time", 2f); staff.StaffAction();
                Assert.That(burner.UseStaff, Is.SameAs(staff), "After clearing the full sink, the next real decision must resume the preserved cooking work.");
                s.Arrive(staff); s.TakeWait(1f)();
                Assert.That(utensil.IsStaffWorking, Is.True);
                Assert.That(burner.AddCookSpeedMul, Is.EqualTo(staff.GetActionValue()));
                Assert.That(Get<object>(burner.CookingData, "_identity"), Is.SameAs(originalOrder));
                Assert.That(UserInfo.TotalCookCount, Is.Zero);
                staff.SetStaffData(null, s.Table.FloorType);
                Assert.That(burner.UseStaff, Is.Null);
                Assert.That(utensil.IsStaffWorking, Is.False);
                Assert.That(burner.AddCookSpeedMul, Is.Zero);
                Assert.That(sink.UseStaff, Is.Null);
                Assert.That(sink.IsStaffWashing, Is.False);
            }
            finally { staff.SetStaffData(null, s.Table.FloorType); }
        }
    }

    private static StaffChef CreateChef(Flow s)
    {
        var source = AssetDatabase.LoadAssetAtPath<StaffData>("Assets/Resources/StaffData/STAFF16.asset");
        Assert.That(source, Is.TypeOf<ChefData>());
        var data = s.Track(Object.Instantiate(source));
        Set(data, "_id", "STAFF-AI-SINK-STAFF16");
        Get<Dictionary<string, SaveStaffData>>(s.Stage, "_giveStaffDic")[data.Id] = new SaveStaffData(data.Id, 1);
        Get<Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>>(s.Stage, "_equipStaffTypeDic")[s.Table.FloorType][EquipStaffType.Chef] = data;
        s.Swap(typeof(StaffDataManager), "_instance", s.Create<StaffDataManager>());
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/Staff/Chef.prefab");
        Assert.That(prefab, Is.Not.Null);
        var staff = s.Track(Object.Instantiate(prefab)).GetComponent<StaffChef>();
        Assert.That(staff, Is.Not.Null);
        staff.Init(EquipStaffType.Chef, s.Manager, Get<KitchenSystem>(s.Manager, "_kitchenSystem"), s.Controller, Get<FeverSystem>(s.Manager, "_feverSystem"));
        staff.SetStaffData(data, s.Table.FloorType);
        Assert.That(staff.Level, Is.EqualTo(1));
        return staff;
    }
}
#endif
