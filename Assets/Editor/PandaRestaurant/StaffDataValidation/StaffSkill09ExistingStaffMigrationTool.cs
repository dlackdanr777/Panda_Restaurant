using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffSkill09ExistingStaffMigrationTool
    {
        private const string PolicyMarker =
            "SKILL09_REMAINING_TIME_MIGRATION_POLICY_2026_08_24_V1";
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Skill09 Existing Staff Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Skill09 Existing Staff Migration";
        private const string ExpectedPackageFingerprint =
            "be7613e884b5ae18dc94e57abc0c941dccfb09486ae9fc5ff75acf4b0e4703af";
        private const string OfficialSkillId = "STAFF_SKILL09";
        private const string OfficialDefinition =
            "전체 주방 남은 음식 조리시간 50% 감소!";
        private const string OfficialStaffDescription = "전체 음식 조리시간 단축!";
        private const string StaffId = "STAFF27";
        private const string StaffDataPath = "Assets/Resources/StaffData/STAFF27.asset";
        private const string StaffDataGuid = "9b53d48f70593ca4bb248b9fa7193cdd";
        private const string ActivePath =
            "Assets/Scripts/Datas/Staff/Skill/STAFF27Skill.asset";
        private const string LegacyGlobalPath =
            "Assets/Scripts/Datas/Staff/LegacySkill/STAFF27GlobalCookingSpeedUpSkill.asset";
        private const string ExistingLegacySpeedPath =
            "Assets/Scripts/Datas/Staff/LegacySkill/STAFF27Skill.asset";
        private const string LegacyGlobalGuid = "f7650976be31bb0458d7625e3a531527";
        private const string ExistingLegacySpeedGuid = "4803555ba0e7f7f46af709248b6ac2be";
        private const string ExistingLegacySpeedAssetSha256 =
            "bf3d91b6f1b78bedad789b7a3131798356522b7a619f8824194e9ba46795aae6";
        private const string ExistingLegacySpeedMetaSha256 =
            "62aa0dfe48bcf573136c6677ac4aacde1bff1282d8eee306259a8957a96766f5";
        private const string LegacyGlobalScriptGuid = "e0e2c583b78c98449a5c47e6abfa0ed7";
        private const string ObjectName = "STAFF27SKILL";
        private const string MigrationLegacyObjectName = "STAFF27GlobalCookingSpeedUpSkill";
        private const string LegacyGlobalDescription =
            "전체 주방 음식 제작 속도 (50%) 증가";
        private const string RuntimeScriptPath =
            "Assets/Scripts/Staff/StaffSkill/GlobalRemainingCookingTimeReductionSkill.cs";
        private static readonly string[] SynchronousImportPaths =
        {
            ActivePath,
            LegacyGlobalPath,
            StaffDataPath,
            ExistingLegacySpeedPath
        };
        private static readonly string[] OriginalRollbackPaths =
        {
            ActivePath,
            ActivePath + ".meta",
            StaffDataPath,
            StaffDataPath + ".meta",
            ExistingLegacySpeedPath,
            ExistingLegacySpeedPath + ".meta"
        };

        [MenuItem(PreviewMenuPath)]
        private static void PreviewMigration()
        {
            RunMigration(false);
        }

        [MenuItem(ApplyMenuPath)]
        private static void ApplyMigrationFromMenu()
        {
            RunMigration(true);
        }

        private static void RunMigration(bool apply)
        {
            if (apply && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "[Skill09 Existing Staff Migration]\n"
                    + "APPLY FAIL: Play Mode에서는 실행할 수 없습니다.");
                return;
            }

            MigrationInspection inspection;
            List<string> errors = new List<string>();
            if (!TryInspect(out inspection, errors) || inspection == null)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (inspection.State == MigrationState.ALREADY_APPLIED)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, true);
                return;
            }

            if (inspection.State != MigrationState.READY_TO_APPLY)
            {
                errors.Add(
                    inspection.State == MigrationState.PARTIAL_MIGRATION_STATE
                        ? "PARTIAL_MIGRATION_STATE: 일부만 적용됐거나 예상하지 않은 Asset 상태입니다."
                        : "INVALID: Skill09 Migration 기준을 충족하지 못했습니다.");
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (!apply)
            {
                LogInspection("PREVIEW", inspection, errors, true);
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Skill09 Existing Staff Migration",
                "STAFF27의 기존 GlobalCookingSpeedUpSkill을 LegacySkill로 이동하고 "
                + "공식 남은 조리시간 50% 감소 Asset을 생성한 뒤 StaffData._skill만 교체합니다.\n\n"
                + "계속하시겠습니까?",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                Debug.LogWarning("Skill09 Existing Staff Migration Apply가 취소됐습니다. 변경 0개.");
                return;
            }

            ApplyMigration(inspection);
        }

        private static bool TryInspect(
            out MigrationInspection inspection,
            List<string> errors)
        {
            inspection = new MigrationInspection();
            if (!ValidatePureStateClassifier(errors))
            {
                inspection.State = MigrationState.INVALID;
                inspection.StateReasons.Add("Pure state-classifier self-validation failed.");
                inspection.Checks = BuildDiagnosticChecks(inspection);
                return false;
            }

            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> diagnostics;
            inspection.OfficialSnapshotValid =
                StaffDataPackValidator.TryBuildCanonicalV8ReadOnlySnapshot(
                    out official,
                    out diagnostics)
                && official != null;
            if (!inspection.OfficialSnapshotValid)
            {
                AddDiagnostics("Canonical Official Snapshot", diagnostics, errors);
            }
            else
            {
                inspection.PackageFingerprint = official.PackageFingerprint;
                inspection.PackageFingerprintValid =
                    official.PackageFingerprint == ExpectedPackageFingerprint;
                if (!inspection.PackageFingerprintValid)
                {
                    errors.Add(
                        "OFFICIAL_SKILL09_TARGET_CHANGED: PackageFingerprint "
                        + official.PackageFingerprint);
                }

                inspection.OfficialSkill09Valid = ValidateOfficialSkill09(official, errors);
            }

            PopulateRuntimeScriptInspection(inspection);
            PopulateStaffInspection(inspection);
            PopulateSkillAssetInspection(inspection, ActivePath, false);
            PopulateSkillAssetInspection(inspection, LegacyGlobalPath, true);
            PopulateExistingLegacySpeedInspection(inspection);

            inspection.HasDuplicateGuid = HasDuplicateNonEmptyGuid(
                inspection.ActiveGuid,
                inspection.LegacyGlobalGuid,
                inspection.ExistingSpeedLegacyGuid);
            inspection.HasFatalDuplicateGuid =
                (!string.IsNullOrEmpty(inspection.ActiveGuid)
                 && inspection.ActiveGuid == inspection.ExistingSpeedLegacyGuid)
                || (!string.IsNullOrEmpty(inspection.LegacyGlobalGuid)
                    && inspection.LegacyGlobalGuid == inspection.ExistingSpeedLegacyGuid);
            inspection.MissingScript =
                (inspection.ActiveAssetExists && inspection.ActiveAsset == null)
                || (inspection.LegacyGlobalAssetExists && inspection.LegacyGlobalAsset == null)
                || (inspection.ExistingSpeedLegacyAssetExists
                    && inspection.ExistingSpeedLegacyAsset == null)
                || (inspection.RuntimeScriptExists && !inspection.RuntimeScriptClassValid);
            inspection.RequiredSerializedPropertyMissing =
                (inspection.ActiveAsset != null && !inspection.ActiveSerializedPropertiesValid)
                || (inspection.LegacyGlobalAsset != null
                    && !inspection.LegacyGlobalSerializedPropertiesValid);

            inspection.State = ClassifyMigrationState(inspection);
            inspection.Checks = BuildDiagnosticChecks(inspection);
            if (inspection.State == MigrationState.INVALID
                || inspection.State == MigrationState.PARTIAL_MIGRATION_STATE)
            {
                AppendFailedInspectionChecks(inspection, errors);
                return false;
            }

            return true;
        }

        private static void PopulateRuntimeScriptInspection(MigrationInspection inspection)
        {
            inspection.RuntimeScriptGuid = AssetDatabase.AssetPathToGUID(RuntimeScriptPath);
            inspection.RuntimeScriptExists = AssetFileExists(RuntimeScriptPath);
            inspection.RuntimeScriptGuidValid = IsUnityGuid(inspection.RuntimeScriptGuid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(RuntimeScriptPath);
            Type runtimeType = script == null ? null : script.GetClass();
            inspection.RuntimeScriptClassName = runtimeType == null
                ? string.Empty
                : runtimeType.Name;
            inspection.RuntimeScriptClassValid = script != null
                                                 && runtimeType
                                                 == typeof(GlobalRemainingCookingTimeReductionSkill);
        }

        private static void PopulateStaffInspection(MigrationInspection inspection)
        {
            inspection.StaffDataAssetExists = AssetFileExists(StaffDataPath);
            inspection.StaffDataMetaExists = AssetFileExists(StaffDataPath + ".meta");
            inspection.StaffDataGuid = AssetDatabase.AssetPathToGUID(StaffDataPath);
            inspection.StaffDataGuidValid = inspection.StaffDataGuid == StaffDataGuid;
            inspection.StaffDataAsset = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            inspection.StaffDataObjectValid = inspection.StaffDataAsset != null;
            if (!inspection.StaffDataObjectValid)
            {
                return;
            }

            SerializedProperty skillProperty =
                new SerializedObject(inspection.StaffDataAsset).FindProperty("_skill");
            inspection.StaffSkillPropertyExists = skillProperty != null;
            if (skillProperty == null)
            {
                return;
            }

            UnityEngine.Object referencedObject = skillProperty.objectReferenceValue;
            inspection.StaffReferenceObjectExists = referencedObject != null;
            inspection.StaffReferencePath = referencedObject == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(referencedObject);
            inspection.StaffReferenceGuid = string.IsNullOrEmpty(inspection.StaffReferencePath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(inspection.StaffReferencePath);
        }

        private static void PopulateSkillAssetInspection(
            MigrationInspection inspection,
            string assetPath,
            bool migrationLegacy)
        {
            bool assetExists = AssetFileExists(assetPath);
            bool metaExists = AssetFileExists(assetPath + ".meta");
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(assetPath);
            string className = skill == null ? string.Empty : skill.GetType().Name;
            string scriptGuid = GetSkillScriptGuid(skill);
            string objectName = skill == null ? string.Empty : skill.name;
            string description = skill == null ? string.Empty : skill.Description;
            double effect = skill == null ? double.NaN : skill.FirstValue;
            double duration = skill == null ? double.NaN : skill.Duration;
            double cooldown = skill == null ? double.NaN : skill.Cooldown;
            bool propertiesValid = skill != null && HasRequiredSkillSerializedProperties(skill);
            int references = string.IsNullOrEmpty(guid)
                ? 0
                : FindAssetReferences(guid, assetPath).Count;

            if (!migrationLegacy)
            {
                inspection.ActiveAssetExists = assetExists;
                inspection.ActiveMetaExists = metaExists;
                inspection.ActiveGuid = guid;
                inspection.ActiveAsset = skill;
                inspection.ActiveClassName = className;
                inspection.ActiveScriptGuid = scriptGuid;
                inspection.ActiveObjectName = objectName;
                inspection.ActiveDescription = description;
                inspection.ActiveEffect = effect;
                inspection.ActiveDuration = duration;
                inspection.ActiveCooldown = cooldown;
                inspection.ActiveSerializedPropertiesValid = propertiesValid;
                inspection.ActiveReferenceCount = references;
                return;
            }

            inspection.LegacyGlobalAssetExists = assetExists;
            inspection.LegacyGlobalMetaExists = metaExists;
            inspection.LegacyGlobalGuid = guid;
            inspection.LegacyGlobalAsset = skill;
            inspection.LegacyGlobalClassName = className;
            inspection.LegacyGlobalScriptGuid = scriptGuid;
            inspection.LegacyGlobalObjectName = objectName;
            inspection.LegacyGlobalDescription = description;
            inspection.LegacyGlobalEffect = effect;
            inspection.LegacyGlobalDuration = duration;
            inspection.LegacyGlobalCooldown = cooldown;
            inspection.LegacyGlobalSerializedPropertiesValid = propertiesValid;
            inspection.LegacyGlobalEffectFieldExists = skill != null
                                                       && new SerializedObject(skill).FindProperty(
                                                           "_globalCookingSpeedUpPercent") != null;
            inspection.LegacyGlobalAssetSha256 = ComputeAssetFileSha256IfExists(assetPath);
            inspection.LegacyGlobalMetaSha256 = ComputeAssetFileSha256IfExists(assetPath + ".meta");
            inspection.LegacyGlobalReferenceCount = references;
        }

        private static void PopulateExistingLegacySpeedInspection(
            MigrationInspection inspection)
        {
            inspection.ExistingSpeedLegacyAssetExists = AssetFileExists(ExistingLegacySpeedPath);
            inspection.ExistingSpeedLegacyMetaExists =
                AssetFileExists(ExistingLegacySpeedPath + ".meta");
            inspection.ExistingSpeedLegacyGuid =
                AssetDatabase.AssetPathToGUID(ExistingLegacySpeedPath);
            inspection.ExistingSpeedLegacyAsset =
                AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(ExistingLegacySpeedPath);
            SpeedUpSkill legacy = inspection.ExistingSpeedLegacyAsset;
            inspection.ExistingSpeedLegacyAssetSha256 =
                ComputeAssetFileSha256IfExists(ExistingLegacySpeedPath);
            inspection.ExistingSpeedLegacyMetaSha256 =
                ComputeAssetFileSha256IfExists(ExistingLegacySpeedPath + ".meta");
            inspection.ExistingSpeedLegacyReferenceCount =
                string.IsNullOrEmpty(inspection.ExistingSpeedLegacyGuid)
                    ? 0
                    : FindAssetReferences(
                        inspection.ExistingSpeedLegacyGuid,
                        ExistingLegacySpeedPath).Count;
            inspection.ExistingSpeedLegacyValid =
                legacy != null
                && inspection.ExistingSpeedLegacyAssetExists
                && inspection.ExistingSpeedLegacyMetaExists
                && inspection.ExistingSpeedLegacyGuid == ExistingLegacySpeedGuid
                && legacy.name == ObjectName
                && string.IsNullOrEmpty(legacy.Description)
                && Approximately(legacy.FirstValue, 100)
                && Approximately(legacy.Duration, 60)
                && Approximately(legacy.Cooldown, 250)
                && inspection.ExistingSpeedLegacyReferenceCount == 0
                && inspection.ExistingSpeedLegacyAssetSha256
                == ExistingLegacySpeedAssetSha256
                && inspection.ExistingSpeedLegacyMetaSha256
                == ExistingLegacySpeedMetaSha256;
        }

        private static bool ValidateOfficialSkill09(
            StaffOfficialDataPackageSnapshot official,
            List<string> errors)
        {
            int errorCountBefore = errors.Count;
            StaffOfficialFileSnapshot finalStaff;
            StaffOfficialFileSnapshot skillType;
            if (!official.TryGetFile(StaffOfficialDataPackageKeys.FinalStaff, out finalStaff)
                || !official.TryGetFile(StaffOfficialDataPackageKeys.SkillType, out skillType))
            {
                errors.Add("OFFICIAL_SKILL09_TARGET_CHANGED: Final18 또는 SkillType Snapshot이 없습니다.");
                return false;
            }

            int definitionCount = 0;
            for (int index = 0; index < skillType.Rows.Count; index++)
            {
                IReadOnlyList<string> row = skillType.Rows[index];
                if (row.Count >= 2 && row[0].Trim() == OfficialSkillId)
                {
                    definitionCount++;
                    if (row[1].Trim() != OfficialDefinition)
                    {
                        errors.Add("OFFICIAL_SKILL09_TARGET_CHANGED: SkillType 설명이 다릅니다.");
                    }
                }
            }

            Dictionary<string, string> expectedNames =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "STAFF27", "고든 팬지" },
                    { "STAFF68", "스윗 염소아치" }
                };
            HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
            bool valid = definitionCount == 1;
            for (int index = 0; index < finalStaff.Rows.Count; index++)
            {
                IReadOnlyList<string> row = finalStaff.Rows[index];
                if (row.Count < 14 || row[10].Trim() != OfficialSkillId)
                {
                    continue;
                }

                string id = row[0].Trim();
                string expectedName;
                double duration;
                double cooldown;
                bool rowValid = actual.Add(id)
                                && expectedNames.TryGetValue(id, out expectedName)
                                && row[1].Trim() == expectedName
                                && row[5].Trim() == "스페셜"
                                && row[6].Trim() == "주방장"
                                && row[11].Trim() == OfficialStaffDescription
                                && TryParseSeconds(row[12], out duration)
                                && TryParseSeconds(row[13], out cooldown)
                                && Approximately(duration, 1)
                                && Approximately(cooldown, 240);
                if (!rowValid)
                {
                    errors.Add("OFFICIAL_SKILL09_TARGET_CHANGED: " + id);
                    valid = false;
                }
            }

            if (!actual.SetEquals(expectedNames.Keys))
            {
                errors.Add(
                    "OFFICIAL_SKILL09_TARGET_CHANGED: STAFF27·STAFF68 분포가 다릅니다. actual "
                    + actual.Count + "/2");
                valid = false;
            }

            return valid && errors.Count == errorCountBefore;
        }

        private static MigrationState ClassifyMigrationState(MigrationInspection inspection)
        {
            if (!HasValidFoundation(inspection))
            {
                return MigrationState.INVALID;
            }

            if (MatchesReadyToApplyContract(inspection))
            {
                return MigrationState.READY_TO_APPLY;
            }

            if (MatchesAlreadyAppliedContract(inspection))
            {
                return MigrationState.ALREADY_APPLIED;
            }

            return MigrationState.PARTIAL_MIGRATION_STATE;
        }

        private static bool HasValidFoundation(MigrationInspection inspection)
        {
            return inspection != null
                   && inspection.OfficialSnapshotValid
                   && inspection.PackageFingerprintValid
                   && inspection.OfficialSkill09Valid
                   && inspection.RuntimeScriptExists
                   && inspection.RuntimeScriptGuidValid
                   && inspection.RuntimeScriptClassValid
                   && inspection.StaffDataAssetExists
                   && inspection.StaffDataMetaExists
                   && inspection.StaffDataGuidValid
                   && inspection.StaffDataObjectValid
                   && inspection.StaffSkillPropertyExists
                   && inspection.ExistingSpeedLegacyValid
                   && !inspection.HasFatalDuplicateGuid
                   && !inspection.MissingScript
                   && !inspection.RequiredSerializedPropertyMissing;
        }

        private static bool MatchesReadyToApplyContract(MigrationInspection inspection)
        {
            return inspection.ActiveAssetExists
                   && inspection.ActiveMetaExists
                   && inspection.ActiveGuid == LegacyGlobalGuid
                   && inspection.ActiveClassName == "GlobalCookingSpeedUpSkill"
                   && inspection.ActiveScriptGuid == LegacyGlobalScriptGuid
                   && inspection.ActiveObjectName == ObjectName
                   && inspection.ActiveDescription == LegacyGlobalDescription
                   && Approximately(inspection.ActiveEffect, 50)
                   && Approximately(inspection.ActiveDuration, 30)
                   && Approximately(inspection.ActiveCooldown, 200)
                   && inspection.ActiveSerializedPropertiesValid
                   && !inspection.LegacyGlobalAssetExists
                   && !inspection.LegacyGlobalMetaExists
                   && string.IsNullOrEmpty(inspection.LegacyGlobalGuid)
                   && inspection.LegacyGlobalAsset == null
                   && inspection.StaffReferenceObjectExists
                   && inspection.StaffReferencePath == ActivePath
                   && inspection.StaffReferenceGuid == LegacyGlobalGuid
                   && !inspection.HasDuplicateGuid;
        }

        private static bool MatchesAlreadyAppliedContract(MigrationInspection inspection)
        {
            return inspection.ActiveAssetExists
                   && inspection.ActiveMetaExists
                   && IsUnityGuid(inspection.ActiveGuid)
                   && inspection.ActiveGuid != LegacyGlobalGuid
                   && inspection.ActiveGuid != ExistingLegacySpeedGuid
                   && inspection.ActiveClassName
                   == "GlobalRemainingCookingTimeReductionSkill"
                   && inspection.ActiveScriptGuid == inspection.RuntimeScriptGuid
                   && inspection.ActiveObjectName == ObjectName
                   && inspection.ActiveDescription == OfficialDefinition
                   && Approximately(inspection.ActiveEffect, 50)
                   && Approximately(inspection.ActiveDuration, 1)
                   && Approximately(inspection.ActiveCooldown, 240)
                   && inspection.ActiveSerializedPropertiesValid
                   && inspection.LegacyGlobalAssetExists
                   && inspection.LegacyGlobalMetaExists
                   && inspection.LegacyGlobalGuid == LegacyGlobalGuid
                   && inspection.LegacyGlobalClassName == "GlobalCookingSpeedUpSkill"
                   && inspection.LegacyGlobalScriptGuid == LegacyGlobalScriptGuid
                   && inspection.LegacyGlobalObjectName == MigrationLegacyObjectName
                   && inspection.LegacyGlobalDescription == LegacyGlobalDescription
                   && Approximately(inspection.LegacyGlobalEffect, 50)
                   && Approximately(inspection.LegacyGlobalDuration, 30)
                   && Approximately(inspection.LegacyGlobalCooldown, 200)
                   && inspection.LegacyGlobalSerializedPropertiesValid
                   && inspection.LegacyGlobalEffectFieldExists
                   && inspection.LegacyGlobalReferenceCount == 0
                   && inspection.StaffReferenceObjectExists
                   && inspection.StaffReferencePath == ActivePath
                   && inspection.StaffReferenceGuid == inspection.ActiveGuid
                   && !inspection.HasDuplicateGuid;
        }

        private static bool ValidatePureStateClassifier(List<string> errors)
        {
            string firstDynamicGuid = "11111111111111111111111111111111";
            string secondDynamicGuid = "22222222222222222222222222222222";
            MigrationInspection ready = CreateReadyClassifierFixture();
            MigrationInspection firstApplied = CreateAppliedClassifierFixture(firstDynamicGuid);
            MigrationInspection secondApplied = CreateAppliedClassifierFixture(secondDynamicGuid);
            MigrationInspection mismatchedReference =
                CreateAppliedClassifierFixture(firstDynamicGuid);
            mismatchedReference.StaffReferenceGuid = LegacyGlobalGuid;
            MigrationInspection mixed = CreateReadyClassifierFixture();
            PopulateLegacyGlobalClassifierFixture(mixed);
            mixed.HasDuplicateGuid = true;
            MigrationInspection missingRuntime = CreateReadyClassifierFixture();
            missingRuntime.RuntimeScriptExists = false;

            bool valid = ClassifyMigrationState(ready) == MigrationState.READY_TO_APPLY
                         && ClassifyMigrationState(firstApplied)
                         == MigrationState.ALREADY_APPLIED
                         && ClassifyMigrationState(secondApplied)
                         == MigrationState.ALREADY_APPLIED
                         && ClassifyMigrationState(mismatchedReference)
                         == MigrationState.PARTIAL_MIGRATION_STATE
                         && ClassifyMigrationState(mixed)
                         == MigrationState.PARTIAL_MIGRATION_STATE
                         && ClassifyMigrationState(missingRuntime) == MigrationState.INVALID;
            if (!valid)
            {
                errors.Add("SKILL09_STATE_CLASSIFIER_SELF_VALIDATION_FAILED");
            }

            return valid;
        }

        private static MigrationInspection CreateReadyClassifierFixture()
        {
            MigrationInspection inspection = CreateFoundationClassifierFixture();
            inspection.ActiveAssetExists = true;
            inspection.ActiveMetaExists = true;
            inspection.ActiveGuid = LegacyGlobalGuid;
            inspection.ActiveClassName = "GlobalCookingSpeedUpSkill";
            inspection.ActiveScriptGuid = LegacyGlobalScriptGuid;
            inspection.ActiveObjectName = ObjectName;
            inspection.ActiveDescription = LegacyGlobalDescription;
            inspection.ActiveEffect = 50;
            inspection.ActiveDuration = 30;
            inspection.ActiveCooldown = 200;
            inspection.ActiveSerializedPropertiesValid = true;
            inspection.ActiveReferenceCount = 1;
            inspection.StaffReferenceObjectExists = true;
            inspection.StaffReferencePath = ActivePath;
            inspection.StaffReferenceGuid = LegacyGlobalGuid;
            return inspection;
        }

        private static MigrationInspection CreateAppliedClassifierFixture(string activeGuid)
        {
            MigrationInspection inspection = CreateFoundationClassifierFixture();
            inspection.ActiveAssetExists = true;
            inspection.ActiveMetaExists = true;
            inspection.ActiveGuid = activeGuid;
            inspection.ActiveClassName = "GlobalRemainingCookingTimeReductionSkill";
            inspection.ActiveScriptGuid = inspection.RuntimeScriptGuid;
            inspection.ActiveObjectName = ObjectName;
            inspection.ActiveDescription = OfficialDefinition;
            inspection.ActiveEffect = 50;
            inspection.ActiveDuration = 1;
            inspection.ActiveCooldown = 240;
            inspection.ActiveSerializedPropertiesValid = true;
            inspection.ActiveReferenceCount = 1;
            inspection.StaffReferenceObjectExists = true;
            inspection.StaffReferencePath = ActivePath;
            inspection.StaffReferenceGuid = activeGuid;
            PopulateLegacyGlobalClassifierFixture(inspection);
            return inspection;
        }

        private static MigrationInspection CreateFoundationClassifierFixture()
        {
            return new MigrationInspection
            {
                OfficialSnapshotValid = true,
                PackageFingerprintValid = true,
                OfficialSkill09Valid = true,
                RuntimeScriptExists = true,
                RuntimeScriptGuidValid = true,
                RuntimeScriptClassValid = true,
                RuntimeScriptGuid = "33333333333333333333333333333333",
                StaffDataAssetExists = true,
                StaffDataMetaExists = true,
                StaffDataGuidValid = true,
                StaffDataObjectValid = true,
                StaffSkillPropertyExists = true,
                ExistingSpeedLegacyValid = true,
                ExistingSpeedLegacyGuid = ExistingLegacySpeedGuid
            };
        }

        private static void PopulateLegacyGlobalClassifierFixture(
            MigrationInspection inspection)
        {
            inspection.LegacyGlobalAssetExists = true;
            inspection.LegacyGlobalMetaExists = true;
            inspection.LegacyGlobalGuid = LegacyGlobalGuid;
            inspection.LegacyGlobalClassName = "GlobalCookingSpeedUpSkill";
            inspection.LegacyGlobalScriptGuid = LegacyGlobalScriptGuid;
            inspection.LegacyGlobalObjectName = MigrationLegacyObjectName;
            inspection.LegacyGlobalDescription = LegacyGlobalDescription;
            inspection.LegacyGlobalEffect = 50;
            inspection.LegacyGlobalDuration = 30;
            inspection.LegacyGlobalCooldown = 200;
            inspection.LegacyGlobalSerializedPropertiesValid = true;
            inspection.LegacyGlobalEffectFieldExists = true;
            inspection.LegacyGlobalReferenceCount = 0;
        }

        private static List<MigrationDiagnosticCheck> BuildDiagnosticChecks(
            MigrationInspection inspection)
        {
            List<MigrationDiagnosticCheck> checks = new List<MigrationDiagnosticCheck>();
            bool appliedContract = inspection.ActiveClassName
                                   == "GlobalRemainingCookingTimeReductionSkill"
                                   || inspection.LegacyGlobalAssetExists
                                   || (IsUnityGuid(inspection.ActiveGuid)
                                       && inspection.ActiveGuid != LegacyGlobalGuid);
            AddCheck(checks, "Official Snapshot", "valid", inspection.OfficialSnapshotValid);
            AddCheck(
                checks,
                "PackageFingerprint",
                ExpectedPackageFingerprint,
                inspection.PackageFingerprint,
                inspection.PackageFingerprintValid);
            AddCheck(checks, "Official Skill09 Contract", "valid", inspection.OfficialSkill09Valid);
            AddCheck(checks, "Runtime Script Exists", "true", inspection.RuntimeScriptExists);
            AddCheck(
                checks,
                "Runtime Script GUID",
                "valid dynamic Unity GUID",
                inspection.RuntimeScriptGuid,
                inspection.RuntimeScriptGuidValid);
            AddCheck(
                checks,
                "Runtime Script Class",
                "GlobalRemainingCookingTimeReductionSkill",
                inspection.RuntimeScriptClassName,
                inspection.RuntimeScriptClassValid);
            AddCheck(
                checks,
                "StaffData Asset/meta Exists",
                "true",
                inspection.StaffDataAssetExists && inspection.StaffDataMetaExists);
            AddCheck(checks, "StaffData Object Exists", "true", inspection.StaffDataObjectValid);
            AddCheck(
                checks,
                "StaffData GUID",
                StaffDataGuid,
                inspection.StaffDataGuid,
                inspection.StaffDataGuidValid);
            AddCheck(checks, "StaffData _skill Property", "present", inspection.StaffSkillPropertyExists);
            AddCheck(checks, "Active Exists", "true", inspection.ActiveAssetExists && inspection.ActiveMetaExists);
            AddCheck(
                checks,
                "Active GUID",
                appliedContract
                    ? "valid dynamic GUID distinct from " + LegacyGlobalGuid
                    : LegacyGlobalGuid,
                inspection.ActiveGuid,
                appliedContract
                    ? IsUnityGuid(inspection.ActiveGuid)
                      && inspection.ActiveGuid != LegacyGlobalGuid
                      && inspection.ActiveGuid != ExistingLegacySpeedGuid
                    : inspection.ActiveGuid == LegacyGlobalGuid);
            AddCheck(
                checks,
                "Active Class",
                appliedContract
                    ? "GlobalRemainingCookingTimeReductionSkill"
                    : "GlobalCookingSpeedUpSkill",
                inspection.ActiveClassName,
                inspection.ActiveClassName
                == (appliedContract
                    ? "GlobalRemainingCookingTimeReductionSkill"
                    : "GlobalCookingSpeedUpSkill"));
            AddCheck(
                checks,
                "Active Script GUID",
                appliedContract ? inspection.RuntimeScriptGuid : LegacyGlobalScriptGuid,
                inspection.ActiveScriptGuid,
                inspection.ActiveScriptGuid
                == (appliedContract ? inspection.RuntimeScriptGuid : LegacyGlobalScriptGuid));
            AddCheck(
                checks,
                "Active Object Name",
                ObjectName,
                inspection.ActiveObjectName,
                inspection.ActiveObjectName == ObjectName);
            AddCheck(
                checks,
                "Active Description",
                appliedContract ? OfficialDefinition : LegacyGlobalDescription,
                inspection.ActiveDescription,
                inspection.ActiveDescription
                == (appliedContract ? OfficialDefinition : LegacyGlobalDescription));
            AddNumberCheck(checks, "Active Effect", 50, inspection.ActiveEffect);
            AddNumberCheck(checks, "Active Duration", appliedContract ? 1 : 30, inspection.ActiveDuration);
            AddNumberCheck(checks, "Active Cooldown", appliedContract ? 240 : 200, inspection.ActiveCooldown);
            AddCheck(
                checks,
                "Active Serialized Properties",
                "present",
                inspection.ActiveSerializedPropertiesValid);
            bool legacyGlobalExists = inspection.LegacyGlobalAssetExists
                                      && inspection.LegacyGlobalMetaExists;
            AddCheck(
                checks,
                "Migration Legacy Exists",
                appliedContract ? "true" : "false",
                legacyGlobalExists ? "true" : "false",
                legacyGlobalExists == appliedContract);
            AddCheck(
                checks,
                "Migration Legacy GUID",
                appliedContract ? LegacyGlobalGuid : "empty",
                inspection.LegacyGlobalGuid,
                appliedContract
                    ? inspection.LegacyGlobalGuid == LegacyGlobalGuid
                    : string.IsNullOrEmpty(inspection.LegacyGlobalGuid));
            AddCheck(
                checks,
                "Migration Legacy Class",
                appliedContract ? "GlobalCookingSpeedUpSkill" : "none",
                inspection.LegacyGlobalClassName,
                appliedContract
                    ? inspection.LegacyGlobalClassName == "GlobalCookingSpeedUpSkill"
                    : string.IsNullOrEmpty(inspection.LegacyGlobalClassName));
            AddCheck(
                checks,
                "Migration Legacy Script GUID",
                appliedContract ? LegacyGlobalScriptGuid : "empty",
                inspection.LegacyGlobalScriptGuid,
                appliedContract
                    ? inspection.LegacyGlobalScriptGuid == LegacyGlobalScriptGuid
                    : string.IsNullOrEmpty(inspection.LegacyGlobalScriptGuid));
            AddCheck(
                checks,
                "Migration Legacy Object Name",
                appliedContract ? MigrationLegacyObjectName : "empty",
                inspection.LegacyGlobalObjectName,
                appliedContract
                    ? inspection.LegacyGlobalObjectName == MigrationLegacyObjectName
                    : string.IsNullOrEmpty(inspection.LegacyGlobalObjectName));
            AddCheck(
                checks,
                "Migration Legacy Description",
                appliedContract ? LegacyGlobalDescription : "empty",
                inspection.LegacyGlobalDescription,
                appliedContract
                    ? inspection.LegacyGlobalDescription == LegacyGlobalDescription
                    : string.IsNullOrEmpty(inspection.LegacyGlobalDescription));
            AddNumberCheck(
                checks,
                "Migration Legacy Effect",
                appliedContract ? 50 : double.NaN,
                inspection.LegacyGlobalEffect,
                !appliedContract);
            AddNumberCheck(
                checks,
                "Migration Legacy Duration",
                appliedContract ? 30 : double.NaN,
                inspection.LegacyGlobalDuration,
                !appliedContract);
            AddNumberCheck(
                checks,
                "Migration Legacy Cooldown",
                appliedContract ? 200 : double.NaN,
                inspection.LegacyGlobalCooldown,
                !appliedContract);
            AddCheck(
                checks,
                "Migration Legacy Serialized Properties",
                appliedContract ? "present" : "not present",
                appliedContract
                    ? inspection.LegacyGlobalSerializedPropertiesValid
                    : !inspection.LegacyGlobalAssetExists);
            AddCheck(
                checks,
                "Migration Legacy Effect Field",
                appliedContract ? "_globalCookingSpeedUpPercent" : "not present",
                appliedContract
                    ? inspection.LegacyGlobalEffectFieldExists
                    : !inspection.LegacyGlobalAssetExists);
            AddCheck(
                checks,
                "Migration Legacy Reference Count",
                appliedContract ? "0" : "not present",
                appliedContract
                    ? inspection.LegacyGlobalReferenceCount.ToString(CultureInfo.InvariantCulture)
                    : "not present",
                !appliedContract || inspection.LegacyGlobalReferenceCount == 0);
            AddCheck(
                checks,
                "Post-Move Legacy Asset SHA",
                appliedContract ? "non-empty (informational; equality not required)" : "not present",
                appliedContract ? inspection.LegacyGlobalAssetSha256 : "not present",
                !appliedContract || !string.IsNullOrEmpty(inspection.LegacyGlobalAssetSha256));
            AddCheck(
                checks,
                "Staff Reference Object",
                "present",
                inspection.StaffReferenceObjectExists);
            AddCheck(
                checks,
                "Staff Reference Path",
                ActivePath,
                inspection.StaffReferencePath,
                inspection.StaffReferencePath == ActivePath);
            AddCheck(
                checks,
                "Staff Reference GUID",
                appliedContract ? inspection.ActiveGuid : LegacyGlobalGuid,
                inspection.StaffReferenceGuid,
                inspection.StaffReferenceGuid
                == (appliedContract ? inspection.ActiveGuid : LegacyGlobalGuid));
            AddCheck(
                checks,
                "Existing SpeedUp Legacy GUID",
                ExistingLegacySpeedGuid,
                inspection.ExistingSpeedLegacyGuid,
                inspection.ExistingSpeedLegacyGuid == ExistingLegacySpeedGuid);
            AddCheck(
                checks,
                "Existing SpeedUp Legacy Asset SHA",
                ExistingLegacySpeedAssetSha256,
                inspection.ExistingSpeedLegacyAssetSha256,
                inspection.ExistingSpeedLegacyAssetSha256
                == ExistingLegacySpeedAssetSha256);
            AddCheck(
                checks,
                "Existing SpeedUp Legacy meta SHA",
                ExistingLegacySpeedMetaSha256,
                inspection.ExistingSpeedLegacyMetaSha256,
                inspection.ExistingSpeedLegacyMetaSha256
                == ExistingLegacySpeedMetaSha256);
            AddCheck(
                checks,
                "Existing SpeedUp Legacy Contract",
                "valid and unreferenced",
                inspection.ExistingSpeedLegacyValid);
            AddCheck(
                checks,
                "Duplicate GUID",
                "false",
                inspection.HasDuplicateGuid ? "true" : "false",
                !inspection.HasDuplicateGuid);
            AddCheck(
                checks,
                "Missing Script",
                "false",
                inspection.MissingScript ? "true" : "false",
                !inspection.MissingScript);
            AddCheck(
                checks,
                "Required Serialized Property Missing",
                "false",
                inspection.RequiredSerializedPropertyMissing ? "true" : "false",
                !inspection.RequiredSerializedPropertyMissing);
            AddCheck(
                checks,
                "Final Migration State",
                appliedContract ? MigrationState.ALREADY_APPLIED.ToString() : MigrationState.READY_TO_APPLY.ToString(),
                inspection.State.ToString(),
                inspection.State
                == (appliedContract
                    ? MigrationState.ALREADY_APPLIED
                    : MigrationState.READY_TO_APPLY));
            return checks;
        }

        private static void AppendFailedInspectionChecks(
            MigrationInspection inspection,
            List<string> errors)
        {
            for (int index = 0; index < inspection.Checks.Count; index++)
            {
                MigrationDiagnosticCheck check = inspection.Checks[index];
                if (check.Passed)
                {
                    continue;
                }

                string reason = check.Name + " expected [" + check.Expected
                                + "] actual [" + check.Actual + "]";
                inspection.StateReasons.Add(reason);
                errors.Add(inspection.State + ": " + reason);
            }
        }

        private static void AddCheck(
            List<MigrationDiagnosticCheck> checks,
            string name,
            string expected,
            string actual,
            bool passed)
        {
            checks.Add(new MigrationDiagnosticCheck(name, expected, actual, passed));
        }

        private static void AddCheck(
            List<MigrationDiagnosticCheck> checks,
            string name,
            string expected,
            bool actual)
        {
            AddCheck(checks, name, expected, actual ? "true" : "false", actual);
        }

        private static void AddNumberCheck(
            List<MigrationDiagnosticCheck> checks,
            string name,
            double expected,
            double actual,
            bool bypass = false)
        {
            bool passed = bypass || Approximately(expected, actual);
            AddCheck(
                checks,
                name,
                bypass ? "not present" : FormatNumber(expected),
                double.IsNaN(actual) ? "not present" : FormatNumber(actual),
                passed);
        }

        private static string FormatNumber(double value)
        {
            return double.IsNaN(value)
                ? "not present"
                : value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static bool HasDuplicateNonEmptyGuid(params string[] values)
        {
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrEmpty(values[index]) && !unique.Add(values[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRequiredSkillSerializedProperties(SkillBase skill)
        {
            SerializedObject serialized = new SerializedObject(skill);
            if (serialized.FindProperty("_description") == null
                || serialized.FindProperty("_duration") == null
                || serialized.FindProperty("_cooldown") == null)
            {
                return false;
            }

            if (skill is GlobalRemainingCookingTimeReductionSkill)
            {
                return serialized.FindProperty("_remainingCookingTimeReductionPercent") != null;
            }

            if (skill is GlobalCookingSpeedUpSkill)
            {
                return serialized.FindProperty("_globalCookingSpeedUpPercent") != null;
            }

            return false;
        }

        private static string GetSkillScriptGuid(SkillBase skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            MonoScript script = MonoScript.FromScriptableObject(skill);
            string path = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
            return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
        }

        private static bool AssetFileExists(string assetPath)
        {
            return File.Exists(GetAbsolutePath(assetPath));
        }

        private static string ComputeAssetFileSha256IfExists(string assetPath)
        {
            return AssetFileExists(assetPath)
                ? ComputeAssetFileSha256(assetPath)
                : string.Empty;
        }

        private static void ApplyMigration(MigrationInspection before)
        {
            MigrationInspection finalPreflight;
            List<string> finalErrors = new List<string>();
            if (!TryInspect(out finalPreflight, finalErrors)
                || finalPreflight.State != MigrationState.READY_TO_APPLY)
            {
                finalErrors.Add("APPLY 직전 상태가 READY_TO_APPLY가 아닙니다. 변경 0개.");
                LogInspection("APPLY", finalPreflight, finalErrors, false);
                return;
            }

            MigrationBackup backup = null;
            MigrationInspection postInspection = null;
            List<string> postErrors = new List<string>();
            bool moved = false;
            bool created = false;
            bool writesStarted = false;
            try
            {
                before = finalPreflight;
                backup = CaptureBackup(finalPreflight);
                writesStarted = true;
                string moveError = AssetDatabase.MoveAsset(ActivePath, LegacyGlobalPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException("STAFF27 GlobalCooking Legacy 이동 실패: " + moveError);
                }

                moved = true;
                SaveAndForceSynchronousImport();
                if (AssetDatabase.AssetPathToGUID(LegacyGlobalPath) != LegacyGlobalGuid)
                {
                    throw new InvalidOperationException("STAFF27 GlobalCooking Legacy GUID가 이동 중 변경됐습니다.");
                }

                GlobalCookingSpeedUpSkill migratedLegacy =
                    AssetDatabase.LoadAssetAtPath<GlobalCookingSpeedUpSkill>(LegacyGlobalPath);
                if (migratedLegacy == null)
                {
                    throw new InvalidOperationException("이동된 STAFF27 GlobalCooking Legacy를 읽을 수 없습니다.");
                }

                if (migratedLegacy.name != MigrationLegacyObjectName)
                {
                    migratedLegacy.name = MigrationLegacyObjectName;
                    EditorUtility.SetDirty(migratedLegacy);
                    SaveAndForceSynchronousImport();
                }

                migratedLegacy =
                    AssetDatabase.LoadAssetAtPath<GlobalCookingSpeedUpSkill>(LegacyGlobalPath);
                if (migratedLegacy == null || migratedLegacy.name != MigrationLegacyObjectName)
                {
                    throw new InvalidOperationException(
                        "STAFF27 GlobalCooking Legacy Object Name 정규화에 실패했습니다.");
                }

                GlobalRemainingCookingTimeReductionSkill skill =
                    ScriptableObject.CreateInstance<GlobalRemainingCookingTimeReductionSkill>();
                AssetDatabase.CreateAsset(skill, ActivePath);
                created = true;
                skill.name = ObjectName;
                ConfigureNewSkill(skill);
                EditorUtility.SetDirty(skill);
                SaveAndForceSynchronousImport();
                string newGuid = AssetDatabase.AssetPathToGUID(ActivePath);
                if (!IsUnityGuid(newGuid)
                    || newGuid == LegacyGlobalGuid
                    || newGuid == ExistingLegacySpeedGuid)
                {
                    throw new InvalidOperationException("신규 Skill09 GUID가 유효하지 않습니다: " + newGuid);
                }

                GlobalRemainingCookingTimeReductionSkill reloadedSkill =
                    AssetDatabase.LoadAssetAtPath<GlobalRemainingCookingTimeReductionSkill>(
                        ActivePath);
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
                if (staff == null || reloadedSkill == null)
                {
                    throw new InvalidOperationException("신규 Skill09 또는 STAFF27 StaffData를 다시 읽을 수 없습니다.");
                }

                SetSkillReference(staff, reloadedSkill);
                SaveAndForceSynchronousImport();

                bool inspected = TryInspect(out postInspection, postErrors);
                bool postPassed = inspected
                                  && postInspection.State == MigrationState.ALREADY_APPLIED;
                postPassed &= RequireCondition(
                    "Post-Apply Migration State",
                    MigrationState.ALREADY_APPLIED.ToString(),
                    postInspection == null ? "null" : postInspection.State.ToString(),
                    postErrors);
                postPassed &= RequireCondition(
                    "Post-Apply Active GUID",
                    newGuid,
                    postInspection == null ? string.Empty : postInspection.ActiveGuid,
                    postErrors);
                postPassed &= RequireCondition(
                    "Post-Apply STAFF27 reference GUID",
                    newGuid,
                    postInspection == null ? string.Empty : postInspection.StaffReferenceGuid,
                    postErrors);
                postPassed &= RequireCondition(
                    "Post-Apply Migration Legacy GUID",
                    LegacyGlobalGuid,
                    postInspection == null ? string.Empty : postInspection.LegacyGlobalGuid,
                    postErrors);
                postPassed &= ValidatePostApplyFiles(backup, postInspection, postErrors);
                postPassed &= ValidatePostInventory(postErrors);
                postPassed &= ValidatePostDryRun(postErrors);
                if (!postPassed)
                {
                    throw new InvalidOperationException(
                        "Post-Apply 검증 실패: " + string.Join(" | ", postErrors.ToArray()));
                }

                LogApplySuccess(before, postInspection, backup);
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = new List<string>();
                bool rollbackPassed = !writesStarted
                                      || RollbackMigration(
                                          backup,
                                          moved,
                                          created,
                                          rollbackErrors);
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Skill09 Existing Staff Migration]");
                output.AppendLine("APPLY FAIL: " + exception.Message);
                AppendInspectionChecks(output, postInspection, "POST-APPLY CHECK");
                for (int index = 0; index < postErrors.Count; index++)
                {
                    output.AppendLine("POST-APPLY ERROR: " + postErrors[index]);
                }

                output.AppendLine(
                    !writesStarted
                        ? "SKILL09 MIGRATION ROLLBACK: NOT_REQUIRED"
                        : rollbackPassed
                            ? "SKILL09 MIGRATION ROLLBACK: PASS"
                            : "SKILL09 MIGRATION ROLLBACK: FAIL");
                for (int index = 0; index < rollbackErrors.Count; index++)
                {
                    output.AppendLine("ROLLBACK ERROR: " + rollbackErrors[index]);
                }

                if (!rollbackPassed)
                {
                    output.AppendLine("CRITICAL_MIGRATION_ROLLBACK_FAILED");
                }

                Debug.LogError(output.ToString());
            }
        }

        private static MigrationBackup CaptureBackup(MigrationInspection inspection)
        {
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            if (staff == null || inspection == null
                || inspection.State != MigrationState.READY_TO_APPLY)
            {
                throw new InvalidOperationException("Backup 직전 READY STAFF27 상태가 없습니다.");
            }

            Dictionary<string, byte[]> originalFiles =
                new Dictionary<string, byte[]>(StringComparer.Ordinal);
            Dictionary<string, string> originalFileHashes =
                new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < OriginalRollbackPaths.Length; index++)
            {
                string path = OriginalRollbackPaths[index];
                string absolutePath = GetAbsolutePath(path);
                if (!File.Exists(absolutePath))
                {
                    throw new InvalidOperationException("Rollback 원본 파일이 없습니다: " + path);
                }

                byte[] bytes = File.ReadAllBytes(absolutePath);
                originalFiles.Add(path, bytes);
                originalFileHashes.Add(path, ComputeSha256(bytes));
            }

            return new MigrationBackup(
                originalFiles,
                originalFileHashes,
                inspection.StaffDataGuid,
                CaptureSerializedFingerprint(staff, string.Empty),
                CaptureSerializedFingerprint(staff, "_skill"),
                inspection.StaffReferenceGuid,
                inspection.ActiveClassName,
                inspection.ActiveGuid,
                inspection.ActiveEffect,
                inspection.ActiveDuration,
                inspection.ActiveCooldown,
                CaptureSerializedFingerprint(inspection.ActiveAsset, "m_Name"),
                inspection.ExistingSpeedLegacyAsset == null
                    ? string.Empty
                    : inspection.ExistingSpeedLegacyAsset.GetType().Name,
                inspection.ExistingSpeedLegacyGuid,
                inspection.ExistingSpeedLegacyAsset == null
                    ? double.NaN
                    : inspection.ExistingSpeedLegacyAsset.FirstValue,
                inspection.ExistingSpeedLegacyAsset == null
                    ? double.NaN
                    : inspection.ExistingSpeedLegacyAsset.Duration,
                inspection.ExistingSpeedLegacyAsset == null
                    ? double.NaN
                    : inspection.ExistingSpeedLegacyAsset.Cooldown,
                CaptureLegacyFileHashes());
        }

        private static void ConfigureNewSkill(
            GlobalRemainingCookingTimeReductionSkill skill)
        {
            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty description = serialized.FindProperty("_description");
            SerializedProperty duration = serialized.FindProperty("_duration");
            SerializedProperty cooldown = serialized.FindProperty("_cooldown");
            SerializedProperty percent = serialized.FindProperty(
                "_remainingCookingTimeReductionPercent");
            if (description == null || duration == null || cooldown == null || percent == null)
            {
                throw new InvalidOperationException("Skill09 직렬화 필드를 찾을 수 없습니다.");
            }

            description.stringValue = OfficialDefinition;
            duration.floatValue = 1f;
            cooldown.floatValue = 240f;
            percent.floatValue = 50f;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException("Skill09 값 적용에 실패했습니다.");
            }
        }

        private static void SetSkillReference(StaffData staff, SkillBase skill)
        {
            SerializedObject serialized = new SerializedObject(staff);
            SerializedProperty property = serialized.FindProperty("_skill");
            if (property == null)
            {
                throw new InvalidOperationException("STAFF27._skill 필드를 찾을 수 없습니다.");
            }

            if (property.objectReferenceValue == skill)
            {
                return;
            }

            property.objectReferenceValue = skill;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException("STAFF27._skill 참조 적용에 실패했습니다.");
            }

            EditorUtility.SetDirty(staff);
        }

        private static bool ValidatePostApplyFiles(
            MigrationBackup backup,
            MigrationInspection inspection,
            List<string> errors)
        {
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            if (backup == null)
            {
                errors.Add("Post-Apply 원본 Backup이 없습니다.");
                return false;
            }

            bool valid = staff != null;
            valid &= RequireCondition(
                "Post-Apply StaffData GUID",
                backup.StaffDataGuid,
                AssetDatabase.AssetPathToGUID(StaffDataPath),
                errors);
            valid &= RequireCondition(
                "Post-Apply StaffData non-skill fingerprint",
                backup.StaffDataNonSkillFingerprint,
                staff == null ? string.Empty : CaptureSerializedFingerprint(staff, "_skill"),
                errors);
            string migratedLegacyAssetSha = ComputeAssetFileSha256IfExists(LegacyGlobalPath);
            valid &= RequireCondition(
                "Post-Move Legacy Asset SHA",
                "non-empty (informational; equality not required)",
                migratedLegacyAssetSha,
                !string.IsNullOrEmpty(migratedLegacyAssetSha),
                errors);
            valid &= RequireFileHash(
                LegacyGlobalPath + ".meta",
                backup.OriginalFileHashes[ActivePath + ".meta"],
                "Post-Move Legacy meta SHA preservation",
                errors);
            SkillBase migratedLegacy = inspection == null
                ? null
                : inspection.LegacyGlobalAsset;
            valid &= RequireCondition(
                "Post-Move Legacy semantic comparison",
                backup.ActiveNonNameFingerprint,
                migratedLegacy == null
                    ? string.Empty
                    : CaptureSerializedFingerprint(migratedLegacy, "m_Name"),
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy GUID preservation",
                LegacyGlobalGuid,
                inspection == null ? string.Empty : inspection.LegacyGlobalGuid,
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Object Name",
                MigrationLegacyObjectName,
                inspection == null ? string.Empty : inspection.LegacyGlobalObjectName,
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Class",
                "GlobalCookingSpeedUpSkill",
                inspection == null ? string.Empty : inspection.LegacyGlobalClassName,
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Script GUID",
                LegacyGlobalScriptGuid,
                inspection == null ? string.Empty : inspection.LegacyGlobalScriptGuid,
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Description",
                LegacyGlobalDescription,
                inspection == null ? string.Empty : inspection.LegacyGlobalDescription,
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Effect Field",
                "present",
                inspection != null && inspection.LegacyGlobalEffectFieldExists
                    ? "present"
                    : "missing",
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Effect",
                "50",
                inspection == null ? string.Empty : FormatNumber(inspection.LegacyGlobalEffect),
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Duration",
                "30",
                inspection == null ? string.Empty : FormatNumber(inspection.LegacyGlobalDuration),
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Cooldown",
                "200",
                inspection == null ? string.Empty : FormatNumber(inspection.LegacyGlobalCooldown),
                errors);
            valid &= RequireCondition(
                "Post-Move Legacy Reference Count",
                "0",
                inspection == null
                    ? string.Empty
                    : inspection.LegacyGlobalReferenceCount.ToString(CultureInfo.InvariantCulture),
                errors);
            valid &= RequireFileHash(
                ExistingLegacySpeedPath,
                backup.OriginalFileHashes[ExistingLegacySpeedPath],
                "Post-Apply existing SpeedUp Legacy asset SHA",
                errors);
            valid &= RequireFileHash(
                ExistingLegacySpeedPath + ".meta",
                backup.OriginalFileHashes[ExistingLegacySpeedPath + ".meta"],
                "Post-Apply existing SpeedUp Legacy meta SHA",
                errors);
            if (!ExistingLegacyFilesUnchanged(backup.LegacyFileHashes))
            {
                errors.Add("Post-Apply 기존 Legacy 17개 SHA 집합이 변경됐습니다.");
                valid = false;
            }

            return valid;
        }

        private static bool ValidatePostInventory(List<string> errors)
        {
            StaffDataAssetInventorySnapshot snapshot;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out snapshot,
                    out diagnostics)
                || snapshot == null)
            {
                AddDiagnostics("Post-Apply Current Inventory", diagnostics, errors);
                return false;
            }

            Dictionary<string, int> classes = new Dictionary<string, int>(StringComparer.Ordinal);
            int shared = 0;
            int orphan = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                int count;
                classes.TryGetValue(skill.ConcreteTypeName, out count);
                classes[skill.ConcreteTypeName] = count + 1;
                shared += skill.IsShared ? 1 : 0;
                orphan += skill.IsOrphan ? 1 : 0;
            }

            int legacyCount = 0;
            int legacyReferences = 0;
            string[] legacyGuids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { StaffDataAssetInventoryReader.LegacySkillFolder });
            for (int index = 0; index < legacyGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(legacyGuids[index]);
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                    || AssetDatabase.LoadAssetAtPath<SkillBase>(path) == null)
                {
                    continue;
                }

                legacyCount++;
                legacyReferences += FindAssetReferences(legacyGuids[index], path).Count;
            }

            bool valid = snapshot.Skills.Count == 32
                         && GetCount(classes, "SpeedUpSkill") == 12
                         && GetCount(classes, "TouchAddCustomerButtonSkill") == 3
                         && GetCount(classes, "AssignedCookingSpeedUpSkill") == 4
                         && GetCount(classes, "FoodPaymentTipUpSkill") == 1
                         && GetCount(classes, "FoodPriceUpSkill") == 6
                         && GetCount(classes, "NormalCustomerMoveSpeedUpSkill") == 5
                         && GetCount(classes, "GlobalCookingSpeedUpSkill") == 0
                         && GetCount(
                             classes,
                             "GlobalRemainingCookingTimeReductionSkill") == 1
                         && GetCount(classes, "AllStaffMoveSpeedUpSkill") == 0
                         && classes.Count == 7
                         && legacyCount == 18
                         && shared == 0
                         && orphan == 0
                         && legacyReferences == 0;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Inventory baseline 불일치: active " + snapshot.Skills.Count
                    + ", legacy " + legacyCount
                    + ", shared/orphan " + shared + "/" + orphan
                    + ", legacy refs " + legacyReferences + ".");
            }

            return valid;
        }

        private static bool ValidatePostDryRun(List<string> errors)
        {
            StaffDataDryRunPlanSnapshot plan;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                    out plan,
                    out diagnostics)
                || plan == null)
            {
                AddDiagnostics("Post-Apply Dry Run V8", diagnostics, errors);
                return false;
            }

            int warnings = 0;
            HashSet<string> prerequisiteKeys = new HashSet<string>(StringComparer.Ordinal);
            int changedFields = 0;
            int existingMismatch = 0;
            int newUnsupported = 0;
            int skillPrerequisites = 0;
            int existingWarnings = 0;
            int existingClass = 0;
            int existingSave = 0;
            int newReady = 0;
            int newClass = 0;
            int durationMismatch = 0;
            int cooldownMismatch = 0;
            int skill09ExistingApplied = 0;
            int skill09ExistingRedesign = 0;
            int skill09NewAssetPlan = 0;
            int skill09NewRuntimePrerequisite = 0;
            bool staff68Valid = false;
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                StaffDataDryRunIssue issue = plan.GlobalIssues[index];
                warnings += issue.IsWarning ? 1 : 0;
                if (issue.IsPrerequisite)
                {
                    prerequisiteKeys.Add("GLOBAL|" + issue.Code);
                }
            }

            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                changedFields += staff.ChangedFieldCount;
                bool hasSkillPrerequisite = false;
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    durationMismatch += SkillNumbersEqual(
                        staff.SkillPlan.CurrentDuration,
                        staff.SkillPlan.TargetDuration) ? 0 : 1;
                    cooldownMismatch += SkillNumbersEqual(
                        staff.SkillPlan.CurrentCooldown,
                        staff.SkillPlan.TargetCooldown) ? 0 : 1;
                    existingWarnings += staff.Readiness
                                        == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS ? 1 : 0;
                    existingClass += staff.Readiness
                                     == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                    existingSave += staff.Readiness
                                    == StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED ? 1 : 0;
                }
                else
                {
                    newReady += staff.Readiness
                                == StaffDryRunReadiness.ASSET_PLAN_READY ? 1 : 0;
                    newClass += staff.Readiness
                                == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                }

                for (int issueIndex = 0; issueIndex < staff.Issues.Count; issueIndex++)
                {
                    StaffDataDryRunIssue issue = staff.Issues[issueIndex];
                    warnings += issue.IsWarning ? 1 : 0;
                    if (issue.IsPrerequisite)
                    {
                        prerequisiteKeys.Add(staff.StaffId);
                    }

                    existingMismatch += issue.Code == "EXISTING_SKILL_CLASS_MISMATCH" ? 1 : 0;
                    newUnsupported += issue.Code
                                      == "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED" ? 1 : 0;
                    hasSkillPrerequisite |= issue.IsPrerequisite
                                            && (issue.Disposition
                                                == StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED
                                                || issue.Disposition
                                                == StaffDryRunFieldDisposition.SKILL_CLASS_MIGRATION_REQUIRED);
                }

                skillPrerequisites += hasSkillPrerequisite ? 1 : 0;
                if (staff.SkillPlan.OfficialSkillId != OfficialSkillId)
                {
                    continue;
                }

                StaffDataDryRunSkillEffectPlan effect = staff.SkillPlan.EffectPlan;
                if (staff.StaffId == "STAFF27")
                {
                    bool applied = staff.SkillPlan.RequiredClassExists
                                   && staff.SkillPlan.ClassMatches
                                   && staff.SkillPlan.CurrentClassName
                                   == "GlobalRemainingCookingTimeReductionSkill"
                                   && effect != null
                                   && effect.FieldMatches
                                   && effect.ValueMatches
                                   && effect.CurrentFieldPath
                                   == "_remainingCookingTimeReductionPercent"
                                   && effect.CurrentValue == "50";
                    skill09ExistingApplied += applied ? 1 : 0;
                    skill09ExistingRedesign += applied ? 0 : 1;
                }
                else if (staff.StaffId == "STAFF68")
                {
                    staff68Valid = staff.SkillPlan.RequiredClassExists
                                   && staff.SkillPlan.ClassMatches
                                   && staff.SkillPlan.RequiredClassName
                                   == "GlobalRemainingCookingTimeReductionSkill"
                                   && effect != null
                                   && effect.TargetFieldPath
                                   == "_remainingCookingTimeReductionPercent"
                                   && effect.TargetValue == "50"
                                   && effect.Disposition
                                   == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                                   && SkillNumbersEqual(staff.SkillPlan.TargetDuration, "1")
                                   && SkillNumbersEqual(staff.SkillPlan.TargetCooldown, "240")
                                   && staff.Readiness
                                   == StaffDryRunReadiness.ASSET_PLAN_READY;
                    skill09NewAssetPlan += staff68Valid ? 1 : 0;
                    skill09NewRuntimePrerequisite += staff68Valid ? 0 : 1;
                }
            }

            bool valid = plan.PlanningPolicyVersion
                         == StaffDataDryRunPlanSnapshot.V8PolicyVersion
                         && plan.OfficialPackageFingerprint == ExpectedPackageFingerprint
                         && existingMismatch == 0
                         && newUnsupported == 0
                         && skillPrerequisites == 0
                         && prerequisiteKeys.Count == 1
                         && warnings == 65
                         && durationMismatch == 0
                         && cooldownMismatch == 0
                         && changedFields == 2111
                         && existingWarnings == 31
                         && existingClass == 0
                         && existingSave == 1
                         && newReady == 60
                         && newClass == 0
                         && skill09ExistingApplied == 1
                         && skill09ExistingRedesign == 0
                         && skill09NewAssetPlan == 1
                         && skill09NewRuntimePrerequisite == 0
                         && staff68Valid;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Dry Run V8 baseline 불일치: mismatch "
                    + existingMismatch + "/" + newUnsupported
                    + ", prerequisite/warning " + prerequisiteKeys.Count + "/" + warnings
                    + ", duration/cooldown " + durationMismatch + "/" + cooldownMismatch
                    + ", field " + changedFields + ".");
            }

            return valid;
        }

        private static bool RollbackMigration(
            MigrationBackup backup,
            bool moved,
            bool created,
            List<string> errors)
        {
            if (backup == null)
            {
                errors.Add("Rollback 검증용 원본 Backup이 없습니다.");
                return false;
            }

            bool valid = true;
            try
            {
                if (created
                    || AssetFileExists(ActivePath)
                    || AssetFileExists(ActivePath + ".meta"))
                {
                    AssetDatabase.DeleteAsset(ActivePath);
                }

                if (moved
                    || AssetFileExists(LegacyGlobalPath)
                    || AssetFileExists(LegacyGlobalPath + ".meta"))
                {
                    AssetDatabase.DeleteAsset(LegacyGlobalPath);
                }

                if (!RestoreOriginalFilesFromSnapshot(backup, errors))
                {
                    valid = false;
                }

                ForceSynchronousReloadAfterRawRestore();
            }
            catch (Exception exception)
            {
                errors.Add("Rollback 예외: " + exception.Message);
                valid = false;
            }

            MigrationInspection restored;
            List<string> inspectionErrors = new List<string>();
            bool readyRestored = TryInspect(out restored, inspectionErrors)
                                 && restored.State == MigrationState.READY_TO_APPLY;
            if (!readyRestored)
            {
                errors.AddRange(inspectionErrors);
                if (restored != null)
                {
                    for (int index = 0; index < restored.Checks.Count; index++)
                    {
                        MigrationDiagnosticCheck check = restored.Checks[index];
                        errors.Add(
                            "ROLLBACK CHECK: " + check.Name
                            + " | expected=" + check.Expected
                            + " | actual=" + check.Actual
                            + " | " + (check.Passed ? "PASS" : "FAIL"));
                    }
                }

                errors.Add("Rollback 후 READY_TO_APPLY 상태가 복구되지 않았습니다.");
                valid = false;
            }

            valid &= ValidateOriginalFilesRestored(backup, restored, errors);
            return valid;
        }

        private static bool RestoreOriginalFilesFromSnapshot(
            MigrationBackup backup,
            List<string> errors)
        {
            try
            {
                string[] residualPaths =
                {
                    ActivePath,
                    ActivePath + ".meta",
                    LegacyGlobalPath,
                    LegacyGlobalPath + ".meta",
                    StaffDataPath,
                    StaffDataPath + ".meta",
                    ExistingLegacySpeedPath,
                    ExistingLegacySpeedPath + ".meta"
                };
                for (int index = 0; index < residualPaths.Length; index++)
                {
                    string absolutePath = GetAbsolutePath(residualPaths[index]);
                    if (File.Exists(absolutePath))
                    {
                        File.Delete(absolutePath);
                    }
                }

                foreach (KeyValuePair<string, byte[]> pair in backup.OriginalFiles)
                {
                    File.WriteAllBytes(GetAbsolutePath(pair.Key), pair.Value);
                }

                return true;
            }
            catch (Exception exception)
            {
                errors.Add("Rollback raw-byte 복구 예외: " + exception.Message);
                return false;
            }
        }

        private static void SaveAndForceSynchronousImport()
        {
            ImportAssetOptions options = ImportAssetOptions.ForceUpdate
                                         | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.SaveAssets();
            for (int index = 0; index < SynchronousImportPaths.Length; index++)
            {
                string path = SynchronousImportPaths[index];
                if (AssetFileExists(path) || AssetFileExists(path + ".meta"))
                {
                    AssetDatabase.ImportAsset(path, options);
                }
            }

            AssetDatabase.Refresh(options);
        }

        private static void ForceSynchronousReloadAfterRawRestore()
        {
            ImportAssetOptions options = ImportAssetOptions.ForceUpdate
                                         | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.Refresh(options);
            for (int index = 0; index < SynchronousImportPaths.Length; index++)
            {
                string path = SynchronousImportPaths[index];
                if (AssetFileExists(path) || AssetFileExists(path + ".meta"))
                {
                    AssetDatabase.ImportAsset(path, options);
                }
            }

            AssetDatabase.Refresh(options);
        }

        private static bool ValidateOriginalFilesRestored(
            MigrationBackup backup,
            MigrationInspection restored,
            List<string> errors)
        {
            bool valid = true;
            foreach (KeyValuePair<string, string> pair in backup.OriginalFileHashes)
            {
                valid &= RequireFileHash(
                    pair.Key,
                    pair.Value,
                    "Rollback original SHA",
                    errors);
            }

            bool migrationAssetAbsent = !AssetFileExists(LegacyGlobalPath);
            bool migrationMetaAbsent = !AssetFileExists(LegacyGlobalPath + ".meta");
            valid &= RequireCondition(
                "Rollback Migration Legacy asset absence",
                "absent",
                migrationAssetAbsent ? "absent" : "present",
                errors);
            valid &= RequireCondition(
                "Rollback Migration Legacy meta absence",
                "absent",
                migrationMetaAbsent ? "absent" : "present",
                errors);
            valid &= RequireCondition(
                "Rollback Active GUID",
                backup.ActiveGuid,
                restored == null ? string.Empty : restored.ActiveGuid,
                errors);
            valid &= RequireCondition(
                "Rollback STAFF27 reference GUID",
                backup.StaffReferenceGuid,
                restored == null ? string.Empty : restored.StaffReferenceGuid,
                errors);
            valid &= RequireCondition(
                "Rollback Existing SpeedUp Legacy GUID",
                backup.ExistingSpeedLegacyGuid,
                restored == null ? string.Empty : restored.ExistingSpeedLegacyGuid,
                errors);
            SpeedUpSkill restoredExistingSpeed = restored == null
                ? null
                : restored.ExistingSpeedLegacyAsset;
            valid &= RequireCondition(
                "Rollback Existing SpeedUp Legacy Class",
                backup.ExistingSpeedLegacyClassName,
                restoredExistingSpeed == null
                    ? string.Empty
                    : restoredExistingSpeed.GetType().Name,
                errors);
            valid &= RequireCondition(
                "Rollback Existing SpeedUp Legacy Effect",
                FormatNumber(backup.ExistingSpeedLegacyEffect),
                restoredExistingSpeed == null
                    ? string.Empty
                    : FormatNumber(restoredExistingSpeed.FirstValue),
                errors);
            valid &= RequireCondition(
                "Rollback Existing SpeedUp Legacy Duration",
                FormatNumber(backup.ExistingSpeedLegacyDuration),
                restoredExistingSpeed == null
                    ? string.Empty
                    : FormatNumber(restoredExistingSpeed.Duration),
                errors);
            valid &= RequireCondition(
                "Rollback Existing SpeedUp Legacy Cooldown",
                FormatNumber(backup.ExistingSpeedLegacyCooldown),
                restoredExistingSpeed == null
                    ? string.Empty
                    : FormatNumber(restoredExistingSpeed.Cooldown),
                errors);
            valid &= RequireCondition(
                "Rollback Active Class",
                backup.ActiveClassName,
                restored == null ? string.Empty : restored.ActiveClassName,
                errors);
            valid &= RequireCondition(
                "Rollback Active Effect",
                FormatNumber(backup.ActiveEffect),
                restored == null ? string.Empty : FormatNumber(restored.ActiveEffect),
                errors);
            valid &= RequireCondition(
                "Rollback Active Duration",
                FormatNumber(backup.ActiveDuration),
                restored == null ? string.Empty : FormatNumber(restored.ActiveDuration),
                errors);
            valid &= RequireCondition(
                "Rollback Active Cooldown",
                FormatNumber(backup.ActiveCooldown),
                restored == null ? string.Empty : FormatNumber(restored.ActiveCooldown),
                errors);

            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            valid &= RequireCondition(
                "Rollback StaffData full fingerprint",
                backup.StaffDataFullFingerprint,
                staff == null ? string.Empty : CaptureSerializedFingerprint(staff, string.Empty),
                errors);
            if (!LegacyFileHashesEqual(
                    backup.LegacyFileHashes,
                    CaptureLegacyFileHashes()))
            {
                errors.Add("Rollback 기존 Legacy SHA 집합 expected/actual 불일치");
                valid = false;
            }

            return valid;
        }

        private static bool RequireFileHash(
            string assetPath,
            string expected,
            string label,
            List<string> errors)
        {
            string actual = ComputeAssetFileSha256IfExists(assetPath);
            return RequireCondition(label + " | " + assetPath, expected, actual, errors);
        }

        private static bool RequireCondition(
            string name,
            string expected,
            string actual,
            List<string> errors)
        {
            bool passed = string.Equals(expected, actual, StringComparison.Ordinal);
            if (!passed)
            {
                errors.Add(name + " expected [" + expected + "] actual [" + actual + "]");
            }

            return passed;
        }

        private static bool RequireCondition(
            string name,
            string expected,
            string actual,
            bool passed,
            List<string> errors)
        {
            if (!passed)
            {
                errors.Add(name + " expected [" + expected + "] actual [" + actual + "]");
            }

            return passed;
        }

        private static Dictionary<string, string> CaptureLegacyFileHashes()
        {
            Dictionary<string, string> hashes =
                new Dictionary<string, string>(StringComparer.Ordinal);
            string absoluteFolder = GetAbsolutePath(
                StaffDataAssetInventoryReader.LegacySkillFolder);
            if (!Directory.Exists(absoluteFolder))
            {
                return hashes;
            }

            string[] files = Directory.GetFiles(absoluteFolder, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            for (int index = 0; index < files.Length; index++)
            {
                string extension = Path.GetExtension(files[index]);
                if (!extension.Equals(".asset", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hashes[Path.GetFileName(files[index])] =
                    ComputeSha256(File.ReadAllBytes(files[index]));
            }

            return hashes;
        }

        private static bool ExistingLegacyFilesUnchanged(
            IReadOnlyDictionary<string, string> before)
        {
            Dictionary<string, string> after = CaptureLegacyFileHashes();
            foreach (KeyValuePair<string, string> pair in before)
            {
                string current;
                if (!after.TryGetValue(pair.Key, out current) || current != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LegacyFileHashesEqual(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> pair in expected)
            {
                string value;
                if (!actual.TryGetValue(pair.Key, out value) || value != pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static string CaptureSerializedFingerprint(
            UnityEngine.Object asset,
            string excludedPropertyPath)
        {
            SerializedProperty property = new SerializedObject(asset).GetIterator();
            StringBuilder input = new StringBuilder();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyPath == excludedPropertyPath
                    || property.propertyPath.StartsWith(
                        excludedPropertyPath + ".",
                        StringComparison.Ordinal))
                {
                    enterChildren = false;
                    continue;
                }

                input.Append(property.propertyPath);
                input.Append('|');
                input.Append(property.propertyType);
                input.Append('|');
                AppendSerializedValue(input, property);
                input.Append('\n');
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(input.ToString()));
        }

        private static void AppendSerializedValue(
            StringBuilder input,
            SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    input.Append(property.longValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.Boolean:
                    input.Append(property.boolValue ? "1" : "0");
                    break;
                case SerializedPropertyType.Float:
                    input.Append(property.doubleValue.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.String:
                    input.Append(property.stringValue);
                    break;
                case SerializedPropertyType.Enum:
                    input.Append(property.enumValueIndex.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.ObjectReference:
                    string guid;
                    long localId;
                    if (property.objectReferenceValue != null
                        && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            property.objectReferenceValue,
                            out guid,
                            out localId))
                    {
                        input.Append(guid);
                        input.Append(':');
                        input.Append(localId.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        input.Append("null");
                    }

                    break;
                default:
                    input.Append(property.type);
                    if (property.isArray)
                    {
                        input.Append(':');
                        input.Append(property.arraySize.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        private static List<string> FindAssetReferences(
            string targetGuid,
            string targetPath)
        {
            List<string> references = new List<string>();
            if (string.IsNullOrEmpty(targetGuid) || string.IsNullOrEmpty(targetPath))
            {
                return references;
            }

            string[] paths = AssetDatabase.GetAllAssetPaths();
            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                    || path == targetPath
                    || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(path, false);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    if (dependencies[dependencyIndex] == targetPath)
                    {
                        references.Add(path);
                        break;
                    }
                }
            }

            references.Sort(StringComparer.Ordinal);
            return references;
        }

        private static bool TryParseSeconds(string value, out double seconds)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.EndsWith("초", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1).Trim();
            }

            return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out seconds);
        }

        private static bool SkillNumbersEqual(string left, string right)
        {
            double leftValue;
            double rightValue;
            return double.TryParse(
                       left,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out leftValue)
                   && double.TryParse(
                       right,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out rightValue)
                   && Approximately(leftValue, rightValue);
        }

        private static bool Approximately(double left, double right)
        {
            return Math.Abs(left - right) <= 0.0001d;
        }

        private static bool IsUnityGuid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool digit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetCount(Dictionary<string, int> values, string key)
        {
            int value;
            values.TryGetValue(key, out value);
            return value;
        }

        private static string ComputeAssetFileSha256(string assetPath)
        {
            string absolutePath = GetAbsolutePath(assetPath);
            return File.Exists(absolutePath)
                ? ComputeSha256(File.ReadAllBytes(absolutePath))
                : string.Empty;
        }

        private static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder output = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    output.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return output.ToString();
            }
        }

        private static void AddDiagnostics(
            string label,
            IReadOnlyList<string> diagnostics,
            List<string> errors)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                errors.Add(label + " 실패 진단이 없습니다.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                errors.Add(label + ": " + diagnostics[index]);
            }
        }

        private static void AppendInspectionChecks(
            StringBuilder output,
            MigrationInspection inspection,
            string prefix)
        {
            if (inspection == null)
            {
                return;
            }

            for (int index = 0; index < inspection.Checks.Count; index++)
            {
                MigrationDiagnosticCheck check = inspection.Checks[index];
                output.AppendLine(
                    prefix + ": " + check.Name
                    + " | expected=" + check.Expected
                    + " | actual=" + check.Actual
                    + " | " + (check.Passed ? "PASS" : "FAIL"));
            }
        }

        private static void LogInspection(
            string phase,
            MigrationInspection inspection,
            IReadOnlyList<string> errors,
            bool passed)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill09 Existing Staff Migration " + phase + "]");
            output.AppendLine("Policy: " + PolicyMarker);
            if (inspection != null)
            {
                output.AppendLine(
                    "Official PackageFingerprint: " + inspection.PackageFingerprint);
                output.AppendLine("Migration State: " + inspection.State);
                output.AppendLine("- STAFF27 Active: " + ActivePath + " (" + inspection.ActiveGuid + ")");
                output.AppendLine("- GlobalCooking Legacy: " + LegacyGlobalPath
                                  + " (" + inspection.LegacyGlobalGuid + ")");
                output.AppendLine("- Existing SpeedUp Legacy: " + ExistingLegacySpeedPath
                                  + " (" + ExistingLegacySpeedGuid + ")");
                output.AppendLine("- Official Effect/Duration/Cooldown: 50/1/240");
                AppendInspectionChecks(output, inspection, "CHECK");

                for (int index = 0; index < inspection.StateReasons.Count; index++)
                {
                    output.AppendLine("STATE REASON: " + inspection.StateReasons[index]);
                }
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            output.AppendLine(
                "SKILL09 EXISTING STAFF MIGRATION " + phase + ": "
                + (inspection != null && inspection.State == MigrationState.ALREADY_APPLIED
                    ? "ALREADY_APPLIED"
                    : passed ? "PASS" : "FAIL"));
            output.AppendLine("Asset write: 0");
            if (passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static void LogApplySuccess(
            MigrationInspection before,
            MigrationInspection after,
            MigrationBackup backup)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill09 Existing Staff Migration APPLY]");
            output.AppendLine("Policy: " + PolicyMarker);
            output.AppendLine("APPLY PASS");
            output.AppendLine("- STAFF27 GlobalCooking Legacy 이동 및 GUID 보존: PASS");
            output.AppendLine("- GlobalRemainingCookingTimeReductionSkill 생성: PASS");
            output.AppendLine("- STAFF27._skill 단독 교체: PASS");
            output.AppendLine("- 기존 Legacy 17개 불변: PASS");
            output.AppendLine("- Active Skill / Legacy Skill: 32 / 18");
            output.AppendLine("- Dry Run Policy: " + StaffDataDryRunPlanSnapshot.V8PolicyVersion);
            output.AppendLine("- Previous Active GUID: " + before.ActiveGuid);
            output.AppendLine("- New Active GUID: " + after.ActiveGuid);
            output.AppendLine("- Preserved Legacy GUID: " + after.LegacyGlobalGuid);
            output.AppendLine("- Pre-Move Active Asset SHA: "
                              + backup.OriginalFileHashes[ActivePath]);
            output.AppendLine("- Post-Move Legacy Asset SHA: "
                              + after.LegacyGlobalAssetSha256);
            output.AppendLine("- Post-Move Legacy semantic comparison: PASS");
            output.AppendLine("- Post-Move Legacy meta/GUID preservation: PASS");
            output.AppendLine("- Existing SpeedUp Legacy Asset SHA: "
                              + after.ExistingSpeedLegacyAssetSha256);
            output.AppendLine("- Existing SpeedUp Legacy meta SHA: "
                              + after.ExistingSpeedLegacyMetaSha256);
            Debug.Log(output.ToString());
        }

        private enum MigrationState
        {
            INVALID,
            READY_TO_APPLY,
            ALREADY_APPLIED,
            PARTIAL_MIGRATION_STATE
        }

        private sealed class MigrationInspection
        {
            internal string PackageFingerprint = string.Empty;
            internal bool OfficialSnapshotValid;
            internal bool PackageFingerprintValid;
            internal bool OfficialSkill09Valid;
            internal bool RuntimeScriptExists;
            internal bool RuntimeScriptGuidValid;
            internal bool RuntimeScriptClassValid;
            internal string RuntimeScriptGuid = string.Empty;
            internal string RuntimeScriptClassName = string.Empty;
            internal bool StaffDataAssetExists;
            internal bool StaffDataMetaExists;
            internal bool StaffDataGuidValid;
            internal bool StaffDataObjectValid;
            internal bool StaffSkillPropertyExists;
            internal string StaffDataGuid = string.Empty;
            internal StaffData StaffDataAsset;
            internal bool StaffReferenceObjectExists;
            internal string StaffReferencePath = string.Empty;
            internal string StaffReferenceGuid = string.Empty;
            internal bool ActiveAssetExists;
            internal bool ActiveMetaExists;
            internal string ActiveGuid = string.Empty;
            internal SkillBase ActiveAsset;
            internal string ActiveClassName = string.Empty;
            internal string ActiveScriptGuid = string.Empty;
            internal string ActiveObjectName = string.Empty;
            internal string ActiveDescription = string.Empty;
            internal double ActiveEffect = double.NaN;
            internal double ActiveDuration = double.NaN;
            internal double ActiveCooldown = double.NaN;
            internal bool ActiveSerializedPropertiesValid;
            internal int ActiveReferenceCount;
            internal bool LegacyGlobalAssetExists;
            internal bool LegacyGlobalMetaExists;
            internal string LegacyGlobalGuid = string.Empty;
            internal SkillBase LegacyGlobalAsset;
            internal string LegacyGlobalClassName = string.Empty;
            internal string LegacyGlobalScriptGuid = string.Empty;
            internal string LegacyGlobalObjectName = string.Empty;
            internal string LegacyGlobalDescription = string.Empty;
            internal double LegacyGlobalEffect = double.NaN;
            internal double LegacyGlobalDuration = double.NaN;
            internal double LegacyGlobalCooldown = double.NaN;
            internal bool LegacyGlobalSerializedPropertiesValid;
            internal bool LegacyGlobalEffectFieldExists;
            internal string LegacyGlobalAssetSha256 = string.Empty;
            internal string LegacyGlobalMetaSha256 = string.Empty;
            internal int LegacyGlobalReferenceCount;
            internal bool ExistingSpeedLegacyAssetExists;
            internal bool ExistingSpeedLegacyMetaExists;
            internal string ExistingSpeedLegacyGuid = string.Empty;
            internal SpeedUpSkill ExistingSpeedLegacyAsset;
            internal string ExistingSpeedLegacyAssetSha256 = string.Empty;
            internal string ExistingSpeedLegacyMetaSha256 = string.Empty;
            internal int ExistingSpeedLegacyReferenceCount;
            internal bool ExistingSpeedLegacyValid;
            internal bool HasDuplicateGuid;
            internal bool HasFatalDuplicateGuid;
            internal bool MissingScript;
            internal bool RequiredSerializedPropertyMissing;
            internal MigrationState State;
            internal List<string> StateReasons = new List<string>();
            internal List<MigrationDiagnosticCheck> Checks =
                new List<MigrationDiagnosticCheck>();
        }

        private sealed class MigrationBackup
        {
            internal IReadOnlyDictionary<string, byte[]> OriginalFiles { get; }
            internal IReadOnlyDictionary<string, string> OriginalFileHashes { get; }
            internal string StaffDataGuid { get; }
            internal string StaffDataFullFingerprint { get; }
            internal string StaffDataNonSkillFingerprint { get; }
            internal string StaffReferenceGuid { get; }
            internal string ActiveClassName { get; }
            internal string ActiveGuid { get; }
            internal double ActiveEffect { get; }
            internal double ActiveDuration { get; }
            internal double ActiveCooldown { get; }
            internal string ActiveNonNameFingerprint { get; }
            internal string ExistingSpeedLegacyClassName { get; }
            internal string ExistingSpeedLegacyGuid { get; }
            internal double ExistingSpeedLegacyEffect { get; }
            internal double ExistingSpeedLegacyDuration { get; }
            internal double ExistingSpeedLegacyCooldown { get; }
            internal IReadOnlyDictionary<string, string> LegacyFileHashes { get; }

            internal MigrationBackup(
                IReadOnlyDictionary<string, byte[]> originalFiles,
                IReadOnlyDictionary<string, string> originalFileHashes,
                string staffDataGuid,
                string staffDataFullFingerprint,
                string staffDataNonSkillFingerprint,
                string staffReferenceGuid,
                string activeClassName,
                string activeGuid,
                double activeEffect,
                double activeDuration,
                double activeCooldown,
                string activeNonNameFingerprint,
                string existingSpeedLegacyClassName,
                string existingSpeedLegacyGuid,
                double existingSpeedLegacyEffect,
                double existingSpeedLegacyDuration,
                double existingSpeedLegacyCooldown,
                IReadOnlyDictionary<string, string> legacyFileHashes)
            {
                OriginalFiles = originalFiles;
                OriginalFileHashes = originalFileHashes;
                StaffDataGuid = staffDataGuid;
                StaffDataFullFingerprint = staffDataFullFingerprint;
                StaffDataNonSkillFingerprint = staffDataNonSkillFingerprint;
                StaffReferenceGuid = staffReferenceGuid;
                ActiveClassName = activeClassName;
                ActiveGuid = activeGuid;
                ActiveEffect = activeEffect;
                ActiveDuration = activeDuration;
                ActiveCooldown = activeCooldown;
                ActiveNonNameFingerprint = activeNonNameFingerprint;
                ExistingSpeedLegacyClassName = existingSpeedLegacyClassName;
                ExistingSpeedLegacyGuid = existingSpeedLegacyGuid;
                ExistingSpeedLegacyEffect = existingSpeedLegacyEffect;
                ExistingSpeedLegacyDuration = existingSpeedLegacyDuration;
                ExistingSpeedLegacyCooldown = existingSpeedLegacyCooldown;
                LegacyFileHashes = legacyFileHashes;
            }
        }

        private sealed class MigrationDiagnosticCheck
        {
            internal string Name { get; }
            internal string Expected { get; }
            internal string Actual { get; }
            internal bool Passed { get; }

            internal MigrationDiagnosticCheck(
                string name,
                string expected,
                string actual,
                bool passed)
            {
                Name = name;
                Expected = expected;
                Actual = actual;
                Passed = passed;
            }
        }
    }
}
