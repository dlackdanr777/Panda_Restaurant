#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Muks.PathFinding;
using Muks.PathFinding.AStar;
using Muks.Tween;
using Muks.BackEnd;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Executes production operations and their captured callbacks in the runner's disposable
// PreviewScene. Time and movement completion are advanced explicitly; this is not a
// frame timing/path interpolation test. No gameplay replacement, SDK or account save.
public sealed class CustomerTableUseRegressionTests
{
    [TestCase("guide", false)]
    [TestCase("seat", false)]
    [TestCase("cook", false)]
    [TestCase("eat-start", false)]
    [TestCase("eat-not-started", false)]
    [TestCase("eat-end", false)]
    [TestCase("settlement", false)]
    [TestCase("garbage", false)]
    [TestCase("exit", false)]
    [TestCase("guide", true)]
    [TestCase("seat", true)]
    [TestCase("cook", true)]
    [TestCase("eat-start", true)]
    [TestCase("eat-not-started", true)]
    [TestCase("eat-end", true)]
    [TestCase("settlement", true)]
    [TestCase("garbage", true)]
    [TestCase("exit", true)]
    public void StaleCallbacks_CannotChangeNextUse_OrReusedPooledCustomer(string point, bool sameObject)
    {
        using (var s = new Flow())
        {
            Action old = s.HoldAt(point);
            var previousParty = s.Customer.VisitParty;
            object oldUse = s.Use;
            var previous = s.Customer;
            s.NextCustomer(sameObject);
            Assert.That(ReferenceEquals(previous, s.Customer), Is.EqualTo(sameObject));
            s.GuideAndSeat();
            var nextParty = s.Customer.VisitParty;
            Assert.That(nextParty, Is.Not.Null);
            if (previousParty != null) Assert.That(previousParty.IsActive, Is.False);
            Assert.That(s.Use, Is.Not.SameAs(oldUse));
            var sentinel = s.NewSprite(); s.Customer.ShowFood(sentinel);
            string before = s.Snapshot();
            old(); old();
            Assert.That(s.Customer.VisitParty, Is.SameAs(nextParty));
            Assert.That(s.Table.CurrentParty, Is.SameAs(nextParty));
            Assert.That(s.Snapshot(), Is.EqualTo(before), point);
            Assert.That(Get<SpriteRenderer>(s.Customer, "_foodRenderer").sprite, Is.SameAs(sentinel));
            s.OrderAndCook(false); s.Eat(); s.Settle();
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.NeedCleaning), "New visit still completes normally");
        }
    }

    [Test]
    public void AdditionalOrder_HasNewIdentity_CopiesKeepIdentity_AndOldCookOrEatCannotCompleteIt()
    {
        using (var s = new Flow(extraOrder: true))
        {
            s.GuideAndSeat(); object use = s.Use;
            var party = s.Customer.VisitParty;
            var first = s.Table.CurrentFood;
            object firstId = Get<object>(first, "_identity");
            Assert.That(Get<object>(s.Table.CurrentFood, "_identity"), Is.SameAs(firstId));
            s.OrderAndCook(false);
            var duplicateEat = (IEnumerator)Invoke(s.Manager, "EatRoutine", s.Table, s.Food);
            Assert.That(duplicateEat.MoveNext(), Is.True);
            s.Eat();
            Assert.That(s.Use, Is.SameAs(use));
            Assert.That(s.Customer.VisitParty, Is.SameAs(party));
            Assert.That(s.Table.CurrentParty, Is.SameAs(party));
            Assert.That(party.Members[0].SeatIndex, Is.EqualTo(s.Table.SitIndex));
            Assert.That(Get<object>(s.Table.CurrentFood, "_identity"), Is.Not.SameAs(firstId));
            Assert.That(s.Table.CurrentFood.FoodData.Id, Is.EqualTo(first.FoodData.Id));
            string before = s.Snapshot(); first.OnCompleted(); first.OnCompleted();
            Assert.That(duplicateEat.MoveNext(), Is.False);
            Assert.That(s.Snapshot(), Is.EqualTo(before));
            s.OrderAndCook(true); s.Eat(); s.Settle(); s.AssertSettled(2);
        }
    }

    [Test]
    public void DuplicateCompletions_PayExactlyOnce_AndTipSurvivesEndUse()
    {
        using (var s = new Flow())
        {
            var arrival = s.Guide(); arrival(); arrival();
            var seating = s.TakeWait(1.5f); seating(); seating(); s.AssertPrepared(1);
            s.OrderAndCook(false); var food = s.Table.CurrentFood;
            string cooked = s.Snapshot(); food.OnCompleted(); food.OnCompleted(); Assert.That(s.Snapshot(), Is.EqualTo(cooked));
            s.Eat(); Invoke(s.Manager, "EndEat", s.Table); Invoke(s.Manager, "EndEat", s.Table);
            s.AssertBeforeSettlement(1);
            var garbage = s.TakeWait(.02f); garbage(); garbage();
            var settle = s.TakeWait(.5f); settle(); string paid = s.Snapshot(); settle();
            Assert.That(s.Snapshot(), Is.EqualTo(paid)); Assert.That(s.Stage.Tip, Is.EqualTo(50)); Assert.That(s.Use, Is.Null);
            var exit = Get<Action>(s.Customer, "_moveCompleted"); exit(); exit();
            Assert.That(Get<Queue<NormalCustomer>>(ObjectPoolManager.Instance, "_normalCustomerPool").Count, Is.EqualTo(1));
        }
    }

    [TestCase("disable")]
    [TestCase("scene")]
    [TestCase("account")]
    [TestCase("stage")]
    public void ExecutionBoundary_InvalidatesHeldCallbacks_AndReentryUsesNewContext(string boundary)
    {
        using (var s = new Flow())
        {
            BackendManager backend = null;
            if (boundary == "account") backend = s.InstallAccount();
            var old = s.HoldAt("settlement");
            if (boundary == "disable") { s.RestartManager(); }
            if (boundary == "scene")
            {
                ((Action)Field(typeof(LoadingSceneManager), "OnLoadSceneHandler").GetValue(null))();
                string sceneBoundary = s.Snapshot(); old(); Assert.That(s.Snapshot(), Is.EqualTo(sceneBoundary));
                s.RestartManager();
            }
            if (boundary == "account") backend.InvalidateGameDataRestore();
            if (boundary == "stage") UserInfo.ChangeStage(EStage.Stage2);
            string before = s.Snapshot(); old(); old(); Assert.That(s.Snapshot(), Is.EqualTo(before));
            if (boundary == "account") s.RestoreAccount(backend);
            UserInfo.ChangeStage(EStage.Stage1);
            // End the old table connection, as scene teardown does, then start a new use.
            Invoke(s.Table, "OnDisable"); s.Table.CurrentCustomer = null;
            // Reentry restores an available table, not the old scene's Eating state.
            s.Table.TableState = ETableState.Empty;
            s.NextCustomer(true); s.GuideAndSeat(); s.OrderAndCook(false); s.Eat(); s.Settle();
            Assert.That(s.Stage.Tip, Is.EqualTo(50));
        }
    }

    [TestCase("ValidatePaymentTipCalculator")]
    [TestCase("ValidateSkill06PaymentBoundary")]
    [TestCase("ValidateSkill05FoodPriceGetterStructure")]
    [TestCase("ValidateSkill05OrderPriceBoundary")]
    [TestCase("ValidateBaseFoodTipPolicy")]
    [TestCase("ValidateFoodTipIsolationStructure")]
    [TestCase("ValidateFeverMultiplierCalculator")]
    [TestCase("ValidateFeverFoodPriceBoundary")]
    [TestCase("ValidateNormalCustomerMoveChannel")]
    [TestCase("ValidateCookingChannelAndCap")]
    public void ExistingPolicyValidators_RemainSatisfied(string method)
    {
        var type = typeof(GachaReviewTestRunner).Assembly.GetType("PandaRestaurant.Editor.StaffDataValidation.StaffSkillRuntimeFoundationValidator", true);
        try { type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    [Test]
    public void PooledVisitAndStopMove_RejectActualQueuedPathResultBeforeItCanStartMovement()
    {
        using (var s = new Flow())
        {
            s.Guide();
            var paths = Get<System.Collections.Concurrent.ConcurrentQueue<Action>>(PathfindingQueue.Instance, "_pathfindingQueue");
            Assert.That(paths.TryDequeue(out Action oldPath), Is.True);
            s.NextCustomer(true); s.GuideAndSeat();
            string before = s.Snapshot();
            oldPath(); MainThreadDispatcher.Instance.Update();
            Assert.That(s.Snapshot(), Is.EqualTo(before));
            Assert.That(Get<Coroutine>(s.Customer, "_moveCoroutine"), Is.Null);
        }
    }

    [Test]
    public void StaffServingAndDuplicateServing_PreserveOneMealAndCapturedTipBonus()
    {
        using (var s = new Flow())
        {
            s.GuideAndSeat(); s.OrderAndCook(true);
            var worker = s.CreateWaiter();
            var reservation = s.Manager.ReserveWaiterWork(s.Table, worker.staff, worker.action, true);
            Assert.That(s.Manager.PickUpWaiterFood(reservation), Is.True);
            var registry = GameManager.Instance.StaffSkillEffectRegistry;
            var token = new StaffSkillSourceToken(1, 1);
            registry.RegisterOrUpdate(StaffSkillEffectType.RestaurantTipPayoutPercent, token, 25f, "detached test");
            Assert.That(s.Manager.TryDeliverFood(s.Table.CurrentFood.Identity, reservation), Is.True);
            s.Manager.StopAllCoroutines();
            var eating = (IEnumerator)Invoke(s.Manager, "EatRoutine", s.Table, s.Food);
            string once = s.Snapshot(); s.Manager.OnServing(s.Table); Assert.That(s.Snapshot(), Is.EqualTo(once));
            eating.MoveNext(); eating.MoveNext(); eating.MoveNext();
            registry.RemoveAllForSource(token); // Payout uses the bonus captured at EndEat.
            var settle = s.TakeWait(.5f); settle(); settle();
            Assert.That(s.Stage.Tip, Is.EqualTo(62));
            Assert.That(s.Coins.CurrentMoney, Is.EqualTo(101));
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.EqualTo(1));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Baseline_SingleCustomer_ManualAndWaiterOrder_PaysVisitsAndCleans(bool waiter)
    {
        using (var s = new Flow())
        {
            s.GuideAndSeat();
            s.AssertPrepared(1);
            s.OrderAndCook(waiter);
            s.Eat();
            s.AssertBeforeSettlement(1);
            s.Settle();
            s.AssertSettled(1);
            s.CleanAndExit();
        }
    }

    [Test]
    public void Baseline_ActualType3Skin_AdditionalSameMenuOrder_SettlesOnce()
    {
        using (var s = new Flow(extraOrder: true))
        {
            Assert.That(s.Customer.OrderCount, Is.EqualTo(2));
            s.GuideAndSeat();
            s.AssertPrepared(1);
            s.OrderAndCook(false);
            s.Eat();
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.Seating));
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.Zero);
            Assert.That(s.Coins.CurrentMoney, Is.Zero);
            s.AssertPrepared(2);
            s.OrderAndCook(false);
            s.Eat();
            s.AssertBeforeSettlement(2);
            s.Settle();
            s.AssertSettled(2);
            s.CleanAndExit();
        }
    }

    [Test]
    public void Baseline_UnownedRecipe_IsSelectedThenRejectedWithoutVisitOrPayment()
    {
        using (var s = new Flow(owned: false))
        {
            // Existing diagnostic: menu preparation queries level before ownership is rejected.
            // Preserve this baseline observation without treating it as the desired policy.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "해당 음식을 보유하고 있지 않습니다: " + s.Food.Id);
            s.GuideAndSeat();
            s.AssertPrepared(1);
            s.Manager.OnCustomerOrder(s.Table);
            Assert.That(s.Queue.Count, Is.Zero);
            Assert.That(s.Table.CurrentCustomer, Is.Null);
            Assert.That(s.Table.TableState, Is.EqualTo(ETableState.Empty));
            Assert.That(s.Table.TotalPrice, Is.Zero);
            Assert.That(s.Table.TotalTip, Is.Zero);
            Assert.That(UserInfo.GetVisitedCustomerCount(s.Data.Id), Is.Zero);
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.Zero);
            Assert.That(s.Stage.Satisfaction, Is.EqualTo(-5));
            Assert.That(s.Stage.Tip, Is.Zero);
            Assert.That(s.Stage.FeverGauge, Is.Zero);
            Assert.That(s.Coins.CurrentMoney, Is.Zero);
        }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    internal static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    internal static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);
    internal static object Invoke(object target, string name, params object[] args)
    {
        try { for (var type=target.GetType(); type!=null; type=type.BaseType) { var method=type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if(method!=null) return method.Invoke(target,args); } throw new MissingMethodException(name); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    internal sealed class Flow : IDisposable
    {
        private readonly List<Action> restore = new List<Action>();
        private readonly List<Object> objects = new List<Object>();
        private readonly UnityEngine.Random.State random = UnityEngine.Random.state;
        private readonly bool tutorial = UserInfo.IsTutorialStart, tutorialClear = UserInfo.IsFirstTutorialClear;
        private GameObject waits, poolParent;
        public TableManager Manager;
        public TableData Table;
        public NormalCustomer Customer;
        public NormalCustomerData Data;
        public FoodData Food;
        public StageInfo Stage;
        public DropCoinArea Coins;
        public CustomerController Controller;
        public KitchenUtensilGroup Kitchen;
        public object Use => typeof(TableData).GetProperty("CurrentUse", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Table);
        public Queue<CookingData> Queue => Get<Queue<CookingData>>(Kitchen, "_cookingQueue");

        public Flow(bool extraOrder = false, bool owned = true)
        {
            try { Initialize(extraOrder, owned); }
            catch { Dispose(); throw; }
        }

        private void Initialize(bool extraOrder, bool owned)
        {
            Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
            Swap(typeof(BackendManager), "_instance", null);
            Swap(typeof(Muks.DataBind.DataBind), "_textBindDic", new Muks.DataBind.DataBindContainer<string>());
            foreach (var field in typeof(UserInfo).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) Swap(typeof(UserInfo), field.Name, null);
            Swap(typeof(LoadingSceneManager), "OnLoadSceneHandler", null);
            Swap(typeof(UserInfo), "_currentStage", EStage.Stage1);
            Stage = new StageInfo();
            Swap(typeof(UserInfo), "_stageInfos", new[] { Stage, new StageInfo(), new StageInfo() });
            foreach (string name in new[] { "_promotionCount", "_totalCumulativeCustomerCount", "_dailyCumulativeCustomerCount", "_weeklyCumulativeCustomerCount", "_totalCookCount", "_dailyCookCount", "_weeklyCookCount" })
                Swap(typeof(UserInfo), name, 0);
            Swap(typeof(UserInfo), "_recipeCookCountDic", new Dictionary<string, int>());
            UserInfo.IsTutorialStart = false; UserInfo.IsFirstTutorialClear = true;
            var game = Create<GameManager>(); Swap(typeof(GameManager), "_instance", game);
            Set(game, "_addGachaItemFoodPriceMulDic", new Dictionary<FoodType, float> { [FoodType.Cozy] = 0 });
            Set(game, "_addGachaItemFoodCookingTimeMulDic", new Dictionary<FoodType, float> { [FoodType.Cozy] = 0 });
            Set(game, "_addGachaItemStaffSpeedMulDic", new Dictionary<StaffGroupType, float> { [StaffGroupType.Waiter] = 0 });
            var sound = Create<SoundManager>(); Swap(typeof(SoundManager), "_instance", sound); Set(sound, "_masterVolume", 0f);
            Swap(typeof(TimeManager), "_instance", Create<TimeManager>());
            var astar = Create<AStar>(); Swap(typeof(AStar), "<Instance>k__BackingField", astar);
            var map = new MapData(new Vector2(-32, -32), new Vector2(96, 96));
            for (int x = 0; x < map.SizeX; x++) for (int y = 0; y < map.SizeY; y++) map.Nodes[x, y] = new Node(false, true, x, y);
            Set(astar, "_maps", new Dictionary<string, MapData> { ["Detached"] = map });
            Swap(typeof(PathfindingQueue), "_instance", Create<PathfindingQueue>());
            Swap(typeof(MainThreadDispatcher), "_instance", Create<MainThreadDispatcher>());
            Swap(typeof(MainThreadDispatcher), "_executionQueue", new Queue<Action>());
            waits = Track(new GameObject("Owned table use waits"));
            Swap(typeof(Tween), "_waitQueueParent", waits); Swap(typeof(Tween), "_tweenWaitQueue", new Queue<TweenWait>());
            var pool = Create<ObjectPoolManager>(); Swap(typeof(ObjectPoolManager), "_instance", pool);
            poolParent = Track(new GameObject("Owned table use pool"));
            Swap(typeof(ObjectPoolManager), "_customerParent", poolParent);
            var staffPools=new Queue<Staff>[(int)EquipStaffType.Length]; var staffParents=new GameObject[(int)EquipStaffType.Length];
            var staffPrefabs=new Staff[(int)EquipStaffType.Length];
            for(int i=0;i<staffPools.Length;i++) { staffPools[i]=new Queue<Staff>(); staffParents[i]=poolParent; }
            staffPrefabs[(int)EquipStaffType.Waiter]=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/Staff/Waiter.prefab").GetComponent<Staff>();
            Swap(typeof(ObjectPoolManager),"_staffPools",staffPools); Swap(typeof(ObjectPoolManager),"_staffParents",staffParents); Swap(typeof(ObjectPoolManager),"_staffPrefabs",staffPrefabs);
            Swap(typeof(ObjectPoolManager), "_normalCustomerPool", new Queue<NormalCustomer>());
            Swap(typeof(ObjectPoolManager), "_normalCustomerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/NormalCustomer.prefab").GetComponent<NormalCustomer>());
            foreach (string kind in new[] { "coin", "garbage" })
            {
                Swap(typeof(ObjectPoolManager), "_" + kind + "Parent", poolParent);
                Swap(typeof(ObjectPoolManager), "_" + kind + "Pool", new Queue<PointerDownSpriteRenderer>());
                Swap(typeof(ObjectPoolManager), "_enabled" + char.ToUpperInvariant(kind[0]) + kind.Substring(1) + "Pool", new List<PointerDownSpriteRenderer>());
                Swap(typeof(ObjectPoolManager), "_" + kind + "Prefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/" + (kind == "coin" ? "Coin" : "Garbage/Garbage") + ".prefab").GetComponent<PointerDownSpriteRenderer>());
            }
            Swap(typeof(ObjectPoolManager), "_garbageImages", new Sprite[] { null });
            Food = Track(ScriptableObject.CreateInstance<FoodData>());
            Set(Food, "_sprite", NewSprite());
            Set(Food, "_id", "TABLE-USE-FOOD"); Set(Food, "_foodType", FoodType.Cozy); Set(Food, "_sellPrice", 101); Set(Food, "_cookingTime", 3f);
            Swap(typeof(FoodDataManager), "_instance", Create<FoodDataManager>());
            Swap(typeof(FoodDataManager), "_foodDataDic", new Dictionary<string, FoodData> { [Food.Id] = Food });
            Swap(typeof(UserInfo), "_giveRecipeLevelDic", owned ? new Dictionary<string, int> { [Food.Id] = 1 } : new Dictionary<string, int>());
            Data = Track(ScriptableObject.CreateInstance<NormalCustomerData>());
            Set(Data, "_sprite", NewSprite());
            Set(Data, "_id", "TABLE-USE-CUSTOMER"); Set(Data, "_requiredDish", Food.Id); Set(Data, "_tendencyType", CustomerTendencyType.Normal);
            Set(Data, "_orderFoodTime", 100); Set(Data, "_wiatTime", 100);
            Set(Data, "_visitCountFoodDic", new Dictionary<int, string>());
            Swap(typeof(CustomerDataManager), "_instance", Create<CustomerDataManager>());
            Swap(typeof(CustomerDataManager), "_customerDataDic", new Dictionary<string, CustomerData> { [Data.Id] = Data });
            Swap(typeof(CustomerDataManager), "_normalCustomerDataList", new List<NormalCustomerData> { Data });
            Swap(typeof(CustomerDataManager), "_specialCustomerDataList", new List<SpecialCustomerData>());
            Swap(typeof(CustomerDataManager), "_gatecrasherCustomerDataList", new List<GatecrasherCustomerData>());
            var skin = Track(ScriptableObject.CreateInstance<CustomerSkinData>());
            Set(skin, "_sprite", NewSprite());
            Set(skin, "_id", "TABLE-USE-SKIN"); Set(skin, "_upgradeType", SkinCustomerUpgradeType.Type3); Set(skin, "_upgradeValue", 1f);
            Swap(typeof(SkinDataManager), "_instance", Create<SkinDataManager>());
            Swap(typeof(SkinDataManager), "_customerSkinDataDic", new Dictionary<string, CustomerSkinData> { [skin.Id] = skin });
            Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData> { [Data.Id] = new SaveCustomerData(Data.Id, extraOrder ? skin.Id : "", 0) });
            Controller = Create<CustomerController>(); Swap(typeof(CustomerController), "_queueOwner", Controller);
            Set(Controller, "_startLine", Create<Transform>());
            Manager = Create<TableManager>();
            Set(Manager, "_customerController", Controller); Set(Manager, "_guideButton", Create<Button>());
            Set(Manager, "_satisfactionSystem", Create<SatisfactionSystem>());
            Set(Controller, "_tableManager", Manager);
            Set(Controller, "_satisfactionSystem", Get<SatisfactionSystem>(Manager, "_satisfactionSystem"));
            Set(Controller, "_lineSpacingGrid", 2);
            Invoke(Controller, "Awake"); Invoke(Controller, "OnEnable");
            Set(Controller, "_breakCustomerEnabled", false);
            Table = Create<TableData>(); Table.FloorType = ERestaurantFloorType.Floor1; Table.TableType = TableType.Table1; Table.TableState = ETableState.Empty;
            Table.TableFurniture = Create<TableFurniture>();
            Set(Table.TableFurniture, "_tableManager", Manager); Set(Table.TableFurniture, "_floor", Table.FloorType);
            Table.TableFurniture.SetTableData(Table, Stage.GetTableData(Table.FloorType, Table.TableType));
            Set(Table, "_chairTrs", new[] { Create<Transform>(), Create<Transform>() });
            Set(Table, "_customerMoveTr", Create<Transform>()); Set(Table, "_tableButtonTr", Create<Transform>());
            Table.OrderButton = Button(); Table.ServingButton = Button();
            Coins = Create<DropCoinArea>(); Set(Coins, "_dropArea", Create<Transform>()); Set(Coins, "_maxCoinCount", 10); Set(Coins, "_data", new CoinAreaData());
            Set(Table, "_dropCoinAreas", new[] { Coins, Coins });
            var garbage = Create<DropGarbageArea>(); Set(garbage, "_dropArea", Create<Transform>()); Set(garbage, "_maxGarbageCount", 10); Set(garbage, "_garbageData", new GarbageAreaData());
            Set(Table, "_dropGarbageArea", garbage);
            var furniture = Create<FurnitureSystem>(); var group = Create<FurnitureGroup>();
            Set(group, "_floorType", Table.FloorType); Set(group, "_foodType", FoodType.Cozy);
            Set(group, "_defaultWaiter1Pos", Create<Transform>()); Set(group, "_hallFoodPos", new[] { Create<Transform>() });
            Set(group, "_door1", Create<Transform>()); Set(group, "_door2", Create<Transform>());
            Get<Transform>(group, "_door1").position = new Vector3(1, 0, 0);
            Get<Transform>(group, "_door2").position = new Vector3(1, 7.64f, 0);
            Set(group, "_tableDataDic", new Dictionary<TableType, TableData> { [Table.TableType] = Table });
            Set(furniture, "_furnitureGroupDic", new Dictionary<ERestaurantFloorType, FurnitureGroup> { [Table.FloorType] = group });
            Set(Manager, "_furnitureSystem", furniture);
            Kitchen = Create<KitchenUtensilGroup>();
            var timer = Create<UIBurnerTimer>(); Set(timer.GetComponent<UIImageFillAmount>(), "_fillImage", Create<Image>()); Set(timer, "_foodImage", Create<Image>()); timer.Init();
            Set(Kitchen, "_burnerDatas", new[] { new KitchenBurnerData { IsUsable = true } });
            Set(Kitchen, "_burnerTimers", new[] { timer }); Set(Kitchen, "_smokeAnimations", new[] { Create<Transform>().gameObject });
            var kitchenSystem = Create<KitchenSystem>(); Set(kitchenSystem, "_kitchenUtensilGroupDic", new Dictionary<ERestaurantFloorType, KitchenUtensilGroup> { [Table.FloorType] = Kitchen });
            Set(Manager, "_kitchenSystem", kitchenSystem);
            var fever = Create<FeverSystem>(); var uiFever = Create<UIFever>();
            var image = Create<Image>(); var fill = Create<UITweenFillAmountImage>(); Set(fill, "_fillAmountImage", image);
            var button = Create<Button>(); var press = button.gameObject.AddComponent<ButtonPressEffect>(); var buttonView = button.gameObject.AddComponent<UIButtonAndPressEffect>();
            Set(buttonView, "_button", button); Set(buttonView, "_pressEffect", press);
            var mirror = Create<Animator>(); mirror.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/Fever/MirrorBallAnimator.controller");
            Set(uiFever, "_fillAmountImage", fill); Set(uiFever, "_feverButton", buttonView); Set(uiFever, "_mirrorBallAnimator", mirror); Set(fever, "_uiFever", uiFever); Set(Manager, "_feverSystem", fever);
            Customer = pool.SpawnNormalCustomer(Vector3.zero, Quaternion.identity);
            Customer.SetData(Data, Controller, Manager);
            Manager.gameObject.SetActive(true);
            // EditMode does not dispatch the player lifecycle for these components.
            Invoke(Manager, "OnEnable");
            Assert.That(Invoke(Controller, "CreateSingleVisit", Customer), Is.True);
        }

        private TableButton Button()
        {
            var button = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Table/ServingButton.prefab"))).GetComponent<TableButton>(); button.Init(); return button;
        }
        public Action Guide()
        {
            if (!Controller.WaitingParties.Contains(Customer.VisitParty))
                Assert.That(Invoke(Controller, "EnqueueVisit", Customer.VisitParty, true), Is.True);
            Invoke(Manager, "OnCustomerGuide", Customer, Table, 0);
            Assert.That(Table.TableState, Is.EqualTo(ETableState.Move));
            Assert.That(Table.CurrentParty, Is.SameAs(Customer.VisitParty));
            Assert.That(Customer.VisitMember.SeatIndex, Is.EqualTo(Table.SitIndex));
            return Get<Action>(Customer, "_moveCompleted");
        }
        public void GuideAndSeat() { Guide()(); TakeWait(1.5f)(); }
        public Sprite NewSprite()
        {
            var texture = Track(new Texture2D(2, 2));
            return Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f));
        }
        public Action HoldAt(string point)
        {
            var arrival = Guide(); if (point == "guide") return arrival;
            arrival(); var seating = TakeWait(1.5f); if (point == "seat") return seating;
            seating(); if (point == "cook") { Manager.OnCustomerOrder(Table); return Table.CurrentFood.OnCompleted; }
            OrderAndCook(false);
            if (point == "eat-start" || point == "eat-end" || point == "eat-not-started")
            {
                var routine = BeginEat(); if (point != "eat-not-started") routine.MoveNext(); if (point == "eat-end") routine.MoveNext();
                if (point == "eat-not-started") return () => { while (routine.MoveNext()) { } };
                return () => { routine.MoveNext(); };
            }
            Eat(); if (point == "garbage") return TakeWait(.02f);
            if (point == "settlement") return TakeWait(.5f);
            Settle(); return Get<Action>(Customer, "_moveCompleted");
        }
        public void NextCustomer(bool sameObject)
        {
            foreach (var pending in waits.GetComponentsInChildren<TweenWait>(true)) pending.Stop();
            if (Table.CurrentCustomer != null) Manager.AngerExitCustomer(Table);
            if (Table.TableState == ETableState.NeedCleaning) Table.TableFurniture.OnCleanAction();
            if (sameObject) ObjectPoolManager.Instance.DespawnNormalCustomer(Customer);
            Customer = ObjectPoolManager.Instance.SpawnNormalCustomer(Vector3.zero, Quaternion.identity);
            Customer.SetData(Data, Controller, Manager);
            Assert.That(Invoke(Controller, "CreateSingleVisit", Customer), Is.True);
            // A cancelled old queued meal is intentionally not completed through Kitchen's
            // known cancellation/count path. Its captured callback is tested separately.
            Queue.Clear();
        }
        public void RestartManager()
        {
            Invoke(Manager, "OnDisable");
            Invoke(Manager, "OnEnable");
            if (typeof(CustomerController).GetProperty("VisitExecution", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Controller) == null)
                Invoke(Controller, "OnEnable");
        }
        public string Snapshot() => string.Join("|", Table.CurrentCustomer == null ? 0 : Table.CurrentCustomer.GetInstanceID(),
            Table.TableState, Table.TotalPrice, Table.TotalTip, Table.Satisfaction, Table.OrdersCount,
            Table.CurrentFood.FoodData == null ? "none" : Table.CurrentFood.FoodData.Id,
            Get<object>(Table.CurrentFood, "_identity")?.GetHashCode(), Stage.Tip, Stage.Satisfaction, Stage.FeverGauge,
            UserInfo.GetVisitedCustomerCount(Data.Id), UserInfo.TotalCumulativeCustomerCount, Coins.CurrentMoney,
            Table.DropGarbageArea.Count, Customer.gameObject.activeSelf, Get<CustomerState>(Customer, "_currentState"),
            Get<SpriteRenderer>(Customer, "_foodRenderer").gameObject.activeSelf, Customer.transform.position);

        public BackendManager InstallAccount()
        {
            var backend = Create<BackendManager>(); Swap(typeof(BackendManager), "_instance", backend); RestoreAccount(backend);
            Assert.That(Customer.VisitParty, Is.Null, "Installing a different account ends the old visit context");
            Customer.SetData(Data, Controller, Manager);
            Assert.That(Invoke(Controller, "CreateSingleVisit", Customer), Is.True);
            return backend;
        }
        public void RestoreAccount(BackendManager backend)
        {
            var context = new GameDataRestoreContext(() => "table-use-offline-account");
            var query = context.BeginQuery();
            var result = context.TryRestore(query, true,
                "{\"rows\":[{\"owner_inDate\":{\"S\":\"table-use-offline-account\"},\"inDate\":{\"S\":\"table-use-offline-row\"},\"Dia\":{\"N\":\"0\"}}]}",
                null, () => true);
            Assert.That(result.CanContinueLegacy, Is.True, result.Reason);
            Set(backend, "_gameDataRestoreContext", context);
        }
        public void AssertPrepared(int orders)
        {
            Assert.That(Table.CurrentFood.FoodData, Is.SameAs(Food));
            Assert.That(Table.TotalPrice, Is.EqualTo(101 * orders)); Assert.That(Table.TotalTip, Is.EqualTo(50 * orders));
            Assert.That(Table.Satisfaction, Is.EqualTo(2 * orders));
            Assert.That(Table.CurrentFood.CookTime, Is.EqualTo(3f));
        }
        public void OrderAndCook(bool waiter)
        {
            if (waiter)
            {
                var worker = CreateWaiter();
                Invoke(worker.action, "OrderAction", Table); Arrive(worker.staff); TakeWait(.5f)(); TakeWait(.5f)();
            }
            else Manager.OnCustomerOrder(Table);
            Assert.That(Table.TableState, Is.EqualTo(ETableState.WaitFood)); Assert.That(Queue.Count, Is.EqualTo(1));
            object identity = Get<object>(Table.CurrentFood, "_identity");
            Assert.That(Get<object>(Queue.Peek(), "_identity"), Is.SameAs(identity));
            Invoke(Kitchen, "DequeueFood", 0);
            Assert.That(Get<object>(Get<KitchenBurnerData[]>(Kitchen, "_burnerDatas")[0].CookingData, "_identity"), Is.SameAs(identity));
            Invoke(Kitchen, "DequeueFood", 0);
            Assert.That(Table.TableState, Is.EqualTo(ETableState.CanServing));
        }
        public (StaffWaiter staff, WaiterAction action) CreateWaiter()
        {
            var staff = Track(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/ObjectPool/Staff/Waiter.prefab"))).GetComponent<StaffWaiter>();
            staff.transform.position = Vector3.zero; Set(staff, "_tableManager", Manager);
            var staffData = Track(ScriptableObject.CreateInstance<WaiterData>());
            Set(staffData, "_id", "TABLE-USE-WAITER-" + staff.GetInstanceID()); Set(staffData, "_speed", 6f);
            Set(staffData, "_waiterLevelData", new[] { new WaiterLevelData() });
            Get<Dictionary<string, SaveStaffData>>(Stage, "_giveStaffDic")[staffData.Id] = new SaveStaffData(staffData.Id, 1);
            Get<Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>>(Stage, "_equipStaffTypeDic")[Table.FloorType][EquipStaffType.Waiter] = staffData;
            Swap(typeof(StaffDataManager), "_instance", Create<StaffDataManager>());
            Set(staffData, "_sprite", NewSprite()); Set(staffData, "_idleSprites", new[] { NewSprite() });
            staff.Init(EquipStaffType.Waiter, Manager, Get<KitchenSystem>(Manager,"_kitchenSystem"), Controller, Get<FeverSystem>(Manager,"_feverSystem"));
            staff.SetStaffData(staffData,Table.FloorType);
            var action=(WaiterAction)Get<IStaffAction>(staff,"_staffAction");
            return (staff, action);
        }
        public void Arrive(Staff staff)
        {
            var callback = Get<Action>(staff, "_moveCompleted");
            Get<GameObject>(staff, "_moveObj").transform.position = Get<Vector2>(staff, "_targetPos");
            callback();
        }
        public IEnumerator BeginEat()
        {
            Manager.OnServing(Table); Manager.StopAllCoroutines();
            Assert.That(Table.TableState, Is.EqualTo(ETableState.Eating));
            return (IEnumerator)Invoke(Manager, "EatRoutine", Table, Food);
        }
        public void Eat()
        {
            var routine = BeginEat(); Assert.That(routine.MoveNext(), Is.True); Assert.That(routine.MoveNext(), Is.True); Assert.That(routine.MoveNext(), Is.False);
        }
        public void AssertBeforeSettlement(int orders)
        {
            Assert.That(UserInfo.GetVisitedCustomerCount(Data.Id), Is.EqualTo(1));
            Assert.That(UserInfo.TotalCumulativeCustomerCount, Is.EqualTo(1)); Assert.That(UserInfo.DailyCumulativeCustomerCount, Is.EqualTo(1)); Assert.That(UserInfo.WeeklyCumulativeCustomerCount, Is.EqualTo(1));
            Assert.That(Coins.CurrentMoney, Is.EqualTo(101 * orders)); Assert.That(UserInfo.GetCookCount(Food.Id), Is.EqualTo(orders));
            Assert.That(Stage.Tip, Is.Zero); Assert.That(Stage.Satisfaction, Is.Zero); Assert.That(Stage.FeverGauge, Is.Zero);
        }
        public void Settle()
        {
            foreach (var delay in waits.GetComponentsInChildren<TweenWait>(true).Where(w => w.enabled && Get<float>(w, "_totalDuration") < .1f).ToArray())
            { var callback = Get<Action>(delay, "_onCompleted"); delay.Stop(); callback(); }
            TakeWait(.5f)();
        }
        public void AssertSettled(int orders)
        {
            Assert.That(Table.CurrentCustomer, Is.Null); Assert.That(Table.TableState, Is.EqualTo(ETableState.NeedCleaning));
            Assert.That(Stage.Tip, Is.EqualTo(50 * orders)); Assert.That(Stage.Satisfaction, Is.EqualTo(2 * orders)); Assert.That(Stage.FeverGauge, Is.EqualTo(20));
            Assert.That(Table.TotalPrice, Is.Zero); Assert.That(Table.TotalTip, Is.Zero);
            Assert.That(Customer.VisitParty, Is.Null); Assert.That(Table.CurrentParty, Is.Null);
            Assert.That(Table.DropGarbageArea.Count, Is.InRange(1, 2));
        }
        public void CleanAndExit()
        {
            Table.TableFurniture.OnCleanAction(); Table.TableFurniture.OnCleanAction();
            Assert.That(Table.TableState, Is.EqualTo(ETableState.Empty)); Assert.That(Stage.GetSinkBowlCount(Table.FloorType), Is.EqualTo(1));
            Get<Action>(Customer, "_moveCompleted")(); Assert.That(Customer.gameObject.activeSelf, Is.False);
        }
        public Action TakeWait(float duration)
        {
            var wait = waits.GetComponentsInChildren<TweenWait>(true).First(w => w.enabled && Mathf.Abs(Get<float>(w, "_totalDuration") - duration) < .0001f);
            var callback = Get<Action>(wait, "_onCompleted"); wait.Stop(); return callback;
        }
        public T Create<T>() where T : Component
        {
            var go = Track(new GameObject("Detached table use " + typeof(T).Name)); go.SetActive(false);
            return typeof(T) == typeof(Transform) ? (T)(Component)go.transform : go.AddComponent<T>();
        }
        public T Track<T>(T value) where T : Object { objects.Add(value); return value; }
        public void Swap(Type type, string name, object value)
        {
            var field = Field(type, name); var prior = field.GetValue(null); restore.Add(() => field.SetValue(null, prior)); field.SetValue(null, value);
        }
        public void Dispose()
        {
            if (Manager != null) { Manager.StopAllCoroutines(); Invoke(Manager, "OnDisable"); }
            if (Controller != null) Invoke(Controller, "OnDisable");
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
            UserInfo.IsTutorialStart = tutorial; UserInfo.IsFirstTutorialClear = tutorialClear; UnityEngine.Random.state = random;
            Assert.That(BackEnd.Backend.IsInitialized || BackEnd.Backend.IsLogin, Is.False);
        }
    }
}
#endif
