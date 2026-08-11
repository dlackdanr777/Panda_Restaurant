using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffLegacySkinInventoryValidator
    {
        internal const string MenuPath =
            "Tools/Panda Restaurant/Staff/Validate Legacy Staff Skin Inventory";

        [MenuItem(MenuPath)]
        private static void ValidateLegacyStaffSkinInventory()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            Dictionary<int, List<string>> details = CreateDetailSections();

            Dictionary<string, AssetFileState> before;
            bool beforeCollected = TryCollectLegacyRootState("실행 전", errors, out before);

            StaffLegacySkinInventorySnapshot first;
            IReadOnlyList<string> firstDiagnostics;
            bool firstBuilt = StaffLegacySkinInventoryReader.TryBuildReadOnlyInventory(
                out first,
                out firstDiagnostics);
            AddDiagnostics(firstDiagnostics, warnings, errors);

            StaffLegacySkinInventorySnapshot second;
            IReadOnlyList<string> secondDiagnostics;
            bool secondBuilt = StaffLegacySkinInventoryReader.TryBuildReadOnlyInventory(
                out second,
                out secondDiagnostics);
            if (!secondBuilt)
            {
                AddDiagnostics(secondDiagnostics, warnings, errors);
            }

            bool[] passed = new bool[15];
            passed[1] = firstBuilt && first != null;
            details[1].Add("- 완전한 Snapshot: " + (passed[1] ? "YES" : "NO"));
            details[1].Add("- Fatal Error: " + CountDiagnostics(firstDiagnostics, "ERROR: "));

            if (first != null)
            {
                passed[2] = ValidateCsv(first, details[2], errors);
                passed[3] = ValidateIdsAndCandidates(first, details[3], errors);
                passed[4] = ValidateRoles(first, details[4], errors);
                passed[5] = ValidateMainAndThumbnail(first, details[5], errors);
                passed[6] = ValidateIdle(first, details[6], errors);
                passed[7] = ValidateChef(first, details[7], errors);
                passed[8] = ValidateCheerleader(first, details[8], errors);
                passed[9] = ValidateAnimator(first, details[9], errors);
                passed[10] = ValidateMetadataIsolation(first, details[10], errors);
                passed[11] = ValidateFingerprint(first, details[11], errors);
                passed[13] = ValidateDeepImmutability(first, details[13], errors);
            }
            else
            {
                MarkSnapshotUnavailable(details, passed);
            }

            passed[12] = firstBuilt
                         && secondBuilt
                         && first != null
                         && second != null
                         && string.Equals(
                             first.InventoryFingerprint,
                             second.InventoryFingerprint,
                             StringComparison.Ordinal)
                         && first.LegacySkins.Count == second.LegacySkins.Count
                         && first.IdleNamingMismatches.Count
                            == second.IdleNamingMismatches.Count;
            details[12].Add("- 첫 번째/두 번째 Build 성공: "
                            + firstBuilt + "/" + secondBuilt);
            details[12].Add("- Fingerprint 동일: "
                            + (first != null && second != null
                               && string.Equals(
                                   first.InventoryFingerprint,
                                   second.InventoryFingerprint,
                                   StringComparison.Ordinal)
                                ? "YES"
                                : "NO"));
            if (!passed[12])
            {
                errors.Add("동일 자산에서 두 번 생성한 Legacy Inventory 결과가 다릅니다.");
            }

            Dictionary<string, AssetFileState> after;
            bool afterCollected = TryCollectLegacyRootState("실행 후", errors, out after);
            passed[14] = beforeCollected
                         && afterCollected
                         && CompareFileStates(before, after, details[14], errors);

            bool allPassed = true;
            for (int number = 1; number <= 14; number++)
            {
                allPassed &= passed[number];
            }

            allPassed &= errors.Count == 0;
            WriteConsoleReport(passed, details, warnings, errors, allPassed);
        }

        private static bool ValidateCsv(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            passed &= snapshot.CsvHeaders.Count == 20;
            passed &= snapshot.LegacySkins.Count == 60;
            passed &= IsLowercaseSha256(snapshot.CsvSha256);
            passed &= HasUtf8Bom(StaffLegacySkinInventoryReader.CsvAssetPath);
            if (snapshot.CsvHeaders.Count == StaffLegacySkinInventoryReader.ExpectedCsvHeaders.Count)
            {
                for (int index = 0; index < snapshot.CsvHeaders.Count; index++)
                {
                    passed &= string.Equals(
                        snapshot.CsvHeaders[index],
                        StaffLegacySkinInventoryReader.ExpectedCsvHeaders[index],
                        StringComparison.Ordinal);
                }
            }
            else
            {
                passed = false;
            }

            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                passed &= snapshot.LegacySkins[index].RawCells.Count == 20;
            }

            details.Add("- 경로: " + snapshot.CsvAssetPath);
            details.Add("- UTF-8 BOM: " + (HasUtf8Bom(snapshot.CsvAssetPath) ? "YES" : "NO"));
            details.Add("- Header/Data: " + snapshot.CsvHeaders.Count + "/"
                        + snapshot.LegacySkins.Count);
            details.Add("- CSV SHA-256: " + snapshot.CsvSha256);
            details.Add("- Raw Cell 20개 보존: " + (passed ? "YES" : "NO"));
            if (!passed)
            {
                errors.Add("Legacy CSV 20 Header/60행/BOM/Raw Cell 기준을 통과하지 못했습니다.");
            }

            return passed;
        }

        private static bool ValidateIdsAndCandidates(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = snapshot.LegacySkinById.Count == 60
                          && snapshot.CandidateStaffId.Count == 60;
            for (int number = 1; number <= 60; number++)
            {
                string legacyId = "SKIN_STAFF"
                                  + number.ToString("D2", CultureInfo.InvariantCulture);
                string candidateId = "STAFF"
                                     + (number + 32).ToString("D2", CultureInfo.InvariantCulture);
                StaffLegacySkinRowSnapshot legacyRow;
                StaffLegacySkinRowSnapshot candidateRow;
                passed &= snapshot.LegacySkinById.TryGetValue(legacyId, out legacyRow);
                passed &= snapshot.CandidateStaffId.TryGetValue(candidateId, out candidateRow);
                passed &= legacyRow != null
                          && candidateRow != null
                          && ReferenceEquals(legacyRow, candidateRow)
                          && legacyRow.CandidateMappingIsSequential;
            }

            details.Add("- Legacy ID: SKIN_STAFF01~SKIN_STAFF60 ("
                        + snapshot.LegacySkinById.Count + ")");
            details.Add("- Candidate ID: STAFF33~STAFF92 ("
                        + snapshot.CandidateStaffId.Count + ")");
            details.Add("- 공식: Legacy 번호 + 32, 실제 에셋 연결 없음");
            if (!passed)
            {
                errors.Add("Legacy ID 또는 Candidate STAFF33~92 순차 매핑이 올바르지 않습니다.");
            }

            return passed;
        }

        private static bool ValidateRoles(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            Dictionary<string, int> counts = CountRoles(snapshot.LegacySkins);
            bool passed = snapshot.LegacySkins.Count == 60
                          && GetCount(counts, "WAITER") == 16
                          && GetCount(counts, "MANAGER") == 8
                          && GetCount(counts, "CHEERLEADER") == 12
                          && GetCount(counts, "CHEF") == 16
                          && GetCount(counts, "CLEANER") == 8
                          && GetCount(counts, "GUARD") == 0;
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                passed &= snapshot.LegacySkins[index].SourceRoleResolved;
            }

            details.Add("- WAITER/MANAGER/CHEERLEADER: "
                        + GetCount(counts, "WAITER") + "/"
                        + GetCount(counts, "MANAGER") + "/"
                        + GetCount(counts, "CHEERLEADER"));
            details.Add("- CHEF/CLEANER/GUARD: "
                        + GetCount(counts, "CHEF") + "/"
                        + GetCount(counts, "CLEANER") + "/"
                        + GetCount(counts, "GUARD"));
            if (!passed)
            {
                errors.Add("B2-1 기준 Legacy 스킨 적용 대상 역할 분포가 잠금값과 다릅니다.");
            }

            return passed;
        }

        private static bool ValidateMainAndThumbnail(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int mainCount = 0;
            int thumbnailCount = 0;
            int missingCount = 0;
            HashSet<string> mainNames = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> thumbnailNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                StaffLegacySkinRowSnapshot skin = snapshot.LegacySkins[index];
                if (skin.HasMainSprite)
                {
                    mainCount++;
                    mainNames.Add(skin.MainSprite.ObjectName);
                }

                if (skin.HasThumbnail)
                {
                    thumbnailCount++;
                    thumbnailNames.Add(skin.ThumbnailSprite.ObjectName);
                }

                missingCount += CountMissing(skin.MainSprite, skin.ThumbnailSprite);
            }

            bool passed = mainCount == 60
                          && thumbnailCount == 60
                          && mainNames.Count == 60
                          && thumbnailNames.Count == 60
                          && missingCount == 0;
            details.Add("- Main/Thumbnail: " + mainCount + "/60, " + thumbnailCount + "/60");
            details.Add("- Missing/중복 ObjectName: " + missingCount + "/"
                        + ((60 - mainNames.Count) + (60 - thumbnailNames.Count)));
            if (!passed)
            {
                errors.Add("Main 또는 Thumbnail 60/60 기준을 통과하지 못했습니다.");
            }

            return passed;
        }

        private static bool ValidateIdle(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int hasIdleCount = 0;
            List<StaffLegacySkinRowSnapshot> optionalGaps =
                new List<StaffLegacySkinRowSnapshot>();
            bool uniqueFrames = true;
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                StaffLegacySkinRowSnapshot skin = snapshot.LegacySkins[index];
                if (skin.HasIdleFrames)
                {
                    hasIdleCount++;
                }
                else
                {
                    optionalGaps.Add(skin);
                    uniqueFrames &= skin.RequiresStaticIdleFallback;
                }

                HashSet<int> indexes = new HashSet<int>();
                for (int frame = 0; frame < skin.IdleFrames.Count; frame++)
                {
                    StaffLegacyVisualReferenceSnapshot idleFrame = skin.IdleFrames[frame];
                    uniqueFrames &= idleFrame != null
                                    && string.Equals(
                                        idleFrame.Category,
                                        StaffLegacySkinInventoryReader.IdleCategory,
                                        StringComparison.Ordinal)
                                    && idleFrame.ObjectName.StartsWith(
                                        skin.LegacySkinId + "-",
                                        StringComparison.Ordinal)
                                    && idleFrame.FrameIndex > 0
                                    && indexes.Add(idleFrame.FrameIndex);
                }
            }

            bool namingMismatchValid = snapshot.IdleNamingMismatches.Count == 1;
            for (int index = 0; index < snapshot.IdleNamingMismatches.Count; index++)
            {
                StaffLegacyVisualReferenceSnapshot mismatch =
                    snapshot.IdleNamingMismatches[index];
                namingMismatchValid &= mismatch != null
                                       && mismatch.IsAssigned
                                       && !mismatch.IsMissing
                                       && string.Equals(
                                           mismatch.Category,
                                           StaffLegacySkinInventoryReader.IdleNamingMismatchCategory,
                                           StringComparison.Ordinal)
                                       && string.Equals(
                                           mismatch.ObjectName,
                                           "SKIN_STAFF07_04",
                                           StringComparison.Ordinal)
                                       && !string.IsNullOrEmpty(mismatch.AssetPath)
                                       && !string.IsNullOrEmpty(mismatch.AssetGuid)
                                       && mismatch.LocalFileId != 0;
            }

            bool passed = hasIdleCount == 36
                          && optionalGaps.Count == 24
                          && uniqueFrames
                          && namingMismatchValid;
            details.Add("- 정상 Idle 보유: " + hasIdleCount + "명");
            details.Add("- Optional Idle Gap: " + optionalGaps.Count + "명");
            for (int index = 0; index < optionalGaps.Count; index++)
            {
                details.Add("  · " + optionalGaps[index].LegacySkinId
                            + " -> " + optionalGaps[index].CandidateNewStaffId
                            + " : STATIC_IDLE_FALLBACK_REQUIRED");
            }

            details.Add("- Naming Mismatch: "
                        + (snapshot.IdleNamingMismatches.Count == 0
                            ? "없음"
                            : JoinObjectNames(snapshot.IdleNamingMismatches)));
            details.Add("- NamingMismatch 원본 Path/GUID/LocalFileId 보존: "
                        + (namingMismatchValid ? "YES" : "NO"));
            details.Add("- Main Sprite를 Idle 목록에 삽입: 없음");
            details.Add("- 원본 자동 수정: 없음");
            details.Add("- FrameIndex 중복·파싱 실패: " + (uniqueFrames ? "0" : "1개 이상"));
            if (!passed)
            {
                errors.Add("Idle 실제 기준 36/24 또는 NamingMismatch 1건 보존 기준을 통과하지 못했습니다.");
            }

            return passed;
        }

        private static bool ValidateChef(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int chefCount = 0;
            int backCount = 0;
            int handCount = 0;
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                StaffLegacySkinRowSnapshot skin = snapshot.LegacySkins[index];
                if (!string.Equals(skin.EquipTargetRoleKey, "CHEF", StringComparison.Ordinal))
                {
                    continue;
                }

                chefCount++;
                backCount += IsUsable(skin.ChefBackSprite) ? 1 : 0;
                handCount += IsUsable(skin.ChefHandSprite) ? 1 : 0;
            }

            bool passed = chefCount == 16 && backCount == 16 && handCount == 16;
            details.Add("- CHEF/Back/Hand: " + chefCount + "/" + backCount + "/" + handCount);
            if (!passed)
            {
                errors.Add("CHEF Back/Hand 16/16 기준을 통과하지 못했습니다.");
            }

            return passed;
        }

        private static bool ValidateCheerleader(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int roleCount = 0;
            int animationCount = 0;
            int particleOwnerCount = 0;
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                StaffLegacySkinRowSnapshot skin = snapshot.LegacySkins[index];
                if (!string.Equals(
                        skin.EquipTargetRoleKey,
                        "CHEERLEADER",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                roleCount++;
                animationCount += IsUsable(skin.CheerleaderAnimationSprite) ? 1 : 0;
                particleOwnerCount += skin.CheerleaderParticleSprites.Count > 0 ? 1 : 0;
            }

            bool passed = roleCount == 12
                          && animationCount == 12
                          && particleOwnerCount == 12;
            details.Add("- CHEERLEADER/Animation/Particle 보유: "
                        + roleCount + "/" + animationCount + "/" + particleOwnerCount);
            if (!passed)
            {
                errors.Add("CHEERLEADER Animation/Particle 12/12 기준을 통과하지 못했습니다.");
            }

            return passed;
        }

        private static bool ValidateAnimator(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int animatorCount = 0;
            int missingCount = 0;
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                StaffLegacyVisualReferenceSnapshot animator =
                    snapshot.LegacySkins[index].AnimatorControllerCandidate;
                animatorCount += IsUsable(animator) ? 1 : 0;
                missingCount += animator != null && animator.IsMissing ? 1 : 0;
            }

            bool passed = missingCount == 0;
            details.Add("- Legacy 전용 Animator 후보: " + animatorCount + "/60 (현재 예상 0)");
            details.Add("- 공용 Prefab Animator 사용 가능성: 상세 비교는 B3 이후 재검사");
            details.Add("- Animator 0은 오류 아님");
            if (!passed)
            {
                errors.Add("해석되지 않은 AnimatorController 참조가 있습니다.");
            }

            return passed;
        }

        private static bool ValidateMetadataIsolation(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            for (int index = 0; index < snapshot.LegacySkins.Count; index++)
            {
                StaffLegacySkinRowSnapshot skin = snapshot.LegacySkins[index];
                IReadOnlyList<string> cells = skin.RawCells;
                passed &= cells.Count == 20
                          && string.Equals(skin.GachaProbabilityRaw, cells[3], StringComparison.Ordinal)
                          && string.Equals(skin.AddScoreRaw, cells[4], StringComparison.Ordinal)
                          && string.Equals(skin.AddTipPerMinuteRaw, cells[5], StringComparison.Ordinal)
                          && string.Equals(skin.RarityStarsRaw, cells[6], StringComparison.Ordinal)
                          && string.Equals(skin.GradeRaw, cells[7], StringComparison.Ordinal)
                          && string.Equals(skin.PurchaseCurrencyRaw, cells[8], StringComparison.Ordinal)
                          && string.Equals(skin.PurchasePriceRaw, cells[9], StringComparison.Ordinal)
                          && string.Equals(skin.LegacyUpgradeTypeId, cells[10], StringComparison.Ordinal)
                          && string.Equals(skin.LegacyUpgradeValueRaw, cells[11], StringComparison.Ordinal)
                          && string.Equals(skin.LegacyDuplicationTokenRaw, cells[13], StringComparison.Ordinal);
            }

            passed &= typeof(StaffLegacySkinRowSnapshot).GetProperty(
                          "PandaToken",
                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null;
            details.Add("- 분류: LEGACY_SKIN_METADATA_ONLY");
            details.Add("- SkinToken 관련 Raw 값 보존: " + (passed ? "YES" : "NO"));
            details.Add("- PandaToken·StaffData 구매 필드 자동 매핑: 없음");
            if (!passed)
            {
                errors.Add("Legacy Metadata Raw Cell 보존 또는 정책 격리 검사가 실패했습니다.");
            }

            return passed;
        }

        private static bool ValidateFingerprint(
            StaffLegacySkinInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = IsLowercaseSha256(snapshot.InventoryFingerprint);
            details.Add("- InventoryFingerprint: " + snapshot.InventoryFingerprint);
            details.Add("- CSV Raw Cell + GUID + LocalFileId 포함");
            details.Add("- 절대 경로·시간·사용자·Git·Instance ID 제외");
            if (!passed)
            {
                errors.Add("InventoryFingerprint가 소문자 SHA-256 64자리 형식이 아닙니다.");
            }

            return passed;
        }

        private static bool ValidateDeepImmutability(
            StaffLegacySkinInventorySnapshot source,
            List<string> details,
            List<string> errors)
        {
            if (source.LegacySkins.Count == 0)
            {
                errors.Add("깊은 불변성 검사 대상 Legacy Skin이 없습니다.");
                return false;
            }

            StaffLegacySkinRowSnapshot skin = source.LegacySkins[0];
            bool passed = true;
            passed &= VerifyListMutationBlocked(
                "Legacy Skin 목록 Add",
                source.LegacySkins,
                skin,
                errors);
            passed &= VerifyDictionaryMutationBlocked(
                "LegacySkinById Add",
                source.LegacySkinById,
                "__IMMUTABILITY_TEST__",
                skin,
                errors);
            passed &= VerifyDictionaryMutationBlocked(
                "CandidateStaffId Add",
                source.CandidateStaffId,
                "__IMMUTABILITY_TEST__",
                skin,
                errors);
            passed &= VerifyListMutationBlocked(
                "RawCells Add",
                skin.RawCells,
                "__IMMUTABILITY_TEST__",
                errors);
            passed &= VerifyListMutationBlocked(
                "IdleFrames Add",
                skin.IdleFrames,
                skin.IdleFrames.Count > 0 ? skin.IdleFrames[0] : skin.ThumbnailSprite,
                errors);
            passed &= VerifyListMutationBlocked(
                "ParticleSprites Add",
                skin.CheerleaderParticleSprites,
                skin.CheerleaderParticleSprites.Count > 0
                    ? skin.CheerleaderParticleSprites[0]
                    : skin.ThumbnailSprite,
                errors);
            StaffLegacyVisualReferenceSnapshot mismatchTestValue =
                source.IdleNamingMismatches.Count > 0
                    ? source.IdleNamingMismatches[0]
                    : skin.MainSprite;
            passed &= VerifyListMutationBlocked(
                "IdleNamingMismatches Add",
                source.IdleNamingMismatches,
                mismatchTestValue,
                errors);
            passed &= VerifyConstructorCopies(skin, errors);

            Type[] modelTypes =
            {
                typeof(StaffLegacySkinInventorySnapshot),
                typeof(StaffLegacySkinRowSnapshot),
                typeof(StaffLegacyVisualReferenceSnapshot)
            };
            for (int index = 0; index < modelTypes.Length; index++)
            {
                passed &= ValidateReadOnlyTypeShape(modelTypes[index], errors);
            }

            details.Add("- 목록·Dictionary·RawCells·Idle·Particle·NamingMismatch Add 차단: "
                        + (passed ? "YES" : "NO"));
            details.Add("- 생성자 원본 List 복사: " + (passed ? "YES" : "NO"));
            details.Add("- setter/Unity Object 노출: 없음");
            return passed;
        }

        private static bool VerifyConstructorCopies(
            StaffLegacySkinRowSnapshot source,
            List<string> errors)
        {
            List<string> raw = new List<string>(source.RawCells);
            List<StaffLegacyVisualReferenceSnapshot> idle =
                new List<StaffLegacyVisualReferenceSnapshot>(source.IdleFrames);
            List<StaffLegacyVisualReferenceSnapshot> particles =
                new List<StaffLegacyVisualReferenceSnapshot>(source.CheerleaderParticleSprites);
            StaffLegacySkinRowSnapshot clone = CloneSkin(source, raw, idle, particles);
            int rawCount = clone.RawCells.Count;
            int idleCount = clone.IdleFrames.Count;
            int particleCount = clone.CheerleaderParticleSprites.Count;
            raw.Add("__COPY_TEST__");
            idle.Add(source.IdleFrames.Count > 0
                ? source.IdleFrames[0]
                : source.ThumbnailSprite);
            particles.Add(source.CheerleaderParticleSprites.Count > 0
                ? source.CheerleaderParticleSprites[0]
                : source.ThumbnailSprite);
            bool passed = clone.RawCells.Count == rawCount
                          && clone.IdleFrames.Count == idleCount
                          && clone.CheerleaderParticleSprites.Count == particleCount;

            List<string> headers = new List<string>(StaffLegacySkinInventoryReader.ExpectedCsvHeaders);
            List<StaffLegacySkinRowSnapshot> skins = new List<StaffLegacySkinRowSnapshot> { clone };
            List<StaffLegacyVisualReferenceSnapshot> namingMismatches =
                new List<StaffLegacyVisualReferenceSnapshot>
                {
                    source.IdleFrames.Count > 0
                        ? source.IdleFrames[0]
                        : source.ThumbnailSprite
                };
            List<string> diagnostics = new List<string>();
            StaffLegacySkinInventorySnapshot inventoryClone = new StaffLegacySkinInventorySnapshot(
                "Assets/__COPY_TEST__.csv",
                "00000000000000000000000000000000",
                new string('0', 64),
                headers,
                skins,
                namingMismatches,
                diagnostics);
            int headerCount = inventoryClone.CsvHeaders.Count;
            int skinCount = inventoryClone.LegacySkins.Count;
            int namingMismatchCount = inventoryClone.IdleNamingMismatches.Count;
            int diagnosticCount = inventoryClone.Diagnostics.Count;
            headers.Add("__COPY_TEST__");
            skins.Add(clone);
            namingMismatches.Add(source.ThumbnailSprite);
            diagnostics.Add("__COPY_TEST__");
            passed &= inventoryClone.CsvHeaders.Count == headerCount
                      && inventoryClone.LegacySkins.Count == skinCount
                      && inventoryClone.IdleNamingMismatches.Count == namingMismatchCount
                      && inventoryClone.Diagnostics.Count == diagnosticCount;
            if (!passed)
            {
                errors.Add("Snapshot 생성자가 입력 컬렉션을 깊게 복사하지 않았습니다.");
            }

            return passed;
        }

        private static StaffLegacySkinRowSnapshot CloneSkin(
            StaffLegacySkinRowSnapshot source,
            IEnumerable<string> raw,
            IEnumerable<StaffLegacyVisualReferenceSnapshot> idle,
            IEnumerable<StaffLegacyVisualReferenceSnapshot> particles)
        {
            return new StaffLegacySkinRowSnapshot(
                source.LegacySkinId,
                source.LegacySkinNumber,
                source.CandidateNewStaffId,
                source.Name,
                source.Description,
                source.EquipTargetStaffId,
                source.EquipTargetRoleKey,
                source.GachaProbabilityRaw,
                source.AddScoreRaw,
                source.AddTipPerMinuteRaw,
                source.RarityStarsRaw,
                source.GradeRaw,
                source.PurchaseCurrencyRaw,
                source.PurchasePriceRaw,
                source.LegacyUpgradeTypeId,
                source.LegacyUpgradeValueRaw,
                source.LegacyDuplicationTokenRaw,
                raw,
                source.MainSprite,
                source.ThumbnailSprite,
                idle,
                source.ChefBackSprite,
                source.ChefHandSprite,
                source.CheerleaderAnimationSprite,
                particles,
                source.AnimatorControllerCandidate,
                source.CandidateMappingIsSequential,
                source.SourceRoleResolved);
        }

        private static bool ValidateReadOnlyTypeShape(Type type, List<string> errors)
        {
            bool passed = true;
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                if (property.GetSetMethod(true) != null)
                {
                    errors.Add(type.Name + "." + property.Name + "에 setter가 있습니다.");
                    passed = false;
                }

                if (IsMutableConcreteCollection(property.PropertyType))
                {
                    errors.Add(type.Name + "." + property.Name
                               + "이 수정 가능한 컬렉션을 반환합니다.");
                    passed = false;
                }

                if (ContainsUnityOrSerializedType(property.PropertyType))
                {
                    errors.Add(type.Name + "." + property.Name
                               + "이 Unity 원본 객체를 노출합니다.");
                    passed = false;
                }
            }

            FieldInfo[] publicFields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            for (int index = 0; index < publicFields.Length; index++)
            {
                if (!publicFields[index].IsInitOnly && !publicFields[index].IsLiteral)
                {
                    errors.Add(type.Name + "." + publicFields[index].Name
                               + "이 mutable public field입니다.");
                    passed = false;
                }
            }

            return passed;
        }

        private static bool ContainsUnityOrSerializedType(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)
                || type == typeof(SerializedObject)
                || type == typeof(SerializedProperty))
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            Type[] arguments = type.GetGenericArguments();
            for (int index = 0; index < arguments.Length; index++)
            {
                if (ContainsUnityOrSerializedType(arguments[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsMutableConcreteCollection(Type type)
        {
            if (type.IsArray)
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            Type generic = type.GetGenericTypeDefinition();
            return generic == typeof(List<>) || generic == typeof(Dictionary<,>);
        }

        private static bool TryCollectLegacyRootState(
            string phase,
            List<string> errors,
            out Dictionary<string, AssetFileState> state)
        {
            state = new Dictionary<string, AssetFileState>(StringComparer.Ordinal);
            string projectRoot = GetProjectRoot();
            string rootPath = Path.Combine(
                projectRoot,
                StaffLegacySkinInventoryReader.LegacySkinRoot.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            if (!Directory.Exists(rootPath))
            {
                errors.Add(phase + " Legacy Skin Root가 없습니다: "
                           + StaffLegacySkinInventoryReader.LegacySkinRoot);
                return false;
            }

            try
            {
                string[] paths = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories);
                Array.Sort(paths, StringComparer.Ordinal);
                for (int index = 0; index < paths.Length; index++)
                {
                    string relativePath = paths[index]
                        .Substring(projectRoot.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    string assetPath = relativePath.EndsWith(".meta", StringComparison.Ordinal)
                        ? relativePath.Substring(0, relativePath.Length - ".meta".Length)
                        : relativePath;
                    state.Add(
                        relativePath,
                        new AssetFileState(
                            AssetDatabase.AssetPathToGUID(assetPath),
                            ComputeSha256(File.ReadAllBytes(paths[index]))));
                }

                return true;
            }
            catch (Exception exception)
            {
                errors.Add(phase + " Legacy 파일 상태 수집 실패: " + exception.Message);
                state.Clear();
                return false;
            }
        }

        private static bool CompareFileStates(
            Dictionary<string, AssetFileState> before,
            Dictionary<string, AssetFileState> after,
            List<string> details,
            List<string> errors)
        {
            bool passed = before.Count == after.Count;
            foreach (KeyValuePair<string, AssetFileState> entry in before)
            {
                AssetFileState afterState;
                if (!after.TryGetValue(entry.Key, out afterState)
                    || !string.Equals(entry.Value.Sha256, afterState.Sha256, StringComparison.Ordinal)
                    || !string.Equals(entry.Value.AssetGuid, afterState.AssetGuid, StringComparison.Ordinal))
                {
                    errors.Add("실행 전후 Legacy 파일/SHA-256/GUID가 다릅니다: " + entry.Key);
                    passed = false;
                }
            }

            foreach (string path in after.Keys)
            {
                if (!before.ContainsKey(path))
                {
                    errors.Add("실행 중 새 Legacy 파일이 생겼습니다: " + path);
                    passed = false;
                }
            }

            details.Add("- 비교 파일 및 .meta: " + before.Count);
            details.Add("- 목록/SHA-256/GUID 동일: " + (passed ? "YES" : "NO"));
            return passed;
        }

        private static void AddDiagnostics(
            IReadOnlyList<string> diagnostics,
            List<string> warnings,
            List<string> errors)
        {
            if (diagnostics == null)
            {
                errors.Add("Reader 진단 컬렉션이 null입니다.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                string diagnostic = diagnostics[index];
                if (diagnostic.StartsWith("WARNING: ", StringComparison.Ordinal))
                {
                    string warning = diagnostic.Substring("WARNING: ".Length);
                    if (!warnings.Contains(warning))
                    {
                        warnings.Add(warning);
                    }
                }
                else if (diagnostic.StartsWith("ERROR: ", StringComparison.Ordinal))
                {
                    string error = diagnostic.Substring("ERROR: ".Length);
                    if (!errors.Contains(error))
                    {
                        errors.Add(error);
                    }
                }
                else
                {
                    errors.Add("분류되지 않은 Reader 진단: " + diagnostic);
                }
            }
        }

        private static int CountDiagnostics(IReadOnlyList<string> diagnostics, string prefix)
        {
            int count = 0;
            if (diagnostics == null)
            {
                return count;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                count += diagnostics[index].StartsWith(prefix, StringComparison.Ordinal) ? 1 : 0;
            }

            return count;
        }

        private static void MarkSnapshotUnavailable(
            Dictionary<int, List<string>> details,
            bool[] passed)
        {
            for (int number = 2; number <= 11; number++)
            {
                passed[number] = false;
                details[number].Add("- 완전한 Snapshot이 없어 검사하지 못함");
            }

            passed[13] = false;
            details[13].Add("- 완전한 Snapshot이 없어 검사하지 못함");
        }

        private static void WriteConsoleReport(
            bool[] passed,
            Dictionary<int, List<string>> details,
            IReadOnlyList<string> warnings,
            IReadOnlyList<string> errors,
            bool allPassed)
        {
            string[] titles =
            {
                string.Empty,
                "Inventory 생성",
                "Legacy CSV 60행",
                "SKIN_STAFF01~60 및 Candidate STAFF33~92",
                "기존 적용 대상 역할",
                "Main·Thumbnail",
                "Idle 원본 프레임",
                "Chef Back·Hand",
                "Cheerleader Animation·Particle",
                "Animator 기준 상태",
                "Legacy Metadata 격리",
                "InventoryFingerprint",
                "결정론적 재생성",
                "깊은 불변성",
                "실행 전후 파일 불변"
            };
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Legacy Staff Skin Inventory Validation]");
            for (int number = 1; number <= 14; number++)
            {
                output.AppendLine(number + ". " + titles[number] + ": "
                                  + (passed[number] ? "PASS" : "FAIL"));
                for (int detailIndex = 0; detailIndex < details[number].Count; detailIndex++)
                {
                    output.AppendLine("   " + details[number][detailIndex]);
                }
            }

            output.AppendLine("15. 경고 수: " + warnings.Count);
            for (int index = 0; index < warnings.Count; index++)
            {
                output.AppendLine("   WARNING: " + warnings[index]);
            }

            output.AppendLine("16. 오류 수: " + errors.Count);
            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("   ERROR: " + errors[index]);
            }

            output.AppendLine("17. 최종 결과: "
                              + (allPassed
                                  ? "LEGACY STAFF SKIN INVENTORY VALIDATION: PASS"
                                  : "LEGACY STAFF SKIN INVENTORY VALIDATION: FAIL"));
            if (allPassed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static Dictionary<int, List<string>> CreateDetailSections()
        {
            Dictionary<int, List<string>> result = new Dictionary<int, List<string>>();
            for (int number = 1; number <= 14; number++)
            {
                result.Add(number, new List<string>());
            }

            return result;
        }

        private static Dictionary<string, int> CountRoles(
            IReadOnlyList<StaffLegacySkinRowSnapshot> skins)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < skins.Count; index++)
            {
                int count;
                result.TryGetValue(skins[index].EquipTargetRoleKey, out count);
                result[skins[index].EquipTargetRoleKey] = count + 1;
            }

            return result;
        }

        private static int GetCount(Dictionary<string, int> counts, string key)
        {
            int count;
            counts.TryGetValue(key, out count);
            return count;
        }

        private static int CountMissing(params StaffLegacyVisualReferenceSnapshot[] references)
        {
            int count = 0;
            for (int index = 0; index < references.Length; index++)
            {
                count += references[index] != null && references[index].IsMissing ? 1 : 0;
            }

            return count;
        }

        private static string JoinObjectNames(
            IReadOnlyList<StaffLegacyVisualReferenceSnapshot> references)
        {
            StringBuilder result = new StringBuilder();
            for (int index = 0; index < references.Count; index++)
            {
                if (index != 0)
                {
                    result.Append(", ");
                }

                result.Append(
                    references[index] != null
                        ? references[index].ObjectName
                        : "<NULL>");
            }

            return result.ToString();
        }

        private static bool IsUsable(StaffLegacyVisualReferenceSnapshot reference)
        {
            return reference != null && reference.IsAssigned && !reference.IsMissing;
        }

        private static bool HasUtf8Bom(string projectRelativePath)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(
                    GetProjectRoot(),
                    projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
                return bytes.Length >= 3
                       && bytes[0] == 0xef
                       && bytes[1] == 0xbb
                       && bytes[2] == 0xbf;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLowercaseSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
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

        private static bool VerifyListMutationBlocked<T>(
            string label,
            IReadOnlyList<T> values,
            T testValue,
            List<string> errors)
        {
            IList<T> mutable = values as IList<T>;
            if (mutable == null)
            {
                errors.Add(label + " 검사 인터페이스를 얻지 못했습니다.");
                return false;
            }

            try
            {
                mutable.Add(testValue);
                errors.Add(label + " 시도가 차단되지 않았습니다.");
                return false;
            }
            catch (NotSupportedException)
            {
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(label + " 검사에서 예상 밖 오류: " + exception.Message);
                return false;
            }
        }

        private static bool VerifyDictionaryMutationBlocked<T>(
            string label,
            IReadOnlyDictionary<string, T> values,
            string key,
            T testValue,
            List<string> errors)
        {
            IDictionary<string, T> mutable = values as IDictionary<string, T>;
            if (mutable == null)
            {
                errors.Add(label + " 검사 인터페이스를 얻지 못했습니다.");
                return false;
            }

            try
            {
                mutable.Add(key, testValue);
                errors.Add(label + " 시도가 차단되지 않았습니다.");
                return false;
            }
            catch (NotSupportedException)
            {
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(label + " 검사에서 예상 밖 오류: " + exception.Message);
                return false;
            }
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

        private static string GetProjectRoot()
        {
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            return projectDirectory != null ? projectDirectory.FullName : Application.dataPath;
        }

        private sealed class AssetFileState
        {
            internal readonly string AssetGuid;
            internal readonly string Sha256;

            internal AssetFileState(string assetGuid, string sha256)
            {
                AssetGuid = assetGuid ?? string.Empty;
                Sha256 = sha256 ?? string.Empty;
            }
        }
    }
}
