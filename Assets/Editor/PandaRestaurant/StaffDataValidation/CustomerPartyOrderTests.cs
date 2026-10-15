#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static CustomerTableUseRegressionTests;
using Flow = CustomerTableUseRegressionTests.Flow;
using Object = UnityEngine.Object;

// Production commands and captured callbacks, advanced explicitly in the owned PreviewScene.
// These cases prove ownership/transactions, not real-time movement or mouse input.
public sealed class CustomerPartyOrderTests
{
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
    internal sealed class Pair : IDisposable
    {
        internal readonly Flow S = new Flow(extraOrder: true);
        internal FoodData SecondFood;
        internal NormalCustomerData SecondData;
        internal CustomerVisitParty Party;
        private readonly Dictionary<int, Action> _exitCallbacks = new Dictionary<int, Action>();
        internal PartyOrderBatch Batch => S.Table.PartyOrders;
        internal KitchenBurnerData Burner => Get<KitchenBurnerData[]>(S.Kitchen, "_burnerDatas")[0];
        internal Pair(bool firstOwned = true, bool secondOwned = true, bool same = false, bool visited = false, bool invalidMenu = false, int ordersA = 2, int ordersB = 1, Action<Pair> beforeGuide = null, bool requireBatch = true, Action<Pair> beforeSpawn = null, bool natural = false)
        {
            SecondFood = same ? S.Food : S.Track(Object.Instantiate(S.Food));
            if (!same)
            {
                Set(SecondFood, "_id", "PARTY-FOOD-B"); Set(SecondFood, "_cookingTime", 7f);
                Set(SecondFood, "_sellPrice", 101); Set(SecondFood, "_foodType", FoodType.Cozy); Set(SecondFood, "_sprite", S.NewSprite());
            }
            Get<Dictionary<string, FoodData>>(FoodDataManager.Instance, "_foodDataDic")[SecondFood.Id] = SecondFood;
            SecondData = S.Track(Object.Instantiate(S.Data)); Set(SecondData, "_id", "PARTY-CUSTOMER-B");
            Set(SecondData, "_requiredDish", visited || invalidMenu ? "" : SecondFood.Id);
            Set(SecondData, "_visitCountFoodDic", visited ? new Dictionary<int, string> { [25] = SecondFood.Id } : new Dictionary<int, string>());
            Get<Dictionary<string, CustomerData>>(CustomerDataManager.Instance, "_customerDataDic")[SecondData.Id] = SecondData;
            ((Dictionary<string, SaveCustomerData>)Field(typeof(UserInfo), "_enabledCustomerDic").GetValue(null))[SecondData.Id] = new SaveCustomerData(SecondData.Id, "", visited ? 25 : 0);
            var owned = ((Dictionary<string, int>)Field(typeof(UserInfo), "_giveRecipeLevelDic").GetValue(null)); owned.Clear();
            if (firstOwned) owned[S.Food.Id] = 1; if (secondOwned) owned[SecondFood.Id] = 1;
            var skins = Get<Dictionary<string, CustomerSkinData>>(SkinDataManager.Instance, "_customerSkinDataDic");
            var visits = (Dictionary<string, SaveCustomerData>)Field(typeof(UserInfo), "_enabledCustomerDic").GetValue(null);
            foreach (var entry in new[] { (S.Data.Id, ordersA), (SecondData.Id, ordersB) })
            {
                var skin = S.Track(ScriptableObject.CreateInstance<CustomerSkinData>());
                Set(skin, "_id", entry.Item1 + "-ORDERS"); Set(skin, "_sprite", S.NewSprite());
                Set(skin, "_upgradeType", SkinCustomerUpgradeType.Type3); Set(skin, "_upgradeValue", (float)(entry.Item2 - 1));
                skins[skin.Id] = skin; visits[entry.Item1] = new SaveCustomerData(entry.Item1, skin.Id, visited ? 25 : 0);
            }
            S.Table.OrderButton = Button(); S.Table.CompanionOrderButton = Button();
            beforeSpawn?.Invoke(this);
            if (natural)
            {
                CustomerNaturalPartyTests.Setup(S);
                Get<List<NormalCustomerData>>(CustomerDataManager.Instance, "_normalCustomerDataList").Add(SecondData);
                CustomerNaturalPartyTests.RepresentativeSeed(0, 2);
                Assert.That(S.Controller.TryAddCustomer(), Is.True); Party = S.Controller.WaitingParties.Last();
                Assert.That(Party.Members.Count, Is.EqualTo(2));
            }
            else Assert.That(S.Controller.TrySpawnWaitingParty(new[] { S.Data.Id, SecondData.Id }, out Party), Is.True);
            foreach (var member in Party.Members) { member.Customer.transform.position=member.Customer.TargetPos; Get<Action>(member.Customer,"_moveCompleted")(); }
            beforeGuide?.Invoke(this);
            Assert.That(S.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
            foreach (var member in Party.Members)
            {
                member.Customer.transform.position = member.Customer.TargetPos;
                Get<Action>(member.Customer, "_moveCompleted")(); S.TakeWait(1.5f)();
            }
            if (requireBatch) Assert.That(Batch, Is.Not.Null);
        }
        private TableButton Button()
        {
            var go = S.Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Table/OrderButton.prefab")));
            var button = go.GetComponent<TableButton>(); button.Init();
            button.AddListener(() => S.Manager.OnCustomerOrder(S.Table));
            return button;
        }
        internal PartyOrderAcceptResult Accept() => S.Manager.TryAcceptPartyOrders(S.Table, Batch);
        internal void Pump() => Invoke(S.Kitchen, "DequeueFood", 0);
        internal void ArriveExit(int index)
        {
            var c = Party.Members[index].Customer;
            if (!_exitCallbacks.TryGetValue(index, out var callback)) _exitCallbacks[index] = callback = Get<Action>(c, "_moveCompleted");
            c.transform.position = c.TargetPos; callback();
        }
        public void Dispose() { S.Dispose(); }
    }

    [TestCase(false)] [TestCase(true)]
    public void OwnMenusVisitsAndAdditionalRemainder_AreStableAcrossRefresh(bool same)
    {
        using (var p = new Pair(same: same, visited: !same))
        {
            var orders = p.Batch.Orders.ToArray(); var random = UnityEngine.Random.state;
            Assert.That(orders.Select(o => o.Food.Id), Is.EqualTo(new[] { p.S.Food.Id, p.SecondFood.Id }));
            Assert.That(orders.Select(o => o.Member.Customer.NormalCustomerData.Id), Is.EqualTo(new[] { p.S.Data.Id, p.SecondData.Id }));
            Assert.That(orders[0].RemainingAdditionalOrders, Is.EqualTo(1));
            Assert.That(orders[1].RemainingAdditionalOrders, Is.Zero);
            Assert.That(Get<object>(orders[0].Cooking, "_identity"), Is.Not.SameAs(Get<object>(orders[1].Cooking, "_identity")));
            p.S.Table.RefreshPartyOrderButtons(); p.S.Manager.OnCustomerSeating(p.S.Table);
            Assert.That(p.Batch.Orders, Is.EqualTo(orders)); Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            Assert.That(p.S.Table.CurrentFood.IsDefault(), Is.True); Assert.That(p.S.Table.TotalPrice, Is.Zero); Assert.That(p.S.Table.TotalTip, Is.Zero);
            foreach (var order in orders)
            {
                Assert.That(order.PlannedPrice, Is.EqualTo((int)(101 * order.Member.Customer.CurrentFoodPriceMul)));
                Assert.That(order.PlannedTip, Is.EqualTo(50));
            }
            Assert.That(Get<Transform>(Get<WorldToSceenPosition>(p.S.Table.OrderButton, "_worldToSceenPos"), "_worldTransform"), Is.SameAs(p.S.Table.ChairTrs[0]));
            Assert.That(Get<Transform>(Get<WorldToSceenPosition>(p.S.Table.CompanionOrderButton, "_worldToSceenPos"), "_worldTransform"), Is.SameAs(p.S.Table.ChairTrs[1]));
        }
    }

    [TestCase(0)] [TestCase(1)]
    public void EitherAuthoredButton_DuplicateSameFrameInput_RegistersWholeBatchOnce(int first)
    {
        using (var p = new Pair())
        {
            var buttons = new[] { p.S.Table.OrderButton, p.S.Table.CompanionOrderButton };
            Get<Button>(buttons[first], "_button").onClick.Invoke();
            Get<Button>(buttons[1-first], "_button").onClick.Invoke();
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.AlreadyHandled));
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Accepted)); Assert.That(p.S.Queue.Count, Is.EqualTo(2));
            Assert.That(p.Batch.Orders.All(o => o.State == PartyFoodState.Queued), Is.True);
            Assert.That(buttons.All(b => !b.gameObject.activeSelf), Is.True);
            Assert.That(p.S.Queue.Select(j => Get<object>(j, "_identity")).Distinct().Count(), Is.EqualTo(2));
        }
    }

    [TestCase(true, true)] [TestCase(true, false)] [TestCase(false, true)] [TestCase(false, false)]
    public void AllRecipeCombinations_AreWholeBatchAndPenaltyOnce(bool a, bool b)
    {
        using (var p = new Pair(a, b))
        {
            Assert.That(p.S.Table.OrderButton.gameObject.activeSelf && p.S.Table.CompanionOrderButton.gameObject.activeSelf, Is.True);
            Assert.That(Get<Button>(p.S.Table.OrderButton, "_button").interactable && Get<Button>(p.S.Table.CompanionOrderButton, "_button").interactable, Is.True);
            Assert.That(p.Accept(), Is.EqualTo(a && b ? PartyOrderAcceptResult.Accepted : PartyOrderAcceptResult.RejectedRecipe));
            var batch = p.Batch;
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.AlreadyHandled));
            Assert.That(p.S.Queue.Count, Is.EqualTo(a && b ? 2 : 0));
            Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(a && b ? 0 : -10));
            Assert.That(UserInfo.TotalCookCount, Is.Zero); Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.Zero);
            Assert.That(p.S.Stage.Tip, Is.Zero); Assert.That(p.S.Coins.CurrentMoney, Is.Zero); Assert.That(p.S.Stage.FeverGauge, Is.Zero);
            if (a && b) return;
            Assert.That(batch.Orders.All(o => o.PlannedPrice == 0 && o.PlannedTip == 0), Is.True);
            p.ArriveExit(1); p.ArriveExit(1);
            Assert.That(p.S.Table.OccupantCount, Is.EqualTo(2)); Assert.That(p.Party.Members.All(m => m.Customer.gameObject.activeSelf), Is.True);
            Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.PartyRejecting));
            p.ArriveExit(0);
            Assert.That(p.S.Table.OccupantCount, Is.Zero); Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.Empty));
            Assert.That(p.Party.Members.All(m => !m.Customer.gameObject.activeSelf), Is.True);
            Assert.That(p.S.Manager.TryAcceptPartyOrders(p.S.Table, batch), Is.EqualTo(PartyOrderAcceptResult.Stale));
            Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(-10));
        }
    }

    [Test]
    public void RejectionClamp_AndOldExitCallbacks_CannotAffectNextUse()
    {
        using (var p = new Pair(true, false))
        {
            UserInfo.AddSatisfaction(EStage.Stage1, -47); p.Accept();
            Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(-50));
            var exits = p.Party.Members.Select(m => Get<Action>(m.Customer, "_moveCompleted")).ToArray();
            p.ArriveExit(0); p.ArriveExit(1);
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[] { p.S.Data.Id }, out var next), Is.True);
            Get<Action>(next.Members[0].Customer, "_moveCompleted")();
            Assert.That(p.S.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1), Is.True);
            foreach (var callback in exits) callback();
            Assert.That(p.S.Table.CurrentParty, Is.SameAs(next)); Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.Move));
            Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(-50));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void PreparationFailure_PreservesOtherQueueAndBurner_ThenCanRetry(bool throws)
    {
        using (var p = new Pair())
        {
            p.S.Kitchen.EqueueFood(default); var before = p.S.Queue.ToArray();
            Set(p.S.Kitchen, "PartyPreparationCheck", new Func<int, bool>(i => { if (i == 1 && throws) throw new InvalidOperationException("Injected second preparation"); return i == 0; }));
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.RegistrationFailed));
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Prepared)); Assert.That(p.S.Queue, Is.EqualTo(before));
            Assert.That(p.Burner.CookingData.IsDefault(), Is.True); Assert.That(p.S.Stage.Satisfaction, Is.Zero); Assert.That(UserInfo.TotalCookCount, Is.Zero);
            Set(p.S.Kitchen, "PartyPreparationCheck", null);
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.Accepted)); Assert.That(p.S.Queue.Count, Is.EqualTo(3));
        }
    }

    [Test]
    public void CommitPrecedesExternalObservers_AndUiFailureCannotRepeatRegistration()
    {
        using (var p = new Pair())
        {
            int observations = 0;
            p.S.Table.OnChangeTableStateHandler += state =>
            {
                if (state != ETableState.PartyWaitingForCooking) return;
                observations++;
                Assert.That(p.S.Queue.Count, Is.EqualTo(2)); Assert.That(p.Batch.Orders.All(o => o.State == PartyFoodState.Queued), Is.True);
                Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.AlreadyHandled));
                throw new InvalidOperationException("Injected presentation failure");
            };
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.AcceptedPresentationFailed));
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Accepted)); Assert.That(observations, Is.EqualTo(1));
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.AlreadyHandled)); Assert.That(p.S.Queue.Count, Is.EqualTo(2));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void DistinctJobsCompleteIndividually_AndStayReadyWithoutRewards(bool same)
    {
        using (var p = new Pair(same: same))
        {
            p.Accept(); p.Pump(); Assert.That(p.S.Queue.Count, Is.EqualTo(1));
            Assert.That(p.Batch.Orders[0].State, Is.EqualTo(PartyFoodState.Cooking));
            p.Pump(); Assert.That(p.Batch.Orders[0].State, Is.EqualTo(PartyFoodState.ReadyForServing));
            Assert.That(p.Batch.Orders[1].State, Is.EqualTo(PartyFoodState.Cooking)); Assert.That(UserInfo.TotalCookCount, Is.EqualTo(1));
            p.Pump(); p.Pump();
            foreach (var order in p.Batch.Orders) { order.Cooking.OnCompleted(); order.Cooking.OnCompleted(); }
            Assert.That(UserInfo.TotalCookCount, Is.EqualTo(2)); Assert.That(p.Batch.Orders.All(o => o.State == PartyFoodState.ReadyForServing), Is.True);
            Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.PartyReadyForServing));
            p.S.Manager.OnServing(p.S.Table); p.S.Manager.OnCustomerSeating(p.S.Table); p.S.Manager.OnUseStaff(p.S.Table); p.S.Manager.OnServigStaff(p.S.Table);
            Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.PartyReadyForServing)); Assert.That(p.S.Table.CurrentFood.IsDefault(), Is.True);
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.Zero); Assert.That(p.S.Stage.Tip, Is.Zero); Assert.That(p.S.Stage.FeverGauge, Is.Zero);
            p.S.Manager.CancelPartyGuide(p.S.Table); Assert.That(UserInfo.TotalCookCount, Is.EqualTo(2));
        }
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void CancelQueuedCookingOrOneCompleted_NeverCountsCancellationOrLateCompletion(int advances)
    {
        using (var p = new Pair())
        {
            p.Accept(); var orders = p.Batch.Orders.ToArray();
            for (int i=0; i<advances; i++) p.Pump();
            int completed = Math.Max(0, advances-1);
            Assert.That(UserInfo.TotalCookCount, Is.EqualTo(completed));
            p.S.Manager.CancelPartyGuide(p.S.Table);
            foreach (var order in orders) order.Cooking.OnCompleted(); p.Pump();
            Assert.That(p.S.Queue, Is.Empty); Assert.That(p.Burner.CookingData.IsDefault(), Is.True);
            Assert.That(UserInfo.TotalCookCount, Is.EqualTo(completed)); Assert.That(p.S.Table.PartyOrders, Is.Null);
            Assert.That(orders.All(o => o.State == PartyFoodState.Cancelled && o.PlannedPrice == 0), Is.True);
        }
    }

    [Test]
    public void MissingOrWrongFloorKitchen_IsRetryableWithoutPenalty()
    {
        using (var p = new Pair())
        {
            Set(p.S.Kitchen, "_floorType", ERestaurantFloorType.Floor2);
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.KitchenUnavailable));
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.Prepared)); Assert.That(p.S.Queue, Is.Empty); Assert.That(p.S.Stage.Satisfaction, Is.Zero);
            Set(p.S.Kitchen, "_floorType", ERestaurantFloorType.Floor1);
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.Accepted));
        }
    }

    internal static TableData AddSingleOrder(Pair p)
    {
        var pairTable = p.S.Table;
        var single = p.S.Track(Object.Instantiate(pairTable));
        single.TableState = ETableState.Empty; single.CurrentCustomer = null; single.TableType = TableType.Table2;
        p.S.Table = single;
        try { p.S.GuideAndSeat(); p.S.Manager.OnCustomerOrder(single); }
        finally { p.S.Table = pairTable; }
        return single;
    }

    [TestCase(false)] [TestCase(true)]
    public void BusyKitchenAcceptsPair_AndPairCancellationPreservesUnrelatedSingle(bool singleFirst)
    {
        using (var p = new Pair())
        {
            TableData single;
            if (singleFirst) { single = AddSingleOrder(p); p.Pump(); p.Accept(); }
            else { p.Accept(); p.Pump(); single = AddSingleOrder(p); }
            Assert.That(p.S.Queue.Count, Is.EqualTo(2));
            int price = single.TotalPrice, tip = single.TotalTip;
            p.S.Manager.CancelPartyGuide(p.S.Table);
            p.Pump(); if (single.TableState != ETableState.CanServing) p.Pump();
            Assert.That(single.TableState, Is.EqualTo(ETableState.CanServing)); Assert.That(UserInfo.TotalCookCount, Is.EqualTo(1));
            Assert.That(single.TotalPrice, Is.EqualTo(price)); Assert.That(single.TotalTip, Is.EqualTo(tip));
            Assert.That(p.S.Queue, Is.Empty);
        }
    }

    [Test]
    public void SecondPreparationFailure_PreservesRealOtherTableOrder()
    {
        using (var p = new Pair())
        {
            var single = AddSingleOrder(p); var existing = p.S.Queue.Peek();
            Set(p.S.Kitchen, "PartyPreparationCheck", new Func<int, bool>(i => i == 0));
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.RegistrationFailed));
            Assert.That(p.S.Queue.Count, Is.EqualTo(1)); Assert.That(p.S.Queue.Peek(), Is.EqualTo(existing));
            p.Pump(); p.Pump(); Assert.That(single.TableState, Is.EqualTo(ETableState.CanServing));
            Assert.That(UserInfo.TotalCookCount, Is.EqualTo(1)); Assert.That(p.S.Stage.Satisfaction, Is.Zero);
        }
    }

    [Test]
    public void CounterObserverException_DoesNotLoseReadyStateOrDoubleCount()
    {
        using (var p = new Pair())
        {
            p.Accept(); p.Pump();
            UserInfo.OnAddCookCountHandler += () => throw new InvalidOperationException("Injected count observer");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[COOK_COMPLETION] Counter observer: Injected count observer");
            p.Pump();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[COOK_COMPLETION] Counter observer: Injected count observer");
            p.Pump(); p.Pump();
            Assert.That(UserInfo.TotalCookCount, Is.EqualTo(2));
            Assert.That(p.Batch.Orders.All(o => o.State == PartyFoodState.ReadyForServing), Is.True);
        }
    }

    [Test]
    public void MissingMenuCandidate_IsExplicitlyHeldWithoutPenaltyOrReroll_AndCanCancel()
    {
        using (var p = new Pair(invalidMenu: true))
        {
            Assert.That(p.Batch.State, Is.EqualTo(PartyOrderBatchState.PreparationFailed));
            Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.PartyOrderPreparationFailed));
            var failed = p.Batch; var random = UnityEngine.Random.state;
            Assert.That(p.Accept(), Is.EqualTo(PartyOrderAcceptResult.InvalidMenu));
            p.S.Table.RefreshPartyOrderButtons();
            Invoke(p.S.Manager, "PreparePartyOrders", p.S.Table, p.S.Table.CurrentUse);
            Assert.That(p.Batch, Is.SameAs(failed)); Assert.That(p.Batch.Orders, Is.Empty);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(random));
            Assert.That(p.S.Queue, Is.Empty); Assert.That(p.S.Stage.Satisfaction, Is.Zero);
            Assert.That(p.S.Manager.CancelPartyGuide(p.S.Table), Is.True); Assert.That(p.S.Table.OccupantCount, Is.Zero);
        }
    }

    [Test]
    public void ReentrantRejectionObserver_CannotWriteOverNewSingleUse()
    {
        using (var p = new Pair(true, false))
        {
            var old = p.Batch;
            p.S.Stage.OnChangeSatisfactionHandler += () => { p.S.Manager.CancelPartyGuide(p.S.Table); p.S.GuideAndSeat(); };
            Assert.That(p.S.Manager.TryAcceptPartyOrders(p.S.Table, old), Is.EqualTo(PartyOrderAcceptResult.RejectedRecipe));
            Assert.That(p.S.Table.CurrentCustomer, Is.SameAs(p.S.Customer)); Assert.That(p.S.Table.TableState, Is.EqualTo(ETableState.Seating));
            Assert.That(p.S.Table.PartyOrders, Is.Null); Assert.That(p.S.Table.TotalPrice, Is.EqualTo(101));
            Assert.That(p.S.Stage.Satisfaction, Is.EqualTo(-10));
        }
    }

    [Test]
    public void CompletionObserverDisablingBurner_KeepsNextOrderQueuedUntilUsable()
    {
        using (var p = new Pair())
        {
            p.Accept(); p.Pump();
            Action disable = () => p.Burner.IsUsable = false;
            UserInfo.OnAddCookCountHandler += disable; p.Pump(); UserInfo.OnAddCookCountHandler -= disable;
            Assert.That(p.Burner.CookingData.IsDefault(), Is.True); Assert.That(p.S.Queue.Count, Is.EqualTo(1));
            p.Burner.IsUsable = true; p.Pump(); p.Pump(); Assert.That(UserInfo.TotalCookCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void NormalCompletionPreservesChefLease_WhileCancellationReleasesOnlyItsBurner()
    {
        using (var p = new Pair())
        {
            p.Accept(); p.Pump();
            var staff = p.S.Create<Staff>(); var burner = p.S.Create<BurnerKitchenUtensil>();
            p.Burner.SetKitchenUtensil(burner); p.Burner.SetUseStaff(staff); p.Burner.SetStaffUsable(true); p.Burner.SetAddCookSpeedMul(7); burner.SetStaffWorking(true);
            p.Pump();
            Assert.That(p.Burner.UseStaff, Is.SameAs(staff)); Assert.That(burner.IsStaffWorking, Is.True);
            p.S.Manager.CancelPartyGuide(p.S.Table);
            Assert.That(p.Burner.UseStaff, Is.Null); Assert.That(p.Burner.IsStaffUsable, Is.False);
            Assert.That(p.Burner.AddCookSpeedMul, Is.Zero); Assert.That(burner.IsStaffWorking, Is.False);
            Assert.That(UserInfo.TotalCookCount, Is.EqualTo(1));
        }
    }

    [Test]
    public void OldChefCleanup_ReleasesLocalReferenceWithoutRemovingNewLease()
    {
        using (var s = new Flow())
        {
            var action = (ChefAction)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ChefAction));
            var staff = s.Create<Staff>(); var data = new KitchenBurnerData(); data.SetUseStaff(staff);
            Set(action, "_staff", staff); Set(action, "_burnerData", data); Set(action, "_burnerReservation", 1L);
            data.SetUseStaff(null); data.SetUseStaff(staff);
            Invoke(action, "CleanupBurnerData", data);
            Assert.That(data.UseStaff, Is.SameAs(staff)); Assert.That(Get<KitchenBurnerData>(action, "_burnerData"), Is.Null);
        }
    }

    [Test]
    public void OldSinkWait_CannotReplaceNewStaffReservation()
    {
        using (var s = new Flow())
        {
            var action = (ChefAction)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ChefAction));
            var staff = s.Create<Staff>(); var newer = s.Create<Staff>(); var sink = s.Create<SinkKitchenUtensil>();
            sink.SetUseStaff(staff); Set(action,"_staff",staff); Set(action,"_sink",sink);
            Invoke(action,"UpdateSinkAction",new Func<bool>(()=>false)); var stale = s.TakeWait(.1f);
            Set(action,"_actionVersion",1L); sink.SetUseStaff(newer); stale();
            Assert.That(sink.UseStaff, Is.SameAs(newer));
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void ReverseBurnerCompletion_PreservesEachRecipientAndReadyRecord(bool same)
    {
        using (var p = new Pair(same:same))
        {
            var timer = Get<UIBurnerTimer[]>(p.S.Kitchen,"_burnerTimers")[0];
            var smoke = Get<GameObject[]>(p.S.Kitchen,"_smokeAnimations")[0];
            Set(p.S.Kitchen,"_burnerDatas",new[] { new KitchenBurnerData { IsUsable=true },new KitchenBurnerData { IsUsable=true } });
            Set(p.S.Kitchen,"_burnerTimers",new[] { timer,timer }); Set(p.S.Kitchen,"_smokeAnimations",new[] { smoke,smoke });
            p.Accept(); Invoke(p.S.Kitchen,"DequeueFood",0); Invoke(p.S.Kitchen,"DequeueFood",1); Invoke(p.S.Kitchen,"DequeueFood",1);
            Assert.That(p.Batch.Orders[0].State,Is.EqualTo(PartyFoodState.Cooking));
            Assert.That(p.Batch.Orders[1].State,Is.EqualTo(PartyFoodState.ReadyForServing));
            Invoke(p.S.Kitchen,"DequeueFood",0);
            Assert.That(UserInfo.TotalCookCount,Is.EqualTo(2)); Assert.That(p.Batch.Orders.All(o=>o.State==PartyFoodState.ReadyForServing),Is.True);
            Assert.That(p.Batch.Orders.Select(o=>o.Member.Customer.NormalCustomerData.Id),Is.EqualTo(new[]{p.S.Data.Id,p.SecondData.Id}));
        }
    }

    [Test]
    public void VipKitchenUsesOnlyItsConnectedFloor()
    {
        using(var p=new Pair())
        {
            p.S.Table.FloorType=ERestaurantFloorType.Floor2; Set(p.S.Kitchen,"_floorType",ERestaurantFloorType.Floor2);
            var system=Get<KitchenSystem>(p.S.Manager,"_kitchenSystem");
            var other=p.S.Create<KitchenUtensilGroup>();
            Set(system,"_kitchenUtensilGroupDic",new Dictionary<ERestaurantFloorType,KitchenUtensilGroup>{[ERestaurantFloorType.Floor1]=other,[ERestaurantFloorType.Floor2]=p.S.Kitchen});
            Assert.That(p.Accept(),Is.EqualTo(PartyOrderAcceptResult.Accepted));
            Assert.That(Get<Queue<CookingData>>(other,"_cookingQueue"),Is.Empty);
            p.Pump(); p.Pump(); p.Pump(); Assert.That(UserInfo.TotalCookCount,Is.EqualTo(2));
        }
    }

    [Test]
    public void OldPairCookingCallbacks_CannotFinishReusedCustomersNewBatch()
    {
        using(var p=new Pair())
        {
            p.Accept(); p.Pump(); var old=p.Batch; var late=old.Orders.Select(o=>o.Cooking.OnCompleted).ToArray();
            p.S.Manager.CancelPartyGuide(p.S.Table);
            Assert.That(p.S.Controller.TrySpawnWaitingParty(new[]{p.S.Data.Id,p.SecondData.Id},out var next),Is.True);
            foreach(var member in next.Members) { member.Customer.transform.position=member.Customer.TargetPos; Get<Action>(member.Customer,"_moveCompleted")(); }
            Assert.That(p.S.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor1),Is.True);
            foreach(var member in next.Members) { member.Customer.transform.position=member.Customer.TargetPos; Get<Action>(member.Customer,"_moveCompleted")(); p.S.TakeWait(1.5f)(); }
            var fresh=p.Batch; foreach(var callback in late)callback();
            Assert.That(p.S.Manager.TryAcceptPartyOrders(p.S.Table,old),Is.EqualTo(PartyOrderAcceptResult.Stale));
            Assert.That(p.Batch,Is.SameAs(fresh)); Assert.That(fresh.State,Is.EqualTo(PartyOrderBatchState.Prepared)); Assert.That(UserInfo.TotalCookCount,Is.Zero);
        }
    }

    [TestCase(false)] [TestCase(true)]
    public void AuthoredBubbleSizeAndOffsets_PersistThroughTemporaryPrefabReloadAndFurnitureRefresh(bool serving)
    {
        string path = "Assets/PartyOrderOwnedTest-" + Guid.NewGuid().ToString("N") + ".prefab";
        string tablePath = path.Replace(".prefab", "-table.prefab");
        try
        {
            using (var p = new Pair())
            {
                if(serving) { p.S.Table.CompanionOrderButton=p.S.Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Table/ServingButton.prefab"))).GetComponent<TableButton>(); p.S.Table.CompanionOrderButton.Init(); }
                var go = p.S.Table.CompanionOrderButton.gameObject;
                var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(123, 87);
                var anchor = Get<WorldToSceenPosition>(p.S.Table.CompanionOrderButton, "_worldToSceenPos");
                Set(anchor, "_offset", new Vector3(.4f, 4.1f, 0));
                PrefabUtility.SaveAsPrefabAsset(go, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var clone = p.S.Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
                p.S.Table.CompanionOrderButton = clone.GetComponent<TableButton>(); p.S.Table.CompanionOrderButton.Init();
                if(serving) p.S.Table.CompanionServingButton=p.S.Table.CompanionOrderButton;
                Set(p.S.Table, "_rightSeatOffset", new Vector2(.3f,.2f)); Set(p.S.Table, serving ? "_rightServingOffset" : "_rightOrderOffset", new Vector3(.6f,.1f,0));
                p.S.Table.SetRightChairTrPos(new Vector3(8,3,0)); p.S.Table.RefreshPartyOrderButtons();
                Assert.That(((RectTransform)clone.transform).sizeDelta, Is.EqualTo(new Vector2(123,87)));
                var reloaded = Get<WorldToSceenPosition>(p.S.Table.CompanionOrderButton, "_worldToSceenPos");
                Assert.That(Get<Vector3>(reloaded, "_offset"), Is.EqualTo(new Vector3(.4f,4.1f,0)));
                Assert.That(Vector3.Distance(Get<Vector3>(reloaded, "_additionalOffset"), new Vector3(.9f,.3f,0)), Is.LessThan(.0001f));
                Assert.That(Get<Transform>(reloaded, "_worldTransform"), Is.SameAs(p.S.Table.ChairTrs[1]));
                // Save the table's authored offsets separately from its live use, then apply
                // the reloaded settings before running the actual furniture layout method.
                var settings = p.S.Create<TableData>();
                Set(settings, "_rightSeatOffset", new Vector2(.3f,.2f));
                Set(settings, serving ? "_rightServingOffset" : "_rightOrderOffset", new Vector3(.6f,.1f,0));
                PrefabUtility.SaveAsPrefabAsset(settings.gameObject, tablePath);
                AssetDatabase.ImportAsset(tablePath, ImportAssetOptions.ForceUpdate);
                var saved = AssetDatabase.LoadAssetAtPath<GameObject>(tablePath).GetComponent<TableData>();
                Assert.That(Get<Vector2>(saved, "_rightSeatOffset"), Is.EqualTo(new Vector2(.3f,.2f)));
                Assert.That(Get<Vector3>(saved, serving ? "_rightServingOffset" : "_rightOrderOffset"), Is.EqualTo(new Vector3(.6f,.1f,0)));
                if(serving) p.S.Table.CompanionServingButton=p.S.Table.CompanionOrderButton;
                Set(p.S.Table, "_rightSeatOffset", Get<Vector2>(saved, "_rightSeatOffset"));
                Set(p.S.Table, serving ? "_rightServingOffset" : "_rightOrderOffset", Get<Vector3>(saved, serving ? "_rightServingOffset" : "_rightOrderOffset"));
                var furniture = p.S.Table.TableFurniture;
                foreach (string field in new[] { "_spriteRenderer", "_leftChairSpriteRenderer", "_rightChairSpriteRenderer",
                    "_leftChairArmrestSpriteRenderer", "_rightChairArmrestSpriteRenderer", "_bowlRenderer" })
                {
                    var renderer = p.S.Create<SpriteRenderer>(); renderer.transform.SetParent(furniture.transform);
                    Set(furniture, field, renderer);
                }
                var art = p.S.Track(ScriptableObject.CreateInstance<TableFurnitureData>());
                Sprite tableSprite = Resources.LoadAll<Sprite>("FurnitureData/Sprites/01/TABLE01_우든 클래식 테이블").First();
                Sprite chairSprite = Resources.LoadAll<Sprite>("FurnitureData/Sprites/01/TABLE01_우든 클래식 체어L").First();
                Set(art, "_sprite", tableSprite); Set(art, "_chairSprite", chairSprite);
                foreach (var position in new[] { new Vector3(1,2,0), new Vector3(8,3,0) })
                {
                    furniture.transform.position = position; furniture.SetFurnitureData(art);
                    p.S.Table.RefreshPartyOrderButtons();
                    var chair = Get<SpriteRenderer>(furniture, "_rightChairSpriteRenderer").transform;
                    Assert.That(p.S.Table.ChairTrs[1].position, Is.EqualTo(chair.position));
                    Assert.That(Vector3.Distance(p.S.Table.GetSeatPosition(1), chair.position + new Vector3(.3f,.2f,0)), Is.LessThan(.0001f));
                    Assert.That(Vector3.Distance(Get<Vector3>(reloaded, "_additionalOffset"), new Vector3(.9f,.3f,0)), Is.LessThan(.0001f));
                    Assert.That(Get<Transform>(reloaded, "_worldTransform"), Is.SameAs(p.S.Table.ChairTrs[1]));
                    Assert.That(((RectTransform)clone.transform).sizeDelta, Is.EqualTo(new Vector2(123,87)));
                }
                // The saved production menu bubble has no text/font component to override.
                Assert.That(clone.GetComponentsInChildren<TMPro.TMP_Text>(true), Is.Empty);
            }
        }
        finally
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(tablePath) != null) AssetDatabase.DeleteAsset(tablePath);
        }
    }

    [Test]
    public void SingleCancelledCooking_IsNotCompletion_AndSingleNormalAdditionalRoundsStillWork()
    {
        using (var s = new Flow())
        {
            s.GuideAndSeat(); s.Manager.OnCustomerOrder(s.Table); Invoke(s.Kitchen, "DequeueFood", 0);
            Invoke(s.Table, "EndUse", null, ETableState.Empty); Invoke(s.Kitchen, "DequeueFood", 0);
            Assert.That(UserInfo.TotalCookCount, Is.Zero); Assert.That(s.Table.TableState, Is.Not.EqualTo(ETableState.CanServing));
        }
        using (var s = new Flow(extraOrder: true))
        { s.GuideAndSeat(); s.OrderAndCook(true); s.Eat(); s.OrderAndCook(false); s.Eat(); s.Settle(); s.AssertSettled(2); }
    }
}
#endif
