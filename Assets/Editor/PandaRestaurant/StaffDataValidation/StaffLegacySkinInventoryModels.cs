using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal sealed class StaffLegacyVisualReferenceSnapshot
    {
        internal string Category { get; }
        internal string AssetPath { get; }
        internal string AssetGuid { get; }
        internal long LocalFileId { get; }
        internal string ObjectName { get; }
        internal string ObjectTypeName { get; }
        internal int FrameIndex { get; }
        internal bool IsAssigned { get; }
        internal bool IsMissing { get; }

        internal StaffLegacyVisualReferenceSnapshot(
            string category,
            string assetPath,
            string assetGuid,
            long localFileId,
            string objectName,
            string objectTypeName,
            int frameIndex,
            bool isAssigned,
            bool isMissing)
        {
            Category = category ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            LocalFileId = localFileId;
            ObjectName = objectName ?? string.Empty;
            ObjectTypeName = objectTypeName ?? string.Empty;
            FrameIndex = frameIndex;
            IsAssigned = isAssigned;
            IsMissing = isMissing;
        }
    }

    internal sealed class StaffLegacySkinRowSnapshot
    {
        private readonly IReadOnlyList<string> _rawCells;
        private readonly IReadOnlyList<StaffLegacyVisualReferenceSnapshot> _idleFrames;
        private readonly IReadOnlyList<StaffLegacyVisualReferenceSnapshot> _cheerleaderParticleSprites;

        internal string LegacySkinId { get; }
        internal int LegacySkinNumber { get; }
        internal string CandidateNewStaffId { get; }
        internal string Name { get; }
        internal string Description { get; }
        internal string EquipTargetStaffId { get; }
        internal string EquipTargetRoleKey { get; }
        internal string GachaProbabilityRaw { get; }
        internal string AddScoreRaw { get; }
        internal string AddTipPerMinuteRaw { get; }
        internal string RarityStarsRaw { get; }
        internal string GradeRaw { get; }
        internal string PurchaseCurrencyRaw { get; }
        internal string PurchasePriceRaw { get; }
        internal string LegacyUpgradeTypeId { get; }
        internal string LegacyUpgradeValueRaw { get; }
        internal string LegacyDuplicationTokenRaw { get; }
        internal IReadOnlyList<string> RawCells { get { return _rawCells; } }
        internal StaffLegacyVisualReferenceSnapshot MainSprite { get; }
        internal StaffLegacyVisualReferenceSnapshot ThumbnailSprite { get; }
        internal IReadOnlyList<StaffLegacyVisualReferenceSnapshot> IdleFrames
        {
            get { return _idleFrames; }
        }

        internal StaffLegacyVisualReferenceSnapshot ChefBackSprite { get; }
        internal StaffLegacyVisualReferenceSnapshot ChefHandSprite { get; }
        internal StaffLegacyVisualReferenceSnapshot CheerleaderAnimationSprite { get; }
        internal IReadOnlyList<StaffLegacyVisualReferenceSnapshot> CheerleaderParticleSprites
        {
            get { return _cheerleaderParticleSprites; }
        }

        internal StaffLegacyVisualReferenceSnapshot AnimatorControllerCandidate { get; }
        internal bool HasMainSprite { get; }
        internal bool HasThumbnail { get; }
        internal bool HasIdleFrames { get; }
        internal bool HasChefParts { get; }
        internal bool HasCheerleaderParts { get; }
        internal bool HasAnimatorController { get; }
        internal bool HasMissingReference { get; }
        internal bool RequiresStaticIdleFallback { get; }
        internal bool CandidateMappingIsSequential { get; }
        internal bool SourceRoleResolved { get; }

        internal StaffLegacySkinRowSnapshot(
            string legacySkinId,
            int legacySkinNumber,
            string candidateNewStaffId,
            string name,
            string description,
            string equipTargetStaffId,
            string equipTargetRoleKey,
            string gachaProbabilityRaw,
            string addScoreRaw,
            string addTipPerMinuteRaw,
            string rarityStarsRaw,
            string gradeRaw,
            string purchaseCurrencyRaw,
            string purchasePriceRaw,
            string legacyUpgradeTypeId,
            string legacyUpgradeValueRaw,
            string legacyDuplicationTokenRaw,
            IEnumerable<string> rawCells,
            StaffLegacyVisualReferenceSnapshot mainSprite,
            StaffLegacyVisualReferenceSnapshot thumbnailSprite,
            IEnumerable<StaffLegacyVisualReferenceSnapshot> idleFrames,
            StaffLegacyVisualReferenceSnapshot chefBackSprite,
            StaffLegacyVisualReferenceSnapshot chefHandSprite,
            StaffLegacyVisualReferenceSnapshot cheerleaderAnimationSprite,
            IEnumerable<StaffLegacyVisualReferenceSnapshot> cheerleaderParticleSprites,
            StaffLegacyVisualReferenceSnapshot animatorControllerCandidate,
            bool candidateMappingIsSequential,
            bool sourceRoleResolved)
        {
            if (rawCells == null)
            {
                throw new ArgumentNullException(nameof(rawCells));
            }

            if (idleFrames == null)
            {
                throw new ArgumentNullException(nameof(idleFrames));
            }

            if (cheerleaderParticleSprites == null)
            {
                throw new ArgumentNullException(nameof(cheerleaderParticleSprites));
            }

            LegacySkinId = legacySkinId ?? string.Empty;
            LegacySkinNumber = legacySkinNumber;
            CandidateNewStaffId = candidateNewStaffId ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            EquipTargetStaffId = equipTargetStaffId ?? string.Empty;
            EquipTargetRoleKey = equipTargetRoleKey ?? string.Empty;
            GachaProbabilityRaw = gachaProbabilityRaw ?? string.Empty;
            AddScoreRaw = addScoreRaw ?? string.Empty;
            AddTipPerMinuteRaw = addTipPerMinuteRaw ?? string.Empty;
            RarityStarsRaw = rarityStarsRaw ?? string.Empty;
            GradeRaw = gradeRaw ?? string.Empty;
            PurchaseCurrencyRaw = purchaseCurrencyRaw ?? string.Empty;
            PurchasePriceRaw = purchasePriceRaw ?? string.Empty;
            LegacyUpgradeTypeId = legacyUpgradeTypeId ?? string.Empty;
            LegacyUpgradeValueRaw = legacyUpgradeValueRaw ?? string.Empty;
            LegacyDuplicationTokenRaw = legacyDuplicationTokenRaw ?? string.Empty;
            _rawCells = new ReadOnlyCollection<string>(new List<string>(rawCells));
            MainSprite = mainSprite;
            ThumbnailSprite = thumbnailSprite;
            _idleFrames = new ReadOnlyCollection<StaffLegacyVisualReferenceSnapshot>(
                new List<StaffLegacyVisualReferenceSnapshot>(idleFrames));
            ChefBackSprite = chefBackSprite;
            ChefHandSprite = chefHandSprite;
            CheerleaderAnimationSprite = cheerleaderAnimationSprite;
            _cheerleaderParticleSprites = new ReadOnlyCollection<StaffLegacyVisualReferenceSnapshot>(
                new List<StaffLegacyVisualReferenceSnapshot>(cheerleaderParticleSprites));
            AnimatorControllerCandidate = animatorControllerCandidate;
            HasMainSprite = IsUsable(MainSprite);
            HasThumbnail = IsUsable(ThumbnailSprite);
            HasIdleFrames = HasUsable(_idleFrames);
            HasChefParts = IsUsable(ChefBackSprite) && IsUsable(ChefHandSprite);
            HasCheerleaderParts = IsUsable(CheerleaderAnimationSprite)
                                  && HasUsable(_cheerleaderParticleSprites);
            HasAnimatorController = IsUsable(AnimatorControllerCandidate);
            RequiresStaticIdleFallback = !HasIdleFrames;
            HasMissingReference = HasMissing(
                MainSprite,
                ThumbnailSprite,
                _idleFrames,
                ChefBackSprite,
                ChefHandSprite,
                CheerleaderAnimationSprite,
                _cheerleaderParticleSprites,
                AnimatorControllerCandidate);
            CandidateMappingIsSequential = candidateMappingIsSequential;
            SourceRoleResolved = sourceRoleResolved;
        }

        private static bool IsUsable(StaffLegacyVisualReferenceSnapshot reference)
        {
            return reference != null && reference.IsAssigned && !reference.IsMissing;
        }

        private static bool HasUsable(IReadOnlyList<StaffLegacyVisualReferenceSnapshot> references)
        {
            for (int index = 0; index < references.Count; index++)
            {
                if (IsUsable(references[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasMissing(
            StaffLegacyVisualReferenceSnapshot main,
            StaffLegacyVisualReferenceSnapshot thumbnail,
            IReadOnlyList<StaffLegacyVisualReferenceSnapshot> idle,
            StaffLegacyVisualReferenceSnapshot chefBack,
            StaffLegacyVisualReferenceSnapshot chefHand,
            StaffLegacyVisualReferenceSnapshot cheerAnimation,
            IReadOnlyList<StaffLegacyVisualReferenceSnapshot> particles,
            StaffLegacyVisualReferenceSnapshot animator)
        {
            if (IsMissing(main)
                || IsMissing(thumbnail)
                || IsMissing(chefBack)
                || IsMissing(chefHand)
                || IsMissing(cheerAnimation)
                || IsMissing(animator))
            {
                return true;
            }

            for (int index = 0; index < idle.Count; index++)
            {
                if (IsMissing(idle[index]))
                {
                    return true;
                }
            }

            for (int index = 0; index < particles.Count; index++)
            {
                if (IsMissing(particles[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMissing(StaffLegacyVisualReferenceSnapshot reference)
        {
            return reference != null && reference.IsMissing;
        }
    }

    internal sealed class StaffLegacySkinInventorySnapshot
    {
        private readonly IReadOnlyList<string> _csvHeaders;
        private readonly IReadOnlyList<StaffLegacySkinRowSnapshot> _legacySkins;
        private readonly IReadOnlyDictionary<string, StaffLegacySkinRowSnapshot> _legacySkinById;
        private readonly IReadOnlyDictionary<string, StaffLegacySkinRowSnapshot> _candidateStaffId;
        private readonly IReadOnlyList<StaffLegacyVisualReferenceSnapshot> _idleNamingMismatches;
        private readonly IReadOnlyList<string> _diagnostics;

        internal string CsvAssetPath { get; }
        internal string CsvAssetGuid { get; }
        internal string CsvSha256 { get; }
        internal IReadOnlyList<string> CsvHeaders { get { return _csvHeaders; } }
        internal IReadOnlyList<StaffLegacySkinRowSnapshot> LegacySkins { get { return _legacySkins; } }
        internal IReadOnlyDictionary<string, StaffLegacySkinRowSnapshot> LegacySkinById
        {
            get { return _legacySkinById; }
        }

        internal IReadOnlyDictionary<string, StaffLegacySkinRowSnapshot> CandidateStaffId
        {
            get { return _candidateStaffId; }
        }

        internal IReadOnlyList<StaffLegacyVisualReferenceSnapshot> IdleNamingMismatches
        {
            get { return _idleNamingMismatches; }
        }

        internal string InventoryFingerprint { get; }
        internal IReadOnlyList<string> Diagnostics { get { return _diagnostics; } }

        internal StaffLegacySkinInventorySnapshot(
            string csvAssetPath,
            string csvAssetGuid,
            string csvSha256,
            IEnumerable<string> csvHeaders,
            IEnumerable<StaffLegacySkinRowSnapshot> legacySkins,
            IEnumerable<StaffLegacyVisualReferenceSnapshot> idleNamingMismatches,
            IEnumerable<string> diagnostics)
        {
            if (csvHeaders == null)
            {
                throw new ArgumentNullException(nameof(csvHeaders));
            }

            if (legacySkins == null)
            {
                throw new ArgumentNullException(nameof(legacySkins));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (idleNamingMismatches == null)
            {
                throw new ArgumentNullException(nameof(idleNamingMismatches));
            }

            CsvAssetPath = csvAssetPath ?? string.Empty;
            CsvAssetGuid = csvAssetGuid ?? string.Empty;
            CsvSha256 = csvSha256 ?? string.Empty;
            _csvHeaders = new ReadOnlyCollection<string>(new List<string>(csvHeaders));

            List<StaffLegacySkinRowSnapshot> skinCopy =
                new List<StaffLegacySkinRowSnapshot>(legacySkins);
            skinCopy.Sort(CompareSkins);
            Dictionary<string, StaffLegacySkinRowSnapshot> byLegacyId =
                new Dictionary<string, StaffLegacySkinRowSnapshot>(StringComparer.Ordinal);
            Dictionary<string, StaffLegacySkinRowSnapshot> byCandidateId =
                new Dictionary<string, StaffLegacySkinRowSnapshot>(StringComparer.Ordinal);
            for (int index = 0; index < skinCopy.Count; index++)
            {
                StaffLegacySkinRowSnapshot skin = skinCopy[index];
                byLegacyId.Add(skin.LegacySkinId, skin);
                byCandidateId.Add(skin.CandidateNewStaffId, skin);
            }

            _legacySkins = new ReadOnlyCollection<StaffLegacySkinRowSnapshot>(skinCopy);
            _legacySkinById =
                new ReadOnlyDictionary<string, StaffLegacySkinRowSnapshot>(byLegacyId);
            _candidateStaffId =
                new ReadOnlyDictionary<string, StaffLegacySkinRowSnapshot>(byCandidateId);
            List<StaffLegacyVisualReferenceSnapshot> mismatchCopy =
                new List<StaffLegacyVisualReferenceSnapshot>(idleNamingMismatches);
            mismatchCopy.Sort(CompareVisuals);
            _idleNamingMismatches =
                new ReadOnlyCollection<StaffLegacyVisualReferenceSnapshot>(mismatchCopy);
            _diagnostics = new ReadOnlyCollection<string>(new List<string>(diagnostics));
            InventoryFingerprint = BuildInventoryFingerprint(
                CsvAssetPath,
                CsvAssetGuid,
                CsvSha256,
                _csvHeaders,
                _legacySkins,
                _idleNamingMismatches);
        }

        private static int CompareSkins(
            StaffLegacySkinRowSnapshot left,
            StaffLegacySkinRowSnapshot right)
        {
            int numberComparison = left.LegacySkinNumber.CompareTo(right.LegacySkinNumber);
            return numberComparison != 0
                ? numberComparison
                : string.Compare(left.LegacySkinId, right.LegacySkinId, StringComparison.Ordinal);
        }

        private static string BuildInventoryFingerprint(
            string csvAssetPath,
            string csvAssetGuid,
            string csvSha256,
            IReadOnlyList<string> csvHeaders,
            IReadOnlyList<StaffLegacySkinRowSnapshot> skins,
            IReadOnlyList<StaffLegacyVisualReferenceSnapshot> idleNamingMismatches)
        {
            StringBuilder input = new StringBuilder();
            AppendValue(input, "CSV");
            AppendValue(input, csvAssetPath);
            AppendValue(input, csvAssetGuid);
            AppendValue(input, csvSha256);
            AppendValues(input, csvHeaders);
            AppendValue(input, skins.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < skins.Count; index++)
            {
                AppendSkin(input, skins[index]);
            }

            AppendValue(
                input,
                idleNamingMismatches.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < idleNamingMismatches.Count; index++)
            {
                AppendVisual(input, idleNamingMismatches[index]);
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

        private static void AppendSkin(StringBuilder input, StaffLegacySkinRowSnapshot skin)
        {
            AppendValue(input, "LEGACY_SKIN");
            AppendValue(input, skin.LegacySkinId);
            AppendValue(input, skin.LegacySkinNumber.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, skin.CandidateNewStaffId);
            AppendValue(input, skin.Name);
            AppendValue(input, skin.Description);
            AppendValue(input, skin.EquipTargetStaffId);
            AppendValue(input, skin.EquipTargetRoleKey);
            AppendValue(input, skin.GachaProbabilityRaw);
            AppendValue(input, skin.AddScoreRaw);
            AppendValue(input, skin.AddTipPerMinuteRaw);
            AppendValue(input, skin.RarityStarsRaw);
            AppendValue(input, skin.GradeRaw);
            AppendValue(input, skin.PurchaseCurrencyRaw);
            AppendValue(input, skin.PurchasePriceRaw);
            AppendValue(input, skin.LegacyUpgradeTypeId);
            AppendValue(input, skin.LegacyUpgradeValueRaw);
            AppendValue(input, skin.LegacyDuplicationTokenRaw);
            AppendValues(input, skin.RawCells);

            List<StaffLegacyVisualReferenceSnapshot> visuals =
                new List<StaffLegacyVisualReferenceSnapshot>
                {
                    skin.MainSprite,
                    skin.ThumbnailSprite,
                    skin.ChefBackSprite,
                    skin.ChefHandSprite,
                    skin.CheerleaderAnimationSprite,
                    skin.AnimatorControllerCandidate
                };
            visuals.AddRange(skin.IdleFrames);
            visuals.AddRange(skin.CheerleaderParticleSprites);
            visuals.Sort(CompareVisuals);
            AppendValue(input, visuals.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < visuals.Count; index++)
            {
                AppendVisual(input, visuals[index]);
            }
        }

        private static int CompareVisuals(
            StaffLegacyVisualReferenceSnapshot left,
            StaffLegacyVisualReferenceSnapshot right)
        {
            int comparison = string.Compare(left.Category, right.Category, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.FrameIndex.CompareTo(right.FrameIndex);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = left.LocalFileId.CompareTo(right.LocalFileId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.AssetPath, right.AssetPath, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(left.ObjectName, right.ObjectName, StringComparison.Ordinal);
        }

        private static void AppendVisual(
            StringBuilder input,
            StaffLegacyVisualReferenceSnapshot visual)
        {
            if (visual == null)
            {
                AppendValue(input, "NULL_VISUAL");
                return;
            }

            AppendValue(input, visual.Category);
            AppendValue(input, visual.AssetPath);
            AppendValue(input, visual.AssetGuid);
            AppendValue(input, visual.LocalFileId.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, visual.ObjectName);
            AppendValue(input, visual.ObjectTypeName);
            AppendValue(input, visual.FrameIndex.ToString(CultureInfo.InvariantCulture));
            AppendValue(input, visual.IsAssigned ? "1" : "0");
            AppendValue(input, visual.IsMissing ? "1" : "0");
        }

        private static void AppendValues(StringBuilder input, IReadOnlyList<string> values)
        {
            AppendValue(input, values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                AppendValue(input, values[index]);
            }
        }

        private static void AppendValue(StringBuilder input, string value)
        {
            string safeValue = value ?? string.Empty;
            input.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            input.Append(':');
            input.Append(safeValue);
            input.Append(';');
        }
    }
}
