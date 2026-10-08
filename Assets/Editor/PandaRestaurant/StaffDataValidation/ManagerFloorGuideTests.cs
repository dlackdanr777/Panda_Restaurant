#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ManagerFloorGuideTests
{
    [TestCase(false, true, ManagerCustomerGuideSource.Action)]
    [TestCase(true, false, ManagerCustomerGuideSource.Action)]
    [TestCase(true, true, ManagerCustomerGuideSource.Action)]
    [TestCase(false, false, ManagerCustomerGuideSource.Action)]
    [TestCase(false, true, ManagerCustomerGuideSource.Skill)]
    [TestCase(true, false, ManagerCustomerGuideSource.Skill)]
    [TestCase(true, true, ManagerCustomerGuideSource.Skill)]
    [TestCase(false, false, ManagerCustomerGuideSource.Skill)]
    public void AssignmentMatrix_OnlyTheAssignedFloorSelectsItsOwnTable(bool first, bool second, ManagerCustomerGuideSource source)
    {
        using (var f = new Fixture())
        {
            f.Assign(ERestaurantFloorType.Floor1, first ? f.First.StaffData : null);
            f.Assign(ERestaurantFloorType.Floor2, second ? f.Second.StaffData : null);
            Assert.That(f.Select(f.First, source, out var firstTable), Is.EqualTo(first));
            Assert.That(firstTable, first ? Is.SameAs(f.FirstTable) : Is.Null);
            Assert.That(f.Select(f.Second, source, out var secondTable), Is.EqualTo(second));
            Assert.That(secondTable, second ? Is.SameAs(f.SecondTable) : Is.Null);
            Assert.That(f.FirstTable.CurrentCustomer, Is.Null);
            Assert.That(f.SecondTable.CurrentCustomer, Is.Null);
        }
    }

    [TestCase(ManagerCustomerGuideSource.Action)]
    [TestCase(ManagerCustomerGuideSource.Skill)]
    public void FullAssignedFloor_NeverFallsBackToOtherFloor(ManagerCustomerGuideSource source)
    {
        using (var f = new Fixture())
        {
            f.FirstTable.TableState = ETableState.Move;
            Assert.That(f.Select(f.First, source, out var table), Is.False);
            Assert.That(table, Is.Null);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("NoTableOnAssignedFloor"));
            Assert.That(f.SecondTable.TableState, Is.EqualTo(ETableState.Empty));
        }
    }

    [TestCase(ManagerCustomerGuideSource.Action)]
    [TestCase(ManagerCustomerGuideSource.Skill)]
    public void MoveAndUnequip_RejectsOldRuntimeEvenBeforeItsEquipEventRuns(ManagerCustomerGuideSource source)
    {
        using (var f = new Fixture())
        {
            StaffData moving = f.First.StaffData;
            f.Assign(ERestaurantFloorType.Floor1, null);
            f.Assign(ERestaurantFloorType.Floor2, moving);
            Set(f.Second, "_staffData", moving);
            f.Register(f.Second);
            Assert.That(f.Select(f.First, source, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("AssignmentChanged"));
            Assert.That(f.Select(f.Second, source, out var table), Is.True);
            Assert.That(table.FloorType, Is.EqualTo(ERestaurantFloorType.Floor2));
            f.Assign(ERestaurantFloorType.Floor2, null);
            Assert.That(f.Select(f.Second, source, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("AssignmentChanged"));
        }
    }

    [Test]
    public void ReplacedSameStaffId_OldRuntimeCannotGuideOrUnregisterReplacement()
    {
        using (var f = new Fixture())
        {
            Staff replacement = f.CreateStaff(ERestaurantFloorType.Floor1, f.First.StaffData);
            f.Register(replacement);
            Assert.That(f.Select(f.First, ManagerCustomerGuideSource.Action, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("StaleRuntime"));
            Invoke(f.Manager, "UnregisterManagerRuntime", ERestaurantFloorType.Floor1, f.First);
            Assert.That(f.Select(replacement, ManagerCustomerGuideSource.Skill, out var table), Is.True);
            Assert.That(table, Is.SameAs(f.FirstTable));
        }
    }

    [Test]
    public void DisabledOrWrongRoleRuntime_CannotGuide()
    {
        using (var f = new Fixture())
        {
            f.First.gameObject.SetActive(false);
            Assert.That(f.Select(f.First, ManagerCustomerGuideSource.Action, out _), Is.False);
            f.First.gameObject.SetActive(true);
            Set(f.First, "_staffType", EquipStaffType.Chef);
            Assert.That(f.Select(f.First, ManagerCustomerGuideSource.Skill, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("InactiveOrInvalidManager"));
        }
    }

    [Test]
    public void Tutorial_DoesNotRedirectSecondFloorManagerIntoFirstFloor()
    {
        using (var f = new Fixture())
        {
            UserInfo.IsFirstTutorialClear = false;
            UserInfo.IsTutorialStart = true;
            Assert.That(f.Select(f.Second, ManagerCustomerGuideSource.Action, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("TutorialFloorRestricted"));
            Assert.That(f.Select(f.First, ManagerCustomerGuideSource.Skill, out var table), Is.True);
            Assert.That(table, Is.SameAs(f.FirstTable));
            object[] manualArgs = { null, (ERestaurantFloorType?)ERestaurantFloorType.Floor2, default(ERestaurantFloorType), null };
            Assert.That((bool)Invoke(f.Manager, "TrySelectCustomerGuideTable", manualArgs), Is.True);
            Assert.That(manualArgs[3], Is.SameAs(f.FirstTable), "Manual/tutorial selection keeps its existing redirect");
        }
    }

    [Test]
    public void LockedOrMislabelledTable_IsRejectedWithoutFallback()
    {
        using (var f = new Fixture())
        {
            Set(f.Stages[0], "_unlockFloor", ERestaurantFloorType.Floor1);
            Assert.That(f.Select(f.Second, ManagerCustomerGuideSource.Action, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("FloorLocked"));
            f.FirstTable.FloorType = ERestaurantFloorType.Floor2;
            Assert.That(f.Select(f.First, ManagerCustomerGuideSource.Action, out _), Is.False);
            Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("TableFloorMismatch"));
            Assert.That(f.Manager.LastManagerGuideDiagnostic.SelectedFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
        }
    }

    [Test]
    public void ActionAndSkill_UseStrictEndpointAndExposeOneCompleteDiagnostic()
    {
        using (var f = new Fixture())
        {
            var action = new ManagerAction(f.Second, f.Manager);
            Set(action, "_actionCoolTime", 0f);
            action.PerformAction(f.Second);
            var d = f.Manager.LastManagerGuideDiagnostic;
            Assert.That(d.Source, Is.EqualTo(ManagerCustomerGuideSource.Action));
            Assert.That(d.StaffId, Is.EqualTo(f.Second.StaffData.Id));
            Assert.That(d.SavedManagerId, Is.EqualTo(d.StaffId));
            Assert.That(d.EquipFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
            Assert.That(d.RequestedFloor, Is.EqualTo(d.EquipFloor));
            Assert.That(d.SelectedFloor, Is.EqualTo(d.EquipFloor));
            Assert.That(d.Result, Is.EqualTo("NoWaitingCustomer"));
            Assert.That(Get<float>(action, "_actionCoolTime"), Is.EqualTo(20f), "The original action cooldown is unchanged");
            var skill = ScriptableObject.CreateInstance<AutoCustomerGuideSkill>();
            try
            {
                skill.ActivateUpdate(f.Second, f.Manager, null, null);
                Assert.That(f.Manager.LastManagerGuideDiagnostic.Source, Is.EqualTo(ManagerCustomerGuideSource.Skill));
                Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("NoWaitingCustomer"));
                f.Assign(ERestaurantFloorType.Floor2, null);
                skill.ActivateUpdate(f.Second, f.Manager, null, null);
                Assert.That(f.Manager.LastManagerGuideDiagnostic.Result, Is.EqualTo("AssignmentChanged"));
                action.Destructor();
                Set(action, "_actionCoolTime", 0f);
                action.PerformAction(f.Second);
                Assert.That(Get<float>(action, "_actionCoolTime"), Is.Zero, "Disposed cooldown work cannot run after reassignment");
            }
            finally { Object.DestroyImmediate(skill); }
        }
    }

    [Test]
    public void SameEmployeeFloorChange_ReleasesOldActionAndRebindsSlotToNewFloor()
    {
        using (var f = new Fixture())
        {
            var lookupField = typeof(StaffDataManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object previousLookup = lookupField.GetValue(null);
            var lookupHost = new GameObject("Detached staff type lookup");
            lookupHost.SetActive(false);
            var data = ScriptableObject.CreateInstance<ManagerFloorRebindProbeData>();
            var oldAction = new ManagerFloorRebindProbeAction();
            try
            {
                lookupField.SetValue(null, lookupHost.AddComponent<StaffDataManager>());
                Set(f.First, "_staffData", data);
                Set(f.First, "_staffAction", oldAction);
                // Stop at AddSlot, after the production reassignment and before unrelated
                // animation/pathfinding setup. The old early-return never reaches this point.
                Assert.Throws<OperationCanceledException>(() => f.First.SetStaffData(data, ERestaurantFloorType.Floor2));
                Assert.That(data.Removed, Is.EqualTo(1));
                Assert.That(oldAction.Disposed, Is.True);
                Assert.That(data.AddedFloor, Is.EqualTo(ERestaurantFloorType.Floor2));
                Assert.That(f.First.EquipFloorType, Is.EqualTo(ERestaurantFloorType.Floor2));
            }
            finally
            {
                lookupField.SetValue(null, previousLookup);
                Set(f.First, "_staffData", null);
                Object.DestroyImmediate(lookupHost);
                Object.DestroyImmediate(data);
            }
        }
    }

    [Test]
    public void SceneReentry_RequiresNewRuntimeBindingAndReadsRestoredFloorAssignment()
    {
        using (var f = new Fixture())
        {
            // The saved per-floor assignment survives; a new scene's TableManager has no old runtime authority.
            TableManager nextScene = f.CreateManager();
            object[] args = { f.Second, ManagerCustomerGuideSource.Action, null };
            Assert.That((bool)Invoke(nextScene, "TrySelectManagerGuideTable", args), Is.False);
            Assert.That(nextScene.LastManagerGuideDiagnostic.Result, Is.EqualTo("StaleRuntime"));
            Staff restored = f.CreateStaff(ERestaurantFloorType.Floor2, UserInfo.GetEquipStaff(EStage.Stage1, ERestaurantFloorType.Floor2, EquipStaffType.Manager));
            Invoke(nextScene, "RegisterManagerRuntime", ERestaurantFloorType.Floor2, restored);
            args[0] = restored;
            Assert.That((bool)Invoke(nextScene, "TrySelectManagerGuideTable", args), Is.True);
            Assert.That(args[2], Is.SameAs(f.SecondTable));
            UserInfo.ChangeStage(EStage.Stage2);
            f.Assign(ERestaurantFloorType.Floor2, restored.StaffData, 1);
            Assert.That((bool)Invoke(nextScene, "TrySelectManagerGuideTable", args), Is.False);
            Assert.That(nextScene.LastManagerGuideDiagnostic.Result, Is.EqualTo("StaleRuntime"));
        }
    }

    private static object Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target);

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("Detached manager floor test");
        private readonly List<Object> created = new List<Object>();
        private readonly FieldInfo stagesField = typeof(UserInfo).GetField("_stageInfos", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object previousStages;
        private readonly EStage previousStage = UserInfo.CurrentStage;
        private readonly bool previousTutorial = UserInfo.IsTutorialStart;
        private readonly bool previousClear = UserInfo.IsFirstTutorialClear;
        private readonly UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
        private readonly FurnitureSystem furniture;
        private readonly CustomerController customers;
        public readonly StageInfo[] Stages = { new StageInfo(), new StageInfo(), new StageInfo() };
        public readonly TableManager Manager;
        public readonly Staff First;
        public readonly Staff Second;
        public readonly TableData FirstTable;
        public readonly TableData SecondTable;

        public Fixture()
        {
            root.SetActive(false);
            previousStages = stagesField.GetValue(null);
            stagesField.SetValue(null, Stages);
            UserInfo.ChangeStage(EStage.Stage1);
            UserInfo.IsTutorialStart = false;
            UserInfo.IsFirstTutorialClear = true;
            furniture = Add<FurnitureSystem>("Furniture");
            customers = Add<CustomerController>("Empty waiting queue");
            FirstTable = Table(ERestaurantFloorType.Floor1);
            SecondTable = Table(ERestaurantFloorType.Floor2);
            Set(furniture, "_furnitureGroupDic", new Dictionary<ERestaurantFloorType, FurnitureGroup>
            {
                { ERestaurantFloorType.Floor1, Group(FirstTable) },
                { ERestaurantFloorType.Floor2, Group(SecondTable) }
            });
            Manager = CreateManager();
            First = CreateStaff(ERestaurantFloorType.Floor1, Data("MANAGER-FLOOR-1"));
            Second = CreateStaff(ERestaurantFloorType.Floor2, Data("MANAGER-FLOOR-2"));
            Assign(ERestaurantFloorType.Floor1, First.StaffData);
            Assign(ERestaurantFloorType.Floor2, Second.StaffData);
            Register(First);
            Register(Second);
        }

        public TableManager CreateManager()
        {
            var manager = Add<TableManager>("Table manager");
            Set(manager, "_furnitureSystem", furniture);
            Set(manager, "_customerController", customers);
            return manager;
        }
        public void Assign(ERestaurantFloorType floor, StaffData data, int stage = 0) =>
            Get<Dictionary<ERestaurantFloorType, Dictionary<EquipStaffType, StaffData>>>(Stages[stage], "_equipStaffTypeDic")[floor][EquipStaffType.Manager] = data;
        public void Register(Staff staff) => Invoke(Manager, "RegisterManagerRuntime", staff.EquipFloorType, staff);
        public bool Select(Staff staff, ManagerCustomerGuideSource source, out TableData table)
        {
            object[] args = { staff, source, null };
            bool result = (bool)Invoke(Manager, "TrySelectManagerGuideTable", args);
            table = (TableData)args[2];
            return result;
        }
        public Staff CreateStaff(ERestaurantFloorType floor, StaffData data)
        {
            var go = new GameObject("Live manager " + floor);
            created.Add(go); // This component alone is active; no scene/customer/pathfinding loop runs.
            var staff = go.AddComponent<Staff>();
            Set(staff, "_staffType", EquipStaffType.Manager);
            Set(staff, "_equipFloorType", floor);
            Set(staff, "_staffData", data);
            return staff;
        }
        private ManagerData Data(string id)
        {
            var data = ScriptableObject.CreateInstance<ManagerData>();
            created.Add(data);
            Set(data, "_id", id);
            var level = new ManagerLevelData();
            Set(level, "_customerGuideTime", 20f);
            Set(data, "_managerLevelData", new[] { level });
            Get<Dictionary<string, SaveStaffData>>(Stages[0], "_giveStaffDic")[id] = new SaveStaffData(id, 1);
            return data;
        }
        private TableData Table(ERestaurantFloorType floor)
        {
            var table = Add<TableData>(floor + " table");
            table.FloorType = floor;
            table.TableState = ETableState.Empty;
            return table;
        }
        private FurnitureGroup Group(TableData table)
        {
            var group = Add<FurnitureGroup>(table.FloorType + " furniture");
            Set(group, "_floorType", table.FloorType);
            Set(group, "_tableDataDic", new Dictionary<TableType, TableData> { { TableType.Table1, table } });
            return group;
        }
        private T Add<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform);
            return go.AddComponent<T>();
        }
        public void Dispose()
        {
            Object.DestroyImmediate(root);
            for (int i = created.Count - 1; i >= 0; --i)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            stagesField.SetValue(null, previousStages);
            UserInfo.ChangeStage(previousStage);
            UserInfo.IsTutorialStart = previousTutorial;
            UserInfo.IsFirstTutorialClear = previousClear;
            Assert.That(UnityEngine.Random.state, Is.EqualTo(previousRandom), "Strict manager routes never randomly choose another floor");
            UnityEngine.Random.state = previousRandom;
        }
    }
}

public sealed class ManagerFloorRebindProbeAction : IStaffAction
{
    public bool Disposed;
    public void Destructor() => Disposed = true;
    public void PerformAction(Staff staff) { }
}

public sealed class ManagerFloorRebindProbeData : ManagerData
{
    public int Removed;
    public ERestaurantFloorType AddedFloor;
    public override void RemoveSlot(Staff staff, TableManager tables, KitchenSystem kitchen, CustomerController customers) => Removed++;
    public override void AddSlot(Staff staff, TableManager tables, KitchenSystem kitchen, CustomerController customers)
    {
        AddedFloor = staff.EquipFloorType;
        throw new OperationCanceledException("Reached new floor slot before visual setup");
    }
}
#endif
