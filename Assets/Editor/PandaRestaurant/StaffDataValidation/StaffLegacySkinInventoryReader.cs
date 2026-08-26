using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffLegacySkinInventoryReader
    {
        internal const string LegacySkinRoot = "Assets/Resources/StaffData/Skin";
        internal const string CsvAssetPath = LegacySkinRoot + "/CSVData/StaffSkinDataList.csv";
        internal const string MainSpriteRoot = LegacySkinRoot + "/Sprites/Sprite";
        internal const string ThumbnailSpriteRoot = LegacySkinRoot + "/Sprites/Thumbnail";
        internal const string IdleSpriteRoot = LegacySkinRoot + "/Sprites/IdleSprites";
        internal const string ChefBackSpriteRoot = LegacySkinRoot + "/Sprites/Chef/Back";
        internal const string ChefHandSpriteRoot = LegacySkinRoot + "/Sprites/Chef/Hand";
        internal const string CheerleaderAnimationSpriteRoot =
            LegacySkinRoot + "/Sprites/치어리더/AnimationSprites";
        internal const string CheerleaderParticleSpriteRoot =
            LegacySkinRoot + "/Sprites/치어리더/Particles";
        internal const string AnimatorCandidateRoot = "Assets/Resources/StaffData/Animator";

        internal const string MainCategory = "MAIN";
        internal const string ThumbnailCategory = "THUMBNAIL";
        internal const string IdleCategory = "IDLE";
        internal const string IdleNamingMismatchCategory = "IDLE_NAMING_MISMATCH";
        internal const string ChefBackCategory = "CHEF_BACK";
        internal const string ChefHandCategory = "CHEF_HAND";
        internal const string CheerleaderAnimationCategory = "CHEERLEADER_ANIMATION";
        internal const string CheerleaderParticleCategory = "CHEERLEADER_PARTICLE";
        internal const string AnimatorCategory = "ANIMATOR_CONTROLLER";

        private static readonly IReadOnlyList<string> CsvHeaderLock =
            new ReadOnlyCollection<string>(new[]
            {
                "ID",
                "이름",
                "설명",
                "가챠 확률",
                "평점 증가",
                "분당 증가 팁",
                "희귀도(별)",
                "등급",
                "구매 수단",
                "금액",
                "착용시 강화 TYPE ID",
                "단위",
                "스킨 적용 ID",
                "중복시 지급 코인",
                "응원봉 크기",
                "응원봉_Left_Pos_X",
                "응원봉_Left_Pos_Y",
                "응원봉_Right_Pos_X",
                "응원봉_Right_Pos_Y",
                "비고"
            });

        internal static IReadOnlyList<string> ExpectedCsvHeaders { get { return CsvHeaderLock; } }

        internal static bool TryBuildReadOnlyInventory(
            out StaffLegacySkinInventorySnapshot snapshot,
            out IReadOnlyList<string> diagnostics)
        {
            snapshot = null;
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            StaffDataAssetInventorySnapshot staffInventory;
            IReadOnlyList<string> staffDiagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out staffInventory,
                    out staffDiagnostics))
            {
                AddChildDiagnostics("B2-1 Staff Inventory", staffDiagnostics, errors);
            }

            string csvGuid;
            string csvSha256;
            List<string> csvHeaders;
            List<LegacyCsvRow> csvRows;
            bool csvRead = TryReadLegacyCsv(
                out csvGuid,
                out csvSha256,
                out csvHeaders,
                out csvRows,
                errors);

            VisualCatalog visuals = ReadVisualCatalog(errors, warnings);
            List<StaffLegacySkinRowSnapshot> skins = new List<StaffLegacySkinRowSnapshot>();
            if (staffInventory != null && csvRead)
            {
                BuildSkinSnapshots(staffInventory, csvRows, visuals, skins, errors, warnings);
                ValidateRoleDistribution(skins, errors);
            }

            List<string> diagnosticCopy = new List<string>();
            for (int index = 0; index < warnings.Count; index++)
            {
                diagnosticCopy.Add("WARNING: " + warnings[index]);
            }

            for (int index = 0; index < errors.Count; index++)
            {
                diagnosticCopy.Add("ERROR: " + errors[index]);
            }

            diagnostics = new ReadOnlyCollection<string>(diagnosticCopy);
            if (errors.Count != 0 || skins.Count != 60)
            {
                snapshot = null;
                return false;
            }

            try
            {
                snapshot = new StaffLegacySkinInventorySnapshot(
                    CsvAssetPath,
                    csvGuid,
                    csvSha256,
                    csvHeaders,
                    skins,
                    visuals.IdleNamingMismatches,
                    diagnosticCopy);
                return true;
            }
            catch (Exception exception)
            {
                diagnosticCopy.Add("ERROR: Legacy Skin Inventory Snapshot 생성 실패: "
                                   + exception.Message);
                diagnostics = new ReadOnlyCollection<string>(diagnosticCopy);
                snapshot = null;
                return false;
            }
        }

        private static bool TryReadLegacyCsv(
            out string csvGuid,
            out string csvSha256,
            out List<string> headers,
            out List<LegacyCsvRow> rows,
            List<string> errors)
        {
            csvGuid = AssetDatabase.AssetPathToGUID(CsvAssetPath);
            csvSha256 = string.Empty;
            headers = new List<string>();
            rows = new List<LegacyCsvRow>();
            string osPath = ToAbsoluteProjectPath(CsvAssetPath);
            if (!File.Exists(osPath))
            {
                errors.Add("Legacy CSV 파일이 없습니다: " + CsvAssetPath);
                return false;
            }

            if (string.IsNullOrEmpty(csvGuid))
            {
                errors.Add("Legacy CSV GUID를 읽지 못했습니다: " + CsvAssetPath);
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(osPath);
                csvSha256 = ComputeSha256(bytes);
            }
            catch (Exception exception)
            {
                errors.Add("Legacy CSV 바이트 읽기 실패: " + exception.Message);
                return false;
            }

            bool hasUtf8Bom = bytes.Length >= 3
                              && bytes[0] == 0xef
                              && bytes[1] == 0xbb
                              && bytes[2] == 0xbf;
            if (!hasUtf8Bom)
            {
                errors.Add("Legacy CSV가 UTF-8 BOM 형식이 아닙니다.");
                return false;
            }

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
            }
            catch (DecoderFallbackException exception)
            {
                errors.Add("Legacy CSV UTF-8 디코딩 실패: " + exception.Message);
                return false;
            }

            List<List<string>> parsedRows;
            string parseError;
            if (!TryParseRfc4180(text, out parsedRows, out parseError))
            {
                errors.Add("Legacy CSV RFC4180 파싱 실패: " + parseError);
                return false;
            }

            if (parsedRows.Count == 0)
            {
                errors.Add("Legacy CSV에 Header가 없습니다.");
                return false;
            }

            headers.AddRange(parsedRows[0]);
            ValidateHeaders(headers, errors);
            if (parsedRows.Count - 1 != 60)
            {
                errors.Add("Legacy CSV 데이터 행 수가 60이 아닙니다: " + (parsedRows.Count - 1));
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> candidates = new HashSet<string>(StringComparer.Ordinal);
            for (int rowIndex = 1; rowIndex < parsedRows.Count; rowIndex++)
            {
                List<string> cells = parsedRows[rowIndex];
                if (cells.Count != CsvHeaderLock.Count)
                {
                    errors.Add("Legacy CSV " + (rowIndex + 1) + "행 Cell 수가 20이 아닙니다: "
                               + cells.Count);
                    continue;
                }

                string legacyId = cells[0];
                int legacyNumber;
                if (!TryParseLegacySkinId(legacyId, out legacyNumber))
                {
                    errors.Add("Legacy CSV ID가 SKIN_STAFF01~60 범위를 벗어납니다: "
                               + (string.IsNullOrEmpty(legacyId) ? "<EMPTY>" : legacyId));
                    continue;
                }

                if (!ids.Add(legacyId))
                {
                    errors.Add("Legacy CSV ID가 중복됩니다: " + legacyId);
                    continue;
                }

                string candidateId = ToCandidateStaffId(legacyNumber);
                if (!candidates.Add(candidateId))
                {
                    errors.Add("Candidate Staff ID가 중복됩니다: " + candidateId);
                    continue;
                }

                rows.Add(new LegacyCsvRow(legacyNumber, cells));
            }

            for (int number = 1; number <= 60; number++)
            {
                string expectedLegacyId = ToLegacySkinId(number);
                if (!ids.Contains(expectedLegacyId))
                {
                    errors.Add("Legacy CSV 필수 ID가 없습니다: " + expectedLegacyId);
                }

                string expectedCandidateId = ToCandidateStaffId(number);
                if (!candidates.Contains(expectedCandidateId))
                {
                    errors.Add("Candidate 필수 ID가 없습니다: " + expectedCandidateId);
                }
            }

            return true;
        }

        private static void ValidateHeaders(IReadOnlyList<string> headers, List<string> errors)
        {
            if (headers.Count != CsvHeaderLock.Count)
            {
                errors.Add("Legacy CSV Header 수가 20이 아닙니다: " + headers.Count);
                return;
            }

            for (int index = 0; index < CsvHeaderLock.Count; index++)
            {
                if (!string.Equals(headers[index], CsvHeaderLock[index], StringComparison.Ordinal))
                {
                    errors.Add("Legacy CSV Header 불일치(" + (index + 1) + "): expected="
                               + CsvHeaderLock[index] + ", actual=" + headers[index]);
                }
            }
        }

        private static VisualCatalog ReadVisualCatalog(
            List<string> errors,
            List<string> warnings)
        {
            VisualCatalog result = new VisualCatalog();
            ReadSpriteCategory(
                MainSpriteRoot,
                MainCategory,
                ParseExactSkinName,
                result.Main,
                errors);
            ReadSpriteCategory(
                ThumbnailSpriteRoot,
                ThumbnailCategory,
                ParseExactSkinName,
                result.Thumbnail,
                errors);
            ReadSpriteCategory(
                IdleSpriteRoot,
                IdleCategory,
                ParseIdleName,
                result.Idle,
                errors,
                result.IdleNamingMismatches,
                warnings);
            ReadSpriteCategory(
                ChefBackSpriteRoot,
                ChefBackCategory,
                ParseExactSkinName,
                result.ChefBack,
                errors);
            ReadSpriteCategory(
                ChefHandSpriteRoot,
                ChefHandCategory,
                ParseExactSkinName,
                result.ChefHand,
                errors);
            ReadSpriteCategory(
                CheerleaderAnimationSpriteRoot,
                CheerleaderAnimationCategory,
                ParseExactSkinName,
                result.CheerleaderAnimation,
                errors);
            ReadSpriteCategory(
                CheerleaderParticleSpriteRoot,
                CheerleaderParticleCategory,
                ParseParticleName,
                result.CheerleaderParticles,
                errors);
            ReadAnimatorCandidates(result.AnimatorCandidates, errors, warnings);
            ValidateCatalogDuplicates(result, errors);
            ValidateIdleNamingMismatches(result.IdleNamingMismatches, errors);
            return result;
        }

        private static void ReadSpriteCategory(
            string root,
            string category,
            TryParseVisualName parser,
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> target,
            List<string> errors,
            List<StaffLegacyVisualReferenceSnapshot> namingMismatches = null,
            List<string> warnings = null)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                errors.Add("Legacy 시각 자료 Root가 없습니다: " + root);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
            List<string> paths = new List<string>();
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (IsPathInFolder(path, root) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                string path = paths[pathIndex];
                UnityEngine.Object[] objects = AssetDatabase.LoadAllAssetsAtPath(path);
                int spriteCount = 0;
                for (int objectIndex = 0; objectIndex < objects.Length; objectIndex++)
                {
                    Sprite sprite = objects[objectIndex] as Sprite;
                    if (sprite == null)
                    {
                        continue;
                    }

                    spriteCount++;
                    string legacyId;
                    int frameIndex;
                    if (!parser(sprite.name, out legacyId, out frameIndex))
                    {
                        if (namingMismatches != null && warnings != null)
                        {
                            int mismatchFrameIndex;
                            TryParseIdleNamingMismatch(sprite.name, out mismatchFrameIndex);
                            StaffLegacyVisualReferenceSnapshot mismatch = CreateVisualReference(
                                IdleNamingMismatchCategory,
                                sprite,
                                mismatchFrameIndex,
                                path);
                            namingMismatches.Add(mismatch);
                            warnings.Add("LEGACY_IDLE_NAMING_MISMATCH: " + sprite.name
                                         + " (원본 자동 수정 없음, 정상 Idle 목록에서 제외)");
                        }
                        else
                        {
                            errors.Add(category + " Sprite 이름 규칙 위반: " + sprite.name
                                       + " (" + path + ")");
                        }

                        continue;
                    }

                    StaffLegacyVisualReferenceSnapshot reference =
                        CreateVisualReference(category, sprite, frameIndex, path);
                    AddReference(target, legacyId, reference);
                }

                if (spriteCount == 0)
                {
                    errors.Add(category + " Texture에서 Sprite를 읽지 못했습니다: " + path);
                }
            }
        }

        private static void ReadAnimatorCandidates(
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> target,
            List<string> errors,
            List<string> warnings)
        {
            if (!AssetDatabase.IsValidFolder(AnimatorCandidateRoot))
            {
                warnings.Add("LEGACY_ANIMATOR_NOT_ASSIGNED: 0명, 지정 Root 없음 - "
                             + AnimatorCandidateRoot + " (유사 경로를 대신 사용하지 않음)");
                return;
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:RuntimeAnimatorController",
                new[] { AnimatorCandidateRoot });
            List<string> paths = new List<string>();
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (IsPathInFolder(path, AnimatorCandidateRoot) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            for (int index = 0; index < paths.Count; index++)
            {
                RuntimeAnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(paths[index]);
                if (controller == null)
                {
                    errors.Add("AnimatorController 후보를 읽지 못했습니다: " + paths[index]);
                    continue;
                }

                int legacyNumber;
                if (!TryParseLegacySkinId(controller.name, out legacyNumber))
                {
                    warnings.Add("UNMAPPED_ANIMATOR_CANDIDATE: " + controller.name
                                 + " (" + paths[index] + ")");
                    continue;
                }

                StaffLegacyVisualReferenceSnapshot reference = CreateVisualReference(
                    AnimatorCategory,
                    controller,
                    0,
                    paths[index]);
                AddReference(target, ToLegacySkinId(legacyNumber), reference);
            }

            if (target.Count == 0)
            {
                warnings.Add("LEGACY_ANIMATOR_NOT_ASSIGNED: 0명"
                             + " (현재 공용 Staff Prefab Animator 구조와 호환)");
            }
        }

        private static void ValidateIdleNamingMismatches(
            IReadOnlyList<StaffLegacyVisualReferenceSnapshot> mismatches,
            List<string> errors)
        {
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < mismatches.Count; index++)
            {
                StaffLegacyVisualReferenceSnapshot mismatch = mismatches[index];
                if (mismatch == null
                    || !mismatch.IsAssigned
                    || mismatch.IsMissing
                    || string.IsNullOrEmpty(mismatch.AssetPath)
                    || string.IsNullOrEmpty(mismatch.AssetGuid)
                    || mismatch.LocalFileId == 0)
                {
                    errors.Add("Idle NamingMismatch 원본 참조를 해석하지 못했습니다: "
                               + (mismatch != null ? mismatch.ObjectName : "<NULL>"));
                    continue;
                }

                string identity = mismatch.AssetGuid + ":"
                                  + mismatch.LocalFileId.ToString(CultureInfo.InvariantCulture);
                if (!identities.Add(identity))
                {
                    errors.Add("Idle NamingMismatch GUID/LocalFileId가 중복됩니다: "
                               + mismatch.ObjectName);
                }
            }
        }

        private static StaffLegacyVisualReferenceSnapshot CreateVisualReference(
            string category,
            UnityEngine.Object asset,
            int frameIndex,
            string fallbackPath)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = fallbackPath ?? string.Empty;
            }

            string guid;
            long localFileId;
            bool resolved = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset,
                out guid,
                out localFileId);
            if (string.IsNullOrEmpty(guid))
            {
                guid = AssetDatabase.AssetPathToGUID(assetPath);
            }

            bool missing = !resolved
                           || string.IsNullOrEmpty(assetPath)
                           || string.IsNullOrEmpty(guid)
                           || localFileId == 0;
            return new StaffLegacyVisualReferenceSnapshot(
                category,
                assetPath,
                guid,
                localFileId,
                asset != null ? asset.name : string.Empty,
                asset != null ? asset.GetType().FullName : string.Empty,
                frameIndex,
                asset != null,
                missing);
        }

        private static void ValidateCatalogDuplicates(VisualCatalog catalog, List<string> errors)
        {
            ValidateSingleReferenceMap(MainCategory, catalog.Main, errors);
            ValidateSingleReferenceMap(ThumbnailCategory, catalog.Thumbnail, errors);
            ValidateFrameReferenceMap(IdleCategory, catalog.Idle, errors);
            ValidateSingleReferenceMap(ChefBackCategory, catalog.ChefBack, errors);
            ValidateSingleReferenceMap(ChefHandCategory, catalog.ChefHand, errors);
            ValidateSingleReferenceMap(
                CheerleaderAnimationCategory,
                catalog.CheerleaderAnimation,
                errors);
            ValidateFrameReferenceMap(
                CheerleaderParticleCategory,
                catalog.CheerleaderParticles,
                errors);
            ValidateSingleReferenceMap(AnimatorCategory, catalog.AnimatorCandidates, errors);
        }

        private static void ValidateSingleReferenceMap(
            string category,
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> values,
            List<string> errors)
        {
            foreach (KeyValuePair<string, List<StaffLegacyVisualReferenceSnapshot>> entry in values)
            {
                if (entry.Value.Count != 1)
                {
                    errors.Add(category + " ObjectName이 중복됩니다: " + entry.Key
                               + " (" + entry.Value.Count + ")");
                }
            }
        }

        private static void ValidateFrameReferenceMap(
            string category,
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> values,
            List<string> errors)
        {
            foreach (KeyValuePair<string, List<StaffLegacyVisualReferenceSnapshot>> entry in values)
            {
                entry.Value.Sort(CompareFrameReferences);
                for (int index = 1; index < entry.Value.Count; index++)
                {
                    if (entry.Value[index - 1].FrameIndex == entry.Value[index].FrameIndex)
                    {
                        errors.Add(category + " FrameIndex가 중복됩니다: " + entry.Key
                                   + " #" + entry.Value[index].FrameIndex);
                    }
                }
            }
        }

        private static void BuildSkinSnapshots(
            StaffDataAssetInventorySnapshot staffInventory,
            IReadOnlyList<LegacyCsvRow> csvRows,
            VisualCatalog visuals,
            List<StaffLegacySkinRowSnapshot> result,
            List<string> errors,
            List<string> warnings)
        {
            List<LegacyCsvRow> sortedRows = new List<LegacyCsvRow>(csvRows);
            sortedRows.Sort((left, right) => left.LegacyNumber.CompareTo(right.LegacyNumber));
            for (int index = 0; index < sortedRows.Count; index++)
            {
                LegacyCsvRow row = sortedRows[index];
                string legacyId = row.Cells[0];
                string candidateId = ToCandidateStaffId(row.LegacyNumber);
                string equipTargetId = row.Cells[12];
                StaffDataAssetSnapshot equipTarget;
                bool roleResolved = staffInventory.TryGetStaff(equipTargetId, out equipTarget);
                string roleKey = roleResolved ? equipTarget.RoleKey : string.Empty;
                if (!roleResolved)
                {
                    errors.Add(legacyId + " 스킨 적용 ID를 B2-1 Inventory에서 찾지 못했습니다: "
                               + equipTargetId);
                }

                StaffLegacyVisualReferenceSnapshot main = GetSingleOrUnassigned(
                    visuals.Main,
                    legacyId,
                    MainCategory);
                StaffLegacyVisualReferenceSnapshot thumbnail = GetSingleOrUnassigned(
                    visuals.Thumbnail,
                    legacyId,
                    ThumbnailCategory);
                List<StaffLegacyVisualReferenceSnapshot> idle = GetFrames(visuals.Idle, legacyId);
                StaffLegacyVisualReferenceSnapshot chefBack = GetSingleOrUnassigned(
                    visuals.ChefBack,
                    legacyId,
                    ChefBackCategory);
                StaffLegacyVisualReferenceSnapshot chefHand = GetSingleOrUnassigned(
                    visuals.ChefHand,
                    legacyId,
                    ChefHandCategory);
                StaffLegacyVisualReferenceSnapshot cheerAnimation = GetSingleOrUnassigned(
                    visuals.CheerleaderAnimation,
                    legacyId,
                    CheerleaderAnimationCategory);
                List<StaffLegacyVisualReferenceSnapshot> cheerParticles = GetFrames(
                    visuals.CheerleaderParticles,
                    legacyId);
                StaffLegacyVisualReferenceSnapshot animator = GetSingleOrUnassigned(
                    visuals.AnimatorCandidates,
                    legacyId,
                    AnimatorCategory);

                bool sequential = string.Equals(
                    candidateId,
                    "STAFF" + (row.LegacyNumber + 32).ToString("D2", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
                StaffLegacySkinRowSnapshot skin = new StaffLegacySkinRowSnapshot(
                    legacyId,
                    row.LegacyNumber,
                    candidateId,
                    row.Cells[1],
                    row.Cells[2],
                    equipTargetId,
                    roleKey,
                    row.Cells[3],
                    row.Cells[4],
                    row.Cells[5],
                    row.Cells[6],
                    row.Cells[7],
                    row.Cells[8],
                    row.Cells[9],
                    row.Cells[10],
                    row.Cells[11],
                    row.Cells[13],
                    row.Cells,
                    main,
                    thumbnail,
                    idle,
                    chefBack,
                    chefHand,
                    cheerAnimation,
                    cheerParticles,
                    animator,
                    sequential,
                    roleResolved);
                result.Add(skin);
                ValidateSkinVisualRequirements(skin, errors, warnings);
            }
        }

        private static void ValidateSkinVisualRequirements(
            StaffLegacySkinRowSnapshot skin,
            List<string> errors,
            List<string> warnings)
        {
            if (!skin.HasMainSprite)
            {
                errors.Add(skin.LegacySkinId + " Main Sprite가 없습니다.");
            }

            if (!skin.HasThumbnail)
            {
                errors.Add(skin.LegacySkinId + " Thumbnail Sprite가 없습니다.");
            }

            if (!skin.HasIdleFrames)
            {
                warnings.Add("LEGACY_OPTIONAL_IDLE_GAP: " + skin.LegacySkinId
                             + " -> " + skin.CandidateNewStaffId
                             + " (후속 처리: STATIC_IDLE_FALLBACK_REQUIRED)");
            }

            if (skin.HasMissingReference)
            {
                errors.Add(skin.LegacySkinId + "에 GUID/LocalFileId가 해석되지 않은 시각 참조가 있습니다.");
            }

            bool hasChefBack = IsAssigned(skin.ChefBackSprite);
            bool hasChefHand = IsAssigned(skin.ChefHandSprite);
            if (string.Equals(skin.EquipTargetRoleKey, "CHEF", StringComparison.Ordinal))
            {
                if (!skin.HasChefParts)
                {
                    errors.Add(skin.LegacySkinId + " CHEF Back/Hand가 완전하지 않습니다.");
                }
            }
            else if (hasChefBack || hasChefHand)
            {
                warnings.Add("EXTRA_LEGACY_VISUAL_REFERENCE: " + skin.LegacySkinId
                             + " 비-CHEF 역할에 Chef Back/Hand가 있습니다.");
            }

            bool hasCheerAnimation = IsAssigned(skin.CheerleaderAnimationSprite);
            bool hasCheerParticles = skin.CheerleaderParticleSprites.Count > 0;
            if (string.Equals(skin.EquipTargetRoleKey, "CHEERLEADER", StringComparison.Ordinal))
            {
                if (!skin.HasCheerleaderParts)
                {
                    errors.Add(skin.LegacySkinId
                               + " CHEERLEADER Animation/Particle이 완전하지 않습니다.");
                }
            }
            else if (hasCheerAnimation || hasCheerParticles)
            {
                warnings.Add("EXTRA_LEGACY_VISUAL_REFERENCE: " + skin.LegacySkinId
                             + " 비-CHEERLEADER 역할에 Animation/Particle이 있습니다.");
            }
        }

        private static void ValidateRoleDistribution(
            IReadOnlyList<StaffLegacySkinRowSnapshot> skins,
            List<string> errors)
        {
            Dictionary<string, int> expected = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "WAITER", 16 },
                { "MANAGER", 8 },
                { "CHEERLEADER", 12 },
                { "CHEF", 16 },
                { "CLEANER", 8 },
                { "GUARD", 0 }
            };
            Dictionary<string, int> actual = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < skins.Count; index++)
            {
                string role = skins[index].EquipTargetRoleKey;
                int count;
                actual.TryGetValue(role, out count);
                actual[role] = count + 1;
            }

            foreach (KeyValuePair<string, int> entry in expected)
            {
                int actualCount;
                actual.TryGetValue(entry.Key, out actualCount);
                if (actualCount != entry.Value)
                {
                    errors.Add("Legacy 적용 대상 역할 분포 불일치: " + entry.Key
                               + " expected=" + entry.Value + ", actual=" + actualCount);
                }
            }

            foreach (string role in actual.Keys)
            {
                if (!expected.ContainsKey(role))
                {
                    errors.Add("알 수 없는 Legacy 적용 대상 역할이 있습니다: " + role);
                }
            }
        }

        private static bool ParseExactSkinName(
            string objectName,
            out string legacyId,
            out int frameIndex)
        {
            int legacyNumber;
            bool parsed = TryParseLegacySkinId(objectName, out legacyNumber);
            legacyId = parsed ? ToLegacySkinId(legacyNumber) : string.Empty;
            frameIndex = 0;
            return parsed;
        }

        private static bool ParseIdleName(
            string objectName,
            out string legacyId,
            out int frameIndex)
        {
            legacyId = string.Empty;
            frameIndex = 0;
            int separatorIndex = objectName.LastIndexOf('-');
            if (separatorIndex <= 0 || separatorIndex == objectName.Length - 1)
            {
                return false;
            }

            string idPart = objectName.Substring(0, separatorIndex);
            int legacyNumber;
            int parsedFrame;
            if (!TryParseLegacySkinId(idPart, out legacyNumber)
                || !int.TryParse(
                    objectName.Substring(separatorIndex + 1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parsedFrame)
                || parsedFrame <= 0)
            {
                return false;
            }

            legacyId = ToLegacySkinId(legacyNumber);
            frameIndex = parsedFrame;
            return true;
        }

        private static bool ParseParticleName(
            string objectName,
            out string legacyId,
            out int frameIndex)
        {
            legacyId = string.Empty;
            frameIndex = 0;
            const string marker = "_Effect";
            int markerIndex = objectName.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex <= 0 || markerIndex + marker.Length == objectName.Length)
            {
                return false;
            }

            int legacyNumber;
            int parsedFrame;
            if (!TryParseLegacySkinId(objectName.Substring(0, markerIndex), out legacyNumber)
                || !int.TryParse(
                    objectName.Substring(markerIndex + marker.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parsedFrame)
                || parsedFrame <= 0)
            {
                return false;
            }

            legacyId = ToLegacySkinId(legacyNumber);
            frameIndex = parsedFrame;
            return true;
        }

        private static bool TryParseIdleNamingMismatch(
            string objectName,
            out int frameIndex)
        {
            frameIndex = 0;
            int separatorIndex = objectName.LastIndexOf('_');
            if (separatorIndex <= 0 || separatorIndex == objectName.Length - 1)
            {
                return false;
            }

            int legacyNumber;
            int parsedFrame;
            if (!TryParseLegacySkinId(objectName.Substring(0, separatorIndex), out legacyNumber)
                || !int.TryParse(
                    objectName.Substring(separatorIndex + 1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parsedFrame)
                || parsedFrame <= 0)
            {
                return false;
            }

            frameIndex = parsedFrame;
            return true;
        }

        private static bool TryParseLegacySkinId(string value, out int legacyNumber)
        {
            legacyNumber = 0;
            const string prefix = "SKIN_STAFF";
            if (string.IsNullOrEmpty(value)
                || value.Length != prefix.Length + 2
                || !value.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(
                    value.Substring(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out legacyNumber))
            {
                return false;
            }

            return legacyNumber >= 1 && legacyNumber <= 60
                   && string.Equals(value, ToLegacySkinId(legacyNumber), StringComparison.Ordinal);
        }

        private static bool TryParseRfc4180(
            string text,
            out List<List<string>> rows,
            out string error)
        {
            rows = new List<List<string>>();
            error = string.Empty;
            List<string> row = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;
            bool afterClosingQuote = false;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (inQuotes)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            inQuotes = false;
                            afterClosingQuote = true;
                        }
                    }
                    else
                    {
                        field.Append(character);
                    }

                    continue;
                }

                if (afterClosingQuote)
                {
                    if (character == ',')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                        afterClosingQuote = false;
                        continue;
                    }

                    if (character == '\r' || character == '\n')
                    {
                        row.Add(field.ToString());
                        rows.Add(row);
                        row = new List<string>();
                        field.Length = 0;
                        afterClosingQuote = false;
                        if (character == '\r'
                            && index + 1 < text.Length
                            && text[index + 1] == '\n')
                        {
                            index++;
                        }

                        continue;
                    }

                    error = "닫는 따옴표 뒤에 구분자 외 문자가 있습니다. index=" + index;
                    return false;
                }

                if (character == '"')
                {
                    if (field.Length != 0)
                    {
                        error = "인용되지 않은 Cell 중간에 따옴표가 있습니다. index=" + index;
                        return false;
                    }

                    inQuotes = true;
                }
                else if (character == ',')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                }
                else if (character == '\r' || character == '\n')
                {
                    row.Add(field.ToString());
                    rows.Add(row);
                    row = new List<string>();
                    field.Length = 0;
                    if (character == '\r'
                        && index + 1 < text.Length
                        && text[index + 1] == '\n')
                    {
                        index++;
                    }
                }
                else
                {
                    field.Append(character);
                }
            }

            if (inQuotes)
            {
                error = "닫히지 않은 인용 Cell이 있습니다.";
                return false;
            }

            if (afterClosingQuote || field.Length != 0 || row.Count != 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }

            return true;
        }

        private static void AddReference(
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> target,
            string legacyId,
            StaffLegacyVisualReferenceSnapshot reference)
        {
            List<StaffLegacyVisualReferenceSnapshot> references;
            if (!target.TryGetValue(legacyId, out references))
            {
                references = new List<StaffLegacyVisualReferenceSnapshot>();
                target.Add(legacyId, references);
            }

            references.Add(reference);
        }

        private static StaffLegacyVisualReferenceSnapshot GetSingleOrUnassigned(
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> source,
            string legacyId,
            string category)
        {
            List<StaffLegacyVisualReferenceSnapshot> references;
            return source.TryGetValue(legacyId, out references) && references.Count != 0
                ? references[0]
                : CreateUnassignedReference(category);
        }

        private static List<StaffLegacyVisualReferenceSnapshot> GetFrames(
            Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> source,
            string legacyId)
        {
            List<StaffLegacyVisualReferenceSnapshot> references;
            if (!source.TryGetValue(legacyId, out references))
            {
                return new List<StaffLegacyVisualReferenceSnapshot>();
            }

            List<StaffLegacyVisualReferenceSnapshot> copy =
                new List<StaffLegacyVisualReferenceSnapshot>(references);
            copy.Sort(CompareFrameReferences);
            return copy;
        }

        private static StaffLegacyVisualReferenceSnapshot CreateUnassignedReference(string category)
        {
            return new StaffLegacyVisualReferenceSnapshot(
                category,
                string.Empty,
                string.Empty,
                0,
                string.Empty,
                string.Empty,
                0,
                false,
                false);
        }

        private static int CompareFrameReferences(
            StaffLegacyVisualReferenceSnapshot left,
            StaffLegacyVisualReferenceSnapshot right)
        {
            int comparison = left.FrameIndex.CompareTo(right.FrameIndex);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = string.Compare(left.AssetGuid, right.AssetGuid, StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : left.LocalFileId.CompareTo(right.LocalFileId);
        }

        private static bool IsAssigned(StaffLegacyVisualReferenceSnapshot reference)
        {
            return reference != null && reference.IsAssigned && !reference.IsMissing;
        }

        private static bool IsPathInFolder(string path, string folder)
        {
            return !string.IsNullOrEmpty(path)
                   && (string.Equals(path, folder, StringComparison.Ordinal)
                       || path.StartsWith(folder + "/", StringComparison.Ordinal));
        }

        private static string ToLegacySkinId(int legacyNumber)
        {
            return "SKIN_STAFF" + legacyNumber.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string ToCandidateStaffId(int legacyNumber)
        {
            return "STAFF" + (legacyNumber + 32).ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string ToAbsoluteProjectPath(string projectRelativePath)
        {
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            string projectRoot = projectDirectory != null
                ? projectDirectory.FullName
                : Application.dataPath;
            return Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
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

        private static void AddChildDiagnostics(
            string label,
            IReadOnlyList<string> childDiagnostics,
            List<string> errors)
        {
            if (childDiagnostics == null || childDiagnostics.Count == 0)
            {
                errors.Add(label + " 생성 실패 진단이 없습니다.");
                return;
            }

            for (int index = 0; index < childDiagnostics.Count; index++)
            {
                string diagnostic = childDiagnostics[index];
                errors.Add(label + " - "
                           + (diagnostic.StartsWith("ERROR: ", StringComparison.Ordinal)
                               ? diagnostic.Substring("ERROR: ".Length)
                               : diagnostic));
            }
        }

        private delegate bool TryParseVisualName(
            string objectName,
            out string legacyId,
            out int frameIndex);

        private sealed class LegacyCsvRow
        {
            internal readonly int LegacyNumber;
            internal readonly IReadOnlyList<string> Cells;

            internal LegacyCsvRow(int legacyNumber, IEnumerable<string> cells)
            {
                LegacyNumber = legacyNumber;
                Cells = new ReadOnlyCollection<string>(new List<string>(cells));
            }
        }

        private sealed class VisualCatalog
        {
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> Main =
                CreateMap();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> Thumbnail =
                CreateMap();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> Idle =
                CreateMap();
            internal readonly List<StaffLegacyVisualReferenceSnapshot> IdleNamingMismatches =
                new List<StaffLegacyVisualReferenceSnapshot>();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> ChefBack =
                CreateMap();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> ChefHand =
                CreateMap();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>>
                CheerleaderAnimation = CreateMap();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>>
                CheerleaderParticles = CreateMap();
            internal readonly Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>>
                AnimatorCandidates = CreateMap();

            private static Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>> CreateMap()
            {
                return new Dictionary<string, List<StaffLegacyVisualReferenceSnapshot>>(
                    StringComparer.Ordinal);
            }
        }
    }
}
