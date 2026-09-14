#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BackEnd;
using Muks.BackEnd;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Share the real Backend boundary and original-state assertions with the raw collection tests.
public partial class StaffStageMigrationCollectionTests
{
    [Test]
    public void MigrationPreparation_WaitsForActualLastApplicationAndReturnsImmutableCurrentEvidence()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var fixture = CreateRuntimeFixture();
            HoldMigrationForPreparationObservation(fixture);
            AssertPreparation(fixture.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.NotStarted);
            fixture.Manager.LoadAllStageData(true);
            AssertPreparation(fixture.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.RawNotReady);
            int lastApplyObservations = 0;
            fixture.Stage.BeforeApply = stage =>
            {
                if (stage != EStage.Stage3) return;
                lastApplyObservations++;
                Assert.That(fixture.Manager.StageMigrationCollection.Result.MigrationCandidate, Is.Not.Null);
                Assert.That(fixture.Manager.StageMigrationCollection.CompletedStageCount, Is.EqualTo(3));
                AssertPreparation(fixture.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.ApplyInProgress);
            };
            ReplyRuntimeRows(fixture);
            Assert.That(lastApplyObservations, Is.EqualTo(1));
            var valid = fixture.Manager.PrepareStaffMigration();
            AssertPreparation(valid, StaffMigrationPreparationStatus.Valid);
            var preparation = valid.Preparation;
            Assert.That(preparation.Query, Is.SameAs(fixture.Manager.StageMigrationCollection.Query));
            Assert.That(preparation.Target.AccountInDate, Is.EqualTo(fixture.Account));
            Assert.That(preparation.Target.RowInDate, Is.EqualTo(fixture.Row));
            Assert.That(preparation.Round, Is.EqualTo(fixture.Manager.StageMigrationCollection.Round));
            Assert.That(preparation.Candidate, Is.SameAs(fixture.Manager.StageMigrationCollection.Result.MigrationCandidate));
            Assert.That(preparation.Candidate.PandaTokens, Is.Zero);
            CollectionAssert.AreEqual(new[] { "STAFF01", "STAFF03" }, preparation.Candidate.Staff.Select(staff => staff.Id));
            CollectionAssert.AreEqual(new[] { 2, 4 }, preparation.Candidate.Staff.Select(staff => staff.Level));
            Assert.That(preparation.Stages.Count, Is.EqualTo(3));
            foreach (var evidence in preparation.Stages)
            {
                Assert.That(evidence.RowInDate, Is.EqualTo(fixture.Row + "-" + evidence.Stage));
                Assert.That(evidence.Snapshot.HasSameState(fixture.Stage.Runtime[evidence.Stage].CaptureStaffRuntimeSnapshot()), Is.True);
                Assert.That(evidence.Snapshot.IsApplying, Is.False);
            }
            Assert.That(fixture.Manager.StageMigrationCollection.Applications.All(item => item.Status == StaffStageApplicationStatus.Succeeded), Is.True);
            AssertPreparation(fixture.Manager.RevalidateStaffMigration(preparation), StaffMigrationPreparationStatus.Valid);
            Assert.That(fixture.Game.Writes, Is.Zero);
            Assert.That(fixture.Stage.A2Saves, Is.Zero);
            Assert.That(fixture.Manager.GameDataRestoreResult.Status, Is.EqualTo(GameDataRestoreStatus.MigrationRequired));
            Assert.That(fixture.Manager.RestoredGameData, Is.Null, "A preparation is not a stored common account or Ready state");
        }
    }

    [Test]
    public void MigrationPreparation_RealApplyFailuresReentrancyAndSessionChangesNeverAuthorizePartialState()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string mode in new[] { "false", "throw", "partial", "source-mismatch", "generation" })
            {
                var fixture = CreateRuntimeFixture();
                fixture.Stage.RuntimeApply = (stage, data) =>
                {
                    if (stage != EStage.Stage2) return fixture.Stage.Runtime[stage].LoadData(data);
                    if (mode == "false") return fixture.Stage.Runtime[stage].LoadData(null);
                    if (mode == "throw") throw new InvalidOperationException("Detached legacy application failed");
                    if (mode == "partial") data.GiveStaffList.Add(Staff("", 1));
                    if (mode == "source-mismatch") data.GiveStaffList[0].LevelUp();
                    bool loaded = fixture.Stage.Runtime[stage].LoadData(data);
                    if (mode == "generation") fixture.Manager.InvalidateGameDataRestore();
                    return loaded;
                };
                fixture.Manager.LoadAllStageData(true);
                ReplyRuntimeRows(fixture);
                var expected = mode == "generation" ? StaffMigrationPreparationStatus.Invalidated
                    : mode == "source-mismatch" ? StaffMigrationPreparationStatus.SourceMismatch : StaffMigrationPreparationStatus.ApplyFailed;
                AssertPreparation(fixture.Manager.PrepareStaffMigration(), expected);
                Assert.That(fixture.Stage.Runtime[EStage.Stage2].IsApplying, Is.False, "LoadData finally must close its actual application fence");
                if (mode == "partial")
                {
                    Assert.That(fixture.Manager.StageMigrationCollection.Applications[1].Status, Is.EqualTo(StaffStageApplicationStatus.Failed));
                    Assert.That(fixture.Stage.Runtime[EStage.Stage2].CaptureStaffRuntimeSnapshot().Staff.Single().Id, Is.EqualTo("STAFF03"),
                        "A partial detached load is not rolled back or relabeled as fully applied");
                }
                Assert.That(fixture.Game.Writes, Is.Zero);
                Assert.That(fixture.Stage.A2Saves, Is.Zero);
            }

            // Observe the actual StageInfo.IsApplying fence from an event emitted inside its real LoadData body.
            var applying = CreateRuntimeFixture();
            HoldMigrationForPreparationObservation(applying);
            int insideLoad = 0;
            applying.Stage.Runtime[EStage.Stage3].OnChangeFurnitureHandler += (_, __) =>
            {
                insideLoad++;
                Assert.That(applying.Stage.Runtime[EStage.Stage3].CaptureStaffRuntimeSnapshot().IsApplying, Is.True);
                AssertPreparation(applying.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.ApplyInProgress);
            };
            applying.Stage.RuntimeApply = (stage, data) =>
            {
                if (stage == EStage.Stage3)
                {
                    data.GiveFurnitureList.Add(scope.Furniture.Id);
                    data.EquipFurnitureList.Add(new List<string> { scope.Furniture.Id });
                }
                return applying.Stage.Runtime[stage].LoadData(data);
            };
            applying.Manager.LoadAllStageData(true);
            ReplyRuntimeRows(applying);
            Assert.That(insideLoad, Is.EqualTo(1));
            AssertPreparation(applying.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.Valid);

            // Reentrant observation is rejected, including the outer call; it does not reuse a partially built preparation.
            StaffMigrationPreparationResult nested = null;
            bool entered = false;
            applying.Stage.RuntimeReader = stage =>
            {
                if (!entered) { entered = true; nested = applying.Manager.PrepareStaffMigration(); }
                return applying.Stage.Runtime[stage].CaptureStaffRuntimeSnapshot();
            };
            AssertPreparation(applying.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.ReentrantValidation);
            AssertPreparation(nested, StaffMigrationPreparationStatus.ReentrantValidation);
            applying.Stage.RuntimeReader = null;
            AssertPreparation(applying.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.Valid);
        }
    }

    [Test]
    public void MigrationPreparation_ActualStaffMutatorsReapplicationAndObjectReplacementInvalidatePriorEvidence()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string mutation in new[] { "give", "upgrade", "skin", "reapply", "replacement" })
            {
                var fixture = CompletedRuntimeFixture(skin: "ORIGINAL_SKIN");
                var preparation = fixture.Manager.PrepareStaffMigration().Preparation;
                Assert.That(preparation, Is.Not.Null);
                string before = DescribePreparation(preparation);
                StageInfo stage = fixture.Stage.Runtime[EStage.Stage1];
                switch (mutation)
                {
                    case "give": stage.GiveStaff(scope.Staff("STAFF02")); break;
                    case "upgrade": Assert.That(stage.UpgradeStaff(scope.Staff("STAFF01")), Is.True); break;
                    case "skin": stage.SetStaffSkin(scope.Staff("STAFF01"), (StaffSkinData)null); break;
                    case "reapply": Assert.That(stage.LoadData(stage.SaveData()), Is.True); break;
                    case "replacement":
                        var replacement = new StageInfo();
                        Assert.That(replacement.LoadData(stage.SaveData()), Is.True);
                        fixture.Stage.Runtime[EStage.Stage1] = replacement;
                        break;
                }
                AssertPreparation(fixture.Manager.RevalidateStaffMigration(preparation), StaffMigrationPreparationStatus.RuntimeChanged);
                AssertPreparation(fixture.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.RuntimeChanged);
                Assert.That(DescribePreparation(preparation), Is.EqualTo(before), "Old immutable evidence must not silently refresh");
                var current = fixture.Stage.Runtime[EStage.Stage1].CaptureStaffRuntimeSnapshot();
                if (mutation == "give") Assert.That(current.Staff.Any(staff => staff.Id == "STAFF02" && staff.Level == 1), Is.True);
                if (mutation == "upgrade") Assert.That(current.Staff.Single(staff => staff.Id == "STAFF01").Level, Is.EqualTo(3));
                if (mutation == "skin") Assert.That(current.Staff.Single().SkinId, Is.Empty);
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
        }
    }

    [Test]
    public void MigrationPreparation_SaveCopiesPreserveSourcesWhileNoOpsAndUnrelatedStateRemainValid()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (bool changeLevel in new[] { true, false })
            {
                var fixture = CompletedRuntimeFixture(skin: "ORIGINAL_SKIN");
                var preparation = fixture.Manager.PrepareStaffMigration().Preparation;
                string original = DescribePreparation(preparation);
                var alias = fixture.Stage.Runtime[EStage.Stage1].SaveData().GiveStaffList.Single();
                if (changeLevel) alias.LevelUp(); else alias.SetSkinId("ALIAS_CHANGED_SKIN");
                AssertPreparation(fixture.Manager.RevalidateStaffMigration(preparation), StaffMigrationPreparationStatus.Valid);
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].GetStaffLevel("STAFF01"), Is.EqualTo(2));
                Assert.That(fixture.Stage.Runtime[EStage.Stage1].SaveData().GiveStaffList.Single().SkinId, Is.EqualTo("ORIGINAL_SKIN"),
                    "SaveData returns detached records: changing the exported copy must not mutate protected Stage ownership");
                Assert.That(DescribePreparation(preparation), Is.EqualTo(original));
                Assert.That(preparation.Stages[0].Snapshot.Staff.Single().Level, Is.EqualTo(2));
                Assert.That(preparation.Stages[0].Snapshot.Staff.Single().SkinId, Is.EqualTo("ORIGINAL_SKIN"));
                Assert.That(preparation.Candidate.Staff[0].Level, Is.EqualTo(2));
            }
            var unchanged = CompletedRuntimeFixture(level1: 5);
            var stable = unchanged.Manager.PrepareStaffMigration().Preparation;
            Assert.That(stable, Is.Not.Null);
            StageInfo stage = unchanged.Stage.Runtime[EStage.Stage1];
            var before = stage.CaptureStaffRuntimeSnapshot();
            stage.GiveStaff(scope.Staff("STAFF01"));
            LogAssert.Expect(LogType.Error, "레벨 초과: STAFF01");
            Assert.That(stage.UpgradeStaff(scope.Staff("STAFF01")), Is.False);
            stage.SetStaffSkin(scope.Staff("STAFF01"), (StaffSkinData)null);
            Assert.That(stage.LoadData(null), Is.False);
            stage.GetTableData(ERestaurantFloorType.Floor1, (TableType)0).CoinAreaDatas[0].SetMoney(123);
            stage.GetTableData(ERestaurantFloorType.Floor1, (TableType)0).CoinAreaDatas[0].SetCoinCount(2);
            stage.GiveFurniture(scope.Furniture);
            stage.SetEquipFurniture(ERestaurantFloorType.Floor1, scope.Furniture);
            Assert.That(stage.GetGiveFurnitureCount(), Is.EqualTo(1));
            Assert.That(stage.GetEquipFurniture(ERestaurantFloorType.Floor1, scope.Furniture.Type), Is.SameAs(scope.Furniture));
            Assert.That(before.HasSameState(stage.CaptureStaffRuntimeSnapshot()), Is.True);
            AssertPreparation(unchanged.Manager.RevalidateStaffMigration(stable), StaffMigrationPreparationStatus.Valid);
            Assert.That(unchanged.Game.Writes, Is.Zero);
            Assert.That(unchanged.Stage.A2Saves, Is.Zero);
        }
    }

    [Test]
    public void MigrationPreparation_CurrentCatalogAndRuntimeObservationCannotBeReplacedWithOldOrReentrantEvidence()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            foreach (string catalogChange in new[] { "missing", "duplicate", "unavailable", "maximum", "throw" })
            {
                var fixture = CompletedRuntimeFixture();
                var preparation = fixture.Manager.PrepareStaffMigration().Preparation;
                string prior = DescribePreparation(preparation);
                var catalog = fixture.Game.Catalog.ToArray();
                if (catalogChange == "missing") fixture.Game.Catalog = catalog.Where(staff => staff.Id != "STAFF02").ToArray();
                if (catalogChange == "duplicate") fixture.Game.Catalog = catalog.Concat(new[] { catalog[0] }).ToArray();
                if (catalogChange == "unavailable") fixture.Game.Catalog = null;
                if (catalogChange == "maximum")
                {
                    StaffData changed = scope.CloneWithChangedMaximum(scope.Staff("STAFF01"));
                    fixture.Game.Catalog = catalog.Select(staff => staff.Id == changed.Id ? changed : staff).ToArray();
                }
                if (catalogChange == "throw") fixture.Game.CatalogReader = () => throw new InvalidOperationException("Catalog unavailable");
                var expected = catalogChange == "missing" || catalogChange == "maximum"
                    ? StaffMigrationPreparationStatus.CatalogChanged : StaffMigrationPreparationStatus.CatalogInvalid;
                AssertPreparation(fixture.Manager.RevalidateStaffMigration(preparation), expected);
                Assert.That(DescribePreparation(preparation), Is.EqualTo(prior));
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
            var unavailable = CompletedRuntimeFixture();
            var valid = unavailable.Manager.PrepareStaffMigration().Preparation;
            unavailable.Stage.RuntimeReader = _ => null;
            AssertPreparation(unavailable.Manager.RevalidateStaffMigration(valid), StaffMigrationPreparationStatus.RuntimeUnavailable);
            unavailable.Stage.RuntimeReader = _ => throw new InvalidOperationException("Runtime provider failed");
            AssertPreparation(unavailable.Manager.RevalidateStaffMigration(valid), StaffMigrationPreparationStatus.RuntimeUnavailable);

            var changing = CompletedRuntimeFixture();
            StaffMigrationPreparationResult inner = null;
            bool once = false;
            changing.Game.CatalogReader = () =>
            {
                if (!once) { once = true; inner = changing.Manager.PrepareStaffMigration(); }
                return changing.Game.Catalog;
            };
            AssertPreparation(changing.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.ReentrantValidation);
            AssertPreparation(inner, StaffMigrationPreparationStatus.ReentrantValidation);
            changing.Game.CatalogReader = () =>
            {
                changing.Stage.Runtime[EStage.Stage1].GiveStaff(scope.Staff("STAFF02"));
                return changing.Game.Catalog;
            };
            AssertPreparation(changing.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.RuntimeChanged);
            Assert.That(changing.Stage.Runtime[EStage.Stage1].IsGiveStaff("STAFF02"), Is.True, "The validator must not roll back an observed mutation");
        }
    }

    [Test]
    public void MigrationPreparation_RawBlocksEmptyAccountsExistingCommonDataAndStaleQueriesRetainTheirMeaning()
    {
        using (var scope = new RuntimeStaffScope(Catalog()))
        {
            var empty = CreateRuntimeFixture();
            HoldMigrationForPreparationObservation(empty);
            empty.Manager.LoadAllStageData(true);
            foreach (EStage stage in new[] { EStage.Stage1, EStage.Stage2, EStage.Stage3 }) Reply(empty, stage, StageResponse(empty, stage));
            var emptyResult = empty.Manager.PrepareStaffMigration();
            AssertPreparation(emptyResult, StaffMigrationPreparationStatus.Valid);
            Assert.That(emptyResult.Preparation.Candidate.Staff, Is.Empty);
            Assert.That(emptyResult.Preparation.Candidate.PandaTokens, Is.Zero);

            var existing = CreateRuntimeFixture(true);
            existing.Manager.LoadAllStageData(true);
            ReplyRuntimeRows(existing);
            AssertPreparation(existing.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.NotRequired);
            Assert.That(existing.Manager.StageMigrationCollection.PlanCount, Is.Zero);
            Assert.That(existing.Manager.RestoredGameData.StaffAccount.PandaTokens, Is.EqualTo(55));

            foreach (bool conflict in new[] { false, true })
            {
                var blocked = CreateRuntimeFixture();
                blocked.Manager.LoadAllStageData(true);
                Reply(blocked, EStage.Stage1, StageResponse(blocked, EStage.Stage1,
                    conflict ? Staff("STAFF01", 2) : Staff("STAFF02", 6)));
                Reply(blocked, EStage.Stage2, StageResponse(blocked, EStage.Stage2,
                    conflict ? Staff("STAFF01", 4) : Staff("STAFF03", 4)));
                Reply(blocked, EStage.Stage3, StageResponse(blocked, EStage.Stage3));
                AssertPreparation(blocked.Manager.PrepareStaffMigration(), StaffMigrationPreparationStatus.RawBlocked);
                Assert.That(blocked.Manager.StageMigrationCollection.Result, Is.Null);
                Assert.That(blocked.Game.Writes, Is.Zero);
                if (!conflict)
                {
                    Assert.That(blocked.Manager.StageMigrationCollection.Stages[0].Records.Single().Level, Is.EqualTo(6));
                    Assert.That(blocked.Stage.Runtime[EStage.Stage1].CaptureStaffRuntimeSnapshot().Staff.Single().Level, Is.EqualTo(5));
                    Assert.That(blocked.Stage.A2Saves, Is.EqualTo(1), "Only the existing A2 callback, not migration persistence");
                }
            }
            foreach (string change in new[] { "new-round", "query", "same-account", "different-account" })
            {
                var fixture = CompletedRuntimeFixture();
                var prior = fixture.Manager.PrepareStaffMigration().Preparation;
                string original = DescribePreparation(prior);
                if (change == "new-round") fixture.Manager.LoadStageData(EStage.Stage1, true);
                else
                {
                    if (change != "query")
                    {
                        fixture.Manager.LogOut();
                        Authenticate(fixture, change == "different-account" ? Unique("replacement-account") : fixture.Account);
                    }
                    RestoreGameData(fixture, false);
                }
                AssertPreparation(fixture.Manager.RevalidateStaffMigration(prior), StaffMigrationPreparationStatus.Invalidated);
                Assert.That(DescribePreparation(prior), Is.EqualTo(original));
                Assert.That(fixture.Game.Writes, Is.Zero);
            }
        }
    }

    private Fixture CreateRuntimeFixture(bool common = false)
    {
        var fixture = Create(common);
        fixture.Stage.UseRuntimeStages = true;
        foreach (EStage stage in new[] { EStage.Stage1, EStage.Stage2, EStage.Stage3 }) fixture.Stage.Runtime.Add(stage, new StageInfo());
        return fixture;
    }
    private Fixture CompletedRuntimeFixture(int level1 = 2, string skin = "")
    {
        var fixture = CreateRuntimeFixture();
        HoldMigrationForPreparationObservation(fixture);
        fixture.Manager.LoadAllStageData(true);
        ReplyRuntimeRows(fixture, level1, skin);
        Assert.That(fixture.Manager.CurrentStaffMigrationExecution.Status, Is.EqualTo(StaffMigrationExecutionStatus.Waiting));
        Assert.That(fixture.Game.Writes, Is.Zero);
        return fixture;
    }
    private static void HoldMigrationForPreparationObservation(Fixture fixture)
    {
        // Preparation diagnostics remain independently testable under a real preceding mail reservation.
        // Execution tests separately prove that releasing this reservation revalidates and transmits the one queued intent.
        Assert.That(fixture.Manager.TryAcquireMailSaveLease(out var lease, out var error), Is.True, error);
        Assert.That(lease.IsCurrent, Is.True);
        Assert.That(fixture.Manager.CurrentGameDataSaveCoordinator.State, Is.EqualTo(GameDataSaveCoordinatorState.ReservedForMail));
    }
    private void ReplyRuntimeRows(Fixture fixture, int level1 = 2, string skin = "")
    {
        Reply(fixture, EStage.Stage1, StageResponse(fixture, EStage.Stage1, Staff("STAFF01", level1, skin)));
        Reply(fixture, EStage.Stage2, StageResponse(fixture, EStage.Stage2, Staff("STAFF03", 4)));
        Reply(fixture, EStage.Stage3, StageResponse(fixture, EStage.Stage3));
    }
    private static void AssertPreparation(StaffMigrationPreparationResult result, StaffMigrationPreparationStatus expected)
    {
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(expected), result.Error);
        if (expected == StaffMigrationPreparationStatus.Valid) Assert.That(result.Preparation, Is.Not.Null);
        else Assert.That(result.Preparation, Is.Null, "Invalid observations must not expose an applicable partial preparation");
    }
    private static string DescribePreparation(StaffMigrationPreparation preparation) => JsonConvert.SerializeObject(new
    {
        preparation.Round, preparation.Query.Generation, preparation.Target.AccountInDate, preparation.Target.RowInDate,
        preparation.Candidate, preparation.CatalogMaximums,
        Stages = preparation.Stages.Select(stage => new { stage.Stage, stage.RowInDate, stage.Snapshot.IsApplying, stage.Snapshot.Staff }).ToArray()
    });

    /// <summary>Existing inactive-manager test pattern; restores original singleton/cache references and never calls Awake/Initialize.</summary>
    private sealed class RuntimeStaffScope : IDisposable
    {
        private readonly object _staffInstance, _staffDictionary, _furnitureInstance, _furnitureDictionary;
        private readonly List<Object> _created = new List<Object>();
        private readonly Dictionary<string, StaffData> _staff;
        public FurnitureData Furniture { get; }
        public RuntimeStaffScope(IReadOnlyList<StaffData> catalog)
        {
            _staffInstance = Field(typeof(StaffDataManager), "_instance", true).GetValue(null);
            _staffDictionary = Field(typeof(StaffDataManager), "_staffDataDic", true).GetValue(null);
            _furnitureInstance = Field(typeof(FurnitureDataManager), "_instance", true).GetValue(null);
            _furnitureDictionary = Field(typeof(FurnitureDataManager), "_furnitureDataDic", true).GetValue(null);
            _staff = catalog.ToDictionary(staff => staff.Id, StringComparer.Ordinal);
            var staffObject = Track(new GameObject("Inactive detached staff preparation test manager"));
            staffObject.SetActive(false);
            Field(typeof(StaffDataManager), "_instance", true).SetValue(null, staffObject.AddComponent<StaffDataManager>());
            Field(typeof(StaffDataManager), "_staffDataDic", true).SetValue(null, _staff);
            var furnitureObject = Track(new GameObject("Inactive detached furniture preparation test manager"));
            furnitureObject.SetActive(false);
            Field(typeof(FurnitureDataManager), "_instance", true).SetValue(null, furnitureObject.AddComponent<FurnitureDataManager>());
            Furniture = Track(ScriptableObject.CreateInstance<FurnitureData>());
            typeof(BasicData).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Furniture, "PREPARATION_MEMORY_FURNITURE");
            Field(typeof(FurnitureData), "_foodType").SetValue(Furniture, FoodType.None);
            Field(typeof(FurnitureDataManager), "_furnitureDataDic", true).SetValue(null,
                new Dictionary<string, FurnitureData> { [Furniture.Id] = Furniture });
        }
        public StaffData Staff(string id) => _staff[id];
        public StaffData CloneWithChangedMaximum(StaffData source)
        {
            StaffData clone = Track(Object.Instantiate(source));
            FieldInfo levels = clone.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(field => field.FieldType.IsArray && typeof(StaffLevelData).IsAssignableFrom(field.FieldType.GetElementType()));
            var original = (Array)levels.GetValue(clone);
            Assert.That(original.Length, Is.GreaterThan(1));
            var changed = Array.CreateInstance(original.GetType().GetElementType(), original.Length - 1);
            Array.Copy(original, changed, changed.Length);
            levels.SetValue(clone, changed);
            Assert.That(clone.MaxLevel, Is.Not.EqualTo(source.MaxLevel));
            return clone;
        }
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
        public void Dispose()
        {
            Field(typeof(StaffDataManager), "_instance", true).SetValue(null, _staffInstance);
            Field(typeof(StaffDataManager), "_staffDataDic", true).SetValue(null, _staffDictionary);
            Field(typeof(FurnitureDataManager), "_instance", true).SetValue(null, _furnitureInstance);
            Field(typeof(FurnitureDataManager), "_furnitureDataDic", true).SetValue(null, _furnitureDictionary);
            for (int index = _created.Count - 1; index >= 0; index--) if (_created[index] != null) Object.DestroyImmediate(_created[index]);
        }
    }
}
#endif
