using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffSkill04OfficialEffectMigrationTool
    {
        private const string PolicyMarker =
            "SKILL04_BALANCE_MIGRATION_PREVIEW_POLICY_2026_08_22_V1";
        private const string ApplyPolicyMarker =
            "SKILL04_BALANCE_MIGRATION_APPLY_POLICY_2026_08_22_V1";
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Skill04 Official Effect Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Skill04 Official Effect Migration";
        private const string ExpectedPackageFingerprint =
            "be7613e884b5ae18dc94e57abc0c941dccfb09486ae9fc5ff75acf4b0e4703af";
        private const string OfficialSkillId = "STAFF_SKILL04";
        private const string CurrentDescription =
            "맡은 주방 음식 제작 속도 (150%) 증가";
        private const string TargetDescription =
            "맡은 주방 음식 제작 속도 (250%) 증가";
        private const string OfficialStaffDescription =
            "담당 음식 제작 속도 증가!";
        private const string RuntimeScriptPath =
            "Assets/Scripts/Staff/StaffSkill/AssignedCookingSpeedUpSkill.cs";
        private const string RuntimeScriptGuid =
            "f6dec9edb1244c84d99fc7f5daea02f9";
        private const float CurrentEffectPercent = 150f;
        private const float TargetEffectPercent = 250f;

        private static readonly MigrationTarget[] Targets =
        {
            new MigrationTarget(
                "STAFF17",
                "염소아치",
                "Assets/Scripts/Datas/Staff/Skill/Staff17Skill.asset",
                "0122b7b6c7b22b840b305232220580ae",
                "STAFF17Skill",
                25f,
                160f,
                "Assets/Scripts/Datas/Staff/LegacySkill/Staff17Skill.asset",
                "c1305190b57c1d54482ece9b2e58be3d",
                18f,
                200f),
            new MigrationTarget(
                "STAFF19",
                "양아치",
                "Assets/Scripts/Datas/Staff/Skill/Staff19Skill.asset",
                "661820f63879f5944aed60a31747802b",
                "STAFF19Skill",
                25f,
                160f,
                "Assets/Scripts/Datas/Staff/LegacySkill/Staff19Skill.asset",
                "67fad8354daa0194fbbcf5833b9ebdca",
                24f,
                150f),
            new MigrationTarget(
                "STAFF20",
                "포코",
                "Assets/Scripts/Datas/Staff/Skill/Staff20Skill.asset",
                "210c460a6e1acc94b9299eb0fb837e6a",
                "STAFF20Skill",
                25f,
                160f,
                "Assets/Scripts/Datas/Staff/LegacySkill/Staff20Skill.asset",
                "ab2b64bcb83dc9d48b5773f0c88a830e",
                27f,
                150f),
            new MigrationTarget(
                "STAFF29",
                "셰프 바라",
                "Assets/Scripts/Datas/Staff/Skill/STAFF29SKILL.asset",
                "adf7562747b72a540af03bf2d2d37471",
                "STAFF29SKILL",
                30f,
                150f,
                "Assets/Scripts/Datas/Staff/LegacySkill/STAFF29SKILL.asset",
                "6513e175122c20641a60cad9e71895fa",
                30f,
                150f)
        };

        private static readonly HashSet<string> ExpectedNewStaffIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF46", "STAFF47", "STAFF48", "STAFF59", "STAFF60",
                "STAFF70", "STAFF79", "STAFF87", "STAFF88"
            };

        [MenuItem(PreviewMenuPath)]
        private static void PreviewMigration()
        {
            MigrationInspection inspection = new MigrationInspection();
            List<string> warnings = new List<string>();
            List<string> errors = new List<string>();
            try
            {
                Inspect(inspection, warnings, errors);
            }
            catch (Exception exception)
            {
                inspection.State = MigrationState.INVALID;
                errors.Add(
                    "SKILL04_PREVIEW_INSPECTION_FAILED: "
                    + exception.GetType().Name + " - " + exception.Message);
            }

            LogPreview(inspection, warnings, errors);
        }

        [MenuItem(ApplyMenuPath)]
        private static void ApplyMigrationFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "[Skill04 Official Effect Migration APPLY]\n"
                    + "Play Mode에서는 Apply를 실행할 수 없습니다.\n"
                    + "Asset write: 0\n"
                    + "SKILL04 OFFICIAL EFFECT MIGRATION APPLY: FAIL");
                return;
            }

            List<string> warnings = new List<string>();
            List<string> errors = new List<string>();
            MigrationInspection inspection = InspectSafely(
                "SKILL04_APPLY_INSPECTION_FAILED",
                warnings,
                errors);
            if (inspection.State == MigrationState.ALREADY_APPLIED && errors.Count == 0)
            {
                LogApplyResult(
                    inspection,
                    warnings,
                    errors,
                    true,
                    "ALREADY_APPLIED",
                    0);
                return;
            }

            if (inspection.State != MigrationState.READY_TO_APPLY || errors.Count != 0)
            {
                LogApplyResult(
                    inspection,
                    warnings,
                    errors,
                    false,
                    "Apply가 차단된 상태입니다.",
                    0);
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Skill04 Official Effect Migration",
                "Active Skill04 Asset 4개를 공식 효과로 전환합니다.\n\n"
                + "- Description 4개 수정\n"
                + "- Effect 4개: 150 → 250\n"
                + "- 총 Serialized Field 8개 수정\n"
                + "- Duration·Cooldown 변경 없음\n"
                + "- GUID 변경 없음\n"
                + "- StaffData 변경 없음\n"
                + "- LegacySkill 변경 없음\n\n"
                + "계속하시겠습니까?",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                Debug.LogWarning(
                    "[Skill04 Official Effect Migration APPLY]\n"
                    + "사용자가 Apply를 취소했습니다.\n"
                    + "Asset write: 0");
                return;
            }

            ExecuteApply();
        }

        private static MigrationInspection InspectSafely(
            string failureMarker,
            List<string> warnings,
            List<string> errors)
        {
            MigrationInspection inspection = new MigrationInspection();
            try
            {
                Inspect(inspection, warnings, errors);
            }
            catch (Exception exception)
            {
                inspection.State = MigrationState.INVALID;
                errors.Add(
                    failureMarker + ": "
                    + exception.GetType().Name + " - " + exception.Message);
            }

            return inspection;
        }

        private static void ExecuteApply()
        {
            List<string> warnings = new List<string>();
            List<string> errors = new List<string>();
            MigrationInspection finalPreflight = InspectSafely(
                "SKILL04_APPLY_FINAL_PREFLIGHT_FAILED",
                warnings,
                errors);
            if (finalPreflight.State != MigrationState.READY_TO_APPLY
                || errors.Count != 0)
            {
                errors.Add(
                    "APPLY_PREFLIGHT_STATE_CHANGED: Apply 직전 상태가 READY_TO_APPLY가 아닙니다.");
                LogApplyResult(
                    finalPreflight,
                    warnings,
                    errors,
                    false,
                    "Apply 직전 재검사 실패.",
                    0);
                return;
            }

            MigrationBackup backup;
            if (!TryCaptureBackup(out backup, errors))
            {
                LogApplyResult(
                    finalPreflight,
                    warnings,
                    errors,
                    false,
                    "Apply Snapshot 생성 실패.",
                    0);
                return;
            }

            List<PreparedTarget> prepared;
            if (!TryPrepareTargets(backup, out prepared, errors))
            {
                LogApplyResult(
                    finalPreflight,
                    warnings,
                    errors,
                    false,
                    "대상 Property 사전 검사 실패.",
                    0);
                return;
            }

            bool writeStarted = false;
            try
            {
                writeStarted = true;
                WriteOfficialValues(prepared);

                List<string> postWarnings = new List<string>();
                List<string> postErrors = new List<string>();
                MigrationInspection postInspection = InspectSafely(
                    "SKILL04_POST_APPLY_INSPECTION_FAILED",
                    postWarnings,
                    postErrors);
                if (postInspection.State != MigrationState.ALREADY_APPLIED
                    || postErrors.Count != 0)
                {
                    AddMessages("Post-Apply", postWarnings, warnings);
                    AddMessages("Post-Apply", postErrors, errors);
                    throw new InvalidOperationException(
                        "Post-Apply 상태가 ALREADY_APPLIED가 아닙니다.");
                }

                MigrationBackup after;
                if (!TryCaptureBackup(out after, errors)
                    || !ValidateBackupInvariants(
                        backup,
                        after,
                        true,
                        errors))
                {
                    throw new InvalidOperationException(
                        "Post-Apply 불변성 검증에 실패했습니다.");
                }

                LogApplySuccess(postInspection, postWarnings);
            }
            catch (Exception exception)
            {
                errors.Add(
                    "SKILL04_EFFECT_APPLY_FAILED: "
                    + exception.GetType().Name + " - " + exception.Message);
                if (!writeStarted)
                {
                    LogApplyResult(
                        finalPreflight,
                        warnings,
                        errors,
                        false,
                        "Apply 실패. Asset write: 0",
                        0);
                    return;
                }

                List<string> rollbackErrors = new List<string>();
                bool rollbackPassed = TryRollback(backup, rollbackErrors);
                if (rollbackPassed)
                {
                    Debug.Log("SKILL04_EFFECT_ROLLBACK: PASS");
                    AddMessages("Apply", errors, rollbackErrors);
                    Debug.LogError(BuildFailureLog(
                        "Apply 실패 후 원래 상태로 복구했습니다.",
                        rollbackErrors));
                }
                else
                {
                    AddMessages("Apply", errors, rollbackErrors);
                    rollbackErrors.Add("CRITICAL_SKILL04_EFFECT_ROLLBACK_FAILED");
                    Debug.LogError(BuildFailureLog(
                        "CRITICAL_SKILL04_EFFECT_ROLLBACK_FAILED",
                        rollbackErrors));
                }
            }
        }

        private static void Inspect(
            MigrationInspection inspection,
            List<string> warnings,
            List<string> errors)
        {
            bool officialValid = InspectOfficial(inspection, warnings, errors);
            bool runtimeValid = InspectRuntime(inspection, errors);

            Dictionary<string, List<string>> references = BuildReferenceMap();
            int readyCount = 0;
            int alreadyCount = 0;
            int partialCount = 0;
            int invalidCount = 0;
            for (int index = 0; index < Targets.Length; index++)
            {
                TargetInspection target = InspectTarget(Targets[index], references, errors);
                inspection.Targets.Add(target);
                switch (target.State)
                {
                    case ActiveState.READY:
                        readyCount++;
                        break;
                    case ActiveState.ALREADY:
                        alreadyCount++;
                        break;
                    case ActiveState.PARTIAL:
                        partialCount++;
                        break;
                    default:
                        invalidCount++;
                        break;
                }
            }

            inspection.ReadyCount = readyCount;
            inspection.AlreadyCount = alreadyCount;
            if (!officialValid || !runtimeValid || invalidCount != 0)
            {
                inspection.State = MigrationState.INVALID;
                return;
            }

            if (partialCount != 0 || (readyCount != 0 && alreadyCount != 0))
            {
                inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
                return;
            }

            if (readyCount == Targets.Length)
            {
                inspection.State = MigrationState.READY_TO_APPLY;
                return;
            }

            if (alreadyCount == Targets.Length)
            {
                inspection.State = MigrationState.ALREADY_APPLIED;
                return;
            }

            inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: READY " + readyCount
                + ", ALREADY " + alreadyCount
                + ", PARTIAL " + partialCount + ".");
        }

        private static bool InspectOfficial(
            MigrationInspection inspection,
            List<string> warnings,
            List<string> errors)
        {
            string activeFolder;
            StaffOfficialDataSourceKind sourceKind;
            string resolveError;
            if (!StaffOfficialDataPathResolver.TryResolveActiveFolder(
                    out activeFolder,
                    out sourceKind,
                    out resolveError))
            {
                errors.Add(
                    "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: "
                    + "Active Folder를 해석할 수 없습니다: " + resolveError);
                return false;
            }

            inspection.ActiveFolder = activeFolder;
            inspection.SourceKind = sourceKind;
            if (sourceKind == StaffOfficialDataSourceKind.SessionOverride)
            {
                warnings.Add("NON_CANONICAL_OVERRIDE: " + activeFolder);
            }

            StaffOfficialDataPackageSnapshot snapshot;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataPackValidator.TryBuildCanonicalV8ReadOnlySnapshot(
                    out snapshot,
                    out diagnostics)
                || snapshot == null)
            {
                AddDiagnostics("Official Snapshot", diagnostics, errors);
                errors.Add("OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: Canonical V8 Snapshot 생성 실패.");
                return false;
            }

            inspection.PackageFingerprint = snapshot.PackageFingerprint;
            bool valid = true;
            if (!PathsEqual(snapshot.SourceFolder, activeFolder))
            {
                errors.Add(
                    "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: Snapshot Source Folder가 Active Folder와 다릅니다.");
                valid = false;
            }

            if (!string.Equals(
                    snapshot.PackageFingerprint,
                    ExpectedPackageFingerprint,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: PackageFingerprint 불일치. 실제 "
                    + snapshot.PackageFingerprint);
                valid = false;
            }

            StaffOfficialFileSnapshot finalStaff;
            StaffOfficialFileSnapshot skillType;
            if (!snapshot.TryGetFile(StaffOfficialDataPackageKeys.FinalStaff, out finalStaff)
                || !snapshot.TryGetFile(StaffOfficialDataPackageKeys.SkillType, out skillType))
            {
                errors.Add(
                    "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: FinalStaff 또는 SkillType Snapshot이 없습니다.");
                return false;
            }

            int definitionCount = 0;
            for (int index = 0; index < skillType.Rows.Count; index++)
            {
                IReadOnlyList<string> row = skillType.Rows[index];
                if (row.Count >= 2 && Trim(row[0]) == OfficialSkillId)
                {
                    definitionCount++;
                    if (!string.Equals(Trim(row[1]), TargetDescription, StringComparison.Ordinal))
                    {
                        errors.Add(
                            "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: SkillType 설명 불일치: "
                            + Trim(row[1]));
                        valid = false;
                    }
                }
            }

            if (definitionCount != 1)
            {
                errors.Add(
                    "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: Skill04 정의 수 "
                    + definitionCount + ".");
                valid = false;
            }

            Dictionary<string, IReadOnlyList<string>> skillRows =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int index = 0; index < finalStaff.Rows.Count; index++)
            {
                IReadOnlyList<string> row = finalStaff.Rows[index];
                if (row.Count < 14 || Trim(row[10]) != OfficialSkillId)
                {
                    continue;
                }

                string staffId = Trim(row[0]);
                if (skillRows.ContainsKey(staffId))
                {
                    errors.Add(
                        "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: FinalStaff 중복 ID " + staffId + ".");
                    valid = false;
                    continue;
                }

                skillRows.Add(staffId, row);
                string userDescription = Trim(row[11]);
                if (Trim(row[6]) != "주방장"
                    || !string.Equals(
                        userDescription,
                        OfficialStaffDescription,
                        StringComparison.Ordinal)
                    || ContainsForbiddenOfficialNumber(userDescription))
                {
                    errors.Add(
                        "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: " + staffId
                        + " 역할 또는 사용자 설명 불일치.");
                    valid = false;
                }
            }

            HashSet<string> expectedExisting = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                expectedExisting.Add(target.StaffId);
                IReadOnlyList<string> row;
                double duration;
                double cooldown;
                if (!skillRows.TryGetValue(target.StaffId, out row)
                    || row.Count < 14
                    || Trim(row[1]) != target.OfficialName
                    || !TryParseSeconds(row[12], out duration)
                    || !TryParseSeconds(row[13], out cooldown)
                    || !Approximately(duration, target.Duration)
                    || !Approximately(cooldown, target.Cooldown))
                {
                    errors.Add(
                        "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: 기존 대상 불일치 "
                        + target.StaffId + ".");
                    valid = false;
                }
            }

            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            foreach (string staffId in skillRows.Keys)
            {
                if (!expectedExisting.Contains(staffId))
                {
                    actualNew.Add(staffId);
                }
            }

            if (skillRows.Count != 13 || !actualNew.SetEquals(ExpectedNewStaffIds))
            {
                errors.Add(
                    "OFFICIAL_SKILL04_BALANCE_TARGET_CHANGED: 전체 " + skillRows.Count
                    + ", 기존 " + expectedExisting.Count
                    + ", 신규 " + actualNew.Count + ".");
                valid = false;
            }

            return valid;
        }

        private static bool InspectRuntime(
            MigrationInspection inspection,
            List<string> errors)
        {
            Type skillType = typeof(AssignedCookingSpeedUpSkill);
            FieldInfo[] declaredFields = skillType.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
            FieldInfo percentField = skillType.GetField(
                "_assignedCookingSpeedUpPercent",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            RangeAttribute range = percentField != null
                ? percentField.GetCustomAttribute<RangeAttribute>()
                : null;
            PropertyInfo firstValue = skillType.GetProperty("FirstValue");
            PropertyInfo secondValue = skillType.GetProperty("SecondValue");
            string scriptGuid = AssetDatabase.AssetPathToGUID(RuntimeScriptPath);
            inspection.RuntimeScriptGuid = scriptGuid;

            string source = ReadProjectText(RuntimeScriptPath);
            source = NormalizeLineEndings(source);
            bool valid = skillType.BaseType == typeof(SkillBase)
                         && declaredFields.Length == 1
                         && percentField != null
                         && percentField.FieldType == typeof(float)
                         && percentField.GetCustomAttribute<SerializeField>() != null
                         && range != null
                         && Approximately(range.min, 0f)
                         && Approximately(range.max, 1000f)
                         && firstValue != null
                         && firstValue.GetSetMethod(true) == null
                         && secondValue != null
                         && secondValue.GetSetMethod(true) == null
                         && scriptGuid == RuntimeScriptGuid
                         && AssetFileExists(RuntimeScriptPath + ".meta")
                         && source.Contains(
                             "private float _assignedCookingSpeedUpPercent = 250f;")
                         && source.Contains(
                             "public override float FirstValue => _assignedCookingSpeedUpPercent;")
                         && source.Contains("public override float SecondValue => 0;")
                         && CountOccurrences(
                             source,
                             "staff.RuntimeSkillContext.SetAssignedCookingBonusPercent(") == 2
                         && CountOccurrences(source, "staff.CurrentSkillSourceToken") == 2
                         && CountOccurrences(
                             source,
                             "            _assignedCookingSpeedUpPercent);") == 1
                         && CountOccurrences(
                             source,
                             "            0f);") == 1
                         && source.Contains(
                             "public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)\n"
                             + "    {\n"
                             + "    }");
            inspection.RuntimeDefaultPercent = valid ? TargetEffectPercent : float.NaN;
            if (!valid)
            {
                errors.Add(
                    "SKILL04_RUNTIME_BASELINE_CHANGED: Runtime 구조·기본값 또는 Script GUID가 다릅니다. 실제 GUID "
                    + scriptGuid + ".");
            }

            return valid;
        }

        private static TargetInspection InspectTarget(
            MigrationTarget target,
            IReadOnlyDictionary<string, List<string>> references,
            List<string> errors)
        {
            TargetInspection result = new TargetInspection(target);
            List<string> activeReferences = GetReferences(references, target.ActivePath);
            List<string> legacyReferences = GetReferences(references, target.LegacyPath);
            result.ReferenceStaff = FormatReferenceStaff(activeReferences);

            bool activeStructureValid = InspectActiveAsset(
                result,
                activeReferences,
                errors);
            bool legacyValid = InspectLegacyAsset(
                result,
                legacyReferences,
                errors);
            if (!activeStructureValid || !legacyValid)
            {
                result.State = ActiveState.INVALID;
                return result;
            }

            bool timeMatches = Approximately(result.Duration, target.Duration)
                               && Approximately(result.Cooldown, target.Cooldown);
            bool ready = timeMatches
                         && result.CurrentDescription == CurrentDescription
                         && Approximately(result.CurrentEffect, CurrentEffectPercent);
            bool already = timeMatches
                           && result.CurrentDescription == TargetDescription
                           && Approximately(result.CurrentEffect, TargetEffectPercent);
            if (ready)
            {
                result.State = ActiveState.READY;
                return result;
            }

            if (already)
            {
                result.State = ActiveState.ALREADY;
                return result;
            }

            result.State = ActiveState.PARTIAL;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: " + target.StaffId
                + " Description='" + result.CurrentDescription
                + "', Effect=" + result.CurrentEffect.ToString("R", CultureInfo.InvariantCulture)
                + ", Duration=" + result.Duration.ToString("R", CultureInfo.InvariantCulture)
                + ", Cooldown=" + result.Cooldown.ToString("R", CultureInfo.InvariantCulture)
                + ".");
            return result;
        }

        private static bool InspectActiveAsset(
            TargetInspection result,
            IReadOnlyList<string> references,
            List<string> errors)
        {
            MigrationTarget target = result.Target;
            string assetGuid = AssetDatabase.AssetPathToGUID(target.ActivePath);
            result.ActiveGuid = assetGuid;
            UnityEngine.Object loaded = AssetDatabase.LoadMainAssetAtPath(target.ActivePath);
            AssignedCookingSpeedUpSkill skill = loaded as AssignedCookingSpeedUpSkill;
            string metaPath = target.ActivePath + ".meta";
            bool metaExists = AssetFileExists(metaPath);
            bool valid = loaded != null
                         && loaded.GetType() == typeof(AssignedCookingSpeedUpSkill)
                         && assetGuid == target.ActiveGuid
                         && loaded.name == target.ObjectName
                         && metaExists
                         && IsExactSingleReference(references, target.StaffDataPath)
                         && StaffReferencesActiveSkill(target, loaded);
            if (skill == null)
            {
                valid = false;
            }
            else
            {
                SerializedObject serialized = new SerializedObject(skill);
                SerializedProperty description = serialized.FindProperty("_description");
                SerializedProperty effect = serialized.FindProperty(
                    "_assignedCookingSpeedUpPercent");
                SerializedProperty duration = serialized.FindProperty("_duration");
                SerializedProperty cooldown = serialized.FindProperty("_cooldown");
                string scriptGuid = GetScriptGuid(serialized);
                result.CurrentDescription = description != null
                    ? description.stringValue
                    : string.Empty;
                result.CurrentEffect = effect != null ? effect.floatValue : float.NaN;
                result.Duration = duration != null ? duration.floatValue : float.NaN;
                result.Cooldown = cooldown != null ? cooldown.floatValue : float.NaN;
                valid &= description != null
                         && effect != null
                         && duration != null
                         && cooldown != null
                         && scriptGuid == RuntimeScriptGuid
                         && !HasMissingSerializedReference(serialized)
                         && Approximately(skill.FirstValue, result.CurrentEffect)
                         && Approximately(skill.Duration, result.Duration)
                         && Approximately(skill.Cooldown, result.Cooldown)
                         && skill.Description == result.CurrentDescription;
            }

            if (!valid)
            {
                errors.Add(
                    "SKILL04_ACTIVE_ASSET_BASELINE_CHANGED: " + target.StaffId
                    + " Path=" + target.ActivePath
                    + ", GUID=" + assetGuid
                    + ", ReferenceCount=" + references.Count + ".");
            }

            return valid;
        }

        private static bool InspectLegacyAsset(
            TargetInspection result,
            IReadOnlyList<string> references,
            List<string> errors)
        {
            MigrationTarget target = result.Target;
            string legacyGuid = AssetDatabase.AssetPathToGUID(target.LegacyPath);
            result.LegacyGuid = legacyGuid;
            result.LegacyAssetSha256 = ComputeAssetFileSha256(target.LegacyPath);
            result.LegacyMetaSha256 = ComputeAssetFileSha256(target.LegacyPath + ".meta");
            UnityEngine.Object loaded = AssetDatabase.LoadMainAssetAtPath(target.LegacyPath);
            SpeedUpSkill skill = loaded as SpeedUpSkill;
            bool valid = loaded != null
                         && loaded.GetType() == typeof(SpeedUpSkill)
                         && legacyGuid == target.LegacyGuid
                         && loaded.name == target.ObjectName
                         && AssetFileExists(target.LegacyPath + ".meta")
                         && references.Count == 0
                         && !string.IsNullOrEmpty(result.LegacyAssetSha256)
                         && !string.IsNullOrEmpty(result.LegacyMetaSha256);
            if (skill == null)
            {
                valid = false;
            }
            else
            {
                SerializedObject serialized = new SerializedObject(skill);
                valid &= !string.IsNullOrEmpty(GetScriptGuid(serialized))
                         && !HasMissingSerializedReference(serialized)
                         && Approximately(skill.Duration, target.LegacyDuration)
                         && Approximately(skill.Cooldown, target.LegacyCooldown)
                         && Approximately(skill.FirstValue, 100f);
            }

            if (!valid)
            {
                errors.Add(
                    "SKILL04_LEGACY_BASELINE_CHANGED: " + target.StaffId
                    + " Path=" + target.LegacyPath
                    + ", GUID=" + legacyGuid
                    + ", ReferenceCount=" + references.Count + ".");
            }

            return valid;
        }

        private static bool StaffReferencesActiveSkill(
            MigrationTarget target,
            UnityEngine.Object activeSkill)
        {
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
            if (staff == null)
            {
                return false;
            }

            SerializedObject serialized = new SerializedObject(staff);
            SerializedProperty skillReference = serialized.FindProperty("_skill");
            return skillReference != null
                   && skillReference.objectReferenceValue == activeSkill
                   && !HasMissingSerializedReference(serialized);
        }

        private static bool TryCaptureBackup(
            out MigrationBackup backup,
            List<string> errors)
        {
            backup = new MigrationBackup();
            Dictionary<string, List<string>> references = BuildReferenceMap();
            bool valid = true;
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                AssignedCookingSpeedUpSkill active =
                    AssetDatabase.LoadAssetAtPath<AssignedCookingSpeedUpSkill>(
                        target.ActivePath);
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(
                    target.StaffDataPath);
                SpeedUpSkill legacy = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(
                    target.LegacyPath);
                if (active == null || staff == null || legacy == null)
                {
                    errors.Add(
                        "SKILL04_APPLY_SNAPSHOT_FAILED: Asset을 읽을 수 없습니다: "
                        + target.StaffId + ".");
                    valid = false;
                    continue;
                }

                SerializedObject activeSerialized = new SerializedObject(active);
                SerializedProperty description = activeSerialized.FindProperty("_description");
                SerializedProperty effect = activeSerialized.FindProperty(
                    "_assignedCookingSpeedUpPercent");
                SerializedProperty duration = activeSerialized.FindProperty("_duration");
                SerializedProperty cooldown = activeSerialized.FindProperty("_cooldown");
                SerializedObject staffSerialized = new SerializedObject(staff);
                SerializedProperty staffSkill = staffSerialized.FindProperty("_skill");
                if (description == null
                    || effect == null
                    || duration == null
                    || cooldown == null
                    || staffSkill == null
                    || staffSkill.objectReferenceValue == null)
                {
                    errors.Add(
                        "SKILL04_APPLY_SNAPSHOT_FAILED: 필수 Serialized Property가 없습니다: "
                        + target.StaffId + ".");
                    valid = false;
                    continue;
                }

                TargetBackup targetBackup = new TargetBackup(target);
                targetBackup.ActiveGuid = AssetDatabase.AssetPathToGUID(target.ActivePath);
                targetBackup.ActiveMetaSha256 = ComputeAssetFileSha256(
                    target.ActivePath + ".meta");
                targetBackup.ObjectName = active.name;
                targetBackup.ScriptGuid = GetScriptGuid(activeSerialized);
                targetBackup.ConcreteClass = active.GetType().FullName;
                targetBackup.Description = description.stringValue;
                targetBackup.Effect = effect.floatValue;
                targetBackup.Duration = duration.floatValue;
                targetBackup.Cooldown = cooldown.floatValue;
                targetBackup.NonTargetSerializedFingerprint =
                    CaptureSerializedFingerprint(
                        activeSerialized,
                        "_description",
                        "_assignedCookingSpeedUpPercent");
                List<string> activeReferences = GetReferences(
                    references,
                    target.ActivePath);
                List<string> legacyReferences = GetReferences(
                    references,
                    target.LegacyPath);
                targetBackup.ActiveReferenceCount = activeReferences.Count;
                targetBackup.ReferencedStaffId = FormatReferenceStaff(activeReferences);
                targetBackup.ActiveReferenceFingerprint = BuildReferenceFingerprint(
                    activeReferences);
                targetBackup.StaffDataAssetSha256 = ComputeAssetFileSha256(
                    target.StaffDataPath);
                targetBackup.StaffDataMetaSha256 = ComputeAssetFileSha256(
                    target.StaffDataPath + ".meta");
                targetBackup.StaffSkillReferenceGuid = GetObjectGuid(
                    staffSkill.objectReferenceValue);
                targetBackup.LegacyAssetSha256 = ComputeAssetFileSha256(
                    target.LegacyPath);
                targetBackup.LegacyMetaSha256 = ComputeAssetFileSha256(
                    target.LegacyPath + ".meta");
                targetBackup.LegacyGuid = AssetDatabase.AssetPathToGUID(
                    target.LegacyPath);
                targetBackup.LegacyReferenceCount = legacyReferences.Count;
                targetBackup.LegacyReferenceFingerprint = BuildReferenceFingerprint(
                    legacyReferences);

                if (string.IsNullOrEmpty(targetBackup.ActiveGuid)
                    || string.IsNullOrEmpty(targetBackup.ActiveMetaSha256)
                    || string.IsNullOrEmpty(targetBackup.ScriptGuid)
                    || string.IsNullOrEmpty(targetBackup.NonTargetSerializedFingerprint)
                    || string.IsNullOrEmpty(targetBackup.StaffDataAssetSha256)
                    || string.IsNullOrEmpty(targetBackup.StaffDataMetaSha256)
                    || string.IsNullOrEmpty(targetBackup.StaffSkillReferenceGuid)
                    || string.IsNullOrEmpty(targetBackup.LegacyAssetSha256)
                    || string.IsNullOrEmpty(targetBackup.LegacyMetaSha256)
                    || string.IsNullOrEmpty(targetBackup.LegacyGuid)
                    || targetBackup.ActiveReferenceCount != 1
                    || targetBackup.ReferencedStaffId != target.StaffId
                    || targetBackup.StaffSkillReferenceGuid != targetBackup.ActiveGuid
                    || targetBackup.LegacyReferenceCount != 0)
                {
                    errors.Add(
                        "SKILL04_APPLY_SNAPSHOT_FAILED: Snapshot 값이 비어 있습니다: "
                        + target.StaffId + ".");
                    valid = false;
                    continue;
                }

                backup.Targets.Add(target.StaffId, targetBackup);
            }

            return valid && backup.Targets.Count == Targets.Length;
        }

        private static bool TryPrepareTargets(
            MigrationBackup backup,
            out List<PreparedTarget> prepared,
            List<string> errors)
        {
            return TryPrepareTargets(backup, true, out prepared, errors);
        }

        private static bool TryPrepareTargets(
            MigrationBackup backup,
            bool requireOriginalValues,
            out List<PreparedTarget> prepared,
            List<string> errors)
        {
            prepared = new List<PreparedTarget>();
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup;
                if (!backup.Targets.TryGetValue(target.StaffId, out targetBackup))
                {
                    errors.Add(
                        "SKILL04_APPLY_TARGET_INVALID: Backup이 없습니다: "
                        + target.StaffId + ".");
                    return false;
                }

                AssignedCookingSpeedUpSkill active =
                    AssetDatabase.LoadAssetAtPath<AssignedCookingSpeedUpSkill>(
                        target.ActivePath);
                if (active == null
                    || active.GetType() != typeof(AssignedCookingSpeedUpSkill)
                    || active.name != targetBackup.ObjectName
                    || AssetDatabase.AssetPathToGUID(target.ActivePath)
                    != targetBackup.ActiveGuid
                    || ComputeAssetFileSha256(target.ActivePath + ".meta")
                    != targetBackup.ActiveMetaSha256)
                {
                    errors.Add(
                        "SKILL04_APPLY_TARGET_INVALID: Active Asset 기준 불일치: "
                        + target.StaffId + ".");
                    return false;
                }

                SerializedObject serialized = new SerializedObject(active);
                SerializedProperty description = serialized.FindProperty("_description");
                SerializedProperty effect = serialized.FindProperty(
                    "_assignedCookingSpeedUpPercent");
                if (description == null
                    || effect == null
                    || GetScriptGuid(serialized) != targetBackup.ScriptGuid
                    || CaptureSerializedFingerprint(
                        serialized,
                        "_description",
                        "_assignedCookingSpeedUpPercent")
                    != targetBackup.NonTargetSerializedFingerprint
                    || (requireOriginalValues
                        && (description.stringValue != targetBackup.Description
                            || !Approximately(effect.floatValue, targetBackup.Effect))))
                {
                    errors.Add(
                        "SKILL04_APPLY_TARGET_INVALID: Property 또는 비대상 필드 불일치: "
                        + target.StaffId + ".");
                    return false;
                }

                prepared.Add(
                    new PreparedTarget(
                        active,
                        serialized,
                        description,
                        effect,
                        targetBackup));
            }

            return prepared.Count == Targets.Length;
        }

        private static void WriteOfficialValues(IReadOnlyList<PreparedTarget> prepared)
        {
            for (int index = 0; index < prepared.Count; index++)
            {
                PreparedTarget target = prepared[index];
                target.Description.stringValue = TargetDescription;
                target.Effect.floatValue = TargetEffectPercent;
                if (!target.Serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    throw new InvalidOperationException(
                        "Serialized Property 적용에 실패했습니다: "
                        + target.Backup.Target.StaffId + ".");
                }

                EditorUtility.SetDirty(target.Asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void WriteBackupValues(IReadOnlyList<PreparedTarget> prepared)
        {
            for (int index = 0; index < prepared.Count; index++)
            {
                PreparedTarget target = prepared[index];
                target.Description.stringValue = target.Backup.Description;
                target.Effect.floatValue = target.Backup.Effect;
                if (!target.Serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    throw new InvalidOperationException(
                        "Rollback Property 적용에 실패했습니다: "
                        + target.Backup.Target.StaffId + ".");
                }

                EditorUtility.SetDirty(target.Asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool TryRollback(
            MigrationBackup backup,
            List<string> errors)
        {
            try
            {
                List<PreparedTarget> prepared;
                if (!TryPrepareTargets(
                        backup,
                        false,
                        out prepared,
                        errors))
                {
                    return false;
                }

                WriteBackupValues(prepared);

                List<string> inspectionWarnings = new List<string>();
                List<string> inspectionErrors = new List<string>();
                MigrationInspection restoredInspection = InspectSafely(
                    "SKILL04_ROLLBACK_INSPECTION_FAILED",
                    inspectionWarnings,
                    inspectionErrors);
                AddMessages("Rollback", inspectionErrors, errors);
                if (inspectionWarnings.Count != 0)
                {
                    Debug.LogWarning(
                        "[Skill04 Official Effect Migration ROLLBACK]\n"
                        + string.Join("\n", inspectionWarnings.ToArray()));
                }
                if (restoredInspection.State != MigrationState.READY_TO_APPLY
                    || inspectionErrors.Count != 0)
                {
                    return false;
                }

                MigrationBackup restored;
                return TryCaptureBackup(out restored, errors)
                       && ValidateBackupInvariants(
                           backup,
                           restored,
                           false,
                           errors);
            }
            catch (Exception exception)
            {
                errors.Add(
                    "CRITICAL_SKILL04_EFFECT_ROLLBACK_FAILED: "
                    + exception.GetType().Name + " - " + exception.Message);
                return false;
            }
        }

        private static bool ValidateBackupInvariants(
            MigrationBackup before,
            MigrationBackup after,
            bool expectApplied,
            List<string> errors)
        {
            bool valid = before.Targets.Count == Targets.Length
                         && after.Targets.Count == Targets.Length;
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup oldValue;
                TargetBackup newValue;
                if (!before.Targets.TryGetValue(target.StaffId, out oldValue)
                    || !after.Targets.TryGetValue(target.StaffId, out newValue))
                {
                    errors.Add(
                        "SKILL04_EFFECT_INVARIANT_CHANGED: Snapshot 누락 "
                        + target.StaffId + ".");
                    valid = false;
                    continue;
                }

                string expectedDescription = expectApplied
                    ? TargetDescription
                    : oldValue.Description;
                float expectedEffect = expectApplied
                    ? TargetEffectPercent
                    : oldValue.Effect;
                bool targetValid = newValue.ActiveGuid == oldValue.ActiveGuid
                                   && newValue.ActiveMetaSha256
                                   == oldValue.ActiveMetaSha256
                                   && newValue.ObjectName == oldValue.ObjectName
                                   && newValue.ScriptGuid == oldValue.ScriptGuid
                                   && newValue.ConcreteClass == oldValue.ConcreteClass
                                   && newValue.NonTargetSerializedFingerprint
                                   == oldValue.NonTargetSerializedFingerprint
                                   && newValue.ActiveReferenceFingerprint
                                   == oldValue.ActiveReferenceFingerprint
                                   && newValue.ActiveReferenceCount
                                   == oldValue.ActiveReferenceCount
                                   && newValue.ReferencedStaffId
                                   == oldValue.ReferencedStaffId
                                   && Approximately(newValue.Duration, oldValue.Duration)
                                   && Approximately(newValue.Cooldown, oldValue.Cooldown)
                                   && newValue.StaffDataAssetSha256
                                   == oldValue.StaffDataAssetSha256
                                   && newValue.StaffDataMetaSha256
                                   == oldValue.StaffDataMetaSha256
                                   && newValue.StaffSkillReferenceGuid
                                   == oldValue.StaffSkillReferenceGuid
                                   && newValue.LegacyAssetSha256
                                   == oldValue.LegacyAssetSha256
                                   && newValue.LegacyMetaSha256
                                   == oldValue.LegacyMetaSha256
                                   && newValue.LegacyGuid == oldValue.LegacyGuid
                                   && newValue.LegacyReferenceCount
                                   == oldValue.LegacyReferenceCount
                                   && newValue.LegacyReferenceFingerprint
                                   == oldValue.LegacyReferenceFingerprint
                                   && newValue.Description == expectedDescription
                                   && Approximately(newValue.Effect, expectedEffect);
                if (!targetValid)
                {
                    errors.Add(
                        "SKILL04_EFFECT_INVARIANT_CHANGED: "
                        + target.StaffId + ".");
                    valid = false;
                }
            }

            return valid;
        }

        private static string CaptureSerializedFingerprint(
            SerializedObject serialized,
            string excludedPropertyA,
            string excludedPropertyB)
        {
            SerializedProperty property = serialized.GetIterator();
            StringBuilder input = new StringBuilder();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (IsExcludedProperty(property.propertyPath, excludedPropertyA)
                    || IsExcludedProperty(property.propertyPath, excludedPropertyB))
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

        private static bool IsExcludedProperty(
            string propertyPath,
            string excludedProperty)
        {
            return propertyPath == excludedProperty
                   || propertyPath.StartsWith(
                       excludedProperty + ".",
                       StringComparison.Ordinal);
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
                    input.Append(
                        property.longValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.Boolean:
                    input.Append(property.boolValue ? "1" : "0");
                    break;
                case SerializedPropertyType.Float:
                    input.Append(
                        property.doubleValue.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.String:
                    input.Append(property.stringValue);
                    break;
                case SerializedPropertyType.Enum:
                    input.Append(
                        property.enumValueIndex.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.ObjectReference:
                    input.Append(GetObjectIdentity(property.objectReferenceValue));
                    break;
                default:
                    input.Append(property.type);
                    if (property.isArray)
                    {
                        input.Append(':');
                        input.Append(
                            property.arraySize.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        private static string GetObjectGuid(UnityEngine.Object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string guid;
            long localId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    value,
                    out guid,
                    out localId))
            {
                return string.Empty;
            }

            return guid;
        }

        private static string GetObjectIdentity(UnityEngine.Object value)
        {
            if (value == null)
            {
                return "null";
            }

            string guid;
            long localId;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    value,
                    out guid,
                    out localId))
            {
                return "unresolved";
            }

            return guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildReferenceFingerprint(
            IReadOnlyList<string> references)
        {
            StringBuilder input = new StringBuilder();
            for (int index = 0; index < references.Count; index++)
            {
                input.Append(references[index]);
                input.Append('\n');
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(input.ToString()));
        }

        private static Dictionary<string, List<string>> BuildReferenceMap()
        {
            Dictionary<string, List<string>> references =
                new Dictionary<string, List<string>>(StringComparer.Ordinal);
            for (int index = 0; index < Targets.Length; index++)
            {
                references.Add(Targets[index].ActivePath, new List<string>());
                references.Add(Targets[index].LegacyPath, new List<string>());
            }

            string[] paths = AssetDatabase.GetAllAssetPaths();
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                string path = paths[pathIndex];
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                    || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(path, false);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    List<string> targetReferences;
                    if (references.TryGetValue(dependencies[dependencyIndex], out targetReferences)
                        && path != dependencies[dependencyIndex])
                    {
                        targetReferences.Add(path);
                    }
                }
            }

            foreach (List<string> pathsForTarget in references.Values)
            {
                pathsForTarget.Sort(StringComparer.Ordinal);
            }

            return references;
        }

        private static List<string> GetReferences(
            IReadOnlyDictionary<string, List<string>> references,
            string path)
        {
            List<string> result;
            return references.TryGetValue(path, out result)
                ? result
                : new List<string>();
        }

        private static bool HasMissingSerializedReference(SerializedObject serialized)
        {
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType == SerializedPropertyType.ObjectReference
                    && iterator.propertyPath != "m_Script"
                    && iterator.objectReferenceValue == null
                    && iterator.objectReferenceInstanceIDValue != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetScriptGuid(SerializedObject serialized)
        {
            SerializedProperty script = serialized.FindProperty("m_Script");
            if (script == null || script.objectReferenceValue == null)
            {
                return string.Empty;
            }

            string guid;
            long localId;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                script.objectReferenceValue,
                out guid,
                out localId)
                ? guid
                : string.Empty;
        }

        private static bool IsExactSingleReference(
            IReadOnlyList<string> references,
            string expectedPath)
        {
            return references.Count == 1 && references[0] == expectedPath;
        }

        private static string FormatReferenceStaff(IReadOnlyList<string> references)
        {
            List<string> labels = new List<string>();
            for (int index = 0; index < references.Count; index++)
            {
                string path = references[index];
                labels.Add(
                    path.StartsWith("Assets/Resources/StaffData/", StringComparison.Ordinal)
                        ? Path.GetFileNameWithoutExtension(path)
                        : path);
            }

            return string.Join(", ", labels.ToArray());
        }

        private static bool ContainsForbiddenOfficialNumber(string description)
        {
            return description.IndexOf("150%", StringComparison.Ordinal) >= 0
                   || description.IndexOf("250%", StringComparison.Ordinal) >= 0
                   || description.IndexOf("3.5", StringComparison.Ordinal) >= 0
                   || description.IndexOf("350%", StringComparison.Ordinal) >= 0;
        }

        private static bool TryParseSeconds(string value, out double seconds)
        {
            string normalized = Trim(value);
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

        private static bool Approximately(double left, double right)
        {
            return Math.Abs(left - right) <= 0.0001d;
        }

        private static string Trim(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadProjectText(string assetPath)
        {
            string absolutePath = GetAbsolutePath(assetPath);
            return File.Exists(absolutePath)
                ? File.ReadAllText(absolutePath, Encoding.UTF8)
                : string.Empty;
        }

        private static bool AssetFileExists(string assetPath)
        {
            return File.Exists(GetAbsolutePath(assetPath));
        }

        private static string ComputeAssetFileSha256(string assetPath)
        {
            string absolutePath = GetAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                return string.Empty;
            }

            return ComputeSha256(File.ReadAllBytes(absolutePath));
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

        private static string GetAbsolutePath(string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    StaffOfficialDataPathResolver.ProjectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string NormalizeLineEndings(string source)
        {
            return (source ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int searchIndex = 0;
            while ((searchIndex = source.IndexOf(value, searchIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                searchIndex += value.Length;
            }

            return count;
        }

        private static void AddDiagnostics(
            string label,
            IReadOnlyList<string> diagnostics,
            List<string> errors)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                errors.Add(label + ": 실패 진단이 없습니다.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                errors.Add(label + ": " + diagnostics[index]);
            }
        }

        private static void AddMessages(
            string label,
            IReadOnlyList<string> source,
            List<string> destination)
        {
            for (int index = 0; index < source.Count; index++)
            {
                destination.Add(label + ": " + source[index]);
            }
        }

        private static void LogApplyResult(
            MigrationInspection inspection,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors,
            bool passed,
            string message,
            int assetWriteCount)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill04 Official Effect Migration APPLY]");
            output.AppendLine("Policy: " + ApplyPolicyMarker);
            output.AppendLine("Migration State: " + inspection.State);
            output.AppendLine(message);
            output.AppendLine("Asset write: " + assetWriteCount);
            for (int index = 0; index < warnings.Count; index++)
            {
                output.AppendLine("WARNING: " + warnings[index]);
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            output.AppendLine(
                "SKILL04 OFFICIAL EFFECT MIGRATION APPLY: "
                + (passed ? "PASS" : "FAIL"));
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
            MigrationInspection inspection,
            IReadOnlyList<string> warnings)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill04 Official Effect Migration APPLY]");
            output.AppendLine("Policy: " + ApplyPolicyMarker);
            output.AppendLine("Migration State: " + inspection.State);
            output.AppendLine("- 변경 Asset: 4");
            output.AppendLine("- 변경 Description: 4");
            output.AppendLine("- 변경 Effect: 4");
            output.AppendLine("- 변경 Serialized Field: 8");
            output.AppendLine("- Duration·Cooldown 변경: 0");
            output.AppendLine("- Active GUID·meta 변경: 0");
            output.AppendLine("- StaffData 변경: 0");
            output.AppendLine("- Legacy 변경: 0");
            for (int index = 0; index < warnings.Count; index++)
            {
                output.AppendLine("WARNING: " + warnings[index]);
            }

            output.AppendLine("SKILL04 OFFICIAL EFFECT MIGRATION APPLY: PASS");
            Debug.Log(output.ToString());
        }

        private static string BuildFailureLog(
            string message,
            IReadOnlyList<string> errors)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill04 Official Effect Migration APPLY]");
            output.AppendLine("Policy: " + ApplyPolicyMarker);
            output.AppendLine(message);
            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            output.AppendLine("SKILL04 OFFICIAL EFFECT MIGRATION APPLY: FAIL");
            return output.ToString();
        }

        private static void LogPreview(
            MigrationInspection inspection,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill04 Official Effect Migration PREVIEW]");
            output.AppendLine("Policy: " + PolicyMarker);
            output.AppendLine("Active Folder: " + inspection.ActiveFolder);
            output.AppendLine("SourceKind: " + inspection.SourceKind);
            output.AppendLine("Official PackageFingerprint: " + inspection.PackageFingerprint);
            output.AppendLine("Migration State: " + inspection.State);
            output.AppendLine(
                "Runtime Default: "
                + inspection.RuntimeDefaultPercent.ToString("R", CultureInfo.InvariantCulture));
            output.AppendLine("Runtime Script GUID: " + inspection.RuntimeScriptGuid);
            output.AppendLine("Official Effect: 250%");
            output.AppendLine("Official Final Multiplier: 3.5x");
            output.AppendLine();

            for (int index = 0; index < inspection.Targets.Count; index++)
            {
                TargetInspection target = inspection.Targets[index];
                output.AppendLine("- Staff ID: " + target.Target.StaffId);
                output.AppendLine("  State: " + target.State);
                output.AppendLine("  Active Path: " + target.Target.ActivePath);
                output.AppendLine("  Active GUID: " + target.ActiveGuid);
                output.AppendLine("  Current Description: " + target.CurrentDescription);
                output.AppendLine("  Target Description: " + TargetDescription);
                output.AppendLine(
                    "  Current Effect: "
                    + target.CurrentEffect.ToString("R", CultureInfo.InvariantCulture));
                output.AppendLine("  Target Effect: 250");
                output.AppendLine(
                    "  Duration / Cooldown: "
                    + target.Duration.ToString("R", CultureInfo.InvariantCulture)
                    + " / " + target.Cooldown.ToString("R", CultureInfo.InvariantCulture));
                output.AppendLine("  Reference Staff: " + target.ReferenceStaff);
                output.AppendLine("  Legacy GUID: " + target.LegacyGuid);
                output.AppendLine("  Legacy Asset SHA: " + target.LegacyAssetSha256);
                output.AppendLine("  Legacy meta SHA: " + target.LegacyMetaSha256);
            }

            int changeDescriptionCount = inspection.State == MigrationState.READY_TO_APPLY
                ? Targets.Length
                : 0;
            int changeEffectCount = inspection.State == MigrationState.READY_TO_APPLY
                ? Targets.Length
                : 0;
            output.AppendLine();
            output.AppendLine("Summary:");
            output.AppendLine("- 대상 Active Asset: " + Targets.Length);
            output.AppendLine("- READY 상태 Active: " + inspection.ReadyCount);
            output.AppendLine("- ALREADY 상태 Active: " + inspection.AlreadyCount);
            output.AppendLine("- 변경 예정 Description: " + changeDescriptionCount);
            output.AppendLine("- 변경 예정 Effect: " + changeEffectCount);
            output.AppendLine(
                "- 변경 예정 Serialized Field: "
                + (changeDescriptionCount + changeEffectCount));
            output.AppendLine("- Legacy 변경 예정: 0");
            output.AppendLine("- StaffData 변경 예정: 0");
            output.AppendLine("- Asset write: 0");

            for (int index = 0; index < warnings.Count; index++)
            {
                output.AppendLine("WARNING: " + warnings[index]);
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            bool passed = errors.Count == 0
                          && (inspection.State == MigrationState.READY_TO_APPLY
                              || inspection.State == MigrationState.ALREADY_APPLIED);
            output.AppendLine(
                "SKILL04 OFFICIAL EFFECT MIGRATION PREVIEW: "
                + (passed ? "PASS" : "FAIL"));

            if (inspection.SourceKind == StaffOfficialDataSourceKind.SessionOverride)
            {
                Debug.LogWarning("NON_CANONICAL_OVERRIDE\n" + inspection.ActiveFolder);
            }

            if (passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private enum MigrationState
        {
            INVALID,
            READY_TO_APPLY,
            ALREADY_APPLIED,
            PARTIAL_MIGRATION_STATE
        }

        private enum ActiveState
        {
            INVALID,
            READY,
            ALREADY,
            PARTIAL
        }

        private sealed class MigrationTarget
        {
            internal string StaffId { get; }
            internal string OfficialName { get; }
            internal string ActivePath { get; }
            internal string ActiveGuid { get; }
            internal string ObjectName { get; }
            internal float Duration { get; }
            internal float Cooldown { get; }
            internal string LegacyPath { get; }
            internal string LegacyGuid { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal string StaffDataPath
            {
                get { return "Assets/Resources/StaffData/" + StaffId + ".asset"; }
            }

            internal MigrationTarget(
                string staffId,
                string officialName,
                string activePath,
                string activeGuid,
                string objectName,
                float duration,
                float cooldown,
                string legacyPath,
                string legacyGuid,
                float legacyDuration,
                float legacyCooldown)
            {
                StaffId = staffId;
                OfficialName = officialName;
                ActivePath = activePath;
                ActiveGuid = activeGuid;
                ObjectName = objectName;
                Duration = duration;
                Cooldown = cooldown;
                LegacyPath = legacyPath;
                LegacyGuid = legacyGuid;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
            }
        }

        private sealed class TargetInspection
        {
            internal MigrationTarget Target { get; }
            internal ActiveState State = ActiveState.INVALID;
            internal string ActiveGuid = string.Empty;
            internal string CurrentDescription = string.Empty;
            internal float CurrentEffect = float.NaN;
            internal float Duration = float.NaN;
            internal float Cooldown = float.NaN;
            internal string ReferenceStaff = string.Empty;
            internal string LegacyGuid = string.Empty;
            internal string LegacyAssetSha256 = string.Empty;
            internal string LegacyMetaSha256 = string.Empty;

            internal TargetInspection(MigrationTarget target)
            {
                Target = target;
            }
        }

        private sealed class MigrationBackup
        {
            internal readonly Dictionary<string, TargetBackup> Targets =
                new Dictionary<string, TargetBackup>(StringComparer.Ordinal);
        }

        private sealed class TargetBackup
        {
            internal MigrationTarget Target { get; }
            internal string ActiveGuid = string.Empty;
            internal string ActiveMetaSha256 = string.Empty;
            internal string ObjectName = string.Empty;
            internal string ScriptGuid = string.Empty;
            internal string ConcreteClass = string.Empty;
            internal string Description = string.Empty;
            internal float Effect;
            internal float Duration;
            internal float Cooldown;
            internal string NonTargetSerializedFingerprint = string.Empty;
            internal int ActiveReferenceCount;
            internal string ReferencedStaffId = string.Empty;
            internal string ActiveReferenceFingerprint = string.Empty;
            internal string StaffDataAssetSha256 = string.Empty;
            internal string StaffDataMetaSha256 = string.Empty;
            internal string StaffSkillReferenceGuid = string.Empty;
            internal string LegacyAssetSha256 = string.Empty;
            internal string LegacyMetaSha256 = string.Empty;
            internal string LegacyGuid = string.Empty;
            internal int LegacyReferenceCount;
            internal string LegacyReferenceFingerprint = string.Empty;

            internal TargetBackup(MigrationTarget target)
            {
                Target = target;
            }
        }

        private sealed class PreparedTarget
        {
            internal AssignedCookingSpeedUpSkill Asset { get; }
            internal SerializedObject Serialized { get; }
            internal SerializedProperty Description { get; }
            internal SerializedProperty Effect { get; }
            internal TargetBackup Backup { get; }

            internal PreparedTarget(
                AssignedCookingSpeedUpSkill asset,
                SerializedObject serialized,
                SerializedProperty description,
                SerializedProperty effect,
                TargetBackup backup)
            {
                Asset = asset;
                Serialized = serialized;
                Description = description;
                Effect = effect;
                Backup = backup;
            }
        }

        private sealed class MigrationInspection
        {
            internal string ActiveFolder = string.Empty;
            internal StaffOfficialDataSourceKind SourceKind =
                StaffOfficialDataSourceKind.Canonical;
            internal string PackageFingerprint = string.Empty;
            internal string RuntimeScriptGuid = string.Empty;
            internal float RuntimeDefaultPercent = float.NaN;
            internal MigrationState State = MigrationState.INVALID;
            internal int ReadyCount;
            internal int AlreadyCount;
            internal readonly List<TargetInspection> Targets =
                new List<TargetInspection>();
        }
    }
}
