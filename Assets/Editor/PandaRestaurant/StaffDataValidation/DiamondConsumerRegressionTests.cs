#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// Invokes the production UI handlers and real UserInfo wallets/grants. Only popup, animation
// and save presentation are overridden; all managers and account references are detached/restored.
public sealed class DiamondConsumerRegressionTests
{
    [Test]
    public void ShopConsumers_RejectInvalidOrReservedProductsAndGuardActualGrantReentry()
    {
        foreach (string product in new[] { "furniture", "kitchen", "recipe" })
        foreach (string scenario in new[] { "normal", "reserved", "owned", "unregistered", "invalid-currency", "negative-price", "null" })
        using (var scope = new ConsumerScope())
        {
            var furniture = scope.NewData<FurnitureData>("SHOP_FURNITURE");
            var kitchen = scope.NewData<KitchenUtensilData>("SHOP_KITCHEN");
            var recipe = scope.NewData<FoodData>("SHOP_RECIPE");
            scope.Register(furniture, kitchen, recipe);
            var viewFurniture = scope.Create<ConsumerFurnitureView>();
            var viewKitchen = scope.Create<ConsumerKitchenView>();
            var viewRecipe = scope.Create<ConsumerRecipeView>();
            BasicData data = product == "furniture" ? (BasicData)furniture : product == "kitchen" ? (BasicData)kitchen : recipe;
            Action invoke = () =>
            {
                object argument = scenario == "null" ? null : data;
                if (product == "furniture") Invoke(typeof(UIFurniture), viewFurniture, "OnBuyButtonClicked", argument);
                else if (product == "kitchen") Invoke(typeof(UIKitchen), viewKitchen, "OnBuyButtonClicked", argument);
                else Invoke(typeof(UIRecipeTab), viewRecipe, "OnBuyButtonClicked", argument);
            };
            if (scenario == "unregistered") scope.Unregister(product);
            if (scenario == "invalid-currency") SetField(data, "_moneyType", (MoneyType)999);
            if (scenario == "negative-price") SetField(data, "_buyPrice", -10);
            if (scenario == "owned")
            {
                if (product == "furniture") scope.Stage.GiveFurniture(furniture);
                else if (product == "kitchen") scope.Stage.GiveKitchenUtensil(kitchen);
                else UserInfo.GiveRecipe(recipe);
            }
            if (scenario == "reserved") Assert.That(UserInfo.StaffPurchaseWallet.TryReserve(scope, 100, 110, out _), Is.True);
            int grants = 0;
            Action onGrant = () => { grants++; invoke(); };
            scope.Stage.OnGiveFurnitureHandler += onGrant;
            scope.Stage.OnGiveKitchenUtensilHandler += onGrant;
            UserInfo.OnGiveRecipeHandler += onGrant;
            UserInfo.OnChangeDiaHandler += invoke;
            viewFurniture.Reenter = invoke; viewKitchen.Reenter = invoke; viewRecipe.Reenter = invoke;
            invoke();
            int presentations = viewFurniture.Completed + viewKitchen.Completed + viewRecipe.Completed;
            Assert.That(UserInfo.Dia, Is.EqualTo(scenario == "normal" ? 100 : 110), product + "/" + scenario);
            Assert.That(grants, Is.EqualTo(scenario == "normal" ? 1 : 0), product + "/" + scenario);
            Assert.That(presentations, Is.EqualTo(scenario == "normal" ? 1 : 0), product + "/" + scenario);
            Assert.That(UserInfo.Money, Is.EqualTo(1000));
            Assert.That(UserInfo.SkinToken, Is.EqualTo(scope.InitialSkinTokens));
            if (scenario == "normal")
            {
                Assert.That(product == "furniture" ? UserInfo.IsGiveFurniture(EStage.Stage1, data.Id)
                    : product == "kitchen" ? UserInfo.IsGiveKitchenUtensil(EStage.Stage1, data.Id)
                    : UserInfo.IsGiveRecipe(data.Id), Is.True);
                invoke(); // Already-owned objects cannot consume again after the synchronous fence exits.
                Assert.That(UserInfo.Dia, Is.EqualTo(100));
                Assert.That(grants, Is.EqualTo(1));
            }
            scope.AssertRandomUnchanged();
        }
    }

    [Test]
    public void GoldConsumer_RejectsInvalidOverflowAndReservedCostsAndChargesOnceBeforeRealReward()
    {
        foreach (string scenario in new[] { "normal", "reserved", "zero", "negative", "bad-price", "bad-currency", "overflow" })
        using (var scope = new ConsumerScope())
        {
            var view = scope.Create<ConsumerPaymentView>();
            int value = scenario == "zero" ? 0 : scenario == "negative" ? -100 : 100;
            int price = scenario == "bad-price" ? -10 : 10;
            MoneyType type = scenario == "bad-currency" ? (MoneyType)999 : MoneyType.Gold;
            long initial = scenario == "overflow" ? long.MaxValue : 1000;
            scope.ReplaceStatic(typeof(UserInfo), "_money", initial);
            if (scenario == "reserved") Assert.That(UserInfo.StaffPurchaseWallet.TryReserve(scope, 100, 110, out _), Is.True);
            Action invoke = () => view.OnSlotButtonClicked(type, value, price);
            int moneyNotifications = 0;
            UserInfo.OnChangeDiaHandler += invoke;
            UserInfo.OnChangeMoneyHandler += () => { moneyNotifications++; invoke(); };
            view.Reenter = invoke;
            invoke();
            Assert.That(UserInfo.Dia, Is.EqualTo(scenario == "normal" ? 100 : 110), scenario);
            Assert.That(UserInfo.Money, Is.EqualTo(scenario == "normal" ? 1100 : initial), scenario);
            Assert.That(moneyNotifications, Is.EqualTo(scenario == "normal" ? 1 : 0), scenario);
            Assert.That(view.Completed, Is.EqualTo(scenario == "normal" ? 1 : 0), scenario);
            scope.AssertRandomUnchanged();
        }
    }

    [Test]
    public void AdReplacement_RejectsUnavailableOrReservedEffectAndGuardsUsageAndRewardReentry()
    {
        foreach (string scenario in new[] { "normal", "reserved", "maximum-customers", "maximum-uses", "missing-target", "wrong-type" })
        using (var scope = new ConsumerScope())
        {
            var view = scope.Create<ConsumerAdView>();
            var reward = scope.Create<WatchAdButton>();
            SetField(view, "_currentAdType", Enum.Parse(Field(typeof(UIAdPopup), "_currentAdType").FieldType,
                scenario == "wrong-type" ? "None" : "Customer"));
            SetField(view, "_currentWatchAdButton", scenario == "missing-target" ? null : reward);
            SetField(view, "_diaButton", scope.Create<UIButtonAndText>());
            if (scenario == "maximum-customers") SetField(scope.Game, "_maxWaitCustomerCount", 0);
            if (scenario == "maximum-uses") scope.ReplaceStatic(typeof(UserInfo), "_addCustomerDiaCount", ConstValue.AD_CUSTOMER_COUNT);
            if (scenario == "reserved") Assert.That(UserInfo.StaffPurchaseWallet.TryReserve(scope, 100, 110, out _), Is.True);
            int initialUsage = UserInfo.AddCustomerDiaCount, effects = 0;
            Action invoke = () => Invoke(typeof(UIAdPopup), view, "OnDiaButtonClicked");
            reward.OnDiaRewarded += () => { effects++; UserInfo.AddAddCustomerDiaCount(); invoke(); };
            UserInfo.OnChangeDiaHandler += invoke;
            view.Reenter = invoke;
            invoke();
            Assert.That(UserInfo.Dia, Is.EqualTo(scenario == "normal" ? 105 : 110), scenario);
            Assert.That(effects, Is.EqualTo(scenario == "normal" ? 1 : 0), scenario);
            Assert.That(UserInfo.AddCustomerDiaCount, Is.EqualTo(initialUsage + (scenario == "normal" ? 1 : 0)), scenario);
            Assert.That(view.Completed, Is.EqualTo(scenario == "normal" ? 1 : 0), scenario);
            scope.AssertRandomUnchanged();
        }
    }

    private static void Invoke(Type declaringType, object instance, string name, params object[] arguments)
    {
        try { declaringType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments); }
        catch (TargetInvocationException exception) { throw exception.InnerException; }
    }

    [Test]
    public void OtherGachaConsumers_ValidateBeforeChargeAndDoNotRepeatEffectsOnReentry()
    {
        foreach (bool skinMode in new[] { false, true })
        foreach (int count in new[] { 1, 11 })
        foreach (string scenario in new[] { "normal", "reserved", "poor", "null-source", "wrong-type", "failed-draw", "reserve-during-draw" })
        using (var scope = new ConsumerScope())
        {
            var item = scope.Track(ScriptableObject.CreateInstance<GachaItemData>());
            var skin = scope.Track(ScriptableObject.CreateInstance<StaffSkinData>());
            SetField(item, "_id", "ITEM_CONSUMER"); SetField(skin, "_id", "SKIN_CONSUMER");
            SetField(skin, "_duplicationToken", 5);
            typeof(GachaData).GetField("_rank", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, Rank.Normal1);
            typeof(GachaData).GetField("_rank", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(skin, Rank.Normal1);
            scope.ReplaceStatic(typeof(ItemManager), "_instance", scope.Create<ItemManager>());
            scope.ReplaceStatic(typeof(ItemManager), "_gachaItemDataDic", new Dictionary<string, GachaItemData> { [item.Id] = item });
            scope.ReplaceStatic(typeof(SkinDataManager), "_instance", scope.Create<SkinDataManager>());
            scope.ReplaceStatic(typeof(SkinDataManager), "_staffSkinDataDic", new Dictionary<string, StaffSkinData> { [skin.Id] = skin });
            scope.ReplaceStatic(typeof(SkinDataManager), "_customerSkinDataDic", new Dictionary<string, CustomerSkinData>());
            var items = scope.Create<ConsumerItemGachaView>();
            var skins = scope.Create<ConsumerSkinGachaView>();
            GachaData selected = skinMode ? (GachaData)skin : item;
            GachaData wrongType = skinMode ? (GachaData)item : skin;
            var source = scenario == "null-source" ? null : new List<GachaData> { scenario == "wrong-type" ? wrongType : selected };
            items.SetSource(source); skins.SetSource(source);
            if (scenario == "reserved") Assert.That(UserInfo.StaffPurchaseWallet.TryReserve(scope, 100, 110, out _), Is.True);
            if (scenario == "poor") scope.ReplaceStatic(typeof(UserInfo), "_dia", 9);
            int draws = 0;
            bool drawReservationAccepted = false;
            Func<List<GachaData>, GachaData> draw = values =>
            {
                draws++;
                if (scenario == "failed-draw" && draws == (count == 1 ? 1 : 6)) return null;
                if (scenario == "reserve-during-draw" && draws == count)
                    drawReservationAccepted = UserInfo.StaffPurchaseWallet.TryReserve(scope, 100, 110, out _);
                return selected;
            };
            items.Draw = draw; skins.Draw = draw;
            Action single = skinMode ? (Action)skins.OnSingleGachaButtonClicked : items.OnSingleGachaButtonClicked;
            Action multi = skinMode ? (Action)skins.OnTenGachaButtonClicked : items.OnTenGachaButtonClicked;
            Action reenter = () => { single(); multi(); };
            items.Reenter = reenter; skins.Reenter = reenter;
            UserInfo.OnChangeDiaHandler += reenter;
            UserInfo.OnGiveGachaItemHandler += reenter;
            UserInfo.OnGiveStaffSkinHandler += reenter;
            UserInfo.OnUseGachaMachineHandler += reenter;
            if (count == 1) single(); else multi();
            string label = (skinMode ? "skin" : "item") + "/" + count + "/" + scenario;
            int initial = scenario == "poor" ? 9 : 110;
            Assert.That(UserInfo.Dia, Is.EqualTo(scenario == "normal" ? initial - (count == 1 ? 10 : 100) : initial), label);
            Assert.That(items.Completed + skins.Completed, Is.EqualTo(scenario == "normal" ? 1 : 0), label);
            int itemCount = UserInfo.GetGiveGachaItemCountDic().TryGetValue(item.Id, out int ownedCount) ? ownedCount : 0;
            Assert.That(itemCount, Is.EqualTo(scenario == "normal" && !skinMode ? count : 0), label);
            Assert.That(UserInfo.IsGiveStaffSkin(skin.Id), Is.EqualTo(scenario == "normal" && skinMode), label);
            Assert.That(UserInfo.SkinToken, Is.EqualTo(scope.InitialSkinTokens + (scenario == "normal" && skinMode ? (count - 1) * 5 : 0)), label);
            Assert.That((int)Field(typeof(UserInfo), "_totalUseGachaMachineCount").GetValue(null), Is.Zero,
                "Count/save/animation are the isolated presentation callback; its count argument is checked below.");
            if (scenario == "normal")
            {
                Assert.That(draws, Is.EqualTo(count), label);
                Assert.That(skinMode ? skins.LastResultCount : items.LastResultCount, Is.EqualTo(count), label);
            }
            if (scenario == "reserved" || scenario == "poor" || scenario == "null-source" || scenario == "wrong-type")
                Assert.That(draws, Is.Zero, label);
            if (scenario == "failed-draw") Assert.That(draws, Is.EqualTo(count == 1 ? 1 : 6), label);
            if (scenario == "reserve-during-draw") Assert.That(drawReservationAccepted, Is.True, label);
            scope.AssertRandomUnchanged();
        }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }

    private static void SetField(object instance, string name, object value) => Field(instance.GetType(), name).SetValue(instance, value);

    private sealed class ConsumerScope : IDisposable
    {
        private readonly List<Action> _restore = new List<Action>();
        private readonly List<Object> _created = new List<Object>();
        private readonly UnityEngine.Random.State _random = UnityEngine.Random.state;
        public StageInfo Stage { get; }
        public GameManager Game { get; }
        public int InitialSkinTokens { get; }

        public ConsumerScope()
        {
            InitialSkinTokens = UserInfo.SkinToken;
            ReplaceStatic(typeof(Muks.DataBind.DataBind), "_textBindDic", new Muks.DataBind.DataBindContainer<string>());
            foreach (FieldInfo field in typeof(UserInfo).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) ReplaceStatic(typeof(UserInfo), field.Name, null);
            foreach (string name in new[] { "_dia", "_score", "_addCustomerDiaCount", "_feverDiaCount", "_staffCostCommitDepth", "_totalUseGachaMachineCount" })
                ReplaceStatic(typeof(UserInfo), name, name == "_dia" ? 110 : 0);
            ReplaceStatic(typeof(UserInfo), "_skinToken", InitialSkinTokens);
            foreach (string name in new[] { "_money", "_totalAddMoney", "_dailyAddMoney", "_weeklyAddMoney" })
                ReplaceStatic(typeof(UserInfo), name, name == "_money" ? 1000L : 0L);
            ReplaceStatic(typeof(UserInfo), "_currentStage", EStage.Stage1);
            ReplaceStatic(typeof(UserInfo), "_giveRecipeLevelDic", new Dictionary<string, int>());
            ReplaceStatic(typeof(UserInfo), "_giveGachaItemCountDic", new Dictionary<string, int>());
            ReplaceStatic(typeof(UserInfo), "_giveGachaItemLevelDic", new Dictionary<string, int>());
            foreach (string name in new[] { "_giveStaffSkinSet", "_giveCustomerSkinSet", "_notificationMessageSet" })
                ReplaceStatic(typeof(UserInfo), name, new HashSet<string>());
            Stage = new StageInfo();
            ReplaceStatic(typeof(UserInfo), "_stageInfos", new[] { Stage, new StageInfo(), new StageInfo() });
            object wallet = UserInfo.StaffPurchaseWallet;
            foreach (FieldInfo field in wallet.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsInitOnly) continue;
                object original = field.GetValue(wallet);
                _restore.Add(() => field.SetValue(wallet, original));
                field.SetValue(wallet, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
            Game = Create<GameManager>();
            ReplaceStatic(typeof(GameManager), "_instance", Game);
            ReplaceStatic(typeof(CustomerController), "_callCustomers", new List<NormalCustomer>());
            ReplaceStatic(typeof(FurnitureDataManager), "_instance", Create<FurnitureDataManager>());
            ReplaceStatic(typeof(KitchenUtensilDataManager), "_instance", Create<KitchenUtensilDataManager>());
            ReplaceStatic(typeof(FoodDataManager), "_instance", Create<FoodDataManager>());
        }

        public T Create<T>() where T : Component
        {
            var go = new GameObject("Inactive detached diamond consumer " + typeof(T).Name);
            go.SetActive(false);
            _created.Add(go);
            return go.AddComponent<T>();
        }

        public T Track<T>(T value) where T : Object { _created.Add(value); return value; }

        public T NewData<T>(string id) where T : BasicData
        {
            var data = ScriptableObject.CreateInstance<T>();
            _created.Add(data);
            SetField(data, "_id", id); SetField(data, "_moneyType", MoneyType.Dia);
            SetField(data, "_buyScore", 0); SetField(data, "_buyPrice", 10);
            if (data is FurnitureData || data is KitchenUtensilData) SetField(data, "_setId", "DETACHED_SET");
            return data;
        }

        public void Register(FurnitureData furniture, KitchenUtensilData kitchen, FoodData recipe)
        {
            ReplaceStatic(typeof(FurnitureDataManager), "_furnitureDataList", new List<FurnitureData> { furniture });
            ReplaceStatic(typeof(FurnitureDataManager), "_furnitureDataDic", new Dictionary<string, FurnitureData> { [furniture.Id] = furniture });
            ReplaceStatic(typeof(KitchenUtensilDataManager), "_kitchenUtensilDataList", new List<KitchenUtensilData> { kitchen });
            ReplaceStatic(typeof(KitchenUtensilDataManager), "_kitchenUtensilDataDic", new Dictionary<string, KitchenUtensilData> { [kitchen.Id] = kitchen });
            ReplaceStatic(typeof(FoodDataManager), "_foodDataList", new List<FoodData> { recipe });
            ReplaceStatic(typeof(FoodDataManager), "_foodDataDic", new Dictionary<string, FoodData> { [recipe.Id] = recipe });
        }

        public void Unregister(string product)
        {
            if (product == "furniture") ReplaceStatic(typeof(FurnitureDataManager), "_furnitureDataList", new List<FurnitureData>());
            else if (product == "kitchen") ReplaceStatic(typeof(KitchenUtensilDataManager), "_kitchenUtensilDataList", new List<KitchenUtensilData>());
            else ReplaceStatic(typeof(FoodDataManager), "_foodDataList", new List<FoodData>());
        }

        public void ReplaceStatic(Type type, string name, object value)
        {
            FieldInfo field = Field(type, name);
            object original = field.GetValue(null);
            _restore.Add(() => field.SetValue(null, original));
            field.SetValue(null, value);
        }

        public void AssertRandomUnchanged() => Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(JsonUtility.ToJson(_random)));

        public void Dispose()
        {
            for (int index = _created.Count - 1; index >= 0; index--) if (_created[index] != null) Object.DestroyImmediate(_created[index]);
            for (int index = _restore.Count - 1; index >= 0; index--) _restore[index]();
            UnityEngine.Random.state = _random;
        }
    }

    public sealed class ConsumerFurnitureView : UIFurniture
    {
        public int Completed; public Action Reenter;
        protected override void ShowPurchaseRejected(string error) { }
        protected override void CompletePurchasePresentation() { Completed++; Reenter?.Invoke(); }
    }
    public sealed class ConsumerKitchenView : UIKitchen
    {
        public int Completed; public Action Reenter;
        protected override void ShowPurchaseRejected(string error) { }
        protected override void CompletePurchasePresentation() { Completed++; Reenter?.Invoke(); }
    }
    public sealed class ConsumerRecipeView : UIRecipeTab
    {
        public int Completed; public Action Reenter;
        protected override void ShowPurchaseRejected(string error) { }
        protected override void CompletePurchasePresentation() { Completed++; Reenter?.Invoke(); }
    }
    public sealed class ConsumerPaymentView : UIPayment
    {
        public int Completed; public Action Reenter;
        protected override void ShowPurchaseRejected(string error) { }
        protected override void CompleteGoldPurchasePresentationAndSave(MoneyType type, int value, int price) { Completed++; Reenter?.Invoke(); }
    }
    public sealed class ConsumerAdView : UIAdPopup
    {
        public int Completed; public Action Reenter;
        protected override void ShowDiaPurchaseRejected(string error) { }
        protected override void CompleteDiaPurchasePresentation() { Completed++; Reenter?.Invoke(); }
    }
    public sealed class ConsumerItemGachaView : UIItemGacha
    {
        public Func<List<GachaData>, GachaData> Draw; public Action Reenter;
        public int Completed, LastResultCount;
        public void SetSource(List<GachaData> source) => _itemDataList = source;
        protected override GachaData DrawPurchaseResult(List<GachaData> source) => Draw(source);
        protected override void PreparePurchasePresentation() { }
        protected override void CompletePurchasePresentationAndSave(int count) { Completed++; LastResultCount = count; Reenter?.Invoke(); }
        protected override void ReportPurchaseError(string error) { }
    }
    public sealed class ConsumerSkinGachaView : UISkinGacha
    {
        public Func<List<GachaData>, GachaData> Draw; public Action Reenter;
        public int Completed, LastResultCount;
        public void SetSource(List<GachaData> source) => _itemDataList = source;
        protected override GachaData DrawPurchaseResult(List<GachaData> source) => Draw(source);
        protected override void PreparePurchasePresentation() { }
        protected override void CompletePurchasePresentationAndSave(int count) { Completed++; LastResultCount = count; Reenter?.Invoke(); }
        protected override void ReportPurchaseError(string error) { }
    }
}
#endif
