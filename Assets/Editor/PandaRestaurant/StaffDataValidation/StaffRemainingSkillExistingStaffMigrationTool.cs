using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffRemainingSkillExistingStaffMigrationTool
    {
        private const string PreviewMenuPath =
            "Tools/Panda Restaurant/Staff/Preview Remaining Skill Existing Staff Migration";
        private const string ApplyMenuPath =
            "Tools/Panda Restaurant/Staff/Apply Remaining Skill Existing Staff Migration";
        private const string Final17Sha256 =
            "ce51987f36fec57b434d3b3fcaf796a5fbd4d907a5dd8a0b6b9c507db392c68d";
        private const string SkillTypeSha256 =
            "26fdb35b14296418c094a929ddaa040ba94d24ebd20108adab4aaad666b46ad7";
        private const string PackageFingerprint =
            "1ad532e385de422110d0f9fcee4d8ff78add9d570a2307f44ab2d8263af2e15b";
        private const string NormalCustomerMoveScriptPath =
            "Assets/Scripts/Staff/StaffSkill/NormalCustomerMoveSpeedUpSkill.cs";
        private const string NormalCustomerMoveScriptGuid =
            "436b93c9b427dbb45a839f587d572cdc";
        private const string GlobalCookingScriptPath =
            "Assets/Scripts/Staff/StaffSkill/GlobalCookingSpeedUpSkill.cs";
        private const string GlobalCookingScriptGuid =
            "e0e2c583b78c98449a5c47e6abfa0ed7";
        private const string AllStaffMoveScriptPath =
            "Assets/Scripts/Staff/StaffSkill/AllStaffMoveSpeedUpSkill.cs";
        private const string AllStaffMoveScriptGuid =
            "8c58ec39a18e0964595b700ba01914e9";

        private static readonly MigrationTarget[] Targets =
        {
            new MigrationTarget("STAFF06", "포야", "매니저", 2, "59935b680c0c13d42839773c1da87294", "STAFF06Skill.asset", "STAFF06Skill", "13f8b997e9fafaf439550199ddfb76f7", "SpeedUpSkill", 100, 7, 150, "cd99f05f26efdf35f7242ce0562cb50975a53bbb5cd4c663b3f1b6f1849ad8b0", "5100e4c42ffa33147ce56974ec697c8354203bdd877fc51026284307d4a23336", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", "_normalCustomerMoveSpeedUpPercent", 100, 18, 150),
            new MigrationTarget("STAFF08", "판다 마쉬", "매니저", 2, "294edc94f7487574784882cf4e07a170", "STAFF08Skill.asset", "STAFF08Skill", "6daef952310a94a41b88276b9cd1cef6", "SpeedUpSkill", 100, 13, 150, "59fbd54deb31ca7439bf8eeff9507fb836cb37fb86a34a32ec3fbbc2cca96184", "07dbbf6be2cabdb4e8a12357ca98796381b0f66286d5541e24be5a3c3e3865c9", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", "_normalCustomerMoveSpeedUpPercent", 100, 18, 150),
            new MigrationTarget("STAFF10", "타로", "매니저", 2, "3e5b3acb2a4730c4492a86a55f93354c", "STAFF10Skill.asset", "STAFF10Skill", "95590518b98a27a4cacc1a699f5f9868", "SpeedUpSkill", 100, 18, 150, "eb89a9b056b94b478998bb67da105a13b277be6712556a2faf36dcf12a892512", "826506f1dbb9158493b175ed9dd490f12e2eccc8f52789dc9b8cb120c4d4d5ee", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", "_normalCustomerMoveSpeedUpPercent", 100, 18, 150),
            new MigrationTarget("STAFF15", "폴", "치어리더", 2, "11f9f22c01f895148bf8d4850846a90b", "Staff15Skill.asset", "STAFF15Skill", "d417214c6d7368941984a20f5d509d44", "TouchAddCustomerButtonSkill", 0.5f, 20, 100, "e124e43bc3b2bf079c6f9459b19a959d6cc45b9b096d0a572daa9e73be0f0ca2", "bcc921d85b3f13217a4d1f74c7dda03d93aa8bd22beacbe8de4b2150cfde52b4", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", "_normalCustomerMoveSpeedUpPercent", 100, 18, 150),
            new MigrationTarget("STAFF30", "피터", "가드", 3, "c46f6778341b4814da437128b0ad3376", "Staff30Skill.asset", "Staff30Skill", "474e93abb1dd6db45baa9ab57be2cef1", "SpeedUpSkill", 100, 30, 80, "54917aa828f654e7de6e712ed273bc70e0e7e45a348ab84eb2382516e6d8f548", "12ad0b7f07aa8f10524ec8cfab4bcb8656e111de6c489b7bc5bcf57b965c054f", "STAFF_SKILL08", "NormalCustomerMoveSpeedUpSkill", "_normalCustomerMoveSpeedUpPercent", 100, 20, 145),
            new MigrationTarget("STAFF27", "고든 팬지", "주방장", 5, "9b53d48f70593ca4bb248b9fa7193cdd", "STAFF27Skill.asset", "STAFF27SKILL", "4803555ba0e7f7f46af709248b6ac2be", "SpeedUpSkill", 100, 60, 250, "bf3d91b6f1b78bedad789b7a3131798356522b7a619f8824194e9ba46795aae6", "62aa0dfe48bcf573136c6677ac4aacde1bff1282d8eee306259a8957a96766f5", "STAFF_SKILL09", "GlobalCookingSpeedUpSkill", "_globalCookingSpeedUpPercent", 50, 30, 200)
        };

        private static readonly Dictionary<string, OfficialTarget> ExpectedNewTargets =
            new Dictionary<string, OfficialTarget>(StringComparer.Ordinal)
            {
                { "STAFF38", new OfficialTarget("팬 케이크 포야", "매니저", 4, "STAFF_SKILL08", 22, 140) },
                { "STAFF71", new OfficialTarget("스윗 비비", "청소부", 4, "STAFF_SKILL08", 22, 140) },
                { "STAFF72", new OfficialTarget("호량콘", "웨이터", 4, "STAFF_SKILL08", 22, 140) },
                { "STAFF74", new OfficialTarget("쿵푸 마쉬", "매니저", 4, "STAFF_SKILL08", 22, 140) },
                { "STAFF84", new OfficialTarget("마쉬 꾼", "매니저", 4, "STAFF_SKILL08", 22, 140) },
                { "STAFF68", new OfficialTarget("스윗 염소아치", "주방장", 5, "STAFF_SKILL09", 30, 200) },
                { "STAFF39", new OfficialTarget("책사 포야", "매니저", 5, "STAFF_SKILL10", 20, 220) },
                { "STAFF40", new OfficialTarget("포상궁", "매니저", 5, "STAFF_SKILL10", 20, 220) },
                { "STAFF64", new OfficialTarget("캔디 마쉬", "매니저", 5, "STAFF_SKILL10", 20, 220) },
                { "STAFF83", new OfficialTarget("추도령", "웨이터", 5, "STAFF_SKILL10", 20, 220) },
                { "STAFF90", new OfficialTarget("복호량", "웨이터", 5, "STAFF_SKILL10", 20, 220) }
            };

        private static readonly LegacyRequirement[] ExistingLegacyAssets =
        {
            new LegacyRequirement("STAFF04", "Staff04Skill.asset", "STAFF04Skill", "ace4d82401b0b084691be89b9fc0f63e", "SpeedUpSkill", 100, 16, 150, "192d22d117c8ecce76ad7b2f2771931a3a1a722f8da56865e7e3f94a004a107d", "8139051e70657b672e0552ace12c602156cc82e41cd35d7ac3404ab66653d018"),
            new LegacyRequirement("STAFF05", "Staff05Skill.asset", "STAFF05Skill", "224358ced57ae144ba65a5f1692e6f1c", "SpeedUpSkill", 100, 18, 150, "437b43c7ae6d6d60d9f9949da6ad81ac2bc3885dac356f86c7813614632ffd5a", "f57e85eaa6412b6bbb4715c445faf5d983bccda1db908925090d91318e92e318"),
            new LegacyRequirement("STAFF07", "STAFF07Skill.asset", "STAFF07Skill", "18348c8c96719374da1f6ba1bcd2987d", "SpeedUpSkill", 100, 10, 150, "72ef1086f08c9e577894bbfc5774bf9631292861695c87b787d95607ad6e9661", "72e410e3246820563624f8039f27c2f84a6fb3a15a98c1d02bfffbbd5b48f1eb"),
            new LegacyRequirement("STAFF09", "STAFF09Skill.asset", "STAFF09Skill", "3576053ed0b398d43a296e16eaf3aff6", "SpeedUpSkill", 100, 16, 150, "e22bbaf8713d24af445ba8a645eaa40f439a7e60baf5af4d3cea0f4b6ed988ad", "c91777fbd7cf53f4d78e476826ea036067344499844d66b5c099d1bfd9f12b9b"),
            new LegacyRequirement("STAFF13", "Staff13Skill.asset", "STAFF13Skill", "cc1627697d70f09418e134e717be2a29", "TouchAddCustomerButtonSkill", 0.5f, 13, 150, "4ab4a5f1815b7fe0434e7f4ff6c2b38c43b896e9504122ca1235b0a8ac64af8b", "fdb3b74e7b5da467fbc9582e5f5dbd2e891708a1839cef78c91131c4fcc33093"),
            new LegacyRequirement("STAFF17", "Staff17Skill.asset", "STAFF17Skill", "c1305190b57c1d54482ece9b2e58be3d", "SpeedUpSkill", 100, 18, 200, "129dc73e0895a1940d8365bb121e45901266ea43da82fb0cd2416197d30b209e", "53d1dc79e03c12031506d0399fbf878051e117df6947537117a8561682d9b4fc"),
            new LegacyRequirement("STAFF19", "Staff19Skill.asset", "STAFF19Skill", "67fad8354daa0194fbbcf5833b9ebdca", "SpeedUpSkill", 100, 24, 150, "1bd9b9d8feafbd4778c85e953db765b1bffa5b09e9fedcab430d437a8e4ef856", "2593fed13f76c81b15e75031a5d003e4af5db1348e33830602dfa60cfa9aa82c"),
            new LegacyRequirement("STAFF20", "Staff20Skill.asset", "STAFF20Skill", "ab2b64bcb83dc9d48b5773f0c88a830e", "SpeedUpSkill", 100, 27, 150, "dfd6e71e54589c8e2e3514a2d728674b83ec4d03cccde9eae65502b1192fc78d", "dba1742ba4d833d0946379401247bd4178be2b7f3941716034cdadb7f380a847"),
            new LegacyRequirement("STAFF26", "Staff26Skill.asset", "STAFF26Skill", "f3029210afede654ca6f3c33a2016896", "SpeedUpSkill", 100, 30, 50, "fa7ea7f0a98c4a0fee00fdd48f28c5c62a2c74efeae9f26f4b8e708bd265de05", "f0c84a1dbcbbed288f1e78c522c25bf80bb9a6e59cea06775e8674b988b6431c"),
            new LegacyRequirement("STAFF29", "STAFF29SKILL.asset", "STAFF29SKILL", "6513e175122c20641a60cad9e71895fa", "SpeedUpSkill", 100, 30, 150, "7ad90c47668da17a4661986ad337a3e2dad347ea787ce7c27f14ab8fb1f82b9c", "1bebe95e0f43ef5a2cdf16ab25da080aa2a1057dc01d684e2836b4c884c2de26"),
            new LegacyRequirement("STAFF32", "Staff32Skill.asset", "Staff32Skill", "9579cb0591dadfa4da2e662700923026", "SpeedUpSkill", 100, 30, 80, "e206a17fdf5edede91df4e335e278d4d5cb2d64edea8fd2715903c39160317ad", "5feee4e55f7fabfe59681d83ea8d1dd28474fdb22e7a883e6033a89803efd605")
        };

        [MenuItem(PreviewMenuPath)]
        private static void PreviewMigration()
        {
            RunMigrationMenu(false);
        }

        [MenuItem(ApplyMenuPath)]
        private static void ApplyMigrationFromMenu()
        {
            RunMigrationMenu(true);
        }

        private static void RunMigrationMenu(bool apply)
        {
            if (apply && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "[Remaining Skill Existing Staff Migration]\n"
                    + "APPLY FAIL: Play Mode에서는 실행할 수 없습니다.");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Select Final17 official Staff data package folder",
                projectRoot,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                Debug.LogWarning("Remaining Skill Existing Staff Migration was cancelled. 변경 0개.");
                return;
            }

            MigrationInspection inspection;
            List<string> errors = new List<string>();
            bool inspected = TryInspect(selectedFolder, out inspection, errors);
            if (!inspected || inspection == null)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (inspection.State == MigrationState.ALREADY_APPLIED)
            {
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, true);
                Debug.Log("ALREADY_APPLIED: Skill08·09 기존 직원 6명은 이미 완전히 전환됐습니다. 변경 0개.");
                return;
            }

            if (inspection.State != MigrationState.READY_TO_APPLY)
            {
                errors.Add("PARTIAL_MIGRATION_STATE: 일부 적용 또는 예상하지 않은 Asset 상태입니다.");
                LogInspection(apply ? "APPLY" : "PREVIEW", inspection, errors, false);
                return;
            }

            if (!apply)
            {
                LogInspection("PREVIEW", inspection, errors, true);
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Remaining Skill Existing Staff Migration",
                "STAFF06, STAFF08, STAFF10, STAFF15, STAFF30, STAFF27의 구형 Skill을 LegacySkill로 이동하고 "
                + "공식 Skill08·09 Asset을 만든 뒤 StaffData의 _skill 참조만 교체합니다.\n\n"
                + "계속하시겠습니까?",
                "Apply",
                "Cancel");
            if (!confirmed)
            {
                Debug.LogWarning("Remaining Skill Existing Staff Migration Apply가 취소됐습니다. 변경 0개.");
                return;
            }

            ApplyMigration(selectedFolder, inspection);
        }

        private static bool TryInspect(
            string officialFolder,
            out MigrationInspection inspection,
            List<string> errors)
        {
            inspection = new MigrationInspection(officialFolder);
            StaffOfficialDataPackageSnapshot official;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataPackValidator.TryBuildReadOnlySnapshot(
                    officialFolder,
                    out official,
                    out diagnostics)
                || official == null)
            {
                AddDiagnostics("Official Snapshot", diagnostics, errors);
                inspection.State = MigrationState.INVALID;
                return false;
            }

            inspection.PackageFingerprint = official.PackageFingerprint;
            if (!ValidateOfficialRemainingSkills(official, inspection, errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateRemainingSkillScripts(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateExistingLegacyAssets(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            if (!ValidateNewStaffAssetsAbsent(errors))
            {
                inspection.State = MigrationState.INVALID;
                return false;
            }

            int initialCount = 0;
            int appliedCount = 0;
            HashSet<string> appliedGuids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> legacyGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Targets.Length; index++)
            {
                legacyGuids.Add(Targets[index].LegacyGuid);
            }

            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                legacyGuids.Add(ExistingLegacyAssets[index].Guid);
            }

            for (int index = 0; index < Targets.Length; index++)
            {
                TargetInspection targetInspection = InspectTarget(
                    Targets[index],
                    inspection.OfficialDescriptions,
                    errors);
                inspection.Targets.Add(targetInspection);
                initialCount += targetInspection.State == TargetState.INITIAL ? 1 : 0;
                appliedCount += targetInspection.State == TargetState.APPLIED ? 1 : 0;
                if (targetInspection.State == TargetState.APPLIED
                    && (!appliedGuids.Add(targetInspection.ActiveGuid)
                        || legacyGuids.Contains(targetInspection.ActiveGuid)))
                {
                    errors.Add("신규 Skill08·09 GUID가 중복되었습니다: " + targetInspection.ActiveGuid);
                }
            }

            if (errors.Count != 0)
            {
                inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
                return false;
            }

            if (initialCount == Targets.Length)
            {
                inspection.State = MigrationState.READY_TO_APPLY;
                return true;
            }

            if (appliedCount == Targets.Length && appliedGuids.Count == Targets.Length)
            {
                inspection.State = MigrationState.ALREADY_APPLIED;
                return true;
            }

            inspection.State = MigrationState.PARTIAL_MIGRATION_STATE;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: Initial " + initialCount
                + "/" + Targets.Length + ", Applied " + appliedCount
                + "/" + Targets.Length);
            return false;
        }

        private static bool ValidateOfficialRemainingSkills(
            StaffOfficialDataPackageSnapshot official,
            MigrationInspection inspection,
            List<string> errors)
        {
            StaffOfficialFileSnapshot final17;
            StaffOfficialFileSnapshot skillType;
            if (!official.TryGetFile("Final17", out final17)
                || !official.TryGetFile("SkillType", out skillType))
            {
                errors.Add("OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: Final17 또는 SkillType Snapshot이 없습니다.");
                return false;
            }

            bool valid = true;
            if (final17.Sha256 != Final17Sha256)
            {
                errors.Add("Final17 SHA-256 불일치: " + final17.Sha256);
                valid = false;
            }

            if (skillType.Sha256 != SkillTypeSha256)
            {
                errors.Add("SkillType SHA-256 불일치: " + skillType.Sha256);
                valid = false;
            }

            if (official.PackageFingerprint != PackageFingerprint)
            {
                errors.Add("Package Fingerprint 불일치: " + official.PackageFingerprint);
                valid = false;
            }

            Dictionary<string, string> descriptions =
                new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < skillType.Rows.Count; index++)
            {
                IReadOnlyList<string> row = skillType.Rows[index];
                if (row.Count < 2)
                {
                    continue;
                }

                string id = row[0].Trim();
                if (id != "STAFF_SKILL08" && id != "STAFF_SKILL09" && id != "STAFF_SKILL10")
                {
                    continue;
                }

                if (descriptions.ContainsKey(id))
                {
                    errors.Add("OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: SkillType 중복 " + id);
                    valid = false;
                    continue;
                }

                descriptions.Add(id, row[1]);
            }

            bool descriptionsValid = descriptions.Count == 3
                                     && descriptions.ContainsKey("STAFF_SKILL08")
                                     && descriptions.ContainsKey("STAFF_SKILL09")
                                     && descriptions.ContainsKey("STAFF_SKILL10")
                                     && descriptions["STAFF_SKILL08"].Contains("(100%)")
                                     && descriptions["STAFF_SKILL09"].Contains("(50%)")
                                     && descriptions["STAFF_SKILL10"].Contains("(50%)");
            if (!descriptionsValid)
            {
                errors.Add("OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: SkillType 정의·효과량 불일치.");
                valid = false;
            }

            Dictionary<string, IReadOnlyList<string>> remainingRows =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            for (int index = 0; index < final17.Rows.Count; index++)
            {
                IReadOnlyList<string> row = final17.Rows[index];
                string skillId = row.Count >= 14 ? row[10].Trim() : string.Empty;
                if (skillId == "STAFF_SKILL08"
                    || skillId == "STAFF_SKILL09"
                    || skillId == "STAFF_SKILL10")
                {
                    string id = row[0].Trim();
                    if (remainingRows.ContainsKey(id))
                    {
                        errors.Add("OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: 중복 ID " + id);
                        valid = false;
                    }
                    else
                    {
                        remainingRows.Add(id, row);
                    }
                }
            }

            HashSet<string> expectedAll = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                expectedAll.Add(target.StaffId);
                IReadOnlyList<string> row;
                double duration;
                double cooldown;
                int stars;
                bool targetValid = remainingRows.TryGetValue(target.StaffId, out row)
                                   && row.Count >= 14
                                   && row[1].Trim() == target.OfficialName
                                   && row[6].Trim() == target.OfficialRole
                                   && int.TryParse(row[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out stars)
                                   && stars == target.OfficialStars
                                   && row[10].Trim() == target.OfficialSkillId
                                   && TryParseSeconds(row[12], out duration)
                                   && TryParseSeconds(row[13], out cooldown)
                                   && Approximately(duration, target.OfficialDuration)
                                   && Approximately(cooldown, target.OfficialCooldown);
                if (!targetValid)
                {
                    errors.Add("OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: " + target.StaffId);
                    valid = false;
                }
            }

            foreach (KeyValuePair<string, OfficialTarget> pair in ExpectedNewTargets)
            {
                expectedAll.Add(pair.Key);
                IReadOnlyList<string> row;
                double duration;
                double cooldown;
                int stars;
                OfficialTarget target = pair.Value;
                bool targetValid = remainingRows.TryGetValue(pair.Key, out row)
                                   && row.Count >= 14
                                   && row[1].Trim() == target.Name
                                   && row[6].Trim() == target.Role
                                   && int.TryParse(row[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out stars)
                                   && stars == target.Stars
                                   && row[10].Trim() == target.SkillId
                                   && TryParseSeconds(row[12], out duration)
                                   && TryParseSeconds(row[13], out cooldown)
                                   && Approximately(duration, target.Duration)
                                   && Approximately(cooldown, target.Cooldown);
                if (!targetValid)
                {
                    errors.Add("OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: " + pair.Key);
                    valid = false;
                }
            }

            int skill08Count = 0;
            int skill09Count = 0;
            int skill10Count = 0;
            foreach (KeyValuePair<string, IReadOnlyList<string>> pair in remainingRows)
            {
                string skillId = pair.Value[10].Trim();
                skill08Count += skillId == "STAFF_SKILL08" ? 1 : 0;
                skill09Count += skillId == "STAFF_SKILL09" ? 1 : 0;
                skill10Count += skillId == "STAFF_SKILL10" ? 1 : 0;
            }

            if (remainingRows.Count != 17
                || expectedAll.Count != 17
                || !new HashSet<string>(remainingRows.Keys, StringComparer.Ordinal).SetEquals(expectedAll)
                || skill08Count != 10
                || skill09Count != 2
                || skill10Count != 5)
            {
                errors.Add(
                    "OFFICIAL_REMAINING_SKILL_DISTRIBUTION_CHANGED: 전체 " + remainingRows.Count
                    + ", Skill08/09/10 " + skill08Count + "/" + skill09Count + "/" + skill10Count);
                valid = false;
            }

            if (valid)
            {
                foreach (KeyValuePair<string, string> pair in descriptions)
                {
                    inspection.OfficialDescriptions.Add(pair.Key, pair.Value);
                }
            }

            return valid;
        }

        private static bool ValidateRemainingSkillScripts(List<string> errors)
        {
            bool normal = ValidateScript(
                NormalCustomerMoveScriptPath,
                NormalCustomerMoveScriptGuid,
                typeof(NormalCustomerMoveSpeedUpSkill));
            bool cooking = ValidateScript(
                GlobalCookingScriptPath,
                GlobalCookingScriptGuid,
                typeof(GlobalCookingSpeedUpSkill));
            bool allStaff = ValidateScript(
                AllStaffMoveScriptPath,
                AllStaffMoveScriptGuid,
                typeof(AllStaffMoveSpeedUpSkill));
            bool valid = normal && cooking && allStaff;
            if (!valid)
            {
                errors.Add(
                    "Skill08·09·10 Runtime Script 또는 GUID가 기준과 다릅니다.");
            }

            return valid;
        }

        private static bool ValidateScript(string path, string expectedGuid, Type expectedType)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            return AssetDatabase.AssetPathToGUID(path) == expectedGuid
                   && script != null
                   && script.GetClass() == expectedType;
        }

        private static bool ValidateExistingLegacyAssets(List<string> errors)
        {
            if (!AssetDatabase.IsValidFolder(StaffDataAssetInventoryReader.LegacySkillFolder))
            {
                errors.Add("기존 LegacySkill 폴더가 없습니다.");
                return false;
            }

            bool valid = true;
            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                LegacyRequirement target = ExistingLegacyAssets[index];
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                string guid = AssetDatabase.AssetPathToGUID(path);
                SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(path);
                bool preserved = skill != null
                                 && guid == target.Guid
                                 && skill.GetType().Name == target.ClassName
                                 && skill.name == target.ObjectName
                                 && string.IsNullOrEmpty(skill.Description)
                                 && Approximately(skill.Duration, target.Duration)
                                 && Approximately(skill.Cooldown, target.Cooldown)
                                 && Approximately(skill.FirstValue, target.EffectValue)
                                 && ComputeAssetFileSha256(path) == target.AssetSha256
                                 && ComputeAssetFileSha256(path + ".meta") == target.MetaSha256
                                 && FindAssetReferences(path).Count == 0;
                if (!preserved)
                {
                    errors.Add("기존 Legacy 11개 Asset 기준 또는 SHA-256 불일치: " + target.StaffId);
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidateNewStaffAssetsAbsent(List<string> errors)
        {
            bool valid = true;
            foreach (string staffId in ExpectedNewTargets.Keys)
            {
                string staffPath = "Assets/Resources/StaffData/" + staffId + ".asset";
                string skillPath = StaffDataAssetInventoryReader.SkillFolder
                                   + "/" + staffId + "Skill.asset";
                bool absent = string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(staffPath))
                              && string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(skillPath));
                if (!absent)
                {
                    errors.Add("신규 계획 대상에 예상하지 않은 실제 Asset이 있습니다: " + staffId);
                    valid = false;
                }
            }

            return valid;
        }

        private static TargetInspection InspectTarget(
            MigrationTarget target,
            IReadOnlyDictionary<string, string> officialDescriptions,
            List<string> errors)
        {
            TargetInspection result = new TargetInspection(target);
            StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
            if (staff == null)
            {
                errors.Add("StaffData를 읽을 수 없습니다: " + target.StaffDataPath);
                return result;
            }

            string staffGuid = AssetDatabase.AssetPathToGUID(target.StaffDataPath);
            SerializedObject staffSerialized = new SerializedObject(staff);
            SerializedProperty skillReference = staffSerialized.FindProperty("_skill");
            if (staffGuid != target.StaffDataGuid || skillReference == null)
            {
                errors.Add("StaffData GUID 또는 _skill 필드가 유효하지 않습니다: " + target.StaffId);
                return result;
            }

            UnityEngine.Object referencedSkill = skillReference.objectReferenceValue;
            string referencedPath = referencedSkill == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(referencedSkill);
            string activeGuid = AssetDatabase.AssetPathToGUID(target.ActivePath);
            string legacyGuid = AssetDatabase.AssetPathToGUID(target.LegacyPath);
            UnityEngine.Object activeObject = AssetDatabase.LoadMainAssetAtPath(target.ActivePath);
            UnityEngine.Object legacyObject = AssetDatabase.LoadMainAssetAtPath(target.LegacyPath);
            result.StaffGuid = staffGuid;
            result.ActiveGuid = activeGuid;
            result.LegacyGuid = legacyGuid;

            bool legacyPathFree = legacyObject == null && string.IsNullOrEmpty(legacyGuid);
            List<string> initialReferences = FindAssetReferences(target.ActivePath);
            string officialDescription;
            if (!officialDescriptions.TryGetValue(target.OfficialSkillId, out officialDescription))
            {
                errors.Add("공식 SkillType 설명을 찾을 수 없습니다: " + target.OfficialSkillId);
                return result;
            }

            bool initial = MatchesLegacySkill(activeObject, target, target.ActivePath)
                           && activeGuid == target.LegacyGuid
                           && referencedPath == target.ActivePath
                           && legacyPathFree
                           && IsExactSingleReference(initialReferences, target.StaffDataPath);
            if (initial)
            {
                result.State = TargetState.INITIAL;
                return result;
            }

            SkillBase activeSkill = activeObject as SkillBase;
            List<string> activeReferences = FindAssetReferences(target.ActivePath);
            List<string> legacyReferences = FindAssetReferences(target.LegacyPath);
            bool applied = activeSkill != null
                           && activeSkill.GetType().Name == target.OfficialClassName
                           && MatchesLegacySkill(legacyObject, target, target.LegacyPath)
                           && IsUnityGuid(activeGuid)
                           && activeGuid != target.LegacyGuid
                           && legacyGuid == target.LegacyGuid
                           && referencedPath == target.ActivePath
                           && activeSkill.name == target.ObjectName
                           && activeSkill.Description == officialDescription
                           && Approximately(activeSkill.Duration, target.OfficialDuration)
                           && Approximately(activeSkill.Cooldown, target.OfficialCooldown)
                           && Approximately(activeSkill.FirstValue, target.OfficialEffectValue)
                           && GetScriptGuid(activeSkill) == target.OfficialScriptGuid
                           && IsExactSingleReference(activeReferences, target.StaffDataPath)
                           && legacyReferences.Count == 0;
            if (applied)
            {
                result.State = TargetState.APPLIED;
                return result;
            }

            result.State = TargetState.INVALID;
            errors.Add(
                "PARTIAL_MIGRATION_STATE: " + target.StaffId
                + " active=" + target.ActivePath + " (" + activeGuid + ")"
                + ", legacy=" + target.LegacyPath + " (" + legacyGuid + ")"
                + ", staff reference=" + referencedPath);
            return result;
        }

        private static bool MatchesLegacySkill(
            UnityEngine.Object asset,
            MigrationTarget target,
            string assetPath)
        {
            SkillBase skill = asset as SkillBase;
            if (skill == null
                || skill.GetType().Name != target.LegacyClassName
                || skill.name != target.ObjectName
                || !string.IsNullOrEmpty(skill.Description)
                || !Approximately(skill.Duration, target.LegacyDuration)
                || !Approximately(skill.Cooldown, target.LegacyCooldown))
            {
                return false;
            }

            return Approximately(skill.FirstValue, target.LegacyEffectValue)
                   && ComputeAssetFileSha256(assetPath) == target.LegacyAssetSha256
                   && ComputeAssetFileSha256(assetPath + ".meta") == target.LegacyMetaSha256;
        }

        private static string GetScriptGuid(SkillBase skill)
        {
            MonoScript script = skill == null ? null : MonoScript.FromScriptableObject(skill);
            return script == null ? string.Empty : AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(script));
        }

        private static void ApplyMigration(
            string officialFolder,
            MigrationInspection inspection)
        {
            MigrationInspection finalPreflight;
            List<string> finalPreflightErrors = new List<string>();
            if (!TryInspect(officialFolder, out finalPreflight, finalPreflightErrors)
                || finalPreflight.State != MigrationState.READY_TO_APPLY)
            {
                finalPreflightErrors.Add(
                    "APPLY 직전 상태가 READY_TO_APPLY가 아닙니다. 변경 0개.");
                LogInspection("APPLY", finalPreflight, finalPreflightErrors, false);
                return;
            }

            MigrationBackup backup = null;
            List<string> createdAssetPaths = new List<string>();
            List<MigrationTarget> movedTargets = new List<MigrationTarget>();
            bool writesStarted = false;
            try
            {
                backup = CaptureBackup();
                writesStarted = true;
                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    string moveError = AssetDatabase.MoveAsset(target.ActivePath, target.LegacyPath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " Legacy 이동 실패: " + moveError);
                    }

                    movedTargets.Add(target);
                    if (AssetDatabase.AssetPathToGUID(target.LegacyPath) != target.LegacyGuid)
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " Legacy GUID가 이동 중 변경됐습니다.");
                    }
                }

                Dictionary<string, SkillBase> newAssets =
                    new Dictionary<string, SkillBase>(StringComparer.Ordinal);
                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    SkillBase skill = CreateOfficialSkill(target);
                    AssetDatabase.CreateAsset(skill, target.ActivePath);
                    createdAssetPaths.Add(target.ActivePath);
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(target.ActivePath)))
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " 신규 Skill08·09 Asset 생성에 실패했습니다.");
                    }

                    skill.name = target.ObjectName;
                    ConfigureNewSkill(
                        skill,
                        target,
                        finalPreflight.OfficialDescriptions[target.OfficialSkillId]);
                    EditorUtility.SetDirty(skill);
                    newAssets.Add(target.StaffId, skill);
                }

                for (int index = 0; index < Targets.Length; index++)
                {
                    MigrationTarget target = Targets[index];
                    StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                    if (staff == null)
                    {
                        throw new InvalidOperationException(
                            target.StaffId + " StaffData를 다시 읽을 수 없습니다.");
                    }

                    SetSkillReference(staff, newAssets[target.StaffId]);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                List<string> postErrors = new List<string>();
                MigrationInspection postInspection;
                bool postPassed = TryInspect(officialFolder, out postInspection, postErrors)
                                  && postInspection.State == MigrationState.ALREADY_APPLIED
                                  && ValidatePostApplyFiles(backup, postErrors)
                                  && ValidatePostInventory(postErrors)
                                  && ValidatePostDryRun(officialFolder, postErrors);
                if (!postPassed)
                {
                    throw new InvalidOperationException(
                        "Post-Apply 검증 실패: " + string.Join(" | ", postErrors.ToArray()));
                }

                LogApplySuccess(inspection, postInspection);
            }
            catch (Exception exception)
            {
                List<string> rollbackErrors = new List<string>();
                bool rollbackPassed = !writesStarted || RollbackMigration(
                                          officialFolder,
                                          backup,
                                          createdAssetPaths,
                                          movedTargets,
                                          rollbackErrors);
                StringBuilder output = new StringBuilder();
                output.AppendLine("[Remaining Skill Existing Staff Migration]");
                output.AppendLine("APPLY FAIL: " + exception.Message);
                output.AppendLine(
                    "Atomic Rollback: "
                    + (!writesStarted ? "NOT_REQUIRED" : rollbackPassed ? "PASS" : "FAIL"));
                for (int index = 0; index < rollbackErrors.Count; index++)
                {
                    output.AppendLine("ROLLBACK ERROR: " + rollbackErrors[index]);
                }

                if (!rollbackPassed)
                {
                    output.AppendLine("CRITICAL_MIGRATION_ROLLBACK_FAILED");
                }

                Debug.LogError(output.ToString());
            }
        }

        private static MigrationBackup CaptureBackup()
        {
            MigrationBackup backup = new MigrationBackup();
            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                if (staff == null)
                {
                    throw new InvalidOperationException("Backup StaffData 누락: " + target.StaffId);
                }

                backup.Targets.Add(
                    target.StaffId,
                    new TargetBackup(
                        AssetDatabase.AssetPathToGUID(target.StaffDataPath),
                        CaptureSerializedFingerprint(staff, "_skill"),
                        ComputeAssetFileSha256(target.ActivePath),
                        ComputeAssetFileSha256(target.ActivePath + ".meta")));
            }

            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                LegacyRequirement target = ExistingLegacyAssets[index];
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                backup.ExistingLegacyFiles.Add(
                    target.StaffId,
                    new FileBackup(
                        ComputeAssetFileSha256(path),
                        ComputeAssetFileSha256(path + ".meta")));
            }

            return backup;
        }

        private static SkillBase CreateOfficialSkill(MigrationTarget target)
        {
            if (target.OfficialSkillId == "STAFF_SKILL08")
            {
                return ScriptableObject.CreateInstance<NormalCustomerMoveSpeedUpSkill>();
            }

            if (target.OfficialSkillId == "STAFF_SKILL09")
            {
                return ScriptableObject.CreateInstance<GlobalCookingSpeedUpSkill>();
            }

            throw new InvalidOperationException(
                target.StaffId + "에 지원되지 않는 공식 Skill ID가 지정됐습니다: "
                + target.OfficialSkillId);
        }

        private static void ConfigureNewSkill(
            SkillBase skill,
            MigrationTarget target,
            string officialDescription)
        {
            SerializedObject serialized = new SerializedObject(skill);
            SerializedProperty description = serialized.FindProperty("_description");
            SerializedProperty duration = serialized.FindProperty("_duration");
            SerializedProperty cooldown = serialized.FindProperty("_cooldown");
            SerializedProperty percent = serialized.FindProperty(target.OfficialEffectFieldName);
            if (description == null || duration == null || cooldown == null || percent == null)
            {
                throw new InvalidOperationException(
                    target.StaffId + " Skill08·09 직렬화 필드를 찾을 수 없습니다.");
            }

            description.stringValue = officialDescription;
            duration.floatValue = target.OfficialDuration;
            cooldown.floatValue = target.OfficialCooldown;
            percent.floatValue = target.OfficialEffectValue;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException(
                    target.StaffId + " Skill08·09 값 적용에 실패했습니다.");
            }
        }

        private static void SetSkillReference(StaffData staff, SkillBase skill)
        {
            SerializedObject serialized = new SerializedObject(staff);
            SerializedProperty skillProperty = serialized.FindProperty("_skill");
            if (skillProperty == null)
            {
                throw new InvalidOperationException(staff.name + "._skill 필드를 찾을 수 없습니다.");
            }

            if (skillProperty.objectReferenceValue == skill)
            {
                return;
            }

            skillProperty.objectReferenceValue = skill;
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException(staff.name + "._skill 참조 적용에 실패했습니다.");
            }

            EditorUtility.SetDirty(staff);
        }

        private static bool ValidatePostApplyFiles(
            MigrationBackup backup,
            List<string> errors)
        {
            bool valid = backup != null;
            for (int index = 0; index < Targets.Length && valid; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup;
                if (!backup.Targets.TryGetValue(target.StaffId, out targetBackup))
                {
                    errors.Add("Post-Apply backup 누락: " + target.StaffId);
                    valid = false;
                    continue;
                }

                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                bool targetValid = staff != null
                                   && AssetDatabase.AssetPathToGUID(target.StaffDataPath)
                                   == targetBackup.StaffDataGuid
                                   && CaptureSerializedFingerprint(staff, "_skill")
                                   == targetBackup.StaffDataFingerprint
                                   && ComputeAssetFileSha256(target.LegacyPath)
                                   == targetBackup.LegacyAssetSha256
                                   && ComputeAssetFileSha256(target.LegacyPath + ".meta")
                                   == targetBackup.LegacyMetaSha256;
                if (!targetValid)
                {
                    errors.Add("Post-Apply 비대상 값 또는 Legacy 파일 변경: " + target.StaffId);
                    valid = false;
                }
            }

            valid &= ValidateExistingLegacyFiles(backup, errors, "Post-Apply");

            return valid;
        }

        private static bool ValidateExistingLegacyFiles(
            MigrationBackup backup,
            List<string> errors,
            string phase)
        {
            if (backup == null)
            {
                return false;
            }

            bool valid = true;
            for (int index = 0; index < ExistingLegacyAssets.Length; index++)
            {
                LegacyRequirement target = ExistingLegacyAssets[index];
                FileBackup fileBackup;
                string path = StaffDataAssetInventoryReader.LegacySkillFolder + "/" + target.FileName;
                bool unchanged = backup.ExistingLegacyFiles.TryGetValue(target.StaffId, out fileBackup)
                                 && ComputeAssetFileSha256(path) == fileBackup.AssetSha256
                                 && ComputeAssetFileSha256(path + ".meta") == fileBackup.MetaSha256;
                if (!unchanged)
                {
                    errors.Add(phase + " 기존 Legacy 11개 파일 변경: " + target.StaffId);
                    valid = false;
                }
            }

            return valid;
        }

        private static bool ValidatePostInventory(List<string> errors)
        {
            StaffDataAssetInventorySnapshot snapshot;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataAssetInventoryReader.TryBuildReadOnlyInventory(
                    out snapshot,
                    out diagnostics)
                || snapshot == null)
            {
                AddDiagnostics("Post-Apply Current Inventory", diagnostics, errors);
                return false;
            }

            Dictionary<string, int> classes = new Dictionary<string, int>(StringComparer.Ordinal);
            int shared = 0;
            int orphan = 0;
            for (int index = 0; index < snapshot.Skills.Count; index++)
            {
                StaffSkillAssetSnapshot skill = snapshot.Skills[index];
                int count;
                classes.TryGetValue(skill.ConcreteTypeName, out count);
                classes[skill.ConcreteTypeName] = count + 1;
                shared += skill.IsShared ? 1 : 0;
                orphan += skill.IsOrphan ? 1 : 0;
            }

            int legacySkillCount = 0;
            int legacyActiveReferences = 0;
            string[] legacyGuids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { StaffDataAssetInventoryReader.LegacySkillFolder });
            for (int index = 0; index < legacyGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(legacyGuids[index]);
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                    && AssetDatabase.LoadAssetAtPath<SkillBase>(path) != null)
                {
                    legacySkillCount++;
                    legacyActiveReferences += FindAssetReferences(path).Count;
                }
            }

            bool valid = snapshot.Skills.Count == 32
                         && GetCount(classes, "SpeedUpSkill") == 12
                         && GetCount(classes, "TouchAddCustomerButtonSkill") == 3
                         && GetCount(classes, "AssignedCookingSpeedUpSkill") == 4
                         && GetCount(classes, "FoodPaymentTipUpSkill") == 1
                         && GetCount(classes, "FoodPriceUpSkill") == 6
                         && GetCount(classes, "NormalCustomerMoveSpeedUpSkill") == 5
                         && GetCount(classes, "GlobalCookingSpeedUpSkill") == 1
                         && GetCount(classes, "AllStaffMoveSpeedUpSkill") == 0
                         && classes.Count == 7
                         && legacySkillCount == 17
                         && shared == 0
                         && orphan == 0
                         && legacyActiveReferences == 0;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Inventory V7 불일치: active " + snapshot.Skills.Count
                    + ", class " + GetCount(classes, "SpeedUpSkill") + "/"
                    + GetCount(classes, "TouchAddCustomerButtonSkill") + "/"
                    + GetCount(classes, "AssignedCookingSpeedUpSkill") + "/"
                    + GetCount(classes, "FoodPaymentTipUpSkill") + "/"
                    + GetCount(classes, "FoodPriceUpSkill") + "/"
                    + GetCount(classes, "NormalCustomerMoveSpeedUpSkill") + "/"
                    + GetCount(classes, "GlobalCookingSpeedUpSkill") + "/"
                    + GetCount(classes, "AllStaffMoveSpeedUpSkill")
                    + ", legacy " + legacySkillCount
                    + ", shared/orphan " + shared + "/" + orphan
                    + ", legacy refs " + legacyActiveReferences);
            }

            return valid;
        }

        private static bool ValidatePostDryRun(
            string officialFolder,
            List<string> errors)
        {
            StaffDataDryRunPlanSnapshot plan;
            IReadOnlyList<string> diagnostics;
            if (!StaffDataDryRunPlanner.TryBuildReadOnlyPlan(
                    officialFolder,
                    out plan,
                    out diagnostics)
                || plan == null)
            {
                AddDiagnostics("Post-Apply Dry Run V7", diagnostics, errors);
                return false;
            }

            int warnings = 0;
            int prerequisites = 0;
            int changedFields = 0;
            int existingMismatch = 0;
            int newUnsupported = 0;
            int existingWarnings = 0;
            int existingClass = 0;
            int existingSave = 0;
            int newReady = 0;
            int newClass = 0;
            int durationMismatch = 0;
            int cooldownMismatch = 0;
            for (int index = 0; index < plan.GlobalIssues.Count; index++)
            {
                warnings += plan.GlobalIssues[index].IsWarning ? 1 : 0;
                prerequisites += plan.GlobalIssues[index].IsPrerequisite ? 1 : 0;
            }

            for (int index = 0; index < plan.StaffPlans.Count; index++)
            {
                StaffDataDryRunStaffPlan staff = plan.StaffPlans[index];
                changedFields += staff.ChangedFieldCount;
                if (staff.AssetAction == StaffDryRunAssetAction.UPDATE_EXISTING)
                {
                    durationMismatch += SkillNumbersEqual(
                        staff.SkillPlan.CurrentDuration,
                        staff.SkillPlan.TargetDuration) ? 0 : 1;
                    cooldownMismatch += SkillNumbersEqual(
                        staff.SkillPlan.CurrentCooldown,
                        staff.SkillPlan.TargetCooldown) ? 0 : 1;
                    existingWarnings += staff.Readiness
                                        == StaffDryRunReadiness.PLAN_READY_WITH_WARNINGS ? 1 : 0;
                    existingClass += staff.Readiness
                                     == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                    existingSave += staff.Readiness
                                    == StaffDryRunReadiness.SAVE_MIGRATION_REQUIRED ? 1 : 0;
                }
                else
                {
                    newReady += staff.Readiness == StaffDryRunReadiness.ASSET_PLAN_READY ? 1 : 0;
                    newClass += staff.Readiness == StaffDryRunReadiness.SKILL_CLASS_REQUIRED ? 1 : 0;
                }

                for (int issueIndex = 0; issueIndex < staff.Issues.Count; issueIndex++)
                {
                    StaffDataDryRunIssue issue = staff.Issues[issueIndex];
                    warnings += issue.IsWarning ? 1 : 0;
                    prerequisites += issue.IsPrerequisite ? 1 : 0;
                    existingMismatch += issue.Code == "EXISTING_SKILL_CLASS_MISMATCH" ? 1 : 0;
                    newUnsupported += issue.Code == "NEW_SKILL_CLASS_IMPLEMENTATION_REQUIRED" ? 1 : 0;
                }
            }

            bool valid = plan.PlanningPolicyVersion
                          == "STAFF_DRY_RUN_POLICY_2026_08_20_V7"
                          && existingMismatch == 0
                          && newUnsupported == 0
                          && prerequisites == 1
                          && warnings == 65
                          && durationMismatch == 0
                          && cooldownMismatch == 0
                          && changedFields == 2146
                          && existingWarnings == 31
                          && existingClass == 0
                          && existingSave == 1
                          && newReady == 60
                          && newClass == 0;
            if (!valid)
            {
                errors.Add(
                    "Post-Apply Dry Run V7 baseline 불일치: mismatch "
                    + existingMismatch + "/" + newUnsupported
                    + ", prerequisite/warning " + prerequisites + "/" + warnings
                    + ", duration/cooldown " + durationMismatch + "/" + cooldownMismatch
                    + ", field " + changedFields);
            }

            return valid;
        }

        private static bool RollbackMigration(
            string officialFolder,
            MigrationBackup backup,
            List<string> createdAssetPaths,
            List<MigrationTarget> movedTargets,
            List<string> errors)
        {
            bool valid = true;
            try
            {
                for (int index = 0; index < movedTargets.Count; index++)
                {
                    MigrationTarget target = movedTargets[index];
                    StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                    SkillBase legacy = AssetDatabase.LoadAssetAtPath<SkillBase>(target.LegacyPath);
                    if (staff != null && legacy != null)
                    {
                        SetSkillReference(staff, legacy);
                    }
                }

                AssetDatabase.SaveAssets();
                for (int index = createdAssetPaths.Count - 1; index >= 0; index--)
                {
                    string path = createdAssetPaths[index];
                    if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path))
                        && !AssetDatabase.DeleteAsset(path))
                    {
                        errors.Add("신규 Skill08·09 삭제 실패: " + path);
                        valid = false;
                    }
                }

                for (int index = movedTargets.Count - 1; index >= 0; index--)
                {
                    MigrationTarget target = movedTargets[index];
                    if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(target.LegacyPath)))
                    {
                        continue;
                    }

                    string moveError = AssetDatabase.MoveAsset(target.LegacyPath, target.ActivePath);
                    if (!string.IsNullOrEmpty(moveError))
                    {
                        errors.Add(target.StaffId + " 원래 경로 복구 실패: " + moveError);
                        valid = false;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception exception)
            {
                errors.Add("Rollback 예외: " + exception.Message);
                valid = false;
            }

            if (backup == null)
            {
                errors.Add("Rollback 검증용 원본 Backup이 없습니다.");
                return false;
            }

            MigrationInspection rollbackInspection;
            List<string> inspectionErrors = new List<string>();
            bool initialRestored = TryInspect(
                                       officialFolder,
                                       out rollbackInspection,
                                       inspectionErrors)
                                   && rollbackInspection.State == MigrationState.READY_TO_APPLY;
            if (!initialRestored)
            {
                errors.AddRange(inspectionErrors);
                errors.Add("Rollback 후 Initial 상태가 복구되지 않았습니다.");
                valid = false;
            }

            for (int index = 0; index < Targets.Length; index++)
            {
                MigrationTarget target = Targets[index];
                TargetBackup targetBackup;
                StaffData staff = AssetDatabase.LoadAssetAtPath<StaffData>(target.StaffDataPath);
                bool targetRestored = backup.Targets.TryGetValue(target.StaffId, out targetBackup)
                                      && staff != null
                                      && AssetDatabase.AssetPathToGUID(target.StaffDataPath)
                                      == targetBackup.StaffDataGuid
                                      && CaptureSerializedFingerprint(staff, "_skill")
                                      == targetBackup.StaffDataFingerprint
                                      && ComputeAssetFileSha256(target.ActivePath)
                                      == targetBackup.LegacyAssetSha256
                                      && ComputeAssetFileSha256(target.ActivePath + ".meta")
                                      == targetBackup.LegacyMetaSha256;
                if (!targetRestored)
                {
                    errors.Add("Rollback 원본 값 검증 실패: " + target.StaffId);
                    valid = false;
                }
            }

            valid &= ValidateExistingLegacyFiles(backup, errors, "Rollback");

            return valid;
        }

        private static string CaptureSerializedFingerprint(
            UnityEngine.Object asset,
            string excludedPropertyPath)
        {
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.GetIterator();
            StringBuilder input = new StringBuilder();
            bool enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = true;
                if (property.propertyPath == excludedPropertyPath
                    || property.propertyPath.StartsWith(
                        excludedPropertyPath + ".",
                        StringComparison.Ordinal))
                {
                    enterChildren = false;
                    continue;
                }

                input.Append(property.propertyPath);
                input.Append('|');
                input.Append(property.propertyType);
                input.Append('|');
                AppendSerializedValue(input, property);
                input.Append('\n');
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(input.ToString()));
        }

        private static void AppendSerializedValue(
            StringBuilder input,
            SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    input.Append(property.longValue.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.Boolean:
                    input.Append(property.boolValue ? "1" : "0");
                    break;
                case SerializedPropertyType.Float:
                    input.Append(property.doubleValue.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.String:
                    input.Append(property.stringValue);
                    break;
                case SerializedPropertyType.Enum:
                    input.Append(property.enumValueIndex.ToString(CultureInfo.InvariantCulture));
                    break;
                case SerializedPropertyType.ObjectReference:
                    string guid;
                    long localId;
                    if (property.objectReferenceValue != null
                        && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            property.objectReferenceValue,
                            out guid,
                            out localId))
                    {
                        input.Append(guid);
                        input.Append(':');
                        input.Append(localId.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        input.Append("null");
                    }

                    break;
                default:
                    input.Append(property.type);
                    if (property.isArray)
                    {
                        input.Append(':');
                        input.Append(property.arraySize.ToString(CultureInfo.InvariantCulture));
                    }

                    break;
            }
        }

        private static List<string> FindAssetReferences(string targetPath)
        {
            List<string> references = new List<string>();
            if (string.IsNullOrEmpty(targetPath)
                || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(targetPath)))
            {
                return references;
            }

            string[] paths = AssetDatabase.GetAllAssetPaths();
            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
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

        private static bool IsExactSingleReference(
            IReadOnlyList<string> references,
            string expectedPath)
        {
            return references.Count == 1 && references[0] == expectedPath;
        }

        private static bool TryParseSeconds(string value, out double seconds)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (normalized.EndsWith("초", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1).Trim();
            }

            return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out seconds);
        }

        private static bool SkillNumbersEqual(string left, string right)
        {
            double leftValue;
            double rightValue;
            return double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out leftValue)
                   && double.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out rightValue)
                   && Approximately(leftValue, rightValue);
        }

        private static bool Approximately(double left, double right)
        {
            return Math.Abs(left - right) <= 0.0001d;
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
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!digit && !lowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetCount(Dictionary<string, int> values, string key)
        {
            int value;
            values.TryGetValue(key, out value);
            return value;
        }

        private static string ComputeAssetFileSha256(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string absolutePath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                return string.Empty;
            }

            return ComputeSha256(File.ReadAllBytes(absolutePath));
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder output = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    output.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return output.ToString();
            }
        }

        private static void AddDiagnostics(
            string label,
            IReadOnlyList<string> diagnostics,
            List<string> errors)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                errors.Add(label + " 실패 진단이 없습니다.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                errors.Add(label + ": " + diagnostics[index]);
            }
        }

        private static void LogInspection(
            string phase,
            MigrationInspection inspection,
            IReadOnlyList<string> errors,
            bool passed)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Remaining Skill Existing Staff Migration " + phase + "]");
            if (inspection != null)
            {
                output.AppendLine("Official Package Fingerprint: " + inspection.PackageFingerprint);
                output.AppendLine("Migration State: " + inspection.State);
                for (int index = 0; index < inspection.Targets.Count; index++)
                {
                    TargetInspection target = inspection.Targets[index];
                    output.AppendLine(
                        "- " + target.Target.StaffId + " " + target.State
                        + " | Active " + target.Target.ActivePath + " (" + target.ActiveGuid + ")"
                        + " | Legacy " + target.Target.LegacyPath + " (" + target.LegacyGuid + ")"
                        + " | Official " + target.Target.OfficialDuration + "/"
                        + target.Target.OfficialCooldown);
                }

                if (inspection.State == MigrationState.READY_TO_APPLY)
                {
                    output.AppendLine("Expected: 구형 Skill 6개 Legacy 이동, Skill08 5개·Skill09 1개 생성, StaffData._skill 6개 교체");
                    output.AppendLine("New STAFF38/39/40/64/68/71/72/74/83/84/90: 계획 검증만 수행, 실제 Asset 생성 없음");
                }
            }

            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("ERROR: " + errors[index]);
            }

            output.AppendLine(
                phase + " "
                + (inspection != null && inspection.State == MigrationState.ALREADY_APPLIED
                    ? "ALREADY_APPLIED"
                    : passed ? "PASS" : "FAIL"));
            if (phase == "PREVIEW")
            {
                output.AppendLine(
                    "REMAINING SKILL EXISTING STAFF MIGRATION PREVIEW: "
                    + (passed ? "PASS" : "FAIL"));
            }
            output.AppendLine("Asset write: 0");
            if (passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static void LogApplySuccess(
            MigrationInspection before,
            MigrationInspection after)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Remaining Skill Existing Staff Migration APPLY]");
            output.AppendLine("APPLY PASS");
            output.AppendLine("- LegacySkill 이동 및 GUID 보존: 6/6 PASS");
            output.AppendLine("- Skill08 5개·Skill09 1개 생성: 6/6 PASS");
            output.AppendLine("- StaffData _skill 참조 교체: 6/6 PASS");
            output.AppendLine("- 신규 Skill08·09 GUID 유효·고유: 6/6 PASS");
            output.AppendLine("- 기존 Legacy 11개 회귀: 11/11 PASS");
            output.AppendLine("- Active Skill Inventory: 32 (12/3/4/1/6/5/1/0)");
            output.AppendLine("- Dry Run Policy: STAFF_DRY_RUN_POLICY_2026_08_20_V7");
            for (int index = 0; index < after.Targets.Count; index++)
            {
                TargetInspection target = after.Targets[index];
                output.AppendLine(
                    "- " + target.Target.StaffId + " Active GUID " + target.ActiveGuid
                    + " | Legacy GUID " + target.LegacyGuid);
            }

            Debug.Log(output.ToString());
        }

        private enum MigrationState
        {
            INVALID,
            READY_TO_APPLY,
            ALREADY_APPLIED,
            PARTIAL_MIGRATION_STATE
        }

        private enum TargetState
        {
            INVALID,
            INITIAL,
            APPLIED
        }

        private sealed class MigrationTarget
        {
            internal string StaffId { get; }
            internal string OfficialName { get; }
            internal string OfficialRole { get; }
            internal int OfficialStars { get; }
            internal string StaffDataGuid { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string LegacyGuid { get; }
            internal string LegacyClassName { get; }
            internal float LegacyEffectValue { get; }
            internal float LegacyDuration { get; }
            internal float LegacyCooldown { get; }
            internal string LegacyAssetSha256 { get; }
            internal string LegacyMetaSha256 { get; }
            internal string OfficialSkillId { get; }
            internal string OfficialClassName { get; }
            internal string OfficialEffectFieldName { get; }
            internal float OfficialEffectValue { get; }
            internal float OfficialDuration { get; }
            internal float OfficialCooldown { get; }
            internal string OfficialScriptGuid
            {
                get
                {
                    return OfficialSkillId == "STAFF_SKILL08"
                        ? NormalCustomerMoveScriptGuid
                        : GlobalCookingScriptGuid;
                }
            }
            internal string StaffDataPath { get { return "Assets/Resources/StaffData/" + StaffId + ".asset"; } }
            internal string ActivePath { get { return StaffDataAssetInventoryReader.SkillFolder + "/" + FileName; } }
            internal string LegacyPath { get { return StaffDataAssetInventoryReader.LegacySkillFolder + "/" + FileName; } }

            internal MigrationTarget(
                string staffId,
                string officialName,
                string officialRole,
                int officialStars,
                string staffDataGuid,
                string fileName,
                string objectName,
                string legacyGuid,
                string legacyClassName,
                float legacyEffectValue,
                float legacyDuration,
                float legacyCooldown,
                string legacyAssetSha256,
                string legacyMetaSha256,
                string officialSkillId,
                string officialClassName,
                string officialEffectFieldName,
                float officialEffectValue,
                float officialDuration,
                float officialCooldown)
            {
                StaffId = staffId;
                OfficialName = officialName;
                OfficialRole = officialRole;
                OfficialStars = officialStars;
                StaffDataGuid = staffDataGuid;
                FileName = fileName;
                ObjectName = objectName;
                LegacyGuid = legacyGuid;
                LegacyClassName = legacyClassName;
                LegacyEffectValue = legacyEffectValue;
                LegacyDuration = legacyDuration;
                LegacyCooldown = legacyCooldown;
                LegacyAssetSha256 = legacyAssetSha256;
                LegacyMetaSha256 = legacyMetaSha256;
                OfficialSkillId = officialSkillId;
                OfficialClassName = officialClassName;
                OfficialEffectFieldName = officialEffectFieldName;
                OfficialEffectValue = officialEffectValue;
                OfficialDuration = officialDuration;
                OfficialCooldown = officialCooldown;
            }
        }

        private sealed class OfficialTarget
        {
            internal string Name { get; }
            internal string Role { get; }
            internal int Stars { get; }
            internal string SkillId { get; }
            internal double Duration { get; }
            internal double Cooldown { get; }

            internal OfficialTarget(
                string name,
                string role,
                int stars,
                string skillId,
                double duration,
                double cooldown)
            {
                Name = name;
                Role = role;
                Stars = stars;
                SkillId = skillId;
                Duration = duration;
                Cooldown = cooldown;
            }
        }

        private sealed class LegacyRequirement
        {
            internal string StaffId { get; }
            internal string FileName { get; }
            internal string ObjectName { get; }
            internal string Guid { get; }
            internal string ClassName { get; }
            internal double EffectValue { get; }
            internal double Duration { get; }
            internal double Cooldown { get; }
            internal string AssetSha256 { get; }
            internal string MetaSha256 { get; }

            internal LegacyRequirement(
                string staffId,
                string fileName,
                string objectName,
                string guid,
                string className,
                double effectValue,
                double duration,
                double cooldown,
                string assetSha256,
                string metaSha256)
            {
                StaffId = staffId;
                FileName = fileName;
                ObjectName = objectName;
                Guid = guid;
                ClassName = className;
                EffectValue = effectValue;
                Duration = duration;
                Cooldown = cooldown;
                AssetSha256 = assetSha256;
                MetaSha256 = metaSha256;
            }
        }

        private sealed class TargetInspection
        {
            internal MigrationTarget Target { get; }
            internal TargetState State;
            internal string StaffGuid = string.Empty;
            internal string ActiveGuid = string.Empty;
            internal string LegacyGuid = string.Empty;

            internal TargetInspection(MigrationTarget target)
            {
                Target = target;
            }
        }

        private sealed class MigrationInspection
        {
            internal string OfficialFolder { get; }
            internal string PackageFingerprint = string.Empty;
            internal MigrationState State;
            internal readonly List<TargetInspection> Targets = new List<TargetInspection>();
            internal readonly Dictionary<string, string> OfficialDescriptions =
                new Dictionary<string, string>(StringComparer.Ordinal);

            internal MigrationInspection(string officialFolder)
            {
                OfficialFolder = officialFolder;
            }
        }

        private sealed class MigrationBackup
        {
            internal readonly Dictionary<string, TargetBackup> Targets =
                new Dictionary<string, TargetBackup>(StringComparer.Ordinal);
            internal readonly Dictionary<string, FileBackup> ExistingLegacyFiles =
                new Dictionary<string, FileBackup>(StringComparer.Ordinal);
        }

        private sealed class FileBackup
        {
            internal string AssetSha256 { get; }
            internal string MetaSha256 { get; }

            internal FileBackup(string assetSha256, string metaSha256)
            {
                AssetSha256 = assetSha256;
                MetaSha256 = metaSha256;
            }
        }

        private sealed class TargetBackup
        {
            internal string StaffDataGuid { get; }
            internal string StaffDataFingerprint { get; }
            internal string LegacyAssetSha256 { get; }
            internal string LegacyMetaSha256 { get; }

            internal TargetBackup(
                string staffDataGuid,
                string staffDataFingerprint,
                string legacyAssetSha256,
                string legacyMetaSha256)
            {
                StaffDataGuid = staffDataGuid;
                StaffDataFingerprint = staffDataFingerprint;
                LegacyAssetSha256 = legacyAssetSha256;
                LegacyMetaSha256 = legacyMetaSha256;
            }
        }
    }
}
