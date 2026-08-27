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
    internal static class StaffDataAssetInventoryValidator
    {
        private const string MenuPath =
            "Tools/Panda Restaurant/Staff/Validate Current Staff Asset Inventory";
        private const string AssignedCookingScriptGuid =
            "f6dec9edb1244c84d99fc7f5daea02f9";
        private const string FoodPaymentTipScriptGuid =
            "5bd8254a6ae09954aba812b6ddc1b280";
        private const string FoodPaymentTipScriptPath =
            "Assets/Scripts/Staff/StaffSkill/FoodPaymentTipUpSkill.cs";
        private const string FoodPriceScriptGuid =
            "2166c039b7672614f9452cc7ec63189a";
        private const string FoodPriceScriptPath =
            "Assets/Scripts/Staff/StaffSkill/FoodPriceUpSkill.cs";
        private const string NormalCustomerMoveScriptGuid =
            "436b93c9b427dbb45a839f587d572cdc";
        private const string NormalCustomerMoveScriptPath =
            "Assets/Scripts/Staff/StaffSkill/NormalCustomerMoveSpeedUpSkill.cs";
        private const string GlobalCookingScriptGuid =
            "e0e2c583b78c98449a5c47e6abfa0ed7";
        private const string GlobalCookingScriptPath =
            "Assets/Scripts/Staff/StaffSkill/GlobalCookingSpeedUpSkill.cs";
        private const string GlobalRemainingCookingScriptPath =
            "Assets/Scripts/Staff/StaffSkill/GlobalRemainingCookingTimeReductionSkill.cs";
        private const string Skill09ActivePath =
            "Assets/Scripts/Datas/Staff/Skill/STAFF27Skill.asset";
        private const string Skill09LegacyGlobalPath =
            "Assets/Scripts/Datas/Staff/LegacySkill/STAFF27GlobalCookingSpeedUpSkill.asset";
        private const string Skill09LegacySpeedPath =
            "Assets/Scripts/Datas/Staff/LegacySkill/STAFF27Skill.asset";
        private const string Skill09LegacyGlobalGuid =
            "f7650976be31bb0458d7625e3a531527";
        private const string Skill09LegacySpeedGuid =
            "4803555ba0e7f7f46af709248b6ac2be";
        private const string Skill09LegacySpeedAssetSha256 =
            "bf3d91b6f1b78bedad789b7a3131798356522b7a619f8824194e9ba46795aae6";
        private const string Skill09LegacySpeedMetaSha256 =
            "62aa0dfe48bcf573136c6677ac4aacde1bff1282d8eee306259a8957a96766f5";
        private const int ExpectedComparisonFileCount = 164;
        private const string PreA1InventoryFingerprint =
            "90c3a56ca032d6542359d392ca014ce29b3c367cb227238165d9eadacf0be15b";
        private const string PreA1PlanFingerprint =
            "9a4162af233fdeb6394687cc65d39108655a9b99c079df039177b28d1ea11fc0";
        private const string AllStaffMoveScriptGuid =
            "8c58ec39a18e0964595b700ba01914e9";
        private const string AllStaffMoveScriptPath =
            "Assets/Scripts/Staff/StaffSkill/AllStaffMoveSpeedUpSkill.cs";

        private static readonly Dictionary<string, int> ExpectedStaffClassCounts =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "WaiterData", 7 },
                { "ManagerData", 5 },
                { "MarketerData", 5 },
                { "ChefData", 7 },
                { "CleanerData", 6 },
                { "GuardData", 2 }
            };

        private static readonly Dictionary<string, int> ExpectedSkillClassCounts =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "SpeedUpSkill", 12 },
                { "TouchAddCustomerButtonSkill", 3 },
                { "AssignedCookingSpeedUpSkill", 4 },
                { "FoodPaymentTipUpSkill", 1 },
                { "FoodPriceUpSkill", 6 },
                { "NormalCustomerMoveSpeedUpSkill", 5 },
                { "GlobalCookingSpeedUpSkill", 0 },
                { "GlobalRemainingCookingTimeReductionSkill", 1 },
                { "AllStaffMoveSpeedUpSkill", 0 }
            };

        private static readonly Skill04MigrationTarget[] Skill04MigrationTargets =
        {
            new Skill04MigrationTarget("STAFF17", "Staff17Skill.asset", "STAFF17Skill", "c1305190b57c1d54482ece9b2e58be3d", 18, 200, 25, 160),
            new Skill04MigrationTarget("STAFF19", "Staff19Skill.asset", "STAFF19Skill", "67fad8354daa0194fbbcf5833b9ebdca", 24, 150, 25, 160),
            new Skill04MigrationTarget("STAFF20", "Staff20Skill.asset", "STAFF20Skill", "ab2b64bcb83dc9d48b5773f0c88a830e", 27, 150, 25, 160),
            new Skill04MigrationTarget("STAFF29", "STAFF29SKILL.asset", "STAFF29SKILL", "6513e175122c20641a60cad9e71895fa", 30, 150, 30, 150)
        };

        private static readonly Skill06MigrationTarget Skill06Target =
            new Skill06MigrationTarget(
                "STAFF09",
                "STAFF09Skill.asset",
                "STAFF09Skill",
                "3576053ed0b398d43a296e16eaf3aff6",
                16,
                150,
                30,
                200);

        private static readonly Skill05MigrationTarget[] Skill05MigrationTargets =
        {
            new Skill05MigrationTarget("STAFF04", "Staff04Skill.asset", "STAFF04Skill", "ace4d82401b0b084691be89b9fc0f63e", "SpeedUpSkill", 100, 16, 150, 30, 200),
            new Skill05MigrationTarget("STAFF05", "Staff05Skill.asset", "STAFF05Skill", "224358ced57ae144ba65a5f1692e6f1c", "SpeedUpSkill", 100, 18, 150, 30, 200),
            new Skill05MigrationTarget("STAFF07", "STAFF07Skill.asset", "STAFF07Skill", "18348c8c96719374da1f6ba1bcd2987d", "SpeedUpSkill", 100, 10, 150, 30, 200),
            new Skill05MigrationTarget("STAFF13", "Staff13Skill.asset", "STAFF13Skill", "cc1627697d70f09418e134e717be2a29", "TouchAddCustomerButtonSkill", 0.5f, 13, 150, 30, 200),
            new Skill05MigrationTarget("STAFF26", "Staff26Skill.asset", "STAFF26Skill", "f3029210afede654ca6f3c33a2016896", "SpeedUpSkill", 100, 30, 50, 35, 190),
            new Skill05MigrationTarget("STAFF32", "Staff32Skill.asset", "Staff32Skill", "9579cb0591dadfa4da2e662700923026", "SpeedUpSkill", 100, 30, 80, 35, 190)
        };

        private static readonly RemainingSkillMigrationTarget[] RemainingSkillMigrationTargets =
        {
            new RemainingSkillMigrationTarget("STAFF06", "STAFF06Skill.asset", "STAFF06Skill", "13f8b997e9fafaf439550199ddfb76f7", "SpeedUpSkill", 100, 7, 150, "NormalCustomerMoveSpeedUpSkill", NormalCustomerMoveScriptGuid, "손님 이동 속도 (100%) 증가", 100, 18, 150),
            new RemainingSkillMigrationTarget("STAFF08", "STAFF08Skill.asset", "STAFF08Skill", "6daef952310a94a41b88276b9cd1cef6", "SpeedUpSkill", 100, 13, 150, "NormalCustomerMoveSpeedUpSkill", NormalCustomerMoveScriptGuid, "손님 이동 속도 (100%) 증가", 100, 18, 150),
            new RemainingSkillMigrationTarget("STAFF10", "STAFF10Skill.asset", "STAFF10Skill", "95590518b98a27a4cacc1a699f5f9868", "SpeedUpSkill", 100, 18, 150, "NormalCustomerMoveSpeedUpSkill", NormalCustomerMoveScriptGuid, "손님 이동 속도 (100%) 증가", 100, 18, 150),
            new RemainingSkillMigrationTarget("STAFF15", "Staff15Skill.asset", "STAFF15Skill", "d417214c6d7368941984a20f5d509d44", "TouchAddCustomerButtonSkill", 0.5f, 20, 100, "NormalCustomerMoveSpeedUpSkill", NormalCustomerMoveScriptGuid, "손님 이동 속도 (100%) 증가", 100, 18, 150),
            new RemainingSkillMigrationTarget("STAFF30", "Staff30Skill.asset", "Staff30Skill", "474e93abb1dd6db45baa9ab57be2cef1", "SpeedUpSkill", 100, 30, 80, "NormalCustomerMoveSpeedUpSkill", NormalCustomerMoveScriptGuid, "손님 이동 속도 (100%) 증가", 100, 20, 145)
        };

        private static readonly ChefPassiveTarget[] ChefPassiveTargets =
        {
            new ChefPassiveTarget("STAFF16", "NORMAL", 50f),
            new ChefPassiveTarget("STAFF17", "NORMAL", 50f),
            new ChefPassiveTarget("STAFF18", "NORMAL", 50f),
            new ChefPassiveTarget("STAFF19", "NORMAL", 50f),
            new ChefPassiveTarget("STAFF20", "NORMAL", 50f),
            new ChefPassiveTarget("STAFF27", "SPECIAL", 200f),
            new ChefPassiveTarget("STAFF29", "UNIQUE", 100f)
        };

        [MenuItem(MenuPath)]
        private static void ValidateCurrentStaffAssetInventory()
        {
            List<string> errors = new List<string>();
            Dictionary<int, List<string>> details = CreateDetailSections();

            Dictionary<string, AssetFileState> beforeState;
            bool beforeStateBuilt = TryCollectTargetAssetState(
                "실행 전",
                errors,
                out beforeState);

            StaffDataAssetInventorySnapshot firstSnapshot;
            IReadOnlyList<string> firstDiagnostics;
            bool firstBuilt = StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                out firstSnapshot,
                out firstDiagnostics);
            if (!firstBuilt || firstSnapshot == null)
            {
                AddDiagnostics("첫 번째 Inventory", firstDiagnostics, errors);
            }

            StaffDataAssetInventorySnapshot secondSnapshot;
            IReadOnlyList<string> secondDiagnostics;
            bool secondBuilt = StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                out secondSnapshot,
                out secondDiagnostics);
            if (!secondBuilt || secondSnapshot == null)
            {
                AddDiagnostics("두 번째 Inventory", secondDiagnostics, errors);
            }

            bool inventoryPassed = firstBuilt && firstSnapshot != null;
            bool staffIdentityPassed = inventoryPassed
                                       && ValidateStaffIdentity(firstSnapshot, details[2], errors);
            bool classDistributionPassed = inventoryPassed
                                           && ValidateStaffClassDistribution(
                                               firstSnapshot,
                                               details[3],
                                               errors);
            bool levelStructurePassed = inventoryPassed
                                        && ValidateLevelStructure(firstSnapshot, details[4], errors);
            bool visualReferencesPassed = inventoryPassed
                                          && ValidateVisualReferences(
                                              firstSnapshot,
                                              details[5],
                                              errors);
            bool officialDataStatePassed = inventoryPassed
                                           && ValidateExistingV18OfficialDataState(
                                               firstSnapshot,
                                               details[6],
                                               errors);
            bool skillStructurePassed = inventoryPassed
                                        && ValidateSkillStructure(
                                            firstSnapshot,
                                            details[7],
                                            errors);
            bool graphPassed = inventoryPassed
                               && ValidateStaffSkillGraph(firstSnapshot, details[8], errors);
            bool fingerprintPassed = inventoryPassed
                                     && ValidateFingerprint(firstSnapshot, details[9], errors);
            bool deterministicPassed = inventoryPassed
                                       && secondBuilt
                                       && secondSnapshot != null
                                       && ValidateDeterministicRebuild(
                                           firstSnapshot,
                                           secondSnapshot,
                                           errors);
            bool immutabilityPassed = inventoryPassed
                                      && ValidateDeepImmutability(firstSnapshot, errors);
            bool skill04MigrationPassed = inventoryPassed
                                          && ValidateSkill04Migration(
                                              firstSnapshot,
                                              details[13],
                                              errors);
            bool skill06MigrationPassed = inventoryPassed
                                           && ValidateSkill06Migration(
                                               firstSnapshot,
                                               details[14],
                                               errors);
            bool skill05MigrationPassed = inventoryPassed
                                           && ValidateSkill05Migration(
                                               firstSnapshot,
                                               details[15],
                                               errors);
            bool remainingSkillMigrationPassed = inventoryPassed
                                                 && ValidateRemainingSkillMigration(
                                                     firstSnapshot,
                                                     details[16],
                                                     errors);
            bool chefPassivePassed = inventoryPassed
                                      && ValidateChefOfficialPassive(
                                          firstSnapshot,
                                          details[17],
                                          errors);

            Dictionary<string, AssetFileState> afterState;
            bool afterStateBuilt = TryCollectTargetAssetState(
                "실행 후",
                errors,
                out afterState);
            bool assetStatePassed = beforeStateBuilt
                                    && afterStateBuilt
                                    && CompareAssetStates(beforeState, afterState, details[12], errors);

            bool passed = inventoryPassed
                          && staffIdentityPassed
                          && classDistributionPassed
                          && levelStructurePassed
                          && visualReferencesPassed
                          && officialDataStatePassed
                          && skillStructurePassed
                          && graphPassed
                          && fingerprintPassed
                          && deterministicPassed
                          && immutabilityPassed
                          && skill04MigrationPassed
                          && skill06MigrationPassed
                          && skill05MigrationPassed
                          && remainingSkillMigrationPassed
                          && chefPassivePassed
                          && assetStatePassed
                          && errors.Count == 0;

            StringBuilder output = new StringBuilder();
            output.AppendLine("[Current Staff Asset Inventory Validation]");
            output.AppendLine();
            AppendResult(output, 1, "Inventory 생성", inventoryPassed, details[1]);
            AppendResult(output, 2, "StaffData 수·ID·GUID", staffIdentityPassed, details[2]);
            AppendResult(output, 3, "역할 클래스 분포", classDistributionPassed, details[3]);
            AppendResult(output, 4, "레벨 배열 구조", levelStructurePassed, details[4]);
            AppendResult(output, 5, "공통·역할별 시각 참조", visualReferencesPassed, details[5]);
            AppendResult(
                output,
                6,
                "Existing Staff V18 Official Data 상태",
                officialDataStatePassed,
                details[6]);
            AppendResult(output, 7, "Skill 에셋 구조", skillStructurePassed, details[7]);
            AppendResult(output, 8, "Staff-Skill 참조 그래프", graphPassed, details[8]);
            AppendResult(output, 9, "InventoryFingerprint", fingerprintPassed, details[9]);
            AppendResult(output, 10, "결정론적 재생성", deterministicPassed, details[10]);
            AppendResult(output, 11, "깊은 불변성", immutabilityPassed, details[11]);
            AppendResult(output, 12, "실행 전후 에셋 불변", assetStatePassed, details[12]);
            AppendResult(output, 13, "Skill04 기존 직원·공식 효과·Legacy 보존", skill04MigrationPassed, details[13]);
            AppendResult(output, 14, "Skill06 기존 직원·Legacy 보존", skill06MigrationPassed, details[14]);
            AppendResult(output, 15, "Skill05 기존 직원·Legacy 보존", skill05MigrationPassed, details[15]);
            AppendResult(output, 16, "Skill08·09·10 기존 직원·Legacy 보존", remainingSkillMigrationPassed, details[16]);
            AppendResult(output, 17, "Chef 공식 패시브 V8", chefPassivePassed, details[17]);
            output.AppendLine("18. 오류 수: " + errors.Count);
            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("   ERROR: " + errors[index]);
            }

            output.AppendLine("19. 최종 결과: " + (passed ? "PASS" : "FAIL"));
            output.AppendLine();
            output.AppendLine(
                "CURRENT STAFF ASSET INVENTORY VALIDATION: " + (passed ? "PASS" : "FAIL"));

            if (passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static bool ValidateStaffIdentity(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            if (snapshot.Staff.Count != 32)
            {
                errors.Add("StaffData 수가 다릅니다. 예상 32, 실제 " + snapshot.Staff.Count);
                passed = false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> guids = new HashSet<string>(StringComparer.Ordinal);
            int emptyIdCount = 0;
            int duplicateIdCount = 0;
            int duplicateGuidCount = 0;
            int fileNameMismatchCount = 0;
            for (int index = 0; index < snapshot.Staff.Count; index++)
            {
                StaffDataAssetSnapshot staff = snapshot.Staff[index];
                if (string.IsNullOrEmpty(staff.Id))
                {
                    emptyIdCount++;
                }
                else if (!ids.Add(staff.Id))
                {
                    duplicateIdCount++;
                }

                if (!guids.Add(staff.AssetGuid))
                {
                    duplicateGuidCount++;
                }

                if (!staff.FileNameMatchesId)
                {
                    fileNameMismatchCount++;
                }
            }

            for (int number = 1; number <= 32; number++)
            {
                string id = "STAFF" + number.ToString("00", CultureInfo.InvariantCulture);
                if (!ids.Contains(id))
                {
                    errors.Add("필수 StaffData ID가 없습니다: " + id);
                    passed = false;
                }
            }

            int futureIdCount = 0;
            for (int number = 33; number <= 92; number++)
            {
                string id = "STAFF" + number.ToString("00", CultureInfo.InvariantCulture);
                if (ids.Contains(id))
                {
                    futureIdCount++;
                }
            }

            passed &= RequireZero("빈 Staff ID", emptyIdCount, errors);
            passed &= RequireZero("중복 Staff ID", duplicateIdCount, errors);
            passed &= RequireZero("중복 StaffData GUID", duplicateGuidCount, errors);
            passed &= RequireZero("파일명과 ID 불일치", fileNameMismatchCount, errors);
            passed &= RequireZero("STAFF33~STAFF92 현재 에셋", futureIdCount, errors);
            details.Add("- StaffData: " + snapshot.Staff.Count + "개");
            details.Add("- STAFF01~STAFF32 ID 확인, STAFF33~STAFF92: " + futureIdCount + "개");
            details.Add("- 빈 ID/중복 ID/GUID 중복/파일명 불일치: "
                        + emptyIdCount + "/" + duplicateIdCount + "/"
                        + duplicateGuidCount + "/" + fileNameMismatchCount);
            return passed;
        }

        private static bool ValidateStaffClassDistribution(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            Dictionary<string, int> counts = CountStaffClasses(snapshot.Staff);
            bool passed = true;
            foreach (KeyValuePair<string, int> expected in ExpectedStaffClassCounts)
            {
                int actual;
                counts.TryGetValue(expected.Key, out actual);
                details.Add("- " + expected.Key + ": " + actual);
                if (actual != expected.Value)
                {
                    errors.Add(
                        expected.Key + " 수가 다릅니다. 예상 " + expected.Value
                        + ", 실제 " + actual);
                    passed = false;
                }
            }

            if (counts.Count != ExpectedStaffClassCounts.Count)
            {
                errors.Add("예상하지 않은 StaffData 파생 클래스가 있습니다.");
                passed = false;
            }

            return passed;
        }

        private static bool ValidateLevelStructure(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            int staff02SixCount = 0;
            int normalFiveCount = 0;
            int unexpectedLevelCount = 0;
            int chefCount = 0;
            int chefFieldCount = 0;
            int chefFieldGapCount = 0;
            int chefAddSpeedValueCount = 0;
            int chefAddSpeedZeroCount = 0;
            int chefAddSpeedMissingCount = 0;
            int chefAddSpeedInvalidCount = 0;
            int chefAddSpeedNonZeroCount = 0;
            for (int index = 0; index < snapshot.Staff.Count; index++)
            {
                StaffDataAssetSnapshot staff = snapshot.Staff[index];
                if (!staff.HasExpectedLevelArray || staff.LevelCount != staff.Levels.Count)
                {
                    errors.Add("레벨 배열 구조가 올바르지 않습니다: " + staff.Id);
                    passed = false;
                }

                if (string.Equals(staff.Id, "STAFF02", StringComparison.Ordinal)
                    && staff.LevelCount == 6)
                {
                    staff02SixCount++;
                }
                else if (!string.Equals(staff.Id, "STAFF02", StringComparison.Ordinal)
                         && staff.LevelCount == 5)
                {
                    normalFiveCount++;
                }
                else
                {
                    unexpectedLevelCount++;
                }

                if (string.Equals(staff.ConcreteTypeName, "ChefData", StringComparison.Ordinal))
                {
                    chefCount++;
                    if (staff.HasChefAddSpeedField)
                    {
                        chefFieldCount++;
                    }
                    else
                    {
                        chefFieldGapCount++;
                    }

                    for (int levelIndex = 0; levelIndex < staff.Levels.Count; levelIndex++)
                    {
                        float? addSpeed = staff.Levels[levelIndex].AddSpeed;
                        if (!addSpeed.HasValue)
                        {
                            chefAddSpeedMissingCount++;
                            continue;
                        }

                        chefAddSpeedValueCount++;
                        if (float.IsNaN(addSpeed.Value) || float.IsInfinity(addSpeed.Value))
                        {
                            chefAddSpeedInvalidCount++;
                        }
                        else if (addSpeed.Value == 0f)
                        {
                            chefAddSpeedZeroCount++;
                        }
                        else
                        {
                            chefAddSpeedNonZeroCount++;
                        }
                    }
                }
            }

            if (staff02SixCount != 1 || normalFiveCount != 31 || unexpectedLevelCount != 0)
            {
                errors.Add(
                    "레벨 배열 기준 상태가 다릅니다. STAFF02 6칸=" + staff02SixCount
                    + ", 나머지 5칸=" + normalFiveCount
                    + ", 예상 외=" + unexpectedLevelCount);
                passed = false;
            }

            bool chefPreA1Values = chefAddSpeedZeroCount == 35
                                   && chefAddSpeedNonZeroCount == 0;
            bool chefPostA1Values = chefAddSpeedZeroCount == 7
                                    && chefAddSpeedNonZeroCount == 28;
            if (chefCount != 7
                || chefFieldCount != 7
                || chefFieldGapCount != 0
                || chefAddSpeedValueCount != 35
                || chefAddSpeedMissingCount != 0
                || chefAddSpeedInvalidCount != 0
                || (!chefPreA1Values && !chefPostA1Values))
            {
                errors.Add(
                    "Chef AddSpeed가 strict Pre-A1 또는 Post-A1 상태가 아닙니다. Chef/field/value/zero/missing/invalid/non-zero: "
                    + chefCount + "/" + chefFieldCount + "/" + chefAddSpeedValueCount + "/"
                    + chefAddSpeedZeroCount + "/" + chefAddSpeedMissingCount + "/"
                    + chefAddSpeedInvalidCount + "/" + chefAddSpeedNonZeroCount + ".");
                passed = false;
            }

            details.Add("- KNOWN_MIGRATION_REQUIRED: STAFF02 레벨 배열 6칸");
            details.Add("- 나머지 StaffData 레벨 배열 5칸: " + normalFiveCount + "명");
            details.Add("- Chef AddSpeed 필드 존재: " + chefFieldCount + "/7");
            details.Add("- Chef AddSpeed 필드 부재: " + chefFieldGapCount);
            details.Add("- Chef AddSpeed 값 존재: " + chefAddSpeedValueCount + "/35");
            details.Add("- Chef AddSpeed 상태: "
                        + (chefPreA1Values
                            ? StaffDataDryRunPlanner.ExistingStaffV18DataReadyToApplyMarker
                            : chefPostA1Values
                                ? StaffDataDryRunPlanner.ExistingStaffV18DataAppliedMarker
                                : StaffDataDryRunPlanner.ExistingStaffV18DataPartialMarker));
            details.Add("- Chef AddSpeed 0/비영: "
                        + chefAddSpeedZeroCount + "/" + chefAddSpeedNonZeroCount);
            details.Add("- Chef AddSpeed Missing/비정상/비영: "
                        + chefAddSpeedMissingCount + "/" + chefAddSpeedInvalidCount + "/"
                        + chefAddSpeedNonZeroCount);
            return passed;
        }

        private static bool ValidateVisualReferences(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int validSprites = 0;
            int validThumbnails = 0;
            int animatorUnassigned = 0;
            int idleUnassigned = 0;
            int idleMissing = 0;
            int roleSpecificMissing = 0;
            for (int index = 0; index < snapshot.Staff.Count; index++)
            {
                StaffDataAssetSnapshot staff = snapshot.Staff[index];
                if (IsValidAssignedReference(staff.SpriteReference))
                {
                    validSprites++;
                }

                if (IsValidAssignedReference(staff.ThumbnailReference))
                {
                    validThumbnails++;
                }

                if (!staff.AnimatorControllerReference.IsAssigned
                    && !staff.AnimatorControllerReference.IsMissing)
                {
                    animatorUnassigned++;
                }

                for (int idleIndex = 0; idleIndex < staff.IdleSpriteReferences.Count; idleIndex++)
                {
                    StaffAssetReferenceSnapshot reference = staff.IdleSpriteReferences[idleIndex];
                    if (!reference.IsAssigned && !reference.IsMissing)
                    {
                        idleUnassigned++;
                    }

                    if (reference.IsMissing)
                    {
                        idleMissing++;
                    }
                }

                roleSpecificMissing += CountMissing(
                    staff.BackSpriteReference,
                    staff.HandSpriteReference,
                    staff.UiSpriteReference,
                    staff.AnimationSpriteReference);
                for (int particleIndex = 0;
                     particleIndex < staff.ParticleSpriteReferences.Count;
                     particleIndex++)
                {
                    if (staff.ParticleSpriteReferences[particleIndex].IsMissing)
                    {
                        roleSpecificMissing++;
                    }
                }
            }

            bool passed = true;
            if (validSprites != 32 || validThumbnails != 32)
            {
                errors.Add(
                    "Sprite/Thumbnail 유효 수가 다릅니다: " + validSprites
                    + "/" + validThumbnails);
                passed = false;
            }

            if (animatorUnassigned != 32)
            {
                errors.Add("AnimatorController 미할당 수가 다릅니다. 예상 32, 실제 " + animatorUnassigned);
                passed = false;
            }

            passed &= RequireZero("할당된 Idle Sprite Missing Reference", idleMissing, errors);
            passed &= RequireZero("Chef·Marketer 전용 Missing Reference", roleSpecificMissing, errors);
            details.Add("- Sprite 유효: " + validSprites + "/32");
            details.Add("- Thumbnail 유효: " + validThumbnails + "/32");
            details.Add("- AnimatorController 미할당: " + animatorUnassigned + "명 (현재 정상 구조)");
            details.Add("- Idle Sprite 빈 칸/Missing: " + idleUnassigned + "/" + idleMissing);
            details.Add("- Chef·Marketer 전용 Missing Reference: " + roleSpecificMissing);
            return passed;
        }

        private static bool ValidateExistingV18OfficialDataState(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            StaffDataDryRunPlanSnapshot firstPlan;
            IReadOnlyList<string> firstDiagnostics;
            if (!StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                    out firstPlan,
                    out firstDiagnostics)
                || firstPlan == null)
            {
                AddDiagnostics("Existing V18 first plan", firstDiagnostics, errors);
                return false;
            }

            StaffDataDryRunPlanSnapshot secondPlan;
            IReadOnlyList<string> secondDiagnostics;
            if (!StaffDataDryRunPlanner.TryBuildCanonicalV8ReadOnlyPlan(
                    out secondPlan,
                    out secondDiagnostics)
                || secondPlan == null)
            {
                AddDiagnostics("Existing V18 second plan", secondDiagnostics, errors);
                return false;
            }

            string marker;
            string reason;
            bool classified = StaffDataDryRunPlanner.TryClassifyExistingV18DataState(
                firstPlan,
                out marker,
                out reason);
            int normal1Count = 0;
            for (int index = 0; index < snapshot.Staff.Count; index++)
            {
                if (string.Equals(snapshot.Staff[index].RankName, "Normal1", StringComparison.Ordinal)
                    && snapshot.Staff[index].RankValue == 0)
                {
                    normal1Count++;
                }
            }

            int officialStaffMatches = 0;
            int nameMatches = 0;
            int descriptionMatches = 0;
            int rankMatches = 0;
            int speedMatches = 0;
            int roleFields = 0;
            int upgradeFields = 0;
            int changedA1Fields = 0;
            int unsupportedFields = 0;
            bool linksValid = firstPlan.CurrentInventoryFingerprint == snapshot.InventoryFingerprint;
            for (int planIndex = 0; planIndex < firstPlan.StaffPlans.Count; planIndex++)
            {
                StaffDataDryRunStaffPlan staff = firstPlan.StaffPlans[planIndex];
                if (staff.AssetAction != StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    continue;
                }

                StaffDataAssetSnapshot current;
                if (!snapshot.TryGetStaff(staff.StaffId, out current) || current == null)
                {
                    linksValid = false;
                    continue;
                }

                linksValid &= current.AssetPath == staff.ExistingAssetPath
                              && current.AssetGuid == staff.ExistingAssetGuid
                              && current.ScriptGuid == staff.ExistingScriptGuid
                              && current.ConcreteTypeName == staff.TargetConcreteTypeName;
                int staffDirectFields = 0;
                int staffRoleFields = 0;
                int staffUpgradeFields = 0;
                bool staffMatches = true;
                for (int fieldIndex = 0; fieldIndex < staff.FieldPlans.Count; fieldIndex++)
                {
                    StaffDataDryRunFieldPlan field = staff.FieldPlans[fieldIndex];
                    if (field.Disposition != StaffDryRunFieldDisposition.AUTO_UPDATE_EXISTING)
                    {
                        continue;
                    }

                    if (!StaffDataDryRunPlanner.IsExistingV18A1FieldPath(field.FieldPath))
                    {
                        unsupportedFields++;
                        staffMatches = false;
                        continue;
                    }

                    changedA1Fields += field.IsChanged ? 1 : 0;
                    staffMatches &= !field.IsChanged;
                    if (field.FieldPath == "StaffData._name")
                    {
                        staffDirectFields++;
                        nameMatches += !field.IsChanged ? 1 : 0;
                    }
                    else if (field.FieldPath == "StaffData._description")
                    {
                        staffDirectFields++;
                        descriptionMatches += !field.IsChanged ? 1 : 0;
                    }
                    else if (field.FieldPath == "StaffData._rank")
                    {
                        staffDirectFields++;
                        rankMatches += !field.IsChanged ? 1 : 0;
                    }
                    else if (field.FieldPath == "StaffData._speed")
                    {
                        staffDirectFields++;
                        speedMatches += !field.IsChanged ? 1 : 0;
                    }
                    else if (IsUpgradeA1Field(field.FieldPath))
                    {
                        staffUpgradeFields++;
                        upgradeFields++;
                    }
                    else
                    {
                        staffRoleFields++;
                        roleFields++;
                    }
                }

                int expectedRoleFields = ExpectedRoleA1FieldCount(staff.RoleKey);
                staffMatches &= staffDirectFields == 4
                                && staffRoleFields == expectedRoleFields
                                && staffUpgradeFields == 14;
                officialStaffMatches += staffMatches ? 1 : 0;
            }

            bool deterministic = firstPlan.PlanFingerprint == secondPlan.PlanFingerprint
                                 && firstPlan.CurrentInventoryFingerprint
                                 == secondPlan.CurrentInventoryFingerprint;
            bool ready = marker
                         == StaffDataDryRunPlanner.ExistingStaffV18DataReadyToApplyMarker
                         && snapshot.InventoryFingerprint == PreA1InventoryFingerprint
                         && firstPlan.PlanFingerprint == PreA1PlanFingerprint
                         && normal1Count == 32;
            bool applied = marker
                           == StaffDataDryRunPlanner.ExistingStaffV18DataAppliedMarker
                           && officialStaffMatches == 32
                           && nameMatches == 32
                           && descriptionMatches == 32
                           && rankMatches == 32
                           && speedMatches == 32
                           && changedA1Fields == 0
                           && unsupportedFields == 0;
            bool passed = classified
                          && (ready || applied)
                          && linksValid
                          && deterministic;
            details.Add("- State Marker: " + marker);
            details.Add("- InventoryFingerprint: " + snapshot.InventoryFingerprint);
            details.Add("- PlanFingerprint: " + firstPlan.PlanFingerprint);
            details.Add("- Official name/description/rank/speed: "
                        + nameMatches + "/" + descriptionMatches + "/"
                        + rankMatches + "/" + speedMatches);
            details.Add("- Official role/upgrade FieldPlans: "
                        + roleFields + "/" + upgradeFields);
            details.Add("- Official Staff matches: " + officialStaffMatches + "/32");
            details.Add("- Changed/unsupported A1 fields: "
                        + changedA1Fields + "/" + unsupportedFields);
            details.Add("- Deterministic inventory/plan regeneration: "
                        + (deterministic ? "PASS" : "FAIL"));
            if (!passed)
            {
                errors.Add(
                    "Existing Staff V18 데이터가 strict Pre-A1/Post-A1 상태가 아닙니다: "
                    + marker + " | " + reason);
            }

            return passed;
        }

        private static bool IsUpgradeA1Field(string fieldPath)
        {
            return fieldPath.EndsWith("._upgradeMinScore", StringComparison.Ordinal)
                   || fieldPath.EndsWith("._moneyType", StringComparison.Ordinal)
                   || fieldPath.EndsWith("._price", StringComparison.Ordinal);
        }

        private static int ExpectedRoleA1FieldCount(string roleKey)
        {
            return roleKey == "CLEANER" || roleKey == "CHEF" ? 10 : 5;
        }

        private static bool ValidateSkillStructure(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            if (snapshot.Skills.Count != 32)
            {
                errors.Add("Skill 에셋 수가 다릅니다. 예상 32, 실제 " + snapshot.Skills.Count);
                passed = false;
            }

            int missingScriptCount = 0;
            int missingSerializedReferenceCount = 0;
            int invalidTimingCount = 0;
            Dictionary<string, int> classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                if (skill.HasMissingScript)
                {
                    missingScriptCount++;
                }

                if (skill.HasMissingSerializedReference)
                {
                    missingSerializedReferenceCount++;
                }

                if (!IsFiniteNonNegative(skill.Duration) || !IsFiniteNonNegative(skill.Cooldown))
                {
                    invalidTimingCount++;
                }

                int count;
                classCounts.TryGetValue(skill.ConcreteTypeName, out count);
                classCounts[skill.ConcreteTypeName] = count + 1;
            }

            passed &= RequireZero("Skill Missing Script", missingScriptCount, errors);
            passed &= RequireZero(
                "Skill Missing Serialized Reference",
                missingSerializedReferenceCount,
                errors);
            passed &= RequireZero("Skill Duration/Cooldown 비정상", invalidTimingCount, errors);
            foreach (KeyValuePair<string, int> expected in ExpectedSkillClassCounts)
            {
                int actual;
                classCounts.TryGetValue(expected.Key, out actual);
                if (actual != expected.Value)
                {
                    errors.Add(
                        expected.Key + " 수가 다릅니다. 예상 " + expected.Value
                        + ", 실제 " + actual);
                    passed = false;
                }
            }

            int expectedNonZeroClassCount = 0;
            foreach (KeyValuePair<string, int> expected in ExpectedSkillClassCounts)
            {
                expectedNonZeroClassCount += expected.Value > 0 ? 1 : 0;
            }

            if (classCounts.Count != expectedNonZeroClassCount)
            {
                errors.Add("예상하지 않은 SkillBase 파생 클래스가 있습니다.");
                passed = false;
            }

            details.Add("- Skill 에셋: " + snapshot.Skills.Count + "개");
            details.Add("- 공통 필드 누락: 0 (Inventory 생성 성공 기준)");
            details.Add("- SpeedUp / Touch / AssignedCooking / FoodPayment / FoodPrice / NormalCustomer / GlobalCooking / GlobalRemaining / AllStaff: "
                        + GetCount(classCounts, "SpeedUpSkill") + " / "
                        + GetCount(classCounts, "TouchAddCustomerButtonSkill") + " / "
                        + GetCount(classCounts, "AssignedCookingSpeedUpSkill") + " / "
                        + GetCount(classCounts, "FoodPaymentTipUpSkill") + " / "
                        + GetCount(classCounts, "FoodPriceUpSkill") + " / "
                        + GetCount(classCounts, "NormalCustomerMoveSpeedUpSkill") + " / "
                        + GetCount(classCounts, "GlobalCookingSpeedUpSkill") + " / "
                        + GetCount(classCounts, "GlobalRemainingCookingTimeReductionSkill") + " / "
                        + GetCount(classCounts, "AllStaffMoveSpeedUpSkill"));
            details.Add("- Missing Script/Serialized Reference/비정상 시간: "
                        + missingScriptCount + "/" + missingSerializedReferenceCount
                        + "/" + invalidTimingCount);
            return passed;
        }

        private static bool ValidateSkill04Migration(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            int activePassed = 0;
            int descriptionPassed = 0;
            int effectPassed = 0;
            int legacyPassed = 0;
            int legacyActiveReferences = 0;
            HashSet<string> activeGuids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> legacyGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Skill04MigrationTargets.Length; index++)
            {
                legacyGuids.Add(Skill04MigrationTargets[index].LegacyGuid);
            }

            for (int index = 0; index < Skill04MigrationTargets.Length; index++)
            {
                Skill04MigrationTarget target = Skill04MigrationTargets[index];
                StaffDataAssetSnapshot staff;
                if (!snapshot.TryGetStaff(target.StaffId, out staff) || staff == null)
                {
                    errors.Add("Skill04 대상 StaffData가 없습니다: " + target.StaffId);
                    passed = false;
                    continue;
                }

                string activePath = StaffDataAssetInventoryReader.SkillFolder
                                    + "/" + target.FileName;
                string legacyPath = StaffDataAssetInventoryReader.LegacySkillFolder
                                    + "/" + target.FileName;
                StaffSkillAssetSnapshot active = null;
                bool activeFound = staff.SkillReference != null
                                   && staff.SkillReference.IsAssigned
                                   && !staff.SkillReference.IsMissing
                                   && staff.SkillReference.AssetPath == activePath
                                   && snapshot.TryGetSkill(staff.SkillReference.AssetGuid, out active)
                                   && active != null;
                bool activeValid = activeFound
                                   && active.ConcreteTypeName == "AssignedCookingSpeedUpSkill"
                                   && active.ScriptGuid == AssignedCookingScriptGuid
                                   && active.UnityObjectName == target.ObjectName
                                   && Approximately(active.Duration, target.OfficialDuration)
                                   && Approximately(active.Cooldown, target.OfficialCooldown)
                                   && active.ReferenceCount == 1
                                   && active.ReferencedStaffIds.Count == 1
                                   && active.ReferencedStaffIds[0] == target.StaffId
                                   && IsUnityGuid(active.AssetGuid)
                                   && !legacyGuids.Contains(active.AssetGuid)
                                   && activeGuids.Add(active.AssetGuid);
                List<string> activeReferences = FindAssetReferences(
                    active == null ? string.Empty : active.AssetGuid,
                    activePath);
                activeValid &= activeReferences.Count == 1
                               && activeReferences[0] == staff.AssetPath;
                bool descriptionValid = activeValid
                                        && active.Description
                                        == "맡은 주방 음식 제작 속도 (250%) 증가";
                AssignedCookingSpeedUpSkill assigned = activeValid
                    ? AssetDatabase.LoadAssetAtPath<AssignedCookingSpeedUpSkill>(activePath)
                    : null;
                SerializedProperty percent = assigned == null
                    ? null
                    : new SerializedObject(assigned).FindProperty(
                        "_assignedCookingSpeedUpPercent");
                bool effectValid = activeValid
                                   && percent != null
                                   && Approximately(percent.floatValue, 250f);
                activeValid &= descriptionValid && effectValid;
                descriptionPassed += descriptionValid ? 1 : 0;
                effectPassed += effectValid ? 1 : 0;
                if (activeValid)
                {
                    activePassed++;
                }
                else
                {
                    errors.Add("Skill04 Active Asset 검증 실패: " + target.StaffId);
                    passed = false;
                }

                string legacyGuid = AssetDatabase.AssetPathToGUID(legacyPath);
                SpeedUpSkill legacy = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(legacyPath);
                bool legacyValid = legacy != null
                                   && legacyGuid == target.LegacyGuid
                                   && legacy.name == target.ObjectName
                                   && Approximately(legacy.Duration, target.LegacyDuration)
                                   && Approximately(legacy.Cooldown, target.LegacyCooldown)
                                   && Approximately(legacy.FirstValue, 100f);
                List<string> legacyReferences = FindAssetReferences(legacyGuid, legacyPath);
                legacyActiveReferences += legacyReferences.Count;
                legacyValid &= legacyReferences.Count == 0;
                if (legacyValid)
                {
                    legacyPassed++;
                }
                else
                {
                    errors.Add("Skill04 Legacy Asset 보존 검증 실패: " + target.StaffId);
                    passed = false;
                }
            }

            Dictionary<string, int> classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int shared = 0;
            int orphan = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                int count;
                classCounts.TryGetValue(skill.ConcreteTypeName, out count);
                classCounts[skill.ConcreteTypeName] = count + 1;
                shared += skill.IsShared ? 1 : 0;
                orphan += skill.IsOrphan ? 1 : 0;
            }

            details.Add("- Active Skill Asset: " + snapshot.Skills.Count);
            details.Add("- SpeedUp / Touch / AssignedCooking: "
                        + GetCount(classCounts, "SpeedUpSkill") + " / "
                        + GetCount(classCounts, "TouchAddCustomerButtonSkill") + " / "
                        + GetCount(classCounts, "AssignedCookingSpeedUpSkill"));
            details.Add("- Skill04 기존 직원 전환: " + activePassed + "/4");
            details.Add("- Description 250: " + descriptionPassed + "/4");
            details.Add("- Effect 250: " + effectPassed + "/4");
            details.Add("- Legacy Skill 보존: " + legacyPassed + "/4");
            details.Add("- Active Shared / Orphan: " + shared + " / " + orphan);
            details.Add("- Legacy Active Reference: " + legacyActiveReferences);
            return passed;
        }

        private static bool ValidateSkill06Migration(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            Skill06MigrationTarget target = Skill06Target;
            StaffDataAssetSnapshot staff;
            if (!snapshot.TryGetStaff(target.StaffId, out staff) || staff == null)
            {
                errors.Add("Skill06 대상 StaffData가 없습니다: " + target.StaffId);
                return false;
            }

            string activePath = StaffDataAssetInventoryReader.SkillFolder + "/" + target.FileName;
            string legacyPath = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
            StaffSkillAssetSnapshot active = null;
            bool activeFound = staff.SkillReference != null
                               && staff.SkillReference.IsAssigned
                               && !staff.SkillReference.IsMissing
                               && staff.SkillReference.AssetPath == activePath
                               && snapshot.TryGetSkill(staff.SkillReference.AssetGuid, out active)
                               && active != null;
            bool activeValid = activeFound
                               && active.ConcreteTypeName == "FoodPaymentTipUpSkill"
                               && active.ScriptGuid == FoodPaymentTipScriptGuid
                               && active.ScriptAssetPath == FoodPaymentTipScriptPath
                               && active.UnityObjectName == target.ObjectName
                               && active.Description == "팁 (50%)증가"
                               && Approximately(active.Duration, target.OfficialDuration)
                               && Approximately(active.Cooldown, target.OfficialCooldown)
                               && active.ReferenceCount == 1
                               && active.ReferencedStaffIds.Count == 1
                               && active.ReferencedStaffIds[0] == target.StaffId
                               && IsUnityGuid(active.AssetGuid)
                               && active.AssetGuid != target.LegacyGuid
                               && !active.HasMissingScript
                               && !active.HasMissingSerializedReference;
            List<string> activeReferences = FindAssetReferences(
                active == null ? string.Empty : active.AssetGuid,
                activePath);
            activeValid &= activeReferences.Count == 1
                           && activeReferences[0] == staff.AssetPath;
            FoodPaymentTipUpSkill paymentTip = activeValid
                ? AssetDatabase.LoadAssetAtPath<FoodPaymentTipUpSkill>(activePath)
                : null;
            SerializedProperty percent = paymentTip == null
                ? null
                : new SerializedObject(paymentTip).FindProperty("_foodPaymentTipUpPercent");
            activeValid &= percent != null && Approximately(percent.floatValue, 50f);
            if (!activeValid)
            {
                errors.Add("Skill06 Active Asset 검증 실패: " + target.StaffId);
                passed = false;
            }

            string legacyGuid = AssetDatabase.AssetPathToGUID(legacyPath);
            SpeedUpSkill legacy = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(legacyPath);
            bool legacyValid = legacy != null
                               && legacyGuid == target.LegacyGuid
                               && legacy.name == target.ObjectName
                               && string.IsNullOrEmpty(legacy.Description)
                               && Approximately(legacy.Duration, target.LegacyDuration)
                               && Approximately(legacy.Cooldown, target.LegacyCooldown)
                               && Approximately(legacy.FirstValue, 100f);
            List<string> legacyReferences = FindAssetReferences(legacyGuid, legacyPath);
            legacyValid &= legacyReferences.Count == 0;
            if (!legacyValid)
            {
                errors.Add("Skill06 Legacy Asset 보존 검증 실패: " + target.StaffId);
                passed = false;
            }

            int preservedLegacy = legacyValid ? 1 : 0;
            int legacyActiveReferences = legacyReferences.Count;
            for (int index = 0; index < Skill04MigrationTargets.Length; index++)
            {
                Skill04MigrationTarget skill04 = Skill04MigrationTargets[index];
                string skill04Path = StaffDataAssetInventoryReader.LegacySkillFolder
                                     + "/" + skill04.FileName;
                string skill04Guid = AssetDatabase.AssetPathToGUID(skill04Path);
                SpeedUpSkill skill04Legacy = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(skill04Path);
                List<string> references = FindAssetReferences(skill04Guid, skill04Path);
                bool preserved = skill04Legacy != null
                                 && skill04Guid == skill04.LegacyGuid
                                 && references.Count == 0;
                preservedLegacy += preserved ? 1 : 0;
                legacyActiveReferences += references.Count;
                if (!preserved)
                {
                    errors.Add("Skill04 Legacy 회귀 검증 실패: " + skill04.StaffId);
                    passed = false;
                }
            }

            Dictionary<string, int> classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int shared = 0;
            int orphan = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                int count;
                classCounts.TryGetValue(skill.ConcreteTypeName, out count);
                classCounts[skill.ConcreteTypeName] = count + 1;
                shared += skill.IsShared ? 1 : 0;
                orphan += skill.IsOrphan ? 1 : 0;
            }

            bool totalsValid = snapshot.Skills.Count == 32
                               && GetCount(classCounts, "SpeedUpSkill") == 12
                               && GetCount(classCounts, "TouchAddCustomerButtonSkill") == 3
                               && GetCount(classCounts, "AssignedCookingSpeedUpSkill") == 4
                               && GetCount(classCounts, "FoodPaymentTipUpSkill") == 1
                               && GetCount(classCounts, "FoodPriceUpSkill") == 6
                               && GetCount(classCounts, "NormalCustomerMoveSpeedUpSkill") == 5
                               && GetCount(classCounts, "GlobalCookingSpeedUpSkill") == 0
                               && GetCount(classCounts, "GlobalRemainingCookingTimeReductionSkill") == 1
                               && GetCount(classCounts, "AllStaffMoveSpeedUpSkill") == 0
                               && classCounts.Count == 7
                               && shared == 0
                               && orphan == 0
                               && preservedLegacy == 5
                               && legacyActiveReferences == 0;
            if (!totalsValid)
            {
                errors.Add("Skill06 Migration 이후 Active 또는 Legacy 전체 기준이 다릅니다.");
                passed = false;
            }

            details.Add("- Active Skill Asset: " + snapshot.Skills.Count);
            details.Add("- SpeedUp / Touch / AssignedCooking / FoodPayment / FoodPrice: "
                        + GetCount(classCounts, "SpeedUpSkill") + " / "
                        + GetCount(classCounts, "TouchAddCustomerButtonSkill") + " / "
                        + GetCount(classCounts, "AssignedCookingSpeedUpSkill") + " / "
                        + GetCount(classCounts, "FoodPaymentTipUpSkill") + " / "
                        + GetCount(classCounts, "FoodPriceUpSkill"));
            details.Add("- Skill04 기존 직원 전환: 4/4");
            details.Add("- Skill06 기존 직원 전환: " + (activeValid ? "1/1" : "0/1"));
            details.Add("- Legacy Skill 보존: " + preservedLegacy + "/5");
            details.Add("- Active Shared / Orphan: " + shared + " / " + orphan);
            details.Add("- Legacy Active Reference: " + legacyActiveReferences);
            return passed;
        }

        private static bool ValidateSkill05Migration(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = true;
            int activePassed = 0;
            int skill05LegacyPassed = 0;
            int previousLegacyPassed = 0;
            int legacyActiveReferences = 0;
            HashSet<string> activeGuids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> allLegacyGuids = new HashSet<string>(StringComparer.Ordinal)
            {
                Skill06Target.LegacyGuid
            };
            for (int index = 0; index < Skill04MigrationTargets.Length; index++)
            {
                allLegacyGuids.Add(Skill04MigrationTargets[index].LegacyGuid);
            }

            for (int index = 0; index < Skill05MigrationTargets.Length; index++)
            {
                allLegacyGuids.Add(Skill05MigrationTargets[index].LegacyGuid);
            }

            for (int index = 0; index < Skill05MigrationTargets.Length; index++)
            {
                Skill05MigrationTarget target = Skill05MigrationTargets[index];
                StaffDataAssetSnapshot staff;
                string activePath = StaffDataAssetInventoryReader.SkillFolder + "/" + target.FileName;
                string legacyPath = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                StaffSkillAssetSnapshot active = null;
                bool activeValid = snapshot.TryGetStaff(target.StaffId, out staff)
                                   && staff != null
                                   && staff.SkillReference != null
                                   && staff.SkillReference.IsAssigned
                                   && !staff.SkillReference.IsMissing
                                   && staff.SkillReference.AssetPath == activePath
                                   && snapshot.TryGetSkill(staff.SkillReference.AssetGuid, out active)
                                   && active != null
                                   && active.ConcreteTypeName == "FoodPriceUpSkill"
                                   && active.ScriptGuid == FoodPriceScriptGuid
                                   && active.ScriptAssetPath == FoodPriceScriptPath
                                   && active.UnityObjectName == target.ObjectName
                                   && active.Description == "음식 가격 (50%)증가"
                                   && Approximately(active.Duration, target.OfficialDuration)
                                   && Approximately(active.Cooldown, target.OfficialCooldown)
                                   && active.ReferenceCount == 1
                                   && active.ReferencedStaffIds.Count == 1
                                   && active.ReferencedStaffIds[0] == target.StaffId
                                   && IsUnityGuid(active.AssetGuid)
                                   && !allLegacyGuids.Contains(active.AssetGuid)
                                   && activeGuids.Add(active.AssetGuid)
                                   && !active.HasMissingScript
                                   && !active.HasMissingSerializedReference;
                List<string> activeReferences = FindAssetReferences(
                    active == null ? string.Empty : active.AssetGuid,
                    activePath);
                activeValid &= activeReferences.Count == 1
                               && staff != null
                               && activeReferences[0] == staff.AssetPath;
                FoodPriceUpSkill foodPrice = activeValid
                    ? AssetDatabase.LoadAssetAtPath<FoodPriceUpSkill>(activePath)
                    : null;
                SerializedProperty percent = foodPrice == null
                    ? null
                    : new SerializedObject(foodPrice).FindProperty("_foodPriceUpPercent");
                activeValid &= percent != null && Approximately(percent.floatValue, 50f);
                if (activeValid)
                {
                    activePassed++;
                }
                else
                {
                    errors.Add("Skill05 Active Asset 검증 실패: " + target.StaffId);
                    passed = false;
                }

                string legacyGuid = AssetDatabase.AssetPathToGUID(legacyPath);
                SkillBase legacy = AssetDatabase.LoadAssetAtPath<SkillBase>(legacyPath);
                List<string> legacyReferences = FindAssetReferences(legacyGuid, legacyPath);
                bool legacyValid = legacy != null
                                   && legacy.GetType().Name == target.LegacyClassName
                                   && legacyGuid == target.LegacyGuid
                                   && legacy.name == target.ObjectName
                                   && string.IsNullOrEmpty(legacy.Description)
                                   && Approximately(legacy.Duration, target.LegacyDuration)
                                   && Approximately(legacy.Cooldown, target.LegacyCooldown)
                                   && Approximately(legacy.FirstValue, target.LegacyEffectValue)
                                   && legacyReferences.Count == 0;
                legacyActiveReferences += legacyReferences.Count;
                if (legacyValid)
                {
                    skill05LegacyPassed++;
                }
                else
                {
                    errors.Add("Skill05 Legacy Asset 보존 검증 실패: " + target.StaffId);
                    passed = false;
                }
            }

            for (int index = 0; index < Skill04MigrationTargets.Length; index++)
            {
                Skill04MigrationTarget target = Skill04MigrationTargets[index];
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                string guid = AssetDatabase.AssetPathToGUID(path);
                SpeedUpSkill legacy = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(path);
                List<string> references = FindAssetReferences(guid, path);
                bool valid = legacy != null
                             && guid == target.LegacyGuid
                             && legacy.name == target.ObjectName
                             && string.IsNullOrEmpty(legacy.Description)
                             && Approximately(legacy.Duration, target.LegacyDuration)
                             && Approximately(legacy.Cooldown, target.LegacyCooldown)
                             && Approximately(legacy.FirstValue, 100f)
                             && references.Count == 0;
                previousLegacyPassed += valid ? 1 : 0;
                legacyActiveReferences += references.Count;
                if (!valid)
                {
                    errors.Add("Skill04 Legacy 회귀 검증 실패: " + target.StaffId);
                    passed = false;
                }
            }

            string skill06Path = StaffDataAssetInventoryReader.LegacySkillFolder
                                 + "/" + Skill06Target.FileName;
            string skill06Guid = AssetDatabase.AssetPathToGUID(skill06Path);
            SpeedUpSkill skill06Legacy = AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(skill06Path);
            List<string> skill06References = FindAssetReferences(skill06Guid, skill06Path);
            bool skill06Valid = skill06Legacy != null
                                && skill06Guid == Skill06Target.LegacyGuid
                                && skill06Legacy.name == Skill06Target.ObjectName
                                && string.IsNullOrEmpty(skill06Legacy.Description)
                                && Approximately(skill06Legacy.Duration, Skill06Target.LegacyDuration)
                                && Approximately(skill06Legacy.Cooldown, Skill06Target.LegacyCooldown)
                                && Approximately(skill06Legacy.FirstValue, 100f)
                                && skill06References.Count == 0;
            previousLegacyPassed += skill06Valid ? 1 : 0;
            legacyActiveReferences += skill06References.Count;
            if (!skill06Valid)
            {
                errors.Add("Skill06 Legacy 회귀 검증 실패: " + Skill06Target.StaffId);
                passed = false;
            }

            Dictionary<string, int> classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int shared = 0;
            int orphan = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                int count;
                classCounts.TryGetValue(skill.ConcreteTypeName, out count);
                classCounts[skill.ConcreteTypeName] = count + 1;
                shared += skill.IsShared ? 1 : 0;
                orphan += skill.IsOrphan ? 1 : 0;
            }

            int legacyAssetCount = 0;
            string[] legacyAssetGuids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { StaffDataAssetInventoryReader.LegacySkillFolder });
            for (int index = 0; index < legacyAssetGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(legacyAssetGuids[index]);
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.LoadAssetAtPath<SkillBase>(path) != null)
                {
                    legacyAssetCount++;
                }
            }

            bool totalsValid = snapshot.Skills.Count == 32
                               && GetCount(classCounts, "SpeedUpSkill") == 12
                               && GetCount(classCounts, "TouchAddCustomerButtonSkill") == 3
                               && GetCount(classCounts, "AssignedCookingSpeedUpSkill") == 4
                               && GetCount(classCounts, "FoodPaymentTipUpSkill") == 1
                               && GetCount(classCounts, "FoodPriceUpSkill") == 6
                               && GetCount(classCounts, "NormalCustomerMoveSpeedUpSkill") == 5
                               && GetCount(classCounts, "GlobalCookingSpeedUpSkill") == 0
                               && GetCount(classCounts, "GlobalRemainingCookingTimeReductionSkill") == 1
                               && GetCount(classCounts, "AllStaffMoveSpeedUpSkill") == 0
                               && classCounts.Count == 7
                               && activePassed == 6
                               && skill05LegacyPassed == 6
                               && previousLegacyPassed == 5
                               && legacyAssetCount == 18
                               && legacyActiveReferences == 0
                               && shared == 0
                               && orphan == 0;
            if (!totalsValid)
            {
                errors.Add("Skill05 Migration 이후 Active 또는 Legacy 전체 기준이 다릅니다.");
                passed = false;
            }

            details.Add("- Active Skill Asset: " + snapshot.Skills.Count);
            details.Add("- SpeedUp / Touch / AssignedCooking / FoodPayment / FoodPrice: "
                        + GetCount(classCounts, "SpeedUpSkill") + " / "
                        + GetCount(classCounts, "TouchAddCustomerButtonSkill") + " / "
                        + GetCount(classCounts, "AssignedCookingSpeedUpSkill") + " / "
                        + GetCount(classCounts, "FoodPaymentTipUpSkill") + " / "
                        + GetCount(classCounts, "FoodPriceUpSkill"));
            details.Add("- Skill04 기존 직원 전환: 4/4");
            details.Add("- Skill06 기존 직원 전환: 1/1");
            details.Add("- Skill05 기존 직원 전환: " + activePassed + "/6");
            details.Add("- Legacy Skill 보존: "
                        + (previousLegacyPassed + skill05LegacyPassed) + "/11");
            details.Add("- Active Shared / Orphan: " + shared + " / " + orphan);
            details.Add("- Legacy Active Reference: " + legacyActiveReferences);
            return passed;
        }

        private static bool ValidateRemainingSkillMigration(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            bool passed = ValidateRemainingSkillScripts(errors);
            int skill08Passed = 0;
            int skill09Passed = 0;
            int legacyPassed = 0;
            int legacyActiveReferences = 0;
            HashSet<string> activeGuids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> legacyGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < RemainingSkillMigrationTargets.Length; index++)
            {
                legacyGuids.Add(RemainingSkillMigrationTargets[index].LegacyGuid);
            }
            legacyGuids.Add(Skill09LegacyGlobalGuid);
            legacyGuids.Add(Skill09LegacySpeedGuid);

            for (int index = 0; index < RemainingSkillMigrationTargets.Length; index++)
            {
                RemainingSkillMigrationTarget target = RemainingSkillMigrationTargets[index];
                StaffDataAssetSnapshot staff;
                if (!snapshot.TryGetStaff(target.StaffId, out staff) || staff == null)
                {
                    errors.Add("Skill08·09 대상 StaffData가 없습니다: " + target.StaffId);
                    passed = false;
                    continue;
                }

                string activePath = StaffDataAssetInventoryReader.SkillFolder + "/" + target.FileName;
                string legacyPath = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                StaffSkillAssetSnapshot active = null;
                bool activeFound = staff.SkillReference != null
                                   && staff.SkillReference.IsAssigned
                                   && !staff.SkillReference.IsMissing
                                   && staff.SkillReference.AssetPath == activePath
                                   && snapshot.TryGetSkill(staff.SkillReference.AssetGuid, out active)
                                   && active != null;
                bool activeValid = activeFound
                                   && active.ConcreteTypeName == target.OfficialClassName
                                   && active.ScriptGuid == target.OfficialScriptGuid
                                   && active.UnityObjectName == target.ObjectName
                                   && active.Description == target.OfficialDescription
                                   && Approximately(active.Duration, target.OfficialDuration)
                                   && Approximately(active.Cooldown, target.OfficialCooldown)
                                   && active.ReferenceCount == 1
                                   && active.ReferencedStaffIds.Count == 1
                                   && active.ReferencedStaffIds[0] == target.StaffId
                                   && IsUnityGuid(active.AssetGuid)
                                   && !legacyGuids.Contains(active.AssetGuid)
                                   && activeGuids.Add(active.AssetGuid);
                List<string> activeReferences = FindAssetReferences(
                    active == null ? string.Empty : active.AssetGuid,
                    activePath);
                activeValid &= activeReferences.Count == 1
                               && activeReferences[0] == staff.AssetPath;
                SkillBase activeSkill = activeValid
                    ? AssetDatabase.LoadAssetAtPath<SkillBase>(activePath)
                    : null;
                activeValid &= activeSkill != null
                               && Approximately(activeSkill.FirstValue, target.OfficialEffectValue);
                if (activeValid)
                {
                    if (target.OfficialClassName == "NormalCustomerMoveSpeedUpSkill")
                    {
                        skill08Passed++;
                    }
                    else
                    {
                        skill09Passed++;
                    }
                }
                else
                {
                    errors.Add("Skill08·09 Active Asset 검증 실패: " + target.StaffId);
                    passed = false;
                }

                string legacyGuid = AssetDatabase.AssetPathToGUID(legacyPath);
                SkillBase legacy = AssetDatabase.LoadAssetAtPath<SkillBase>(legacyPath);
                bool legacyValid = legacy != null
                                   && legacyGuid == target.LegacyGuid
                                   && legacy.GetType().Name == target.LegacyClassName
                                   && legacy.name == target.ObjectName
                                   && string.IsNullOrEmpty(legacy.Description)
                                   && Approximately(legacy.Duration, target.LegacyDuration)
                                   && Approximately(legacy.Cooldown, target.LegacyCooldown)
                                   && Approximately(legacy.FirstValue, target.LegacyEffectValue);
                List<string> legacyReferences = FindAssetReferences(legacyGuid, legacyPath);
                legacyActiveReferences += legacyReferences.Count;
                legacyValid &= legacyReferences.Count == 0;
                if (legacyValid)
                {
                    legacyPassed++;
                }
                else
                {
                    errors.Add("Skill08·09 Legacy Asset 보존 검증 실패: " + target.StaffId);
                    passed = false;
                }
            }

            StaffDataAssetSnapshot skill09Staff;
            StaffSkillAssetSnapshot skill09Active = null;
            string globalRemainingScriptGuid =
                AssetDatabase.AssetPathToGUID(GlobalRemainingCookingScriptPath);
            bool skill09ActiveValid = snapshot.TryGetStaff("STAFF27", out skill09Staff)
                                      && skill09Staff != null
                                      && skill09Staff.SkillReference != null
                                      && skill09Staff.SkillReference.IsAssigned
                                      && !skill09Staff.SkillReference.IsMissing
                                      && skill09Staff.SkillReference.AssetPath == Skill09ActivePath
                                      && snapshot.TryGetSkill(
                                          skill09Staff.SkillReference.AssetGuid,
                                          out skill09Active)
                                      && skill09Active != null
                                      && skill09Active.ConcreteTypeName
                                      == "GlobalRemainingCookingTimeReductionSkill"
                                      && skill09Active.ScriptGuid == globalRemainingScriptGuid
                                      && skill09Active.UnityObjectName == "STAFF27SKILL"
                                      && skill09Active.Description
                                      == "전체 주방 남은 음식 조리시간 50% 감소!"
                                      && Approximately(skill09Active.Duration, 1)
                                      && Approximately(skill09Active.Cooldown, 240)
                                      && skill09Active.ReferenceCount == 1
                                      && skill09Active.ReferencedStaffIds.Count == 1
                                      && skill09Active.ReferencedStaffIds[0] == "STAFF27"
                                      && IsUnityGuid(skill09Active.AssetGuid)
                                      && !legacyGuids.Contains(skill09Active.AssetGuid)
                                      && activeGuids.Add(skill09Active.AssetGuid);
            List<string> skill09ActiveReferences = FindAssetReferences(
                skill09Active == null ? string.Empty : skill09Active.AssetGuid,
                Skill09ActivePath);
            GlobalRemainingCookingTimeReductionSkill skill09ActiveAsset =
                skill09ActiveValid
                    ? AssetDatabase.LoadAssetAtPath<GlobalRemainingCookingTimeReductionSkill>(
                        Skill09ActivePath)
                    : null;
            SerializedObject skill09ActiveSerialized = skill09ActiveAsset == null
                ? null
                : new SerializedObject(skill09ActiveAsset);
            SerializedProperty skill09ActivePercent = skill09ActiveSerialized == null
                ? null
                : skill09ActiveSerialized.FindProperty(
                    "_remainingCookingTimeReductionPercent");
            skill09ActiveValid &= skill09ActiveReferences.Count == 1
                                  && skill09ActiveReferences[0]
                                  == "Assets/Resources/StaffData/STAFF27.asset"
                                  && skill09ActiveAsset != null
                                  && skill09ActivePercent != null
                                  && Approximately(skill09ActivePercent.floatValue, 50)
                                  && Approximately(skill09ActiveAsset.FirstValue, 50)
                                  && !skill09Active.HasMissingScript
                                  && !skill09Active.HasMissingSerializedReference;
            if (skill09ActiveValid)
            {
                skill09Passed++;
            }
            else
            {
                errors.Add("Skill09 remaining-time Active Asset validation failed: STAFF27");
                passed = false;
            }

            string skill09LegacyGlobalGuid =
                AssetDatabase.AssetPathToGUID(Skill09LegacyGlobalPath);
            GlobalCookingSpeedUpSkill skill09LegacyGlobal =
                AssetDatabase.LoadAssetAtPath<GlobalCookingSpeedUpSkill>(
                    Skill09LegacyGlobalPath);
            SerializedObject skill09LegacyGlobalSerialized = skill09LegacyGlobal == null
                ? null
                : new SerializedObject(skill09LegacyGlobal);
            SerializedProperty skill09LegacyGlobalPercent =
                skill09LegacyGlobalSerialized == null
                    ? null
                    : skill09LegacyGlobalSerialized.FindProperty(
                        "_globalCookingSpeedUpPercent");
            string skill09LegacyGlobalAssetSha = string.Empty;
            string skill09LegacyGlobalMetaSha = string.Empty;
            bool skill09LegacyGlobalFilesReadable = TryGetProjectFileSha256(
                                                        Skill09LegacyGlobalPath,
                                                        out skill09LegacyGlobalAssetSha)
                                                    && TryGetProjectFileSha256(
                                                        Skill09LegacyGlobalPath + ".meta",
                                                        out skill09LegacyGlobalMetaSha);
            List<string> skill09LegacyGlobalReferences = FindAssetReferences(
                skill09LegacyGlobalGuid,
                Skill09LegacyGlobalPath);
            bool skill09LegacyGlobalValid = skill09LegacyGlobal != null
                                            && skill09LegacyGlobalFilesReadable
                                            && skill09LegacyGlobalGuid
                                            == Skill09LegacyGlobalGuid
                                            && GetScriptGuid(skill09LegacyGlobal)
                                            == GlobalCookingScriptGuid
                                            && skill09LegacyGlobal.name
                                            == "STAFF27GlobalCookingSpeedUpSkill"
                                            && skill09LegacyGlobal.Description
                                            == "전체 주방 음식 제작 속도 (50%) 증가"
                                            && skill09LegacyGlobalPercent != null
                                            && Approximately(
                                                skill09LegacyGlobalPercent.floatValue,
                                                50)
                                            && Approximately(skill09LegacyGlobal.FirstValue, 50)
                                            && Approximately(skill09LegacyGlobal.Duration, 30)
                                            && Approximately(skill09LegacyGlobal.Cooldown, 200)
                                            && !HasMissingSerializedReference(
                                                skill09LegacyGlobalSerialized)
                                            && skill09LegacyGlobalReferences.Count == 0;
            legacyActiveReferences += skill09LegacyGlobalReferences.Count;
            legacyPassed += skill09LegacyGlobalValid ? 1 : 0;
            if (!skill09LegacyGlobalValid)
            {
                errors.Add("Skill09 GlobalCooking Legacy Asset preservation failed: STAFF27");
                passed = false;
            }

            string skill09LegacySpeedGuid =
                AssetDatabase.AssetPathToGUID(Skill09LegacySpeedPath);
            SpeedUpSkill skill09LegacySpeed =
                AssetDatabase.LoadAssetAtPath<SpeedUpSkill>(Skill09LegacySpeedPath);
            string skill09LegacySpeedAssetSha;
            string skill09LegacySpeedMetaSha;
            bool skill09LegacySpeedShaValid = TryGetProjectFileSha256(
                                                  Skill09LegacySpeedPath,
                                                  out skill09LegacySpeedAssetSha)
                                              && TryGetProjectFileSha256(
                                                  Skill09LegacySpeedPath + ".meta",
                                                  out skill09LegacySpeedMetaSha)
                                              && skill09LegacySpeedAssetSha
                                              == Skill09LegacySpeedAssetSha256
                                              && skill09LegacySpeedMetaSha
                                              == Skill09LegacySpeedMetaSha256;
            List<string> skill09LegacySpeedReferences = FindAssetReferences(
                skill09LegacySpeedGuid,
                Skill09LegacySpeedPath);
            bool skill09LegacySpeedValid = skill09LegacySpeed != null
                                           && skill09LegacySpeedShaValid
                                           && skill09LegacySpeedGuid
                                           == Skill09LegacySpeedGuid
                                           && skill09LegacySpeed.name == "STAFF27SKILL"
                                           && string.IsNullOrEmpty(skill09LegacySpeed.Description)
                                           && Approximately(skill09LegacySpeed.FirstValue, 100)
                                           && Approximately(skill09LegacySpeed.Duration, 60)
                                           && Approximately(skill09LegacySpeed.Cooldown, 250)
                                           && skill09LegacySpeedReferences.Count == 0;
            legacyActiveReferences += skill09LegacySpeedReferences.Count;
            legacyPassed += skill09LegacySpeedValid ? 1 : 0;
            if (!skill09LegacySpeedValid)
            {
                errors.Add("Skill09 SpeedUp Legacy Asset preservation failed: STAFF27");
                passed = false;
            }

            Dictionary<string, int> classCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int shared = 0;
            int orphan = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                int count;
                classCounts.TryGetValue(skill.ConcreteTypeName, out count);
                classCounts[skill.ConcreteTypeName] = count + 1;
                shared += skill.IsShared ? 1 : 0;
                orphan += skill.IsOrphan ? 1 : 0;
            }

            int legacySkillCount = 0;
            int allLegacyReferences = 0;
            string[] allLegacyGuids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { StaffDataAssetInventoryReader.LegacySkillFolder });
            for (int index = 0; index < allLegacyGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(allLegacyGuids[index]);
                if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                    || AssetDatabase.LoadAssetAtPath<SkillBase>(path) == null)
                {
                    continue;
                }

                legacySkillCount++;
                allLegacyReferences += FindAssetReferences(allLegacyGuids[index], path).Count;
            }

            bool distributionValid = snapshot.Skills.Count == 32
                                     && GetCount(classCounts, "SpeedUpSkill") == 12
                                     && GetCount(classCounts, "TouchAddCustomerButtonSkill") == 3
                                     && GetCount(classCounts, "AssignedCookingSpeedUpSkill") == 4
                                     && GetCount(classCounts, "FoodPaymentTipUpSkill") == 1
                                     && GetCount(classCounts, "FoodPriceUpSkill") == 6
                                     && GetCount(classCounts, "NormalCustomerMoveSpeedUpSkill") == 5
                                     && GetCount(classCounts, "GlobalCookingSpeedUpSkill") == 0
                                     && GetCount(classCounts, "GlobalRemainingCookingTimeReductionSkill") == 1
                                     && GetCount(classCounts, "AllStaffMoveSpeedUpSkill") == 0
                                     && classCounts.Count == 7
                                     && activeGuids.Count == 6
                                     && skill08Passed == 5
                                     && skill09Passed == 1
                                     && legacyPassed == 7
                                     && legacySkillCount == 18
                                     && shared == 0
                                     && orphan == 0
                                     && legacyActiveReferences == 0
                                     && allLegacyReferences == 0;
            if (!distributionValid)
            {
                errors.Add(
                    "Skill08·09·10 기존 직원·Legacy 보존 분포 불일치: active "
                    + snapshot.Skills.Count
                    + ", class " + GetCount(classCounts, "SpeedUpSkill") + "/"
                    + GetCount(classCounts, "TouchAddCustomerButtonSkill") + "/"
                    + GetCount(classCounts, "AssignedCookingSpeedUpSkill") + "/"
                    + GetCount(classCounts, "FoodPaymentTipUpSkill") + "/"
                    + GetCount(classCounts, "FoodPriceUpSkill") + "/"
                    + GetCount(classCounts, "NormalCustomerMoveSpeedUpSkill") + "/"
                    + GetCount(classCounts, "GlobalCookingSpeedUpSkill") + "/"
                    + GetCount(classCounts, "GlobalRemainingCookingTimeReductionSkill") + "/"
                    + GetCount(classCounts, "AllStaffMoveSpeedUpSkill")
                    + ", legacy " + legacySkillCount
                    + ", shared/orphan " + shared + "/" + orphan
                    + ", legacy refs " + allLegacyReferences + ".");
                passed = false;
            }

            details.Add("- Active Skill Asset: " + snapshot.Skills.Count);
            details.Add("- Active Class: "
                        + GetCount(classCounts, "SpeedUpSkill") + " / "
                        + GetCount(classCounts, "TouchAddCustomerButtonSkill") + " / "
                        + GetCount(classCounts, "AssignedCookingSpeedUpSkill") + " / "
                        + GetCount(classCounts, "FoodPaymentTipUpSkill") + " / "
                        + GetCount(classCounts, "FoodPriceUpSkill") + " / "
                        + GetCount(classCounts, "NormalCustomerMoveSpeedUpSkill") + " / "
                        + GetCount(classCounts, "GlobalCookingSpeedUpSkill") + " / "
                        + GetCount(classCounts, "GlobalRemainingCookingTimeReductionSkill") + " / "
                        + GetCount(classCounts, "AllStaffMoveSpeedUpSkill"));
            details.Add("- Skill08 기존 직원 전환: " + skill08Passed + "/5");
            details.Add("- Skill09 기존 직원 전환: " + skill09Passed + "/1");
            details.Add("- Skill10 기존 직원 대상: 0/0");
            details.Add("- Legacy Skill 보존: " + legacySkillCount + "/18");
            details.Add("- Active Shared / Orphan: " + shared + " / " + orphan);
            details.Add("- Legacy Active Reference: " + allLegacyReferences);
            details.Add("- Skill09 Migration Legacy semantic: "
                        + (skill09LegacyGlobalValid ? "PASS" : "FAIL"));
            details.Add("- Skill09 Migration Legacy Asset/meta SHA-256 (정보): "
                        + skill09LegacyGlobalAssetSha + " / " + skill09LegacyGlobalMetaSha);
            details.Add("- Skill09 기존 SpeedUp Legacy Asset/meta SHA-256: "
                        + (skill09LegacySpeedShaValid ? "PASS" : "FAIL"));
            return passed;
        }

        private static bool ValidateRemainingSkillScripts(List<string> errors)
        {
            bool normal = AssetDatabase.AssetPathToGUID(NormalCustomerMoveScriptPath)
                          == NormalCustomerMoveScriptGuid;
            MonoScript normalScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                NormalCustomerMoveScriptPath);
            normal &= normalScript != null
                      && normalScript.GetClass() == typeof(NormalCustomerMoveSpeedUpSkill);

            bool cooking = AssetDatabase.AssetPathToGUID(GlobalCookingScriptPath)
                           == GlobalCookingScriptGuid;
            MonoScript cookingScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                GlobalCookingScriptPath);
            cooking &= cookingScript != null
                       && cookingScript.GetClass() == typeof(GlobalCookingSpeedUpSkill);

            string globalRemainingGuid =
                AssetDatabase.AssetPathToGUID(GlobalRemainingCookingScriptPath);
            MonoScript globalRemainingScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                GlobalRemainingCookingScriptPath);
            bool globalRemaining = IsUnityGuid(globalRemainingGuid)
                                   && globalRemainingScript != null
                                   && globalRemainingScript.GetClass()
                                   == typeof(GlobalRemainingCookingTimeReductionSkill);

            bool allStaff = AssetDatabase.AssetPathToGUID(AllStaffMoveScriptPath)
                            == AllStaffMoveScriptGuid;
            MonoScript allStaffScript = AssetDatabase.LoadAssetAtPath<MonoScript>(
                AllStaffMoveScriptPath);
            allStaff &= allStaffScript != null
                        && allStaffScript.GetClass() == typeof(AllStaffMoveSpeedUpSkill);
            if (!normal || !cooking || !globalRemaining || !allStaff)
            {
                errors.Add("Skill08·09·10 Runtime Script 또는 GUID가 기준과 다릅니다.");
            }

            return normal && cooking && globalRemaining && allStaff;
        }

        private static bool ValidateChefOfficialPassive(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int staffCount = 0;
            int valueCount = 0;
            int normalCount = 0;
            int uniqueCount = 0;
            int specialCount = 0;
            bool passed = true;
            for (int targetIndex = 0; targetIndex < ChefPassiveTargets.Length; targetIndex++)
            {
                ChefPassiveTarget target = ChefPassiveTargets[targetIndex];
                StaffDataAssetSnapshot staff;
                if (!snapshot.TryGetStaff(target.StaffId, out staff)
                    || staff == null
                    || staff.ConcreteTypeName != "ChefData"
                    || staff.LevelCount != 5)
                {
                    errors.Add("Chef 공식 패시브 대상 구조가 올바르지 않습니다: " + target.StaffId);
                    passed = false;
                    continue;
                }

                staffCount++;
                int staffValueCount = 0;
                for (int levelIndex = 0; levelIndex < staff.Levels.Count; levelIndex++)
                {
                    float? value = staff.Levels[levelIndex].FoodSpeedAddPercent;
                    if (value.HasValue
                        && IsFiniteNonNegative(value.Value)
                        && Approximately(value.Value, target.ExpectedValue))
                    {
                        staffValueCount++;
                        valueCount++;
                    }
                }

                if (staffValueCount != 5)
                {
                    errors.Add(
                        "Chef 공식 패시브 값이 일치하지 않습니다: " + target.StaffId
                        + " expected "
                        + target.ExpectedValue.ToString("0.####", CultureInfo.InvariantCulture)
                        + ", matched " + staffValueCount + "/5");
                    passed = false;
                }

                normalCount += target.GradeKey == "NORMAL" ? staffValueCount : 0;
                uniqueCount += target.GradeKey == "UNIQUE" ? staffValueCount : 0;
                specialCount += target.GradeKey == "SPECIAL" ? staffValueCount : 0;
                details.Add(
                    "- " + target.StaffId + " / " + target.GradeKey + " / "
                    + target.ExpectedValue.ToString("0.####", CultureInfo.InvariantCulture)
                    + ": " + staffValueCount + "/5");
            }

            if (staffCount != 7
                || valueCount != 35
                || normalCount != 25
                || uniqueCount != 5
                || specialCount != 5)
            {
                errors.Add(
                    "Chef 공식 패시브 V8 합계가 일치하지 않습니다. staff/value/normal/unique/special: "
                    + staffCount + "/" + valueCount + "/" + normalCount + "/"
                    + uniqueCount + "/" + specialCount);
                passed = false;
            }

            details.Add("- Existing Chef: " + staffCount + "/7");
            details.Add("- 공식 패시브 값: " + valueCount + "/35");
            details.Add("- NORMAL / UNIQUE / SPECIAL: "
                        + normalCount + "/25, " + uniqueCount + "/5, " + specialCount + "/5");
            return passed;
        }

        private static List<string> FindAssetReferences(string guid, string targetPath)
        {
            List<string> references = new List<string>();
            if (string.IsNullOrEmpty(guid))
            {
                return references;
            }

            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            for (int index = 0; index < assetPaths.Length; index++)
            {
                string path = assetPaths[index];
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                    || path == targetPath
                    || AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(path, false);
                for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
                {
                    if (dependencies[dependencyIndex] == targetPath)
                    {
                        references.Add(path);
                        break;
                    }
                }
            }

            references.Sort(StringComparer.Ordinal);
            return references;
        }

        private static bool ValidateStaffSkillGraph(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            int nullSkillCount = 0;
            int missingSkillCount = 0;
            int outsideFolderCount = 0;
            for (int index = 0; index < snapshot.Staff.Count; index++)
            {
                StaffAssetReferenceSnapshot reference = snapshot.Staff[index].SkillReference;
                if (reference == null || !reference.IsAssigned)
                {
                    nullSkillCount++;
                    continue;
                }

                StaffSkillAssetSnapshot skill;
                if (reference.IsMissing
                    || !snapshot.TryGetSkill(reference.AssetGuid, out skill)
                    || skill == null)
                {
                    missingSkillCount++;
                }

                if (!IsPathInSkillFolder(reference.AssetPath))
                {
                    outsideFolderCount++;
                }
            }

            int referenceCountOne = 0;
            int sharedCount = 0;
            int orphanCount = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                if (skill.ReferenceCount == 1)
                {
                    referenceCountOne++;
                }

                if (skill.IsShared)
                {
                    sharedCount++;
                }

                if (skill.IsOrphan)
                {
                    orphanCount++;
                }
            }

            bool passed = true;
            passed &= RequireZero("Staff Skill null", nullSkillCount, errors);
            passed &= RequireZero("Missing Skill 참조", missingSkillCount, errors);
            passed &= RequireZero("Skill 폴더 밖 참조", outsideFolderCount, errors);
            passed &= RequireZero("공유 Skill", sharedCount, errors);
            passed &= RequireZero("고아 Skill", orphanCount, errors);
            if (referenceCountOne != 32)
            {
                errors.Add("ReferenceCount 1인 Skill 수가 다릅니다. 예상 32, 실제 " + referenceCountOne);
                passed = false;
            }

            details.Add("- ReferenceCount 1인 Skill: " + referenceCountOne + "/32");
            details.Add("- 공유·고아 Skill: " + sharedCount + "/" + orphanCount);
            details.Add("- Null/Missing/폴더 밖 참조: "
                        + nullSkillCount + "/" + missingSkillCount + "/" + outsideFolderCount);
            return passed;
        }

        private static bool ValidateFingerprint(
            StaffDataAssetInventorySnapshot snapshot,
            List<string> details,
            List<string> errors)
        {
            details.Add("- InventoryFingerprint: " + snapshot.InventoryFingerprint);
            if (IsLowercaseSha256(snapshot.InventoryFingerprint))
            {
                return true;
            }

            errors.Add("InventoryFingerprint가 소문자 SHA-256 64자리 형식이 아닙니다.");
            return false;
        }

        private static bool ValidateDeterministicRebuild(
            StaffDataAssetInventorySnapshot first,
            StaffDataAssetInventorySnapshot second,
            List<string> errors)
        {
            if (!string.Equals(
                    first.InventoryFingerprint,
                    second.InventoryFingerprint,
                    StringComparison.Ordinal)
                || first.Staff.Count != second.Staff.Count
                || first.Skills.Count != second.Skills.Count)
            {
                errors.Add("동일 자산에서 두 번 생성한 Inventory 결과가 다릅니다.");
                return false;
            }

            return true;
        }

        private static bool ValidateDeepImmutability(
            StaffDataAssetInventorySnapshot source,
            List<string> errors)
        {
            if (source.Staff.Count == 0 || source.Skills.Count == 0)
            {
                errors.Add("불변성 검사에 사용할 Staff 또는 Skill이 없습니다.");
                return false;
            }

            StaffDataAssetSnapshot testStaff = CloneStaff(source.Staff[0]);
            StaffSkillAssetSnapshot testSkill = CloneSkill(source.Skills[0]);
            StaffDataAssetInventorySnapshot testSnapshot = new StaffDataAssetInventorySnapshot(
                new[] { testStaff },
                new[] { testSkill });

            bool passed = true;
            passed &= VerifyNotSupported(
                "Staff 목록 Add",
                AsMutation(testSnapshot.Staff, () => testSnapshot.Staff[0]),
                errors);
            passed &= VerifyNotSupported(
                "Skill 목록 Add",
                AsMutation(testSnapshot.Skills, () => testSnapshot.Skills[0]),
                errors);
            passed &= VerifyDictionaryNotSupported(
                "StaffById Add",
                testSnapshot.StaffById,
                "__IMMUTABILITY_TEST__",
                testStaff,
                errors);
            passed &= VerifyDictionaryNotSupported(
                "SkillByGuid Add",
                testSnapshot.SkillByGuid,
                "__IMMUTABILITY_TEST__",
                testSkill,
                errors);
            passed &= VerifyNotSupported(
                "Levels Add",
                AsMutation(testStaff.Levels, () => testStaff.Levels[0]),
                errors);
            passed &= VerifyNotSupported(
                "IdleSprites Add",
                AsMutation(testStaff.IdleSpriteReferences, () => testStaff.SpriteReference),
                errors);
            passed &= VerifyNotSupported(
                "ReferencedStaffIds Add",
                AsMutation(testSkill.ReferencedStaffIds, () => "__IMMUTABILITY_TEST__"),
                errors);

            Type[] modelTypes =
            {
                typeof(StaffDataAssetInventorySnapshot),
                typeof(StaffDataAssetSnapshot),
                typeof(StaffLevelAssetSnapshot),
                typeof(StaffSkillAssetSnapshot),
                typeof(StaffAssetReferenceSnapshot)
            };
            for (int index = 0; index < modelTypes.Length; index++)
            {
                passed &= ValidateReadOnlyTypeShape(modelTypes[index], errors);
            }

            return passed;
        }

        private static StaffDataAssetSnapshot CloneStaff(StaffDataAssetSnapshot source)
        {
            return new StaffDataAssetSnapshot(
                source.AssetPath,
                source.AssetGuid,
                source.FileName,
                source.ScriptAssetPath,
                source.ScriptGuid,
                source.UnityObjectName,
                source.Id,
                source.Name,
                source.Description,
                source.ConcreteTypeName,
                source.RoleKey,
                source.RankName,
                source.RankValue,
                source.Speed,
                source.SalesLocationTypeName,
                source.SalesLocationTypeValue,
                source.MoneyTypeName,
                source.MoneyTypeValue,
                source.BuyScore,
                source.BuyPrice,
                source.LevelArrayPropertyPath,
                source.Levels,
                source.SkillReference,
                source.SkillConcreteTypeName,
                source.SkillDuration,
                source.SkillCooldown,
                source.SpriteReference,
                source.ThumbnailReference,
                source.AnimatorControllerReference,
                source.IdleSpriteReferences,
                source.BackSpriteReference,
                source.HandSpriteReference,
                source.HandOffsetX,
                source.HandOffsetY,
                source.UiSpriteReference,
                source.AnimationSpriteReference,
                source.ParticleCount,
                source.ParticleSpriteReferences,
                source.HasExpectedLevelArray,
                source.HasChefAddSpeedField,
                source.HasMissingRequiredReference,
                source.FileNameMatchesId);
        }

        private static StaffSkillAssetSnapshot CloneSkill(StaffSkillAssetSnapshot source)
        {
            return new StaffSkillAssetSnapshot(
                source.AssetPath,
                source.AssetGuid,
                source.FileName,
                source.UnityObjectName,
                source.ScriptAssetPath,
                source.ScriptGuid,
                source.ConcreteTypeName,
                source.Description,
                source.Duration,
                source.Cooldown,
                source.ReferencedStaffIds,
                source.HasMissingScript,
                source.HasMissingSerializedReference);
        }

        private static bool TryCollectTargetAssetState(
            string phase,
            List<string> errors,
            out Dictionary<string, AssetFileState> state)
        {
            state = new Dictionary<string, AssetFileState>(StringComparer.Ordinal);
            IReadOnlyList<string> assetPaths = StaffDataAssetInventoryReader.FindTargetAssetPaths();
            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            for (int index = 0; index < assetPaths.Count; index++)
            {
                string assetPath = assetPaths[index];
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (!TryAddFileState(assetPath, guid, projectRoot, state, errors))
                {
                    errors.Add(phase + " 대상 에셋 해시 수집 실패: " + assetPath);
                }

                string metaPath = assetPath + ".meta";
                if (!TryAddFileState(metaPath, guid, projectRoot, state, errors))
                {
                    errors.Add(phase + " 대상 meta 해시 수집 실패: " + metaPath);
                }
            }

            return state.Count == assetPaths.Count * 2;
        }

        private static bool TryAddFileState(
            string projectRelativePath,
            string assetGuid,
            string projectRoot,
            Dictionary<string, AssetFileState> state,
            List<string> errors)
        {
            try
            {
                string osPath = Path.Combine(
                    projectRoot,
                    projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(osPath))
                {
                    errors.Add("대상 파일이 없습니다: " + projectRelativePath);
                    return false;
                }

                state.Add(
                    projectRelativePath,
                    new AssetFileState(assetGuid, ComputeSha256(File.ReadAllBytes(osPath))));
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(projectRelativePath + " 해시 수집 오류: " + exception.Message);
                return false;
            }
        }

        private static bool CompareAssetStates(
            Dictionary<string, AssetFileState> before,
            Dictionary<string, AssetFileState> after,
            List<string> details,
            List<string> errors)
        {
            bool expectedCountPassed = before.Count == ExpectedComparisonFileCount
                                       && after.Count == ExpectedComparisonFileCount;
            bool fileListPassed = before.Count == after.Count;
            bool shaPassed = true;
            bool guidPassed = true;
            if (!expectedCountPassed)
            {
                errors.Add("실행 전후 비교 파일 수가 Post-B5 기준 "
                           + ExpectedComparisonFileCount + "개와 다릅니다: "
                           + before.Count + "/" + after.Count);
            }

            if (before.Count != after.Count)
            {
                errors.Add("실행 전후 대상 파일 수가 다릅니다.");
            }

            foreach (KeyValuePair<string, AssetFileState> entry in before)
            {
                AssetFileState afterState;
                if (!after.TryGetValue(entry.Key, out afterState))
                {
                    errors.Add("실행 후 사라진 대상 파일입니다: " + entry.Key);
                    fileListPassed = false;
                    continue;
                }

                if (!string.Equals(
                        entry.Value.AssetGuid,
                        afterState.AssetGuid,
                        StringComparison.Ordinal))
                {
                    errors.Add("실행 전후 GUID가 바뀌었습니다: " + entry.Key);
                    guidPassed = false;
                }

                if (!string.Equals(
                        entry.Value.Sha256,
                        afterState.Sha256,
                        StringComparison.Ordinal))
                {
                    errors.Add("실행 전후 SHA-256이 바뀌었습니다: " + entry.Key);
                    shaPassed = false;
                }
            }

            foreach (string path in after.Keys)
            {
                if (!before.ContainsKey(path))
                {
                    errors.Add("실행 후 새로 생긴 대상 파일입니다: " + path);
                    fileListPassed = false;
                }
            }

            details.Add("- 비교 파일: " + before.Count + "/" + after.Count
                        + " (기대 " + ExpectedComparisonFileCount + "/"
                        + ExpectedComparisonFileCount + ")");
            details.Add("- 실행 전후 File List 동일: "
                        + (fileListPassed ? "YES" : "NO"));
            details.Add("- 실행 전후 SHA-256 동일: " + (shaPassed ? "YES" : "NO"));
            details.Add("- 실행 전후 GUID 동일: " + (guidPassed ? "YES" : "NO"));
            return expectedCountPassed && fileListPassed && shaPassed && guidPassed;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static bool TryGetProjectFileSha256(
            string projectRelativePath,
            out string sha256)
        {
            sha256 = string.Empty;
            try
            {
                DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
                string projectRoot = projectDirectory == null
                    ? Application.dataPath
                    : projectDirectory.FullName;
                string osPath = Path.Combine(
                    projectRoot,
                    projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(osPath))
                {
                    return false;
                }

                sha256 = ComputeSha256(File.ReadAllBytes(osPath));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetScriptGuid(ScriptableObject scriptableObject)
        {
            if (scriptableObject == null)
            {
                return string.Empty;
            }

            MonoScript script = MonoScript.FromScriptableObject(scriptableObject);
            string scriptPath = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
            return string.IsNullOrEmpty(scriptPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(scriptPath);
        }

        private static bool HasMissingSerializedReference(SerializedObject serializedObject)
        {
            if (serializedObject == null)
            {
                return true;
            }

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType == SerializedPropertyType.ObjectReference
                    && !string.Equals(iterator.propertyPath, "m_Script", StringComparison.Ordinal)
                    && iterator.objectReferenceValue == null
                    && iterator.objectReferenceInstanceIDValue != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Action AsMutation<T>(IReadOnlyList<T> values, Func<T> valueFactory)
        {
            IList<T> mutable = values as IList<T>;
            return mutable == null ? null : (Action)(() => mutable.Add(valueFactory()));
        }

        private static bool VerifyDictionaryNotSupported<T>(
            string testName,
            IReadOnlyDictionary<string, T> values,
            string key,
            T value,
            List<string> errors)
        {
            IDictionary<string, T> mutable = values as IDictionary<string, T>;
            return VerifyNotSupported(
                testName,
                mutable == null ? null : (Action)(() => mutable.Add(key, value)),
                errors);
        }

        private static bool VerifyNotSupported(
            string testName,
            Action mutationAttempt,
            List<string> errors)
        {
            if (mutationAttempt == null)
            {
                errors.Add(testName + " 검사를 수행할 컬렉션 인터페이스가 없습니다.");
                return false;
            }

            try
            {
                mutationAttempt();
                errors.Add(testName + " 시도가 차단되지 않았습니다.");
                return false;
            }
            catch (NotSupportedException)
            {
                return true;
            }
            catch (Exception exception)
            {
                errors.Add(testName + " 시 예상 밖 오류가 발생했습니다: " + exception.Message);
                return false;
            }
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
                    errors.Add(type.Name + "." + property.Name + "이 수정 가능한 컬렉션을 반환합니다.");
                    passed = false;
                }

                if (IsUnityOrSerializedType(property.PropertyType))
                {
                    errors.Add(type.Name + "." + property.Name + "이 Unity 원본 객체를 노출합니다.");
                    passed = false;
                }
            }

            FieldInfo[] publicFields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            for (int index = 0; index < publicFields.Length; index++)
            {
                if (!publicFields[index].IsInitOnly && !publicFields[index].IsLiteral)
                {
                    errors.Add(type.Name + "." + publicFields[index].Name + "이 mutable public field입니다.");
                    passed = false;
                }
            }

            return passed;
        }

        private static bool IsMutableConcreteCollection(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.IsArray;
            }

            Type genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(List<>) || genericType == typeof(Dictionary<,>);
        }

        private static bool IsUnityOrSerializedType(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return true;
            }

            return type == typeof(SerializedObject) || type == typeof(SerializedProperty);
        }

        private static Dictionary<string, int> CountStaffClasses(
            IReadOnlyList<StaffDataAssetSnapshot> staff)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < staff.Count; index++)
            {
                int count;
                counts.TryGetValue(staff[index].ConcreteTypeName, out count);
                counts[staff[index].ConcreteTypeName] = count + 1;
            }

            return counts;
        }

        private static int CountMissing(params StaffAssetReferenceSnapshot[] references)
        {
            int count = 0;
            for (int index = 0; index < references.Length; index++)
            {
                if (references[index] != null && references[index].IsMissing)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsValidAssignedReference(StaffAssetReferenceSnapshot reference)
        {
            return reference != null && reference.IsAssigned && !reference.IsMissing;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) <= 0.0001f;
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

        private static bool IsPathInSkillFolder(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.StartsWith(
                       StaffDataAssetInventoryReader.SkillFolder + "/",
                       StringComparison.Ordinal);
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
                bool digit = character >= '0' && character <= '9';
                bool lowercaseHex = character >= 'a' && character <= 'f';
                if (!digit && !lowercaseHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool RequireZero(string label, int count, List<string> errors)
        {
            if (count == 0)
            {
                return true;
            }

            errors.Add(label + " 수가 0이 아닙니다: " + count);
            return false;
        }

        private static int GetCount(Dictionary<string, int> counts, string key)
        {
            int count;
            counts.TryGetValue(key, out count);
            return count;
        }

        private static void AddDiagnostics(
            string label,
            IReadOnlyList<string> diagnostics,
            List<string> errors)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                errors.Add(label + " 생성 실패 진단이 없습니다.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                string diagnostic = diagnostics[index];
                errors.Add(
                    label + " - "
                    + (diagnostic.StartsWith("ERROR: ", StringComparison.Ordinal)
                        ? diagnostic.Substring("ERROR: ".Length)
                        : diagnostic));
            }
        }

        private static Dictionary<int, List<string>> CreateDetailSections()
        {
            Dictionary<int, List<string>> result = new Dictionary<int, List<string>>();
            for (int number = 1; number <= 17; number++)
            {
                result.Add(number, new List<string>());
            }

            return result;
        }

        private sealed class ChefPassiveTarget
        {
            internal string StaffId { get; }
            internal string GradeKey { get; }
            internal float ExpectedValue { get; }

            internal ChefPassiveTarget(string staffId, string gradeKey, float expectedValue)
            {
                StaffId = staffId;
                GradeKey = gradeKey;
                ExpectedValue = expectedValue;
            }
        }

        private static void AppendResult(
            StringBuilder output,
            int number,
            string title,
            bool passed,
            IReadOnlyList<string> details)
        {
            output.AppendLine(number + ". " + title + ": " + (passed ? "PASS" : "FAIL"));
            for (int index = 0; index < details.Count; index++)
            {
                output.AppendLine("   " + details[index]);
            }
        }

        private sealed class Skill04MigrationTarget
        {
            internal string StaffId { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal float OfficialDuration { get; }
            internal float OfficialCooldown { get; }

            internal Skill04MigrationTarget(
                string staffId,
                string fileName,
                string objectName,
                string legacyGuid,
                float legacyDuration,
                float legacyCooldown,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
            }
        }

        private sealed class Skill06MigrationTarget
        {
            internal string StaffId { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal float OfficialDuration { get; }
            internal float OfficialCooldown { get; }

            internal Skill06MigrationTarget(
                string staffId,
                string fileName,
                string objectName,
                string legacyGuid,
                float legacyDuration,
                float legacyCooldown,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
            }
        }

        private sealed class Skill05MigrationTarget
        {
            internal string StaffId { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal string LegacyClassName { get; }
            internal float LegacyEffectValue { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal float OfficialDuration { get; }
            internal float OfficialCooldown { get; }

            internal Skill05MigrationTarget(
                string staffId,
                string fileName,
                string objectName,
                string legacyGuid,
                string legacyClassName,
                float legacyEffectValue,
                float legacyDuration,
                float legacyCooldown,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyClassName = legacyClassName;
                LegacyEffectValue = legacyEffectValue;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
            }
        }

        private sealed class RemainingSkillMigrationTarget
        {
            internal string StaffId { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal string LegacyClassName { get; }
            internal float LegacyEffectValue { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal string OfficialClassName { get; }
            internal string OfficialScriptGuid { get; }
            internal string OfficialDescription { get; }
            internal float OfficialEffectValue { get; }
            internal float OfficialDuration { get; }
            internal float OfficialCooldown { get; }

            internal RemainingSkillMigrationTarget(
                string staffId,
                string fileName,
                string objectName,
                string legacyGuid,
                string legacyClassName,
                float legacyEffectValue,
                float legacyDuration,
                float legacyCooldown,
                string officialClassName,
                string officialScriptGuid,
                string officialDescription,
                float officialEffectValue,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyClassName = legacyClassName;
                LegacyEffectValue = legacyEffectValue;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                OfficialClassName = officialClassName;
                OfficialScriptGuid = officialScriptGuid;
                OfficialDescription = officialDescription;
                OfficialEffectValue = officialEffectValue;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
            }
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
