#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// Detached scene objects exercise the same selection and rejection methods used by the
// real guide buttons. No SDK, account initialization, resources, or pathfinding is started.
public class FirstTutorialCustomerFloorTests
{
    [Test]
    public void ActiveFirstTutorial_UsesFloor1TableForAutomaticAndExplicitGuides()
    {
        using (var fixture = new Fixture())
        {
            var randomBefore = UnityEngine.Random.state;
            foreach (ERestaurantFloorType? requested in new ERestaurantFloorType?[]
                     { null, ERestaurantFloorType.Floor2, ERestaurantFloorType.Floor1 })
            {
                Assert.That(fixture.Select(requested, out var floor, out var table), Is.True);
                Assert.That(floor, Is.EqualTo(ERestaurantFloorType.Floor1));
                Assert.That(table, Is.SameAs(fixture.FirstTable));
                Assert.That(table.FloorType, Is.EqualTo(floor));
                Assert.That(table.ChairTrs[0], Is.SameAs(fixture.FirstChair),
                    "OnCustomerGuide uses this selected table's chair as the movement destination");
                Assert.That(table.ChairTrs[0].position.y, Is.Not.EqualTo(fixture.SecondTable.ChairTrs[0].position.y));
            }
            Assert.That(UnityEngine.Random.state, Is.EqualTo(randomBefore), "The tutorial must not roll a different floor");
            Assert.That(fixture.Customer.WaitCount, Is.EqualTo(1));
            Assert.That(fixture.FirstTable.CurrentCustomer, Is.Null, "Selecting a destination does not move or dequeue yet");
            Assert.That(fixture.SecondTable.CurrentCustomer, Is.Null);
            fixture.AssertUnlocksPreserved();
        }
    }

    [Test]
    public void FullFirstFloor_RejectsBothActualGuideRoutesAndKeepsTutorialWaiting()
    {
        using (var fixture = new Fixture())
        {
            fixture.FirstTable.TableState = ETableState.Move;
            int completedGuides = 0;
            fixture.Customer.OnGuideCustomerHandler += () => completedGuides++;
            var randomBefore = UnityEngine.Random.state;

            Assert.That(fixture.Select(null, out var floor, out var unavailable), Is.False);
            Assert.That(floor, Is.EqualTo(ERestaurantFloorType.Floor1));
            Assert.That(unavailable, Is.Null);
            Assert.That(fixture.Manager.OnCustomerGuideEvent(0), Is.False);
            Assert.That(fixture.Manager.OnCustomerGuideEvent(ERestaurantFloorType.Floor2, 0), Is.False);
            Assert.That(fixture.Manager.OnCustomerGuideEventPlayUISound(0), Is.False);
            Invoke(fixture.Tutorial, "OnCustomerGuideButtonClicked");

            Assert.That(fixture.Tutorial.IsButtonClicked, Is.False, "A failed guide must not advance the tutorial");
            Assert.That(fixture.TutorialGuide.gameObject.activeSelf, Is.True);
            Assert.That(fixture.MainGuide.gameObject.activeSelf, Is.False, "Floor2 availability does not enable the guide");
            Assert.That(fixture.Customer.WaitCount, Is.EqualTo(1));
            Assert.That(completedGuides, Is.Zero);
            Assert.That(fixture.FirstTable.TableState, Is.EqualTo(ETableState.Move));
            Assert.That(fixture.SecondTable.TableState, Is.EqualTo(ETableState.Empty));
            Assert.That(fixture.FirstTable.CurrentCustomer, Is.Null);
            Assert.That(fixture.SecondTable.CurrentCustomer, Is.Null);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(randomBefore));

            fixture.FirstTable.TableState = ETableState.Empty;
            fixture.Manager.UpdateTable();
            Assert.That(fixture.MainGuide.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Select(null, out floor, out var available), Is.True);
            Assert.That(available, Is.SameAs(fixture.FirstTable), "Existing wait/guide flow becomes available again");
            fixture.AssertUnlocksPreserved();
        }
    }

    [Test]
    public void CompletedOrSkippedAndOutsideFirstTutorial_PreserveExistingUnlockedFloorPolicy()
    {
        using (var fixture = new Fixture())
        {
            fixture.FirstTable.TableState = ETableState.Move;
            // Normal completion and Skip both commit Clear=true and close IsTutorialStart.
            // Also cover a later tutorial and a different Stage without changing saved unlocks.
            foreach (var state in new[]
                     {
                         (EStage.Stage1, true, false),
                         (EStage.Stage1, true, true),
                         (EStage.Stage1, false, false),
                         (EStage.Stage2, false, true)
                     })
            {
                UserInfo.ChangeStage(state.Item1);
                UserInfo.IsFirstTutorialClear = state.Item2;
                UserInfo.IsTutorialStart = state.Item3;
                foreach (ERestaurantFloorType? requested in new ERestaurantFloorType?[] { null, ERestaurantFloorType.Floor2 })
                {
                    Assert.That(fixture.Select(requested, out var floor, out var table), Is.True);
                    Assert.That(floor, Is.EqualTo(ERestaurantFloorType.Floor2));
                    Assert.That(table, Is.SameAs(fixture.SecondTable));
                }
                fixture.Manager.UpdateTable();
                Assert.That(fixture.MainGuide.gameObject.activeSelf, Is.True);
                fixture.AssertUnlocksPreserved();
            }
        }
    }

    private static object Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root;
        private readonly List<UnityEngine.Object> dataObjects = new List<UnityEngine.Object>();
        private readonly List<(FieldInfo field, object prior)> globals = new List<(FieldInfo, object)>();
        private readonly EStage priorStage = UserInfo.CurrentStage;
        private readonly bool priorStarted = UserInfo.IsTutorialStart;
        private readonly bool priorClear = UserInfo.IsFirstTutorialClear;
        private readonly UnityEngine.Random.State priorRandom = UnityEngine.Random.state;
        private readonly StageInfo[] stages;
        private readonly NormalCustomer waiting;
        public readonly TableManager Manager;
        public readonly CustomerController Customer;
        public readonly TableData FirstTable;
        public readonly TableData SecondTable;
        public readonly Transform FirstChair;
        public readonly Button MainGuide;
        public readonly Button TutorialGuide;
        public readonly UITutorial Tutorial;

        public Fixture()
        {
            root = new GameObject("Detached first tutorial customer floor test");
            root.SetActive(false); // No Awake/Start/player loop or scene initialization.
            stages = new[] { new StageInfo(), new StageInfo(), new StageInfo() };
            Swap(typeof(UserInfo), "_stageInfos", stages);
            UserInfo.ChangeStage(EStage.Stage1);
            UserInfo.IsFirstTutorialClear = false;
            UserInfo.IsTutorialStart = true;

            Customer = Add<CustomerController>("Waiting customers");
            waiting = Add<NormalCustomer>("Unassigned normal customer");
            Set(Customer, "_waitCustomers", new List<NormalCustomer> { waiting });
            Manager = Add<TableManager>("Tables");
            var furniture = Add<FurnitureSystem>("Furniture");
            FirstTable = Table(ERestaurantFloorType.Floor1, 0);
            SecondTable = Table(ERestaurantFloorType.Floor2, 20);
            FirstChair = FirstTable.ChairTrs[0];
            var firstGroup = Group(FirstTable);
            var secondGroup = Group(SecondTable);
            Set(furniture, "_furnitureGroupDic", new Dictionary<ERestaurantFloorType, FurnitureGroup>
            {
                { ERestaurantFloorType.Floor1, firstGroup }, { ERestaurantFloorType.Floor2, secondGroup }
            });
            MainGuide = Add<Button>("Main guide");
            Set(Manager, "_customerController", Customer);
            Set(Manager, "_furnitureSystem", furniture);
            Set(Manager, "_guideButton", MainGuide);
            TutorialGuide = Add<Button>("Tutorial guide");
            Tutorial = Add<UITutorial>("Tutorial view");
            Set(Tutorial, "_tableManager", Manager);
            Set(Tutorial, "_customerGuideButton", TutorialGuide);

            // Provide only in-memory food/visit inputs needed by the unchanged normal
            // weighted-floor selector; never initialize the real resource managers.
            var foodManager = Add<FoodDataManager>("Detached food lookup");
            var food = ScriptableObject.CreateInstance<FoodData>();
            dataObjects.Add(food);
            Set(food, "_id", "FOOD-FLOOR-TEST");
            Set(food, "_foodType", FoodType.Cozy);
            Swap(typeof(FoodDataManager), "_instance", foodManager);
            Swap(typeof(FoodDataManager), "_foodDataDic", new Dictionary<string, FoodData> { { food.Id, food } });
            Swap(typeof(UserInfo), "_giveRecipeLevelDic", new Dictionary<string, int> { { food.Id, 1 } });
            var customerData = ScriptableObject.CreateInstance<NormalCustomerData>();
            dataObjects.Add(customerData);
            Set(customerData, "_id", "CUSTOMER-FLOOR-TEST");
            Set(customerData, "_requiredDish", food.Id);
            Set(customerData, "_visitCountFoodDic", new Dictionary<int, string>());
            Set(waiting, "_normalCustomerData", customerData);
            Swap(typeof(UserInfo), "_enabledCustomerDic", new Dictionary<string, SaveCustomerData>
            {
                { customerData.Id, new SaveCustomerData(customerData.Id, "", 0) }
            });
        }

        public bool Select(ERestaurantFloorType? requested, out ERestaurantFloorType floor, out TableData table)
        {
            object[] arguments = { waiting, requested, default(ERestaurantFloorType), null };
            bool result = (bool)Invoke(Manager, "TrySelectCustomerGuideTable", arguments);
            floor = (ERestaurantFloorType)arguments[2];
            table = (TableData)arguments[3];
            return result;
        }

        public void AssertUnlocksPreserved()
        {
            foreach (var stage in stages)
                Assert.That(stage.UnlockFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
        }

        private TableData Table(ERestaurantFloorType floor, float y)
        {
            var table = Add<TableData>(floor + " table");
            table.FloorType = floor;
            table.TableState = ETableState.Empty;
            var chair = Add<Transform>(floor + " chair");
            chair.position = new Vector3(1, y, 0);
            Set(table, "_chairTrs", new[] { chair });
            return table;
        }

        private FurnitureGroup Group(TableData table)
        {
            var group = Add<FurnitureGroup>(table.FloorType + " group");
            Set(group, "_floorType", table.FloorType);
            Set(group, "_foodType", FoodType.Cozy);
            Set(group, "_tableDataDic", new Dictionary<TableType, TableData> { { TableType.Table1, table } });
            return group;
        }

        private T Add<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            return typeof(T) == typeof(Transform) ? (T)(Component)go.transform : go.AddComponent<T>();
        }

        private void Swap(Type type, string name, object value)
        {
            var field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            globals.Add((field, field.GetValue(null)));
            field.SetValue(null, value);
        }

        private static void Set(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        public void Dispose()
        {
            for (int i = globals.Count - 1; i >= 0; i--)
                globals[i].field.SetValue(null, globals[i].prior);
            UserInfo.ChangeStage(priorStage);
            UserInfo.IsTutorialStart = priorStarted;
            UserInfo.IsFirstTutorialClear = priorClear;
            UnityEngine.Random.state = priorRandom;
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var data in dataObjects) UnityEngine.Object.DestroyImmediate(data);
        }
    }
}
#endif
