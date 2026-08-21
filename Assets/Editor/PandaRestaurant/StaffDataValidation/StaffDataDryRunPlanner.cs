using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using UnityEditor;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffDataDryRunPlanner
    {
        private const int OfficialStaffCount = 92;
        private const int ExistingStaffCount = 32;
        private const int NewStaffStartNumber = 33;
        private const string V8OfficialPackageFingerprint =
            "be7613e884b5ae18dc94e57abc0c941dccfb09486ae9fc5ff75acf4b0e4703af";
        private const string V8CurrentInventoryFingerprint =
            "38494f37574b54397201eb0c4f2120be4959275181ba44bb0b27bba6abf74eaa";

        private static readonly IReadOnlyDictionary<string, string> RoleClassNames =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "WAITER", "WaiterData" },
                    { "CLEANER", "CleanerData" },
                    { "CHEF", "ChefData" },
                    { "MANAGER", "ManagerData" },
                    { "CHEERLEADER", "MarketerData" },
                    { "GUARD", "GuardData" }
                });

        private static readonly IReadOnlyDictionary<int, string> StarGradeKeys =
            new ReadOnlyDictionary<int, string>(
                new Dictionary<int, string>
                {
                    { 2, "NORMAL" },
                    { 3, "RARE" },
                    { 4, "UNIQUE" },
                    { 5, "SPECIAL" }
                });

        private static readonly IReadOnlyDictionary<string, string> GradeRankNames =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "NORMAL", "Normal2" },
                    { "RARE", "Rare" },
                    { "UNIQUE", "Unique" },
                    { "SPECIAL", "Special" }
                });

        private static readonly IReadOnlyDictionary<string, string> LegacyV7SupportedSkillClasses =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "STAFF_SKILL01", "SpeedUpSkill" },
                    { "STAFF_SKILL03", "TouchAddCustomerButtonSkill" },
                    { "STAFF_SKILL04", "AssignedCookingSpeedUpSkill" },
                    { "STAFF_SKILL05", "FoodPriceUpSkill" },
                    { "STAFF_SKILL06", "FoodPaymentTipUpSkill" },
                    { "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill" },
                    { "STAFF_SKILL09", "GlobalCookingSpeedUpSkill" },
                    { "STAFF_SKILL10", "AllStaffMoveSpeedUpSkill" }
                });

        private static readonly IReadOnlyDictionary<string, string> V8SupportedSkillClasses =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "STAFF_SKILL01", "SpeedUpSkill" },
                    { "STAFF_SKILL03", "TouchAddCustomerButtonSkill" },
                    { "STAFF_SKILL04", "AssignedCookingSpeedUpSkill" },
                    { "STAFF_SKILL05", "FoodPriceUpSkill" },
                    { "STAFF_SKILL06", "FoodPaymentTipUpSkill" },
                    { "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill" },
                    { "STAFF_SKILL09", "GlobalRemainingCookingTimeReductionSkill" },
                    { "STAFF_SKILL10", "AllStaffMoveSpeedUpSkill" }
                });

        private static readonly PlanningProfile LegacyV7Profile = new PlanningProfile(
            StaffDataDryRunPlanSnapshot.LegacyV7PolicyVersion,
            "Final17",
            "Final17",
            LegacyV7SupportedSkillClasses,
            false,
            false);

        private static readonly PlanningProfile CanonicalV8Profile = new PlanningProfile(
            StaffDataDryRunPlanSnapshot.V8PolicyVersion,
            StaffOfficialDataPackageKeys.FinalStaff,
            "Final Staff",
            V8SupportedSkillClasses,
            true,
            true);

        private static readonly HashSet<string> IntentionalNoFrameIdleStaff =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF41", "STAFF42", "STAFF43", "STAFF44",
                "STAFF56", "STAFF57", "STAFF66", "STAFF67",
                "STAFF76", "STAFF77", "STAFF91", "STAFF92"
            };

        private static readonly HashSet<string> SharedBreathingIdleStaff =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "STAFF54", "STAFF55", "STAFF58", "STAFF64",
                "STAFF65", "STAFF68", "STAFF74", "STAFF75",
                "STAFF78", "STAFF84", "STAFF85", "STAFF86"
            };

        internal static bool TryBuildReadOnlyPlan(
            string officialDataFolder,
            out StaffDataDryRunPlanSnapshot plan,
            out IReadOnlyList<string> diagnostics)
        {
            plan = null;
            List<string> errors = new List<string>();

            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> officialDiagnostics;
            if (!StaffDataPackValidator.TryBuildReadOnlySnapshot(
                    officialDataFolder,
                    out official,
                    out officialDiagnostics))
            {
                AddChildDiagnostics("B1 official package", officialDiagnostics, errors);
            }

            StaffDataAssetInventorySnapshot current;
            IReadOnlyList<string> currentDiagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out current,
                    out currentDiagnostics))
            {
                AddChildDiagnostics("B2-1 current inventory", currentDiagnostics, errors);
            }

            StaffLegacySkinInventorySnapshot legacy;
            IReadOnlyList<string> legacyDiagnostics;
            if (!StaffLegacySkinInventoryReader.TryBuildReadOnlyInventory(
                    out legacy,
                    out legacyDiagnostics))
            {
                AddChildDiagnostics("B2-2 legacy inventory", legacyDiagnostics, errors);
            }

            if (errors.Count != 0 || official == null || current == null || legacy == null)
            {
                diagnostics = ToDiagnostics(errors);
                return false;
            }

            try
            {
                BuildContext context = BuildContext.Create(
                    official,
                    current,
                    legacy,
                    LegacyV7Profile,
                    errors);
                if (errors.Count == 0)
                {
                    List<StaffDataDryRunStaffPlan> staffPlans = BuildStaffPlans(context, errors);
                    List<StaffDataDryRunIssue> globalIssues = BuildGlobalIssues(context);
                    ValidateLockedBaseline(context, staffPlans, globalIssues, errors);
                    if (errors.Count == 0)
                    {
                        plan = new StaffDataDryRunPlanSnapshot(
                            official.PackageFingerprint,
                            current.InventoryFingerprint,
                            legacy.InventoryFingerprint,
                            staffPlans,
                            globalIssues);
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add("Dry Run plan construction failed: " + exception.Message);
            }

            diagnostics = ToDiagnostics(errors);
            if (errors.Count != 0 || plan == null)
            {
                plan = null;
                return false;
            }

            return true;
        }

        internal static bool TryBuildCanonicalV8ReadOnlyPlan(
            out StaffDataDryRunPlanSnapshot plan,
            out IReadOnlyList<string> diagnostics)
        {
            plan = null;
            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> officialDiagnostics;
            if (!StaffDataPackValidator.TryBuildCanonicalV8ReadOnlySnapshot(
                    out official,
                    out officialDiagnostics))
            {
                diagnostics = officialDiagnostics;
                return false;
            }

            IReadOnlyList<string> planDiagnostics;
            bool built = TryBuildV8ReadOnlyPlan(official, out plan, out planDiagnostics);
            List<string> combined = new List<string>();
            AddDiagnostics(officialDiagnostics, combined);
            AddDiagnostics(planDiagnostics, combined);
            diagnostics = new ReadOnlyCollection<string>(combined);
            return built;
        }

        internal static bool TryBuildV8ReadOnlyPlan(
            StaffOfficialDataPackageSnapshot official,
            out StaffDataDryRunPlanSnapshot plan,
            out IReadOnlyList<string> diagnostics)
        {
            plan = null;
            List<string> errors = new List<string>();
            if (official == null)
            {
                errors.Add("Canonical V8 official snapshot is null.");
                diagnostics = ToDiagnostics(errors);
                return false;
            }

            StaffDataAssetInventorySnapshot current;
            IReadOnlyList<string> currentDiagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out current,
                    out currentDiagnostics))
            {
                AddChildDiagnostics("B2-1 current inventory", currentDiagnostics, errors);
            }

            StaffLegacySkinInventorySnapshot legacy;
            IReadOnlyList<string> legacyDiagnostics;
            if (!StaffLegacySkinInventoryReader.TryBuildReadOnlyInventory(
                    out legacy,
                    out legacyDiagnostics))
            {
                AddChildDiagnostics("B2-2 legacy inventory", legacyDiagnostics, errors);
            }

            if (errors.Count != 0 || current == null || legacy == null)
            {
                diagnostics = ToDiagnostics(errors);
                return false;
            }

            if (official.PackageFingerprint != V8OfficialPackageFingerprint)
            {
                errors.Add("V8 official package fingerprint changed: "
                           + official.PackageFingerprint);
            }

            if (current.InventoryFingerprint != V8CurrentInventoryFingerprint)
            {
                errors.Add("V8 current inventory fingerprint changed: "
                           + current.InventoryFingerprint);
            }

            if (errors.Count != 0)
            {
                diagnostics = ToDiagnostics(errors);
                return false;
            }

            try
            {
                BuildContext context = BuildContext.Create(
                    official,
                    current,
                    legacy,
                    CanonicalV8Profile,
                    errors);
                if (errors.Count == 0)
                {
                    List<StaffDataDryRunStaffPlan> staffPlans = BuildStaffPlans(context, errors);
                    List<StaffDataDryRunIssue> globalIssues = BuildGlobalIssues(context);
                    ValidateLockedBaseline(context, staffPlans, globalIssues, errors);
                    if (errors.Count == 0)
                    {
                        plan = new StaffDataDryRunPlanSnapshot(
                            official.PackageFingerprint,
                            current.InventoryFingerprint,
                            legacy.InventoryFingerprint,
                            staffPlans,
                            globalIssues,
                            CanonicalV8Profile.PolicyVersion);
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add("V8 Dry Run plan construction failed: " + exception.Message);
            }

            diagnostics = ToDiagnostics(errors);
            if (errors.Count != 0 || plan == null)
            {
                plan = null;
                return false;
            }

            return true;
        }

        private static List<StaffDataDryRunStaffPlan> BuildStaffPlans(
            BuildContext context,
            List<string> errors)
        {
            List<StaffDataDryRunStaffPlan> plans = new List<StaffDataDryRunStaffPlan>();
            for (int number = 1; number <= OfficialStaffCount; number++)
            {
                string id = BuildStaffId(number);
                OfficialStaff official;
                if (!context.OfficialStaff.TryGetValue(id, out official))
                {
                    errors.Add("Official " + context.Profile.FinalStaffDisplayName
                               + " row is missing: " + id);
                    continue;
                }

                StaffDataDryRunStaffPlan staffPlan = number <= ExistingStaffCount
                    ? BuildExistingPlan(context, official)
                    : BuildNewPlan(context, official);
                plans.Add(staffPlan);
            }

            return plans;
        }

        private static StaffDataDryRunStaffPlan BuildExistingPlan(
            BuildContext context,
            OfficialStaff official)
        {
            StaffDataAssetSnapshot current = context.Current.StaffById[official.Id];
            List<StaffDataDryRunFieldPlan> fields = new List<StaffDataDryRunFieldPlan>();
            List<StaffDataDryRunIssue> issues = new List<StaffDataDryRunIssue>();

            AddComparableField(fields, "StaffData._name", current.Name, official.Name, false);
            AddComparableField(
                fields,
                "StaffData._description",
                current.Description,
                official.Description,
                false);
            AddComparableField(
                fields,
                "StaffData._rank",
                current.RankName,
                official.RankName,
                false);
            AddComparableField(
                fields,
                "StaffData._speed",
                FormatNumber(current.Speed),
                FormatNumber(official.BaseSpeed),
                true);
            AddIdentityPreservationFields(fields, current);

            bool roleValueMismatch = AddRoleLevelPlans(
                fields,
                current,
                official,
                context,
                StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING);
            bool costMismatch = AddUpgradeCostPlans(
                fields,
                current,
                official,
                context,
                StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING);
            AddFutureSystemPlans(fields, official, context.Profile);

            if (roleValueMismatch)
            {
                issues.Add(Issue(
                    "CURRENT_ROLE_VALUE_MISMATCH",
                    official.Id,
                    StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING,
                    "One or more current role level values differ from the official target.",
                    true,
                    false));
            }

            if (costMismatch)
            {
                issues.Add(Issue(
                    "CURRENT_UPGRADE_COST_MISMATCH",
                    official.Id,
                    StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING,
                    "Current upgrade cost or score values differ from the official target.",
                    true,
                    false));
            }

            if (official.Id == "STAFF02" && current.LevelCount == 6)
            {
                issues.Add(Issue(
                    "STAFF02_LEVEL6_SAVE_MIGRATION_REQUIRED",
                    official.Id,
                    StaffDryRunFieldDisposition.SAVE_MIGRATION_REQUIRED,
                    "The six-slot current array must become five slots and saved level 6 must clamp to level 5.",
                    false,
                    true));
            }

            StaffDataDryRunSkillPlan skill = BuildExistingSkillPlan(context, official, current, issues);
            StaffDataDryRunVisualPlan visual = BuildExistingVisualPlan(current);
            StaffDryRunReadiness readiness = DetermineReadiness(false, issues, official);

            return CreateStaffPlan(
                official,
                current,
                StaffDryRunAssetAction.UPDATE_EXISTING,
                readiness,
                current.AssetPath,
                true,
                fields,
                skill,
                visual,
                issues);
        }

        private static StaffDataDryRunStaffPlan BuildNewPlan(
            BuildContext context,
            OfficialStaff official)
        {
            StaffLegacySkinRowSnapshot legacy = context.Legacy.CandidateStaffId[official.Id];
            List<StaffDataDryRunFieldPlan> fields = new List<StaffDataDryRunFieldPlan>();
            List<StaffDataDryRunIssue> issues = new List<StaffDataDryRunIssue>();
            const StaffDryRunFieldDisposition create = StaffDryRunFieldDisposition.AUTO_CREATE_NEW;

            AddCreatedField(fields, "StaffData._id", official.Id, create);
            AddCreatedField(fields, "StaffData._name", official.Name, create);
            AddCreatedField(fields, "StaffData._description", official.Description, create);
            AddCreatedField(fields, "StaffData._rank", official.RankName, create);
            AddCreatedField(fields, "StaffData._speed", FormatNumber(official.BaseSpeed), create);
            AddRoleLevelPlans(fields, null, official, context, create);
            AddUpgradeCostPlans(fields, null, official, context, create);
            AddFutureSystemPlans(fields, official, context.Profile);
            AddLegacyMetadataPlans(fields, legacy);

            StaffDataDryRunSkillPlan skill = BuildNewSkillPlan(context, official, issues);
            StaffDataDryRunVisualPlan visual = BuildNewVisualPlan(context, official, legacy, issues);
            StaffDryRunReadiness readiness = DetermineReadiness(true, issues, official);

            return CreateStaffPlan(
                official,
                null,
                StaffDryRunAssetAction.CREATE_NEW,
                readiness,
                "Assets/Resources/StaffData/" + official.Id + ".asset",
                false,
                fields,
                skill,
                visual,
                issues);
        }

        private static StaffDataDryRunStaffPlan CreateStaffPlan(
            OfficialStaff official,
            StaffDataAssetSnapshot current,
            StaffDryRunAssetAction action,
            StaffDryRunReadiness readiness,
            string plannedPath,
            bool preserveGuid,
            List<StaffDataDryRunFieldPlan> fields,
            StaffDataDryRunSkillPlan skill,
            StaffDataDryRunVisualPlan visual,
            List<StaffDataDryRunIssue> issues)
        {
            return new StaffDataDryRunStaffPlan(
                official.Id,
                official.Number,
                action,
                readiness,
                official.RoleKey,
                official.RankName,
                RoleClassNames[official.RoleKey],
                current != null ? current.AssetPath : string.Empty,
                current != null ? current.AssetGuid : string.Empty,
                current != null ? current.ScriptGuid : string.Empty,
                plannedPath,
                preserveGuid,
                current != null ? current.Name : string.Empty,
                official.Name,
                current != null ? current.Description : string.Empty,
                official.Description,
                current != null ? current.RankName : string.Empty,
                current != null ? FormatNumber(current.Speed) : string.Empty,
                FormatNumber(official.BaseSpeed),
                official.GachaProbabilityRaw,
                official.AcquisitionCurrencyRaw,
                official.DuplicationTokenRaw,
                official.TokenPurchasePriceRaw,
                fields,
                skill,
                visual,
                issues);
        }

        private static StaffDataDryRunSkillPlan BuildExistingSkillPlan(
            BuildContext context,
            OfficialStaff official,
            StaffDataAssetSnapshot current,
            List<StaffDataDryRunIssue> issues)
        {
            string requiredClass;
            bool supported = context.Profile.SupportedSkillClasses.TryGetValue(
                official.SkillId,
                out requiredClass);
            requiredClass = supported ? requiredClass : string.Empty;
            bool classExists = supported && context.RuntimeSkillClassNames.Contains(requiredClass);
            bool matches = supported
                           && classExists
                           && string.Equals(
                               current.SkillConcreteTypeName,
                               requiredClass,
                               StringComparison.Ordinal);
            string currentGuid = ReferenceGuid(current.SkillReference);
            StaffSkillAssetSnapshot currentSkill;
            context.Current.TryGetSkill(currentGuid, out currentSkill);

            if (!matches)
            {
                issues.Add(Issue(
                    "EXISTING_SKILL_CLASS_MISMATCH",
                    official.Id,
                    StaffDryRunFieldDisposition.SKILL_CLASS_MIGRATION_REQUIRED,
                    "Current skill class '" + current.SkillConcreteTypeName
                    + "' does not match official skill " + official.SkillId
                    + (supported ? " ('" + requiredClass + "')." : " (runtime class is not defined)."),
                    false,
                    true));
            }

            if (supported && !classExists)
            {
                issues.Add(Issue(
                    "SUPPORTED_SKILL_RUNTIME_TYPE_MISSING",
                    official.Id,
                    StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED,
                    "Expected runtime class is not present: " + requiredClass,
                    false,
                    true));
            }

            StaffDataDryRunSkillEffectPlan effectPlan = BuildExistingSkillEffectPlan(
                context,
                official,
                current);
            return new StaffDataDryRunSkillPlan(
                official.SkillId,
                requiredClass,
                classExists,
                ReferencePath(current.SkillReference),
                currentGuid,
                current.SkillConcreteTypeName,
                ReferencePath(current.SkillReference),
                true,
                false,
                matches,
                currentSkill != null ? currentSkill.Description : string.Empty,
                official.SkillDescription,
                FormatNumber(current.SkillDuration),
                FormatNumber(official.SkillDuration),
                FormatNumber(current.SkillCooldown),
                FormatNumber(official.SkillCooldown),
                matches
                    ? StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                    : StaffDryRunFieldDisposition.SKILL_CLASS_MIGRATION_REQUIRED,
                effectPlan);
        }

        private static StaffDataDryRunSkillPlan BuildNewSkillPlan(
            BuildContext context,
            OfficialStaff official,
            List<StaffDataDryRunIssue> issues)
        {
            string requiredClass;
            bool supported = context.Profile.SupportedSkillClasses.TryGetValue(
                official.SkillId,
                out requiredClass);
            requiredClass = supported ? requiredClass : string.Empty;
            bool classExists = supported && context.RuntimeSkillClassNames.Contains(requiredClass);
            if (!supported || !classExists)
            {
                issues.Add(Issue(
                    "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED",
                    official.Id,
                    StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED,
                    "Official skill " + official.SkillId + " has no confirmed runtime class.",
                    false,
                    true));
            }

            string path = "Assets/Scripts/Datas/Staff/Skill/" + official.Id + "Skill.asset";
            StaffDataDryRunSkillEffectPlan effectPlan = BuildNewSkillEffectPlan(
                context,
                official);
            return new StaffDataDryRunSkillPlan(
                official.SkillId,
                requiredClass,
                classExists,
                string.Empty,
                string.Empty,
                string.Empty,
                path,
                false,
                true,
                supported && classExists,
                string.Empty,
                official.SkillDescription,
                string.Empty,
                FormatNumber(official.SkillDuration),
                string.Empty,
                FormatNumber(official.SkillCooldown),
                supported && classExists
                    ? StaffDryRunFieldDisposition.AUTO_CREATE_NEW
                    : StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED,
                effectPlan);
        }

        private static StaffDataDryRunSkillEffectPlan BuildExistingSkillEffectPlan(
            BuildContext context,
            OfficialStaff official,
            StaffDataAssetSnapshot current)
        {
            if (!context.Profile.SkillEffectPlanEnabled)
            {
                return null;
            }

            string currentFieldPath;
            string targetFieldPath;
            string targetValue;
            if (official.SkillId == "STAFF_SKILL04")
            {
                currentFieldPath = "_assignedCookingSpeedUpPercent";
                targetFieldPath = currentFieldPath;
                targetValue = "250";
            }
            else if (official.SkillId == "STAFF_SKILL09")
            {
                currentFieldPath = "_globalCookingSpeedUpPercent";
                targetFieldPath = "_remainingCookingTimeReductionPercent";
                targetValue = "50";
            }
            else
            {
                return null;
            }

            string assetPath = ReferencePath(current.SkillReference);
            StaffSkillEffectConfigurationSnapshot snapshot;
            string error;
            if (!StaffSkillEffectConfigurationReader.TryReadFloat(
                    assetPath,
                    currentFieldPath,
                    out snapshot,
                    out error))
            {
                context.Errors.Add("Skill effect configuration read failed for "
                                   + official.Id + ": " + error);
                return null;
            }

            bool fieldMatches = string.Equals(
                currentFieldPath,
                targetFieldPath,
                StringComparison.Ordinal);
            bool valueMatches = NumbersEqual(snapshot.NormalizedValue, targetValue);
            return new StaffDataDryRunSkillEffectPlan(
                currentFieldPath,
                targetFieldPath,
                snapshot.NormalizedValue,
                targetValue,
                fieldMatches,
                valueMatches,
                official.SkillId == "STAFF_SKILL04"
                    ? StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING
                    : StaffDryRunFieldDisposition.SKILL_CLASS_MIGRATION_REQUIRED,
                official.SkillId == "STAFF_SKILL04"
                    ? "V8 balance plan updates the existing assigned-cooking bonus from 150 to 250."
                    : "V8 redesign migrates the legacy global cooking-speed field to remaining-time reduction.");
        }

        private static StaffDataDryRunSkillEffectPlan BuildNewSkillEffectPlan(
            BuildContext context,
            OfficialStaff official)
        {
            if (!context.Profile.SkillEffectPlanEnabled)
            {
                return null;
            }

            if (official.SkillId == "STAFF_SKILL04")
            {
                return new StaffDataDryRunSkillEffectPlan(
                    string.Empty,
                    "_assignedCookingSpeedUpPercent",
                    string.Empty,
                    "250",
                    false,
                    false,
                    StaffDryRunFieldDisposition.AUTO_CREATE_NEW,
                    "Create the individual Skill04 asset with the V8 assigned-cooking bonus.");
            }

            if (official.SkillId == "STAFF_SKILL09")
            {
                return new StaffDataDryRunSkillEffectPlan(
                    string.Empty,
                    "_remainingCookingTimeReductionPercent",
                    string.Empty,
                    "50",
                    false,
                    false,
                    StaffDryRunFieldDisposition.AUTO_CREATE_NEW,
                    "Create after the V8 remaining-cooking-time reduction runtime class exists.");
            }

            return null;
        }

        private static StaffDataDryRunVisualPlan BuildExistingVisualPlan(
            StaffDataAssetSnapshot current)
        {
            List<string> all = new List<string>();
            List<string> idle = new List<string>();
            List<string> particles = new List<string>();
            AddCurrentReference(all, current.SpriteReference);
            AddCurrentReference(all, current.ThumbnailReference);
            AddCurrentReference(all, current.AnimatorControllerReference);
            AddCurrentReference(all, current.BackSpriteReference);
            AddCurrentReference(all, current.HandSpriteReference);
            AddCurrentReference(all, current.UiSpriteReference);
            AddCurrentReference(all, current.AnimationSpriteReference);
            for (int index = 0; index < current.IdleSpriteReferences.Count; index++)
            {
                AddCurrentReference(all, current.IdleSpriteReferences[index]);
                AddCurrentReference(idle, current.IdleSpriteReferences[index]);
            }

            for (int index = 0; index < current.ParticleSpriteReferences.Count; index++)
            {
                AddCurrentReference(all, current.ParticleSpriteReferences[index]);
                AddCurrentReference(particles, current.ParticleSpriteReferences[index]);
            }

            return new StaffDataDryRunVisualPlan(
                true,
                string.Empty,
                CurrentReferenceKey(current.SpriteReference),
                CurrentReferenceKey(current.ThumbnailReference),
                all,
                idle,
                particles,
                new string[0],
                CurrentReferenceKey(current.BackSpriteReference),
                CurrentReferenceKey(current.HandSpriteReference),
                CurrentReferenceKey(current.AnimationSpriteReference),
                StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                "PRESERVE_EXISTING_IDLE",
                "PRESERVE_EXISTING_ANIMATOR",
                IsUsable(current.SpriteReference),
                IsUsable(current.ThumbnailReference),
                !current.HasMissingRequiredReference,
                current.HasMissingRequiredReference,
                false);
        }

        private static StaffDataDryRunVisualPlan BuildNewVisualPlan(
            BuildContext context,
            OfficialStaff official,
            StaffLegacySkinRowSnapshot legacy,
            List<StaffDataDryRunIssue> issues)
        {
            List<string> all = new List<string>();
            List<string> idle = new List<string>();
            List<string> particles = new List<string>();
            List<string> mismatches = new List<string>();
            AddLegacyReference(all, legacy.MainSprite);
            AddLegacyReference(all, legacy.ThumbnailSprite);
            AddLegacyReference(all, legacy.ChefBackSprite);
            AddLegacyReference(all, legacy.ChefHandSprite);
            AddLegacyReference(all, legacy.CheerleaderAnimationSprite);
            for (int index = 0; index < legacy.IdleFrames.Count; index++)
            {
                AddLegacyReference(all, legacy.IdleFrames[index]);
                AddLegacyReference(idle, legacy.IdleFrames[index]);
            }

            for (int index = 0; index < legacy.CheerleaderParticleSprites.Count; index++)
            {
                AddLegacyReference(all, legacy.CheerleaderParticleSprites[index]);
                AddLegacyReference(particles, legacy.CheerleaderParticleSprites[index]);
            }

            bool namingMismatch = official.Id == "STAFF39";
            if (namingMismatch)
            {
                for (int index = 0; index < context.Legacy.IdleNamingMismatches.Count; index++)
                {
                    StaffLegacyVisualReferenceSnapshot mismatch =
                        context.Legacy.IdleNamingMismatches[index];
                    if (string.Equals(mismatch.ObjectName, "SKIN_STAFF07_04", StringComparison.Ordinal))
                    {
                        AddLegacyReference(mismatches, mismatch);
                    }
                }

                issues.Add(Issue(
                    "LEGACY_IDLE_NAMING_MISMATCH_REVIEW",
                    official.Id,
                    StaffDryRunFieldDisposition.LEGACY_METADATA_ONLY,
                    "SKIN_STAFF07_04 remains unchanged and is excluded from normal idle frames.",
                    true,
                    false));
            }

            StaffDryRunFieldDisposition idleDisposition;
            string idlePolicy;
            if (IntentionalNoFrameIdleStaff.Contains(official.Id))
            {
                idleDisposition = StaffDryRunFieldDisposition.INTENTIONAL_NO_FRAME_IDLE;
                idlePolicy = "INTENTIONAL_NO_FRAME_IDLE_CHEERLEADER";
            }
            else if (SharedBreathingIdleStaff.Contains(official.Id))
            {
                idleDisposition = StaffDryRunFieldDisposition.USE_BASE_SPRITE_SHARED_BREATHING;
                idlePolicy = "BASE_SPRITE_IDLE_WITH_SHARED_BREATHING";
            }
            else
            {
                idleDisposition = StaffDryRunFieldDisposition.USE_LEGACY_IDLE_FRAMES;
                idlePolicy = "USE_LEGACY_IDLE_FRAMES";
            }

            bool roleParts = official.RoleKey == "CHEF"
                ? legacy.HasChefParts
                : official.RoleKey != "CHEERLEADER" || legacy.HasCheerleaderParts;
            return new StaffDataDryRunVisualPlan(
                false,
                legacy.LegacySkinId,
                LegacyReferenceKey(legacy.MainSprite),
                LegacyReferenceKey(legacy.ThumbnailSprite),
                all,
                idle,
                particles,
                mismatches,
                LegacyReferenceKey(legacy.ChefBackSprite),
                LegacyReferenceKey(legacy.ChefHandSprite),
                LegacyReferenceKey(legacy.CheerleaderAnimationSprite),
                idleDisposition,
                StaffDryRunFieldDisposition.USE_SHARED_PREFAB_ANIMATOR,
                idlePolicy,
                "USE_SHARED_PREFAB_ANIMATOR",
                legacy.HasMainSprite,
                legacy.HasThumbnail,
                roleParts,
                legacy.HasMissingReference,
                namingMismatch);
        }

        private static bool AddRoleLevelPlans(
            List<StaffDataDryRunFieldPlan> fields,
            StaffDataAssetSnapshot current,
            OfficialStaff official,
            BuildContext context,
            StaffDryRunFieldDisposition normalDisposition)
        {
            bool mismatch = false;
            double[] growth = context.RoleGrowth[official.RoleKey];
            for (int levelIndex = 0; levelIndex < 5; levelIndex++)
            {
                int level = levelIndex + 1;
                StaffLevelAssetSnapshot currentLevel = GetCurrentLevel(current, levelIndex);
                if (official.RoleKey == "WAITER" || official.RoleKey == "CLEANER")
                {
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._addSpeed",
                        NullableNumber(currentLevel != null ? currentLevel.AddSpeed : null),
                        growth[levelIndex],
                        normalDisposition,
                        current != null);
                }

                if (official.RoleKey == "CLEANER")
                {
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._cleaningTime",
                        NullableNumber(currentLevel != null ? currentLevel.CleaningTime : null),
                        context.GetRoleBase(official, "CLEANING_TIME").BaseValue,
                        normalDisposition,
                        current != null);
                }
                else if (official.RoleKey == "CHEF")
                {
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._foodSpeedAddPercent",
                        NullableNumber(currentLevel != null ? currentLevel.FoodSpeedAddPercent : null),
                        context.GetRoleBase(official, "COOKING_EFFICIENCY").BaseValue,
                        normalDisposition,
                        current != null);
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._addSpeed",
                        NullableNumber(currentLevel != null ? currentLevel.AddSpeed : null),
                        growth[levelIndex],
                        normalDisposition,
                        current != null);
                }
                else if (official.RoleKey == "MANAGER")
                {
                    RoleBaseValue roleBase = context.GetRoleBase(official, "GUIDE_TIME");
                    double target = Math.Max(roleBase.MinimumValue, roleBase.BaseValue + growth[levelIndex]);
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._customerGuideTime",
                        NullableNumber(currentLevel != null ? currentLevel.CustomerGuideTime : null),
                        target,
                        normalDisposition,
                        current != null);
                }
                else if (official.RoleKey == "CHEERLEADER")
                {
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._marketingTime",
                        NullableNumber(currentLevel != null ? currentLevel.MarketingTime : null),
                        context.GetRoleBase(official, "AUTO_CALL_INTERVAL").BaseValue
                        + growth[levelIndex],
                        normalDisposition,
                        current != null);
                }
                else if (official.RoleKey == "GUARD")
                {
                    mismatch |= AddRoleNumberField(
                        fields,
                        "Levels[" + levelIndex + "]._actionTime",
                        NullableNumber(currentLevel != null ? currentLevel.ActionTime : null),
                        context.GetRoleBase(official, "TROUBLEMAKER_REMOVE_TIME").BaseValue
                        + growth[levelIndex],
                        normalDisposition,
                        current != null);
                }
            }

            return mismatch;
        }

        private static bool AddUpgradeCostPlans(
            List<StaffDataDryRunFieldPlan> fields,
            StaffDataAssetSnapshot current,
            OfficialStaff official,
            BuildContext context,
            StaffDryRunFieldDisposition normalDisposition)
        {
            UpgradeCost cost = context.UpgradeCosts[official.GradeKey];
            bool mismatch = false;
            for (int levelIndex = 0; levelIndex < 5; levelIndex++)
            {
                StaffLevelAssetSnapshot currentLevel = GetCurrentLevel(current, levelIndex);
                int score = levelIndex == 4 ? -1 : 0;
                int price = levelIndex == 4 ? -1 : cost.Amounts[levelIndex];
                string money = levelIndex == 4
                    ? (currentLevel != null ? currentLevel.UpgradeMoneyTypeName : "TERMINAL_REVIEW")
                    : RuntimeMoneyName(cost.Currencies[levelIndex]);
                string prefix = "Levels[" + levelIndex + "].";
                mismatch |= AddComparableNumberField(
                    fields,
                    prefix + "_upgradeMinScore",
                    currentLevel != null
                        ? currentLevel.UpgradeMinScore.ToString(CultureInfo.InvariantCulture)
                        : string.Empty,
                    score.ToString(CultureInfo.InvariantCulture),
                    normalDisposition,
                    current != null);
                mismatch |= AddComparableNumberField(
                    fields,
                    prefix + "_price",
                    currentLevel != null
                        ? currentLevel.UpgradePrice.ToString(CultureInfo.InvariantCulture)
                        : string.Empty,
                    price.ToString(CultureInfo.InvariantCulture),
                    normalDisposition,
                    current != null);

                bool moneyChanged = currentLevel != null
                                    && levelIndex < 4
                                    && !string.Equals(
                                        currentLevel.UpgradeMoneyTypeName,
                                        money,
                                        StringComparison.Ordinal);
                fields.Add(new StaffDataDryRunFieldPlan(
                    prefix + "_moneyType",
                    currentLevel != null ? currentLevel.UpgradeMoneyTypeName : string.Empty,
                    money,
                    levelIndex == 4
                        ? StaffDryRunFieldDisposition.PRESERVE_EXISTING
                        : normalDisposition,
                    current == null || moneyChanged,
                    levelIndex == 4
                        ? "TERMINAL_SENTINEL_MONEY_TYPE_REVIEW"
                        : "COIN maps to Gold and DIAMOND maps to Dia."));
                mismatch |= moneyChanged;
            }

            return mismatch;
        }

        private static void AddFutureSystemPlans(
            List<StaffDataDryRunFieldPlan> fields,
            OfficialStaff official,
            PlanningProfile profile)
        {
            string sourcePrefix = profile.FinalStaffKey + ".";
            fields.Add(new StaffDataDryRunFieldPlan(
                sourcePrefix + "GachaProbabilityRaw",
                string.Empty,
                official.GachaProbabilityRaw,
                StaffDryRunFieldDisposition.FUTURE_STAFF_ACQUISITION_SYSTEM_REQUIRED,
                false,
                "Do not map to current StaffData purchase fields."));
            fields.Add(new StaffDataDryRunFieldPlan(
                sourcePrefix + "AcquisitionCurrencyRaw",
                string.Empty,
                official.AcquisitionCurrencyRaw,
                StaffDryRunFieldDisposition.FUTURE_STAFF_ACQUISITION_SYSTEM_REQUIRED,
                false,
                "Raw official value is preserved for a future acquisition system."));
            fields.Add(new StaffDataDryRunFieldPlan(
                sourcePrefix + "DuplicationTokenRaw",
                string.Empty,
                official.DuplicationTokenRaw,
                StaffDryRunFieldDisposition.FUTURE_PANDA_TOKEN_SYSTEM_REQUIRED,
                false,
                "PandaToken remains separate from SkinToken and MoneyType."));
            fields.Add(new StaffDataDryRunFieldPlan(
                sourcePrefix + "TokenPurchasePriceRaw",
                string.Empty,
                official.TokenPurchasePriceRaw,
                StaffDryRunFieldDisposition.FUTURE_PANDA_TOKEN_SYSTEM_REQUIRED,
                false,
                "Do not map to StaffData._buyPrice."));
            fields.Add(new StaffDataDryRunFieldPlan(
                sourcePrefix + "PassiveRaw",
                string.Empty,
                official.PassiveRaw,
                StaffDryRunFieldDisposition.NO_RUNTIME_FIELD,
                false,
                "Official passive metadata is preserved without a current runtime field."));
            fields.Add(new StaffDataDryRunFieldPlan(
                sourcePrefix + "PassiveDescriptionRaw",
                string.Empty,
                official.PassiveDescriptionRaw,
                StaffDryRunFieldDisposition.NO_RUNTIME_FIELD,
                false,
                "Official passive metadata is preserved without a current runtime field."));
        }

        private static void AddLegacyMetadataPlans(
            List<StaffDataDryRunFieldPlan> fields,
            StaffLegacySkinRowSnapshot legacy)
        {
            fields.Add(LegacyField("Legacy.GachaProbabilityRaw", legacy.GachaProbabilityRaw));
            fields.Add(LegacyField("Legacy.PurchaseCurrencyRaw", legacy.PurchaseCurrencyRaw));
            fields.Add(LegacyField("Legacy.PurchasePriceRaw", legacy.PurchasePriceRaw));
            fields.Add(LegacyField("Legacy.UpgradeTypeId", legacy.LegacyUpgradeTypeId));
            fields.Add(LegacyField("Legacy.UpgradeValueRaw", legacy.LegacyUpgradeValueRaw));
            fields.Add(LegacyField("Legacy.DuplicationTokenRaw", legacy.LegacyDuplicationTokenRaw));
        }

        private static StaffDataDryRunFieldPlan LegacyField(string path, string value)
        {
            return new StaffDataDryRunFieldPlan(
                path,
                string.Empty,
                value,
                StaffDryRunFieldDisposition.LEGACY_METADATA_ONLY,
                false,
                "Legacy metadata is isolated and is not mapped to new Staff purchase or upgrade fields.");
        }

        private static void AddIdentityPreservationFields(
            List<StaffDataDryRunFieldPlan> fields,
            StaffDataAssetSnapshot current)
        {
            fields.Add(PreserveField("StaffData.AssetPath", current.AssetPath));
            fields.Add(PreserveField("StaffData.AssetGuid", current.AssetGuid));
            fields.Add(PreserveField("StaffData.ScriptGuid", current.ScriptGuid));
            fields.Add(PreserveField("StaffData._id", current.Id));
        }

        private static StaffDataDryRunFieldPlan PreserveField(string path, string value)
        {
            return new StaffDataDryRunFieldPlan(
                path,
                value,
                value,
                StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                false,
                "Existing identity/reference is preserved.");
        }

        private static void AddComparableField(
            List<StaffDataDryRunFieldPlan> fields,
            string path,
            string current,
            string target,
            bool numeric)
        {
            bool changed = numeric
                ? !NumbersEqual(current, target)
                : !string.Equals(current, target, StringComparison.Ordinal);
            fields.Add(new StaffDataDryRunFieldPlan(
                path,
                current,
                target,
                StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING,
                changed,
                string.Empty));
        }

        private static void AddCreatedField(
            List<StaffDataDryRunFieldPlan> fields,
            string path,
            string target,
            StaffDryRunFieldDisposition disposition)
        {
            fields.Add(new StaffDataDryRunFieldPlan(
                path,
                string.Empty,
                target,
                disposition,
                true,
                string.Empty));
        }

        private static bool AddRoleNumberField(
            List<StaffDataDryRunFieldPlan> fields,
            string path,
            string current,
            double target,
            StaffDryRunFieldDisposition disposition,
            bool compareCurrent)
        {
            string targetText = FormatNumber(target);
            bool changed = !compareCurrent || !NumbersEqual(current, targetText);
            fields.Add(new StaffDataDryRunFieldPlan(
                path,
                current,
                targetText,
                disposition,
                changed,
                string.Empty));
            return compareCurrent && changed;
        }

        private static bool AddComparableNumberField(
            List<StaffDataDryRunFieldPlan> fields,
            string path,
            string current,
            string target,
            StaffDryRunFieldDisposition disposition,
            bool compareCurrent)
        {
            bool changed = !compareCurrent || !NumbersEqual(current, target);
            fields.Add(new StaffDataDryRunFieldPlan(
                path,
                current,
                target,
                disposition,
                changed,
                string.Empty));
            return compareCurrent && changed;
        }

        private static StaffDryRunReadiness DetermineReadiness(
            bool isNew,
            List<StaffDataDryRunIssue> issues,
            OfficialStaff official)
        {
            bool skill = HasPrerequisite(issues, "SKILL");
            bool schema = HasPrerequisite(issues, "SCHEMA");
            bool migration = HasPrerequisite(issues, "MIGRATION");
            int prerequisiteKinds = (skill ? 1 : 0) + (schema ? 1 : 0) + (migration ? 1 : 0);
            if (prerequisiteKinds > 1)
            {
                return StaffDryRunReadiness.MULTIPLE_PREREQUISITES_REQUIRED;
            }

            if (skill)
            {
                return StaffDryRunReadiness.SKILL_CLASS_REQUIRED;
            }

            if (schema)
            {
                return StaffDryRunReadiness.RUNTIME_SCHEMA_REQUIRED;
            }

            if (migration)
            {
                return StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED;
            }

            if (isNew)
            {
                return StaffDryRunReadiness.ASSET_PLAN_READY;
            }

            bool hasFutureValues = !string.IsNullOrEmpty(official.AcquisitionCurrencyRaw)
                                   || !string.IsNullOrEmpty(official.DuplicationTokenRaw)
                                   || !string.IsNullOrEmpty(official.TokenPurchasePriceRaw);
            return hasFutureValues
                ? StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS
                : StaffDryRunReadiness.PLAN_READY;
        }

        private static bool HasPrerequisite(List<StaffDataDryRunIssue> issues, string token)
        {
            for (int index = 0; index < issues.Count; index++)
            {
                if (issues[index].IsPrerequisite
                    && issues[index].Code.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<StaffDataDryRunIssue> BuildGlobalIssues(BuildContext context)
        {
            List<StaffDataDryRunIssue> issues = new List<StaffDataDryRunIssue>
            {
                Issue(
                    "UPGRADE25_RUNTIME_MAPPING_MISMATCH",
                    string.Empty,
                    StaffDryRunFieldDisposition.RUNTIME_MAPPING_MISMATCH,
                    "Official UPGRADE25 is Waiter movement/permanent role performance; current runtime is connected to the Manager dictionary.",
                    true,
                    false),
                Issue(
                    "UPGRADE26_MAPPING_RECORDED",
                    string.Empty,
                    StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                    "UPGRADE26 official Chef movement-speed channel is recorded for future integration review.",
                    true,
                    false),
                Issue(
                    "UPGRADE27_MAPPING_RECORDED",
                    string.Empty,
                    StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                    "UPGRADE27 official Cleaner movement-speed channel is recorded for future integration review.",
                    true,
                    false),
                Issue(
                    "UPGRADE28_EXPEL_SPEED_RECORDED",
                    string.Empty,
                    StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                    "UPGRADE28 is Guard expel speed and remains distinct from movement speed.",
                    true,
                    false),
                Issue(
                    "TERMINAL_SENTINEL_MONEY_TYPE_REVIEW",
                    string.Empty,
                    StaffDryRunFieldDisposition.PRESERVE_EXISTING,
                    "Level 5 uses score -1 and price -1; terminal MoneyType is preserved for review.",
                    true,
                    false)
            };

            SnapshotTable gacha = context.Tables["GachaUpgradeType"];
            string[] ids = { "UPGRADE25", "UPGRADE26", "UPGRADE27", "UPGRADE28" };
            for (int idIndex = 0; idIndex < ids.Length; idIndex++)
            {
                IReadOnlyList<string> row;
                if (gacha.TryGetRowByFirstCell(ids[idIndex], out row))
                {
                    issues.Add(Issue(
                        ids[idIndex] + "_OFFICIAL_ROW_PRESERVED",
                        string.Empty,
                        StaffDryRunFieldDisposition.LEGACY_METADATA_ONLY,
                        JoinRow(row),
                        true,
                        false));
                }
            }

            return issues;
        }

        private static void ValidateLockedBaseline(
            BuildContext context,
            List<StaffDataDryRunStaffPlan> plans,
            List<StaffDataDryRunIssue> globalIssues,
            List<string> errors)
        {
            RequireCount("official staff plans", plans.Count, 92, errors);
            RequireCount("current staff assets", context.Current.Staff.Count, 32, errors);
            RequireCount("current skill assets", context.Current.Skills.Count, 32, errors);
            RequireCount("legacy skin rows", context.Legacy.LegacySkins.Count, 60, errors);

            Dictionary<string, int> roles = CountBy(plans, plan => plan.RoleKey);
            RequireCount("role WAITER", GetCount(roles, "WAITER"), 23, errors);
            RequireCount("role MANAGER", GetCount(roles, "MANAGER"), 13, errors);
            RequireCount("role CHEERLEADER", GetCount(roles, "CHEERLEADER"), 17, errors);
            RequireCount("role CHEF", GetCount(roles, "CHEF"), 23, errors);
            RequireCount("role CLEANER", GetCount(roles, "CLEANER"), 14, errors);
            RequireCount("role GUARD", GetCount(roles, "GUARD"), 2, errors);
            RequireSkill04Distribution(plans, errors);
            RequireSkill06Distribution(plans, errors);
            RequireSkill05Distribution(plans, errors);
            RequireRemainingSkillDistribution(plans, context.Profile, errors);

            Dictionary<string, int> ranks = CountBy(plans, plan => plan.TargetRankName);
            RequireCount("rank Normal2", GetCount(ranks, "Normal2"), 23, errors);
            RequireCount("rank Rare", GetCount(ranks, "Rare"), 26, errors);
            RequireCount("rank Unique", GetCount(ranks, "Unique"), 35, errors);
            RequireCount("rank Special", GetCount(ranks, "Special"), 8, errors);
            RequireCount("rank Normal1", GetCount(ranks, "Normal1"), 0, errors);

            List<StaffDataDryRunStaffPlan> existing = Filter(plans, StaffDryRunAssetAction.UPDATE_EXISTING);
            List<StaffDataDryRunStaffPlan> created = Filter(plans, StaffDryRunAssetAction.CREATE_NEW);
            RequireCount("existing update plans", existing.Count, 32, errors);
            RequireCount("new create plans", created.Count, 60, errors);
            RequireChangedFieldCount(existing, "StaffData._name", 2, "current name mismatch", errors);
            RequireChangedFieldCount(existing, "StaffData._description", 32, "current description mismatch", errors);
            RequireChangedFieldCount(existing, "StaffData._rank", 32, "current rank mismatch", errors);
            RequireChangedFieldCount(existing, "StaffData._speed", 29, "current speed mismatch", errors);
            RequireIssueCount(existing, "CURRENT_ROLE_VALUE_MISMATCH", 23, errors);
            RequireIssueCount(existing, "CURRENT_UPGRADE_COST_MISMATCH", 32, errors);
            RequireSkillNumberMismatch(
                existing,
                true,
                context.Profile.IsV8 ? 1 : 0,
                "current skill duration mismatch",
                errors);
            RequireSkillNumberMismatch(
                existing,
                false,
                context.Profile.IsV8 ? 1 : 0,
                "current skill cooldown mismatch",
                errors);
            RequireIssueCount(
                existing,
                "EXISTING_SKILL_CLASS_MISMATCH",
                context.Profile.IsV8 ? 1 : 0,
                errors);
            RequireIssueCount(existing, "CHEF_ADD_SPEED_SCHEMA_REQUIRED", 0, errors);
            RequireIssueCount(existing, "STAFF02_LEVEL6_SAVE_MIGRATION_REQUIRED", 1, errors);
            RequireReadinessCount(
                existing,
                StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS,
                context.Profile.IsV8 ? 30 : 31,
                errors);
            RequireReadinessCount(
                existing,
                StaffDryRunReadiness.SKILL_CLASS_REQUIRED,
                context.Profile.IsV8 ? 1 : 0,
                errors);
            RequireReadinessCount(existing, StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED, 1, errors);
            RequireReadinessCount(existing, StaffDryRunReadiness.RUNTIME_SCHEMA_REQUIRED, 0, errors);
            RequireReadinessCount(
                existing,
                StaffDryRunReadiness.MULTIPLE_PREREQUISITES_REQUIRED,
                0,
                errors);

            RequireIssueCount(
                created,
                "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED",
                context.Profile.IsV8 ? 1 : 0,
                errors);
            RequireIssueCount(created, "CHEF_ADD_SPEED_SCHEMA_REQUIRED", 0, errors);
            RequireNewPrerequisiteUnion(created, context.Profile.IsV8 ? 1 : 0, errors);
            RequireReadinessCount(
                created,
                StaffDryRunReadiness.ASSET_PLAN_READY,
                context.Profile.IsV8 ? 59 : 60,
                errors);
            RequireReadinessCount(
                created,
                StaffDryRunReadiness.SKILL_CLASS_REQUIRED,
                context.Profile.IsV8 ? 1 : 0,
                errors);
            RequireReadinessCount(created, StaffDryRunReadiness.RUNTIME_SCHEMA_REQUIRED, 0, errors);
            RequireReadinessCount(
                created,
                StaffDryRunReadiness.MULTIPLE_PREREQUISITES_REQUIRED,
                0,
                errors);
            RequireIdlePolicyCount(created, "USE_LEGACY_IDLE_FRAMES", 36, errors);
            RequireIdlePolicyCount(
                created,
                "INTENTIONAL_NO_FRAME_IDLE_CHEERLEADER",
                12,
                errors);
            RequireIdlePolicyCount(
                created,
                "BASE_SPRITE_IDLE_WITH_SHARED_BREATHING",
                12,
                errors);
            RequireAnimatorPolicyCount(created, "USE_SHARED_PREFAB_ANIMATOR", 60, errors);
            RequireNamingMismatchCount(created, 1, errors);
            RequireGlobalIssueCount(globalIssues, "UPGRADE25_RUNTIME_MAPPING_MISMATCH", 1, errors);
            if (context.Profile.IsV8)
            {
                RequireV8BalancePlans(plans, errors);
            }
        }

        private static void RequireSkill04Distribution(
            IReadOnlyList<StaffDataDryRunStaffPlan> plans,
            List<string> errors)
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
            bool allChef = true;
            for (int index = 0; index < plans.Count; index++)
            {
                StaffDataDryRunStaffPlan plan = plans[index];
                if (plan.SkillPlan.OfficialSkillId != "STAFF_SKILL04")
                {
                    continue;
                }

                allChef &= plan.RoleKey == "CHEF";
                if (plan.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(plan.StaffId);
                }
                else
                {
                    actualNew.Add(plan.StaffId);
                }
            }

            if (!actualExisting.SetEquals(expectedExisting)
                || !actualNew.SetEquals(expectedNew)
                || !allChef)
            {
                errors.Add(
                    "OFFICIAL_SKILL04_DISTRIBUTION_CHANGED: expected existing 4/new 9, all CHEF; actual existing "
                    + actualExisting.Count + "/new " + actualNew.Count
                    + ", all CHEF " + allChef + ".");
            }
        }

        private static void RequireV8BalancePlans(
            IReadOnlyList<StaffDataDryRunStaffPlan> plans,
            List<string> errors)
        {
            Dictionary<string, int> skillCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int existingChefPassiveChanges = 0;
            int newChefPassivePlans = 0;
            int skill03Unchanged = 0;
            int skill04ExistingEffects = 0;
            int skill04NewEffects = 0;
            int skill09ExistingRedesign = 0;
            int skill09NewPrerequisite = 0;
            int automaticFieldPlans = 0;
            int warnings = 0;
            HashSet<string> prerequisiteStaff = new HashSet<string>(StringComparer.Ordinal);

            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                StaffDataDryRunStaffPlan plan = plans[planIndex];
                string skillId = plan.SkillPlan.OfficialSkillId;
                int skillCount;
                skillCounts.TryGetValue(skillId, out skillCount);
                skillCounts[skillId] = skillCount + 1;
                automaticFieldPlans += plan.ChangedFieldCount;

                for (int issueIndex = 0; issueIndex < plan.Issues.Count; issueIndex++)
                {
                    warnings += plan.Issues[issueIndex].IsWarning ? 1 : 0;
                    if (plan.Issues[issueIndex].IsPrerequisite)
                    {
                        prerequisiteStaff.Add(plan.StaffId);
                    }
                }

                if (plan.RoleKey == "CHEF")
                {
                    for (int fieldIndex = 0; fieldIndex < plan.FieldPlans.Count; fieldIndex++)
                    {
                        StaffDataDryRunFieldPlan field = plan.FieldPlans[fieldIndex];
                        if (!field.FieldPath.EndsWith(
                                "._foodSpeedAddPercent",
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string expectedTarget = plan.TargetRankName == "Normal2"
                            ? "50"
                            : plan.TargetRankName == "Rare"
                                ? "70"
                                : plan.TargetRankName == "Unique" ? "100" : "200";
                        if (field.TargetValue != expectedTarget)
                        {
                            errors.Add("V8 Chef passive target changed: " + plan.StaffId
                                       + " | " + field.FieldPath + " | " + field.TargetValue);
                        }

                        if (plan.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                        {
                            existingChefPassiveChanges += field.IsChanged ? 1 : 0;
                        }
                        else
                        {
                            newChefPassivePlans++;
                        }
                    }
                }

                if (skillId == "STAFF_SKILL03"
                    && plan.SkillPlan.RequiredClassName == "TouchAddCustomerButtonSkill"
                    && plan.SkillPlan.RequiredClassExists
                    && plan.SkillPlan.ClassMatches
                    && plan.SkillPlan.EffectPlan == null)
                {
                    skill03Unchanged++;
                }

                StaffDataDryRunSkillEffectPlan effect = plan.SkillPlan.EffectPlan;
                if (skillId == "STAFF_SKILL04" && effect != null)
                {
                    bool common = effect.TargetFieldPath == "_assignedCookingSpeedUpPercent"
                                  && effect.TargetValue == "250";
                    if (plan.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                    {
                        bool existing = common
                                        && effect.CurrentFieldPath
                                        == "_assignedCookingSpeedUpPercent"
                                        && effect.CurrentValue == "150"
                                        && effect.FieldMatches
                                        && !effect.ValueMatches
                                        && effect.Disposition
                                        == StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING;
                        skill04ExistingEffects += existing ? 1 : 0;
                    }
                    else
                    {
                        bool created = common
                                       && string.IsNullOrEmpty(effect.CurrentFieldPath)
                                       && string.IsNullOrEmpty(effect.CurrentValue)
                                       && effect.Disposition
                                       == StaffDryRunFieldDisposition.AUTO_CREATE_NEW;
                        skill04NewEffects += created ? 1 : 0;
                    }
                }

                if (skillId == "STAFF_SKILL09" && effect != null)
                {
                    if (plan.StaffId == "STAFF27")
                    {
                        bool redesign = plan.SkillPlan.RequiredClassName
                                        == "GlobalRemainingCookingTimeReductionSkill"
                                        && !plan.SkillPlan.RequiredClassExists
                                        && !plan.SkillPlan.ClassMatches
                                        && effect.CurrentFieldPath == "_globalCookingSpeedUpPercent"
                                        && effect.TargetFieldPath
                                        == "_remainingCookingTimeReductionPercent"
                                        && effect.CurrentValue == "50"
                                        && effect.TargetValue == "50"
                                        && !effect.FieldMatches
                                        && effect.ValueMatches
                                        && plan.SkillPlan.TargetDuration == "1"
                                        && plan.SkillPlan.TargetCooldown == "240";
                        skill09ExistingRedesign += redesign ? 1 : 0;
                    }
                    else if (plan.StaffId == "STAFF68")
                    {
                        bool newPrerequisite = !plan.SkillPlan.RequiredClassExists
                                               && plan.Readiness
                                               == StaffDryRunReadiness.SKILL_CLASS_REQUIRED
                                               && effect.TargetFieldPath
                                               == "_remainingCookingTimeReductionPercent"
                                               && effect.TargetValue == "50"
                                               && plan.SkillPlan.TargetDuration == "1"
                                               && plan.SkillPlan.TargetCooldown == "240";
                        skill09NewPrerequisite += newPrerequisite ? 1 : 0;
                    }
                }
            }

            RequireCount("V8 Skill01 distribution", GetCount(skillCounts, "STAFF_SKILL01"), 34, errors);
            RequireCount("V8 Skill03 distribution", GetCount(skillCounts, "STAFF_SKILL03"), 15, errors);
            RequireCount("V8 Skill04 distribution", GetCount(skillCounts, "STAFF_SKILL04"), 13, errors);
            RequireCount("V8 Skill05 distribution", GetCount(skillCounts, "STAFF_SKILL05"), 10, errors);
            RequireCount("V8 Skill06 distribution", GetCount(skillCounts, "STAFF_SKILL06"), 3, errors);
            RequireCount("V8 Skill08 distribution", GetCount(skillCounts, "STAFF_SKILL08"), 10, errors);
            RequireCount("V8 Skill09 distribution", GetCount(skillCounts, "STAFF_SKILL09"), 2, errors);
            RequireCount("V8 Skill10 distribution", GetCount(skillCounts, "STAFF_SKILL10"), 5, errors);
            RequireCount("V8 reserved Skill02 distribution", GetCount(skillCounts, "STAFF_SKILL02"), 0, errors);
            RequireCount("V8 reserved Skill07 distribution", GetCount(skillCounts, "STAFF_SKILL07"), 0, errors);
            RequireCount("V8 existing Chef passive changes", existingChefPassiveChanges, 35, errors);
            RequireCount("V8 new Chef passive plans", newChefPassivePlans, 80, errors);
            RequireCount("SKILL03_UNCHANGED_PASS", skill03Unchanged, 15, errors);
            RequireCount("V8 Skill04 existing effect updates", skill04ExistingEffects, 4, errors);
            RequireCount("V8 Skill04 new effect plans", skill04NewEffects, 9, errors);
            RequireCount("V8 Skill09 existing redesign", skill09ExistingRedesign, 1, errors);
            RequireCount("V8 Skill09 new prerequisite", skill09NewPrerequisite, 1, errors);
            RequireCount("V8 automatic StaffData FieldPlans", automaticFieldPlans, 2146, errors);
            RequireCount("V8 Staff warning count", warnings, 56, errors);
            RequireCount("V8 prerequisite Staff", prerequisiteStaff.Count, 3, errors);
        }

        private static void RequireSkill06Distribution(
            IReadOnlyList<StaffDataDryRunStaffPlan> plans,
            List<string> errors)
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
            bool rolesMatch = true;
            for (int index = 0; index < plans.Count; index++)
            {
                StaffDataDryRunStaffPlan plan = plans[index];
                if (plan.SkillPlan.OfficialSkillId != "STAFF_SKILL06")
                {
                    continue;
                }

                if (plan.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(plan.StaffId);
                    rolesMatch &= plan.RoleKey == "MANAGER";
                }
                else
                {
                    actualNew.Add(plan.StaffId);
                    rolesMatch &= plan.RoleKey == "CLEANER";
                }
            }

            if (!actualExisting.SetEquals(expectedExisting)
                || !actualNew.SetEquals(expectedNew)
                || !rolesMatch)
            {
                errors.Add(
                    "OFFICIAL_SKILL06_DISTRIBUTION_CHANGED: expected existing STAFF09 MANAGER/new STAFF51, STAFF81 CLEANER; actual existing "
                    + actualExisting.Count + "/new " + actualNew.Count
                    + ", roles match " + rolesMatch + ".");
            }
        }

        private static void RequireSkill05Distribution(
            IReadOnlyList<StaffDataDryRunStaffPlan> plans,
            List<string> errors)
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
            bool mappingMatches = true;
            for (int index = 0; index < plans.Count; index++)
            {
                StaffDataDryRunStaffPlan plan = plans[index];
                if (plan.SkillPlan.OfficialSkillId != "STAFF_SKILL05")
                {
                    continue;
                }

                mappingMatches &= plan.SkillPlan.RequiredClassName == "FoodPriceUpSkill"
                                  && plan.SkillPlan.RequiredClassExists
                                  && plan.SkillPlan.ClassMatches;
                if (plan.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(plan.StaffId);
                    mappingMatches &= !plan.SkillPlan.CreateIndividualAsset;
                }
                else
                {
                    actualNew.Add(plan.StaffId);
                    mappingMatches &= plan.SkillPlan.CreateIndividualAsset
                                      && plan.SkillPlan.ClassDisposition
                                      == StaffDryRunFieldDisposition.AUTO_CREATE_NEW;
                }
            }

            if (!actualExisting.SetEquals(expectedExisting)
                || !actualNew.SetEquals(expectedNew)
                || !mappingMatches)
            {
                errors.Add(
                    "OFFICIAL_SKILL05_DISTRIBUTION_CHANGED: expected existing 6/new 4 with FoodPriceUpSkill mapping; actual existing "
                    + actualExisting.Count + "/new " + actualNew.Count
                    + ", mapping match " + mappingMatches + ".");
            }
        }

        private static void RequireRemainingSkillDistribution(
            IReadOnlyList<StaffDataDryRunStaffPlan> plans,
            PlanningProfile profile,
            List<string> errors)
        {
            RequireSupportedSkillDistribution(
                plans,
                "STAFF_SKILL08",
                "NormalCustomerMoveSpeedUpSkill",
                new[] { "STAFF06", "STAFF08", "STAFF10", "STAFF15", "STAFF30" },
                new[] { "STAFF38", "STAFF71", "STAFF72", "STAFF74", "STAFF84" },
                false,
                errors);
            RequireSupportedSkillDistribution(
                plans,
                "STAFF_SKILL09",
                profile.IsV8
                    ? "GlobalRemainingCookingTimeReductionSkill"
                    : "GlobalCookingSpeedUpSkill",
                new[] { "STAFF27" },
                new[] { "STAFF68" },
                profile.IsV8,
                errors);
            RequireSupportedSkillDistribution(
                plans,
                "STAFF_SKILL10",
                "AllStaffMoveSpeedUpSkill",
                new string[0],
                new[] { "STAFF39", "STAFF40", "STAFF64", "STAFF83", "STAFF90" },
                false,
                errors);
        }

        private static void RequireSupportedSkillDistribution(
            IReadOnlyList<StaffDataDryRunStaffPlan> plans,
            string skillId,
            string runtimeClassName,
            IEnumerable<string> expectedExistingIds,
            IEnumerable<string> expectedNewIds,
            bool expectedRuntimeClassMissing,
            List<string> errors)
        {
            HashSet<string> expectedExisting = new HashSet<string>(expectedExistingIds, StringComparer.Ordinal);
            HashSet<string> expectedNew = new HashSet<string>(expectedNewIds, StringComparer.Ordinal);
            HashSet<string> actualExisting = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> actualNew = new HashSet<string>(StringComparer.Ordinal);
            bool mappingMatches = true;
            for (int index = 0; index < plans.Count; index++)
            {
                StaffDataDryRunStaffPlan plan = plans[index];
                if (plan.SkillPlan.OfficialSkillId != skillId)
                {
                    continue;
                }

                mappingMatches &= plan.SkillPlan.RequiredClassName == runtimeClassName;
                if (expectedRuntimeClassMissing)
                {
                    mappingMatches &= !plan.SkillPlan.RequiredClassExists
                                      && !plan.SkillPlan.ClassMatches;
                }
                else
                {
                    mappingMatches &= plan.SkillPlan.RequiredClassExists
                                      && plan.SkillPlan.ClassMatches;
                }
                if (plan.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    actualExisting.Add(plan.StaffId);
                    mappingMatches &= !plan.SkillPlan.CreateIndividualAsset;
                }
                else
                {
                    actualNew.Add(plan.StaffId);
                    mappingMatches &= plan.SkillPlan.CreateIndividualAsset
                                      && plan.SkillPlan.ClassDisposition
                                      == (expectedRuntimeClassMissing
                                          ? StaffDryRunFieldDisposition.SKILL_CLASS_IMPLEMENTATION_REQUIRED
                                          : StaffDryRunFieldDisposition.AUTO_CREATE_NEW);
                }
            }

            if (!actualExisting.SetEquals(expectedExisting)
                || !actualNew.SetEquals(expectedNew)
                || !mappingMatches)
            {
                errors.Add(
                    "OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: " + skillId
                    + " expected existing " + expectedExisting.Count + "/new " + expectedNew.Count
                    + "; actual existing " + actualExisting.Count + "/new " + actualNew.Count
                    + ", mapping match " + mappingMatches + ".");
            }
        }

        private static void RequireChangedFieldCount(
            List<StaffDataDryRunStaffPlan> plans,
            string fieldPath,
            int expected,
            string label,
            List<string> errors)
        {
            int count = 0;
            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                for (int fieldIndex = 0; fieldIndex < plans[planIndex].FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = plans[planIndex].FieldPlans[fieldIndex];
                    if (field.FieldPath == fieldPath && field.IsChanged)
                    {
                        count++;
                    }
                }
            }

            RequireCount(label, count, expected, errors);
        }

        private static void RequireSkillNumberMismatch(
            List<StaffDataDryRunStaffPlan> plans,
            bool duration,
            int expected,
            string label,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                StaffDataDryRunSkillPlan skill = plans[index].SkillPlan;
                if (!NumbersEqual(
                        duration ? skill.CurrentDuration : skill.CurrentCooldown,
                        duration ? skill.TargetDuration : skill.TargetCooldown))
                {
                    count++;
                }
            }

            RequireCount(label, count, expected, errors);
        }

        private static void RequireIssueCount(
            List<StaffDataDryRunStaffPlan> plans,
            string code,
            int expected,
            List<string> errors)
        {
            int count = 0;
            List<string> staffIds = new List<string>();
            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                for (int issueIndex = 0; issueIndex < plans[planIndex].Issues.Count; issueIndex++)
                {
                    if (plans[planIndex].Issues[issueIndex].Code == code)
                    {
                        count++;
                        staffIds.Add(plans[planIndex].StaffId);
                    }
                }
            }

            if (count != expected)
            {
                staffIds.Sort(StringComparer.Ordinal);
                errors.Add(
                    "CURRENT_BASELINE_CHANGED: " + code + " expected "
                    + expected.ToString(CultureInfo.InvariantCulture) + ", actual "
                    + count.ToString(CultureInfo.InvariantCulture) + ". Staff IDs: "
                    + string.Join(", ", staffIds));
            }
        }

        private static void RequireIssueStaffIds(
            List<StaffDataDryRunStaffPlan> plans,
            string code,
            IReadOnlyCollection<string> expectedIds,
            List<string> errors)
        {
            HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
            for (int planIndex = 0; planIndex < plans.Count; planIndex++)
            {
                for (int issueIndex = 0; issueIndex < plans[planIndex].Issues.Count; issueIndex++)
                {
                    if (plans[planIndex].Issues[issueIndex].Code == code)
                    {
                        actual.Add(plans[planIndex].StaffId);
                    }
                }
            }

            if (!actual.SetEquals(expectedIds))
            {
                List<string> actualIds = new List<string>(actual);
                actualIds.Sort(StringComparer.Ordinal);
                errors.Add(
                    "CURRENT_BASELINE_CHANGED: " + code
                    + " Staff IDs: " + string.Join(", ", actualIds));
            }
        }

        private static void RequireGlobalIssueCount(
            List<StaffDataDryRunIssue> issues,
            string code,
            int expected,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < issues.Count; index++)
            {
                count += issues[index].Code == code ? 1 : 0;
            }

            RequireCount(code, count, expected, errors);
        }

        private static void RequireNewPrerequisiteUnion(
            List<StaffDataDryRunStaffPlan> plans,
            int expected,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                count += plans[index].Readiness == StaffDryRunReadiness.SKILL_CLASS_REQUIRED
                         || plans[index].Readiness == StaffDryRunReadiness.RUNTIME_SCHEMA_REQUIRED
                         || plans[index].Readiness == StaffDryRunReadiness.MULTIPLE_PREREQUISITES_REQUIRED
                    ? 1
                    : 0;
            }

            RequireCount("new prerequisite union", count, expected, errors);
        }

        private static void RequireReadinessCount(
            List<StaffDataDryRunStaffPlan> plans,
            StaffDryRunReadiness readiness,
            int expected,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                count += plans[index].Readiness == readiness ? 1 : 0;
            }

            RequireCount("readiness " + readiness, count, expected, errors);
        }

        private static void RequireIdlePolicyCount(
            List<StaffDataDryRunStaffPlan> plans,
            string policy,
            int expected,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                count += plans[index].VisualPlan.IdlePolicyCode == policy ? 1 : 0;
            }

            RequireCount("idle policy " + policy, count, expected, errors);
        }

        private static void RequireAnimatorPolicyCount(
            List<StaffDataDryRunStaffPlan> plans,
            string policy,
            int expected,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                count += plans[index].VisualPlan.AnimatorPolicyCode == policy ? 1 : 0;
            }

            RequireCount("animator policy " + policy, count, expected, errors);
        }

        private static void RequireNamingMismatchCount(
            List<StaffDataDryRunStaffPlan> plans,
            int expected,
            List<string> errors)
        {
            int count = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                count += plans[index].VisualPlan.NamingMismatchReviewRequired ? 1 : 0;
            }

            RequireCount("legacy naming mismatch review", count, expected, errors);
        }

        private static Dictionary<string, int> CountBy(
            List<StaffDataDryRunStaffPlan> plans,
            Func<StaffDataDryRunStaffPlan, string> selector)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < plans.Count; index++)
            {
                string key = selector(plans[index]);
                int count;
                counts.TryGetValue(key, out count);
                counts[key] = count + 1;
            }

            return counts;
        }

        private static List<StaffDataDryRunStaffPlan> Filter(
            List<StaffDataDryRunStaffPlan> plans,
            StaffDryRunAssetAction action)
        {
            List<StaffDataDryRunStaffPlan> result = new List<StaffDataDryRunStaffPlan>();
            for (int index = 0; index < plans.Count; index++)
            {
                if (plans[index].AssetAction == action)
                {
                    result.Add(plans[index]);
                }
            }

            return result;
        }

        private static void RequireCount(
            string label,
            int actual,
            int expected,
            List<string> errors)
        {
            if (actual != expected)
            {
                errors.Add(
                    "CURRENT_BASELINE_CHANGED: " + label + " expected "
                    + expected.ToString(CultureInfo.InvariantCulture) + ", actual "
                    + actual.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static int GetCount(Dictionary<string, int> counts, string key)
        {
            int count;
            return counts.TryGetValue(key, out count) ? count : 0;
        }

        private static StaffLevelAssetSnapshot GetCurrentLevel(
            StaffDataAssetSnapshot current,
            int levelIndex)
        {
            return current != null && levelIndex < current.Levels.Count
                ? current.Levels[levelIndex]
                : null;
        }

        private static bool NumbersEqual(string left, string right)
        {
            double leftValue;
            double rightValue;
            return double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out leftValue)
                   && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out rightValue)
                   && Math.Abs(leftValue - rightValue) <= 0.0001d;
        }

        private static string NullableNumber(float? value)
        {
            return value.HasValue ? FormatNumber(value.Value) : string.Empty;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static string RuntimeMoneyName(string officialCurrency)
        {
            if (officialCurrency == "COIN")
            {
                return "Gold";
            }

            if (officialCurrency == "DIAMOND")
            {
                return "Dia";
            }

            return officialCurrency;
        }

        private static bool IsUsable(StaffAssetReferenceSnapshot reference)
        {
            return reference != null && reference.IsAssigned && !reference.IsMissing;
        }

        private static string ReferencePath(StaffAssetReferenceSnapshot reference)
        {
            return reference != null ? reference.AssetPath : string.Empty;
        }

        private static string ReferenceGuid(StaffAssetReferenceSnapshot reference)
        {
            return reference != null ? reference.AssetGuid : string.Empty;
        }

        private static void AddCurrentReference(
            List<string> target,
            StaffAssetReferenceSnapshot reference)
        {
            if (reference != null && reference.IsAssigned)
            {
                target.Add(CurrentReferenceKey(reference));
            }
        }

        private static string CurrentReferenceKey(StaffAssetReferenceSnapshot reference)
        {
            return reference == null
                ? string.Empty
                : reference.FieldPath + "|" + reference.AssetPath + "|" + reference.AssetGuid
                  + "|" + reference.ObjectName + "|" + reference.ObjectTypeName
                  + "|missing=" + (reference.IsMissing ? "1" : "0");
        }

        private static void AddLegacyReference(
            List<string> target,
            StaffLegacyVisualReferenceSnapshot reference)
        {
            if (reference != null && reference.IsAssigned)
            {
                target.Add(LegacyReferenceKey(reference));
            }
        }

        private static string LegacyReferenceKey(StaffLegacyVisualReferenceSnapshot reference)
        {
            return reference == null
                ? string.Empty
                : reference.Category + "|" + reference.AssetPath + "|" + reference.AssetGuid
                  + "|" + reference.LocalFileId.ToString(CultureInfo.InvariantCulture)
                  + "|" + reference.ObjectName + "|" + reference.ObjectTypeName
                  + "|frame=" + reference.FrameIndex.ToString(CultureInfo.InvariantCulture)
                  + "|missing=" + (reference.IsMissing ? "1" : "0");
        }

        private static StaffDataDryRunIssue Issue(
            string code,
            string staffId,
            StaffDryRunFieldDisposition disposition,
            string message,
            bool warning,
            bool prerequisite)
        {
            return new StaffDataDryRunIssue(
                code,
                staffId,
                disposition,
                message,
                warning,
                prerequisite);
        }

        private static string BuildStaffId(int number)
        {
            return "STAFF" + number.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string JoinRow(IReadOnlyList<string> row)
        {
            string result = string.Empty;
            for (int index = 0; index < row.Count; index++)
            {
                result += (index == 0 ? string.Empty : " | ") + (row[index] ?? string.Empty);
            }

            return result;
        }

        private static void AddChildDiagnostics(
            string label,
            IReadOnlyList<string> child,
            List<string> errors)
        {
            if (child == null || child.Count == 0)
            {
                errors.Add(label + " failed without diagnostics.");
                return;
            }

            for (int index = 0; index < child.Count; index++)
            {
                errors.Add(label + ": " + child[index]);
            }
        }

        private static void AddDiagnostics(
            IReadOnlyList<string> source,
            List<string> target)
        {
            if (source == null)
            {
                return;
            }

            for (int index = 0; index < source.Count; index++)
            {
                target.Add(source[index]);
            }
        }

        private static IReadOnlyList<string> ToDiagnostics(List<string> errors)
        {
            List<string> result = new List<string>();
            for (int index = 0; index < errors.Count; index++)
            {
                result.Add("ERROR: " + errors[index]);
            }

            return new ReadOnlyCollection<string>(result);
        }

        private sealed class PlanningProfile
        {
            internal readonly string PolicyVersion;
            internal readonly string FinalStaffKey;
            internal readonly string FinalStaffDisplayName;
            internal readonly IReadOnlyDictionary<string, string> SupportedSkillClasses;
            internal readonly bool IsV8;
            internal readonly bool SkillEffectPlanEnabled;

            internal PlanningProfile(
                string policyVersion,
                string finalStaffKey,
                string finalStaffDisplayName,
                IReadOnlyDictionary<string, string> supportedSkillClasses,
                bool isV8,
                bool skillEffectPlanEnabled)
            {
                PolicyVersion = policyVersion;
                FinalStaffKey = finalStaffKey;
                FinalStaffDisplayName = finalStaffDisplayName;
                SupportedSkillClasses = supportedSkillClasses;
                IsV8 = isV8;
                SkillEffectPlanEnabled = skillEffectPlanEnabled;
            }
        }

        private sealed class BuildContext
        {
            internal readonly StaffOfficialDataPackageSnapshot Official;
            internal readonly StaffDataAssetInventorySnapshot Current;
            internal readonly StaffLegacySkinInventorySnapshot Legacy;
            internal readonly Dictionary<string, SnapshotTable> Tables;
            internal readonly Dictionary<string, OfficialStaff> OfficialStaff;
            internal readonly Dictionary<string, RoleBaseValue> RoleBases;
            internal readonly Dictionary<string, double[]> RoleGrowth;
            internal readonly Dictionary<string, UpgradeCost> UpgradeCosts;
            internal readonly HashSet<string> RuntimeSkillClassNames;
            internal readonly PlanningProfile Profile;
            internal readonly List<string> Errors;

            private BuildContext(
                StaffOfficialDataPackageSnapshot official,
                StaffDataAssetInventorySnapshot current,
                StaffLegacySkinInventorySnapshot legacy,
                Dictionary<string, SnapshotTable> tables,
                Dictionary<string, OfficialStaff> officialStaff,
                Dictionary<string, RoleBaseValue> roleBases,
                Dictionary<string, double[]> roleGrowth,
                Dictionary<string, UpgradeCost> upgradeCosts,
                HashSet<string> runtimeSkillClassNames,
                PlanningProfile profile,
                List<string> errors)
            {
                Official = official;
                Current = current;
                Legacy = legacy;
                Tables = tables;
                OfficialStaff = officialStaff;
                RoleBases = roleBases;
                RoleGrowth = roleGrowth;
                UpgradeCosts = upgradeCosts;
                RuntimeSkillClassNames = runtimeSkillClassNames;
                Profile = profile;
                Errors = errors;
            }

            internal RoleBaseValue GetRoleBase(OfficialStaff staff, string stat)
            {
                return RoleBases[staff.RoleKey + "|" + staff.GradeKey + "|" + stat];
            }

            internal static BuildContext Create(
                StaffOfficialDataPackageSnapshot official,
                StaffDataAssetInventorySnapshot current,
                StaffLegacySkinInventorySnapshot legacy,
                PlanningProfile profile,
                List<string> errors)
            {
                Dictionary<string, SnapshotTable> tables = BuildTables(official, profile, errors);
                Dictionary<string, string> roleNames = BuildRoleNames(tables, errors);
                Dictionary<string, string> gradeNames = BuildGradeNames(tables, errors);
                Dictionary<string, string> skillDescriptions = BuildSkillDescriptions(tables, errors);
                Dictionary<string, OfficialStaff> staff = BuildOfficialStaff(
                    tables,
                    roleNames,
                    gradeNames,
                    skillDescriptions,
                    profile,
                    errors);
                Dictionary<string, RoleBaseValue> bases = BuildRoleBases(tables, errors);
                Dictionary<string, double[]> growth = BuildRoleGrowth(tables, errors);
                Dictionary<string, UpgradeCost> costs = BuildUpgradeCosts(tables, errors);
                ValidateInventoryLinks(current, legacy, staff, errors);

                HashSet<string> skillClassNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (Type type in TypeCache.GetTypesDerivedFrom<SkillBase>())
                {
                    if (type != null && !type.IsAbstract)
                    {
                        skillClassNames.Add(type.Name);
                    }
                }

                return new BuildContext(
                    official,
                    current,
                    legacy,
                    tables,
                    staff,
                    bases,
                    growth,
                    costs,
                    skillClassNames,
                    profile,
                    errors);
            }

            private static Dictionary<string, SnapshotTable> BuildTables(
                StaffOfficialDataPackageSnapshot official,
                PlanningProfile profile,
                List<string> errors)
            {
                string[] required =
                {
                    profile.FinalStaffKey, "RoleBase", "RoleGrowth", "LevelRule", "CostRule",
                    "Summary", "Policy", "SkillType", "GachaUpgradeType"
                };
                Dictionary<string, SnapshotTable> result =
                    new Dictionary<string, SnapshotTable>(StringComparer.Ordinal);
                RequireCount("official snapshot files", official.OfficialFileCount, 9, errors);
                for (int index = 0; index < required.Length; index++)
                {
                    StaffOfficialFileSnapshot file;
                    if (!official.TryGetFile(required[index], out file))
                    {
                        errors.Add("Official snapshot file is missing: " + required[index]);
                        continue;
                    }

                    SnapshotTable table = new SnapshotTable(file);
                    if (table.Headers.Count == 0)
                    {
                        errors.Add("Official snapshot has no headers: " + required[index]);
                    }

                    for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                    {
                        if (table.Rows[rowIndex].Count != table.Headers.Count)
                        {
                            errors.Add(
                                "Official snapshot column count changed: " + required[index]
                                + " row " + (rowIndex + 2).ToString(CultureInfo.InvariantCulture));
                        }
                    }

                    result.Add(required[index], table);
                }

                return result;
            }

            private static Dictionary<string, string> BuildRoleNames(
                Dictionary<string, SnapshotTable> tables,
                List<string> errors)
            {
                Dictionary<string, string> result =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue("RoleGrowth", out table))
                {
                    return result;
                }

                for (int index = 0; index < table.Rows.Count; index++)
                {
                    IReadOnlyList<string> row = table.Rows[index];
                    if (row.Count < 2 || !RoleClassNames.ContainsKey(row[0].Trim()))
                    {
                        errors.Add("RoleGrowth row cannot define an official role name.");
                        continue;
                    }

                    result[row[1].Trim()] = row[0].Trim();
                }

                RequireCount("official role name mappings", result.Count, 6, errors);
                return result;
            }

            private static Dictionary<string, string> BuildGradeNames(
                Dictionary<string, SnapshotTable> tables,
                List<string> errors)
            {
                Dictionary<string, string> result =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue("CostRule", out table))
                {
                    return result;
                }

                for (int index = 0; index < table.Rows.Count; index++)
                {
                    IReadOnlyList<string> row = table.Rows[index];
                    if (row.Count < 2 || !GradeRankNames.ContainsKey(row[0].Trim()))
                    {
                        errors.Add("CostRule row cannot define an official grade name.");
                        continue;
                    }

                    result[row[1].Trim()] = row[0].Trim();
                }

                RequireCount("official grade name mappings", result.Count, 4, errors);
                return result;
            }

            private static Dictionary<string, string> BuildSkillDescriptions(
                Dictionary<string, SnapshotTable> tables,
                List<string> errors)
            {
                Dictionary<string, string> result =
                    new Dictionary<string, string>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue("SkillType", out table))
                {
                    return result;
                }

                for (int index = 0; index < table.Rows.Count; index++)
                {
                    IReadOnlyList<string> row = table.Rows[index];
                    if (row.Count < 2)
                    {
                        errors.Add("SkillType row has fewer than two columns.");
                        continue;
                    }

                    result[row[0].Trim()] = row[1];
                }

                RequireCount("official skill definitions", result.Count, 10, errors);
                return result;
            }

            private static Dictionary<string, OfficialStaff> BuildOfficialStaff(
                Dictionary<string, SnapshotTable> tables,
                Dictionary<string, string> roleNames,
                Dictionary<string, string> gradeNames,
                Dictionary<string, string> skillDescriptions,
                PlanningProfile profile,
                List<string> errors)
            {
                Dictionary<string, OfficialStaff> result =
                    new Dictionary<string, OfficialStaff>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue(profile.FinalStaffKey, out table))
                {
                    return result;
                }

                for (int index = 0; index < table.Rows.Count; index++)
                {
                    IReadOnlyList<string> row = table.Rows[index];
                    if (row.Count < 17)
                    {
                        errors.Add(profile.FinalStaffDisplayName + " row has fewer than 17 columns.");
                        continue;
                    }

                    string id = row[0].Trim();
                    int number;
                    int stars;
                    double speed;
                    double duration;
                    double cooldown;
                    string role;
                    string gradeFromName;
                    string gradeFromStars;
                    if (!TryParseStaffNumber(id, out number)
                        || !int.TryParse(row[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out stars)
                        || !double.TryParse(row[9], NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
                        || !TryParseSeconds(row[12], out duration)
                        || !TryParseSeconds(row[13], out cooldown)
                        || !roleNames.TryGetValue(row[6].Trim(), out role)
                        || !gradeNames.TryGetValue(row[5].Trim(), out gradeFromName)
                        || !StarGradeKeys.TryGetValue(stars, out gradeFromStars)
                        || gradeFromName != gradeFromStars)
                    {
                        errors.Add(profile.FinalStaffDisplayName
                                   + " row cannot be interpreted consistently: " + id);
                        continue;
                    }

                    string skillId = row[10].Trim();
                    string lockedSkillDescription;
                    if (!skillDescriptions.TryGetValue(skillId, out lockedSkillDescription))
                    {
                        errors.Add(profile.FinalStaffDisplayName
                                   + " references an undefined official skill: " + skillId);
                        continue;
                    }

                    if (result.ContainsKey(id))
                    {
                        errors.Add(profile.FinalStaffDisplayName + " contains a duplicate ID: " + id);
                        continue;
                    }

                    result.Add(
                        id,
                        new OfficialStaff(
                            id,
                            number,
                            row[1],
                            row[2],
                            row[3],
                            stars,
                            gradeFromStars,
                            GradeRankNames[gradeFromStars],
                            role,
                            row[7],
                            row[8],
                            speed,
                            skillId,
                            skillId == "STAFF_SKILL04" || skillId == "STAFF_SKILL05" || skillId == "STAFF_SKILL06"
                            || skillId == "STAFF_SKILL08" || skillId == "STAFF_SKILL09" || skillId == "STAFF_SKILL10"
                                ? lockedSkillDescription
                                : row[11],
                            duration,
                            cooldown,
                            row[14],
                            row[15],
                            row[16]));
                }

                RequireCount(
                    profile.FinalStaffDisplayName + " interpreted staff rows",
                    result.Count,
                    92,
                    errors);
                return result;
            }

            private static Dictionary<string, RoleBaseValue> BuildRoleBases(
                Dictionary<string, SnapshotTable> tables,
                List<string> errors)
            {
                Dictionary<string, RoleBaseValue> result =
                    new Dictionary<string, RoleBaseValue>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue("RoleBase", out table))
                {
                    return result;
                }

                for (int index = 0; index < table.Rows.Count; index++)
                {
                    IReadOnlyList<string> row = table.Rows[index];
                    double baseValue;
                    double minimumValue;
                    if (row.Count < 7
                        || !double.TryParse(row[3], NumberStyles.Float, CultureInfo.InvariantCulture, out baseValue)
                        || !double.TryParse(row[5], NumberStyles.Float, CultureInfo.InvariantCulture, out minimumValue))
                    {
                        errors.Add("RoleBase row cannot be interpreted.");
                        continue;
                    }

                    string key = row[0].Trim() + "|" + row[1].Trim() + "|" + row[2].Trim();
                    result[key] = new RoleBaseValue(baseValue, minimumValue);
                }

                return result;
            }

            private static Dictionary<string, double[]> BuildRoleGrowth(
                Dictionary<string, SnapshotTable> tables,
                List<string> errors)
            {
                Dictionary<string, double[]> result =
                    new Dictionary<string, double[]>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue("RoleGrowth", out table))
                {
                    return result;
                }

                for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    IReadOnlyList<string> row = table.Rows[rowIndex];
                    double[] values = new double[5];
                    bool valid = row.Count >= 11;
                    for (int level = 0; level < values.Length && valid; level++)
                    {
                        valid = double.TryParse(
                            row[4 + level],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out values[level]);
                    }

                    if (!valid)
                    {
                        errors.Add("RoleGrowth row cannot be interpreted.");
                        continue;
                    }

                    result[row[0].Trim()] = values;
                }

                RequireCount("role growth rows", result.Count, 6, errors);
                return result;
            }

            private static Dictionary<string, UpgradeCost> BuildUpgradeCosts(
                Dictionary<string, SnapshotTable> tables,
                List<string> errors)
            {
                Dictionary<string, UpgradeCost> result =
                    new Dictionary<string, UpgradeCost>(StringComparer.Ordinal);
                SnapshotTable table;
                if (!tables.TryGetValue("CostRule", out table))
                {
                    return result;
                }

                for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
                {
                    IReadOnlyList<string> row = table.Rows[rowIndex];
                    string[] currencies = new string[4];
                    int[] amounts = new int[4];
                    bool valid = row.Count >= 10;
                    for (int step = 0; step < 4 && valid; step++)
                    {
                        currencies[step] = row[2 + step * 2].Trim();
                        valid = int.TryParse(
                            row[3 + step * 2],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out amounts[step]);
                    }

                    if (!valid)
                    {
                        errors.Add("CostRule row cannot be interpreted.");
                        continue;
                    }

                    result[row[0].Trim()] = new UpgradeCost(currencies, amounts);
                }

                RequireCount("upgrade cost rows", result.Count, 4, errors);
                return result;
            }

            private static void ValidateInventoryLinks(
                StaffDataAssetInventorySnapshot current,
                StaffLegacySkinInventorySnapshot legacy,
                Dictionary<string, OfficialStaff> official,
                List<string> errors)
            {
                for (int number = 1; number <= ExistingStaffCount; number++)
                {
                    string id = BuildStaffId(number);
                    if (!official.ContainsKey(id) || !current.StaffById.ContainsKey(id))
                    {
                        errors.Add("Current StaffData link is missing: " + id);
                    }
                }

                for (int number = NewStaffStartNumber; number <= OfficialStaffCount; number++)
                {
                    string id = BuildStaffId(number);
                    StaffLegacySkinRowSnapshot skin;
                    if (!official.ContainsKey(id)
                        || !legacy.CandidateStaffId.TryGetValue(id, out skin)
                        || !skin.CandidateMappingIsSequential)
                    {
                        errors.Add("Legacy candidate link is missing or non-sequential: " + id);
                    }
                }
            }
        }

        private sealed class SnapshotTable
        {
            private readonly IReadOnlyList<string> _headers;
            private readonly IReadOnlyList<IReadOnlyList<string>> _rows;

            internal IReadOnlyList<string> Headers { get { return _headers; } }
            internal IReadOnlyList<IReadOnlyList<string>> Rows { get { return _rows; } }

            internal SnapshotTable(StaffOfficialFileSnapshot file)
            {
                _headers = file.Headers;
                _rows = file.Rows;
            }

            internal bool TryGetRowByFirstCell(string value, out IReadOnlyList<string> row)
            {
                for (int index = 0; index < Rows.Count; index++)
                {
                    if (Rows[index].Count > 0
                        && string.Equals(Rows[index][0].Trim(), value, StringComparison.Ordinal))
                    {
                        row = Rows[index];
                        return true;
                    }
                }

                row = null;
                return false;
            }
        }

        private sealed class OfficialStaff
        {
            internal readonly string Id;
            internal readonly int Number;
            internal readonly string Name;
            internal readonly string Description;
            internal readonly string GachaProbabilityRaw;
            internal readonly int RarityStars;
            internal readonly string GradeKey;
            internal readonly string RankName;
            internal readonly string RoleKey;
            internal readonly string PassiveRaw;
            internal readonly string PassiveDescriptionRaw;
            internal readonly double BaseSpeed;
            internal readonly string SkillId;
            internal readonly string SkillDescription;
            internal readonly double SkillDuration;
            internal readonly double SkillCooldown;
            internal readonly string AcquisitionCurrencyRaw;
            internal readonly string DuplicationTokenRaw;
            internal readonly string TokenPurchasePriceRaw;

            internal OfficialStaff(
                string id,
                int number,
                string name,
                string description,
                string gachaProbabilityRaw,
                int rarityStars,
                string gradeKey,
                string rankName,
                string roleKey,
                string passiveRaw,
                string passiveDescriptionRaw,
                double baseSpeed,
                string skillId,
                string skillDescription,
                double skillDuration,
                double skillCooldown,
                string acquisitionCurrencyRaw,
                string duplicationTokenRaw,
                string tokenPurchasePriceRaw)
            {
                Id = id;
                Number = number;
                Name = name;
                Description = description;
                GachaProbabilityRaw = gachaProbabilityRaw;
                RarityStars = rarityStars;
                GradeKey = gradeKey;
                RankName = rankName;
                RoleKey = roleKey;
                PassiveRaw = passiveRaw;
                PassiveDescriptionRaw = passiveDescriptionRaw;
                BaseSpeed = baseSpeed;
                SkillId = skillId;
                SkillDescription = skillDescription;
                SkillDuration = skillDuration;
                SkillCooldown = skillCooldown;
                AcquisitionCurrencyRaw = acquisitionCurrencyRaw;
                DuplicationTokenRaw = duplicationTokenRaw;
                TokenPurchasePriceRaw = tokenPurchasePriceRaw;
            }
        }

        private sealed class RoleBaseValue
        {
            internal readonly double BaseValue;
            internal readonly double MinimumValue;

            internal RoleBaseValue(double baseValue, double minimumValue)
            {
                BaseValue = baseValue;
                MinimumValue = minimumValue;
            }
        }

        private sealed class UpgradeCost
        {
            internal readonly string[] Currencies;
            internal readonly int[] Amounts;

            internal UpgradeCost(string[] currencies, int[] amounts)
            {
                Currencies = (string[])currencies.Clone();
                Amounts = (int[])amounts.Clone();
            }
        }

        private static bool TryParseStaffNumber(string id, out int number)
        {
            number = 0;
            return id != null
                   && id.Length == 7
                   && id.StartsWith("STAFF", StringComparison.Ordinal)
                   && int.TryParse(
                       id.Substring(5),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out number)
                   && number >= 1
                   && number <= OfficialStaffCount;
        }

        private static bool TryParseSeconds(string raw, out double value)
        {
            value = 0d;
            if (raw == null)
            {
                return false;
            }

            string trimmed = raw.Trim();
            int end = 0;
            while (end < trimmed.Length
                   && (char.IsDigit(trimmed[end])
                       || trimmed[end] == '-'
                       || trimmed[end] == '+'
                       || trimmed[end] == '.'))
            {
                end++;
            }

            return end > 0
                   && double.TryParse(
                       trimmed.Substring(0, end),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out value);
        }
    }
}
