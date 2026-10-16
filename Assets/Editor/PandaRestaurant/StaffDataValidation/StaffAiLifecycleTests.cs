#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;
using Object = UnityEngine.Object;

// Production selection, reassignment and completion callbacks run in the existing
// memory fixture. Decision time and arrival are advanced explicitly: these cases
// do not claim PlayerLoop, path interpolation or manual UI verification.
public sealed class StaffAiLifecycleTests
{
    [Test]
    public void MailSubscribersDestroyedWithoutManager_DoNotCreateMailManager()
    {
        RequireMailSubscriberTeardownWithoutManagerCreation<UIMailAlarm>(false);
        RequireMailSubscriberTeardownWithoutManagerCreation<UIMailbox>(false);
    }

    [Test]
    public void MailSubscribersDestroyedAfterManager_DoNotRecreateMailManager()
    {
        RequireMailSubscriberTeardownWithoutManagerCreation<UIMailAlarm>(true);
        RequireMailSubscriberTeardownWithoutManagerCreation<UIMailbox>(true);
    }

    private static void RequireMailSubscriberTeardownWithoutManagerCreation<T>(bool destroySourceFirst) where T : MonoBehaviour
    {
        using (var s = new Flow())
        {
            var instanceField = typeof(MailManager).GetField("_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(instanceField, Is.Not.Null);
            var fixtureMailManager = instanceField.GetValue(null);
            MailManager source = destroySourceFirst ? s.Create<MailManager>() : null;
            s.Swap(typeof(MailManager), "_instance", source);
            var subscriber = s.Create<T>();
            // Seed only the source reference. Mail UI initialization and all
            // mailbox transport/account operations stay outside this teardown test.
            Set(subscriber, "_subscribedMailManager", source);
            try
            {
                if (destroySourceFirst)
                {
                    Object.DestroyImmediate(source.gameObject);
                    Assert.That(source == null, Is.True);
                }
                Invoke(subscriber, "OnDestroy");
                Assert.That(ReferenceEquals(instanceField.GetValue(null), source), Is.True,
                    typeof(T).Name + " teardown must not recreate an absent or destroyed MailManager.");
                Assert.That(Get<MailManager>(subscriber, "_subscribedMailManager"), Is.Null);
            }
            finally
            {
                var unexpected = instanceField.GetValue(null) as MailManager;
                if (unexpected != null && !ReferenceEquals(unexpected, source))
                    Object.DestroyImmediate(unexpected.gameObject);
                instanceField.SetValue(null, fixtureMailManager);
            }
        }
    }

    [Test]
    public void UIMainDestroyedWithoutTimeManager_DoesNotCreateTimeManager()
        => RequireTimeSubscriberTeardownWithoutManagerCreation<UIMain>(false);

    [Test]
    public void UIMainDestroyedAfterTimeManager_DoesNotRecreateTimeManager()
        => RequireTimeSubscriberTeardownWithoutManagerCreation<UIMain>(true);

    [Test]
    public void UIFeverDestroyedWithoutTimeManager_DoesNotCreateTimeManager()
        => RequireTimeSubscriberTeardownWithoutManagerCreation<UIFever>(false);

    [Test]
    public void UIFeverDestroyedAfterTimeManager_DoesNotRecreateTimeManager()
        => RequireTimeSubscriberTeardownWithoutManagerCreation<UIFever>(true);

    [Test]
    public void UIRecipePreviewDestroyedWithoutTimeManager_DoesNotCreateTimeManager()
        => RequireTimeSubscriberTeardownWithoutManagerCreation<UIRecipePreview>(false);

    [Test]
    public void UIRecipePreviewDestroyedAfterTimeManager_DoesNotRecreateTimeManager()
        => RequireTimeSubscriberTeardownWithoutManagerCreation<UIRecipePreview>(true);

    private static void RequireTimeSubscriberTeardownWithoutManagerCreation<T>(bool destroySourceFirst) where T : MonoBehaviour
    {
        using (var s = new Flow())
        {
            var instanceField = typeof(TimeManager).GetField("_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(instanceField, Is.Not.Null);
            var fixtureTimeManager = instanceField.GetValue(null);
            TimeManager source = destroySourceFirst ? s.Create<TimeManager>() : null;
            s.Swap(typeof(TimeManager), "_instance", source);
            var subscriber = s.Create<T>();
            // Isolate teardown: seed only the cached source, then destroy the real
            // Unity object. UI initialization, ads and purchases are not invoked.
            Set(subscriber, "_subscribedTimeManager", source);
            try
            {
                if (destroySourceFirst)
                {
                    Object.DestroyImmediate(source.gameObject);
                    Assert.That(source == null, Is.True);
                }
                Invoke(subscriber, "OnDestroy");
                Assert.That(ReferenceEquals(instanceField.GetValue(null), source), Is.True,
                    "UI teardown must not recreate an absent or destroyed TimeManager.");
                Assert.That(Get<TimeManager>(subscriber, "_subscribedTimeManager"), Is.Null);
            }
            finally
            {
                var unexpected = instanceField.GetValue(null) as TimeManager;
                if (unexpected != null && !ReferenceEquals(unexpected, source))
                    Object.DestroyImmediate(unexpected.gameObject);
                instanceField.SetValue(null, fixtureTimeManager);
            }
        }
    }

    [Test]
    public void CustomerDataManagerDestroyedBeforeInitialization_DoesNotCreateGameManager()
        => RequireCustomerDataManagerTeardownWithoutGameManagerCreation(false);

    [Test]
    public void CustomerDataManagerDestroyedAfterGameManager_DoesNotRecreateGameManager()
        => RequireCustomerDataManagerTeardownWithoutGameManagerCreation(true);

    private static void RequireCustomerDataManagerTeardownWithoutGameManagerCreation(bool destroySourceFirst)
    {
        using (var s = new Flow())
        {
            var instanceField = typeof(GameManager).GetField("_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(instanceField, Is.Not.Null);
            var fixtureGameManager = instanceField.GetValue(null);
            GameManager source = destroySourceFirst ? s.Create<GameManager>() : null;
            s.Swap(typeof(GameManager), "_instance", source);
            var customers = s.Create<CustomerDataManager>();
            try
            {
                // Exercise the real teardown boundary without running CSV/resource
                // initialization or changing the production subscription policy.
                if (destroySourceFirst)
                {
                    Object.DestroyImmediate(source.gameObject);
                    Assert.That(source == null, Is.True);
                }
                Assert.That(GameManager.HasInstance, Is.False);
                Invoke(customers, "OnDestroy");
                Assert.That(ReferenceEquals(instanceField.GetValue(null), source), Is.True,
                    "Customer data teardown must not recreate an absent or destroyed GameManager.");
                Assert.That(GameManager.HasInstance, Is.False);
            }
            finally
            {
                var unexpected = instanceField.GetValue(null) as GameManager;
                if (unexpected != null && !ReferenceEquals(unexpected, source))
                    Object.DestroyImmediate(unexpected.gameObject);
                instanceField.SetValue(null, fixtureGameManager);
            }
        }
    }

    [Test]
    public void FurnitureTutorialDestroyedBeforeStart_DoesNotCreateChallengeManager()
        => RequireTutorialTeardownWithoutManagerCreation<FurnitureTutorial>(false);

    [Test]
    public void FurnitureTutorialDestroyedAfterSubscriptionSource_DoesNotRecreateChallengeManager()
        => RequireTutorialTeardownWithoutManagerCreation<FurnitureTutorial>(true);

    [Test]
    public void RecipeTutorialDestroyedBeforeStart_DoesNotCreateChallengeManager()
        => RequireTutorialTeardownWithoutManagerCreation<RecipeTutorial>(false);

    [Test]
    public void RecipeTutorialDestroyedAfterSubscriptionSource_DoesNotRecreateChallengeManager()
        => RequireTutorialTeardownWithoutManagerCreation<RecipeTutorial>(true);

    private static void RequireTutorialTeardownWithoutManagerCreation<T>(bool startThenDestroySource) where T : MonoBehaviour
    {
        using (var s = new Flow())
        {
            var instanceField = typeof(ChallengeManager).GetField("_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.That(instanceField, Is.Not.Null);
            ChallengeManager source = startThenDestroySource ? s.Create<ChallengeManager>() : null;
            s.Swap(typeof(ChallengeManager), "_instance", source);
            var tutorial = s.Create<T>();
            try
            {
                if (startThenDestroySource)
                {
                    // Run the real subscription path without activating a manager
                    // or loading its production data during this detached test.
                    Invoke(tutorial, "Start");
                    Assert.That(Get<Action>(source, "OnMainChallengeUpdateHandler").Target, Is.SameAs(tutorial));
                    Object.DestroyImmediate(source.gameObject);
                    Assert.That(source == null, Is.True, "The subscription source must be a destroyed Unity object.");
                }
                Invoke(tutorial, "OnDestroy");
                Assert.That(ReferenceEquals(instanceField.GetValue(null), source), Is.True,
                    "Tutorial teardown must not access the creating singleton getter after its source is absent or destroyed.");
                Assert.That(Get<ChallengeManager>(tutorial, "_subscribedChallengeManager"), Is.Null);
            }
            finally
            {
                // Keep a regression failure from leaking a recreated singleton
                // into later cases; Flow restores the original static reference.
                var unexpected = instanceField.GetValue(null) as ChallengeManager;
                if (unexpected != null && !ReferenceEquals(unexpected, source))
                    Object.DestroyImmediate(unexpected.gameObject);
            }
        }
    }

    private static FurnitureGroup Hall(Flow s)
    {
        var furniture = Get<FurnitureSystem>(s.Manager, "_furnitureSystem");
        return Get<Dictionary<ERestaurantFloorType, FurnitureGroup>>(furniture, "_furnitureGroupDic")[s.Table.FloorType];
    }

    private static T CreateStaff<T>(Flow s, EquipStaffType role, string id, string prefabName) where T : Staff
    {
        var source = AssetDatabase.LoadAssetAtPath<StaffData>("Assets/Resources/StaffData/" + id + ".asset");
        Assert.That(source, Is.Not.Null);
        var data = s.Track(Object.Instantiate(source));
        Set(data, "_id", "STAFF-AI-LIFECYCLE-" + id);
        Get<Dictionary<string, SaveStaffData>>(s.Stage, "_giveStaffDic")[data.Id] = new SaveStaffData(data.Id, 1);
        Get<Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>>(s.Stage, "_equipStaffTypeDic")[s.Table.FloorType][role] = data;
        s.Swap(typeof(StaffDataManager), "_instance", s.Create<StaffDataManager>());
        Get<Dictionary<StaffGroupType, float>>(GameManager.Instance, "_addGachaItemStaffSpeedMulDic")[StaffDataManager.Instance.GetStaffGroupType(role)] = 0f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/Staff/" + prefabName + ".prefab");
        Assert.That(prefab, Is.Not.Null);
        var staff = s.Track(Object.Instantiate(prefab)).GetComponent<T>();
        Assert.That(staff, Is.Not.Null);
        staff.Init(role, s.Manager, Get<KitchenSystem>(s.Manager, "_kitchenSystem"), s.Controller, Get<FeverSystem>(s.Manager, "_feverSystem"));
        staff.SetStaffData(data, s.Table.FloorType);
        Assert.That(staff.Level, Is.EqualTo(1));
        return staff;
    }

    private static void DecideAfterInterval(Staff staff)
    {
        var action = Get<IStaffAction>(staff, "_staffAction");
        Assert.That(action, Is.Not.Null, "An assigned employee must keep an executable action.");
        // Only advance the existing decision interval. Do not alter busy flags,
        // reservations, selected targets or movement completion delegates.
        Set(action, "_time", 2f);
        staff.StaffAction();
    }

    private static void ReassignAndRequireWorkArrival(Staff staff, Vector2 expectedTarget, bool removeFirst = false)
    {
        var oldArrival = Get<Action>(staff, "_moveCompleted");
        var oldAction = Get<IStaffAction>(staff, "_staffAction");
        var data = staff.StaffData;
        var floor = staff.EquipFloorType;
        var position = staff.transform.position;
        long lifetime = staff.WorkLifetime;
        Assert.That(oldArrival, Is.Not.Null, "The production decision must first start work travel.");
        Assert.That(Get<Vector2>(staff, "_targetPos"), Is.EqualTo(expectedTarget));
        if (removeFirst)
        {
            staff.SetStaffData(null, floor);
            Assert.That(staff.WorkLifetime, Is.GreaterThan(lifetime));
            Assert.That(Get<Action>(staff, "_moveCompleted"), Is.Null);
            Assert.That(Get<IStaffAction>(staff, "_staffAction"), Is.Null);
        }
        staff.SetStaffData(data, floor);
        if (!removeFirst)
            Assert.That(staff.transform.position, Is.EqualTo(position), "An unchanged assignment must not teleport the employee.");
        // Accept either preserving the current trip or cancelling and acquiring
        // fresh work. A permanently busy action with no trip is not executable.
        for (int attempt = 0; attempt < 3 && Get<Action>(staff, "_moveCompleted") == null; attempt++)
            DecideAfterInterval(staff);
        Assert.That(Get<Action>(staff, "_moveCompleted"), Is.Not.Null,
            "Same-data, same-floor assignment cancelled travel but left the employee busy, so subsequent decisions cannot resume work.");
        Assert.That(Get<Vector2>(staff, "_targetPos"), Is.EqualTo(expectedTarget));
        if (removeFirst)
        {
            var freshArrival = Get<Action>(staff, "_moveCompleted");
            Assert.That(Get<IStaffAction>(staff, "_staffAction"), Is.Not.SameAs(oldAction));
            oldArrival();
            Assert.That(Get<Action>(staff, "_moveCompleted"), Is.SameAs(freshArrival), "A removed assignment must not replace the new trip.");
            Assert.That(Get<object>(oldAction, "_tweenData"), Is.Null, "A removed assignment's late arrival must not schedule work.");
        }
    }

    [Test]
    public void SameChefAssignmentDuringTravel_CanResumeAndApplyCookingWork() => RunChefAssignment(false);

    [Test]
    public void RemovedChefReequippedDuringTravel_IgnoresOldArrivalAndCanWork() => RunChefAssignment(true);

    private static void RunChefAssignment(bool removeFirst)
    {
        using (var s = new Flow())
        {
            Set(s.Kitchen, "_defaultChef1Pos", s.Create<Transform>());
            var door1 = s.Create<Transform>(); door1.position = new Vector3(1f, 0f, 0f);
            var door2 = s.Create<Transform>(); door2.position = new Vector3(1f, 7.64f, 0f);
            Set(s.Kitchen, "_door1", door1); Set(s.Kitchen, "_door2", door2);
            Set(s.Kitchen, "_sinkKitchenUtensil", s.Create<SinkKitchenUtensil>());
            var burner = Get<KitchenBurnerData[]>(s.Kitchen, "_burnerDatas")[0];
            var utensil = s.Create<BurnerKitchenUtensil>(); utensil.transform.position = new Vector3(4f, 0f, 0f);
            burner.SetKitchenUtensil(utensil);
            s.GuideAndSeat(); s.Manager.OnCustomerOrder(s.Table); Invoke(s.Kitchen, "DequeueFood", 0);
            Assert.That(burner.CookingData.IsDefault(), Is.False);
            var staff = CreateStaff<StaffChef>(s, EquipStaffType.Chef, "STAFF16", "Chef");
            try
            {
                DecideAfterInterval(staff);
                Assert.That(burner.UseStaff, Is.SameAs(staff));
                ReassignAndRequireWorkArrival(staff, utensil.transform.position, removeFirst);
                s.Arrive(staff); s.TakeWait(1f)();
                Assert.That(burner.UseStaff, Is.SameAs(staff));
                Assert.That(utensil.IsStaffWorking, Is.True, "The resumed trip must reach the actual cooking work callback.");
                Assert.That(burner.AddCookSpeedMul, Is.EqualTo(staff.GetActionValue()));
                Assert.That(UserInfo.TotalCookCount, Is.Zero, "Chef reassignment must not complete or duplicate the food.");
            }
            finally { staff.SetStaffData(null, s.Table.FloorType); }
        }
    }

    [Test]
    public void SameCleanerAssignmentDuringTravel_CanResumeAndCleanTheTable() => RunCleanerAssignment(false);

    [Test]
    public void RemovedCleanerReequippedDuringTravel_IgnoresOldArrivalAndCanClean() => RunCleanerAssignment(true);

    private static void RunCleanerAssignment(bool removeFirst)
    {
        using (var s = new Flow())
        {
            var hall = Hall(s);
            Set(hall, "_cleanerWaitTr", s.Create<Transform>());
            Set(hall, "_tableDataList", new List<TableData> { s.Table });
            s.Table.transform.position = new Vector3(4f, 0f, 0f);
            s.Table.TableState = ETableState.NeedCleaning;
            s.Stage.GetTableData(s.Table.FloorType, s.Table.TableType).SetNeedCleaning(true);
            Assert.That(s.Table.TableFurniture.CanClean, Is.True);
            var staff = CreateStaff<StaffCleaner>(s, EquipStaffType.Cleaner, "STAFF21", "Cleaner");
            try
            {
                DecideAfterInterval(staff);
                ReassignAndRequireWorkArrival(staff, s.Table.transform.position, removeFirst);
                s.Arrive(staff); s.TakeWait(1f)();
                Assert.That(s.Table.TableState, Is.EqualTo(ETableState.Empty), "The resumed trip must finish the production cleaning callback.");
                Assert.That(s.Stage.GetTableData(s.Table.FloorType, s.Table.TableType).NeedCleaning, Is.False);
                Assert.That(s.Stage.GetSinkBowlCount(s.Table.FloorType), Is.EqualTo(1));
                s.TakeWait(1f)();
                Assert.That(Get<bool>(Get<IStaffAction>(staff, "_staffAction"), "_isUsed"), Is.False,
                    "The employee must become available for the next task after cleaning.");
            }
            finally { staff.SetStaffData(null, s.Table.FloorType); }
        }
    }
}
#endif
