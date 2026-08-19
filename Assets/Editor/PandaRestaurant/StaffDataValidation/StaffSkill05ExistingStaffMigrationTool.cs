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
    internal static class StaffSkill05ExistingStaffMigrationTool
    {
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Skill05 Existing Staff Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Skill05 Existing Staff Migration";
        private const string Final17Sha256 =
            "ce51987f36fec57b434d3b3fcaf796a5fbd4d907a5dd8a0b6b9c507db392c68d";
        private const string SkillTypeSha256 =
            "26fdb35b14296418c094a929ddaa040ba94d24ebd20108adab4aaad666b46ad7";
        private const string FoodPriceScriptPath =
            "Assets/Scripts/Staff/StaffSkill/FoodPriceUpSkill.cs";
        private const string FoodPriceScriptGuid =
            "2166c039b7672614f9452cc7ec63189a";
        private const string OfficialSkillId = "STAFF_SKILL05";
        private const string OfficialDescription =
            "음식 가격 (50%)증가";

        private static readonly MigrationTarget[] Targets =
        {
            new MigrationTarget("STAFF04", "루피", "웨이터", "23ed6c08345350a4bb78cfb28f65c0fe", "Staff04Skill.asset", "STAFF04Skill", "ace4d82401b0b084691be89b9fc0f63e", "SpeedUpSkill", 100, 16, 150, 30, 200),
            new MigrationTarget("STAFF05", "샐린", "웨이터", "67271e431b72f4541ade0d589907574f", "Staff05Skill.asset", "STAFF05Skill", "224358ced57ae144ba65a5f1692e6f1c", "SpeedUpSkill", 100, 18, 150, 30, 200),
            new MigrationTarget("STAFF07", "바쿠", "매니저", "436f6fb37766a674aa3e8feb4c89a9be", "STAFF07Skill.asset", "STAFF07Skill", "18348c8c96719374da1f6ba1bcd2987d", "SpeedUpSkill", 100, 10, 150, 30, 200),
            new MigrationTarget("STAFF13", "라라", "치어리더", "9718408ac4584a94e9693feb2ec7e087", "Staff13Skill.asset", "STAFF13Skill", "cc1627697d70f09418e134e717be2a29", "TouchAddCustomerButtonSkill", 0.5f, 13, 150, 30, 200),
            new MigrationTarget("STAFF26", "베르베르", "가드", "edc23f451ea3108419720297d7a49a30", "Staff26Skill.asset", "STAFF26Skill", "f3029210afede654ca6f3c33a2016896", "SpeedUpSkill", 100, 30, 50, 35, 190),
            new MigrationTarget("STAFF32", "슈슈", "청소부", "2e43896a39f712a4da691ea084db98f1", "Staff32Skill.asset", "Staff32Skill", "9579cb0591dadfa4da2e662700923026", "SpeedUpSkill", 100, 30, 80, 35, 190)
        };

        private static readonly HashSet<string> ExpectedNewSkill05Staff =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF37", "STAFF49", "STAFF54", "STAFF73"
            };

        private static readonly Dictionary<string, OfficialTarget> ExpectedNewSkill05Targets =
            new Dictionary<string, OfficialTarget>(StringComparer.Ordinal)
            {
                { "STAFF37", new OfficialTarget("베테랑 포야", "매니저", 32, 195) },
                { "STAFF49", new OfficialTarget("성실한 멜로", "청소부", 32, 195) },
                { "STAFF54", new OfficialTarget("베테랑 판다 마쉬", "매니저", 32, 195) },
                { "STAFF73", new OfficialTarget("탕추추", "웨이터", 32, 195) }
            };

        private static readonly LegacyRequirement[] ExistingLegacyAssets =
        {
            new LegacyRequirement("STAFF09", "STAFF09Skill.asset", "STAFF09Skill", "3576053ed0b398d43a296e16eaf3aff6", 16, 150),
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
                    "[Skill05 Existing Staff Migration]\n"
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
                Debug.LogWarning("Skill05 Existing Staff Migration was cancelled. 변경 0개.");
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
                Debug.Log("ALREADY_APPLIED: Skill05 기존 직원 6명은 이미 완전히 전환됐습니다. 변경 0개.");
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
                "Apply Skill05 Existing Staff Migration",
                "STAFF04, STAFF05, STAFF07, STAFF13, STAFF26, STAFF32의 구형 Skill을 LegacySkill로 이동하고 "
                + "공식 Skill05 Asset을 만든 뒤 StaffData의 _skill 참조만 교체합니다.\n\n"
                + "계속하시겠습니까?",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                Debug.LogWarning("Skill05 Existing Staff Migration Apply가 취소됐습니다. 변경 0개.");
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
            if (!ValidateOfficialSkill05(official, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateFoodPriceScript(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateExistingLegacyAssets(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            int initialCount = 0;
            int appliedCount = 0;
            HashSet<string> appliedGuids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> legacyGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Targets.Length; index++)
            {
                legacyGuids.Add(Targets[index].LegacyGuid);
            }

            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                legacyGuids.Add(ExistingLegacyAssets[index].Guid);
            }

            for (int index = 0; index < Targets.Length; index++)
            {
                TargetInspection targetInspection = InspectTarget(Targets[index], errors);
                inspection.Targets.Add(targetInspection);
                initialCount += targetInspection.State == TargetState.INITIAL ? 1 : 0;
                appliedCount += targetInspection.State == TargetState.APPLIED ? 1 : 0;
                if (targetInspection.State == TargetState.APPLIED
                    && (!appliedGuids.Add(targetInspection.ActiveGuid)
                        || legacyGuids.Contains(targetInspection.ActiveGuid)))
                {
                    errors.Add("신규 Skill05 GUID가 중복되었습니다: " + targetInspection.ActiveGuid);
                }
            }

            if (errors.Count != 0)
            {
                inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
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

            inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: Initial " + initialCount
                + "/" + Targets.Length + ", Applied " + appliedCount
                + "/" + Targets.Length);
            return false;
        }

        private static bool ValidateOfficialSkill05(
            StaffOfficialDataPackageSnapshot official,
            List<string> errors)
        {
            StaffOfficialFileSnapshot final17;
            StaffOfficialFileSnapshot skillType;
            if (!official.TryGetFile("Final17", out final17)
                || !official.TryGetFile("SkillType", out skillType))
            {
                errors.Add("OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: Final17 또는 SkillType Snapshot이 없습니다.");
                return false;
            }

            bool valid = true;
            if (final17.Sha256 != Final17Sha256)
            {
                errors.Add("Final17 SHA-256 불일치: " + final17.Sha256);
                valid = false;
            }

            if (skillType.Sha256 != SkillTypeSha256)
            {
                errors.Add("SkillType SHA-256 불일치: " + skillType.Sha256);
                valid = false;
            }

            int definitionCount = 0;
            for (int index = 0; index < skillType.Rows.Count; index++)
            {
                IReadOnlyList<string> row = skillType.Rows[index];
                if (row.Count >= 2 && row[0].Trim() == OfficialSkillId)
                {
                    definitionCount++;
                    if (row[1].Trim() != OfficialDescription)
                    {
                        errors.Add("OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: SkillType 설명 불일치.");
                        valid = false;
                    }
                }
            }

            Dictionary<string, IReadOnlyList<string>> skill05Rows =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int index = 0; index < final17.Rows.Count; index++)
            {
                IReadOnlyList<string> row = final17.Rows[index];
                if (row.Count >= 14 && row[10].Trim() == OfficialSkillId)
                {
                    string id = row[0].Trim();
                    if (skill05Rows.ContainsKey(id))
                    {
                        errors.Add("OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: 중복 ID " + id);
                        valid = false;
                    }
                    else
                    {
                        skill05Rows.Add(id, row);
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
                bool targetValid = skill05Rows.TryGetValue(target.StaffId, out row)
                                   && row.Count >= 14
                                   && row[1].Trim() == target.OfficialName
                                   && row[6].Trim() == target.OfficialRole
                                   && TryParseSeconds(row[12], out duration)
                                   && TryParseSeconds(row[13], out cooldown)
                                   && Approximately(duration, target.OfficialDuration)
                                   && Approximately(cooldown, target.OfficialCooldown);
                if (!targetValid)
                {
                    errors.Add("OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: " + target.StaffId);
                    valid = false;
                }
            }

            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in skill05Rows.Keys)
            {
                if (!expectedExisting.Contains(id))
                {
                    actualNew.Add(id);
                }
            }

            foreach (KeyValuePair<string, OfficialTarget> pair in ExpectedNewSkill05Targets)
            {
                IReadOnlyList<string> row;
                double duration;
                double cooldown;
                OfficialTarget target = pair.Value;
                bool targetValid = skill05Rows.TryGetValue(pair.Key, out row)
                                   && row.Count >= 14
                                   && row[1].Trim() == target.Name
                                   && row[6].Trim() == target.Role
                                   && TryParseSeconds(row[12], out duration)
                                   && TryParseSeconds(row[13], out cooldown)
                                   && Approximately(duration, target.Duration)
                                   && Approximately(cooldown, target.Cooldown);
                if (!targetValid)
                {
                    errors.Add("OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: " + pair.Key);
                    valid = false;
                }
            }

            if (definitionCount != 1
                || skill05Rows.Count != 10
                || expectedExisting.Count != 6
                || !actualNew.SetEquals(ExpectedNewSkill05Staff))
            {
                errors.Add(
                    "OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: 정의 " + definitionCount
                    + ", 전체 " + skill05Rows.Count + ", 기존 " + expectedExisting.Count
                    + ", 신규 " + actualNew.Count);
                valid = false;
            }

            return valid;
        }

        private static bool ValidateFoodPriceScript(List<string> errors)
        {
            string guid = AssetDatabase.AssetPathToGUID(FoodPriceScriptPath);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(FoodPriceScriptPath);
            bool valid = guid == FoodPriceScriptGuid
                         && script != null
                         && script.GetClass() == typeof(FoodPriceUpSkill);
            if (!valid)
            {
                errors.Add(
                    "FoodPriceUpSkill Script 또는 GUID가 기준과 다릅니다. 실제 GUID: "
                    + guid);
            }

            return valid;
        }

        private static bool ValidateExistingLegacyAssets(List<string> errors)
        {
            if (!AssetDatabase.IsValidFolder(StaffDataAssetInventoryReader.LegacySkillFolder))
            {
                errors.Add("기존 LegacySkill 폴더가 없습니다.");
                return false;
            }

            bool valid = true;
            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                LegacyRequirement target = ExistingLegacyAssets[index];
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                string guid = AssetDatabase.AssetPathToGUID(path);
                SpeedUpSkill skill = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(path);
                bool preserved = skill != null
                                 && guid == target.Guid
                                 && skill.name == target.ObjectName
                                 && string.IsNullOrEmpty(skill.Description)
                                 && Approximately(skill.Duration, target.Duration)
                                 && Approximately(skill.Cooldown, target.Cooldown)
                                 && Approximately(skill.FirstValue, 100d)
                                 && FindAssetReferences(path).Count == 0;
                if (!preserved)
                {
                    errors.Add("기존 Skill04·06 Legacy Asset 기준 불일치: " + target.StaffId);
                    valid = false;
                }
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
            if (staffGuid != target.StaffDataGuid || skillReference == null)
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

            bool legacyPathFree = legacyObject == null && string.IsNullOrEmpty(legacyGuid);
            List<string> initialReferences = FindAssetReferences(target.ActivePath);
            bool initial = MatchesLegacySkill(activeObject, target)
                           && activeGuid == target.LegacyGuid
                           && referencedPath == target.ActivePath
                           && legacyPathFree
                           && IsExactSingleReference(initialReferences, target.StaffDataPath);
            if (initial)
            {
                result.State = TargetState.INITIAL;
                return result;
            }

            FoodPriceUpSkill activeSkill = activeObject as FoodPriceUpSkill;
            List<string> activeReferences = FindAssetReferences(target.ActivePath);
            List<string> legacyReferences = FindAssetReferences(target.LegacyPath);
            bool applied = activeSkill != null
                           && MatchesLegacySkill(legacyObject, target)
                           && IsUnityGuid(activeGuid)
                           && activeGuid != target.LegacyGuid
                           && legacyGuid == target.LegacyGuid
                           && referencedPath == target.ActivePath
                           && activeSkill.name == target.ObjectName
                           && activeSkill.Description == OfficialDescription
                           && Approximately(activeSkill.Duration, target.OfficialDuration)
                           && Approximately(activeSkill.Cooldown, target.OfficialCooldown)
                           && Approximately(activeSkill.FirstValue, 50d)
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

        private static bool MatchesLegacySkill(
            UnityEngine.Object asset,
            MigrationTarget target)
        {
            SkillBase skill = asset as SkillBase;
            if (skill == null
                || skill.GetType().Name != target.LegacyClassName
                || skill.name != target.ObjectName
                || !string.IsNullOrEmpty(skill.Description)
                || !Approximately(skill.Duration, target.LegacyDuration)
                || !Approximately(skill.Cooldown, target.LegacyCooldown))
            {
                return false;
            }

            return Approximately(skill.FirstValue, target.LegacyEffectValue);
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

                Dictionary<string, FoodPriceUpSkill> newAssets =
                    new Dictionary<string, FoodPriceUpSkill>(StringComparer.Ordinal);
                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    FoodPriceUpSkill skill =
                        ScriptableObject.CreateInstance<FoodPriceUpSkill>();
                    AssetDatabase.CreateAsset(skill, target.ActivePath);
                    createdAssetPaths.Add(target.ActivePath);
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(target.ActivePath)))
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " 신규 Skill05 Asset 생성에 실패했습니다.");
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
                output.AppendLine("[Skill05 Existing Staff Migration]");
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

            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                LegacyRequirement target = ExistingLegacyAssets[index];
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                backup.ExistingLegacyFiles.Add(
                    target.StaffId,
                    new FileBackup(
                        ComputeAssetFileSha256(path),
                        ComputeAssetFileSha256(path + ".meta")));
            }

            return backup;
        }

        private static void ConfigureNewSkill(
            FoodPriceUpSkill skill,
            MigrationTarget target)
        {
            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty description = serialized.FindProperty("_description");
            SerializedProperty duration = serialized.FindProperty("_duration");
            SerializedProperty cooldown = serialized.FindProperty("_cooldown");
            SerializedProperty percent = serialized.FindProperty("_foodPriceUpPercent");
            if (description == null || duration == null || cooldown == null || percent == null)
            {
                throw new InvalidOperationException(
                    target.StaffId + " Skill05 직렬화 필드를 찾을 수 없습니다.");
            }

            description.stringValue = OfficialDescription;
            duration.floatValue = target.OfficialDuration;
            cooldown.floatValue = target.OfficialCooldown;
            percent.floatValue = 50f;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException(
                    target.StaffId + " Skill05 값 적용에 실패했습니다.");
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

            valid &= ValidateExistingLegacyFiles(backup, errors, "Post-Apply");

            return valid;
        }

        private static bool ValidateExistingLegacyFiles(
            MigrationBackup backup,
            List<string> errors,
            string phase)
        {
            if (backup == null)
            {
                return false;
            }

            bool valid = true;
            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                LegacyRequirement target = ExistingLegacyAssets[index];
                FileBackup fileBackup;
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                bool unchanged = backup.ExistingLegacyFiles.TryGetValue(target.StaffId, out fileBackup)
                                 && ComputeAssetFileSha256(path) == fileBackup.AssetSha256
                                 && ComputeAssetFileSha256(path + ".meta") == fileBackup.MetaSha256;
                if (!unchanged)
                {
                    errors.Add(phase + " 기존 Skill04·06 Legacy 파일 변경: " + target.StaffId);
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

            int legacySkillCount = 0;
            string[] legacyGuids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { StaffDataAssetInventoryReader.LegacySkillFolder });
            for (int index = 0; index < legacyGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(legacyGuids[index]);
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.LoadAssetAtPath<SkillBase>(path) != null)
                {
                    legacySkillCount++;
                }
            }

            bool valid = snapshot.Skills.Count == 32
                         && GetCount(classes, "SpeedUpSkill") == 17
                         && GetCount(classes, "TouchAddCustomerButtonSkill") == 4
                         && GetCount(classes, "AssignedCookingSpeedUpSkill") == 4
                         && GetCount(classes, "FoodPaymentTipUpSkill") == 1
                         && GetCount(classes, "FoodPriceUpSkill") == 6
                         && classes.Count == 5
                         && legacySkillCount == 11
                         && shared == 0
                         && orphan == 0;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Inventory V6 불일치: active " + snapshot.Skills.Count
                    + ", class " + GetCount(classes, "SpeedUpSkill") + "/"
                    + GetCount(classes, "TouchAddCustomerButtonSkill") + "/"
                    + GetCount(classes, "AssignedCookingSpeedUpSkill") + "/"
                    + GetCount(classes, "FoodPaymentTipUpSkill") + "/"
                    + GetCount(classes, "FoodPriceUpSkill")
                    + ", legacy " + legacySkillCount
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
                AddDiagnostics("Post-Apply Dry Run V6", diagnostics, errors);
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
                         == "STAFF_DRY_RUN_POLICY_2026_08_19_V6"
                         && existingMismatch == 6
                         && newUnsupported == 11
                         && prerequisites == 18
                         && warnings == 65
                         && durationMismatch == 5
                         && cooldownMismatch == 3
                         && changedFields == 2146
                         && existingWarnings == 25
                         && existingClass == 6
                         && existingSave == 1
                         && newReady == 49
                         && newClass == 11;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Dry Run V6 baseline 불일치: mismatch "
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
                        errors.Add("신규 Skill05 삭제 실패: " + path);
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

            valid &= ValidateExistingLegacyFiles(backup, errors, "Rollback");

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
            output.AppendLine("[Skill05 Existing Staff Migration " + phase + "]");
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

                if (inspection.State == MigrationState.READY_TO_APPLY)
                {
                    output.AppendLine("Expected: 구형 Skill 6개 Legacy 이동, FoodPriceUpSkill 6개 생성, StaffData._skill 6개 교체");
                    output.AppendLine("New STAFF37/49/54/73: 계획 검증만 수행, 실제 Asset 생성 없음");
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
            if (phase == "PREVIEW")
            {
                output.AppendLine(
                    "SKILL05 EXISTING STAFF MIGRATION PREVIEW: "
                    + (passed ? "PASS" : "FAIL"));
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

        private static void LogApplySuccess(
            MigrationInspection before,
            MigrationInspection after)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Skill05 Existing Staff Migration APPLY]");
            output.AppendLine("APPLY PASS");
            output.AppendLine("- LegacySkill 이동 및 GUID 보존: 6/6 PASS");
            output.AppendLine("- FoodPriceUpSkill 생성: 6/6 PASS");
            output.AppendLine("- StaffData _skill 참조 교체: 6/6 PASS");
            output.AppendLine("- 신규 Skill05 GUID 유효·고유: 6/6 PASS");
            output.AppendLine("- 기존 Skill04·06 Legacy 회귀: 5/5 PASS");
            output.AppendLine("- Active Skill Inventory: 32 (17/4/4/1/6)");
            output.AppendLine("- Dry Run Policy: STAFF_DRY_RUN_POLICY_2026_08_19_V6");
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
            PARTIAL_MIGRATION_STATE
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
            internal string OfficialRole { get; }
            internal string StaffDataGuid { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal string LegacyClassName { get; }
            internal float LegacyEffectValue { get; }
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
                string officialRole,
                string staffDataGuid,
                string fileName,
                string objectName,
                string legacyGuid,
                string legacyClassName,
                float legacyEffectValue,
                float legacyDuration,
                float legacyCooldown,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                OfficialName = officialName;
                OfficialRole = officialRole;
                StaffDataGuid = staffDataGuid;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyClassName = legacyClassName;
                LegacyEffectValue = legacyEffectValue;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
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
            internal readonly Dictionary<string, FileBackup> ExistingLegacyFiles =
                new Dictionary<string, FileBackup>(StringComparer.Ordinal);
        }

        private sealed class FileBackup
        {
            internal string AssetSha256 { get; }
            internal string MetaSha256 { get; }

            internal FileBackup(string assetSha256, string metaSha256)
            {
                AssetSha256 = assetSha256;
                MetaSha256 = metaSha256;
            }
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
