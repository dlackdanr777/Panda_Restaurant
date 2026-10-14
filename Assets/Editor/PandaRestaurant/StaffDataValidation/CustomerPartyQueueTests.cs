#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using LitJson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;

public sealed class CustomerPartyQueueTests
{
    private static CustomerVisitParty Add(Flow s, int members)
    {
        Assert.That(s.Controller.TrySpawnWaitingParty(Enumerable.Repeat(s.Data.Id, members).ToArray(), out var party), Is.True, s.Controller.LastVisitDiagnostic);
        return party;
    }
    private static void Arrive(CustomerVisitParty party)
    {
        foreach (var member in party.Members) Get<Action>(member.Customer, "_moveCompleted")();
    }

    [TestCase(18, 2, 20, 22)]
    [TestCase(19, 1, 20, 21)]
    [TestCase(0, 30, 30, 60)]
    public void Reservations_CountTeamsIncludingEntering_AndSeparatePeople(int singles, int pairs, int limit, int people)
    {
        using (var s = new Flow())
        {
            Set(GameManager.Instance, "_maxWaitCustomerCount", limit);
            for (int i = 0; i < singles; i++) Add(s, 1);
            for (int i = 0; i < pairs; i++) Add(s, 2);
            Assert.That(s.Controller.Count, Is.EqualTo(limit)); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(people));
            Assert.That(s.Controller.WaitCount, Is.Zero);
            Assert.That(CustomerController.IsMaxCount, Is.True);
            Assert.That(s.Controller.TrySpawnWaitingParty(new[] { s.Data.Id }, out _), Is.False);
            Assert.That(s.Controller.Count, Is.EqualTo(limit));
        }
    }

    [Test]
    public void ReversedArrival_DoesNotOvertakeOrDuplicate_AndReadyZeroCancelReleasesWholeTeam()
    {
        using (var s = new Flow())
        {
            var first = Add(s, 2); var second = Add(s, 1);
            var a = Get<Action>(first.Members[0].Customer, "_moveCompleted");
            var b = Get<Action>(first.Members[1].Customer, "_moveCompleted");
            b(); b(); Arrive(second);
            Assert.That(s.Controller.Count, Is.EqualTo(2)); Assert.That(s.Controller.WaitCount, Is.EqualTo(1));
            Assert.That(s.Controller.IsEmpty(), Is.True); Assert.That(s.Manager.OnCustomerGuideEvent(), Is.False);
            a(); Assert.That(s.Controller.FirstReadyParty, Is.SameAs(first));
            Assert.That(s.Controller.CancelWaitingParty(first), Is.True);
            Assert.That(first.Members.All(m => !m.Customer.gameObject.activeSelf), Is.True);
            Assert.That(s.Controller.Count, Is.EqualTo(1));
            a(); b(); Assert.That(s.Controller.Count, Is.EqualTo(1));
            var entering = Add(s, 2);
            Assert.That(s.Controller.CancelWaitingParty(entering), Is.True);
            Assert.That(s.Controller.CancelWaitingParty(entering), Is.False);
            Assert.That(s.Controller.WaitingParties.Single(), Is.SameAs(second));
        }
    }

    [Test]
    public void SingleEnteringTimeout_RemovesReservationEvenWithNoReadyCustomers()
    {
        using (var s = new Flow())
        {
            var party = Add(s, 1); var customer = party.Members[0].Customer;
            Assert.That(s.Controller.WaitCount, Is.Zero);
            var late = Get<Action>(customer, "_moveCompleted");
            Invoke(customer, "LeaveRestaurant");
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(party.IsActive, Is.False);
            late(); Assert.That(s.Controller.WaitCount, Is.Zero);
            Get<Action>(customer, "_moveCompleted")(); Assert.That(customer.gameObject.activeSelf, Is.False);
            Assert.That(UserInfo.GetWaitingPartySave(EStage.Stage1).Teams, Is.Empty);
        }
    }

    [Test]
    public void DisableOneWaitingMember_CleansWholeTeamOnce_AndStaleCancelCannotRemoveReusedVisit()
    {
        using (var s = new Flow())
        {
            var old = Add(s, 2); var customers = old.Members.Select(m => m.Customer).ToArray();
            var late = Get<Action>(customers[1], "_moveCompleted");
            ObjectPoolManager.Instance.DespawnNormalCustomer(customers[0]);
            Invoke(customers[0], "OnDisable"); // EditMode lifecycle is explicitly dispatched.
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(customers.All(c => !c.gameObject.activeSelf), Is.True);
            var fresh = Add(s, 2);
            Assert.That(s.Controller.CancelWaitingParty(old), Is.False); late();
            Assert.That(s.Controller.WaitingParties.Single(), Is.SameAs(fresh));
        }
    }

    [Test]
    public void SecondPoolAllocationFailure_RollsBackSlotAndFirstCustomerWithoutSavingPartialTeam()
    {
        using (var s = new Flow())
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            var original = UserInfo.GetWaitingPartySave(EStage.Stage1).Serialize();
            s.Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", null);
            Assert.That(s.Controller.TrySpawnWaitingParty(new[] { s.Data.Id, s.Data.Id }, out _), Is.False);
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(s.Controller.WaitingPersonCount, Is.Zero);
            Assert.That(s.Customer.gameObject.activeSelf, Is.False); Assert.That(s.Customer.VisitParty, Is.Null);
            Assert.That(Get<Queue<NormalCustomer>>(ObjectPoolManager.Instance, "_normalCustomerPool").Count, Is.EqualTo(1));
            Assert.That(UserInfo.GetWaitingPartySave(EStage.Stage1).Serialize(), Is.EqualTo(original));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PairGuide_ReservesDistinctSeats_WaitsForBoth_AndStopsBeforeEverySingleMealEntry(bool reverse)
    {
        using (var s = new Flow())
        {
            var party = Add(s, 2); Arrive(party);
            Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(s.Table.OccupantCount, Is.EqualTo(2));
            Assert.That(s.Table.CurrentCustomer, Is.Null);
            Assert.That(party.Members.Select(m => m.SeatIndex), Is.EqualTo(new[] { 0, 1 }));
            var first = party.Members[reverse ? 1 : 0]; var last = party.Members[reverse ? 0 : 1];
            var old = Get<Action>(first.Customer, "_moveCompleted"); first.Customer.transform.position = first.Customer.TargetPos; old(); s.TakeWait(1.5f)();
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.Move));
            Assert.That(s.Table.CurrentFood.IsDefault(), Is.True);
            last.Customer.transform.position = last.Customer.TargetPos; Get<Action>(last.Customer, "_moveCompleted")(); s.TakeWait(1.5f)();
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.PartySeatedAwaitingOrders));
            foreach (var member in party.Members)
            {
                Assert.That(Get<CustomerState>(member.Customer, "_currentState"), Is.EqualTo(CustomerState.Sit));
                Assert.That(member.Customer.transform.position, Is.EqualTo(s.Table.GetSeatPosition(member.SeatIndex)));
            }
            s.Manager.OnCustomerSeating(s.Table); s.Manager.OnServing(s.Table);
            Assert.That(s.Table.CurrentFood.IsDefault(), Is.True); Assert.That(s.Queue, Is.Empty);
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.Zero); Assert.That(s.Stage.Tip, Is.Zero);
            Assert.That(s.Stage.Satisfaction, Is.Zero); Assert.That(s.Stage.FeverGauge, Is.Zero);
            Assert.That(s.Manager.OnCustomerGuideEvent(), Is.False);
            Assert.That(s.Manager.CancelPartyGuide(s.Table), Is.True);
            var fresh = Add(s, 2); Arrive(fresh); Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
            old(); Assert.That(s.Table.CurrentParty, Is.SameAs(fresh)); Assert.That(s.Table.TableState, Is.EqualTo(ETableState.Move));
        }
    }

    [Test]
    public void LayoutOffsets_SurviveFurnitureRecalculationAndDoNotChangeTeamIdentity()
    {
        using (var s = new Flow())
        {
            var party = Add(s, 2);
            Set(s.Controller, "_teamSpacing", 3f); Set(s.Controller, "_lineDirection", Vector2.left); Set(s.Controller, "_memberLineOffset", new Vector2(.8f, .1f));
            Assert.That(s.Controller.GetWaitingPosition(2, 1), Is.EqualTo(new Vector3(-5.2f, .1f, 0)));
            Set(s.Table, "_leftSeatOffset", new Vector2(.2f, .3f));
            s.Table.SetLeftChairTrPos(new Vector3(1, 2, 0));
            Assert.That(s.Table.GetSeatPosition(0), Is.EqualTo(new Vector3(1.2f, 2.3f, 0)));
            s.Table.SetLeftChairTrPos(new Vector3(5, 6, 0));
            Assert.That(s.Table.GetSeatPosition(0), Is.EqualTo(new Vector3(5.2f, 6.3f, 0)));
            Invoke(s.Controller, "SortCustomerLine"); Assert.That(s.Controller.WaitingParties.Single(), Is.SameAs(party));
        }
    }

    [TestCase("[]", 0)]
    [TestCase("[\"A\"]", 1)]
    [TestCase("[\"A\",\"A\",\"B\"]", 3)]
    public void LegacyParser_PreservesEachOccurrenceAsSingleTeam(string ids, int count)
    {
        var saved = WaitingPartySaveData.ParsePlain(JObject.Parse("{\"WaitingCustomerIdList\":" + ids + "}"));
        Assert.That(saved.IsProtected, Is.False); Assert.That(saved.IsLegacy, Is.True);
        Assert.That(saved.Teams.Count, Is.EqualTo(count)); Assert.That(saved.Teams.All(t => t.Count == 1), Is.True);
        Assert.That(saved.Teams.SelectMany(t => t), Is.EqualTo(JArray.Parse(ids).Select(id => (string)id)));
    }

    [TestCase("{\"version\":1,\"teams\":[]}", false)]
    [TestCase("{\"version\":1,\"teams\":[[\"A\"],[\"B\",\"A\"]]}", false)]
    [TestCase("{\"version\":2,\"teams\":[]}", true)]
    [TestCase("{\"version\":\"1\",\"teams\":[]}", true)]
    [TestCase("{\"version\":1,\"teams\":[[]]}", true)]
    [TestCase("{\"version\":1,\"teams\":[[\"A\",\"B\",\"C\"]]}", true)]
    [TestCase("{broken", true)]
    public void NewParser_IsAuthoritativeIncludingEmpty_AndProtectsBadOriginal(string raw, bool rejected)
    {
        var saved = WaitingPartySaveData.ParsePlain(new JObject { [WaitingPartySaveData.FieldName] = raw, ["WaitingCustomerIdList"] = new JArray("OLD") });
        Assert.That(saved.IsProtected, Is.EqualTo(rejected)); Assert.That(saved.IsLegacy, Is.False);
        Assert.That(saved.Original, Is.EqualTo(raw));
        if (rejected) Assert.Throws<InvalidOperationException>(() => new ServerStageData { WaitingParties = saved }.GetParam());
        else Assert.That(saved.Teams.SelectMany(t => t), Does.Not.Contain("OLD"));
    }

    [Test]
    public void ActualParamSdkParserAndRestore_RoundTripMixedTeamsWithoutRandomOrFlatSplit()
    {
        using (var s = new Flow())
        {
            UserInfo.SetWaitingParties(EStage.Stage1, new[] { new[] { s.Data.Id }, new[] { s.Data.Id, s.Data.Id } });
            var param = s.Stage.SaveData().GetParam(); var plain = JObject.Parse(param.GetJson());
            Assert.That(((JArray)plain["WaitingCustomerIdList"]).Count, Is.Zero);
            var response = Response(plain); var loaded = new StageInfo(); int saves = 0;
            Assert.That(UserInfo.TryApplyStageDataResponse(EStage.Stage1, response, () => true, loaded.LoadData, () => saves++), Is.True);
            Assert.That(loaded.WaitingParties.Teams.Select(t => t.Count), Is.EqualTo(new[] { 1, 2 }));
            Set(s.Stage, "_waitingParties", loaded.WaitingParties);
            var random = UnityEngine.Random.state;
            Invoke(s.Controller, "LoadWaitingCustomers");
            Assert.That(s.Controller.Count, Is.EqualTo(2)); Assert.That(s.Controller.WaitingPersonCount, Is.EqualTo(3));
            Assert.That(s.Controller.WaitingParties.Select(t => t.Members.Count), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random)); Assert.That(UserInfo.PromotionCount, Is.Zero);
            Assert.That(s.Controller.WaitingParties.Select(t => t).Distinct().Count(), Is.EqualTo(2));
        }
    }

    [TestCase("unknown")]
    [TestCase("limit")]
    public void RestoreRejectedBeforeAnySpawn_PreservesOriginalAndBlocksStageSave(string reason)
    {
        using (var s = new Flow())
        {
            var teams = reason == "unknown" ? new[] { new[] { s.Data.Id }, new[] { "UNKNOWN" } }
                : Enumerable.Range(0, 11).Select(i => new[] { s.Data.Id }).ToArray();
            UserInfo.SetWaitingParties(EStage.Stage1, teams);
            var original = UserInfo.GetWaitingPartySave(EStage.Stage1);
            Invoke(s.Controller, "LoadWaitingCustomers");
            Assert.That(s.Controller.Count, Is.Zero);
            Assert.That(UserInfo.GetWaitingPartySave(EStage.Stage1).IsProtected, Is.True);
            Assert.That(UserInfo.GetWaitingPartySave(EStage.Stage1).Teams.SelectMany(t => t), Is.EqualTo(original.Teams.SelectMany(t => t)));
            Assert.That(s.Controller.TrySpawnWaitingParty(new[] { s.Data.Id }, out _), Is.False);
            UserInfo.SaveStageData(EStage.Stage1); UserInfo.SaveStageDataAsync(EStage.Stage1);
            Assert.That(typeof(Muks.BackEnd.BackendManager).GetProperty("ExistingInstance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.Null);
        }
    }

    [Test]
    public void CorruptWaitingField_ProtectsOriginalWhileLoadingOtherStageFields()
    {
        using (var s = new Flow())
        {
            var response = Response(new JObject { [WaitingPartySaveData.FieldName] = new JObject { ["bad"] = true }, ["WaitingCustomerIdList"] = new JArray(s.Data.Id) });
            int applied = 0, saved = 0;
            Assert.That(UserInfo.TryApplyStageDataResponse(EStage.Stage1, response, () => true,
                data => { applied++; Set(s.Stage, "_waitingParties", data.WaitingParties); return true; }, () => saved++, rejected => Set(s.Stage, "_waitingParties", rejected)), Is.True);
            Assert.That(applied, Is.EqualTo(1)); Assert.That(saved, Is.Zero);
            Assert.That(s.Stage.WaitingParties.IsProtected, Is.True);
            Assert.That(s.Stage.WaitingParties.Original, Is.EqualTo(response.GetReturnValue()));
            UserInfo.SetWaitingCustomerIds(EStage.Stage1, new List<string>());
            Assert.That(s.Stage.WaitingParties.IsProtected, Is.True);
        }
    }

    [Test]
    public void ValidNewEmpty_IgnoresDamagedLegacy_ThroughActualSdkParser()
    {
        var response = Response(new JObject { [WaitingPartySaveData.FieldName] = "{\"version\":1,\"teams\":[]}", ["WaitingCustomerIdList"] = JValue.CreateNull() });
        ServerStageData result = null;
        Assert.That(UserInfo.TryApplyStageDataResponse(EStage.Stage1, response, () => true, data => { result = data; return true; }, null), Is.True);
        Assert.That(result.WaitingParties.Teams, Is.Empty);
        Assert.That(result.WaitingParties.CanSave, Is.True);
    }

    [Test]
    public void StaffMigrationCanSaveOtherFieldsWhileWaitingIdsRemainUnvalidated()
    {
        var input = new ServerStageData { WaitingParties = WaitingPartySaveData.FromTeams(new[] { new[] { "UNKNOWN" } }) };
        input.GiveStaffList.Add(new SaveStaffData("STAFF02", 6));
        int saved = 0; ServerStageData loaded = null;
        Assert.That(UserInfo.TryApplyStageDataResponse(EStage.Stage1, Response(JObject.Parse(input.GetParam().GetJson())), () => true,
            data => { loaded = data; return true; }, () => saved++), Is.True);
        Assert.That(loaded.GiveStaffList.Single().Level, Is.EqualTo(5));
        Assert.That(loaded.WaitingParties.NeedsValidation, Is.True); Assert.That(saved, Is.EqualTo(1));
        Assert.That(JObject.Parse(loaded.GetParam(true).GetJson()).ContainsKey(WaitingPartySaveData.FieldName), Is.False);
        Assert.Throws<InvalidOperationException>(() => loaded.GetParam());
    }

    [Test]
    public void CurrentWaiterRequest_ReleasesItselfWhenItsUseEnds_WithoutChangingTheTable()
    {
        using (var s = new Flow())
        {
            s.GuideAndSeat();
            var staff = s.Track(UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/Staff/Waiter.prefab"))).GetComponent<StaffWaiter>();
            Set(staff, "_idleSprites", new[] { staff.GetComponentInChildren<SpriteRenderer>(true).sprite });
            var action = (WaiterAction)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WaiterAction));
            var request = new object();
            Set(action, "_waiter", staff); Set(action, "_tableManager", s.Manager); Set(action, "_currentData", s.Table);
            Set(action, "_use", s.Use); Set(action, "_order", Get<object>(s.Table.CurrentFood, "_identity"));
            Set(action, "_request", request); Set(action, "_isUsed", true);
            Invoke(s.Table, "OnDisable"); s.Table.CurrentCustomer = null;
            int orders = s.Table.OrdersCount;
            Assert.That(Invoke(action, "ContinueRequest", request), Is.False);
            Assert.That(Get<bool>(action, "_isUsed"), Is.False);
            Assert.That(s.Table.OrdersCount, Is.EqualTo(orders));
            Set(action, "_request", new object()); Set(action, "_isUsed", true);
            Assert.That(Invoke(action, "ContinueRequest", request), Is.False);
            Assert.That(Get<bool>(action, "_isUsed"), Is.True, "An old request cannot reset a replacement");
        }
    }

    [Test]
    public void OldWaiterMovementAndDestructor_CannotTouchLaterPairOrSingleUse()
    {
        using (var s = new Flow())
        {
            s.GuideAndSeat();
            var worker = s.CreateWaiter(); var staff = worker.staff; var action = worker.action;
            Invoke(action, "OrderAction", s.Table);
            var late = Get<Action>(staff, "_moveCompleted"); s.Arrive(staff);
            var lateWait = s.TakeWait(.5f);
            s.NextCustomer(true); s.GuideAndSeat();
            int orders = s.Table.OrdersCount; var food = s.Table.CurrentFood;
            action.Destructor(); late(); lateWait();
            Assert.That(s.Table.OrdersCount, Is.EqualTo(orders)); Assert.That(s.Table.CurrentFood, Is.EqualTo(food));
            Invoke(s.Table, "OnDisable"); s.Table.CurrentCustomer = null; s.Table.TableState = ETableState.Empty;
            var pair = Add(s, 2); Arrive(pair); Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
            orders = s.Table.OrdersCount; action.Destructor(); late(); lateWait();
            Assert.That(s.Table.OrdersCount, Is.EqualTo(orders)); Assert.That(s.Table.CurrentParty, Is.SameAs(pair));
        }
    }

    [Test]
    public void PendingSemanticValidation_BlocksSettersSpawnAndSave_UntilLateLoadCompletes()
    {
        using (var s = new Flow())
        {
            var saved = WaitingPartySaveData.FromTeams(new[] { new[] { s.Data.Id, s.Data.Id } }).RequireValidation();
            Set(s.Stage, "_waitingParties", saved);
            Assert.That(s.Stage.SetWaitingParties(Array.Empty<string[]>()), Is.False);
            s.Stage.SetWaitingCustomerIds(new List<string>());
            Assert.That(s.Stage.WaitingParties, Is.SameAs(saved));
            Assert.That(s.Controller.TrySpawnWaitingParty(new[] { s.Data.Id }, out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => s.Stage.SaveData().GetParam());
            UserInfo.SaveStageData(EStage.Stage1); UserInfo.SaveStageDataAsync(EStage.Stage1);
            Invoke(s.Controller, "OnWaitingPartiesLoaded", EStage.Stage1);
            Assert.That(s.Controller.Count, Is.EqualTo(1)); Assert.That(saved.CanSave, Is.True);
            Assert.DoesNotThrow(() => s.Stage.SaveData().GetParam());
        }
    }

    [Test]
    public void LateSnapshotWithExistingQueue_IsProtectedWithoutReplacingOrSavingEitherQueue()
    {
        using (var s = new Flow())
        {
            var old = Add(s, 1);
            var received = WaitingPartySaveData.ParseNew("{\"version\":1,\"teams\":[]}");
            Set(s.Stage, "_waitingParties", received);
            Invoke(s.Controller, "OnWaitingPartiesLoaded", EStage.Stage1);
            Assert.That(s.Controller.WaitingParties.Single(), Is.SameAs(old));
            Assert.That(s.Stage.WaitingParties.IsProtected, Is.True);
            Assert.That(s.Stage.WaitingParties.Original, Is.EqualTo(received.Original));
            Arrive(old); Assert.That(s.Stage.WaitingParties.IsProtected, Is.True);
        }
    }

    [Test]
    public void LaterTeamRestoreFailure_ReleasesEarlierTeam_AndKeepsOriginalProtected()
    {
        using (var s = new Flow())
        {
            ObjectPoolManager.Instance.DespawnNormalCustomer(s.Customer);
            s.Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", null);
            var saved = WaitingPartySaveData.FromTeams(new[] { new[] { s.Data.Id }, new[] { s.Data.Id, s.Data.Id } }).RequireValidation();
            Set(s.Stage, "_waitingParties", saved);
            Invoke(s.Controller, "LoadWaitingCustomers");
            Assert.That(s.Controller.Count, Is.Zero); Assert.That(s.Controller.WaitingPersonCount, Is.Zero);
            Assert.That(s.Stage.WaitingParties.IsProtected, Is.True);
            Assert.That(s.Stage.WaitingParties.Teams.Select(t => t.Count), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(Get<Queue<NormalCustomer>>(ObjectPoolManager.Instance, "_normalCustomerPool").Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void WrongPathEndpoint_CannotClaimASeat_AndExplicitCancelReleasesBoth()
    {
        using (var s = new Flow())
        {
            var party = Add(s, 2); Arrive(party);
            Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
            Get<Action>(party.Members[0].Customer, "_moveCompleted")();
            Assert.That(party.Members[0].Customer.HasPathFailure, Is.True);
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.Move));
            Assert.That(s.Manager.CancelPartyGuide(s.Table), Is.True);
            Assert.That(party.Members.All(m => !m.Customer.gameObject.activeSelf), Is.True);
            Assert.That(s.Table.OccupantCount, Is.Zero);
        }
    }

    [Test]
    public void GuideNotificationMayCancelAndReuse_WithoutContinuingOldMovement()
    {
        using (var s = new Flow())
        {
            var party = Add(s, 2); Arrive(party);
            Action handler = () => s.Manager.CancelPartyGuide(s.Table);
            s.Controller.OnGuideCustomerHandler += handler;
            Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.False);
            s.Controller.OnGuideCustomerHandler -= handler;
            Assert.That(s.Table.OccupantCount, Is.Zero);
            Assert.That(party.Members.All(m => !m.Customer.gameObject.activeSelf), Is.True);
            var next = Add(s, 2); Arrive(next);
            Assert.That(s.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
        }
    }

    [TestCase(ERestaurantFloorType.Floor1)]
    [TestCase(ERestaurantFloorType.Floor2)]
    public void RealManagerEntry_GuidesWholePairOnlyOnAssignedFloor_AndRejectsDuplicate(ERestaurantFloorType floor)
    {
        using (var s = new Flow())
        {
            var system = Get<FurnitureSystem>(s.Manager, "_furnitureSystem");
            var groups = Get<Dictionary<ERestaurantFloorType, FurnitureGroup>>(system, "_furnitureGroupDic");
            var group = groups.Values.First(); groups[floor] = group;
            Set(group, "_floorType", floor); s.Table.FloorType = floor;
            var data = s.Track(ScriptableObject.CreateInstance<ManagerData>()); Set(data, "_id", "PAIR-MANAGER");
            var staff = s.Create<Staff>(); Set(staff, "_staffData", data); Set(staff, "_staffType", EquipStaffType.Manager); Set(staff, "_equipFloorType", floor);
            staff.gameObject.SetActive(true);
            Get<Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>>(s.Stage, "_equipStaffTypeDic")[floor][EquipStaffType.Manager] = data;
            Invoke(s.Manager, "RegisterManagerRuntime", floor, staff);
            var party = Add(s, 2); Arrive(party);
            Assert.That(s.Manager.OnManagerCustomerGuideEvent(staff, ManagerCustomerGuideSource.Action), Is.True);
            Assert.That(s.Table.CurrentParty, Is.SameAs(party)); Assert.That(party.CurrentTable.FloorType, Is.EqualTo(floor));
            Assert.That(s.Manager.OnManagerCustomerGuideEvent(staff, ManagerCustomerGuideSource.Action), Is.False);
            foreach (var member in party.Members)
            { member.Customer.transform.position = member.Customer.TargetPos; Get<Action>(member.Customer, "_moveCompleted")(); s.TakeWait(1.5f)(); }
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.PartySeatedAwaitingOrders));
        }
    }

    private static BackendReturnObject Response(JObject plain)
    {
        JToken Attribute(JToken token)
        {
            if (token is JObject obj) return new JObject { ["M"] = new JObject(obj.Properties().Select(p => new JProperty(p.Name, Attribute(p.Value)))) };
            if (token is JArray array) return new JObject { ["L"] = new JArray(array.Select(Attribute)) };
            if (token.Type == JTokenType.String) return new JObject { ["S"] = token };
            if (token.Type == JTokenType.Boolean) return new JObject { ["BOOL"] = token };
            if (token.Type == JTokenType.Null) return new JObject { ["NULL"] = true };
            return new JObject { ["N"] = token.ToString(Formatting.None) };
        }
        var row = new JObject(plain.Properties().Select(p => new JProperty(p.Name, Attribute(p.Value))));
        var bro = new BackendReturnObject();
        foreach (var pair in new Dictionary<string, string> { ["StatusCode"] = "200", ["ReturnValue"] = new JObject { ["rows"] = new JArray(row) }.ToString(Formatting.None), ["ErrorCode"] = "", ["Message"] = "" })
            {
                var property = typeof(BackendReturnObject).GetProperty(pair.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                property.SetValue(bro, Convert.ChangeType(pair.Value, property.PropertyType));
            }
        return bro;
    }
}
#endif
