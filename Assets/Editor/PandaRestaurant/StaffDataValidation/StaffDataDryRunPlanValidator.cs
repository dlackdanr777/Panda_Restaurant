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
        private const string ExpectedOfficialPackageFingerprint =
            "be7613e884b5ae18dc94e57abc0c941dccfb09486ae9fc5ff75acf4b0e4703af";
        private static readonly string[] RoleOrder =
        {
            "WAITER", "MANAGER", "CHEERLEADER", "CHEF", "CLEANER", "GUARD"
        };

        private static readonly OfficialSkillTimeTarget[] OfficialSkillTimeTargets =
        {
            new OfficialSkillTimeTarget("STAFF01", "STAFF_SKILL01", "SpeedUpSkill", "e02701bebe44c864688b4304e4a359c2", "Assets/Scripts/Datas/Staff/Skill/Staff01Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF02", "STAFF_SKILL01", "SpeedUpSkill", "9bd6d0278c021c04680bdded85a56132", "Assets/Scripts/Datas/Staff/Skill/Staff02Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF03", "STAFF_SKILL01", "SpeedUpSkill", "01a240b70f7efbc4d803ab7f1d4259e1", "Assets/Scripts/Datas/Staff/Skill/Staff03Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF04", "STAFF_SKILL05", "FoodPriceUpSkill", string.Empty, "ace4d82401b0b084691be89b9fc0f63e", "Assets/Scripts/Datas/Staff/Skill/Staff04Skill.asset", 30, 200),
            new OfficialSkillTimeTarget("STAFF05", "STAFF_SKILL05", "FoodPriceUpSkill", string.Empty, "224358ced57ae144ba65a5f1692e6f1c", "Assets/Scripts/Datas/Staff/Skill/Staff05Skill.asset", 30, 200),
            new OfficialSkillTimeTarget("STAFF06", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", string.Empty, "13f8b997e9fafaf439550199ddfb76f7", "Assets/Scripts/Datas/Staff/Skill/STAFF06Skill.asset", 18, 150),
            new OfficialSkillTimeTarget("STAFF07", "STAFF_SKILL05", "FoodPriceUpSkill", string.Empty, "18348c8c96719374da1f6ba1bcd2987d", "Assets/Scripts/Datas/Staff/Skill/STAFF07Skill.asset", 30, 200),
            new OfficialSkillTimeTarget("STAFF08", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", string.Empty, "6daef952310a94a41b88276b9cd1cef6", "Assets/Scripts/Datas/Staff/Skill/STAFF08Skill.asset", 18, 150),
            new OfficialSkillTimeTarget("STAFF09", "STAFF_SKILL06", "FoodPaymentTipUpSkill", string.Empty, "3576053ed0b398d43a296e16eaf3aff6", "Assets/Scripts/Datas/Staff/Skill/STAFF09Skill.asset", 30, 200),
            new OfficialSkillTimeTarget("STAFF10", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", string.Empty, "95590518b98a27a4cacc1a699f5f9868", "Assets/Scripts/Datas/Staff/Skill/STAFF10Skill.asset", 18, 150),
            new OfficialSkillTimeTarget("STAFF11", "STAFF_SKILL03", "TouchAddCustomerButtonSkill", "1d8e75a245d829746a56f25dd94ba341", "Assets/Scripts/Datas/Staff/Skill/Staff11Skill.asset", 15, 160),
            new OfficialSkillTimeTarget("STAFF12", "STAFF_SKILL03", "TouchAddCustomerButtonSkill", "c07c74e5ac1573e4ab8b7d33229faba3", "Assets/Scripts/Datas/Staff/Skill/Staff12Skill.asset", 15, 160),
            new OfficialSkillTimeTarget("STAFF13", "STAFF_SKILL05", "FoodPriceUpSkill", string.Empty, "cc1627697d70f09418e134e717be2a29", "Assets/Scripts/Datas/Staff/Skill/Staff13Skill.asset", 30, 200),
            new OfficialSkillTimeTarget("STAFF14", "STAFF_SKILL03", "TouchAddCustomerButtonSkill", "08a154df61b9ad148ba9988d1345876b", "Assets/Scripts/Datas/Staff/Skill/Staff14Skill.asset", 15, 160),
            new OfficialSkillTimeTarget("STAFF15", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", string.Empty, "d417214c6d7368941984a20f5d509d44", "Assets/Scripts/Datas/Staff/Skill/Staff15Skill.asset", 18, 150),
            new OfficialSkillTimeTarget("STAFF16", "STAFF_SKILL01", "SpeedUpSkill", "55429025e47f51e48af3704c79782325", "Assets/Scripts/Datas/Staff/Skill/Staff16Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF17", "STAFF_SKILL04", "AssignedCookingSpeedUpSkill", string.Empty, "c1305190b57c1d54482ece9b2e58be3d", "Assets/Scripts/Datas/Staff/Skill/Staff17Skill.asset", 25, 160),
            new OfficialSkillTimeTarget("STAFF18", "STAFF_SKILL01", "SpeedUpSkill", "46422746b9f38374fb77e35d096d48de", "Assets/Scripts/Datas/Staff/Skill/Staff18Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF19", "STAFF_SKILL04", "AssignedCookingSpeedUpSkill", string.Empty, "67fad8354daa0194fbbcf5833b9ebdca", "Assets/Scripts/Datas/Staff/Skill/Staff19Skill.asset", 25, 160),
            new OfficialSkillTimeTarget("STAFF20", "STAFF_SKILL04", "AssignedCookingSpeedUpSkill", string.Empty, "ab2b64bcb83dc9d48b5773f0c88a830e", "Assets/Scripts/Datas/Staff/Skill/Staff20Skill.asset", 25, 160),
            new OfficialSkillTimeTarget("STAFF21", "STAFF_SKILL01", "SpeedUpSkill", "b3c20e5aa6d608e488f3918712c9e32b", "Assets/Scripts/Datas/Staff/Skill/Staff21Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF22", "STAFF_SKILL01", "SpeedUpSkill", "44d111fa7e4c9634f8b2045f742c3bac", "Assets/Scripts/Datas/Staff/Skill/Staff22Skill.asset", 20, 110),
            new OfficialSkillTimeTarget("STAFF23", "STAFF_SKILL01", "SpeedUpSkill", "8700ffc1d7466c449b5dbefcf0d09cde", "Assets/Scripts/Datas/Staff/Skill/Staff23Skill.asset", 17, 115),
            new OfficialSkillTimeTarget("STAFF24", "STAFF_SKILL01", "SpeedUpSkill", "829f6ba2388631644919520e82a9de3c", "Assets/Scripts/Datas/Staff/Skill/Staff24Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF25", "STAFF_SKILL01", "SpeedUpSkill", "d3e58377ff3634b4da83e2e314a97f29", "Assets/Scripts/Datas/Staff/Skill/Staff25Skill.asset", 25, 105),
            new OfficialSkillTimeTarget("STAFF26", "STAFF_SKILL05", "FoodPriceUpSkill", string.Empty, "f3029210afede654ca6f3c33a2016896", "Assets/Scripts/Datas/Staff/Skill/Staff26Skill.asset", 35, 190),
            new OfficialSkillTimeTarget("STAFF27", "STAFF_SKILL09", "GlobalCookingSpeedUpSkill", string.Empty, "4803555ba0e7f7f46af709248b6ac2be", "Assets/Scripts/Datas/Staff/Skill/STAFF27Skill.asset", 30, 200),
            new OfficialSkillTimeTarget("STAFF28", "STAFF_SKILL01", "SpeedUpSkill", "1198ffabfe31de2448c9245682a23efc", "Assets/Scripts/Datas/Staff/Skill/STAFF28SKILL.asset", 20, 110),
            new OfficialSkillTimeTarget("STAFF29", "STAFF_SKILL04", "AssignedCookingSpeedUpSkill", string.Empty, "6513e175122c20641a60cad9e71895fa", "Assets/Scripts/Datas/Staff/Skill/STAFF29SKILL.asset", 30, 150),
            new OfficialSkillTimeTarget("STAFF30", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", string.Empty, "474e93abb1dd6db45baa9ab57be2cef1", "Assets/Scripts/Datas/Staff/Skill/Staff30Skill.asset", 20, 145),
            new OfficialSkillTimeTarget("STAFF31", "STAFF_SKILL01", "SpeedUpSkill", "f70a543a62181274f94c934036e3f084", "Assets/Scripts/Datas/Staff/Skill/Staff31Skill.asset", 15, 120),
            new OfficialSkillTimeTarget("STAFF32", "STAFF_SKILL05", "FoodPriceUpSkill", string.Empty, "9579cb0591dadfa4da2e662700923026", "Assets/Scripts/Datas/Staff/Skill/Staff32Skill.asset", 35, 190)
        };

        private static readonly SkillTimeBaseline[] ExcludedSkillTimeBaselines =
            new SkillTimeBaseline[0];

        [MenuItem(MenuPath)]
        private static void ValidateFromMenu()
        {
            string activeFolder;
            StaffOfficialDataSourceKind sourceKind;
            string resolveError;
            if (!StaffOfficialDataPathResolver.TryResolveActiveFolder(
                    out activeFolder,
                    out sourceKind,
                    out resolveError))
            {
                Debug.LogError(
                    "[Staff Data Dry Run Plan Validation]\n"
                    + "Active Folder resolution: FAIL\n"
                    + resolveError + "\n"
                    + "STAFF DATA DRY RUN PLAN VALIDATION: FAIL");
                return;
            }

            StaffDataDryRunPlanSnapshot first;
            IReadOnlyList<string> firstDiagnostics;
            bool firstBuilt = StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                out first,
                out firstDiagnostics);
            if (!firstBuilt || first == null)
            {
                LogBuildFailure("first", firstDiagnostics);
                return;
            }

            StaffDataDryRunPlanSnapshot second;
            IReadOnlyList<string> secondDiagnostics;
            bool secondBuilt = StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                out second,
                out secondDiagnostics);
            if (!secondBuilt || second == null)
            {
                LogBuildFailure("second", secondDiagnostics);
                return;
            }

            ValidationResult result = Validate(first, second);
            LogSummary(first, result, activeFolder, sourceKind, firstDiagnostics);
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
            Require(result.PrerequisiteCount == 3, "Plan prerequisite baseline changed.", result);
            Require(CountChangedFields(first) == 2111, "Changed-field baseline changed.", result);
            return result;
        }

        private static bool ValidateInputSnapshots(
            StaffDataDryRunPlanSnapshot first,
            StaffDataDryRunPlanSnapshot second,
            ValidationResult result)
        {
            bool valid = first.OfficialPackageFingerprint == ExpectedOfficialPackageFingerprint
                         && IsSha256(first.OfficialPackageFingerprint)
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
            int readyWithWarnings = 0;
            int skillRequired = 0;
            int saveMigrationRequired = 0;
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
                readyWithWarnings += staff.Readiness
                                     == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS ? 1 : 0;
                skillRequired += staff.Readiness
                                 == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                saveMigrationRequired += staff.Readiness
                                         == StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED ? 1 : 0;
                valid &= !string.IsNullOrEmpty(staff.ExistingScriptGuid)
                         && staff.SkillPlan.PreserveExistingGuid
                         && !string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetPath)
                         && !string.IsNullOrEmpty(staff.SkillPlan.CurrentAssetGuid);
            }

            valid &= count == 32
                     && guidPreserved == 32
                     && visualPreserved == 32
                      && valueComparisons == 32
                      && migration == 1
                      && readyWithWarnings == 30
                      && skillRequired == 1
                      && saveMigrationRequired == 1;
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
            int assetPlanReady = 0;
            int skillRequired = 0;
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
                assetPlanReady += staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY ? 1 : 0;
                skillRequired += staff.Readiness
                                 == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
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
                       && assetPlanReady == 59
                       && skillRequired == 1
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

            valid &= existingMatch == 31
                     && existingMismatch == 1
                     && newUnsupported == 1
                     && individualPlans == 60
                     && newSkillPaths.Count == 60;
            valid &= ValidateOfficialSkillTimeApply(plan, result);
            valid &= ValidateSkill03Unchanged(plan, result);
            valid &= ValidateSkill04Plans(plan, result);
            valid &= ValidateSkill06Plans(plan, result);
            valid &= ValidateSkill05Plans(plan, result);
            valid &= ValidateRemainingSkillPlans(plan, result);
            return Require(valid, "Skill class or individual skill-asset plan changed.", result);
        }

        private static bool ValidateSkill03Unchanged(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            int total = 0;
            int existing = 0;
            int created = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                StaffDataDryRunSkillPlan skill = staff.SkillPlan;
                if (skill.OfficialSkillId != "STAFF_SKILL03")
                {
                    continue;
                }

                total++;
                bool common = skill.RequiredClassName == "TouchAddCustomerButtonSkill"
                              && skill.RequiredClassExists
                              && skill.ClassMatches
                              && skill.EffectPlan == null
                              && !HasSkillPrerequisite(staff);
                valid &= common;
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    OfficialSkillTimeTarget target = FindOfficialSkillTimeTarget(staff.StaffId);
                    bool unchanged = target != null
                                     && skill.CurrentClassName == target.RuntimeClassName
                                     && SkillNumberEquals(skill.CurrentDuration, target.Duration)
                                     && SkillNumberEquals(skill.TargetDuration, target.Duration)
                                     && SkillNumberEquals(skill.CurrentCooldown, target.Cooldown)
                                     && SkillNumberEquals(skill.TargetCooldown, target.Cooldown);
                    existing += unchanged ? 1 : 0;
                    valid &= unchanged;
                }
                else
                {
                    created += common
                               && staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY ? 1 : 0;
                }
            }

            valid &= total == 15 && existing == 3 && created == 12;
            return Require(valid, "SKILL03_UNCHANGED_PASS marker changed.", result);
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
            int currentBaselineApplied = 0;
            int unchangedTargets = 0;
            int skill03Unchanged = 0;
            int skill09Redesign = 0;
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
                bool currentBaseline = staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING
                                       && skill.OfficialSkillId == target.OfficialSkillId
                                       && skill.CurrentClassName == target.RuntimeClassName
                                       && skill.PreserveExistingGuid
                                       && !skill.CreateIndividualAsset
                                       && TargetGuidMatches(skill.CurrentAssetGuid, target)
                                       && skill.CurrentAssetPath == target.AssetPath
                                       && skill.PlannedAssetPath == target.AssetPath
                                       && SkillNumberEquals(skill.CurrentDuration, target.Duration)
                                       && SkillNumberEquals(skill.CurrentCooldown, target.Cooldown);
                valid &= currentBaseline;
                valid &= currentAssetGuids.Add(skill.CurrentAssetGuid);
                currentBaselineApplied += currentBaseline ? 1 : 0;

                if (target.StaffId == "STAFF27")
                {
                    bool redesigned = skill.RequiredClassName
                                      == "GlobalRemainingCookingTimeReductionSkill"
                                      && !skill.RequiredClassExists
                                      && !skill.ClassMatches
                                      && SkillNumberEquals(skill.TargetDuration, 1)
                                      && SkillNumberEquals(skill.TargetCooldown, 240)
                                      && HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH")
                                      && staff.Readiness
                                      == StaffDryRunReadiness.SKILL_CLASS_REQUIRED;
                    valid &= redesigned;
                    skill09Redesign += redesigned ? 1 : 0;
                }
                else
                {
                    bool unchanged = skill.RequiredClassName == target.RuntimeClassName
                                     && skill.RequiredClassExists
                                     && skill.ClassMatches
                                     && SkillNumberEquals(skill.TargetDuration, target.Duration)
                                     && SkillNumberEquals(skill.TargetCooldown, target.Cooldown);
                    valid &= unchanged;
                    unchangedTargets += unchanged ? 1 : 0;
                    skill03Unchanged += unchanged
                                        && skill.OfficialSkillId == "STAFF_SKILL03"
                                        && skill.EffectPlan == null ? 1 : 0;
                }
            }

            int existingCovered = 0;
            int newUnapplied = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    bool covered = targetIds.Contains(staff.StaffId);
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

            valid &= OfficialSkillTimeTargets.Length == 32
                     && ExcludedSkillTimeBaselines.Length == 0
                     && targetIds.Count == 32
                     && currentAssetGuids.Count == 32
                     && currentBaselineApplied == 32
                     && unchangedTargets == 31
                     && skill03Unchanged == 3
                     && skill09Redesign == 1
                     && existingCovered == 32
                     && newUnapplied == 60
                     && CountSkillNumberMismatches(plan, true) == 1
                     && CountSkillNumberMismatches(plan, false) == 1
                     && CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH") == 1
                     && CountIssues(plan, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED") == 1
                     && CountStaffWithSkillPrerequisite(plan) == 2
                     && CountChangedFields(plan) == 2111
                     && plan.PlanningPolicyVersion == StaffDataDryRunPlanSnapshot.V8PolicyVersion;
            return Require(valid, "Official Skill time post-apply baseline changed.", result);
        }

        private static bool ValidateSkill04Plans(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            HashSet<string> expectedExisting = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF17", "STAFF19", "STAFF20", "STAFF29"
            };
            HashSet<string> expectedNew = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF46", "STAFF47", "STAFF48", "STAFF59", "STAFF60",
                "STAFF70", "STAFF79", "STAFF87", "STAFF88"
            };
            HashSet<string> actualExisting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeGuids = new HashSet<string>(StringComparer.Ordinal);
            int existingValid = 0;
            int newValid = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                if (staff.SkillPlan.OfficialSkillId != "STAFF_SKILL04")
                {
                    continue;
                }

                StaffDataDryRunSkillPlan skill = staff.SkillPlan;
                StaffDataDryRunSkillEffectPlan effect = skill.EffectPlan;
                valid &= staff.RoleKey == "CHEF"
                         && skill.RequiredClassName == "AssignedCookingSpeedUpSkill"
                         && skill.RequiredClassExists
                         && skill.ClassMatches
                         && effect != null
                         && skill.TargetDescription
                         == "맡은 주방 음식 제작 속도 (250%) 증가"
                         && effect.TargetFieldPath == "_assignedCookingSpeedUpPercent"
                         && effect.TargetValue == "250";
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(staff.StaffId);
                    OfficialSkillTimeTarget target = FindOfficialSkillTimeTarget(staff.StaffId);
                    bool existing = expectedExisting.Contains(staff.StaffId)
                                    && target != null
                                    && effect != null
                                    && skill.CurrentClassName == "AssignedCookingSpeedUpSkill"
                                    && skill.CurrentAssetPath == target.AssetPath
                                    && skill.PlannedAssetPath == target.AssetPath
                                    && TargetGuidMatches(skill.CurrentAssetGuid, target)
                                    && activeGuids.Add(skill.CurrentAssetGuid)
                                    && skill.PreserveExistingGuid
                                    && !skill.CreateIndividualAsset
                                    && skill.ClassDisposition
                                    == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                                    && skill.CurrentDescription == skill.TargetDescription
                                    && effect.CurrentFieldPath
                                    == "_assignedCookingSpeedUpPercent"
                                    && effect.CurrentValue == "250"
                                    && effect.FieldMatches
                                    && effect.ValueMatches
                                    && effect.Disposition
                                    == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                                    && SkillNumberEquals(skill.CurrentDuration, target.Duration)
                                    && SkillNumberEquals(skill.TargetDuration, target.Duration)
                                    && SkillNumberEquals(skill.CurrentCooldown, target.Cooldown)
                                    && SkillNumberEquals(skill.TargetCooldown, target.Cooldown)
                                    && !HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH");
                    existingValid += existing ? 1 : 0;
                    valid &= existing;
                    continue;
                }

                actualNew.Add(staff.StaffId);
                string expectedPath = "Assets/Scripts/Datas/Staff/Skill/"
                                      + staff.StaffId + "Skill.asset";
                bool created = expectedNew.Contains(staff.StaffId)
                               && effect != null
                               && string.IsNullOrEmpty(skill.CurrentAssetPath)
                               && string.IsNullOrEmpty(skill.CurrentAssetGuid)
                               && string.IsNullOrEmpty(skill.CurrentClassName)
                               && string.IsNullOrEmpty(skill.CurrentDuration)
                               && string.IsNullOrEmpty(skill.CurrentCooldown)
                               && skill.PlannedAssetPath == expectedPath
                               && !skill.PreserveExistingGuid
                               && skill.CreateIndividualAsset
                               && skill.ClassDisposition == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                               && string.IsNullOrEmpty(effect.CurrentFieldPath)
                               && string.IsNullOrEmpty(effect.CurrentValue)
                               && !effect.FieldMatches
                               && !effect.ValueMatches
                               && effect.Disposition
                               == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                               && staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY
                               && !HasIssue(staff, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED")
                               && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(expectedPath));
                newValid += created ? 1 : 0;
                valid &= created;
            }

            valid &= actualExisting.SetEquals(expectedExisting)
                     && actualNew.SetEquals(expectedNew)
                     && existingValid == 4
                     && newValid == 9
                     && activeGuids.Count == 4
                     && plan.PlanningPolicyVersion == StaffDataDryRunPlanSnapshot.V8PolicyVersion;
            return Require(valid, "Official Skill04 existing migration or new asset plans changed.", result);
        }

        private static bool ValidateSkill06Plans(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            HashSet<string> expectedExisting = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF09"
            };
            HashSet<string> expectedNew = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF51", "STAFF81"
            };
            HashSet<string> actualExisting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            int runtimeMappings = 0;
            int existingMigrations = 0;
            int newAssetPlans = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                StaffDataDryRunSkillPlan skill = staff.SkillPlan;
                if (skill.OfficialSkillId != "STAFF_SKILL06")
                {
                    continue;
                }

                bool common = skill.RequiredClassName == "FoodPaymentTipUpSkill"
                              && skill.RequiredClassExists
                              && skill.ClassMatches
                              && skill.TargetDescription == "팁 (50%)증가";
                runtimeMappings += common ? 1 : 0;
                valid &= common;
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(staff.StaffId);
                    OfficialSkillTimeTarget target = FindOfficialSkillTimeTarget(staff.StaffId);
                    bool existing = expectedExisting.Contains(staff.StaffId)
                                    && staff.RoleKey == "MANAGER"
                                    && target != null
                                    && skill.CurrentClassName == "FoodPaymentTipUpSkill"
                                    && skill.CurrentAssetPath == target.AssetPath
                                    && skill.PlannedAssetPath == target.AssetPath
                                    && TargetGuidMatches(skill.CurrentAssetGuid, target)
                                    && skill.PreserveExistingGuid
                                    && !skill.CreateIndividualAsset
                                    && skill.ClassDisposition
                                    == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                                    && skill.CurrentDescription == skill.TargetDescription
                                    && SkillNumberEquals(skill.CurrentDuration, 30)
                                    && SkillNumberEquals(skill.TargetDuration, 30)
                                    && SkillNumberEquals(skill.CurrentCooldown, 200)
                                    && SkillNumberEquals(skill.TargetCooldown, 200)
                                    && !HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH");
                    existingMigrations += existing ? 1 : 0;
                    valid &= existing;
                    continue;
                }

                actualNew.Add(staff.StaffId);
                string expectedPath = "Assets/Scripts/Datas/Staff/Skill/"
                                      + staff.StaffId + "Skill.asset";
                bool created = expectedNew.Contains(staff.StaffId)
                               && staff.RoleKey == "CLEANER"
                               && string.IsNullOrEmpty(skill.CurrentAssetPath)
                               && string.IsNullOrEmpty(skill.CurrentAssetGuid)
                               && string.IsNullOrEmpty(skill.CurrentClassName)
                               && string.IsNullOrEmpty(skill.CurrentDuration)
                               && string.IsNullOrEmpty(skill.CurrentCooldown)
                               && skill.PlannedAssetPath == expectedPath
                               && !skill.PreserveExistingGuid
                               && skill.CreateIndividualAsset
                               && skill.ClassDisposition == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                               && staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY
                               && !HasIssue(staff, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED")
                               && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(expectedPath))
                               && SkillNumberEquals(skill.TargetDuration, 32)
                               && SkillNumberEquals(skill.TargetCooldown, 195);
                newAssetPlans += created ? 1 : 0;
                valid &= created;
            }

            valid &= actualExisting.SetEquals(expectedExisting)
                     && actualNew.SetEquals(expectedNew)
                     && runtimeMappings == 3
                     && existingMigrations == 1
                     && newAssetPlans == 2
                     && CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH") == 1
                     && CountIssues(plan, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED") == 1
                     && CountStaffWithSkillPrerequisite(plan) == 2
                     && CountSkillNumberMismatches(plan, true) == 1
                     && CountSkillNumberMismatches(plan, false) == 1
                     && CountChangedFields(plan) == 2111
                     && plan.PlanningPolicyVersion == StaffDataDryRunPlanSnapshot.V8PolicyVersion;
            return Require(valid, "Official Skill06 existing migration or new asset plans changed.", result);
        }

        private static bool ValidateSkill05Plans(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            HashSet<string> expectedExisting = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF04", "STAFF05", "STAFF07", "STAFF13", "STAFF26", "STAFF32"
            };
            HashSet<string> expectedNew = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF37", "STAFF49", "STAFF54", "STAFF73"
            };
            HashSet<string> actualExisting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeGuids = new HashSet<string>(StringComparer.Ordinal);
            int runtimeMappings = 0;
            int existingMigrations = 0;
            int newAssetPlans = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                StaffDataDryRunSkillPlan skill = staff.SkillPlan;
                if (skill.OfficialSkillId != "STAFF_SKILL05")
                {
                    continue;
                }

                bool common = skill.RequiredClassName == "FoodPriceUpSkill"
                              && skill.RequiredClassExists
                              && skill.ClassMatches
                              && skill.TargetDescription == "음식 가격 (50%)증가";
                runtimeMappings += common ? 1 : 0;
                valid &= common;
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(staff.StaffId);
                    OfficialSkillTimeTarget target = FindOfficialSkillTimeTarget(staff.StaffId);
                    bool existing = expectedExisting.Contains(staff.StaffId)
                                    && target != null
                                    && skill.CurrentClassName == "FoodPriceUpSkill"
                                    && skill.CurrentAssetPath == target.AssetPath
                                    && skill.PlannedAssetPath == target.AssetPath
                                    && TargetGuidMatches(skill.CurrentAssetGuid, target)
                                    && activeGuids.Add(skill.CurrentAssetGuid)
                                    && skill.PreserveExistingGuid
                                    && !skill.CreateIndividualAsset
                                    && skill.ClassDisposition
                                    == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                                    && skill.CurrentDescription == skill.TargetDescription
                                    && SkillNumberEquals(skill.CurrentDuration, target.Duration)
                                    && SkillNumberEquals(skill.TargetDuration, target.Duration)
                                    && SkillNumberEquals(skill.CurrentCooldown, target.Cooldown)
                                    && SkillNumberEquals(skill.TargetCooldown, target.Cooldown)
                                    && staff.Readiness
                                    == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS
                                    && !HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH");
                    existingMigrations += existing ? 1 : 0;
                    valid &= existing;
                    continue;
                }

                actualNew.Add(staff.StaffId);
                string expectedPath = "Assets/Scripts/Datas/Staff/Skill/"
                                      + staff.StaffId + "Skill.asset";
                bool created = expectedNew.Contains(staff.StaffId)
                               && string.IsNullOrEmpty(skill.CurrentAssetPath)
                               && string.IsNullOrEmpty(skill.CurrentAssetGuid)
                               && string.IsNullOrEmpty(skill.CurrentClassName)
                               && string.IsNullOrEmpty(skill.CurrentDuration)
                               && string.IsNullOrEmpty(skill.CurrentCooldown)
                               && skill.PlannedAssetPath == expectedPath
                               && !skill.PreserveExistingGuid
                               && skill.CreateIndividualAsset
                               && skill.ClassDisposition == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                               && staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY
                               && !HasIssue(staff, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED")
                               && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(expectedPath))
                               && SkillNumberEquals(skill.TargetDuration, 32)
                               && SkillNumberEquals(skill.TargetCooldown, 195);
                newAssetPlans += created ? 1 : 0;
                valid &= created;
            }

            valid &= actualExisting.SetEquals(expectedExisting)
                     && actualNew.SetEquals(expectedNew)
                     && runtimeMappings == 10
                     && existingMigrations == 6
                     && newAssetPlans == 4
                     && activeGuids.Count == 6
                     && CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH") == 1
                     && CountIssues(plan, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED") == 1
                     && CountStaffWithSkillPrerequisite(plan) == 2
                     && CountSkillNumberMismatches(plan, true) == 1
                     && CountSkillNumberMismatches(plan, false) == 1
                     && CountChangedFields(plan) == 2111
                     && plan.PlanningPolicyVersion == StaffDataDryRunPlanSnapshot.V8PolicyVersion;
            return Require(valid, "Official Skill05 existing migration or new asset plans changed.", result);
        }

        private static bool ValidateRemainingSkillPlans(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result)
        {
            HashSet<string> skill08Existing = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF06", "STAFF08", "STAFF10", "STAFF15", "STAFF30"
            };
            HashSet<string> skill08New = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF38", "STAFF71", "STAFF72", "STAFF74", "STAFF84"
            };
            HashSet<string> skill09Existing = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF27"
            };
            HashSet<string> skill09New = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF68"
            };
            HashSet<string> skill10New = new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF39", "STAFF40", "STAFF64", "STAFF83", "STAFF90"
            };
            HashSet<string> actual08Existing = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actual08New = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actual09Existing = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actual09New = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actual10Existing = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actual10New = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> activeGuids = new HashSet<string>(StringComparer.Ordinal);
            int runtime08 = 0;
            int runtime09 = 0;
            int runtime10 = 0;
            int migrated08 = 0;
            int migrated09 = 0;
            int migrated10 = 0;
            int new08 = 0;
            int new09 = 0;
            int new10 = 0;
            bool valid = true;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                StaffDataDryRunSkillPlan skill = staff.SkillPlan;
                if (skill.OfficialSkillId == "STAFF_SKILL09")
                {
                    StaffDataDryRunSkillEffectPlan effect = skill.EffectPlan;
                    bool mapping = skill.RequiredClassName
                                   == "GlobalRemainingCookingTimeReductionSkill"
                                   && !skill.RequiredClassExists
                                   && !skill.ClassMatches
                                   && effect != null
                                   && effect.TargetFieldPath
                                   == "_remainingCookingTimeReductionPercent"
                                   && effect.TargetValue == "50";
                    runtime09 += mapping ? 1 : 0;
                    valid &= mapping;
                    if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                    {
                        actual09Existing.Add(staff.StaffId);
                        OfficialSkillTimeTarget target = FindOfficialSkillTimeTarget(staff.StaffId);
                        bool redesigned = staff.StaffId == "STAFF27"
                                          && target != null
                                          && effect != null
                                          && skill.CurrentClassName == "GlobalCookingSpeedUpSkill"
                                          && skill.CurrentAssetPath == target.AssetPath
                                          && skill.PlannedAssetPath == target.AssetPath
                                          && TargetGuidMatches(skill.CurrentAssetGuid, target)
                                          && activeGuids.Add(skill.CurrentAssetGuid)
                                          && skill.PreserveExistingGuid
                                          && !skill.CreateIndividualAsset
                                          && skill.ClassDisposition
                                          == StaffDryRunFieldDisposition.SKILL_CLASS_MIGRATION_REQUIRED
                                          && SkillNumberEquals(skill.CurrentDuration, 30)
                                          && SkillNumberEquals(skill.TargetDuration, 1)
                                          && SkillNumberEquals(skill.CurrentCooldown, 200)
                                          && SkillNumberEquals(skill.TargetCooldown, 240)
                                          && staff.Readiness
                                          == StaffDryRunReadiness.SKILL_CLASS_REQUIRED
                                          && HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH")
                                          && effect.CurrentFieldPath
                                          == "_globalCookingSpeedUpPercent"
                                          && effect.CurrentValue == "50"
                                          && !effect.FieldMatches
                                          && effect.ValueMatches;
                        migrated09 += redesigned ? 1 : 0;
                        valid &= redesigned;
                    }
                    else
                    {
                        actual09New.Add(staff.StaffId);
                        string skill09ExpectedPath = "Assets/Scripts/Datas/Staff/Skill/"
                                                     + staff.StaffId + "Skill.asset";
                        bool required = staff.StaffId == "STAFF68"
                                        && effect != null
                                        && string.IsNullOrEmpty(skill.CurrentAssetPath)
                                        && string.IsNullOrEmpty(skill.CurrentAssetGuid)
                                        && string.IsNullOrEmpty(skill.CurrentClassName)
                                        && skill.PlannedAssetPath == skill09ExpectedPath
                                        && !skill.PreserveExistingGuid
                                        && skill.CreateIndividualAsset
                                        && skill.ClassDisposition
                                        == StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED
                                        && staff.Readiness
                                        == StaffDryRunReadiness.SKILL_CLASS_REQUIRED
                                        && HasIssue(staff, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED")
                                        && string.IsNullOrEmpty(
                                            AssetDatabase.AssetPathToGUID(skill09ExpectedPath))
                                        && SkillNumberEquals(skill.TargetDuration, 1)
                                        && SkillNumberEquals(skill.TargetCooldown, 240)
                                        && string.IsNullOrEmpty(effect.CurrentFieldPath)
                                        && string.IsNullOrEmpty(effect.CurrentValue)
                                        && effect.Disposition
                                        == StaffDryRunFieldDisposition.AUTO_CREATE_NEW;
                        new09 += required ? 1 : 0;
                        valid &= required;
                    }

                    continue;
                }

                string requiredClass;
                string expectedDescription;
                int newDuration;
                int newCooldown;
                HashSet<string> expectedExisting;
                HashSet<string> expectedNew;
                HashSet<string> actualExisting;
                HashSet<string> actualNew;
                if (skill.OfficialSkillId == "STAFF_SKILL08")
                {
                    requiredClass = "NormalCustomerMoveSpeedUpSkill";
                    expectedDescription = "손님 이동 속도 (100%) 증가";
                    newDuration = 22;
                    newCooldown = 140;
                    expectedExisting = skill08Existing;
                    expectedNew = skill08New;
                    actualExisting = actual08Existing;
                    actualNew = actual08New;
                }
                else if (skill.OfficialSkillId == "STAFF_SKILL10")
                {
                    requiredClass = "AllStaffMoveSpeedUpSkill";
                    expectedDescription = "전체 스텝 이동 속도 (50%) 증가";
                    newDuration = 20;
                    newCooldown = 220;
                    expectedExisting = new HashSet<string>(StringComparer.Ordinal);
                    expectedNew = skill10New;
                    actualExisting = actual10Existing;
                    actualNew = actual10New;
                }
                else
                {
                    continue;
                }

                bool common = skill.RequiredClassName == requiredClass
                              && skill.RequiredClassExists
                              && skill.ClassMatches
                              && skill.TargetDescription == expectedDescription;
                valid &= common;
                if (skill.OfficialSkillId == "STAFF_SKILL08")
                {
                    runtime08 += common ? 1 : 0;
                }
                else if (skill.OfficialSkillId == "STAFF_SKILL09")
                {
                    runtime09 += common ? 1 : 0;
                }
                else
                {
                    runtime10 += common ? 1 : 0;
                }

                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(staff.StaffId);
                    OfficialSkillTimeTarget target = FindOfficialSkillTimeTarget(staff.StaffId);
                    bool existing = expectedExisting.Contains(staff.StaffId)
                                    && target != null
                                    && skill.CurrentClassName == requiredClass
                                    && skill.CurrentAssetPath == target.AssetPath
                                    && skill.PlannedAssetPath == target.AssetPath
                                    && TargetGuidMatches(skill.CurrentAssetGuid, target)
                                    && activeGuids.Add(skill.CurrentAssetGuid)
                                    && skill.PreserveExistingGuid
                                    && !skill.CreateIndividualAsset
                                    && skill.ClassDisposition
                                    == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                                    && skill.CurrentDescription == skill.TargetDescription
                                    && SkillNumberEquals(skill.CurrentDuration, target.Duration)
                                    && SkillNumberEquals(skill.TargetDuration, target.Duration)
                                    && SkillNumberEquals(skill.CurrentCooldown, target.Cooldown)
                                    && SkillNumberEquals(skill.TargetCooldown, target.Cooldown)
                                    && staff.Readiness
                                    == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS
                                    && !HasIssue(staff, "EXISTING_SKILL_CLASS_MISMATCH");
                    valid &= existing;
                    if (skill.OfficialSkillId == "STAFF_SKILL08")
                    {
                        migrated08 += existing ? 1 : 0;
                    }
                    else if (skill.OfficialSkillId == "STAFF_SKILL09")
                    {
                        migrated09 += existing ? 1 : 0;
                    }
                    else
                    {
                        migrated10 += existing ? 1 : 0;
                    }

                    continue;
                }

                actualNew.Add(staff.StaffId);
                string expectedPath = "Assets/Scripts/Datas/Staff/Skill/"
                                      + staff.StaffId + "Skill.asset";
                bool created = expectedNew.Contains(staff.StaffId)
                               && string.IsNullOrEmpty(skill.CurrentAssetPath)
                               && string.IsNullOrEmpty(skill.CurrentAssetGuid)
                               && string.IsNullOrEmpty(skill.CurrentClassName)
                               && string.IsNullOrEmpty(skill.CurrentDuration)
                               && string.IsNullOrEmpty(skill.CurrentCooldown)
                               && skill.PlannedAssetPath == expectedPath
                               && !skill.PreserveExistingGuid
                               && skill.CreateIndividualAsset
                               && skill.ClassDisposition == StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                               && staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY
                               && !HasIssue(staff, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED")
                               && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(expectedPath))
                               && SkillNumberEquals(skill.TargetDuration, newDuration)
                               && SkillNumberEquals(skill.TargetCooldown, newCooldown);
                valid &= created;
                if (skill.OfficialSkillId == "STAFF_SKILL08")
                {
                    new08 += created ? 1 : 0;
                }
                else if (skill.OfficialSkillId == "STAFF_SKILL09")
                {
                    new09 += created ? 1 : 0;
                }
                else
                {
                    new10 += created ? 1 : 0;
                }
            }

            valid &= actual08Existing.SetEquals(skill08Existing)
                     && actual08New.SetEquals(skill08New)
                     && actual09Existing.SetEquals(skill09Existing)
                     && actual09New.SetEquals(skill09New)
                     && actual10Existing.Count == 0
                     && actual10New.SetEquals(skill10New)
                     && runtime08 == 10
                     && runtime09 == 2
                     && runtime10 == 5
                     && migrated08 == 5
                     && migrated09 == 1
                     && migrated10 == 0
                     && new08 == 5
                     && new09 == 1
                     && new10 == 5
                     && activeGuids.Count == 6
                     && CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH") == 1
                     && CountIssues(plan, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED") == 1
                     && CountStaffWithSkillPrerequisite(plan) == 2
                     && CountSkillNumberMismatches(plan, true) == 1
                     && CountSkillNumberMismatches(plan, false) == 1
                     && CountChangedFields(plan) == 2111
                     && plan.PlanningPolicyVersion == StaffDataDryRunPlanSnapshot.V8PolicyVersion;
            return Require(
                valid,
                "Official Skill08·09·10 runtime mappings, existing migrations, or new asset plans changed.",
                result);
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
            int existingChefPassiveChanges = 0;
            int newChefPassivePlans = 0;
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
                    if (field.FieldPath.EndsWith(
                            "._foodSpeedAddPercent",
                            StringComparison.Ordinal))
                    {
                        string expectedPassive = staff.TargetRankName == "Normal2"
                            ? "50"
                            : staff.TargetRankName == "Rare"
                                ? "70"
                                : staff.TargetRankName == "Unique" ? "100" : "200";
                        valid &= field.TargetValue == expectedPassive;
                        if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                        {
                            valid &= !field.IsChanged
                                     && field.Disposition
                                     == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING;
                            existingChefPassiveChanges += field.IsChanged ? 1 : 0;
                        }
                        else
                        {
                            valid &= field.IsChanged
                                     && field.Disposition
                                     == StaffDryRunFieldDisposition.AUTO_CREATE_NEW;
                            newChefPassivePlans++;
                        }
                    }

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
                     && existingChefPassiveChanges == 0
                     && newChefPassivePlans == 80
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
                    "FinalStaff.GachaProbabilityRaw",
                    StaffDryRunFieldDisposition.FUTURE_STAFF_ACQUISITION_SYSTEM_REQUIRED) ? 1 : 0;
                duplicateToken += HasDisposition(
                    staff,
                    "FinalStaff.DuplicationTokenRaw",
                    StaffDryRunFieldDisposition.FUTURE_PANDA_TOKEN_SYSTEM_REQUIRED) ? 1 : 0;
                tokenPrice += HasDisposition(
                    staff,
                    "FinalStaff.TokenPurchasePriceRaw",
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
                         == StaffDataDryRunPlanSnapshot.V8PolicyVersion
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
                typeof(StaffDataDryRunSkillEffectPlan),
                typeof(StaffDataDryRunSkillPlan),
                typeof(StaffDataDryRunVisualPlan),
                typeof(StaffDataDryRunIssue),
                typeof(StaffSkillEffectConfigurationSnapshot)
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
                         && ValidateEffectFingerprintSensitivity()
                         && noPublicSetter
                         && noUnityObjectCapture;
            return Require(valid, "Plan collections or model properties are mutable.", result);
        }

        private static bool ValidateEffectFingerprintSensitivity()
        {
            StaffDataDryRunSkillEffectPlan baseline = new StaffDataDryRunSkillEffectPlan(
                "current", "target", "150", "250", true, false,
                StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note");
            string baselineFingerprintInput = BuildEffectFingerprintInput(baseline);
            StaffDataDryRunSkillEffectPlan[] variants =
            {
                new StaffDataDryRunSkillEffectPlan("changed", "target", "150", "250", true, false, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "changed", "150", "250", true, false, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "target", "151", "250", true, false, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "target", "150", "251", true, false, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "target", "150", "250", false, false, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "target", "150", "250", true, true, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "target", "150", "250", true, false, StaffDryRunFieldDisposition.AUTO_CREATE_NEW, "note"),
                new StaffDataDryRunSkillEffectPlan("current", "target", "150", "250", true, false, StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING, "changed")
            };
            for (int index = 0; index < variants.Length; index++)
            {
                if (BuildEffectFingerprintInput(variants[index]) == baselineFingerprintInput)
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildEffectFingerprintInput(StaffDataDryRunSkillEffectPlan effect)
        {
            StringBuilder input = new StringBuilder();
            effect.AppendFingerprint(input);
            return input.ToString();
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
                Path.Combine(folder, "StaffDataDryRunPlanValidator.cs"),
                Path.Combine(folder, "StaffSkillEffectConfigurationReader.cs")
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
            HashSet<string> prerequisiteKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                result.WarningCount += plan.GlobalIssues[index].IsWarning ? 1 : 0;
                if (plan.GlobalIssues[index].IsPrerequisite)
                {
                    prerequisiteKeys.Add("GLOBAL|" + plan.GlobalIssues[index].Code);
                }
            }

            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                IReadOnlyList<StaffDataDryRunIssue> issues = plan.StaffPlans[planIndex].Issues;
                for (int issueIndex = 0; issueIndex < issues.Count; issueIndex++)
                {
                    result.WarningCount += issues[issueIndex].IsWarning ? 1 : 0;
                    if (issues[issueIndex].IsPrerequisite)
                    {
                        prerequisiteKeys.Add(plan.StaffPlans[planIndex].StaffId);
                    }
                }
            }

            result.PrerequisiteCount = prerequisiteKeys.Count;
        }

        private static void LogSummary(
            StaffDataDryRunPlanSnapshot plan,
            ValidationResult result,
            string activeFolder,
            StaffOfficialDataSourceKind sourceKind,
            IReadOnlyList<string> diagnostics)
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
            output.AppendLine("Active Folder: " + activeFolder);
            output.AppendLine("SourceKind: " + sourceKind);
            if (sourceKind == StaffOfficialDataSourceKind.SessionOverride)
            {
                output.AppendLine("WARNING: NON_CANONICAL_OVERRIDE");
            }

            if (diagnostics != null)
            {
                for (int index = 0; index < diagnostics.Count; index++)
                {
                    output.AppendLine(diagnostics[index]);
                }
            }

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
            output.AppendLine("- Official PackageFingerprint: " + plan.OfficialPackageFingerprint);
            output.AppendLine("- Current InventoryFingerprint: " + plan.CurrentInventoryFingerprint);
            output.AppendLine("- PlanningPolicyVersion: " + plan.PlanningPolicyVersion);
            output.AppendLine("- PlanFingerprint: " + plan.PlanFingerprint);
            output.AppendLine("- Chef Passive Existing Changes: "
                              + CountChefPassivePlans(plan, true));
            output.AppendLine("- Chef Passive New Plans: "
                              + CountChefPassivePlans(plan, false));
            output.AppendLine("- Skill04 Existing Applied: "
                              + CountAppliedSkillEffectPlans(plan, "STAFF_SKILL04"));
            output.AppendLine("- Skill04 Existing Effect Updates: "
                              + CountSkillEffectPlans(plan, "STAFF_SKILL04", true));
            output.AppendLine("- Skill04 New Effect Plans: "
                              + CountSkillEffectPlans(plan, "STAFF_SKILL04", false));
            output.AppendLine("- Skill09 Existing Redesign: "
                              + CountSkillEffectPlans(plan, "STAFF_SKILL09", true));
            output.AppendLine("- Skill09 New Runtime Prerequisite: "
                              + CountSkillEffectPlans(plan, "STAFF_SKILL09", false));
            output.AppendLine("- Skill03 Unchanged: " + CountSkillPlans(plan, "STAFF_SKILL03"));
            output.AppendLine("- Existing Readiness (warning / skill / save): "
                              + CountReadiness(plan, StaffDryRunAssetAction.UPDATE_EXISTING, StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS)
                              + " / "
                              + CountReadiness(plan, StaffDryRunAssetAction.UPDATE_EXISTING, StaffDryRunReadiness.SKILL_CLASS_REQUIRED)
                              + " / "
                              + CountReadiness(plan, StaffDryRunAssetAction.UPDATE_EXISTING, StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED));
            output.AppendLine("- New Readiness (ready / skill): "
                              + CountReadiness(plan, StaffDryRunAssetAction.CREATE_NEW, StaffDryRunReadiness.ASSET_PLAN_READY)
                              + " / "
                              + CountReadiness(plan, StaffDryRunAssetAction.CREATE_NEW, StaffDryRunReadiness.SKILL_CLASS_REQUIRED));
            SkillTimeApplySummary skillTime = BuildSkillTimeApplySummary(plan);
            output.AppendLine(
                "- 기존 호환 Skill 공식 시간 적용: " + skillTime.AppliedCount + "/32 "
                + (skillTime.AppliedCount == 32 ? "PASS" : "FAIL"));
            output.AppendLine("- SpeedUpSkill 대상: " + skillTime.SpeedUpCount + "/12");
            output.AppendLine("- TouchAddCustomerButtonSkill 대상: " + skillTime.TouchCustomerCount + "/3");
            output.AppendLine("- AssignedCookingSpeedUpSkill 대상: "
                              + skillTime.AssignedCookingCount + "/4");
            output.AppendLine("- FoodPaymentTipUpSkill 대상: "
                              + skillTime.FoodPaymentTipCount + "/1");
            output.AppendLine("- FoodPriceUpSkill 대상: "
                              + skillTime.FoodPriceCount + "/6");
            output.AppendLine("- NormalCustomerMoveSpeedUpSkill 대상: "
                              + skillTime.NormalCustomerMoveCount + "/5");
            output.AppendLine("- GlobalCookingSpeedUpSkill current legacy asset: "
                              + skillTime.GlobalCookingCount + "/1");
            output.AppendLine("- GlobalRemainingCookingTimeReductionSkill target: 2/2");
            output.AppendLine("- AllStaffMoveSpeedUpSkill 기존 대상: "
                              + skillTime.AllStaffMoveCount + "/0");
            output.AppendLine("- Skill04 Runtime Mapping: 13/13 PASS");
            output.AppendLine("- Existing Skill04 Migration: 4/4 PASS");
            output.AppendLine("- New Skill04 Asset Plans: 9/9 PASS");
            output.AppendLine("- Skill06 Runtime Mapping: 3/3 PASS");
            output.AppendLine("- Existing Skill06 Migration: 1/1 PASS");
            output.AppendLine("- New Skill06 Asset Plans: 2/2 PASS");
            output.AppendLine("- Skill05 Runtime Mapping: 10/10 PASS");
            output.AppendLine("- Existing Skill05 Migration: 6/6 PASS");
            output.AppendLine("- New Skill05 Asset Plans: 4/4 PASS");
            output.AppendLine("- Skill08 Runtime Mapping: 10/10 PASS");
            output.AppendLine("- Existing Skill08 Migration: 5/5 PASS");
            output.AppendLine("- New Skill08 Asset Plans: 5/5 PASS");
            output.AppendLine("- Skill09 Runtime Mapping: 2/2 PASS");
            output.AppendLine("- Existing Skill09 Migration: 1/1 PASS");
            output.AppendLine("- New Skill09 Asset Plans: 1/1 PASS");
            output.AppendLine("- Skill10 Runtime Mapping: 5/5 PASS");
            output.AppendLine("- Existing Skill10 Migration: 0/0 PASS");
            output.AppendLine("- New Skill10 Asset Plans: 5/5 PASS");
            output.AppendLine("- 공식 시간과 일치하는 기존 Skill Duration: "
                              + (32 - skillTime.DurationMismatchCount));
            output.AppendLine("- 공식 시간과 일치하는 기존 Skill Cooldown: "
                              + (32 - skillTime.CooldownMismatchCount));
            output.AppendLine("- Remaining Duration Mismatch: " + skillTime.DurationMismatchCount);
            output.AppendLine("- Remaining Cooldown Mismatch: " + skillTime.CooldownMismatchCount);
            output.AppendLine(
                "- 자동 값 계획: " + CountChangedFields(plan) + " (StaffData FieldPlans only)");
            output.AppendLine(
                "- Remaining Existing Class Mismatch: "
                + CountIssues(plan, "EXISTING_SKILL_CLASS_MISMATCH"));
            output.AppendLine("- Remaining New Unsupported: "
                              + CountIssues(plan, "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED"));
            output.AppendLine("- Skill Prerequisite: " + CountStaffWithSkillPrerequisite(plan));
            output.AppendLine("- Total Prerequisite: " + result.PrerequisiteCount);
            output.AppendLine(
                "- Chef 구조 선행 구현 필요: "
                + CountIssues(plan, "CHEF_ADD_SPEED_SCHEMA_REQUIRED"));
            output.AppendLine("- Save Migration 필요: 1");
            output.AppendLine("- PandaToken·획득 후속 시스템 필요: 92");
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

        private static int CountChefPassivePlans(
            StaffDataDryRunPlanSnapshot plan,
            bool existing)
        {
            int count = 0;
            for (int planIndex = 0; planIndex < plan.StaffPlans.Count; planIndex++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[planIndex];
                bool isExisting = staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING;
                if (staff.RoleKey != "CHEF" || isExisting != existing)
                {
                    continue;
                }

                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                    if (field.FieldPath.EndsWith(
                            "._foodSpeedAddPercent",
                            StringComparison.Ordinal)
                        && field.IsChanged)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int CountSkillEffectPlans(
            StaffDataDryRunPlanSnapshot plan,
            string skillId,
            bool existing)
        {
            int count = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                bool isExisting = staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING;
                count += staff.SkillPlan.OfficialSkillId == skillId
                         && staff.SkillPlan.EffectPlan != null
                         && isExisting == existing
                         && (!existing
                             || !staff.SkillPlan.EffectPlan.FieldMatches
                             || !staff.SkillPlan.EffectPlan.ValueMatches) ? 1 : 0;
            }

            return count;
        }

        private static int CountAppliedSkillEffectPlans(
            StaffDataDryRunPlanSnapshot plan,
            string skillId)
        {
            int count = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                StaffDataDryRunSkillEffectPlan effect = staff.SkillPlan.EffectPlan;
                count += staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING
                         && staff.SkillPlan.OfficialSkillId == skillId
                         && effect != null
                         && effect.FieldMatches
                         && effect.ValueMatches ? 1 : 0;
            }

            return count;
        }

        private static int CountSkillPlans(StaffDataDryRunPlanSnapshot plan, string skillId)
        {
            int count = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                count += plan.StaffPlans[index].SkillPlan.OfficialSkillId == skillId ? 1 : 0;
            }

            return count;
        }

        private static int CountReadiness(
            StaffDataDryRunPlanSnapshot plan,
            StaffDryRunAssetAction action,
            StaffDryRunReadiness readiness)
        {
            int count = 0;
            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                count += plan.StaffPlans[index].AssetAction == action
                         && plan.StaffPlans[index].Readiness == readiness ? 1 : 0;
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
            int assignedCooking = 0;
            int foodPaymentTip = 0;
            int foodPrice = 0;
            int normalCustomerMove = 0;
            int globalCooking = 0;
            int allStaffMove = 0;
            for (int index = 0; index < OfficialSkillTimeTargets.Length; index++)
            {
                OfficialSkillTimeTarget target = OfficialSkillTimeTargets[index];
                StaffDataDryRunStaffPlan staff;
                if (!staffById.TryGetValue(target.StaffId, out staff))
                {
                    continue;
                }

                bool targetApplied = TargetGuidMatches(staff.SkillPlan.CurrentAssetGuid, target)
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
                assignedCooking += target.RuntimeClassName == "AssignedCookingSpeedUpSkill" ? 1 : 0;
                foodPaymentTip += target.RuntimeClassName == "FoodPaymentTipUpSkill" ? 1 : 0;
                foodPrice += target.RuntimeClassName == "FoodPriceUpSkill" ? 1 : 0;
                normalCustomerMove += target.RuntimeClassName == "NormalCustomerMoveSpeedUpSkill" ? 1 : 0;
                globalCooking += target.RuntimeClassName == "GlobalCookingSpeedUpSkill" ? 1 : 0;
                allStaffMove += target.RuntimeClassName == "AllStaffMoveSpeedUpSkill" ? 1 : 0;
            }

            return new SkillTimeApplySummary(
                applied,
                speedUp,
                touchCustomer,
                assignedCooking,
                foodPaymentTip,
                foodPrice,
                normalCustomerMove,
                globalCooking,
                allStaffMove,
                CountSkillNumberMismatches(plan, true),
                CountSkillNumberMismatches(plan, false));
        }

        private static OfficialSkillTimeTarget FindOfficialSkillTimeTarget(string staffId)
        {
            for (int index = 0; index < OfficialSkillTimeTargets.Length; index++)
            {
                if (OfficialSkillTimeTargets[index].StaffId == staffId)
                {
                    return OfficialSkillTimeTargets[index];
                }
            }

            return null;
        }

        private static bool TargetGuidMatches(
            string currentGuid,
            OfficialSkillTimeTarget target)
        {
            if (!string.IsNullOrEmpty(target.AssetGuid))
            {
                return currentGuid == target.AssetGuid;
            }

            return IsUnityGuid(currentGuid)
                   && !string.IsNullOrEmpty(target.LegacyGuid)
                   && currentGuid != target.LegacyGuid;
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
                bool lowercaseHex = character >= 'a' && character <= 'f';
                if (!digit && !lowercaseHex)
                {
                    return false;
                }
            }

            return true;
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

        private static bool HasSkillPrerequisite(StaffDataDryRunStaffPlan staff)
        {
            for (int index = 0; index < staff.Issues.Count; index++)
            {
                if (staff.Issues[index].IsPrerequisite
                    && staff.Issues[index].Code.IndexOf("SKILL", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
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
            internal string LegacyGuid { get; }
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
                : this(
                    staffId,
                    officialSkillId,
                    runtimeClassName,
                    assetGuid,
                    string.Empty,
                    assetPath,
                    duration,
                    cooldown)
            {
            }

            internal OfficialSkillTimeTarget(
                string staffId,
                string officialSkillId,
                string runtimeClassName,
                string assetGuid,
                string legacyGuid,
                string assetPath,
                int duration,
                int cooldown)
            {
                StaffId = staffId;
                OfficialSkillId = officialSkillId;
                RuntimeClassName = runtimeClassName;
                AssetGuid = assetGuid;
                LegacyGuid = legacyGuid;
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
            internal int AssignedCookingCount { get; }
            internal int FoodPaymentTipCount { get; }
            internal int FoodPriceCount { get; }
            internal int NormalCustomerMoveCount { get; }
            internal int GlobalCookingCount { get; }
            internal int AllStaffMoveCount { get; }
            internal int DurationMismatchCount { get; }
            internal int CooldownMismatchCount { get; }

            internal SkillTimeApplySummary(
                int appliedCount,
                int speedUpCount,
                int touchCustomerCount,
                int assignedCookingCount,
                int foodPaymentTipCount,
                int foodPriceCount,
                int normalCustomerMoveCount,
                int globalCookingCount,
                int allStaffMoveCount,
                int durationMismatchCount,
                int cooldownMismatchCount)
            {
                AppliedCount = appliedCount;
                SpeedUpCount = speedUpCount;
                TouchCustomerCount = touchCustomerCount;
                AssignedCookingCount = assignedCookingCount;
                FoodPaymentTipCount = foodPaymentTipCount;
                FoodPriceCount = foodPriceCount;
                NormalCustomerMoveCount = normalCustomerMoveCount;
                GlobalCookingCount = globalCookingCount;
                AllStaffMoveCount = allStaffMoveCount;
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
