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
    internal static class StaffSkill04ExistingStaffMigrationTool
    {
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Skill04 Existing Staff Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Skill04 Existing Staff Migration";
        private const string Final17Sha256 =
            "ce51987f36fec57b434d3b3fcaf796a5fbd4d907a5dd8a0b6b9c507db392c68d";
        private const string AssignedCookingScriptPath =
            "Assets/Scripts/Staff/StaffSkill/AssignedCookingSpeedUpSkill.cs";
        private const string AssignedCookingScriptGuid =
            "f6dec9edb1244c84d99fc7f5daea02f9";
        private const string OfficialSkillId = "STAFF_SKILL04";
        private const string OfficialDescription =
            "맡은 주방 음식 제작 속도 (150%) 증가";

        private static readonly MigrationTarget[] Targets =
        {
            new MigrationTarget("STAFF17", "염소아치", "Staff17Skill.asset", "STAFF17Skill", "c1305190b57c1d54482ece9b2e58be3d", 18, 200, 25, 160),
            new MigrationTarget("STAFF19", "양아치", "Staff19Skill.asset", "STAFF19Skill", "67fad8354daa0194fbbcf5833b9ebdca", 24, 150, 25, 160),
            new MigrationTarget("STAFF20", "포코", "Staff20Skill.asset", "STAFF20Skill", "ab2b64bcb83dc9d48b5773f0c88a830e", 27, 150, 25, 160),
            new MigrationTarget("STAFF29", "셰프 바라", "STAFF29SKILL.asset", "STAFF29SKILL", "6513e175122c20641a60cad9e71895fa", 30, 150, 30, 150)
        };

        private static readonly HashSet<string> ExpectedNewSkill04Staff =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF46", "STAFF47", "STAFF48", "STAFF59", "STAFF60",
                "STAFF70", "STAFF79", "STAFF87", "STAFF88"
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
                    "[Skill04 Existing Staff Migration]\n"
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
                Debug.LogWarning("Skill04 Existing Staff Migration was cancelled. 변경 0개.");
                return;
            }

            MigrationInspection inspection;
            List<string> errors = new List<string>();
            bool inspected = TryInspect(selectedFolder, out inspection, errors);
            if (!inspected || inspection == null)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (inspection.State == MigrationState.ALREADY_APPLIED)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, true);
                Debug.Log("ALREADY_APPLIED: Skill04 기존 직원 4명은 이미 완전히 전환됐습니다. 변경 0개.");
                return;
            }

            if (inspection.State != MigrationState.READY_TO_APPLY)
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
                "Apply Skill04 Existing Staff Migration",
                "STAFF17, STAFF19, STAFF20, STAFF29의 구형 Skill을 LegacySkill로 이동하고 "
                + "공식 Skill04 Asset을 만든 뒤 StaffData의 _skill 참조만 교체합니다.\n\n"
                + "계속하시겠습니까?",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                Debug.LogWarning("Skill04 Existing Staff Migration Apply가 취소됐습니다. 변경 0개.");
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
            if (!ValidateOfficialSkill04(official, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateAssignedCookingScript(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            int initialCount = 0;
            int appliedCount = 0;
            HashSet<string> appliedGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Targets.Length; index++)
            {
                TargetInspection targetInspection = InspectTarget(Targets[index], errors);
                inspection.Targets.Add(targetInspection);
                initialCount += targetInspection.State == TargetState.INITIAL ? 1 : 0;
                appliedCount += targetInspection.State == TargetState.APPLIED ? 1 : 0;
                if (targetInspection.State == TargetState.APPLIED
                    && !appliedGuids.Add(targetInspection.ActiveGuid))
                {
                    errors.Add("신규 Skill04 GUID가 중복되었습니다: " + targetInspection.ActiveGuid);
                }
            }

            if (errors.Count != 0)
            {
                inspection.State = MigrationState.PARTIAL_OR_INVALID;
                return false;
            }

            if (initialCount == Targets.Length)
            {
                inspection.State = MigrationState.READY_TO_APPLY;
                return true;
            }

            if (appliedCount == Targets.Length && appliedGuids.Count == Targets.Length)
            {
                inspection.State = MigrationState.ALREADY_APPLIED;
                return true;
            }

            inspection.State = MigrationState.PARTIAL_OR_INVALID;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: Initial " + initialCount
                + "/4, Applied " + appliedCount + "/4");
            return false;
        }

        private static bool ValidateOfficialSkill04(
            StaffOfficialDataPackageSnapshot official,
            List<string> errors)
        {
            StaffOfficialFileSnapshot final17;
            StaffOfficialFileSnapshot skillType;
            if (!official.TryGetFile("Final17", out final17)
                || !official.TryGetFile("SkillType", out skillType))
            {
                errors.Add("OFFICIAL_SKILL04_DISTRIBUTION_CHANGED: Final17 또는 SkillType Snapshot이 없습니다.");
                return false;
            }

            bool valid = true;
            if (final17.Sha256 != Final17Sha256)
            {
                errors.Add("Final17 SHA-256 불일치: " + final17.Sha256);
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

            Dictionary<string, IReadOnlyList<string>> skill04Rows =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int index = 0; index < final17.Rows.Count; index++)
            {
                IReadOnlyList<string> row = final17.Rows[index];
                if (row.Count >= 14 && row[10].Trim() == OfficialSkillId)
                {
                    string id = row[0].Trim();
                    if (row[6].Trim() != "주방장")
                    {
                        errors.Add("OFFICIAL_SKILL04_DISTRIBUTION_CHANGED: CHEF가 아닌 대상 " + id);
                        valid = false;
                    }

                    if (skill04Rows.ContainsKey(id))
                    {
                        errors.Add("OFFICIAL_SKILL04_DISTRIBUTION_CHANGED: 중복 ID " + id);
                        valid = false;
                    }
                    else
                    {
                        skill04Rows.Add(id, row);
                    }
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
                bool targetValid = skill04Rows.TryGetValue(target.StaffId, out row)
                                   && row.Count >= 14
                                   && row[1].Trim() == target.OfficialName
                                   && TryParseSeconds(row[12], out duration)
                                   && TryParseSeconds(row[13], out cooldown)
                                   && Approximately(duration, target.OfficialDuration)
                                   && Approximately(cooldown, target.OfficialCooldown);
                if (!targetValid)
                {
                    errors.Add("OFFICIAL_SKILL04_DISTRIBUTION_CHANGED: " + target.StaffId);
                    valid = false;
                }
            }

            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in skill04Rows.Keys)
            {
                if (!expectedExisting.Contains(id))
                {
                    actualNew.Add(id);
                }
            }

            if (definitionCount != 1
                || skill04Rows.Count != 13
                || expectedExisting.Count != 4
                || !actualNew.SetEquals(ExpectedNewSkill04Staff))
            {
                errors.Add(
                    "OFFICIAL_SKILL04_DISTRIBUTION_CHANGED: 정의 " + definitionCount
                    + ", 전체 " + skill04Rows.Count + ", 기존 " + expectedExisting.Count
                    + ", 신규 " + actualNew.Count);
                valid = false;
            }

            return valid;
        }

        private static bool ValidateAssignedCookingScript(List<string> errors)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssignedCookingScriptPath);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssignedCookingScriptPath);
            bool valid = guid == AssignedCookingScriptGuid
                         && script != null
                         && script.GetClass() == typeof(AssignedCookingSpeedUpSkill);
            if (!valid)
            {
                errors.Add(
                    "AssignedCookingSpeedUpSkill Script 또는 GUID가 기준과 다릅니다. 실제 GUID: "
                    + guid);
            }

            return valid;
        }

        private static TargetInspection InspectTarget(
            MigrationTarget target,
            List<string> errors)
        {
            TargetInspection result = new TargetInspection(target);
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
            if (staff == null)
            {
                errors.Add("StaffData를 읽을 수 없습니다: " + target.StaffDataPath);
                return result;
            }

            string staffGuid = AssetDatabase.AssetPathToGUID(target.StaffDataPath);
            SerializedObject staffSerialized = new SerializedObject(staff);
            SerializedProperty skillReference = staffSerialized.FindProperty("_skill");
            if (!IsUnityGuid(staffGuid) || skillReference == null)
            {
                errors.Add("StaffData GUID 또는 _skill 필드가 유효하지 않습니다: " + target.StaffId);
                return result;
            }

            UnityEngine.Object referencedSkill = skillReference.objectReferenceValue;
            string referencedPath = referencedSkill == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(referencedSkill);
            string activeGuid = AssetDatabase.AssetPathToGUID(target.ActivePath);
            string legacyGuid = AssetDatabase.AssetPathToGUID(target.LegacyPath);
            UnityEngine.Object activeObject = AssetDatabase.LoadMainAssetAtPath(target.ActivePath);
            UnityEngine.Object legacyObject = AssetDatabase.LoadMainAssetAtPath(target.LegacyPath);
            result.StaffGuid = staffGuid;
            result.ActiveGuid = activeGuid;
            result.LegacyGuid = legacyGuid;

            SpeedUpSkill initialSkill = activeObject as SpeedUpSkill;
            bool legacyPathFree = legacyObject == null && string.IsNullOrEmpty(legacyGuid);
            List<string> initialReferences = FindAssetReferences(target.ActivePath);
            bool initial = initialSkill != null
                           && activeGuid == target.LegacyGuid
                           && referencedPath == target.ActivePath
                           && initialSkill.name == target.ObjectName
                           && Approximately(initialSkill.Duration, target.LegacyDuration)
                           && Approximately(initialSkill.Cooldown, target.LegacyCooldown)
                           && Approximately(initialSkill.FirstValue, 100d)
                           && legacyPathFree
                           && IsExactSingleReference(initialReferences, target.StaffDataPath);
            if (initial)
            {
                result.State = TargetState.INITIAL;
                return result;
            }

            AssignedCookingSpeedUpSkill activeSkill = activeObject as AssignedCookingSpeedUpSkill;
            SpeedUpSkill preservedSkill = legacyObject as SpeedUpSkill;
            List<string> activeReferences = FindAssetReferences(target.ActivePath);
            List<string> legacyReferences = FindAssetReferences(target.LegacyPath);
            bool applied = activeSkill != null
                           && preservedSkill != null
                           && IsUnityGuid(activeGuid)
                           && activeGuid != target.LegacyGuid
                           && legacyGuid == target.LegacyGuid
                           && referencedPath == target.ActivePath
                           && activeSkill.name == target.ObjectName
                           && activeSkill.Description == OfficialDescription
                           && Approximately(activeSkill.Duration, target.OfficialDuration)
                           && Approximately(activeSkill.Cooldown, target.OfficialCooldown)
                           && Approximately(activeSkill.FirstValue, 150d)
                           && preservedSkill.name == target.ObjectName
                           && Approximately(preservedSkill.Duration, target.LegacyDuration)
                           && Approximately(preservedSkill.Cooldown, target.LegacyCooldown)
                           && Approximately(preservedSkill.FirstValue, 100d)
                           && IsExactSingleReference(activeReferences, target.StaffDataPath)
                           && legacyReferences.Count == 0;
            if (applied)
            {
                result.State = TargetState.APPLIED;
                return result;
            }

            result.State = TargetState.INVALID;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: " + target.StaffId
                + " active=" + target.ActivePath + " (" + activeGuid + ")"
                + ", legacy=" + target.LegacyPath + " (" + legacyGuid + ")"
                + ", staff reference=" + referencedPath);
            return result;
        }

        private static void ApplyMigration(
            string officialFolder,
            MigrationInspection inspection)
        {
            MigrationInspection finalPreflight;
            List<string> finalPreflightErrors = new List<string>();
            if (!TryInspect(officialFolder, out finalPreflight, finalPreflightErrors)
                || finalPreflight.State != MigrationState.READY_TO_APPLY)
            {
                finalPreflightErrors.Add(
                    "APPLY 직전 상태가 READY_TO_APPLY가 아닙니다. 변경 0개.");
                LogInspection("APPLY", finalPreflight, finalPreflightErrors, false);
                return;
            }

            MigrationBackup backup = null;
            List<string> createdAssetPaths = new List<string>();
            List<MigrationTarget> movedTargets = new List<MigrationTarget>();
            bool legacyFolderCreated = false;
            bool writesStarted = false;
            try
            {
                backup = CaptureBackup();
                writesStarted = true;
                if (!AssetDatabase.IsValidFolder(StaffDataAssetInventoryReader.LegacySkillFolder))
                {
                    string folderGuid = AssetDatabase.CreateFolder(
                        "Assets/Scripts/Datas/Staff",
                        "LegacySkill");
                    if (string.IsNullOrEmpty(folderGuid))
                    {
                        throw new InvalidOperationException("LegacySkill 폴더 생성에 실패했습니다.");
                    }

                    legacyFolderCreated = true;
                }

                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    string moveError = AssetDatabase.MoveAsset(target.ActivePath, target.LegacyPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " Legacy 이동 실패: " + moveError);
                    }

                    movedTargets.Add(target);
                    if (AssetDatabase.AssetPathToGUID(target.LegacyPath) != target.LegacyGuid)
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " Legacy GUID가 이동 중 변경됐습니다.");
                    }
                }

                Dictionary<string, AssignedCookingSpeedUpSkill> newAssets =
                    new Dictionary<string, AssignedCookingSpeedUpSkill>(StringComparer.Ordinal);
                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    AssignedCookingSpeedUpSkill skill =
                        ScriptableObject.CreateInstance<AssignedCookingSpeedUpSkill>();
                    AssetDatabase.CreateAsset(skill, target.ActivePath);
                    createdAssetPaths.Add(target.ActivePath);
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(target.ActivePath)))
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " 신규 Skill04 Asset 생성에 실패했습니다.");
                    }

                    skill.name = target.ObjectName;
                    ConfigureNewSkill(skill, target);
                    EditorUtility.SetDirty(skill);
                    newAssets.Add(target.StaffId, skill);
                }

                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                    if (staff == null)
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " StaffData를 다시 읽을 수 없습니다.");
                    }

                    SetSkillReference(staff, newAssets[target.StaffId]);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                List<string> postErrors = new List<string>();
                MigrationInspection postInspection;
                bool postPassed = TryInspect(officialFolder, out postInspection, postErrors)
                                  && postInspection.State == MigrationState.ALREADY_APPLIED
                                  && ValidatePostApplyFiles(backup, postErrors)
                                  && ValidatePostInventory(postErrors)
                                  && ValidatePostDryRun(officialFolder, postErrors);
                if (!postPassed)
                {
                    throw new InvalidOperationException(
                        "Post-Apply 검증 실패: " + string.Join(" | ", postErrors.ToArray()));
                }

                LogApplySuccess(inspection, postInspection);
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = new List<string>();
                bool rollbackPassed = !writesStarted || RollbackMigration(
                                          officialFolder,
                                          backup,
                                          createdAssetPaths,
                                          movedTargets,
                                          legacyFolderCreated,
                                          rollbackErrors);
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Skill04 Existing Staff Migration]");
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
            MigrationBackup backup = new MigrationBackup();
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                if (staff == null)
                {
                    throw new InvalidOperationException("Backup StaffData 누락: " + target.StaffId);
                }

                backup.Targets.Add(
                    target.StaffId,
                    new TargetBackup(
                        AssetDatabase.AssetPathToGUID(target.StaffDataPath),
                        CaptureSerializedFingerprint(staff, "_skill"),
                        ComputeAssetFileSha256(target.ActivePath),
                        ComputeAssetFileSha256(target.ActivePath + ".meta")));
            }

            return backup;
        }

        private static void ConfigureNewSkill(
            AssignedCookingSpeedUpSkill skill,
            MigrationTarget target)
        {
            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty description = serialized.FindProperty("_description");
            SerializedProperty duration = serialized.FindProperty("_duration");
            SerializedProperty cooldown = serialized.FindProperty("_cooldown");
            SerializedProperty percent = serialized.FindProperty("_assignedCookingSpeedUpPercent");
            if (description == null || duration == null || cooldown == null || percent == null)
            {
                throw new InvalidOperationException(
                    target.StaffId + " Skill04 직렬화 필드를 찾을 수 없습니다.");
            }

            description.stringValue = OfficialDescription;
            duration.floatValue = target.OfficialDuration;
            cooldown.floatValue = target.OfficialCooldown;
            percent.floatValue = 150f;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException(
                    target.StaffId + " Skill04 값 적용에 실패했습니다.");
            }
        }

        private static void SetSkillReference(StaffData staff, SkillBase skill)
        {
            SerializedObject serialized = new SerializedObject(staff);
            SerializedProperty skillProperty = serialized.FindProperty("_skill");
            if (skillProperty == null)
            {
                throw new InvalidOperationException(staff.name + "._skill 필드를 찾을 수 없습니다.");
            }

            if (skillProperty.objectReferenceValue == skill)
            {
                return;
            }

            skillProperty.objectReferenceValue = skill;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException(staff.name + "._skill 참조 적용에 실패했습니다.");
            }

            EditorUtility.SetDirty(staff);
        }

        private static bool ValidatePostApplyFiles(
            MigrationBackup backup,
            List<string> errors)
        {
            bool valid = backup != null;
            for (int index = 0; index < Targets.Length && valid; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup;
                if (!backup.Targets.TryGetValue(target.StaffId, out targetBackup))
                {
                    errors.Add("Post-Apply backup 누락: " + target.StaffId);
                    valid = false;
                    continue;
                }

                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                bool targetValid = staff != null
                                   && AssetDatabase.AssetPathToGUID(target.StaffDataPath)
                                   == targetBackup.StaffDataGuid
                                   && CaptureSerializedFingerprint(staff, "_skill")
                                   == targetBackup.StaffDataFingerprint
                                   && ComputeAssetFileSha256(target.LegacyPath)
                                   == targetBackup.LegacyAssetSha256
                                   && ComputeAssetFileSha256(target.LegacyPath + ".meta")
                                   == targetBackup.LegacyMetaSha256;
                if (!targetValid)
                {
                    errors.Add("Post-Apply 비대상 값 또는 Legacy 파일 변경: " + target.StaffId);
                    valid = false;
                }
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

            bool valid = snapshot.Skills.Count == 32
                         && GetCount(classes, "SpeedUpSkill") == 23
                         && GetCount(classes, "TouchAddCustomerButtonSkill") == 5
                         && GetCount(classes, "AssignedCookingSpeedUpSkill") == 4
                         && classes.Count == 3
                         && shared == 0
                         && orphan == 0;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Inventory V4 불일치: active " + snapshot.Skills.Count
                    + ", class " + GetCount(classes, "SpeedUpSkill") + "/"
                    + GetCount(classes, "TouchAddCustomerButtonSkill") + "/"
                    + GetCount(classes, "AssignedCookingSpeedUpSkill")
                    + ", shared/orphan " + shared + "/" + orphan);
            }

            return valid;
        }

        private static bool ValidatePostDryRun(
            string officialFolder,
            List<string> errors)
        {
            StaffDataDryRunPlanSnapshot plan;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataDryRunPlanner.TryBuildReadOnlyPlan(
                    officialFolder,
                    out plan,
                    out diagnostics)
                || plan == null)
            {
                AddDiagnostics("Post-Apply Dry Run V4", diagnostics, errors);
                return false;
            }

            int warnings = 0;
            int prerequisites = 0;
            int changedFields = 0;
            int existingMismatch = 0;
            int newUnsupported = 0;
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
                }
            }

            bool valid = plan.PlanningPolicyVersion
                         == "STAFF_DRY_RUN_POLICY_2026_08_18_V4"
                         && existingMismatch == 13
                         && newUnsupported == 13
                         && prerequisites == 27
                         && warnings == 65
                         && durationMismatch == 12
                         && cooldownMismatch == 10
                         && changedFields == 2146
                         && existingWarnings == 18
                         && existingClass == 13
                         && existingSave == 1
                         && newReady == 47
                         && newClass == 13;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Dry Run V4 baseline 불일치: mismatch "
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
            List<string> createdAssetPaths,
            List<MigrationTarget> movedTargets,
            bool legacyFolderCreated,
            List<string> errors)
        {
            bool valid = true;
            try
            {
                for (int index = 0; index < movedTargets.Count; index++)
                {
                    MigrationTarget target = movedTargets[index];
                    StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                    SkillBase legacy = AssetDatabase.LoadAssetAtPath<SkillBase>(target.LegacyPath);
                    if (staff != null && legacy != null)
                    {
                        SetSkillReference(staff, legacy);
                    }
                }

                AssetDatabase.SaveAssets();
                for (int index = createdAssetPaths.Count - 1; index >= 0; index--)
                {
                    string path = createdAssetPaths[index];
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))
                        && !AssetDatabase.DeleteAsset(path))
                    {
                        errors.Add("신규 Skill04 삭제 실패: " + path);
                        valid = false;
                    }
                }

                for (int index = movedTargets.Count - 1; index >= 0; index--)
                {
                    MigrationTarget target = movedTargets[index];
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(target.LegacyPath)))
                    {
                        continue;
                    }

                    string moveError = AssetDatabase.MoveAsset(target.LegacyPath, target.ActivePath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        errors.Add(target.StaffId + " 원래 경로 복구 실패: " + moveError);
                        valid = false;
                    }
                }

                if (legacyFolderCreated
                    && AssetDatabase.IsValidFolder(StaffDataAssetInventoryReader.LegacySkillFolder)
                    && AssetDatabase.FindAssets(
                        string.Empty,
                        new[] { StaffDataAssetInventoryReader.LegacySkillFolder }).Length == 0
                    && !AssetDatabase.DeleteAsset(StaffDataAssetInventoryReader.LegacySkillFolder))
                {
                    errors.Add("새 LegacySkill 폴더 제거 실패.");
                    valid = false;
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

            MigrationInspection rollbackInspection;
            List<string> inspectionErrors = new List<string>();
            bool initialRestored = TryInspect(
                                       officialFolder,
                                       out rollbackInspection,
                                       inspectionErrors)
                                   && rollbackInspection.State == MigrationState.READY_TO_APPLY;
            if (!initialRestored)
            {
                errors.AddRange(inspectionErrors);
                errors.Add("Rollback 후 Initial 상태가 복구되지 않았습니다.");
                valid = false;
            }

            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup;
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                bool targetRestored = backup.Targets.TryGetValue(target.StaffId, out targetBackup)
                                      && staff != null
                                      && AssetDatabase.AssetPathToGUID(target.StaffDataPath)
                                      == targetBackup.StaffDataGuid
                                      && CaptureSerializedFingerprint(staff, "_skill")
                                      == targetBackup.StaffDataFingerprint
                                      && ComputeAssetFileSha256(target.ActivePath)
                                      == targetBackup.LegacyAssetSha256
                                      && ComputeAssetFileSha256(target.ActivePath + ".meta")
                                      == targetBackup.LegacyMetaSha256;
                if (!targetRestored)
                {
                    errors.Add("Rollback 원본 값 검증 실패: " + target.StaffId);
                    valid = false;
                }
            }

            return valid;
        }

        private static string CaptureSerializedFingerprint(
            UnityEngine.Object asset,
            string excludedPropertyPath)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
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

        private static bool IsExactSingleReference(
            IReadOnlyList<string> references,
            string expectedPath)
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
            output.AppendLine("[Skill04 Existing Staff Migration " + phase + "]");
            if (inspection != null)
            {
                output.AppendLine("Official Package Fingerprint: " + inspection.PackageFingerprint);
                output.AppendLine("Migration State: " + inspection.State);
                for (int index = 0; index < inspection.Targets.Count; index++)
                {
                    TargetInspection target = inspection.Targets[index];
                    output.AppendLine(
                        "- " + target.Target.StaffId + " " + target.State
                        + " | Active " + target.Target.ActivePath + " (" + target.ActiveGuid + ")"
                        + " | Legacy " + target.Target.LegacyPath + " (" + target.LegacyGuid + ")"
                        + " | Official " + target.Target.OfficialDuration + "/"
                        + target.Target.OfficialCooldown);
                }
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            output.AppendLine(
                phase + " "
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
            MigrationInspection after)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill04 Existing Staff Migration APPLY]");
            output.AppendLine("APPLY PASS");
            output.AppendLine("- LegacySkill 이동 및 GUID 보존: 4/4 PASS");
            output.AppendLine("- AssignedCookingSpeedUpSkill 생성: 4/4 PASS");
            output.AppendLine("- StaffData _skill 참조 교체: 4/4 PASS");
            output.AppendLine("- 신규 Skill04 GUID 유효·고유: 4/4 PASS");
            output.AppendLine("- Active Skill Inventory: 32 (23/5/4)");
            output.AppendLine("- Dry Run Policy: STAFF_DRY_RUN_POLICY_2026_08_18_V4");
            for (int index = 0; index < after.Targets.Count; index++)
            {
                TargetInspection target = after.Targets[index];
                output.AppendLine(
                    "- " + target.Target.StaffId + " Active GUID " + target.ActiveGuid
                    + " | Legacy GUID " + target.LegacyGuid);
            }

            Debug.Log(output.ToString());
        }

        private enum MigrationState
        {
            INVALID,
            READY_TO_APPLY,
            ALREADY_APPLIED,
            PARTIAL_OR_INVALID
        }

        private enum TargetState
        {
            INVALID,
            INITIAL,
            APPLIED
        }

        private sealed class MigrationTarget
        {
            internal string StaffId { get; }
            internal string OfficialName { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal float OfficialDuration { get; }
            internal float OfficialCooldown { get; }
            internal string StaffDataPath { get { return "Assets/Resources/StaffData/" + StaffId + ".asset"; } }
            internal string ActivePath { get { return StaffDataAssetInventoryReader.SkillFolder + "/" + FileName; } }
            internal string LegacyPath { get { return StaffDataAssetInventoryReader.LegacySkillFolder + "/" + FileName; } }

            internal MigrationTarget(
                string staffId,
                string officialName,
                string fileName,
                string objectName,
                string legacyGuid,
                float legacyDuration,
                float legacyCooldown,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                OfficialName = officialName;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
            }
        }

        private sealed class TargetInspection
        {
            internal MigrationTarget Target { get; }
            internal TargetState State;
            internal string StaffGuid = string.Empty;
            internal string ActiveGuid = string.Empty;
            internal string LegacyGuid = string.Empty;

            internal TargetInspection(MigrationTarget target)
            {
                Target = target;
            }
        }

        private sealed class MigrationInspection
        {
            internal string OfficialFolder { get; }
            internal string PackageFingerprint = string.Empty;
            internal MigrationState State;
            internal readonly List<TargetInspection> Targets = new List<TargetInspection>();

            internal MigrationInspection(string officialFolder)
            {
                OfficialFolder = officialFolder;
            }
        }

        private sealed class MigrationBackup
        {
            internal readonly Dictionary<string, TargetBackup> Targets =
                new Dictionary<string, TargetBackup>(StringComparer.Ordinal);
        }

        private sealed class TargetBackup
        {
            internal string StaffDataGuid { get; }
            internal string StaffDataFingerprint { get; }
            internal string LegacyAssetSha256 { get; }
            internal string LegacyMetaSha256 { get; }

            internal TargetBackup(
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
