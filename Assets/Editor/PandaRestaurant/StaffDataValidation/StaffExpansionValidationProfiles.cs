using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    // Expectations are approved constants, never inferred from the installed Assets.
    internal sealed class StaffExpansionValidationProfile
    {
        private readonly HashSet<string> _staffIdSet;
        internal string Name { get; }
        internal IReadOnlyList<string> StaffIds { get; }
        internal int StaffCount { get { return StaffIds.Count; } }
        internal int SkillCount { get { return StaffCount; } }
        internal int NewStaffCount { get { return StaffCount - 32; } }
        internal int RemainingNewStaffCount { get { return 92 - StaffCount; } }
        internal int ComparisonFileCount { get { return 2 * (StaffCount + SkillCount + 18); } }
        internal IReadOnlyDictionary<string, int> StaffClassCounts { get; }
        internal IReadOnlyDictionary<string, int> RankCounts { get; }
        internal IReadOnlyDictionary<string, int> SkillIdCounts { get; }
        internal IReadOnlyDictionary<string, int> SkillClassCounts { get; }

        internal StaffExpansionValidationProfile(
            string name, IEnumerable<string> ids, int[] roles, int[] ranks, int[] skills)
        {
            Name = name;
            List<string> copy = new List<string>(ids);
            copy.Sort(StringComparer.Ordinal);
            StaffIds = copy.AsReadOnly();
            _staffIdSet = new HashSet<string>(copy, StringComparer.Ordinal);
            StaffClassCounts = Freeze(
                new[] { "WaiterData", "ManagerData", "MarketerData", "ChefData", "CleanerData", "GuardData" }, roles);
            RankCounts = Freeze(new[] { "Normal", "Rare", "Unique", "Special" }, ranks);
            SkillIdCounts = Freeze(
                new[] { "STAFF_SKILL01", "STAFF_SKILL03", "STAFF_SKILL04", "STAFF_SKILL05", "STAFF_SKILL06", "STAFF_SKILL08", "STAFF_SKILL09", "STAFF_SKILL10" }, skills);
            Dictionary<string, int> classes = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] classNames =
            {
                "SpeedUpSkill", "TouchAddCustomerButtonSkill", "AssignedCookingSpeedUpSkill", "FoodPriceUpSkill",
                "FoodPaymentTipUpSkill", "NormalCustomerMoveSpeedUpSkill", "GlobalRemainingCookingTimeReductionSkill", "AllStaffMoveSpeedUpSkill"
            };
            for (int index = 0; index < classNames.Length; index++) classes.Add(classNames[index], skills[index]);
            classes.Add("GlobalCookingSpeedUpSkill", 0);
            SkillClassCounts = new ReadOnlyDictionary<string, int>(classes);
        }

        internal bool ContainsStaffId(string id) { return id != null && _staffIdSet.Contains(id); }

        private static IReadOnlyDictionary<string, int> Freeze(string[] keys, int[] values)
        {
            if (keys.Length != values.Length) throw new ArgumentException("Profile distribution length mismatch.");
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < keys.Length; index++) result.Add(keys[index], values[index]);
            return new ReadOnlyDictionary<string, int>(result);
        }
    }

    internal static class StaffExpansionValidationProfiles
    {
        internal static readonly StaffExpansionValidationProfile Baseline32 = new StaffExpansionValidationProfile(
            "Baseline32", BuildIds(32), new[] { 7, 5, 5, 7, 6, 2 }, new[] { 23, 2, 5, 2 }, new[] { 12, 3, 4, 6, 1, 5, 1, 0 });
        internal static readonly StaffExpansionValidationProfile Pilot37 = new StaffExpansionValidationProfile(
            "Pilot37", BuildIds(32, 33, 39, 41, 45, 49), new[] { 8, 6, 6, 8, 7, 2 }, new[] { 23, 6, 5, 3 }, new[] { 14, 4, 4, 7, 1, 5, 1, 1 });
        internal static readonly StaffExpansionValidationProfile Full92 = new StaffExpansionValidationProfile(
            "Full92", BuildIds(92), new[] { 23, 13, 17, 23, 14, 2 }, new[] { 23, 26, 35, 8 }, new[] { 34, 15, 13, 10, 3, 10, 2, 5 });

        internal static bool IsBaselineStaffId(string id) { return Baseline32.ContainsStaffId(id); }

        internal static void RequireApproved(StaffExpansionValidationProfile profile)
        {
            if (!ReferenceEquals(profile, Baseline32) && !ReferenceEquals(profile, Pilot37) && !ReferenceEquals(profile, Full92))
                throw new ArgumentException("An explicit approved Baseline32, Pilot37 or Full92 profile is required.", nameof(profile));
        }

        internal static bool ValidateExactIds(
            IEnumerable<string> ids, StaffExpansionValidationProfile profile, List<string> errors)
        {
            RequireApproved(profile);
            int initialErrors = errors.Count;
            HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (string id in ids)
            {
                count++;
                if (string.IsNullOrEmpty(id) || !actual.Add(id)) errors.Add(profile.Name + " duplicate/empty Staff ID: " + id);
                if (!profile.ContainsStaffId(id)) errors.Add(profile.Name + " unapproved Staff ID: " + id);
            }
            foreach (string id in profile.StaffIds)
                if (!actual.Contains(id)) errors.Add(profile.Name + " missing Staff ID: " + id);
            if (count != profile.StaffCount) errors.Add(profile.Name + " expected Staff " + profile.StaffCount + ", actual " + count);
            return initialErrors == errors.Count;
        }

        internal static bool ValidateInventory(
            StaffDataAssetInventorySnapshot snapshot, StaffExpansionValidationProfile profile, List<string> errors)
        {
            RequireApproved(profile);
            int initialErrors = errors.Count;
            if (snapshot == null) { errors.Add(profile.Name + " inventory is null."); return false; }
            List<string> ids = new List<string>();
            Dictionary<string, int> roles = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> skills = new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (StaffDataAssetSnapshot staff in snapshot.Staff)
            {
                ids.Add(staff.Id);
                AddCount(roles, staff.ConcreteTypeName);
                AddCount(ranks, staff.RankName == "Normal2" ? "Normal" : staff.RankName);
                AddGuid(guids, staff.AssetGuid, staff.Id, errors);
                if (!staff.FileNameMatchesId || staff.AssetPath != "Assets/Resources/StaffData/" + staff.Id + ".asset")
                    errors.Add("StaffData identity/path mismatch: " + staff.Id);
                if (string.IsNullOrWhiteSpace(staff.Name) || string.IsNullOrWhiteSpace(staff.Description))
                    errors.Add("StaffData name/description is empty: " + staff.Id);
                if (!staff.HasExpectedLevelArray || staff.LevelCount != 5 || staff.Levels.Count != 5)
                    errors.Add("StaffData must have five official levels: " + staff.Id);
                if (staff.HasMissingRequiredReference || !ValidReference(staff.SpriteReference) || !ValidReference(staff.ThumbnailReference))
                    errors.Add("StaffData required visual reference missing: " + staff.Id);
                if (staff.AnimatorControllerReference == null || staff.AnimatorControllerReference.IsAssigned || staff.AnimatorControllerReference.IsMissing)
                    errors.Add("StaffData AnimatorController must be null: " + staff.Id);
                StaffSkillAssetSnapshot skill;
                if (!ValidReference(staff.SkillReference) || !snapshot.TryGetSkill(staff.SkillReference.AssetGuid, out skill)
                    || skill.ReferenceCount != 1 || skill.ReferencedStaffIds[0] != staff.Id)
                    errors.Add("StaffData-Skill one-to-one reference mismatch: " + staff.Id);
                if (!IsBaselineStaffId(staff.Id))
                {
                    if (staff.SkillReference == null || staff.SkillReference.AssetPath != "Assets/Scripts/Datas/Staff/Skill/" + staff.Id + "Skill.asset")
                        errors.Add("Expansion staff must own its independent Skill Asset: " + staff.Id);
                    ValidateExpansionReference(staff.SpriteReference, staff.Id, true, errors);
                    ValidateExpansionReference(staff.ThumbnailReference, staff.Id, true, errors);
                    foreach (StaffAssetReferenceSnapshot reference in staff.IdleSpriteReferences) ValidateExpansionReference(reference, staff.Id, true, errors);
                    ValidateExpansionReference(staff.BackSpriteReference, staff.Id, staff.ConcreteTypeName == "ChefData", errors);
                    ValidateExpansionReference(staff.HandSpriteReference, staff.Id, staff.ConcreteTypeName == "ChefData", errors);
                    ValidateExpansionReference(staff.UiSpriteReference, staff.Id, staff.ConcreteTypeName == "MarketerData", errors);
                    ValidateExpansionReference(staff.AnimationSpriteReference, staff.Id, staff.ConcreteTypeName == "MarketerData", errors);
                    foreach (StaffAssetReferenceSnapshot reference in staff.ParticleSpriteReferences) ValidateExpansionReference(reference, staff.Id, true, errors);
                }
            }
            ValidateExactIds(ids, profile, errors);
            foreach (StaffSkillAssetSnapshot skill in snapshot.Skills)
            {
                AddCount(skills, skill.ConcreteTypeName);
                AddGuid(guids, skill.AssetGuid, skill.AssetPath, errors);
                if (skill.HasMissingScript || skill.HasMissingSerializedReference || skill.ReferenceCount != 1 || skill.IsShared || skill.IsOrphan)
                    errors.Add("Skill missing reference/script or nonexclusive ownership: " + skill.AssetPath);
                if (float.IsNaN(skill.Duration) || float.IsInfinity(skill.Duration) || skill.Duration < 0
                    || float.IsNaN(skill.Cooldown) || float.IsInfinity(skill.Cooldown) || skill.Cooldown < 0)
                    errors.Add("Invalid Skill timing: " + skill.AssetPath);
            }
            if (snapshot.Skills.Count != profile.SkillCount) errors.Add(profile.Name + " expected Skill " + profile.SkillCount + ", actual " + snapshot.Skills.Count);
            ValidateDistribution("role", roles, profile.StaffClassCounts, errors);
            ValidateDistribution("rank", ranks, profile.RankCounts, errors);
            ValidateDistribution("skill", skills, profile.SkillClassCounts, errors);
            return initialErrors == errors.Count;
        }

        // The historical STAFF01~32 migrations retain their original assertions.
        // This projection never selects a profile or validates an expanded catalog by itself.
        internal static StaffDataAssetInventorySnapshot CreateBaselineSnapshot(StaffDataAssetInventorySnapshot current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            List<StaffDataAssetSnapshot> staff = new List<StaffDataAssetSnapshot>();
            List<StaffSkillAssetSnapshot> skills = new List<StaffSkillAssetSnapshot>();
            HashSet<string> referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (StaffDataAssetSnapshot item in current.Staff)
                if (IsBaselineStaffId(item.Id))
                {
                    staff.Add(item);
                    if (item.SkillReference != null) referenced.Add(item.SkillReference.AssetGuid);
                }
            foreach (StaffSkillAssetSnapshot item in current.Skills)
                if (referenced.Contains(item.AssetGuid)) skills.Add(item);
            return new StaffDataAssetInventorySnapshot(staff, skills);
        }

        internal static void ValidateDefinitions()
        {
            foreach (StaffExpansionValidationProfile profile in new[] { Baseline32, Pilot37, Full92 })
            {
                List<string> errors = new List<string>();
                if (!ValidateExactIds(profile.StaffIds, profile, errors)) throw new InvalidOperationException("Invalid profile ID definition.");
                foreach (IReadOnlyDictionary<string, int> distribution in new[] { profile.StaffClassCounts, profile.RankCounts, profile.SkillIdCounts, profile.SkillClassCounts })
                {
                    int sum = 0;
                    foreach (int count in distribution.Values) sum += count;
                    if (sum != profile.StaffCount) throw new InvalidOperationException("Profile distribution total mismatch: " + profile.Name);
                    IDictionary<string, int> mutable = distribution as IDictionary<string, int>;
                    if (mutable == null || !mutable.IsReadOnly) throw new InvalidOperationException("Mutable profile distribution.");
                    ExpectReadOnly(() => mutable["unexpected"] = 1);
                }
                IList<string> list = profile.StaffIds as IList<string>;
                if (list == null || !list.IsReadOnly) throw new InvalidOperationException("Mutable profile IDs.");
                ExpectReadOnly(() => list[0] = "STAFF99");
            }
            Reject(BuildIds(33), Baseline32);
            Reject(BuildIds(32, 34), Baseline32);
            Reject(BuildIds(32, 33, 39, 41, 45), Pilot37);
            Reject(BuildIds(32, 34, 39, 41, 45, 49), Pilot37);
            List<string> wrongFull = BuildIds(91, 93);
            Reject(wrongFull, Full92);
            wrongFull[91] = "STAFF91";
            Reject(wrongFull, Full92);
        }

        private static List<string> BuildIds(int lastBaseline, params int[] additional)
        {
            List<string> ids = new List<string>();
            for (int number = 1; number <= lastBaseline; number++) ids.Add("STAFF" + number.ToString("00", CultureInfo.InvariantCulture));
            foreach (int number in additional) ids.Add("STAFF" + number.ToString("00", CultureInfo.InvariantCulture));
            return ids;
        }

        private static void Reject(IEnumerable<string> ids, StaffExpansionValidationProfile profile)
        {
            List<string> errors = new List<string>();
            if (ValidateExactIds(ids, profile, errors) || errors.Count == 0) throw new InvalidOperationException("Unapproved intermediate set was accepted: " + profile.Name);
        }

        private static void ExpectReadOnly(Action mutation)
        {
            try { mutation(); }
            catch (NotSupportedException) { return; }
            throw new InvalidOperationException("Profile mutation was not rejected.");
        }

        private static bool ValidReference(StaffAssetReferenceSnapshot reference)
        { return reference != null && reference.IsAssigned && !reference.IsMissing && !string.IsNullOrEmpty(reference.AssetGuid); }

        private static void ValidateExpansionReference(StaffAssetReferenceSnapshot reference, string id, bool required, List<string> errors)
        {
            if (!ValidReference(reference))
            {
                if (required || (reference != null && reference.IsMissing)) errors.Add("Expansion visual reference missing: " + id);
                return;
            }
            const string visualRoot = "Assets/Resources/StaffData/Expansion/STAFF33_92/Visuals/";
            if (!reference.AssetPath.StartsWith(visualRoot, StringComparison.Ordinal)) errors.Add("Expansion visual reference outside target root: " + id + " " + reference.AssetPath);
        }

        private static void AddGuid(HashSet<string> guids, string guid, string path, List<string> errors)
        { if (string.IsNullOrEmpty(guid) || !guids.Add(guid)) errors.Add("Missing or duplicate Asset GUID: " + path); }

        private static void AddCount(Dictionary<string, int> counts, string key)
        { int count; counts.TryGetValue(key, out count); counts[key] = count + 1; }

        private static void ValidateDistribution(string name, Dictionary<string, int> actual, IReadOnlyDictionary<string, int> expected, List<string> errors)
        {
            foreach (KeyValuePair<string, int> item in expected)
            {
                int count; actual.TryGetValue(item.Key, out count);
                if (count != item.Value) errors.Add(name + " " + item.Key + " expected " + item.Value + ", actual " + count);
            }
            foreach (string key in actual.Keys) if (!expected.ContainsKey(key)) errors.Add("Unexpected " + name + ": " + key);
        }
    }
}
