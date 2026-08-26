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
    internal static class StaffChefPassiveExistingStaffMigrationTool
    {
        private const string PolicyMarker = "CHEF_PASSIVE_MIGRATION_POLICY_2026_08_21_V1";
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Chef Passive Existing Staff Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Chef Passive Existing Staff Migration";
        private const string OfficialPackageFingerprint =
            "be7613e884b5ae18dc94e57abc0c941dccfb09486ae9fc5ff75acf4b0e4703af";
        private const string InitialInventoryFingerprint =
            "38494f37574b54397201eb0c4f2120be4959275181ba44bb0b27bba6abf74eaa";
        private const string ChefDataScriptGuid =
            "93bdf2a61116f5a4bbf3c395f44840da";
        private const string LevelArrayPath = "_chefLevelData";
        private const string PassiveFieldName = "_foodSpeedAddPercent";

        private static readonly MigrationTarget[] Targets =
        {
            new MigrationTarget("STAFF16", "NORMAL", "da705e4f8a75fb14e9dc72bc28ccbe1f", 50f, 5f, 5.5f, 6f, 6.5f, 7f),
            new MigrationTarget("STAFF17", "NORMAL", "0f3d8c7113aeaf842b4abdca0d6e3428", 50f, 5.5f, 6f, 6.5f, 7f, 7.5f),
            new MigrationTarget("STAFF18", "NORMAL", "99decdeb5bd9c7340bf68f89584f0d59", 50f, 6f, 6.5f, 7f, 7.5f, 8f),
            new MigrationTarget("STAFF19", "NORMAL", "770fa62dcd6b3ba4dabc3902f20dec83", 50f, 6.5f, 7f, 7.5f, 8f, 8.5f),
            new MigrationTarget("STAFF20", "NORMAL", "ef0624ef5a06d7645ad0d42fa2cf9a92", 50f, 7f, 7.5f, 8f, 8.5f, 9f),
            new MigrationTarget("STAFF27", "SPECIAL", "9b53d48f70593ca4bb248b9fa7193cdd", 200f, 12f, 13f, 14f, 15f, 16f),
            new MigrationTarget("STAFF29", "UNIQUE", "d633b00ca05bb73439909966100a0a09", 100f, 7.5f, 8f, 8.5f, 9f, 9.5f)
        };

        [MenuItem(PreviewMenuPath)]
        private static void PreviewMigration()
        {
            MigrationInspection inspection;
            List<string> errors = new List<string>();
            bool inspected = TryInspect(out inspection, errors);
            LogInspection("PREVIEW", inspection, errors, inspected);
        }

        [MenuItem(ApplyMenuPath)]
        private static void ApplyMigrationFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "[Chef Passive Existing Staff Migration]\n"
                    + "APPLY FAIL: Play Mode에서는 실행할 수 없습니다.");
                return;
            }

            MigrationInspection inspection;
            List<string> errors = new List<string>();
            if (!TryInspect(out inspection, errors) || inspection == null)
            {
                LogInspection("APPLY", inspection, errors, false);
                return;
            }

            if (inspection.State == MigrationState.ALREADY_APPLIED)
            {
                LogInspection("APPLY", inspection, errors, true);
                Debug.Log("ALREADY_APPLIED: Chef 7명의 공식 패시브가 이미 적용되어 있습니다. 변경 0개.");
                return;
            }

            if (inspection.State != MigrationState.READY_TO_APPLY)
            {
                errors.Add("PARTIAL_MIGRATION_STATE: 일부 적용 또는 기준 밖 상태이므로 쓰기를 차단했습니다.");
                LogInspection("APPLY", inspection, errors, false);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Apply Chef Passive Existing Staff Migration",
                    "StaffData 7개의 _foodSpeedAddPercent 숫자 35개만 공식값으로 변경합니다.\n\n"
                    + "다른 StaffData 필드, Skill Asset, GUID는 변경하지 않습니다.\n\n"
                    + "계속하시겠습니까?",
                    "Apply",
                    "Cancel"))
            {
                Debug.LogWarning("Chef Passive Existing Staff Migration Apply가 취소되었습니다. 변경 0개.");
                return;
            }

            ApplyMigration();
        }

        private static bool TryInspect(
            out MigrationInspection inspection,
            List<string> errors)
        {
            inspection = new MigrationInspection();
            string activeFolder;
            StaffOfficialDataSourceKind sourceKind;
            string resolveError;
            if (!StaffOfficialDataPathResolver.TryResolveActiveFolder(
                    out activeFolder,
                    out sourceKind,
                    out resolveError))
            {
                errors.Add("OfficialData 경로 확인 실패: " + resolveError);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.ActiveFolder = activeFolder;
            inspection.SourceKind = sourceKind;
            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> officialDiagnostics;
            if (!StaffDataPackValidator.TryBuildCanonicalV8ReadOnlySnapshot(
                    out official,
                    out officialDiagnostics)
                || official == null)
            {
                AddDiagnostics("Official Snapshot", officialDiagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.PackageFingerprint = official.PackageFingerprint;
            if (!ValidateOfficialTargets(official, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            StaffDataAssetInventorySnapshot inventory;
            IReadOnlyList<string> inventoryDiagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out inventory,
                    out inventoryDiagnostics)
                || inventory == null)
            {
                AddDiagnostics("Current Inventory", inventoryDiagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.InventoryFingerprint = inventory.InventoryFingerprint;
            if (!ValidateUniqueStaffGuids(inventory, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            bool structuralValid = true;
            bool allIdentity = true;
            bool allBaseline = true;
            bool allTarget = true;
            for (int index = 0; index < Targets.Length; index++)
            {
                TargetInspection targetInspection = InspectTarget(Targets[index], inventory, errors);
                inspection.Targets.Add(targetInspection);
                structuralValid &= targetInspection.StructuralValid;
                allIdentity &= targetInspection.IdentityMatches;
                allBaseline &= targetInspection.IsBaseline;
                allTarget &= targetInspection.IsTarget;
            }

            if (!structuralValid)
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (allIdentity && allBaseline)
            {
                if (inventory.InventoryFingerprint != InitialInventoryFingerprint)
                {
                    errors.Add(
                        "CHEF_PASSIVE_ASSET_BASELINE_CHANGED: 초기 값은 맞지만 InventoryFingerprint가 다릅니다: "
                        + inventory.InventoryFingerprint);
                    inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
                    return true;
                }

                inspection.State = MigrationState.READY_TO_APPLY;
                return true;
            }

            if (allIdentity && allTarget)
            {
                inspection.State = MigrationState.ALREADY_APPLIED;
                return true;
            }

            inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
            return true;
        }

        private static bool ValidateOfficialTargets(
            StaffOfficialDataPackageSnapshot official,
            List<string> errors)
        {
            if (official.PackageFingerprint != OfficialPackageFingerprint)
            {
                errors.Add(
                    "OFFICIAL_CHEF_PASSIVE_TARGET_CHANGED: PackageFingerprint "
                    + official.PackageFingerprint);
                return false;
            }

            StaffOfficialFileSnapshot finalStaff;
            StaffOfficialFileSnapshot roleBase;
            if (!official.TryGetFile(StaffOfficialDataPackageKeys.FinalStaff, out finalStaff)
                || !official.TryGetFile("RoleBase", out roleBase)
                || finalStaff == null
                || roleBase == null)
            {
                errors.Add("OFFICIAL_CHEF_PASSIVE_TARGET_CHANGED: FinalStaff 또는 RoleBase가 없습니다.");
                return false;
            }

            Dictionary<string, IReadOnlyList<string>> staffRows =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            Dictionary<string, int> chefGrades =
                new Dictionary<string, int>(StringComparer.Ordinal);
            int chefCount = 0;
            for (int index = 0; index < finalStaff.Rows.Count; index++)
            {
                IReadOnlyList<string> row = finalStaff.Rows[index];
                if (row.Count < 17)
                {
                    errors.Add("OFFICIAL_CHEF_PASSIVE_TARGET_CHANGED: FinalStaff 열 수가 부족합니다.");
                    return false;
                }

                string id = row[0].Trim();
                if (!staffRows.ContainsKey(id))
                {
                    staffRows.Add(id, row);
                }

                if (row[6].Trim() != "주방장")
                {
                    continue;
                }

                string gradeKey;
                if (!TryGetGradeKey(row[4], out gradeKey))
                {
                    errors.Add("OFFICIAL_CHEF_PASSIVE_TARGET_CHANGED: Chef 등급 해석 실패 " + id);
                    return false;
                }

                chefCount++;
                int count;
                chefGrades.TryGetValue(gradeKey, out count);
                chefGrades[gradeKey] = count + 1;
            }

            Dictionary<string, double> roleBaseValues =
                new Dictionary<string, double>(StringComparer.Ordinal);
            for (int index = 0; index < roleBase.Rows.Count; index++)
            {
                IReadOnlyList<string> row = roleBase.Rows[index];
                double value;
                if (row.Count >= 4
                    && row[0].Trim() == "CHEF"
                    && row[2].Trim() == "COOKING_EFFICIENCY"
                    && double.TryParse(
                        row[3],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value))
                {
                    roleBaseValues[row[1].Trim()] = value;
                }
            }

            bool valid = chefCount == 23
                         && GetCount(chefGrades, "NORMAL") == 5
                         && GetCount(chefGrades, "RARE") == 7
                         && GetCount(chefGrades, "UNIQUE") == 9
                         && GetCount(chefGrades, "SPECIAL") == 2
                         && RoleBaseEquals(roleBaseValues, "NORMAL", 50d)
                         && RoleBaseEquals(roleBaseValues, "RARE", 70d)
                         && RoleBaseEquals(roleBaseValues, "UNIQUE", 100d)
                         && RoleBaseEquals(roleBaseValues, "SPECIAL", 200d);
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                IReadOnlyList<string> row;
                string gradeKey;
                bool rowValid = staffRows.TryGetValue(target.StaffId, out row)
                                && row.Count >= 17
                                && row[6].Trim() == "주방장"
                                && TryGetGradeKey(row[4], out gradeKey)
                                && gradeKey == target.GradeKey
                                && RoleBaseEquals(
                                    roleBaseValues,
                                    target.GradeKey,
                                    target.TargetValue);
                if (!rowValid)
                {
                    errors.Add("OFFICIAL_CHEF_PASSIVE_TARGET_CHANGED: " + target.StaffId);
                    valid = false;
                }
            }

            if (!valid)
            {
                errors.Add(
                    "OFFICIAL_CHEF_PASSIVE_TARGET_CHANGED: Chef/grade " + chefCount + " / "
                    + GetCount(chefGrades, "NORMAL") + "/" + GetCount(chefGrades, "RARE")
                    + "/" + GetCount(chefGrades, "UNIQUE") + "/"
                    + GetCount(chefGrades, "SPECIAL"));
            }

            return valid;
        }

        private static TargetInspection InspectTarget(
            MigrationTarget target,
            StaffDataAssetInventorySnapshot inventory,
            List<string> errors)
        {
            TargetInspection result = new TargetInspection(target);
            StaffDataAssetSnapshot snapshot;
            if (!inventory.TryGetStaff(target.StaffId, out snapshot) || snapshot == null)
            {
                errors.Add("CHEF_PASSIVE_ASSET_BASELINE_CHANGED: Asset 누락 " + target.StaffId);
                return result;
            }

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(snapshot.AssetPath);
            if (asset == null)
            {
                errors.Add("CHEF_PASSIVE_ASSET_BASELINE_CHANGED: Asset 로드 실패 " + target.StaffId);
                return result;
            }

            string projectRoot = StaffOfficialDataPathResolver.ProjectRoot;
            string metaPath = Path.Combine(
                projectRoot,
                (snapshot.AssetPath + ".meta").Replace('/', Path.DirectorySeparatorChar));
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty levels = serialized.FindProperty(LevelArrayPath);
            if (levels == null
                || !levels.isArray
                || !File.Exists(metaPath)
                || snapshot.HasMissingRequiredReference)
            {
                errors.Add("CHEF_PASSIVE_ASSET_BASELINE_CHANGED: 구조 또는 참조 오류 " + target.StaffId);
                return result;
            }

            result.StructuralValid = true;
            result.IdentityMatches = snapshot.AssetPath == target.AssetPath
                                     && snapshot.AssetGuid == target.AssetGuid
                                     && snapshot.ScriptGuid == ChefDataScriptGuid
                                     && snapshot.ConcreteTypeName == "ChefData"
                                     && snapshot.UnityObjectName == target.StaffId
                                     && snapshot.Id == target.StaffId
                                     && snapshot.LevelCount == 5
                                     && levels.arraySize == 5;
            for (int levelIndex = 0; levelIndex < levels.arraySize; levelIndex++)
            {
                SerializedProperty value = levels.GetArrayElementAtIndex(levelIndex)
                    .FindPropertyRelative(PassiveFieldName);
                if (value == null || value.propertyType != SerializedPropertyType.Float)
                {
                    errors.Add(
                        "CHEF_PASSIVE_ASSET_BASELINE_CHANGED: 직렬화 필드 누락 "
                        + target.StaffId + " Lv." + (levelIndex + 1));
                    result.StructuralValid = false;
                    return result;
                }

                result.CurrentValues.Add(value.floatValue);
            }

            result.IsBaseline = ValuesEqual(result.CurrentValues, target.BaselineValues);
            result.IsTarget = ValuesEqual(result.CurrentValues, target.TargetValues);
            return result;
        }

        private static bool ValidateUniqueStaffGuids(
            StaffDataAssetInventorySnapshot inventory,
            List<string> errors)
        {
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < inventory.Staff.Count; index++)
            {
                string guid = inventory.Staff[index].AssetGuid;
                if (string.IsNullOrEmpty(guid) || !guids.Add(guid))
                {
                    errors.Add("CHEF_PASSIVE_ASSET_BASELINE_CHANGED: StaffData GUID 중복 또는 누락 " + guid);
                    return false;
                }
            }

            return true;
        }

        private static void ApplyMigration()
        {
            MigrationInspection reinspection;
            List<string> preflightErrors = new List<string>();
            if (!TryInspect(out reinspection, preflightErrors)
                || reinspection == null
                || reinspection.State != MigrationState.READY_TO_APPLY)
            {
                preflightErrors.Add("Apply 직전 전체 Preflight가 READY_TO_APPLY가 아닙니다.");
                LogInspection("APPLY", reinspection, preflightErrors, false);
                return;
            }

            MigrationBackup backup = null;
            bool writesStarted = false;
            try
            {
                backup = CaptureBackup();
                for (int index = 0; index < Targets.Length; index++)
                {
                    writesStarted = true;
                    SetTargetValues(Targets[index], Targets[index].TargetValues);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                List<string> verificationErrors = new List<string>();
                MigrationInspection applied;
                bool stateValid = TryInspect(out applied, verificationErrors)
                                  && applied != null
                                  && applied.State == MigrationState.ALREADY_APPLIED;
                bool invariantsValid = ValidatePostApplyInvariants(backup, verificationErrors);
                bool inventoryValid = ValidatePostApplyInventory(
                    backup.InventoryFingerprint,
                    verificationErrors);
                bool dryRunValid = ValidatePostApplyDryRun(verificationErrors);
                if (!stateValid || !invariantsValid || !inventoryValid || !dryRunValid)
                {
                    throw new InvalidOperationException(
                        "Post-Apply 검증 실패: " + string.Join(" | ", verificationErrors));
                }

                StringBuilder output = new StringBuilder();
                output.AppendLine("[Chef Passive Existing Staff Migration]");
                output.AppendLine("Policy: " + PolicyMarker);
                output.AppendLine("CHEF PASSIVE EXISTING STAFF MIGRATION APPLY: PASS");
                output.AppendLine("StaffData: 7");
                output.AppendLine("Changed fields: 35");
                output.AppendLine("InventoryFingerprint: " + applied.InventoryFingerprint);
                output.AppendLine("Asset GUID / non-target fields / meta: PRESERVED");
                output.AppendLine("Canonical Dry Run V8 FieldPlans: 2111");
                Debug.Log(output.ToString());
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = new List<string>();
                bool rollbackPassed = !writesStarted || RollbackMigration(backup, rollbackErrors);
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Chef Passive Existing Staff Migration]");
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
                    output.AppendLine("CRITICAL_CHEF_PASSIVE_ROLLBACK_FAILED");
                }

                Debug.LogError(output.ToString());
            }
        }

        private static MigrationBackup CaptureBackup()
        {
            MigrationBackup backup = new MigrationBackup();
            StaffDataAssetInventorySnapshot inventory;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out inventory,
                    out diagnostics)
                || inventory == null)
            {
                throw new InvalidOperationException("Backup Inventory 생성 실패.");
            }

            backup.InventoryFingerprint = inventory.InventoryFingerprint;
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                ChefData asset = AssetDatabase.LoadAssetAtPath<ChefData>(target.AssetPath);
                if (asset == null)
                {
                    throw new InvalidOperationException("Backup ChefData 누락: " + target.StaffId);
                }

                backup.Targets.Add(
                    target.StaffId,
                    new TargetBackup(
                        ReadPassiveValues(asset),
                        CaptureNonTargetSerializedFingerprint(asset),
                        AssetDatabase.AssetPathToGUID(target.AssetPath),
                        GetScriptGuid(asset),
                        ComputeAssetFileSha256(target.AssetPath + ".meta")));
            }

            return backup;
        }

        private static void SetTargetValues(MigrationTarget target, IReadOnlyList<float> values)
        {
            ChefData asset = AssetDatabase.LoadAssetAtPath<ChefData>(target.AssetPath);
            if (asset == null)
            {
                throw new InvalidOperationException("ChefData 로드 실패: " + target.StaffId);
            }

            if (ValuesEqual(ReadPassiveValues(asset), values))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty levels = serialized.FindProperty(LevelArrayPath);
            if (levels == null || !levels.isArray || levels.arraySize != values.Count)
            {
                throw new InvalidOperationException("Level 배열 오류: " + target.StaffId);
            }

            for (int levelIndex = 0; levelIndex < values.Count; levelIndex++)
            {
                SerializedProperty value = levels.GetArrayElementAtIndex(levelIndex)
                    .FindPropertyRelative(PassiveFieldName);
                if (value == null || value.propertyType != SerializedPropertyType.Float)
                {
                    throw new InvalidOperationException(
                        "Passive 필드 오류: " + target.StaffId + " Lv." + (levelIndex + 1));
                }

                value.floatValue = values[levelIndex];
            }

            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException("값 적용 실패: " + target.StaffId);
            }

            EditorUtility.SetDirty(asset);
        }

        private static bool ValidatePostApplyInvariants(
            MigrationBackup backup,
            List<string> errors)
        {
            if (backup == null)
            {
                errors.Add("Post-Apply Backup이 없습니다.");
                return false;
            }

            bool valid = true;
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup;
                ChefData asset = AssetDatabase.LoadAssetAtPath<ChefData>(target.AssetPath);
                bool targetValid = backup.Targets.TryGetValue(target.StaffId, out targetBackup)
                                   && asset != null
                                   && ValuesEqual(ReadPassiveValues(asset), target.TargetValues)
                                   && CaptureNonTargetSerializedFingerprint(asset)
                                   == targetBackup.NonTargetFingerprint
                                   && AssetDatabase.GetAssetPath(asset) == target.AssetPath
                                   && AssetDatabase.AssetPathToGUID(target.AssetPath)
                                   == targetBackup.AssetGuid
                                   && GetScriptGuid(asset) == targetBackup.ScriptGuid
                                   && ComputeAssetFileSha256(target.AssetPath + ".meta")
                                   == targetBackup.MetaSha256;
                if (!targetValid)
                {
                    errors.Add("Post-Apply 비대상 불변성 실패: " + target.StaffId);
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidatePostApplyInventory(
            string initialFingerprint,
            List<string> errors)
        {
            StaffDataAssetInventorySnapshot inventory;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out inventory,
                    out diagnostics)
                || inventory == null)
            {
                AddDiagnostics("Post-Apply Inventory", diagnostics, errors);
                return false;
            }

            int matched = 0;
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                StaffDataAssetSnapshot staff;
                if (!inventory.TryGetStaff(target.StaffId, out staff) || staff == null)
                {
                    continue;
                }

                for (int levelIndex = 0; levelIndex < staff.Levels.Count; levelIndex++)
                {
                    float? value = staff.Levels[levelIndex].FoodSpeedAddPercent;
                    matched += value.HasValue && Approximately(value.Value, target.TargetValue) ? 1 : 0;
                }
            }

            bool valid = matched == 35
                         && IsSha256(inventory.InventoryFingerprint)
                         && inventory.InventoryFingerprint != initialFingerprint;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Inventory 검증 실패: matched " + matched
                    + "/35, fingerprint " + inventory.InventoryFingerprint);
            }

            return valid;
        }

        private static bool ValidatePostApplyDryRun(List<string> errors)
        {
            StaffDataDryRunPlanSnapshot plan;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(out plan, out diagnostics)
                || plan == null)
            {
                AddDiagnostics("Post-Apply Dry Run V8", diagnostics, errors);
                return false;
            }

            int fieldPlans = 0;
            int existingChefPassiveChanges = 0;
            int newChefPassivePlans = 0;
            int warnings = 0;
            int existingClassMismatch = 0;
            int newUnsupported = 0;
            int durationMismatch = 0;
            int cooldownMismatch = 0;
            int skillPrerequisite = 0;
            int existingWarning = 0;
            int existingSkill = 0;
            int existingSave = 0;
            int newReady = 0;
            int newSkill = 0;
            HashSet<string> prerequisiteKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int globalIndex = 0; globalIndex < plan.GlobalIssues.Count; globalIndex++)
            {
                StaffDataDryRunIssue issue = plan.GlobalIssues[globalIndex];
                warnings += issue.IsWarning ? 1 : 0;
                if (issue.IsPrerequisite)
                {
                    prerequisiteKeys.Add("GLOBAL|" + issue.Code);
                }
            }

            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[planIndex];
                fieldPlans += staff.ChangedFieldCount;
                bool existing = staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING;
                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                    if (staff.RoleKey == "CHEF"
                        && field.FieldPath.EndsWith("._foodSpeedAddPercent", StringComparison.Ordinal))
                    {
                        if (existing)
                        {
                            existingChefPassiveChanges += field.IsChanged ? 1 : 0;
                        }
                        else
                        {
                            newChefPassivePlans++;
                        }
                    }
                }

                bool hasSkillPrerequisite = false;
                for (int issueIndex = 0; issueIndex < staff.Issues.Count; issueIndex++)
                {
                    StaffDataDryRunIssue issue = staff.Issues[issueIndex];
                    warnings += issue.IsWarning ? 1 : 0;
                    existingClassMismatch += issue.Code == "EXISTING_SKILL_CLASS_MISMATCH" ? 1 : 0;
                    newUnsupported += issue.Code == "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED" ? 1 : 0;
                    hasSkillPrerequisite |= issue.Code == "EXISTING_SKILL_CLASS_MISMATCH"
                                            || issue.Code == "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED";
                    if (issue.IsPrerequisite)
                    {
                        prerequisiteKeys.Add(staff.StaffId);
                    }
                }

                skillPrerequisite += hasSkillPrerequisite ? 1 : 0;
                if (existing)
                {
                    durationMismatch += NumbersEqual(
                        staff.SkillPlan.CurrentDuration,
                        staff.SkillPlan.TargetDuration) ? 0 : 1;
                    cooldownMismatch += NumbersEqual(
                        staff.SkillPlan.CurrentCooldown,
                        staff.SkillPlan.TargetCooldown) ? 0 : 1;
                }
                existingWarning += existing
                                   && staff.Readiness == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS ? 1 : 0;
                existingSkill += existing
                                 && staff.Readiness == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                existingSave += existing
                                && staff.Readiness == StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED ? 1 : 0;
                newReady += !existing
                            && staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY ? 1 : 0;
                newSkill += !existing
                            && staff.Readiness == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
            }

            bool valid = plan.OfficialPackageFingerprint == OfficialPackageFingerprint
                         && plan.PlanningPolicyVersion == StaffDataDryRunPlanSnapshot.V8PolicyVersion
                         && IsSha256(plan.CurrentInventoryFingerprint)
                         && IsSha256(plan.PlanFingerprint)
                         && existingChefPassiveChanges == 0
                         && newChefPassivePlans == 80
                         && fieldPlans == 2111
                         && warnings == 65
                         && existingClassMismatch == 1
                         && newUnsupported == 1
                         && skillPrerequisite == 2
                         && prerequisiteKeys.Count == 3
                         && durationMismatch == 1
                         && cooldownMismatch == 1
                         && existingWarning == 30
                         && existingSkill == 1
                         && existingSave == 1
                         && newReady == 59
                         && newSkill == 1;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Dry Run V8 기준 불일치: passive "
                    + existingChefPassiveChanges + "/" + newChefPassivePlans
                    + ", fields " + fieldPlans + ", warning " + warnings
                    + ", prerequisite " + prerequisiteKeys.Count + ".");
            }

            return valid;
        }

        private static bool RollbackMigration(
            MigrationBackup backup,
            List<string> errors)
        {
            if (backup == null || backup.Targets.Count != Targets.Length)
            {
                errors.Add("Rollback 검증용 원본 Backup이 없습니다.");
                return false;
            }

            try
            {
                for (int index = 0; index < Targets.Length; index++)
                {
                    TargetBackup targetBackup = backup.Targets[Targets[index].StaffId];
                    SetTargetValues(Targets[index], targetBackup.PassiveValues);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                errors.Add("Rollback 예외: " + exception.Message);
                return false;
            }

            MigrationInspection restored;
            List<string> inspectionErrors = new List<string>();
            bool valid = TryInspect(out restored, inspectionErrors)
                         && restored != null
                         && restored.State == MigrationState.READY_TO_APPLY;
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup = backup.Targets[target.StaffId];
                ChefData asset = AssetDatabase.LoadAssetAtPath<ChefData>(target.AssetPath);
                valid &= asset != null
                         && ValuesEqual(ReadPassiveValues(asset), targetBackup.PassiveValues)
                         && CaptureNonTargetSerializedFingerprint(asset)
                         == targetBackup.NonTargetFingerprint
                         && AssetDatabase.AssetPathToGUID(target.AssetPath) == targetBackup.AssetGuid
                         && GetScriptGuid(asset) == targetBackup.ScriptGuid
                         && ComputeAssetFileSha256(target.AssetPath + ".meta")
                         == targetBackup.MetaSha256;
            }

            if (!valid)
            {
                errors.Add("Rollback 후 Baseline 또는 불변성 복구에 실패했습니다.");
                errors.AddRange(inspectionErrors);
            }

            return valid;
        }

        private static List<float> ReadPassiveValues(ChefData asset)
        {
            List<float> values = new List<float>();
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty levels = serialized.FindProperty(LevelArrayPath);
            if (levels == null || !levels.isArray)
            {
                return values;
            }

            for (int index = 0; index < levels.arraySize; index++)
            {
                SerializedProperty value = levels.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(PassiveFieldName);
                if (value != null && value.propertyType == SerializedPropertyType.Float)
                {
                    values.Add(value.floatValue);
                }
            }

            return values;
        }

        private static string CaptureNonTargetSerializedFingerprint(UnityEngine.Object asset)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            StringBuilder input = new StringBuilder();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyPath.EndsWith(
                        "." + PassiveFieldName,
                        StringComparison.Ordinal))
                {
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

        private static string GetScriptGuid(ChefData asset)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty script = serialized.FindProperty("m_Script");
            string guid;
            long localId;
            return script != null
                   && script.objectReferenceValue != null
                   && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                       script.objectReferenceValue,
                       out guid,
                       out localId)
                ? guid
                : string.Empty;
        }

        private static string ComputeAssetFileSha256(string assetPath)
        {
            string absolutePath = Path.Combine(
                StaffOfficialDataPathResolver.ProjectRoot,
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
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static void LogInspection(
            string mode,
            MigrationInspection inspection,
            IReadOnlyList<string> errors,
            bool inspected)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Chef Passive Existing Staff Migration]");
            output.AppendLine("Policy: " + PolicyMarker);
            output.AppendLine("Mode: " + mode);
            output.AppendLine("Active OfficialData Folder: "
                              + (inspection != null ? inspection.ActiveFolder : string.Empty));
            output.AppendLine("SourceKind: "
                              + (inspection != null ? inspection.SourceKind.ToString() : string.Empty));
            if (inspection != null
                && inspection.SourceKind == StaffOfficialDataSourceKind.SessionOverride)
            {
                output.AppendLine("WARNING: NON_CANONICAL_OVERRIDE");
            }

            output.AppendLine("Official PackageFingerprint: "
                              + (inspection != null ? inspection.PackageFingerprint : string.Empty));
            output.AppendLine("InventoryFingerprint: "
                              + (inspection != null ? inspection.InventoryFingerprint : string.Empty));
            output.AppendLine("Migration State: "
                              + (inspection != null ? inspection.State.ToString() : MigrationState.INVALID.ToString()));
            if (inspection != null)
            {
                for (int index = 0; index < inspection.Targets.Count; index++)
                {
                    TargetInspection target = inspection.Targets[index];
                    output.AppendLine(
                        "- " + target.Target.StaffId + " / " + target.Target.GradeKey
                        + " | current " + JoinNumbers(target.CurrentValues)
                        + " | target " + JoinNumbers(target.Target.TargetValues)
                        + " | GUID preserve YES");
                }
            }

            output.AppendLine("Target Staff: 7");
            output.AppendLine("Planned numeric fields: 35");
            output.AppendLine("Skill / Visual / Upgrade / AddSpeed changes: 0");
            output.AppendLine("Asset write: 0");
            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            bool passed = inspected
                          && inspection != null
                          && (inspection.State == MigrationState.READY_TO_APPLY
                              || inspection.State == MigrationState.ALREADY_APPLIED)
                          && errors.Count == 0;
            output.AppendLine(
                "CHEF PASSIVE EXISTING STAFF MIGRATION " + mode + ": "
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

        private static bool TryGetGradeKey(string starsText, out string gradeKey)
        {
            gradeKey = string.Empty;
            int stars;
            if (!int.TryParse(
                    (starsText ?? string.Empty).Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out stars))
            {
                return false;
            }

            gradeKey = stars == 2
                ? "NORMAL"
                : stars == 3 ? "RARE" : stars == 4 ? "UNIQUE" : stars == 5 ? "SPECIAL" : string.Empty;
            return !string.IsNullOrEmpty(gradeKey);
        }

        private static bool RoleBaseEquals(
            Dictionary<string, double> values,
            string key,
            double expected)
        {
            double actual;
            return values.TryGetValue(key, out actual) && Approximately(actual, expected);
        }

        private static int GetCount(Dictionary<string, int> values, string key)
        {
            int value;
            return values.TryGetValue(key, out value) ? value : 0;
        }

        private static bool ValuesEqual(
            IReadOnlyList<float> left,
            IReadOnlyList<float> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (!Approximately(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NumbersEqual(string left, string right)
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

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
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

        private static string JoinNumbers(IReadOnlyList<float> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            string[] text = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                text[index] = values[index].ToString("0.####", CultureInfo.InvariantCulture);
            }

            return string.Join(" / ", text);
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
                errors.Add(label + " - " + diagnostics[index]);
            }
        }

        private enum MigrationState
        {
            INVALID,
            READY_TO_APPLY,
            ALREADY_APPLIED,
            PARTIAL_MIGRATION_STATE
        }

        private sealed class MigrationTarget
        {
            internal string StaffId { get; }
            internal string GradeKey { get; }
            internal string AssetGuid { get; }
            internal float TargetValue { get; }
            internal IReadOnlyList<float> BaselineValues { get; }
            internal IReadOnlyList<float> TargetValues { get; }
            internal string AssetPath
            {
                get { return "Assets/Resources/StaffData/" + StaffId + ".asset"; }
            }

            internal MigrationTarget(
                string staffId,
                string gradeKey,
                string assetGuid,
                float targetValue,
                params float[] baselineValues)
            {
                StaffId = staffId;
                GradeKey = gradeKey;
                AssetGuid = assetGuid;
                TargetValue = targetValue;
                BaselineValues = Array.AsReadOnly((float[])baselineValues.Clone());
                TargetValues = Array.AsReadOnly(
                    new[] { targetValue, targetValue, targetValue, targetValue, targetValue });
            }
        }

        private sealed class TargetInspection
        {
            internal MigrationTarget Target { get; }
            internal List<float> CurrentValues { get; } = new List<float>();
            internal bool StructuralValid;
            internal bool IdentityMatches;
            internal bool IsBaseline;
            internal bool IsTarget;

            internal TargetInspection(MigrationTarget target)
            {
                Target = target;
            }
        }

        private sealed class MigrationInspection
        {
            internal string ActiveFolder = string.Empty;
            internal StaffOfficialDataSourceKind SourceKind;
            internal string PackageFingerprint = string.Empty;
            internal string InventoryFingerprint = string.Empty;
            internal MigrationState State = MigrationState.INVALID;
            internal readonly List<TargetInspection> Targets = new List<TargetInspection>();
        }

        private sealed class MigrationBackup
        {
            internal string InventoryFingerprint = string.Empty;
            internal readonly Dictionary<string, TargetBackup> Targets =
                new Dictionary<string, TargetBackup>(StringComparer.Ordinal);
        }

        private sealed class TargetBackup
        {
            internal IReadOnlyList<float> PassiveValues { get; }
            internal string NonTargetFingerprint { get; }
            internal string AssetGuid { get; }
            internal string ScriptGuid { get; }
            internal string MetaSha256 { get; }

            internal TargetBackup(
                IReadOnlyList<float> passiveValues,
                string nonTargetFingerprint,
                string assetGuid,
                string scriptGuid,
                string metaSha256)
            {
                PassiveValues = Array.AsReadOnly(new List<float>(passiveValues).ToArray());
                NonTargetFingerprint = nonTargetFingerprint;
                AssetGuid = assetGuid;
                ScriptGuid = scriptGuid;
                MetaSha256 = metaSha256;
            }
        }
    }
}
