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
    internal static class StaffSkill06ExistingStaffMigrationTool
    {
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Skill06 Existing Staff Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Skill06 Existing Staff Migration";
        private const string Final17Sha256 =
            "ce51987f36fec57b434d3b3fcaf796a5fbd4d907a5dd8a0b6b9c507db392c68d";
        private const string SkillTypeSha256 =
            "26fdb35b14296418c094a929ddaa040ba94d24ebd20108adab4aaad666b46ad7";
        private const string PaymentTipScriptPath =
            "Assets/Scripts/Staff/StaffSkill/FoodPaymentTipUpSkill.cs";
        private const string PaymentTipScriptGuid =
            "5bd8254a6ae09954aba812b6ddc1b280";
        private const string OfficialSkillId = "STAFF_SKILL06";
        private const string OfficialDescription = "팁 (50%)증가";
        private const string StaffId = "STAFF09";
        private const string StaffDataPath = "Assets/Resources/StaffData/STAFF09.asset";
        private const string StaffDataGuid = "eb7f4fa7aeac2a44baf5122341737cd1";
        private const string ActivePath = "Assets/Scripts/Datas/Staff/Skill/STAFF09Skill.asset";
        private const string LegacyPath = "Assets/Scripts/Datas/Staff/LegacySkill/STAFF09Skill.asset";
        private const string LegacyGuid = "3576053ed0b398d43a296e16eaf3aff6";
        private const string ObjectName = "STAFF09Skill";

        private static readonly LegacyRequirement[] ExistingSkill04Legacy =
        {
            new LegacyRequirement("STAFF17", "Staff17Skill.asset", "STAFF17Skill", "c1305190b57c1d54482ece9b2e58be3d", 18, 200),
            new LegacyRequirement("STAFF19", "Staff19Skill.asset", "STAFF19Skill", "67fad8354daa0194fbbcf5833b9ebdca", 24, 150),
            new LegacyRequirement("STAFF20", "Staff20Skill.asset", "STAFF20Skill", "ab2b64bcb83dc9d48b5773f0c88a830e", 27, 150),
            new LegacyRequirement("STAFF29", "STAFF29SKILL.asset", "STAFF29SKILL", "6513e175122c20641a60cad9e71895fa", 30, 150)
        };

        [MenuItem(PreviewMenuPath)]
        private static void PreviewMigration()
        {
            RunMigrationMenu(false);
        }

        [MenuItem(ApplyMenuPath)]
        private static void ApplyMigrationFromMenu()
        {
            RunMigrationMenu(true);
        }

        private static void RunMigrationMenu(bool apply)
        {
            if (apply && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "[Skill06 Existing Staff Migration]\n"
                    + "APPLY FAIL: Play Mode에서는 실행할 수 없습니다.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Select Final17 official Staff data package folder",
                projectRoot,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                Debug.LogWarning("Skill06 Existing Staff Migration was cancelled. 변경 0개.");
                return;
            }

            MigrationInspection inspection;
            List<string> errors = new List<string>();
            if (!TryInspect(selectedFolder, out inspection, errors) || inspection == null)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (inspection.State == MigrationState.ALREADY_APPLIED)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, true);
                Debug.Log("ALREADY_APPLIED: STAFF09는 이미 공식 Skill06으로 완전히 전환됐습니다. 변경 0개.");
                return;
            }

            if (inspection.State != MigrationState.INITIAL)
            {
                errors.Add("PARTIAL_MIGRATION_STATE: 일부 적용 또는 예상하지 않은 Asset 상태입니다.");
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (!apply)
            {
                LogInspection("PREVIEW", inspection, errors, true);
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Skill06 Existing Staff Migration",
                "STAFF09의 구형 SpeedUpSkill을 LegacySkill로 이동하고 공식 Skill06 Asset을 만든 뒤 "
                + "StaffData의 _skill 참조만 교체합니다.\n\n계속하시겠습니까?",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                Debug.LogWarning("Skill06 Existing Staff Migration Apply가 취소됐습니다. 변경 0개.");
                return;
            }

            ApplyMigration(selectedFolder, inspection);
        }

        private static bool TryInspect(
            string officialFolder,
            out MigrationInspection inspection,
            List<string> errors)
        {
            inspection = new MigrationInspection(officialFolder);
            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataPackValidator.TryBuildReadOnlySnapshot(
                    officialFolder,
                    out official,
                    out diagnostics)
                || official == null)
            {
                AddDiagnostics("Official Snapshot", diagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.PackageFingerprint = official.PackageFingerprint;
            if (!ValidateOfficialSkill06(official, errors)
                || !ValidatePaymentTipScript(errors)
                || !ValidateExistingSkill04Legacy(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            InspectTarget(inspection, errors);
            return errors.Count == 0
                   && (inspection.State == MigrationState.INITIAL
                       || inspection.State == MigrationState.ALREADY_APPLIED);
        }

        private static bool ValidateOfficialSkill06(
            StaffOfficialDataPackageSnapshot official,
            List<string> errors)
        {
            StaffOfficialFileSnapshot final17;
            StaffOfficialFileSnapshot skillType;
            if (!official.TryGetFile("Final17", out final17)
                || !official.TryGetFile("SkillType", out skillType))
            {
                errors.Add("OFFICIAL_SKILL06_DISTRIBUTION_CHANGED: Final17 또는 SkillType Snapshot이 없습니다.");
                return false;
            }

            bool valid = true;
            if (final17.Sha256 != Final17Sha256 || skillType.Sha256 != SkillTypeSha256)
            {
                errors.Add(
                    "공식 SHA-256 불일치: Final17 " + final17.Sha256
                    + ", SkillType " + skillType.Sha256);
                valid = false;
            }

            int definitionCount = 0;
            for (int index = 0; index < skillType.Rows.Count; index++)
            {
                IReadOnlyList<string> row = skillType.Rows[index];
                if (row.Count >= 2 && row[0].Trim() == OfficialSkillId)
                {
                    definitionCount++;
                    valid &= row[1].Trim() == OfficialDescription;
                }
            }

            Dictionary<string, OfficialTarget> expected = new Dictionary<string, OfficialTarget>(StringComparer.Ordinal)
            {
                { "STAFF09", new OfficialTarget("루나", "매니저", 30, 200) },
                { "STAFF51", new OfficialTarget("베스트 멜로", "청소부", 32, 195) },
                { "STAFF81", new OfficialTarget("홍비비", "청소부", 32, 195) }
            };
            HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < final17.Rows.Count; index++)
            {
                IReadOnlyList<string> row = final17.Rows[index];
                if (row.Count < 14 || row[10].Trim() != OfficialSkillId)
                {
                    continue;
                }

                string id = row[0].Trim();
                OfficialTarget target;
                double duration;
                double cooldown;
                bool rowValid = actual.Add(id)
                                && expected.TryGetValue(id, out target)
                                && row[1].Trim() == target.Name
                                && row[6].Trim() == target.Role
                                && TryParseSeconds(row[12], out duration)
                                && TryParseSeconds(row[13], out cooldown)
                                && Approximately(duration, target.Duration)
                                && Approximately(cooldown, target.Cooldown);
                if (!rowValid)
                {
                    errors.Add("OFFICIAL_SKILL06_DISTRIBUTION_CHANGED: " + id);
                    valid = false;
                }
            }

            if (definitionCount != 1 || !actual.SetEquals(expected.Keys))
            {
                errors.Add(
                    "OFFICIAL_SKILL06_DISTRIBUTION_CHANGED: 정의 " + definitionCount
                    + ", 전체 " + actual.Count + "/3");
                valid = false;
            }

            return valid;
        }

        private static bool ValidatePaymentTipScript(List<string> errors)
        {
            string guid = AssetDatabase.AssetPathToGUID(PaymentTipScriptPath);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(PaymentTipScriptPath);
            bool valid = guid == PaymentTipScriptGuid
                         && script != null
                         && script.GetClass() == typeof(FoodPaymentTipUpSkill);
            if (!valid)
            {
                errors.Add("FoodPaymentTipUpSkill Script 또는 GUID가 기준과 다릅니다. 실제 GUID: " + guid);
            }

            return valid;
        }

        private static bool ValidateExistingSkill04Legacy(List<string> errors)
        {
            if (!AssetDatabase.IsValidFolder(StaffDataAssetInventoryReader.LegacySkillFolder))
            {
                errors.Add("기존 LegacySkill 폴더가 없습니다.");
                return false;
            }

            bool valid = true;
            for (int index = 0; index < ExistingSkill04Legacy.Length; index++)
            {
                LegacyRequirement target = ExistingSkill04Legacy[index];
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                string guid = AssetDatabase.AssetPathToGUID(path);
                SpeedUpSkill skill = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(path);
                bool preserved = skill != null
                                 && guid == target.Guid
                                 && skill.name == target.ObjectName
                                 && Approximately(skill.Duration, target.Duration)
                                 && Approximately(skill.Cooldown, target.Cooldown)
                                 && Approximately(skill.FirstValue, 100d)
                                 && FindAssetReferences(path).Count == 0;
                if (!preserved)
                {
                    errors.Add("기존 Skill04 Legacy Asset 기준 불일치: " + target.StaffId);
                    valid = false;
                }
            }

            return valid;
        }

        private static void InspectTarget(MigrationInspection inspection, List<string> errors)
        {
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            if (staff == null || AssetDatabase.AssetPathToGUID(StaffDataPath) != StaffDataGuid)
            {
                errors.Add("STAFF09 StaffData 또는 GUID가 기준과 다릅니다.");
                inspection.State = MigrationState.INVALID;
                return;
            }

            SerializedProperty skillProperty = new SerializedObject(staff).FindProperty("_skill");
            if (skillProperty == null)
            {
                errors.Add("STAFF09._skill 필드를 찾을 수 없습니다.");
                inspection.State = MigrationState.INVALID;
                return;
            }

            UnityEngine.Object referenced = skillProperty.objectReferenceValue;
            string referencedPath = referenced == null ? string.Empty : AssetDatabase.GetAssetPath(referenced);
            string activeGuid = AssetDatabase.AssetPathToGUID(ActivePath);
            string legacyGuid = AssetDatabase.AssetPathToGUID(LegacyPath);
            UnityEngine.Object activeObject = AssetDatabase.LoadMainAssetAtPath(ActivePath);
            UnityEngine.Object legacyObject = AssetDatabase.LoadMainAssetAtPath(LegacyPath);
            inspection.ActiveGuid = activeGuid;
            inspection.LegacyGuid = legacyGuid;

            SpeedUpSkill initialSkill = activeObject as SpeedUpSkill;
            bool initial = initialSkill != null
                           && activeGuid == LegacyGuid
                           && referencedPath == ActivePath
                           && initialSkill.name == ObjectName
                           && string.IsNullOrEmpty(initialSkill.Description)
                           && Approximately(initialSkill.Duration, 16d)
                           && Approximately(initialSkill.Cooldown, 150d)
                           && Approximately(initialSkill.FirstValue, 100d)
                           && legacyObject == null
                           && string.IsNullOrEmpty(legacyGuid)
                           && IsExactSingleReference(FindAssetReferences(ActivePath), StaffDataPath);
            if (initial)
            {
                inspection.State = MigrationState.INITIAL;
                return;
            }

            FoodPaymentTipUpSkill activeSkill = activeObject as FoodPaymentTipUpSkill;
            SpeedUpSkill preservedSkill = legacyObject as SpeedUpSkill;
            bool applied = activeSkill != null
                           && preservedSkill != null
                           && IsUnityGuid(activeGuid)
                           && activeGuid != LegacyGuid
                           && legacyGuid == LegacyGuid
                           && referencedPath == ActivePath
                           && activeSkill.name == ObjectName
                           && activeSkill.Description == OfficialDescription
                           && Approximately(activeSkill.Duration, 30d)
                           && Approximately(activeSkill.Cooldown, 200d)
                           && Approximately(activeSkill.FirstValue, 50d)
                           && preservedSkill.name == ObjectName
                           && string.IsNullOrEmpty(preservedSkill.Description)
                           && Approximately(preservedSkill.Duration, 16d)
                           && Approximately(preservedSkill.Cooldown, 150d)
                           && Approximately(preservedSkill.FirstValue, 100d)
                           && IsExactSingleReference(FindAssetReferences(ActivePath), StaffDataPath)
                           && FindAssetReferences(LegacyPath).Count == 0;
            if (applied)
            {
                inspection.State = MigrationState.ALREADY_APPLIED;
                return;
            }

            inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: active=" + ActivePath + " (" + activeGuid + ")"
                + ", legacy=" + LegacyPath + " (" + legacyGuid + ")"
                + ", staff reference=" + referencedPath);
        }

        private static void ApplyMigration(string officialFolder, MigrationInspection before)
        {
            MigrationInspection finalPreflight;
            List<string> finalErrors = new List<string>();
            if (!TryInspect(officialFolder, out finalPreflight, finalErrors)
                || finalPreflight.State != MigrationState.INITIAL)
            {
                finalErrors.Add("APPLY 직전 상태가 INITIAL이 아닙니다. 변경 0개.");
                LogInspection("APPLY", finalPreflight, finalErrors, false);
                return;
            }

            MigrationBackup backup = null;
            bool moved = false;
            bool created = false;
            bool writesStarted = false;
            try
            {
                backup = CaptureBackup();
                writesStarted = true;
                string moveError = AssetDatabase.MoveAsset(ActivePath, LegacyPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException("STAFF09 Legacy 이동 실패: " + moveError);
                }

                moved = true;
                if (AssetDatabase.AssetPathToGUID(LegacyPath) != LegacyGuid)
                {
                    throw new InvalidOperationException("STAFF09 Legacy GUID가 이동 중 변경됐습니다.");
                }

                FoodPaymentTipUpSkill skill = ScriptableObject.CreateInstance<FoodPaymentTipUpSkill>();
                AssetDatabase.CreateAsset(skill, ActivePath);
                created = true;
                string newGuid = AssetDatabase.AssetPathToGUID(ActivePath);
                if (!IsUnityGuid(newGuid) || newGuid == LegacyGuid)
                {
                    throw new InvalidOperationException("신규 Skill06 GUID가 유효하지 않습니다: " + newGuid);
                }

                skill.name = ObjectName;
                ConfigureNewSkill(skill);
                EditorUtility.SetDirty(skill);
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
                if (staff == null)
                {
                    throw new InvalidOperationException("STAFF09 StaffData를 다시 읽을 수 없습니다.");
                }

                SetSkillReference(staff, skill);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                List<string> postErrors = new List<string>();
                MigrationInspection after;
                bool postPassed = TryInspect(officialFolder, out after, postErrors)
                                  && after.State == MigrationState.ALREADY_APPLIED
                                  && ValidatePostApplyFiles(backup, postErrors)
                                  && ValidatePostInventory(postErrors)
                                  && ValidatePostDryRun(officialFolder, postErrors);
                if (!postPassed)
                {
                    throw new InvalidOperationException(
                        "Post-Apply 검증 실패: " + string.Join(" | ", postErrors.ToArray()));
                }

                LogApplySuccess(before, after);
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = new List<string>();
                bool rollbackPassed = !writesStarted
                                      || RollbackMigration(
                                          officialFolder,
                                          backup,
                                          moved,
                                          created,
                                          rollbackErrors);
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Skill06 Existing Staff Migration]");
                output.AppendLine("APPLY FAIL: " + exception.Message);
                output.AppendLine(
                    "Atomic Rollback: "
                    + (!writesStarted ? "NOT_REQUIRED" : rollbackPassed ? "PASS" : "FAIL"));
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

        private static MigrationBackup CaptureBackup()
        {
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            if (staff == null)
            {
                throw new InvalidOperationException("Backup STAFF09 StaffData가 없습니다.");
            }

            return new MigrationBackup(
                AssetDatabase.AssetPathToGUID(StaffDataPath),
                CaptureSerializedFingerprint(staff, "_skill"),
                ComputeAssetFileSha256(ActivePath),
                ComputeAssetFileSha256(ActivePath + ".meta"));
        }

        private static void ConfigureNewSkill(FoodPaymentTipUpSkill skill)
        {
            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty description = serialized.FindProperty("_description");
            SerializedProperty duration = serialized.FindProperty("_duration");
            SerializedProperty cooldown = serialized.FindProperty("_cooldown");
            SerializedProperty percent = serialized.FindProperty("_foodPaymentTipUpPercent");
            if (description == null || duration == null || cooldown == null || percent == null)
            {
                throw new InvalidOperationException("Skill06 직렬화 필드를 찾을 수 없습니다.");
            }

            description.stringValue = OfficialDescription;
            duration.floatValue = 30f;
            cooldown.floatValue = 200f;
            percent.floatValue = 50f;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException("Skill06 값 적용에 실패했습니다.");
            }
        }

        private static void SetSkillReference(StaffData staff, SkillBase skill)
        {
            SerializedObject serialized = new SerializedObject(staff);
            SerializedProperty property = serialized.FindProperty("_skill");
            if (property == null)
            {
                throw new InvalidOperationException("STAFF09._skill 필드를 찾을 수 없습니다.");
            }

            if (property.objectReferenceValue != skill)
            {
                property.objectReferenceValue = skill;
                if (!serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    throw new InvalidOperationException("STAFF09._skill 참조 적용에 실패했습니다.");
                }

                EditorUtility.SetDirty(staff);
            }
        }

        private static bool ValidatePostApplyFiles(MigrationBackup backup, List<string> errors)
        {
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            bool valid = backup != null
                         && staff != null
                         && AssetDatabase.AssetPathToGUID(StaffDataPath) == backup.StaffDataGuid
                         && CaptureSerializedFingerprint(staff, "_skill") == backup.StaffDataFingerprint
                         && ComputeAssetFileSha256(LegacyPath) == backup.LegacyAssetSha256
                         && ComputeAssetFileSha256(LegacyPath + ".meta") == backup.LegacyMetaSha256;
            if (!valid)
            {
                errors.Add("Post-Apply STAFF09 비대상 값 또는 Legacy 파일이 변경됐습니다.");
            }

            return valid;
        }

        private static bool ValidatePostInventory(List<string> errors)
        {
            StaffDataAssetInventorySnapshot snapshot;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(out snapshot, out diagnostics)
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

            bool valid = snapshot.Skills.Count == 32
                         && GetCount(classes, "SpeedUpSkill") == 22
                         && GetCount(classes, "TouchAddCustomerButtonSkill") == 5
                         && GetCount(classes, "AssignedCookingSpeedUpSkill") == 4
                         && GetCount(classes, "FoodPaymentTipUpSkill") == 1
                         && classes.Count == 4
                         && shared == 0
                         && orphan == 0;
            if (!valid)
            {
                errors.Add("Post-Apply Inventory V5 baseline이 다릅니다.");
            }

            return valid;
        }

        private static bool ValidatePostDryRun(string officialFolder, List<string> errors)
        {
            StaffDataDryRunPlanSnapshot plan;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataDryRunPlanner.TryBuildReadOnlyPlan(
                    officialFolder,
                    out plan,
                    out diagnostics)
                || plan == null)
            {
                AddDiagnostics("Post-Apply Dry Run V5", diagnostics, errors);
                return false;
            }

            int warnings = 0;
            int prerequisites = 0;
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
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                warnings += plan.GlobalIssues[index].IsWarning ? 1 : 0;
                prerequisites += plan.GlobalIssues[index].IsPrerequisite ? 1 : 0;
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
                    existingWarnings += staff.Readiness == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS ? 1 : 0;
                    existingClass += staff.Readiness == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                    existingSave += staff.Readiness == StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED ? 1 : 0;
                }
                else
                {
                    newReady += staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY ? 1 : 0;
                    newClass += staff.Readiness == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                }

                for (int issueIndex = 0; issueIndex < staff.Issues.Count; issueIndex++)
                {
                    StaffDataDryRunIssue issue = staff.Issues[issueIndex];
                    warnings += issue.IsWarning ? 1 : 0;
                    prerequisites += issue.IsPrerequisite ? 1 : 0;
                    existingMismatch += issue.Code == "EXISTING_SKILL_CLASS_MISMATCH" ? 1 : 0;
                    newUnsupported += issue.Code == "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED" ? 1 : 0;
                    hasSkillPrerequisite |= issue.IsPrerequisite
                                            && (issue.Disposition
                                                == StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED
                                                || issue.Disposition
                                                == StaffDryRunFieldDisposition.SKILL_CLASS_MIGRATION_REQUIRED);
                }

                skillPrerequisites += hasSkillPrerequisite ? 1 : 0;
            }

            bool valid = plan.PlanningPolicyVersion == "STAFF_DRY_RUN_POLICY_2026_08_19_V5"
                         && existingMismatch == 12
                         && newUnsupported == 11
                         && skillPrerequisites == 23
                         && prerequisites == 24
                         && warnings == 65
                         && durationMismatch == 11
                         && cooldownMismatch == 9
                         && changedFields == 2146
                         && existingWarnings == 19
                         && existingClass == 12
                         && existingSave == 1
                         && newReady == 49
                         && newClass == 11;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Dry Run V5 baseline 불일치: mismatch "
                    + existingMismatch + "/" + newUnsupported
                    + ", prerequisite/warning " + prerequisites + "/" + warnings
                    + ", duration/cooldown " + durationMismatch + "/" + cooldownMismatch
                    + ", field " + changedFields);
            }

            return valid;
        }

        private static bool RollbackMigration(
            string officialFolder,
            MigrationBackup backup,
            bool moved,
            bool created,
            List<string> errors)
        {
            bool valid = true;
            try
            {
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
                SkillBase legacy = AssetDatabase.LoadAssetAtPath<SkillBase>(LegacyPath);
                if (staff != null && legacy != null)
                {
                    SetSkillReference(staff, legacy);
                    AssetDatabase.SaveAssets();
                }

                if (created
                    && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ActivePath))
                    && !AssetDatabase.DeleteAsset(ActivePath))
                {
                    errors.Add("신규 Skill06 Asset 삭제 실패: " + ActivePath);
                    valid = false;
                }

                if (moved && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(LegacyPath)))
                {
                    string moveError = AssetDatabase.MoveAsset(LegacyPath, ActivePath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        errors.Add("구형 STAFF09 Skill 원래 경로 복구 실패: " + moveError);
                        valid = false;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                errors.Add("Rollback 예외: " + exception.Message);
                valid = false;
            }

            if (backup == null)
            {
                errors.Add("Rollback 검증용 원본 Backup이 없습니다.");
                return false;
            }

            MigrationInspection restored;
            List<string> inspectionErrors = new List<string>();
            bool initialRestored = TryInspect(officialFolder, out restored, inspectionErrors)
                                   && restored.State == MigrationState.INITIAL;
            if (!initialRestored)
            {
                errors.AddRange(inspectionErrors);
                errors.Add("Rollback 후 INITIAL 상태가 복구되지 않았습니다.");
                valid = false;
            }

            StaffData restoredStaff = AssetDatabase.LoadAssetAtPath<StaffData>(StaffDataPath);
            bool filesRestored = restoredStaff != null
                                 && AssetDatabase.AssetPathToGUID(StaffDataPath) == backup.StaffDataGuid
                                 && CaptureSerializedFingerprint(restoredStaff, "_skill")
                                 == backup.StaffDataFingerprint
                                 && ComputeAssetFileSha256(ActivePath) == backup.LegacyAssetSha256
                                 && ComputeAssetFileSha256(ActivePath + ".meta")
                                 == backup.LegacyMetaSha256;
            if (!filesRestored)
            {
                errors.Add("Rollback 원본 파일 검증 실패: STAFF09");
                valid = false;
            }

            return valid;
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

        private static void AppendSerializedValue(StringBuilder input, SerializedProperty property)
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

        private static List<string> FindAssetReferences(string targetPath)
        {
            List<string> references = new List<string>();
            if (string.IsNullOrEmpty(targetPath)
                || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(targetPath)))
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

        private static bool IsExactSingleReference(IReadOnlyList<string> references, string expectedPath)
        {
            return references.Count == 1 && references[0] == expectedPath;
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
            return double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out leftValue)
                   && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out rightValue)
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
            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string absolutePath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(absolutePath)
                ? ComputeSha256(File.ReadAllBytes(absolutePath))
                : string.Empty;
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

        private static void LogInspection(
            string phase,
            MigrationInspection inspection,
            IReadOnlyList<string> errors,
            bool passed)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill06 Existing Staff Migration " + phase + "]");
            if (inspection != null)
            {
                output.AppendLine("Official Package Fingerprint: " + inspection.PackageFingerprint);
                output.AppendLine("Migration State: " + inspection.State);
                output.AppendLine("- STAFF09 Active " + ActivePath + " (" + inspection.ActiveGuid + ")");
                output.AppendLine("- STAFF09 Legacy " + LegacyPath + " (" + inspection.LegacyGuid + ")");
                output.AppendLine("- Official Duration/Cooldown/Percent: 30/200/50");
                if (inspection.State == MigrationState.INITIAL)
                {
                    output.AppendLine("- Expected: Legacy 이동, FoodPaymentTipUpSkill 생성, STAFF09._skill 교체");
                }
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            if (phase == "PREVIEW")
            {
                output.AppendLine(
                    "SKILL06 EXISTING STAFF MIGRATION PREVIEW: "
                    + (inspection != null && inspection.State == MigrationState.ALREADY_APPLIED
                        ? "ALREADY_APPLIED"
                        : passed ? "PASS" : "FAIL"));
            }
            else
            {
                output.AppendLine(
                    "APPLY "
                    + (inspection != null && inspection.State == MigrationState.ALREADY_APPLIED
                        ? "ALREADY_APPLIED"
                        : passed ? "PASS" : "FAIL"));
            }

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

        private static void LogApplySuccess(MigrationInspection before, MigrationInspection after)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill06 Existing Staff Migration APPLY]");
            output.AppendLine("APPLY PASS");
            output.AppendLine("- LegacySkill 이동 및 GUID 보존: 1/1 PASS");
            output.AppendLine("- FoodPaymentTipUpSkill 생성: 1/1 PASS");
            output.AppendLine("- StaffData _skill 참조 교체: 1/1 PASS");
            output.AppendLine("- Active Skill Inventory: 32 (22/5/4/1)");
            output.AppendLine("- Legacy Skill 보존: 5/5 PASS");
            output.AppendLine("- Dry Run Policy: STAFF_DRY_RUN_POLICY_2026_08_19_V5");
            output.AppendLine("- Active GUID: " + after.ActiveGuid);
            output.AppendLine("- Legacy GUID: " + after.LegacyGuid);
            Debug.Log(output.ToString());
        }

        private enum MigrationState
        {
            INVALID,
            INITIAL,
            ALREADY_APPLIED,
            PARTIAL_MIGRATION_STATE
        }

        private sealed class MigrationInspection
        {
            internal string OfficialFolder { get; }
            internal string PackageFingerprint = string.Empty;
            internal string ActiveGuid = string.Empty;
            internal string LegacyGuid = string.Empty;
            internal MigrationState State;

            internal MigrationInspection(string officialFolder)
            {
                OfficialFolder = officialFolder;
            }
        }

        private sealed class OfficialTarget
        {
            internal string Name { get; }
            internal string Role { get; }
            internal double Duration { get; }
            internal double Cooldown { get; }

            internal OfficialTarget(string name, string role, double duration, double cooldown)
            {
                Name = name;
                Role = role;
                Duration = duration;
                Cooldown = cooldown;
            }
        }

        private sealed class LegacyRequirement
        {
            internal string StaffId { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string Guid { get; }
            internal double Duration { get; }
            internal double Cooldown { get; }

            internal LegacyRequirement(
                string staffId,
                string fileName,
                string objectName,
                string guid,
                double duration,
                double cooldown)
            {
                StaffId = staffId;
                FileName = fileName;
                ObjectName = objectName;
                Guid = guid;
                Duration = duration;
                Cooldown = cooldown;
            }
        }

        private sealed class MigrationBackup
        {
            internal string StaffDataGuid { get; }
            internal string StaffDataFingerprint { get; }
            internal string LegacyAssetSha256 { get; }
            internal string LegacyMetaSha256 { get; }

            internal MigrationBackup(
                string staffDataGuid,
                string staffDataFingerprint,
                string legacyAssetSha256,
                string legacyMetaSha256)
            {
                StaffDataGuid = staffDataGuid;
                StaffDataFingerprint = staffDataFingerprint;
                LegacyAssetSha256 = legacyAssetSha256;
                LegacyMetaSha256 = legacyMetaSha256;
            }
        }
    }
}
