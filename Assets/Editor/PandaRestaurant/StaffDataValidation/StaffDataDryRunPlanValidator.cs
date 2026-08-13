using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffDataDryRunPlanValidator
    {
        private const string MenuPath =
            "Tools/Panda Restaurant/Staff/Validate Staff Data Dry Run Plan";

        private static readonly string[] RoleOrder =
        {
            "WAITER", "MANAGER", "CHEERLEADER", "CHEF", "CLEANER", "GUARD"
        };

        private static readonly OfficialSkillTimeTarget[] OfficialSkillTimeTargets =
        {
            new OfficialSkillTimeTarget("STAFF01", "STAFF_SKILL01", "SpeedUpSkill", "e02701bebe44c864688b4304e4a359c2", "Assets/Scripts/Datas/Staff/Skill/Staff01Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF02", "STAFF_SKILL01", "SpeedUpSkill", "9bd6d0278c021c04680bdded85a56132", "Assets/Scripts/Datas/Staff/Skill/Staff02Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF03", "STAFF_SKILL01", "SpeedUpSkill", "01a240b70f7efbc4d803ab7f1d4259e1", "Assets/Scripts/Datas/Staff/Skill/Staff03Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF11", "STAFF_SKILL03", "TouchAddCustomerButtonSkill", "1d8e75a245d829746a56f25dd94ba341", "Assets/Scripts/Datas/Staff/Skill/Staff11Skill.asset", 15, 160),
            new OfficialSkillTimeTarget("STAFF12", "STAFF_SKILL03", "TouchAddCustomerButtonSkill", "c07c74e5ac1573e4ab8b7d33229faba3", "Assets/Scripts/Datas/Staff/Skill/Staff12Skill.asset", 15, 160),
            new OfficialSkillTimeTarget("STAFF14", "STAFF_SKILL03", "TouchAddCustomerButtonSkill", "08a154df61b9ad148ba9988d1345876b", "Assets/Scripts/Datas/Staff/Skill/Staff14Skill.asset", 15, 160),
            new OfficialSkillTimeTarget("STAFF16", "STAFF_SKILL01", "SpeedUpSkill", "55429025e47f51e48af3704c79782325", "Assets/Scripts/Datas/Staff/Skill/Staff16Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF18", "STAFF_SKILL01", "SpeedUpSkill", "46422746b9f38374fb77e35d096d48de", "Assets/Scripts/Datas/Staff/Skill/Staff18Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF21", "STAFF_SKILL01", "SpeedUpSkill", "b3c20e5aa6d608e488f3918712c9e32b", "Assets/Scripts/Datas/Staff/Skill/Staff21Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF22", "STAFF_SKILL01", "SpeedUpSkill", "44d111fa7e4c9634f8b2045f742c3bac", "Assets/Scripts/Datas/Staff/Skill/Staff22Skill.asset", 20, 110),
            new OfficialSkillTimeTarget("STAFF23", "STAFF_SKILL01", "SpeedUpSkill", "8700ffc1d7466c449b5dbefcf0d09cde", "Assets/Scripts/Datas/Staff/Skill/Staff23Skill.asset", 17, 115),
            new OfficialSkillTimeTarget("STAFF24", "STAFF_SKILL01", "SpeedUpSkill", "829f6ba2388631644919520e82a9de3c", "Assets/Scripts/Datas/Staff/Skill/Staff24Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF25", "STAFF_SKILL01", "SpeedUpSkill", "d3e58377ff3634b4da83e2e314a97f29", "Assets/Scripts/Datas/Staff/Skill/Staff25Skill.asset", 25, 105),
            new OfficialSkillTimeTarget("STAFF28", "STAFF_SKILL01", "SpeedUpSkill", "1198ffabfe31de2448c9245682a23efc", "Assets/Scripts/Datas/Staff/Skill/STAFF28SKILL.asset", 20, 110),
            new OfficialSkillTimeTarget("STAFF31", "STAFF_SKILL01", "SpeedUpSkill", "f70a543a62181274f94c934036e3f084", "Assets/Scripts/Datas/Staff/Skill/Staff31Skill.asset", 15, 120)
        };

        private static readonly SkillTimeBaseline[] ExcludedSkillTimeBaselines =
        {
            new SkillTimeBaseline("STAFF04", 16, 150),
            new SkillTimeBaseline("STAFF05", 18, 150),
            new SkillTimeBaseline("STAFF06", 7, 150),
            new SkillTimeBaseline("STAFF07", 10, 150),
            new SkillTimeBaseline("STAFF08", 13, 150),
            new SkillTimeBaseline("STAFF09", 16, 150),
            new SkillTimeBaseline("STAFF10", 18, 150),
            new SkillTimeBaseline("STAFF13", 13, 150),
            new SkillTimeBaseline("STAFF15", 20, 100),
            new SkillTimeBaseline("STAFF17", 18, 200),
            new SkillTimeBaseline("STAFF19", 24, 150),
            new SkillTimeBaseline("STAFF20", 27, 150),
            new SkillTimeBaseline("STAFF26", 30, 50),
            new SkillTimeBaseline("STAFF27", 60, 250),
            new SkillTimeBaseline("STAFF29", 30, 150),
            new SkillTimeBaseline("STAFF30", 30, 80),
            new SkillTimeBaseline("STAFF32", 30, 80)
        };

        [MenuItem(MenuPath)]
        private static void ValidateFromMenu()
        {
            string parent = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Select Final17 official Staff data package folder",
                parent,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                Debug.LogWarning("Staff Data Dry Run Plan validation was cancelled.");
                return;
            }

            StaffDataDryRunPlanSnapshot first;
            IReadOnlyList<string> firstDiagnostics;
            bool firstBuilt = StaffDataDryRunPlanner.TryBuildReadOnlyPlan(
                selectedFolder,
                out first,
                out firstDiagnostics);
            if (!firstBuilt || first == null)
            {
                LogBuildFailure("first", firstDiagnostics);
                return;
            }

            StaffDataDryRunPlanSnapshot second;
            IReadOnlyList<string> secondDiagnostics;
            bool secondBuilt = StaffDataDryRunPlanner.TryBuildReadOnlyPlan(
                selectedFolder,
                out second,
                out secondDiagnostics);
            if (!secondBuilt || second == null)
            {
                LogBuildFailure("second", secondDiagnostics);
                return;
            }

            ValidationResult result = Validate(first, second);
            LogSummary(first, result);
            LogRoleDetails(first);
        }

        private static ValidationResult Validate(
            StaffDataDryRunPlanSnapshot first,
            StaffDataDryRunPlanSnapshot second)
        {
            ValidationResult result = new ValidationResult();
            result.Set(1, ValidateInputSnapshots(first, second, result));
            result.Set(2, ValidateAllStaff(first, result));
            result.Set(3, ValidateRoleAndRank(first, result));
            result.Set(4, ValidateExisting(first, result));
            result.Set(5, ValidateNew(first, result));
            result.Set(6, ValidateVisualPolicies(first, result));
            result.Set(7, ValidateSkills(first, result));
            result.Set(8, ValidateChefSchema(first, result));
            result.Set(9, ValidateUpgradeAndMigration(first, result));
            result.Set(10, ValidateFutureSystemIsolation(first, result));
            result.Set(11, ValidateRuntimeMappingIssues(first, result));
            result.Set(12, ValidateFingerprint(first, second, result));
            result.Set(13, ValidateDeterministicRegeneration(first, second, result));
            result.Set(14, ValidateDeepImmutability(first, result));
            result.Set(15, ValidateNonDestructiveImplementation(first, second, result));
            CountWarningsAndPrerequisites(first, result);
            Require(result.WarningCount == 65, "Plan warning baseline changed.", result);
            Require(result.PrerequisiteCount == 40, "Plan prerequisite baseline changed.", result);
            Require(CountChangedFields(first) == 2146, "Changed-field baseline changed.", result);
            return result;
        }

        private static bool ValidateInputSnapshots(
            StaffDataDryRunPlanSnapshot first,
            StaffDataDryRunPlanSnapshot second,
            ValidationResult result)
        {
            bool valid = IsSha256(first.OfficialPackageFingerprint)
                         && IsSha256(first.CurrentInventoryFingerprint)
                         && IsSha256(first.LegacyInventoryFingerprint)
                         && first.OfficialPackageFingerprint == second.OfficialPackageFingerprint
                         && first.CurrentInventoryFingerprint == second.CurrentInventoryFingerprint
                         && first.LegacyInventoryFingerprint == second.LegacyInventoryFingerprint;
            return Require(valid, "Input snapshot fingerprints are incomplete or changed.", result);
        }

        private static bool ValidateAllStaff(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            bool valid = plan.StaffPlans.Count == 92 && plan.StaffById.Count == 92;
            for (int number = 1; number <= 92; number++)
            {
                string id = "STAFF" + number.ToString("00", CultureInfo.InvariantCulture);
                StaffDataDryRunStaffPlan staff;
                valid &= plan.StaffById.TryGetValue(id, out staff)
                         && staff != null
                         && staff.StaffNumber == number;
            }

            return Require(valid, "STAFF01~STAFF92 coverage or uniqueness is invalid.", result);
        }

        private static bool ValidateRoleAndRank(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            Dictionary<string, int> roles = CountPlans(plan, false);
            Dictionary<string, int> ranks = CountPlans(plan, true);
            bool valid = GetCount(roles, "WAITER") == 23
                         && GetCount(roles, "MANAGER") == 13
                         && GetCount(roles, "CHEERLEADER") == 17
                         && GetCount(roles, "CHEF") == 23
                         && GetCount(roles, "CLEANER") == 14
                         && GetCount(roles, "GUARD") == 2
                         && GetCount(ranks, "Normal2") == 23
                         && GetCount(ranks, "Rare") == 26
                         && GetCount(ranks, "Unique") == 35
                         && GetCount(ranks, "Special") == 8
                         && GetCount(ranks, "Normal1") == 0;
            return Require(valid, "Official role or rank distribution changed.", result);
        }

        private static bool ValidateExisting(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int count = 0;
            int guidPreserved = 0;
            int visualPreserved = 0;
            int valueComparisons = 0;
            int migration = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                count++;
                guidPreserved += staff.PreserveExistingGuid
                                 && !string.IsNullOrEmpty(staff.ExistingAssetGuid)
                                 && staff.ExistingAssetPath == staff.PlannedAssetPath ? 1 : 0;
                visualPreserved += staff.VisualPlan.PreserveExistingVisuals ? 1 : 0;
                valueComparisons += HasCoreComparisons(staff) ? 1 : 0;
                migration += HasIssue(staff, "STAFF02_LEVEL6_SAVE_MIGRATION_REQUIRED") ? 1 : 0;
                valid &= !string.IsNullOrEmpty(staff.ExistingScriptGuid)
                         && staff.SkillPlan.PreserveExistingGuid
                         && !string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetPath)
                         && !string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetGuid);
            }

            valid &= count == 32
                     && guidPreserved == 32
                     && visualPreserved == 32
                     && valueComparisons == 32
                     && migration == 1;
            return Require(valid, "Existing STAFF01~32 preservation/update plan is incomplete.", result);
        }

        private static bool ValidateNew(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int count = 0;
            int legacyLinks = 0;
            int main = 0;
            int thumbnail = 0;
            int chefParts = 0;
            int cheerParts = 0;
            HashSet<string> staffPaths = new HashSet<string>(StringComparer.Ordinal);
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction != StaffDryRunAssetAction.CREATE_NEW)
                {
                    continue;
                }

                count++;
                legacyLinks += !string.IsNullOrEmpty(staff.VisualPlan.LegacySkinId) ? 1 : 0;
                main += staff.VisualPlan.HasMain ? 1 : 0;
                thumbnail += staff.VisualPlan.HasThumbnail ? 1 : 0;
                chefParts += staff.RoleKey == "CHEF" && staff.VisualPlan.HasRequiredRoleParts ? 1 : 0;
                cheerParts += staff.RoleKey == "CHEERLEADER"
                              && staff.VisualPlan.HasRequiredRoleParts ? 1 : 0;
                valid &= staff.PlannedAssetPath
                             == "Assets/Resources/StaffData/" + staff.StaffId + ".asset"
                         && staffPaths.Add(staff.PlannedAssetPath)
                         && !staff.PreserveExistingGuid
                         && staff.SkillPlan.CreateIndividualAsset;
            }

            valid &= count == 60
                     && legacyLinks == 60
                     && main == 60
                     && thumbnail == 60
                     && chefParts == 16
                     && cheerParts == 12
                     && staffPaths.Count == 60;
            return Require(valid, "New STAFF33~92 asset/legacy plan is incomplete.", result);
        }

        private static bool ValidateVisualPolicies(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int legacyIdle = 0;
            int cheerNoFrame = 0;
            int breathing = 0;
            int sharedAnimator = 0;
            int namingMismatch = 0;
            int missing = 0;
            int manualIdleArt = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction != StaffDryRunAssetAction.CREATE_NEW)
                {
                    continue;
                }

                string idle = staff.VisualPlan.IdlePolicyCode;
                legacyIdle += idle == "USE_LEGACY_IDLE_FRAMES" ? 1 : 0;
                cheerNoFrame += idle == "INTENTIONAL_NO_FRAME_IDLE_CHEERLEADER" ? 1 : 0;
                breathing += idle == "BASE_SPRITE_IDLE_WITH_SHARED_BREATHING" ? 1 : 0;
                manualIdleArt += idle == "MANUAL_IDLE_ART_REQUIRED" ? 1 : 0;
                sharedAnimator += staff.VisualPlan.AnimatorPolicyCode
                                  == "USE_SHARED_PREFAB_ANIMATOR" ? 1 : 0;
                namingMismatch += staff.VisualPlan.NamingMismatchReviewRequired ? 1 : 0;
                missing += staff.VisualPlan.HasMissingReference ? 1 : 0;
            }

            bool valid = legacyIdle == 36
                         && cheerNoFrame == 12
                         && breathing == 12
                         && manualIdleArt == 0
                         && sharedAnimator == 60
                         && namingMismatch == 1
                         && missing == 0;
            return Require(valid, "Visual, idle, animator, or naming-mismatch policy changed.", result);
        }

        private static bool ValidateSkills(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int existingMatch = 0;
            int existingMismatch = 0;
            int newUnsupported = 0;
            int individualPlans = 0;
            HashSet<string> newSkillPaths = new HashSet<string>(StringComparer.Ordinal);
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    existingMatch += staff.SkillPlan.ClassMatches ? 1 : 0;
                    existingMismatch += HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH") ? 1 : 0;
                }
                else
                {
                    newUnsupported += HasIssue(staff, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED") ? 1 : 0;
                    individualPlans += staff.SkillPlan.CreateIndividualAsset ? 1 : 0;
                    valid &= newSkillPaths.Add(staff.SkillPlan.PlannedAssetPath)
                             && staff.SkillPlan.PlannedAssetPath
                             == "Assets/Scripts/Datas/Staff/Skill/" + staff.StaffId + "Skill.asset";
                }
            }

            valid &= existingMatch == 15
                     && existingMismatch == 17
                     && newUnsupported == 22
                     && individualPlans == 60
                     && newSkillPaths.Count == 60;
            valid &= ValidateOfficialSkillTimeApply(plan, result);
            return Require(valid, "Skill class or individual skill-asset plan changed.", result);
        }

        private static bool ValidateOfficialSkillTimeApply(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            Dictionary<string, StaffDataDryRunStaffPlan> staffById =
                new Dictionary<string, StaffDataDryRunStaffPlan>(StringComparer.Ordinal);
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staffById.ContainsKey(staff.StaffId))
                {
                    valid = false;
                    continue;
                }

                staffById.Add(staff.StaffId, staff);
            }

            HashSet<string> targetIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> currentAssetGuids = new HashSet<string>(StringComparer.Ordinal);
            int applied = 0;
            int speedUp = 0;
            int touchCustomer = 0;
            for (int index = 0; index < OfficialSkillTimeTargets.Length; index++)
            {
                OfficialSkillTimeTarget target = OfficialSkillTimeTargets[index];
                valid &= targetIds.Add(target.StaffId);

                StaffDataDryRunStaffPlan staff;
                if (!staffById.TryGetValue(target.StaffId, out staff))
                {
                    valid = false;
                    continue;
                }

                StaffDataDryRunSkillPlan skill = staff.SkillPlan;
                bool targetApplied = staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING
                                     && skill.OfficialSkillId == target.OfficialSkillId
                                     && skill.RequiredClassName == target.RuntimeClassName
                                     && skill.CurrentClassName == target.RuntimeClassName
                                     && skill.ClassMatches
                                     && skill.PreserveExistingGuid
                                     && !skill.CreateIndividualAsset
                                     && skill.CurrentAssetGuid == target.AssetGuid
                                     && skill.CurrentAssetPath == target.AssetPath
                                     && skill.PlannedAssetPath == target.AssetPath
                                     && SkillNumberEquals(skill.CurrentDuration, target.Duration)
                                     && SkillNumberEquals(skill.TargetDuration, target.Duration)
                                     && SkillNumberEquals(skill.CurrentCooldown, target.Cooldown)
                                     && SkillNumberEquals(skill.TargetCooldown, target.Cooldown);
                valid &= targetApplied;
                valid &= currentAssetGuids.Add(skill.CurrentAssetGuid);
                if (targetApplied)
                {
                    applied++;
                    speedUp += target.RuntimeClassName == "SpeedUpSkill" ? 1 : 0;
                    touchCustomer += target.RuntimeClassName == "TouchAddCustomerButtonSkill" ? 1 : 0;
                }
            }

            HashSet<string> excludedIds = new HashSet<string>(StringComparer.Ordinal);
            int excludedClassMismatch = 0;
            for (int index = 0; index < ExcludedSkillTimeBaselines.Length; index++)
            {
                SkillTimeBaseline baseline = ExcludedSkillTimeBaselines[index];
                valid &= excludedIds.Add(baseline.StaffId);
                valid &= !targetIds.Contains(baseline.StaffId);

                StaffDataDryRunStaffPlan staff;
                if (!staffById.TryGetValue(baseline.StaffId, out staff))
                {
                    valid = false;
                    continue;
                }

                bool excludedUnchanged = staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING
                                         && HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH")
                                         && SkillNumberEquals(staff.SkillPlan.CurrentDuration, baseline.Duration)
                                         && SkillNumberEquals(staff.SkillPlan.CurrentCooldown, baseline.Cooldown);
                valid &= excludedUnchanged;
                excludedClassMismatch += excludedUnchanged ? 1 : 0;
            }

            int existingCovered = 0;
            int newUnapplied = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    bool covered = targetIds.Contains(staff.StaffId)
                                   || excludedIds.Contains(staff.StaffId);
                    valid &= covered;
                    existingCovered += covered ? 1 : 0;
                    continue;
                }

                bool unapplied = staff.StaffNumber >= 33
                                 && staff.StaffNumber <= 92
                                 && string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetPath)
                                 && string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetGuid)
                                 && string.IsNullOrEmpty(staff.SkillPlan.CurrentClassName)
                                 && string.IsNullOrEmpty(staff.SkillPlan.CurrentDuration)
                                 && string.IsNullOrEmpty(staff.SkillPlan.CurrentCooldown);
                valid &= unapplied;
                newUnapplied += unapplied ? 1 : 0;
            }

            valid &= OfficialSkillTimeTargets.Length == 15
                     && ExcludedSkillTimeBaselines.Length == 17
                     && targetIds.Count == 15
                     && currentAssetGuids.Count == 15
                     && applied == 15
                     && speedUp == 12
                     && touchCustomer == 3
                     && excludedClassMismatch == 17
                     && existingCovered == 32
                     && newUnapplied == 60
                     && CountSkillNumberMismatches(plan, true) == 15
                     && CountSkillNumberMismatches(plan, false) == 13
                     && CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH") == 17
                     && CountIssues(plan, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED") == 22
                     && CountStaffWithSkillPrerequisite(plan) == 39
                     && CountChangedFields(plan) == 2146
                     && plan.PlanningPolicyVersion == "STAFF_DRY_RUN_POLICY_2026_08_11_V3";
            return Require(valid, "Official Skill time post-apply baseline changed.", result);
        }

        private static bool ValidateChefSchema(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int existingGap = 0;
            int newGap = 0;
            int schemaIssueCount = CountIssues(plan, "CHEF_ADD_SPEED_SCHEMA_REQUIRED");
            int addSpeedFieldCount = 0;
            int existingAddSpeedFieldCount = 0;
            int newAddSpeedFieldCount = 0;
            int existingUnchangedLevelOneCount = 0;
            int existingChangedHigherLevelCount = 0;
            int repurposedFoodField = 0;
            string[] targets = { "0", "0.5", "1", "1.5", "2" };
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.RoleKey != "CHEF")
                {
                    continue;
                }

                bool gap = HasIssue(staff, "CHEF_ADD_SPEED_SCHEMA_REQUIRED");
                existingGap += gap && staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING ? 1 : 0;
                newGap += gap && staff.AssetAction == StaffDryRunAssetAction.CREATE_NEW ? 1 : 0;
                repurposedFoodField += HasRepurposedChefFoodField(staff) ? 1 : 0;

                int staffAddSpeedFieldCount = 0;
                HashSet<int> addSpeedLevels = new HashSet<int>();
                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                    if (!field.FieldPath.EndsWith("._addSpeed", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int levelIndex;
                    if (!TryGetLevelIndex(field.FieldPath, out levelIndex))
                    {
                        valid = false;
                        continue;
                    }

                    addSpeedFieldCount++;
                    staffAddSpeedFieldCount++;
                    valid &= addSpeedLevels.Add(levelIndex)
                             && field.TargetValue == targets[levelIndex];
                    if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                    {
                        existingAddSpeedFieldCount++;
                        valid &= field.CurrentValue == "0"
                                 && field.Disposition
                                 == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                                 && field.IsChanged == (levelIndex > 0);
                        existingUnchangedLevelOneCount += levelIndex == 0 && !field.IsChanged ? 1 : 0;
                        existingChangedHigherLevelCount += levelIndex > 0 && field.IsChanged ? 1 : 0;
                    }
                    else
                    {
                        newAddSpeedFieldCount++;
                        valid &= field.CurrentValue == string.Empty
                                 && field.Disposition
                                 == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                                 && field.IsChanged;
                    }
                }

                valid &= staffAddSpeedFieldCount == 5 && addSpeedLevels.Count == 5;
            }

            valid &= existingGap == 0
                     && newGap == 0
                     && schemaIssueCount == 0
                     && addSpeedFieldCount == 115
                     && existingAddSpeedFieldCount == 35
                     && newAddSpeedFieldCount == 80
                     && existingUnchangedLevelOneCount == 7
                     && existingChangedHigherLevelCount == 28
                     && repurposedFoodField == 0;
            return Require(valid, "Chef movement-speed schema plan is incomplete or repurposes cooking efficiency.", result);
        }

        private static bool ValidateUpgradeAndMigration(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int validLevelPlans = 0;
            int migration = 0;
            int terminalReview = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                HashSet<int> levels = new HashSet<int>();
                int transitionPrices = 0;
                int zeroScores = 0;
                bool terminalScore = false;
                bool terminalPrice = false;
                bool staffTerminalReview = false;
                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                    int levelIndex;
                    if (!TryGetLevelIndex(field.FieldPath, out levelIndex))
                    {
                        continue;
                    }

                    levels.Add(levelIndex);
                    if (field.FieldPath.EndsWith("._price", StringComparison.Ordinal))
                    {
                        transitionPrices += levelIndex < 4 ? 1 : 0;
                        terminalPrice |= levelIndex == 4 && field.TargetValue == "-1";
                    }
                    else if (field.FieldPath.EndsWith("._upgradeMinScore", StringComparison.Ordinal))
                    {
                        zeroScores += levelIndex < 4 && field.TargetValue == "0" ? 1 : 0;
                        terminalScore |= levelIndex == 4 && field.TargetValue == "-1";
                    }

                    staffTerminalReview |= field.Note == "TERMINAL_SENTINEL_MONEY_TYPE_REVIEW";
                }

                validLevelPlans += levels.Count == 5
                                   && transitionPrices == 4
                                   && zeroScores == 4
                                   && terminalScore
                                   && terminalPrice ? 1 : 0;
                terminalReview += staffTerminalReview ? 1 : 0;
                migration += HasIssue(staff, "STAFF02_LEVEL6_SAVE_MIGRATION_REQUIRED") ? 1 : 0;
            }

            valid &= validLevelPlans == 92 && terminalReview == 92 && migration == 1;
            return Require(valid, "Five-level upgrade, terminal sentinel, or STAFF02 migration plan is invalid.", result);
        }

        private static bool ValidateFutureSystemIsolation(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int acquisition = 0;
            int duplicateToken = 0;
            int tokenPrice = 0;
            int forbiddenPurchaseMapping = 0;
            int skinTokenMapping = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                acquisition += HasDisposition(
                    staff,
                    "Final17.GachaProbabilityRaw",
                    StaffDryRunFieldDisposition.FUTURE_STAFF_ACQUISITION_SYSTEM_REQUIRED) ? 1 : 0;
                duplicateToken += HasDisposition(
                    staff,
                    "Final17.DuplicationTokenRaw",
                    StaffDryRunFieldDisposition.FUTURE_PANDA_TOKEN_SYSTEM_REQUIRED) ? 1 : 0;
                tokenPrice += HasDisposition(
                    staff,
                    "Final17.TokenPurchasePriceRaw",
                    StaffDryRunFieldDisposition.FUTURE_PANDA_TOKEN_SYSTEM_REQUIRED) ? 1 : 0;
                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    string path = staff.FieldPlans[fieldIndex].FieldPath;
                    forbiddenPurchaseMapping += path == "StaffData._buyPrice"
                                                || path == "StaffData._moneyType" ? 1 : 0;
                    skinTokenMapping += path.IndexOf("SkinToken", StringComparison.Ordinal) >= 0 ? 1 : 0;
                }
            }

            bool valid = acquisition == 92
                         && duplicateToken == 92
                         && tokenPrice == 92
                         && forbiddenPurchaseMapping == 0
                         && skinTokenMapping == 0;
            return Require(valid, "PandaToken/acquisition values were not isolated from current purchase systems.", result);
        }

        private static bool ValidateRuntimeMappingIssues(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int mismatch = 0;
            int recorded26 = 0;
            int recorded27 = 0;
            int recorded28 = 0;
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                string code = plan.GlobalIssues[index].Code;
                mismatch += code == "UPGRADE25_RUNTIME_MAPPING_MISMATCH" ? 1 : 0;
                recorded26 += code == "UPGRADE26_MAPPING_RECORDED" ? 1 : 0;
                recorded27 += code == "UPGRADE27_MAPPING_RECORDED" ? 1 : 0;
                recorded28 += code == "UPGRADE28_EXPEL_SPEED_RECORDED" ? 1 : 0;
            }

            bool valid = mismatch == 1 && recorded26 == 1 && recorded27 == 1 && recorded28 == 1;
            return Require(valid, "UPGRADE25~28 runtime mapping audit records are incomplete.", result);
        }

        private static bool ValidateFingerprint(
            StaffDataDryRunPlanSnapshot first,
            StaffDataDryRunPlanSnapshot second,
            ValidationResult result)
        {
            bool valid = first.PlanningPolicyVersion
                         == "STAFF_DRY_RUN_POLICY_2026_08_11_V3"
                         && IsSha256(first.PlanFingerprint)
                         && first.PlanFingerprint == second.PlanFingerprint;
            return Require(valid, "Plan fingerprint or policy version is invalid.", result);
        }

        private static bool ValidateDeterministicRegeneration(
            StaffDataDryRunPlanSnapshot first,
            StaffDataDryRunPlanSnapshot second,
            ValidationResult result)
        {
            bool valid = first.PlanFingerprint == second.PlanFingerprint
                         && first.StaffPlans.Count == second.StaffPlans.Count
                         && first.GlobalIssues.Count == second.GlobalIssues.Count;
            for (int index = 0; index < first.StaffPlans.Count && valid; index++)
            {
                StaffDataDryRunStaffPlan left = first.StaffPlans[index];
                StaffDataDryRunStaffPlan right = second.StaffPlans[index];
                valid &= left.StaffId == right.StaffId
                         && left.Readiness == right.Readiness
                         && left.ChangedFieldCount == right.ChangedFieldCount
                         && left.SkillPlan.PlannedAssetPath == right.SkillPlan.PlannedAssetPath;
            }

            return Require(valid, "Two in-memory plan generations are not deterministic.", result);
        }

        private static bool ValidateDeepImmutability(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            bool listBlocked = MutationIsBlocked(
                delegate
                {
                    ((ICollection<StaffDataDryRunStaffPlan>)plan.StaffPlans).Add(null);
                });
            bool dictionaryBlocked = MutationIsBlocked(
                delegate
                {
                    ((IDictionary<string, StaffDataDryRunStaffPlan>)plan.StaffById)
                        .Add("STAFF99", null);
                });
            bool fieldBlocked = MutationIsBlocked(
                delegate
                {
                    ((ICollection<StaffDataDryRunFieldPlan>)plan.StaffPlans[0].FieldPlans)
                        .Add(null);
                });
            bool issueBlocked = MutationIsBlocked(
                delegate
                {
                    ((ICollection<StaffDataDryRunIssue>)plan.StaffPlans[0].Issues).Add(null);
                });
            bool visualBlocked = MutationIsBlocked(
                delegate
                {
                    ((ICollection<string>)plan.StaffPlans[32].VisualPlan.ReferenceKeys)
                        .Add("MUTATION");
                });

            Type[] modelTypes =
            {
                typeof(StaffDataDryRunPlanSnapshot),
                typeof(StaffDataDryRunStaffPlan),
                typeof(StaffDataDryRunFieldPlan),
                typeof(StaffDataDryRunSkillPlan),
                typeof(StaffDataDryRunVisualPlan),
                typeof(StaffDataDryRunIssue)
            };
            bool noPublicSetter = true;
            bool noUnityObjectCapture = true;
            for (int typeIndex = 0; typeIndex < modelTypes.Length; typeIndex++)
            {
                PropertyInfo[] properties = modelTypes[typeIndex].GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
                {
                    MethodInfo setter = properties[propertyIndex].GetSetMethod(false);
                    noPublicSetter &= setter == null;
                    noUnityObjectCapture &= !typeof(UnityEngine.Object).IsAssignableFrom(
                        properties[propertyIndex].PropertyType);
                }

                FieldInfo[] fields = modelTypes[typeIndex].GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    noUnityObjectCapture &= !typeof(UnityEngine.Object).IsAssignableFrom(
                        fields[fieldIndex].FieldType);
                }
            }

            bool valid = listBlocked
                         && dictionaryBlocked
                         && fieldBlocked
                         && issueBlocked
                         && visualBlocked
                         && noPublicSetter
                         && noUnityObjectCapture;
            return Require(valid, "Plan collections or model properties are mutable.", result);
        }

        private static bool ValidateNonDestructiveImplementation(
            StaffDataDryRunPlanSnapshot first,
            StaffDataDryRunPlanSnapshot second,
            ValidationResult result)
        {
            bool fingerprintsStable = first.OfficialPackageFingerprint
                                      == second.OfficialPackageFingerprint
                                      && first.CurrentInventoryFingerprint
                                      == second.CurrentInventoryFingerprint
                                      && first.LegacyInventoryFingerprint
                                      == second.LegacyInventoryFingerprint;
            List<string> forbiddenHits = ScanForbiddenSourceTokens();
            for (int index = 0; index < forbiddenHits.Count; index++)
            {
                result.Errors.Add(forbiddenHits[index]);
            }

            return Require(
                fingerprintsStable && forbiddenHits.Count == 0,
                "Input snapshots changed or a forbidden write/exploration token was found.",
                result);
        }

        private static List<string> ScanForbiddenSourceTokens()
        {
            string root = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : string.Empty;
            string folder = Path.Combine(
                root,
                "Assets",
                "Editor",
                "PandaRestaurant",
                "StaffDataValidation");
            string[] files =
            {
                Path.Combine(folder, "StaffDataDryRunPlanModels.cs"),
                Path.Combine(folder, "StaffDataDryRunPlanner.cs"),
                Path.Combine(folder, "StaffDataDryRunPlanValidator.cs")
            };
            string assetApi = "Asset" + "Database";
            string serialized = "Serialized" + "Object";
            string scriptable = "Scriptable" + "Object";
            string game = "Game" + "Object";
            string[] forbidden =
            {
                serialized + ".ApplyModifiedProperties",
                serialized + ".ApplyModifiedPropertiesWithoutUndo",
                "EditorUtility." + "SetDirty",
                "Undo." + "RecordObject",
                assetApi + ".CreateAsset",
                assetApi + ".DeleteAsset",
                assetApi + ".MoveAsset",
                assetApi + ".CopyAsset",
                assetApi + ".SaveAssets",
                assetApi + ".ImportAsset",
                assetApi + ".Refresh",
                assetApi + ".StartAssetEditing",
                assetApi + ".StopAssetEditing",
                assetApi + ".FindAssets",
                assetApi + ".LoadAssetAtPath",
                scriptable + ".CreateInstance",
                "new " + game,
                ".Add" + "Component",
                "Resources." + "LoadAll",
                "File." + "WriteAllText",
                "File." + "WriteAllBytes",
                "File." + "Copy",
                "File." + "Move",
                "File." + "Delete",
                "Directory." + "CreateDirectory",
                "Directory." + "Delete"
            };

            List<string> hits = new List<string>();
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string source = File.ReadAllText(files[fileIndex]);
                for (int tokenIndex = 0; tokenIndex < forbidden.Length; tokenIndex++)
                {
                    if (source.IndexOf(forbidden[tokenIndex], StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(
                            "Forbidden token '" + forbidden[tokenIndex]
                            + "' in " + Path.GetFileName(files[fileIndex]) + ".");
                    }
                }
            }

            return hits;
        }

        private static void CountWarningsAndPrerequisites(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                result.WarningCount += plan.GlobalIssues[index].IsWarning ? 1 : 0;
                result.PrerequisiteCount += plan.GlobalIssues[index].IsPrerequisite ? 1 : 0;
            }

            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                IReadOnlyList<StaffDataDryRunIssue> issues = plan.StaffPlans[planIndex].Issues;
                for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                {
                    result.WarningCount += issues[issueIndex].IsWarning ? 1 : 0;
                    result.PrerequisiteCount += issues[issueIndex].IsPrerequisite ? 1 : 0;
                }
            }
        }

        private static void LogSummary(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            string[] labels =
            {
                string.Empty,
                "Input Snapshot 3종",
                "전체 STAFF01~92 계획",
                "역할·Rank 분포",
                "기존 STAFF01~32 갱신 계획",
                "신규 STAFF33~92 생성 계획",
                "시각·Idle·Animator 정책",
                "Skill 계획",
                "Chef 구조 Gap",
                "강화·Migration 계획",
                "PandaToken·획득 시스템 분리",
                "Runtime Mapping Mismatch 기록",
                "PlanFingerprint",
                "결정론적 재생성",
                "깊은 불변성",
                "비파괴 확인"
            };
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Staff Data Dry Run Plan Validation]");
            for (int section = 1; section <= 15; section++)
            {
                output.AppendLine(
                    section.ToString(CultureInfo.InvariantCulture) + ". " + labels[section]
                    + ": " + (result.Sections[section] ? "PASS" : "FAIL"));
            }

            output.AppendLine("16. 계획 Warning 수: " + result.WarningCount);
            output.AppendLine("17. 선행 조건 수: " + result.PrerequisiteCount);
            output.AppendLine("18. 구조 오류 수: " + result.Errors.Count);
            output.AppendLine("19. 최종 결과: " + (result.Passed ? "PASS" : "FAIL"));
            output.AppendLine();
            output.AppendLine("[Summary]");
            output.AppendLine("- 전체 92명 / 기존 32명 / 신규 60명");
            SkillTimeApplySummary skillTime = BuildSkillTimeApplySummary(plan);
            output.AppendLine(
                "- 기존 호환 Skill 공식 시간 적용: " + skillTime.AppliedCount + "/15 "
                + (skillTime.AppliedCount == 15 ? "PASS" : "FAIL"));
            output.AppendLine("- SpeedUpSkill 대상: " + skillTime.SpeedUpCount + "/12");
            output.AppendLine("- TouchAddCustomerButtonSkill 대상: " + skillTime.TouchCustomerCount + "/3");
            output.AppendLine("- 해결된 Duration 계획: " + (28 - skillTime.DurationMismatchCount));
            output.AppendLine("- 해결된 Cooldown 계획: " + (28 - skillTime.CooldownMismatchCount));
            output.AppendLine("- Remaining Duration Mismatch: " + skillTime.DurationMismatchCount);
            output.AppendLine("- Remaining Cooldown Mismatch: " + skillTime.CooldownMismatchCount);
            output.AppendLine(
                "- 자동 값 계획: " + CountChangedFields(plan) + " (StaffData FieldPlans only)");
            output.AppendLine(
                "- 적용 제외 Class Mismatch: "
                + CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH"));
            output.AppendLine("- Skill 선행 구현 필요: " + CountStaffWithSkillPrerequisite(plan));
            output.AppendLine(
                "- Chef 구조 선행 구현 필요: "
                + CountIssues(plan, "CHEF_ADD_SPEED_SCHEMA_REQUIRED"));
            output.AppendLine("- Save Migration 필요: 1");
            output.AppendLine("- PandaToken·획득 후속 시스템 필요: 92");
            output.AppendLine("- PlanningPolicyVersion: " + plan.PlanningPolicyVersion);
            output.AppendLine("- PlanFingerprint: " + plan.PlanFingerprint);
            for (int index = 0; index < result.Errors.Count; index++)
            {
                output.AppendLine("ERROR: " + result.Errors[index]);
            }

            output.AppendLine();
            output.AppendLine(
                "STAFF DATA DRY RUN PLAN VALIDATION: " + (result.Passed ? "PASS" : "FAIL"));
            if (result.Passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static void LogRoleDetails(StaffDataDryRunPlanSnapshot plan)
        {
            for (int roleIndex = 0; roleIndex < RoleOrder.Length; roleIndex++)
            {
                string role = RoleOrder[roleIndex];
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Staff Dry Run Role Detail: " + role + "]");
                for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
                {
                    StaffDataDryRunStaffPlan staff = plan.StaffPlans[planIndex];
                    if (staff.RoleKey != role)
                    {
                        continue;
                    }

                    output.AppendLine(
                        staff.StaffId + " | " + staff.AssetAction
                        + " | current=" + staff.CurrentName
                        + " | target=" + staff.TargetName
                        + " | changed=" + staff.ChangedFieldCount
                        + " | preserved=" + staff.PreservedFieldCount
                        + " | visual=" + staff.VisualPlan.IdlePolicyCode
                        + " | skill=" + staff.SkillPlan.OfficialSkillId + "/"
                        + (string.IsNullOrEmpty(staff.SkillPlan.RequiredClassName)
                            ? "IMPLEMENTATION_REQUIRED"
                            : staff.SkillPlan.RequiredClassName)
                        + " | readiness=" + staff.Readiness
                        + " | issues=" + staff.Issues.Count);
                    for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                    {
                        StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                        output.AppendLine(
                            "  FIELD " + field.FieldPath
                            + " | current=" + field.CurrentValue
                            + " | target=" + field.TargetValue
                            + " | changed=" + (field.IsChanged ? "YES" : "NO")
                            + " | disposition=" + field.Disposition);
                    }

                    output.AppendLine(
                        "  VISUAL | legacy=" + staff.VisualPlan.LegacySkinId
                        + " | main=" + staff.VisualPlan.MainReferenceKey
                        + " | thumbnail=" + staff.VisualPlan.ThumbnailReferenceKey
                        + " | idle=" + staff.VisualPlan.IdlePolicyCode
                        + " | animator=" + staff.VisualPlan.AnimatorPolicyCode);
                    output.AppendLine(
                        "  SKILL | currentPath=" + staff.SkillPlan.CurrentAssetPath
                        + " | currentGuid=" + staff.SkillPlan.CurrentAssetGuid
                        + " | currentClass=" + staff.SkillPlan.CurrentClassName
                        + " | targetId=" + staff.SkillPlan.OfficialSkillId
                        + " | targetClass=" + staff.SkillPlan.RequiredClassName
                        + " | plannedPath=" + staff.SkillPlan.PlannedAssetPath
                        + " | duration=" + staff.SkillPlan.CurrentDuration + "->"
                        + staff.SkillPlan.TargetDuration
                        + " | cooldown=" + staff.SkillPlan.CurrentCooldown + "->"
                        + staff.SkillPlan.TargetCooldown);
                    for (int issueIndex = 0; issueIndex < staff.Issues.Count; issueIndex++)
                    {
                        StaffDataDryRunIssue issue = staff.Issues[issueIndex];
                        output.AppendLine(
                            "  ISSUE " + issue.Code
                            + " | prerequisite=" + (issue.IsPrerequisite ? "YES" : "NO")
                            + " | warning=" + (issue.IsWarning ? "YES" : "NO")
                            + " | " + issue.Message);
                    }
                }

                Debug.Log(output.ToString());
            }
        }

        private static bool HasCoreComparisons(StaffDataDryRunStaffPlan staff)
        {
            bool name = false;
            bool description = false;
            bool rank = false;
            bool speed = false;
            for (int index = 0; index < staff.FieldPlans.Count; index++)
            {
                string path = staff.FieldPlans[index].FieldPath;
                name |= path == "StaffData._name";
                description |= path == "StaffData._description";
                rank |= path == "StaffData._rank";
                speed |= path == "StaffData._speed";
            }

            return name && description && rank && speed;
        }

        private static bool HasIssue(StaffDataDryRunStaffPlan staff, string code)
        {
            for (int index = 0; index < staff.Issues.Count; index++)
            {
                if (staff.Issues[index].Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountIssues(StaffDataDryRunPlanSnapshot plan, string code)
        {
            int count = 0;
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                count += plan.GlobalIssues[index].Code == code ? 1 : 0;
            }

            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                IReadOnlyList<StaffDataDryRunIssue> issues = plan.StaffPlans[planIndex].Issues;
                for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                {
                    count += issues[issueIndex].Code == code ? 1 : 0;
                }
            }

            return count;
        }

        private static bool HasDisposition(
            StaffDataDryRunStaffPlan staff,
            string path,
            StaffDryRunFieldDisposition disposition)
        {
            for (int index = 0; index < staff.FieldPlans.Count; index++)
            {
                if (staff.FieldPlans[index].FieldPath == path
                    && staff.FieldPlans[index].Disposition == disposition)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasRepurposedChefFoodField(StaffDataDryRunStaffPlan staff)
        {
            for (int index = 0; index < staff.FieldPlans.Count; index++)
            {
                StaffDataDryRunFieldPlan field = staff.FieldPlans[index];
                if (field.FieldPath.EndsWith("._foodSpeedAddPercent", StringComparison.Ordinal)
                    && field.Note.IndexOf("movement", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetLevelIndex(string path, out int levelIndex)
        {
            levelIndex = -1;
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Levels[", StringComparison.Ordinal))
            {
                return false;
            }

            int close = path.IndexOf(']');
            return close > 7
                   && int.TryParse(
                       path.Substring(7, close - 7),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out levelIndex)
                   && levelIndex >= 0
                   && levelIndex < 5;
        }

        private static Dictionary<string, int> CountPlans(
            StaffDataDryRunPlanSnapshot plan,
            bool ranks)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                string key = ranks
                    ? plan.StaffPlans[index].TargetRankName
                    : plan.StaffPlans[index].RoleKey;
                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
            }

            return counts;
        }

        private static int GetCount(Dictionary<string, int> counts, string key)
        {
            int count;
            return counts.TryGetValue(key, out count) ? count : 0;
        }

        private static int CountChangedFields(StaffDataDryRunPlanSnapshot plan)
        {
            int count = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                count += plan.StaffPlans[index].ChangedFieldCount;
            }

            return count;
        }

        private static int CountSkillNumberMismatches(
            StaffDataDryRunPlanSnapshot plan,
            bool duration)
        {
            int count = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                string current = duration
                    ? staff.SkillPlan.CurrentDuration
                    : staff.SkillPlan.CurrentCooldown;
                string target = duration
                    ? staff.SkillPlan.TargetDuration
                    : staff.SkillPlan.TargetCooldown;
                count += SkillNumbersEqual(current, target) ? 0 : 1;
            }

            return count;
        }

        private static SkillTimeApplySummary BuildSkillTimeApplySummary(
            StaffDataDryRunPlanSnapshot plan)
        {
            Dictionary<string, StaffDataDryRunStaffPlan> staffById =
                new Dictionary<string, StaffDataDryRunStaffPlan>(StringComparer.Ordinal);
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                staffById[plan.StaffPlans[index].StaffId] = plan.StaffPlans[index];
            }

            int applied = 0;
            int speedUp = 0;
            int touchCustomer = 0;
            for (int index = 0; index < OfficialSkillTimeTargets.Length; index++)
            {
                OfficialSkillTimeTarget target = OfficialSkillTimeTargets[index];
                StaffDataDryRunStaffPlan staff;
                if (!staffById.TryGetValue(target.StaffId, out staff))
                {
                    continue;
                }

                bool targetApplied = staff.SkillPlan.CurrentAssetGuid == target.AssetGuid
                                     && staff.SkillPlan.CurrentAssetPath == target.AssetPath
                                     && staff.SkillPlan.CurrentClassName == target.RuntimeClassName
                                     && SkillNumberEquals(staff.SkillPlan.CurrentDuration, target.Duration)
                                     && SkillNumberEquals(staff.SkillPlan.CurrentCooldown, target.Cooldown);
                if (!targetApplied)
                {
                    continue;
                }

                applied++;
                speedUp += target.RuntimeClassName == "SpeedUpSkill" ? 1 : 0;
                touchCustomer += target.RuntimeClassName == "TouchAddCustomerButtonSkill" ? 1 : 0;
            }

            return new SkillTimeApplySummary(
                applied,
                speedUp,
                touchCustomer,
                CountSkillNumberMismatches(plan, true),
                CountSkillNumberMismatches(plan, false));
        }

        private static bool SkillNumberEquals(string value, int expected)
        {
            double parsed;
            return double.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out parsed)
                   && Math.Abs(parsed - expected) <= 0.0001d;
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
                   && Math.Abs(leftValue - rightValue) <= 0.0001d;
        }

        private static int CountStaffWithSkillPrerequisite(StaffDataDryRunPlanSnapshot plan)
        {
            int count = 0;
            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                IReadOnlyList<StaffDataDryRunIssue> issues = plan.StaffPlans[planIndex].Issues;
                bool found = false;
                for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                {
                    found |= issues[issueIndex].IsPrerequisite
                             && issues[issueIndex].Code.IndexOf("SKILL", StringComparison.Ordinal) >= 0;
                }

                count += found ? 1 : 0;
            }

            return count;
        }

        private static bool MutationIsBlocked(Action mutation)
        {
            try
            {
                mutation();
                return false;
            }
            catch (NotSupportedException)
            {
                return true;
            }
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9')
                      || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Require(bool condition, string error, ValidationResult result)
        {
            if (!condition)
            {
                result.Errors.Add(error);
            }

            return condition;
        }

        private static void LogBuildFailure(string phase, IReadOnlyList<string> diagnostics)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Staff Data Dry Run Plan Validation]");
            output.AppendLine("Plan " + phase + " build: FAIL");
            if (diagnostics != null)
            {
                for (int index = 0; index < diagnostics.Count; index++)
                {
                    output.AppendLine(diagnostics[index]);
                }
            }

            output.AppendLine("STAFF DATA DRY RUN PLAN VALIDATION: FAIL");
            Debug.LogError(output.ToString());
        }

        private sealed class OfficialSkillTimeTarget
        {
            internal string StaffId { get; }
            internal string OfficialSkillId { get; }
            internal string RuntimeClassName { get; }
            internal string AssetGuid { get; }
            internal string AssetPath { get; }
            internal int Duration { get; }
            internal int Cooldown { get; }

            internal OfficialSkillTimeTarget(
                string staffId,
                string officialSkillId,
                string runtimeClassName,
                string assetGuid,
                string assetPath,
                int duration,
                int cooldown)
            {
                StaffId = staffId;
                OfficialSkillId = officialSkillId;
                RuntimeClassName = runtimeClassName;
                AssetGuid = assetGuid;
                AssetPath = assetPath;
                Duration = duration;
                Cooldown = cooldown;
            }
        }

        private sealed class SkillTimeBaseline
        {
            internal string StaffId { get; }
            internal int Duration { get; }
            internal int Cooldown { get; }

            internal SkillTimeBaseline(string staffId, int duration, int cooldown)
            {
                StaffId = staffId;
                Duration = duration;
                Cooldown = cooldown;
            }
        }

        private sealed class SkillTimeApplySummary
        {
            internal int AppliedCount { get; }
            internal int SpeedUpCount { get; }
            internal int TouchCustomerCount { get; }
            internal int DurationMismatchCount { get; }
            internal int CooldownMismatchCount { get; }

            internal SkillTimeApplySummary(
                int appliedCount,
                int speedUpCount,
                int touchCustomerCount,
                int durationMismatchCount,
                int cooldownMismatchCount)
            {
                AppliedCount = appliedCount;
                SpeedUpCount = speedUpCount;
                TouchCustomerCount = touchCustomerCount;
                DurationMismatchCount = durationMismatchCount;
                CooldownMismatchCount = cooldownMismatchCount;
            }
        }

        private sealed class ValidationResult
        {
            internal readonly bool[] Sections = new bool[16];
            internal readonly List<string> Errors = new List<string>();
            internal int WarningCount;
            internal int PrerequisiteCount;
            internal bool Passed
            {
                get
                {
                    if (Errors.Count != 0)
                    {
                        return false;
                    }

                    for (int section = 1; section <= 15; section++)
                    {
                        if (!Sections[section])
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            internal void Set(int section, bool passed)
            {
                Sections[section] = passed;
            }
        }
    }
}
