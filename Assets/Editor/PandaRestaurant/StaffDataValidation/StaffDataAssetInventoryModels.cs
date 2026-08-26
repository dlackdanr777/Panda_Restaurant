using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal sealed class StaffAssetReferenceSnapshot
    {
        internal string FieldPath { get; }
        internal bool IsAssigned { get; }
        internal bool IsMissing { get; }
        internal string AssetPath { get; }
        internal string AssetGuid { get; }
        internal string ObjectName { get; }
        internal string ObjectTypeName { get; }

        internal StaffAssetReferenceSnapshot(
            string fieldPath,
            bool isAssigned,
            bool isMissing,
            string assetPath,
            string assetGuid,
            string objectName,
            string objectTypeName)
        {
            FieldPath = fieldPath ?? string.Empty;
            IsAssigned = isAssigned;
            IsMissing = isMissing;
            AssetPath = assetPath ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            ObjectName = objectName ?? string.Empty;
            ObjectTypeName = objectTypeName ?? string.Empty;
        }
    }

    internal sealed class StaffLevelAssetSnapshot
    {
        internal int LevelNumber { get; }
        internal int UpgradeMinScore { get; }
        internal string UpgradeMoneyTypeName { get; }
        internal int UpgradeMoneyTypeValue { get; }
        internal int UpgradePrice { get; }
        internal float? AddSpeed { get; }
        internal float? CleaningTime { get; }
        internal float? FoodSpeedAddPercent { get; }
        internal float? CustomerGuideTime { get; }
        internal float? MarketingTime { get; }
        internal float? ActionTime { get; }

        internal StaffLevelAssetSnapshot(
            int levelNumber,
            int upgradeMinScore,
            string upgradeMoneyTypeName,
            int upgradeMoneyTypeValue,
            int upgradePrice,
            float? addSpeed,
            float? cleaningTime,
            float? foodSpeedAddPercent,
            float? customerGuideTime,
            float? marketingTime,
            float? actionTime)
        {
            LevelNumber = levelNumber;
            UpgradeMinScore = upgradeMinScore;
            UpgradeMoneyTypeName = upgradeMoneyTypeName ?? string.Empty;
            UpgradeMoneyTypeValue = upgradeMoneyTypeValue;
            UpgradePrice = upgradePrice;
            AddSpeed = addSpeed;
            CleaningTime = cleaningTime;
            FoodSpeedAddPercent = foodSpeedAddPercent;
            CustomerGuideTime = customerGuideTime;
            MarketingTime = marketingTime;
            ActionTime = actionTime;
        }
    }

    internal sealed class StaffDataAssetSnapshot
    {
        private readonly IReadOnlyList<StaffLevelAssetSnapshot> _levels;
        private readonly IReadOnlyList<StaffAssetReferenceSnapshot> _idleSpriteReferences;
        private readonly IReadOnlyList<StaffAssetReferenceSnapshot> _particleSpriteReferences;

        internal string AssetPath { get; }
        internal string AssetGuid { get; }
        internal string FileName { get; }
        internal string ScriptAssetPath { get; }
        internal string ScriptGuid { get; }
        internal string UnityObjectName { get; }
        internal string Id { get; }
        internal string Name { get; }
        internal string Description { get; }
        internal string ConcreteTypeName { get; }
        internal string RoleKey { get; }
        internal string RankName { get; }
        internal int RankValue { get; }
        internal float Speed { get; }
        internal string SalesLocationTypeName { get; }
        internal int SalesLocationTypeValue { get; }
        internal string MoneyTypeName { get; }
        internal int MoneyTypeValue { get; }
        internal int BuyScore { get; }
        internal int BuyPrice { get; }
        internal string LevelArrayPropertyPath { get; }
        internal int LevelCount { get { return _levels.Count; } }
        internal IReadOnlyList<StaffLevelAssetSnapshot> Levels { get { return _levels; } }
        internal StaffAssetReferenceSnapshot SkillReference { get; }
        internal string SkillConcreteTypeName { get; }
        internal float SkillDuration { get; }
        internal float SkillCooldown { get; }
        internal StaffAssetReferenceSnapshot SpriteReference { get; }
        internal StaffAssetReferenceSnapshot ThumbnailReference { get; }
        internal StaffAssetReferenceSnapshot AnimatorControllerReference { get; }
        internal IReadOnlyList<StaffAssetReferenceSnapshot> IdleSpriteReferences
        {
            get { return _idleSpriteReferences; }
        }

        internal StaffAssetReferenceSnapshot BackSpriteReference { get; }
        internal StaffAssetReferenceSnapshot HandSpriteReference { get; }
        internal float HandOffsetX { get; }
        internal float HandOffsetY { get; }
        internal StaffAssetReferenceSnapshot UiSpriteReference { get; }
        internal StaffAssetReferenceSnapshot AnimationSpriteReference { get; }
        internal int ParticleCount { get; }
        internal IReadOnlyList<StaffAssetReferenceSnapshot> ParticleSpriteReferences
        {
            get { return _particleSpriteReferences; }
        }

        internal bool HasExpectedLevelArray { get; }
        internal bool HasChefAddSpeedField { get; }
        internal bool HasMissingRequiredReference { get; }
        internal bool FileNameMatchesId { get; }

        internal StaffDataAssetSnapshot(
            string assetPath,
            string assetGuid,
            string fileName,
            string scriptAssetPath,
            string scriptGuid,
            string unityObjectName,
            string id,
            string name,
            string description,
            string concreteTypeName,
            string roleKey,
            string rankName,
            int rankValue,
            float speed,
            string salesLocationTypeName,
            int salesLocationTypeValue,
            string moneyTypeName,
            int moneyTypeValue,
            int buyScore,
            int buyPrice,
            string levelArrayPropertyPath,
            IEnumerable<StaffLevelAssetSnapshot> levels,
            StaffAssetReferenceSnapshot skillReference,
            string skillConcreteTypeName,
            float skillDuration,
            float skillCooldown,
            StaffAssetReferenceSnapshot spriteReference,
            StaffAssetReferenceSnapshot thumbnailReference,
            StaffAssetReferenceSnapshot animatorControllerReference,
            IEnumerable<StaffAssetReferenceSnapshot> idleSpriteReferences,
            StaffAssetReferenceSnapshot backSpriteReference,
            StaffAssetReferenceSnapshot handSpriteReference,
            float handOffsetX,
            float handOffsetY,
            StaffAssetReferenceSnapshot uiSpriteReference,
            StaffAssetReferenceSnapshot animationSpriteReference,
            int particleCount,
            IEnumerable<StaffAssetReferenceSnapshot> particleSpriteReferences,
            bool hasExpectedLevelArray,
            bool hasChefAddSpeedField,
            bool hasMissingRequiredReference,
            bool fileNameMatchesId)
        {
            AssetPath = assetPath ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            FileName = fileName ?? string.Empty;
            ScriptAssetPath = scriptAssetPath ?? string.Empty;
            ScriptGuid = scriptGuid ?? string.Empty;
            UnityObjectName = unityObjectName ?? string.Empty;
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            ConcreteTypeName = concreteTypeName ?? string.Empty;
            RoleKey = roleKey ?? string.Empty;
            RankName = rankName ?? string.Empty;
            RankValue = rankValue;
            Speed = speed;
            SalesLocationTypeName = salesLocationTypeName ?? string.Empty;
            SalesLocationTypeValue = salesLocationTypeValue;
            MoneyTypeName = moneyTypeName ?? string.Empty;
            MoneyTypeValue = moneyTypeValue;
            BuyScore = buyScore;
            BuyPrice = buyPrice;
            LevelArrayPropertyPath = levelArrayPropertyPath ?? string.Empty;
            SkillReference = skillReference;
            SkillConcreteTypeName = skillConcreteTypeName ?? string.Empty;
            SkillDuration = skillDuration;
            SkillCooldown = skillCooldown;
            SpriteReference = spriteReference;
            ThumbnailReference = thumbnailReference;
            AnimatorControllerReference = animatorControllerReference;
            BackSpriteReference = backSpriteReference;
            HandSpriteReference = handSpriteReference;
            HandOffsetX = handOffsetX;
            HandOffsetY = handOffsetY;
            UiSpriteReference = uiSpriteReference;
            AnimationSpriteReference = animationSpriteReference;
            ParticleCount = particleCount;
            HasExpectedLevelArray = hasExpectedLevelArray;
            HasChefAddSpeedField = hasChefAddSpeedField;
            HasMissingRequiredReference = hasMissingRequiredReference;
            FileNameMatchesId = fileNameMatchesId;

            _levels = CopyReadOnly(levels, nameof(levels));
            _idleSpriteReferences = CopyReadOnly(idleSpriteReferences, nameof(idleSpriteReferences));
            _particleSpriteReferences = CopyReadOnly(
                particleSpriteReferences,
                nameof(particleSpriteReferences));
        }

        private static IReadOnlyList<T> CopyReadOnly<T>(IEnumerable<T> values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return new ReadOnlyCollection<T>(new List<T>(values));
        }
    }

    internal sealed class StaffSkillAssetSnapshot
    {
        private readonly IReadOnlyList<string> _referencedStaffIds;

        internal string AssetPath { get; }
        internal string AssetGuid { get; }
        internal string FileName { get; }
        internal string UnityObjectName { get; }
        internal string ScriptAssetPath { get; }
        internal string ScriptGuid { get; }
        internal string ConcreteTypeName { get; }
        internal string Description { get; }
        internal float Duration { get; }
        internal float Cooldown { get; }
        internal IReadOnlyList<string> ReferencedStaffIds { get { return _referencedStaffIds; } }
        internal int ReferenceCount { get { return _referencedStaffIds.Count; } }
        internal bool IsOrphan { get; }
        internal bool IsShared { get; }
        internal bool HasMissingScript { get; }
        internal bool HasMissingSerializedReference { get; }

        internal StaffSkillAssetSnapshot(
            string assetPath,
            string assetGuid,
            string fileName,
            string unityObjectName,
            string scriptAssetPath,
            string scriptGuid,
            string concreteTypeName,
            string description,
            float duration,
            float cooldown,
            IEnumerable<string> referencedStaffIds,
            bool hasMissingScript,
            bool hasMissingSerializedReference)
        {
            if (referencedStaffIds == null)
            {
                throw new ArgumentNullException(nameof(referencedStaffIds));
            }

            AssetPath = assetPath ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            FileName = fileName ?? string.Empty;
            UnityObjectName = unityObjectName ?? string.Empty;
            ScriptAssetPath = scriptAssetPath ?? string.Empty;
            ScriptGuid = scriptGuid ?? string.Empty;
            ConcreteTypeName = concreteTypeName ?? string.Empty;
            Description = description ?? string.Empty;
            Duration = duration;
            Cooldown = cooldown;
            List<string> staffIds = new List<string>(referencedStaffIds);
            staffIds.Sort(StringComparer.Ordinal);
            _referencedStaffIds = new ReadOnlyCollection<string>(staffIds);
            IsOrphan = staffIds.Count == 0;
            IsShared = staffIds.Count > 1;
            HasMissingScript = hasMissingScript;
            HasMissingSerializedReference = hasMissingSerializedReference;
        }
    }

    internal sealed class StaffDataAssetInventorySnapshot
    {
        private readonly IReadOnlyList<StaffDataAssetSnapshot> _staff;
        private readonly IReadOnlyList<StaffSkillAssetSnapshot> _skills;
        private readonly IReadOnlyDictionary<string, StaffDataAssetSnapshot> _staffById;
        private readonly IReadOnlyDictionary<string, StaffSkillAssetSnapshot> _skillByGuid;

        internal string InventoryFingerprint { get; }
        internal IReadOnlyList<StaffDataAssetSnapshot> Staff { get { return _staff; } }
        internal IReadOnlyList<StaffSkillAssetSnapshot> Skills { get { return _skills; } }
        internal IReadOnlyDictionary<string, StaffDataAssetSnapshot> StaffById
        {
            get { return _staffById; }
        }

        internal IReadOnlyDictionary<string, StaffSkillAssetSnapshot> SkillByGuid
        {
            get { return _skillByGuid; }
        }

        internal StaffDataAssetInventorySnapshot(
            IEnumerable<StaffDataAssetSnapshot> staff,
            IEnumerable<StaffSkillAssetSnapshot> skills)
        {
            if (staff == null)
            {
                throw new ArgumentNullException(nameof(staff));
            }

            if (skills == null)
            {
                throw new ArgumentNullException(nameof(skills));
            }

            List<StaffDataAssetSnapshot> staffCopy = new List<StaffDataAssetSnapshot>(staff);
            staffCopy.Sort(CompareStaff);
            List<StaffSkillAssetSnapshot> skillCopy = new List<StaffSkillAssetSnapshot>(skills);
            skillCopy.Sort(CompareSkills);

            Dictionary<string, StaffDataAssetSnapshot> staffById =
                new Dictionary<string, StaffDataAssetSnapshot>(StringComparer.Ordinal);
            for (int index = 0; index < staffCopy.Count; index++)
            {
                staffById.Add(staffCopy[index].Id, staffCopy[index]);
            }

            Dictionary<string, StaffSkillAssetSnapshot> skillByGuid =
                new Dictionary<string, StaffSkillAssetSnapshot>(StringComparer.Ordinal);
            for (int index = 0; index < skillCopy.Count; index++)
            {
                skillByGuid.Add(skillCopy[index].AssetGuid, skillCopy[index]);
            }

            _staff = new ReadOnlyCollection<StaffDataAssetSnapshot>(staffCopy);
            _skills = new ReadOnlyCollection<StaffSkillAssetSnapshot>(skillCopy);
            _staffById = new ReadOnlyDictionary<string, StaffDataAssetSnapshot>(staffById);
            _skillByGuid = new ReadOnlyDictionary<string, StaffSkillAssetSnapshot>(skillByGuid);
            InventoryFingerprint = BuildInventoryFingerprint(staffCopy, skillCopy);
        }

        internal bool TryGetStaff(string id, out StaffDataAssetSnapshot staff)
        {
            return _staffById.TryGetValue(id, out staff);
        }

        internal bool TryGetSkill(string guid, out StaffSkillAssetSnapshot skill)
        {
            return _skillByGuid.TryGetValue(guid, out skill);
        }

        private static int CompareStaff(StaffDataAssetSnapshot left, StaffDataAssetSnapshot right)
        {
            int idComparison = string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            return idComparison != 0
                ? idComparison
                : string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
        }

        private static int CompareSkills(StaffSkillAssetSnapshot left, StaffSkillAssetSnapshot right)
        {
            int guidComparison = string.Compare(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal);
            return guidComparison != 0
                ? guidComparison
                : string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
        }

        private static string BuildInventoryFingerprint(
            IReadOnlyList<StaffDataAssetSnapshot> staff,
            IReadOnlyList<StaffSkillAssetSnapshot> skills)
        {
            StringBuilder input = new StringBuilder();
            AppendValue(input, "STAFF_COUNT");
            AppendValue(input, staff.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < staff.Count; index++)
            {
                AppendStaff(input, staff[index]);
            }

            AppendValue(input, "SKILL_COUNT");
            AppendValue(input, skills.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < skills.Count; index++)
            {
                AppendSkill(input, skills[index]);
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input.ToString()));
                StringBuilder fingerprint = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    fingerprint.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return fingerprint.ToString();
            }
        }

        private static void AppendStaff(StringBuilder input, StaffDataAssetSnapshot staff)
        {
            AppendValue(input, "STAFF");
            AppendValue(input, staff.AssetPath);
            AppendValue(input, staff.AssetGuid);
            AppendValue(input, staff.ScriptGuid);
            AppendValue(input, staff.Id);
            AppendValue(input, staff.Name);
            AppendValue(input, staff.Description);
            AppendValue(input, staff.ConcreteTypeName);
            AppendValue(input, staff.RoleKey);
            AppendValue(input, staff.RankName);
            AppendValue(input, staff.RankValue.ToString(CultureInfo.InvariantCulture));
            AppendFloat(input, staff.Speed);
            AppendValue(input, staff.SalesLocationTypeName);
            AppendValue(input, staff.SalesLocationTypeValue.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, staff.MoneyTypeName);
            AppendValue(input, staff.MoneyTypeValue.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, staff.BuyScore.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, staff.BuyPrice.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, staff.LevelCount.ToString(CultureInfo.InvariantCulture));
            for (int levelIndex = 0; levelIndex < staff.Levels.Count; levelIndex++)
            {
                AppendLevel(input, staff.Levels[levelIndex]);
            }

            AppendReference(input, staff.SkillReference);
            AppendReference(input, staff.SpriteReference);
            AppendReference(input, staff.ThumbnailReference);
            AppendReference(input, staff.AnimatorControllerReference);
            AppendReferences(input, staff.IdleSpriteReferences);
            AppendReference(input, staff.BackSpriteReference);
            AppendReference(input, staff.HandSpriteReference);
            AppendFloat(input, staff.HandOffsetX);
            AppendFloat(input, staff.HandOffsetY);
            AppendReference(input, staff.UiSpriteReference);
            AppendReference(input, staff.AnimationSpriteReference);
            AppendValue(input, staff.ParticleCount.ToString(CultureInfo.InvariantCulture));
            AppendReferences(input, staff.ParticleSpriteReferences);
        }

        private static void AppendLevel(StringBuilder input, StaffLevelAssetSnapshot level)
        {
            AppendValue(input, level.LevelNumber.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, level.UpgradeMinScore.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, level.UpgradeMoneyTypeName);
            AppendValue(input, level.UpgradeMoneyTypeValue.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, level.UpgradePrice.ToString(CultureInfo.InvariantCulture));
            AppendNullableFloat(input, level.AddSpeed);
            AppendNullableFloat(input, level.CleaningTime);
            AppendNullableFloat(input, level.FoodSpeedAddPercent);
            AppendNullableFloat(input, level.CustomerGuideTime);
            AppendNullableFloat(input, level.MarketingTime);
            AppendNullableFloat(input, level.ActionTime);
        }

        private static void AppendSkill(StringBuilder input, StaffSkillAssetSnapshot skill)
        {
            AppendValue(input, "SKILL");
            AppendValue(input, skill.AssetPath);
            AppendValue(input, skill.AssetGuid);
            AppendValue(input, skill.ScriptGuid);
            AppendValue(input, skill.ConcreteTypeName);
            AppendValue(input, skill.Description);
            AppendFloat(input, skill.Duration);
            AppendFloat(input, skill.Cooldown);
            AppendValue(input, skill.ReferenceCount.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < skill.ReferencedStaffIds.Count; index++)
            {
                AppendValue(input, skill.ReferencedStaffIds[index]);
            }
        }

        private static void AppendReferences(
            StringBuilder input,
            IReadOnlyList<StaffAssetReferenceSnapshot> references)
        {
            AppendValue(input, references.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < references.Count; index++)
            {
                AppendReference(input, references[index]);
            }
        }

        private static void AppendReference(StringBuilder input, StaffAssetReferenceSnapshot reference)
        {
            if (reference == null)
            {
                AppendValue(input, "<null>");
                return;
            }

            AppendValue(input, reference.FieldPath);
            AppendValue(input, reference.IsAssigned ? "1" : "0");
            AppendValue(input, reference.IsMissing ? "1" : "0");
            AppendValue(input, reference.AssetGuid);
        }

        private static void AppendNullableFloat(StringBuilder input, float? value)
        {
            AppendValue(
                input,
                value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "<null>");
        }

        private static void AppendFloat(StringBuilder input, float value)
        {
            AppendValue(input, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendValue(StringBuilder input, string value)
        {
            string safeValue = value ?? string.Empty;
            input.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            input.Append(':');
            input.Append(safeValue);
            input.Append('\n');
        }
    }
}
