using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal enum StaffDryRunAssetAction
    {
        UPDATE_EXISTING,
        CREATE_NEW
    }

    internal enum StaffDryRunReadiness
    {
        PLAN_READY,
        PLAN_READY_WITH_WARNINGS,
        ASSET_PLAN_READY,
        RUNTIME_SCHEMA_REQUIRED,
        SKILL_CLASS_REQUIRED,
        SAVE_MIGRATION_REQUIRED,
        FUTURE_SYSTEM_REQUIRED,
        MULTIPLE_PREREQUISITES_REQUIRED
    }

    internal enum StaffDryRunFieldDisposition
    {
        AUTO_UPDATE_EXISTING,
        AUTO_CREATE_NEW,
        PRESERVE_EXISTING,
        NO_RUNTIME_FIELD,
        RUNTIME_SCHEMA_REQUIRED,
        RUNTIME_MAPPING_MISMATCH,
        SAVE_MIGRATION_REQUIRED,
        FUTURE_STAFF_ACQUISITION_SYSTEM_REQUIRED,
        FUTURE_PANDA_TOKEN_SYSTEM_REQUIRED,
        LEGACY_METADATA_ONLY,
        INTENTIONAL_NO_FRAME_IDLE,
        USE_LEGACY_IDLE_FRAMES,
        USE_BASE_SPRITE_SHARED_BREATHING,
        USE_SHARED_PREFAB_ANIMATOR,
        SKILL_CLASS_MIGRATION_REQUIRED,
        SKILL_CLASS_IMPLEMENTATION_REQUIRED
    }

    internal sealed class StaffDataDryRunFieldPlan
    {
        internal string FieldPath { get; }
        internal string CurrentValue { get; }
        internal string TargetValue { get; }
        internal StaffDryRunFieldDisposition Disposition { get; }
        internal bool IsChanged { get; }
        internal string Note { get; }

        internal StaffDataDryRunFieldPlan(
            string fieldPath,
            string currentValue,
            string targetValue,
            StaffDryRunFieldDisposition disposition,
            bool isChanged,
            string note)
        {
            FieldPath = fieldPath ?? string.Empty;
            CurrentValue = currentValue ?? string.Empty;
            TargetValue = targetValue ?? string.Empty;
            Disposition = disposition;
            IsChanged = isChanged;
            Note = note ?? string.Empty;
        }

        internal void AppendFingerprint(StringBuilder input)
        {
            StaffDataDryRunPlanSnapshot.AppendValue(input, FieldPath);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentValue);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetValue);
            StaffDataDryRunPlanSnapshot.AppendValue(input, Disposition.ToString());
            StaffDataDryRunPlanSnapshot.AppendValue(input, IsChanged ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, Note);
        }
    }

    internal sealed class StaffDataDryRunSkillPlan
    {
        internal string OfficialSkillId { get; }
        internal string RequiredClassName { get; }
        internal bool RequiredClassExists { get; }
        internal string CurrentAssetPath { get; }
        internal string CurrentAssetGuid { get; }
        internal string CurrentClassName { get; }
        internal string PlannedAssetPath { get; }
        internal bool PreserveExistingGuid { get; }
        internal bool CreateIndividualAsset { get; }
        internal bool ClassMatches { get; }
        internal string CurrentDescription { get; }
        internal string TargetDescription { get; }
        internal string CurrentDuration { get; }
        internal string TargetDuration { get; }
        internal string CurrentCooldown { get; }
        internal string TargetCooldown { get; }
        internal StaffDryRunFieldDisposition ClassDisposition { get; }

        internal StaffDataDryRunSkillPlan(
            string officialSkillId,
            string requiredClassName,
            bool requiredClassExists,
            string currentAssetPath,
            string currentAssetGuid,
            string currentClassName,
            string plannedAssetPath,
            bool preserveExistingGuid,
            bool createIndividualAsset,
            bool classMatches,
            string currentDescription,
            string targetDescription,
            string currentDuration,
            string targetDuration,
            string currentCooldown,
            string targetCooldown,
            StaffDryRunFieldDisposition classDisposition)
        {
            OfficialSkillId = officialSkillId ?? string.Empty;
            RequiredClassName = requiredClassName ?? string.Empty;
            RequiredClassExists = requiredClassExists;
            CurrentAssetPath = currentAssetPath ?? string.Empty;
            CurrentAssetGuid = currentAssetGuid ?? string.Empty;
            CurrentClassName = currentClassName ?? string.Empty;
            PlannedAssetPath = plannedAssetPath ?? string.Empty;
            PreserveExistingGuid = preserveExistingGuid;
            CreateIndividualAsset = createIndividualAsset;
            ClassMatches = classMatches;
            CurrentDescription = currentDescription ?? string.Empty;
            TargetDescription = targetDescription ?? string.Empty;
            CurrentDuration = currentDuration ?? string.Empty;
            TargetDuration = targetDuration ?? string.Empty;
            CurrentCooldown = currentCooldown ?? string.Empty;
            TargetCooldown = targetCooldown ?? string.Empty;
            ClassDisposition = classDisposition;
        }

        internal void AppendFingerprint(StringBuilder input)
        {
            StaffDataDryRunPlanSnapshot.AppendValue(input, OfficialSkillId);
            StaffDataDryRunPlanSnapshot.AppendValue(input, RequiredClassName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, RequiredClassExists ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentAssetPath);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentAssetGuid);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentClassName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, PlannedAssetPath);
            StaffDataDryRunPlanSnapshot.AppendValue(input, PreserveExistingGuid ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, CreateIndividualAsset ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, ClassMatches ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentDescription);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetDescription);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentDuration);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetDuration);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentCooldown);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetCooldown);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ClassDisposition.ToString());
        }
    }

    internal sealed class StaffDataDryRunVisualPlan
    {
        private readonly IReadOnlyList<string> _referenceKeys;
        private readonly IReadOnlyList<string> _idleReferenceKeys;
        private readonly IReadOnlyList<string> _particleReferenceKeys;
        private readonly IReadOnlyList<string> _namingMismatchReferenceKeys;

        internal bool PreserveExistingVisuals { get; }
        internal string LegacySkinId { get; }
        internal string MainReferenceKey { get; }
        internal string ThumbnailReferenceKey { get; }
        internal IReadOnlyList<string> ReferenceKeys { get { return _referenceKeys; } }
        internal IReadOnlyList<string> IdleReferenceKeys { get { return _idleReferenceKeys; } }
        internal IReadOnlyList<string> ParticleReferenceKeys { get { return _particleReferenceKeys; } }
        internal IReadOnlyList<string> NamingMismatchReferenceKeys
        {
            get { return _namingMismatchReferenceKeys; }
        }

        internal string ChefBackReferenceKey { get; }
        internal string ChefHandReferenceKey { get; }
        internal string CheerleaderAnimationReferenceKey { get; }
        internal StaffDryRunFieldDisposition IdleDisposition { get; }
        internal StaffDryRunFieldDisposition AnimatorDisposition { get; }
        internal string IdlePolicyCode { get; }
        internal string AnimatorPolicyCode { get; }
        internal bool HasMain { get; }
        internal bool HasThumbnail { get; }
        internal bool HasRequiredRoleParts { get; }
        internal bool HasMissingReference { get; }
        internal bool NamingMismatchReviewRequired { get; }

        internal StaffDataDryRunVisualPlan(
            bool preserveExistingVisuals,
            string legacySkinId,
            string mainReferenceKey,
            string thumbnailReferenceKey,
            IEnumerable<string> referenceKeys,
            IEnumerable<string> idleReferenceKeys,
            IEnumerable<string> particleReferenceKeys,
            IEnumerable<string> namingMismatchReferenceKeys,
            string chefBackReferenceKey,
            string chefHandReferenceKey,
            string cheerleaderAnimationReferenceKey,
            StaffDryRunFieldDisposition idleDisposition,
            StaffDryRunFieldDisposition animatorDisposition,
            string idlePolicyCode,
            string animatorPolicyCode,
            bool hasMain,
            bool hasThumbnail,
            bool hasRequiredRoleParts,
            bool hasMissingReference,
            bool namingMismatchReviewRequired)
        {
            if (referenceKeys == null)
            {
                throw new ArgumentNullException(nameof(referenceKeys));
            }

            if (idleReferenceKeys == null)
            {
                throw new ArgumentNullException(nameof(idleReferenceKeys));
            }

            if (particleReferenceKeys == null)
            {
                throw new ArgumentNullException(nameof(particleReferenceKeys));
            }

            if (namingMismatchReferenceKeys == null)
            {
                throw new ArgumentNullException(nameof(namingMismatchReferenceKeys));
            }

            PreserveExistingVisuals = preserveExistingVisuals;
            LegacySkinId = legacySkinId ?? string.Empty;
            MainReferenceKey = mainReferenceKey ?? string.Empty;
            ThumbnailReferenceKey = thumbnailReferenceKey ?? string.Empty;
            _referenceKeys = CopyStrings(referenceKeys);
            _idleReferenceKeys = CopyStrings(idleReferenceKeys);
            _particleReferenceKeys = CopyStrings(particleReferenceKeys);
            _namingMismatchReferenceKeys = CopyStrings(namingMismatchReferenceKeys);
            ChefBackReferenceKey = chefBackReferenceKey ?? string.Empty;
            ChefHandReferenceKey = chefHandReferenceKey ?? string.Empty;
            CheerleaderAnimationReferenceKey = cheerleaderAnimationReferenceKey ?? string.Empty;
            IdleDisposition = idleDisposition;
            AnimatorDisposition = animatorDisposition;
            IdlePolicyCode = idlePolicyCode ?? string.Empty;
            AnimatorPolicyCode = animatorPolicyCode ?? string.Empty;
            HasMain = hasMain;
            HasThumbnail = hasThumbnail;
            HasRequiredRoleParts = hasRequiredRoleParts;
            HasMissingReference = hasMissingReference;
            NamingMismatchReviewRequired = namingMismatchReviewRequired;
        }

        internal void AppendFingerprint(StringBuilder input)
        {
            StaffDataDryRunPlanSnapshot.AppendValue(input, PreserveExistingVisuals ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, LegacySkinId);
            StaffDataDryRunPlanSnapshot.AppendValue(input, MainReferenceKey);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ThumbnailReferenceKey);
            StaffDataDryRunPlanSnapshot.AppendValues(input, ReferenceKeys);
            StaffDataDryRunPlanSnapshot.AppendValues(input, IdleReferenceKeys);
            StaffDataDryRunPlanSnapshot.AppendValues(input, ParticleReferenceKeys);
            StaffDataDryRunPlanSnapshot.AppendValues(input, NamingMismatchReferenceKeys);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ChefBackReferenceKey);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ChefHandReferenceKey);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CheerleaderAnimationReferenceKey);
            StaffDataDryRunPlanSnapshot.AppendValue(input, IdleDisposition.ToString());
            StaffDataDryRunPlanSnapshot.AppendValue(input, AnimatorDisposition.ToString());
            StaffDataDryRunPlanSnapshot.AppendValue(input, IdlePolicyCode);
            StaffDataDryRunPlanSnapshot.AppendValue(input, AnimatorPolicyCode);
            StaffDataDryRunPlanSnapshot.AppendValue(input, HasMain ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, HasThumbnail ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, HasRequiredRoleParts ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, HasMissingReference ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, NamingMismatchReviewRequired ? "1" : "0");
        }

        private static IReadOnlyList<string> CopyStrings(IEnumerable<string> values)
        {
            List<string> copy = new List<string>();
            foreach (string value in values)
            {
                copy.Add(value ?? string.Empty);
            }

            copy.Sort(StringComparer.Ordinal);
            return new ReadOnlyCollection<string>(copy);
        }
    }

    internal sealed class StaffDataDryRunIssue
    {
        internal string Code { get; }
        internal string StaffId { get; }
        internal StaffDryRunFieldDisposition Disposition { get; }
        internal string Message { get; }
        internal bool IsWarning { get; }
        internal bool IsPrerequisite { get; }

        internal StaffDataDryRunIssue(
            string code,
            string staffId,
            StaffDryRunFieldDisposition disposition,
            string message,
            bool isWarning,
            bool isPrerequisite)
        {
            Code = code ?? string.Empty;
            StaffId = staffId ?? string.Empty;
            Disposition = disposition;
            Message = message ?? string.Empty;
            IsWarning = isWarning;
            IsPrerequisite = isPrerequisite;
        }

        internal void AppendFingerprint(StringBuilder input)
        {
            StaffDataDryRunPlanSnapshot.AppendValue(input, Code);
            StaffDataDryRunPlanSnapshot.AppendValue(input, StaffId);
            StaffDataDryRunPlanSnapshot.AppendValue(input, Disposition.ToString());
            StaffDataDryRunPlanSnapshot.AppendValue(input, Message);
            StaffDataDryRunPlanSnapshot.AppendValue(input, IsWarning ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, IsPrerequisite ? "1" : "0");
        }
    }

    internal sealed class StaffDataDryRunStaffPlan
    {
        private readonly IReadOnlyList<StaffDataDryRunFieldPlan> _fieldPlans;
        private readonly IReadOnlyList<StaffDataDryRunIssue> _issues;

        internal string StaffId { get; }
        internal int StaffNumber { get; }
        internal StaffDryRunAssetAction AssetAction { get; }
        internal StaffDryRunReadiness Readiness { get; }
        internal string RoleKey { get; }
        internal string TargetRankName { get; }
        internal string TargetConcreteTypeName { get; }
        internal string ExistingAssetPath { get; }
        internal string ExistingAssetGuid { get; }
        internal string ExistingScriptGuid { get; }
        internal string PlannedAssetPath { get; }
        internal bool PreserveExistingGuid { get; }
        internal string CurrentName { get; }
        internal string TargetName { get; }
        internal string CurrentDescription { get; }
        internal string TargetDescription { get; }
        internal string CurrentRankName { get; }
        internal string CurrentSpeed { get; }
        internal string TargetSpeed { get; }
        internal string GachaProbabilityRaw { get; }
        internal string AcquisitionCurrencyRaw { get; }
        internal string DuplicationTokenRaw { get; }
        internal string TokenPurchasePriceRaw { get; }
        internal IReadOnlyList<StaffDataDryRunFieldPlan> FieldPlans { get { return _fieldPlans; } }
        internal StaffDataDryRunSkillPlan SkillPlan { get; }
        internal StaffDataDryRunVisualPlan VisualPlan { get; }
        internal IReadOnlyList<StaffDataDryRunIssue> Issues { get { return _issues; } }
        internal int ChangedFieldCount { get; }
        internal int PreservedFieldCount { get; }

        internal StaffDataDryRunStaffPlan(
            string staffId,
            int staffNumber,
            StaffDryRunAssetAction assetAction,
            StaffDryRunReadiness readiness,
            string roleKey,
            string targetRankName,
            string targetConcreteTypeName,
            string existingAssetPath,
            string existingAssetGuid,
            string existingScriptGuid,
            string plannedAssetPath,
            bool preserveExistingGuid,
            string currentName,
            string targetName,
            string currentDescription,
            string targetDescription,
            string currentRankName,
            string currentSpeed,
            string targetSpeed,
            string gachaProbabilityRaw,
            string acquisitionCurrencyRaw,
            string duplicationTokenRaw,
            string tokenPurchasePriceRaw,
            IEnumerable<StaffDataDryRunFieldPlan> fieldPlans,
            StaffDataDryRunSkillPlan skillPlan,
            StaffDataDryRunVisualPlan visualPlan,
            IEnumerable<StaffDataDryRunIssue> issues)
        {
            if (fieldPlans == null)
            {
                throw new ArgumentNullException(nameof(fieldPlans));
            }

            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            StaffId = staffId ?? string.Empty;
            StaffNumber = staffNumber;
            AssetAction = assetAction;
            Readiness = readiness;
            RoleKey = roleKey ?? string.Empty;
            TargetRankName = targetRankName ?? string.Empty;
            TargetConcreteTypeName = targetConcreteTypeName ?? string.Empty;
            ExistingAssetPath = existingAssetPath ?? string.Empty;
            ExistingAssetGuid = existingAssetGuid ?? string.Empty;
            ExistingScriptGuid = existingScriptGuid ?? string.Empty;
            PlannedAssetPath = plannedAssetPath ?? string.Empty;
            PreserveExistingGuid = preserveExistingGuid;
            CurrentName = currentName ?? string.Empty;
            TargetName = targetName ?? string.Empty;
            CurrentDescription = currentDescription ?? string.Empty;
            TargetDescription = targetDescription ?? string.Empty;
            CurrentRankName = currentRankName ?? string.Empty;
            CurrentSpeed = currentSpeed ?? string.Empty;
            TargetSpeed = targetSpeed ?? string.Empty;
            GachaProbabilityRaw = gachaProbabilityRaw ?? string.Empty;
            AcquisitionCurrencyRaw = acquisitionCurrencyRaw ?? string.Empty;
            DuplicationTokenRaw = duplicationTokenRaw ?? string.Empty;
            TokenPurchasePriceRaw = tokenPurchasePriceRaw ?? string.Empty;
            _fieldPlans = new ReadOnlyCollection<StaffDataDryRunFieldPlan>(
                new List<StaffDataDryRunFieldPlan>(fieldPlans));
            SkillPlan = skillPlan ?? throw new ArgumentNullException(nameof(skillPlan));
            VisualPlan = visualPlan ?? throw new ArgumentNullException(nameof(visualPlan));
            _issues = new ReadOnlyCollection<StaffDataDryRunIssue>(
                new List<StaffDataDryRunIssue>(issues));

            int changed = 0;
            int preserved = 0;
            for (int index = 0; index < _fieldPlans.Count; index++)
            {
                changed += _fieldPlans[index].IsChanged ? 1 : 0;
                preserved += _fieldPlans[index].Disposition
                             == StaffDryRunFieldDisposition.PRESERVE_EXISTING ? 1 : 0;
            }

            ChangedFieldCount = changed;
            PreservedFieldCount = preserved;
        }

        internal void AppendFingerprint(StringBuilder input)
        {
            StaffDataDryRunPlanSnapshot.AppendValue(input, StaffId);
            StaffDataDryRunPlanSnapshot.AppendValue(
                input,
                StaffNumber.ToString(CultureInfo.InvariantCulture));
            StaffDataDryRunPlanSnapshot.AppendValue(input, AssetAction.ToString());
            StaffDataDryRunPlanSnapshot.AppendValue(input, Readiness.ToString());
            StaffDataDryRunPlanSnapshot.AppendValue(input, RoleKey);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetRankName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetConcreteTypeName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ExistingAssetPath);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ExistingAssetGuid);
            StaffDataDryRunPlanSnapshot.AppendValue(input, ExistingScriptGuid);
            StaffDataDryRunPlanSnapshot.AppendValue(input, PlannedAssetPath);
            StaffDataDryRunPlanSnapshot.AppendValue(input, PreserveExistingGuid ? "1" : "0");
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentDescription);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetDescription);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentRankName);
            StaffDataDryRunPlanSnapshot.AppendValue(input, CurrentSpeed);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TargetSpeed);
            StaffDataDryRunPlanSnapshot.AppendValue(input, GachaProbabilityRaw);
            StaffDataDryRunPlanSnapshot.AppendValue(input, AcquisitionCurrencyRaw);
            StaffDataDryRunPlanSnapshot.AppendValue(input, DuplicationTokenRaw);
            StaffDataDryRunPlanSnapshot.AppendValue(input, TokenPurchasePriceRaw);
            StaffDataDryRunPlanSnapshot.AppendValue(
                input,
                FieldPlans.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < FieldPlans.Count; index++)
            {
                FieldPlans[index].AppendFingerprint(input);
            }

            SkillPlan.AppendFingerprint(input);
            VisualPlan.AppendFingerprint(input);
            StaffDataDryRunPlanSnapshot.AppendValue(
                input,
                Issues.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < Issues.Count; index++)
            {
                Issues[index].AppendFingerprint(input);
            }
        }
    }

    internal sealed class StaffDataDryRunPlanSnapshot
    {
        internal const string PolicyVersion = "STAFF_DRY_RUN_POLICY_2026_08_18_V4";

        private readonly IReadOnlyList<StaffDataDryRunStaffPlan> _staffPlans;
        private readonly IReadOnlyDictionary<string, StaffDataDryRunStaffPlan> _staffById;
        private readonly IReadOnlyList<StaffDataDryRunIssue> _globalIssues;

        internal string OfficialPackageFingerprint { get; }
        internal string CurrentInventoryFingerprint { get; }
        internal string LegacyInventoryFingerprint { get; }
        internal string PlanningPolicyVersion { get { return PolicyVersion; } }
        internal IReadOnlyList<StaffDataDryRunStaffPlan> StaffPlans { get { return _staffPlans; } }
        internal IReadOnlyDictionary<string, StaffDataDryRunStaffPlan> StaffById
        {
            get { return _staffById; }
        }

        internal IReadOnlyList<StaffDataDryRunIssue> GlobalIssues { get { return _globalIssues; } }
        internal string PlanFingerprint { get; }

        internal StaffDataDryRunPlanSnapshot(
            string officialPackageFingerprint,
            string currentInventoryFingerprint,
            string legacyInventoryFingerprint,
            IEnumerable<StaffDataDryRunStaffPlan> staffPlans,
            IEnumerable<StaffDataDryRunIssue> globalIssues)
        {
            if (staffPlans == null)
            {
                throw new ArgumentNullException(nameof(staffPlans));
            }

            if (globalIssues == null)
            {
                throw new ArgumentNullException(nameof(globalIssues));
            }

            OfficialPackageFingerprint = officialPackageFingerprint ?? string.Empty;
            CurrentInventoryFingerprint = currentInventoryFingerprint ?? string.Empty;
            LegacyInventoryFingerprint = legacyInventoryFingerprint ?? string.Empty;
            List<StaffDataDryRunStaffPlan> planCopy = new List<StaffDataDryRunStaffPlan>(staffPlans);
            planCopy.Sort((left, right) => left.StaffNumber.CompareTo(right.StaffNumber));
            Dictionary<string, StaffDataDryRunStaffPlan> byId =
                new Dictionary<string, StaffDataDryRunStaffPlan>(StringComparer.Ordinal);
            for (int index = 0; index < planCopy.Count; index++)
            {
                byId.Add(planCopy[index].StaffId, planCopy[index]);
            }

            _staffPlans = new ReadOnlyCollection<StaffDataDryRunStaffPlan>(planCopy);
            _staffById = new ReadOnlyDictionary<string, StaffDataDryRunStaffPlan>(byId);
            List<StaffDataDryRunIssue> issueCopy = new List<StaffDataDryRunIssue>(globalIssues);
            issueCopy.Sort(CompareIssues);
            _globalIssues = new ReadOnlyCollection<StaffDataDryRunIssue>(issueCopy);
            PlanFingerprint = BuildFingerprint();
        }

        private string BuildFingerprint()
        {
            StringBuilder input = new StringBuilder();
            AppendValue(input, PolicyVersion);
            AppendValue(input, OfficialPackageFingerprint);
            AppendValue(input, CurrentInventoryFingerprint);
            AppendValue(input, LegacyInventoryFingerprint);
            AppendValue(input, StaffPlans.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < StaffPlans.Count; index++)
            {
                StaffPlans[index].AppendFingerprint(input);
            }

            AppendValue(input, GlobalIssues.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < GlobalIssues.Count; index++)
            {
                GlobalIssues[index].AppendFingerprint(input);
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));
                StringBuilder result = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    result.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        private static int CompareIssues(StaffDataDryRunIssue left, StaffDataDryRunIssue right)
        {
            int comparison = string.Compare(left.Code, right.Code, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.StaffId, right.StaffId, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.Message, right.Message, StringComparison.Ordinal);
        }

        internal static void AppendValues(StringBuilder input, IReadOnlyList<string> values)
        {
            AppendValue(input, values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                AppendValue(input, values[index]);
            }
        }

        internal static void AppendValue(StringBuilder input, string value)
        {
            string safeValue = value ?? string.Empty;
            input.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            input.Append(':');
            input.Append(safeValue);
            input.Append(';');
        }
    }
}
