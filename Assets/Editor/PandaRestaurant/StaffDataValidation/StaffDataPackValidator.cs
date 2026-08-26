using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffDataPackValidator
    {
        private const string MenuPath = "Tools/Panda Restaurant/Staff/Validate Staff Data Pack";
        private const string ExpectedBranch = "26/08/06_CodexTest_01(Staff-Skill-01)";
        private const string ReservedUnusedSlotACode = "RESERVED_UNUSED_SLOT_A";
        private const string ReservedUnusedSlotADescription = "예약 스킬 슬롯 A (현재 미사용)";
        private const string ReservedUnusedSlotBCode = "RESERVED_UNUSED_SLOT_B";
        private const string ReservedUnusedSlotBDescription = "예약 스킬 슬롯 B (현재 미사용)";

        private static readonly OfficialFileSpec[] OfficialFiles =
        {
            new OfficialFileSpec(
                "Final17",
                "Final17 StaffData",
                "ce51987f36fec57b434d3b3fcaf796a5fbd4d907a5dd8a0b6b9c507db392c68d",
                93,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "RoleBase",
                "StaffRoleBaseRule",
                "b87dc00bdfd11f9e1c890a164ce3bb6c8f36d2e7926ebe1117dce39e077f1495",
                35,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "RoleGrowth",
                "StaffRoleGrowthRule",
                "396bb7be6199fb68db51ffc2b8507d9b4f8004b2516fc8045561bd13f147c413",
                7,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "LevelRule",
                "StaffSkillLevelRule",
                "afccbbb4e3e092097df4c84c66edde001afa4a002f4c371e0ae877cb88f28605",
                6,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "CostRule",
                "StaffUpgradeCostRule",
                "92a8fd3fc1bc236c7f3aaa671cbbb8386b40976d0feb3fda13d5595e731fa35b",
                5,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "Summary",
                "StaffUpgradeSummary",
                "392b0e0396c5144bbb7c58a247a483d0b76ea30759ad65fb81f0ea58ab69b689",
                5,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "Policy",
                "StaffUpgradePolicy",
                "4a78ec9d409d7d20e58026f0cde7d68662c230091517f40656ee464ba689760b",
                14,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                "SkillType",
                "StaffSkillType",
                "26fdb35b14296418c094a929ddaa040ba94d24ebd20108adab4aaad666b46ad7",
                11,
                ExpectedEncoding.Cp949),
            new OfficialFileSpec(
                "GachaUpgradeType",
                "Final03 GOTCHA UpgradeType",
                "fd3b74d6972f4e97e6dc50d944162af35c1ca5b0504fa903088c3c914e5e6994",
                31,
                ExpectedEncoding.Cp949)
        };

        private static readonly OfficialFileSpec[] V8OfficialFiles =
        {
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.FinalStaff,
                "Final18 StaffData",
                "298f6c9f032b5f7ca73facd3a6d13649af0bf6357a5e802064a7f52ac706e51f",
                93,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.RoleBase,
                "StaffRoleBaseRule",
                "9ade4cdbf6f1d1cd84cd0b138545641e6ae8ef63db85238a65e08c1c32cfd17e",
                35,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.RoleGrowth,
                "StaffRoleGrowthRule",
                "396bb7be6199fb68db51ffc2b8507d9b4f8004b2516fc8045561bd13f147c413",
                7,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.LevelRule,
                "StaffSkillLevelRule",
                "afccbbb4e3e092097df4c84c66edde001afa4a002f4c371e0ae877cb88f28605",
                6,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.CostRule,
                "StaffUpgradeCostRule",
                "92a8fd3fc1bc236c7f3aaa671cbbb8386b40976d0feb3fda13d5595e731fa35b",
                5,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.Summary,
                "StaffUpgradeSummary",
                "392b0e0396c5144bbb7c58a247a483d0b76ea30759ad65fb81f0ea58ab69b689",
                5,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.Policy,
                "StaffUpgradePolicy",
                "4a78ec9d409d7d20e58026f0cde7d68662c230091517f40656ee464ba689760b",
                14,
                ExpectedEncoding.Utf8Bom),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.SkillType,
                "Final02 StaffSkillType",
                "80dd32318d554e4f1ad0d19493184503c4a81ed081db74fc8980aeb5a425bfd0",
                11,
                ExpectedEncoding.Cp949),
            new OfficialFileSpec(
                StaffOfficialDataPackageKeys.GachaUpgradeType,
                "Final03 GOTCHA UpgradeType",
                "fd3b74d6972f4e97e6dc50d944162af35c1ca5b0504fa903088c3c914e5e6994",
                31,
                ExpectedEncoding.Cp949)
        };

        private static readonly string[] Final17Headers =
        {
            "ID",
            "이름",
            "설명*",
            "가챠 확률",
            "희귀도(별)",
            "등급",
            "역할",
            "패시브",
            "패시브 설명",
            "기본속도",
            "스킬",
            "스킬 설명",
            "스킬 타임",
            "쿨타임",
            "획득 방법 재화",
            "중복시 지급 토큰",
            "토큰 구매 가격"
        };

        private static readonly string[] FinalStaffHeaders =
        {
            "ID",
            "이름",
            "설명*",
            "가챠 확률",
            "희귀도(별)",
            "등급",
            "역할",
            "패시브",
            "패시브 설명",
            "기본속도",
            "스킬",
            "스킬 설명",
            "스킬 타임",
            "쿨타임",
            "획득 방법 재화",
            "중복시 지급 토큰",
            "토큰 구매 가격"
        };

        private static readonly string[] GradeKeys = { "NORMAL", "RARE", "UNIQUE", "SPECIAL" };
        private static readonly string[] RoleKeys = { "WAITER", "CLEANER", "CHEF", "MANAGER", "CHEERLEADER", "GUARD" };

        private static readonly string[] GachaUpgradeTypeHeaders =
        {
            "강화 TYPE ID",
            "강화 내용(숫자는 초록색으로 색 변경)",
            "강화",
            "단위",
            "속성"
        };

        private static readonly GachaUpgradeExpectation[] GachaUpgradeExpectations =
        {
            new GachaUpgradeExpectation(
                "UPGRADE18",
                "전체 스텝 스킬 타임 n% 증가",
                "전체 스텝 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE19",
                "매니저 스텝 스킬 타임 n% 증가",
                "매니저 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE20",
                "웨이터 스텝 스킬 타임 n% 증가",
                "웨이터 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE21",
                "주방장 스텝 스킬 타임 n% 증가",
                "주방장 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE22",
                "치어리더 스텝 스킬 타임 n% 증가",
                "치어리더 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE23",
                "청소부 스텝 스킬 타임 n% 증가",
                "청소부 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE24",
                "가드 스텝 스킬 타임 n% 증가",
                "가드 스킬 지속시간 증가",
                "지속시간%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE25",
                "웨이터 스텝 이동 속도 n% 증가",
                "웨이터 이동 속도 증가",
                "이동속도%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE26",
                "주방장 스텝 이동 속도 n% 증가",
                "주방장 이동 속도 증가",
                "이동속도%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE27",
                "청소부 스텝 이동 속도 n% 증가",
                "청소부 이동 속도 증가",
                "이동속도%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE28",
                "가드 스텝 퇴치 속도 n% 증가",
                "가드 퇴치 속도 증가",
                "퇴치속도%증가"),
            new GachaUpgradeExpectation(
                "UPGRADE29",
                "피버 타임 n초 증가",
                "피버 타임 증가",
                "SEC"),
            new GachaUpgradeExpectation(
                "UPGRADE30",
                "최대 줄서기 손님 n명 증가",
                "최대 줄서기 손님 증가",
                "인원 증가")
        };

        [MenuItem(MenuPath)]
        private static void ValidateStaffDataPack()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Staff Data Pack Validation]");
            output.AppendLine();

            string activeFolder;
            StaffOfficialDataSourceKind sourceKind;
            string resolveError;
            bool resolved = StaffOfficialDataPathResolver.TryResolveActiveFolder(
                out activeFolder,
                out sourceKind,
                out resolveError);
            output.AppendLine("Policy: " + StaffOfficialDataPathResolver.PolicyMarker);
            output.AppendLine("Active Folder: " + (resolved ? activeFolder : "확인 실패"));
            output.AppendLine("SourceKind: " + (resolved ? sourceKind.ToString() : "확인 실패"));
            output.AppendLine(
                "Canonical / Override: "
                + (resolved && sourceKind == StaffOfficialDataSourceKind.SessionOverride
                    ? "Override"
                    : "Canonical"));

            GitInfo gitInfo = ReadGitInfo();
            output.AppendLine("Git branch: " + gitInfo.Branch);
            output.AppendLine("Git HEAD: " + gitInfo.Head);
            output.AppendLine();

            StaffOfficialDataPackageSnapshot snapshot;
            IReadOnlyList<string> diagnostics;
            bool built = StaffDataPackValidator.TryBuildCanonicalV8ReadOnlySnapshot(
                out snapshot,
                out diagnostics);

            output.AppendLine("1. Active Folder 해석: " + (resolved ? "PASS" : "FAIL"));
            output.AppendLine("2. V8 Snapshot 생성: " + (built ? "PASS" : "FAIL"));
            output.AppendLine(
                "3. 공식 파일 9개 Strict Package: "
                + (built && snapshot.OfficialFileCount == V8OfficialFiles.Length ? "PASS" : "FAIL"));

            if (built && snapshot != null)
            {
                for (int index = 0; index < snapshot.FilesInOfficialOrder.Count; index++)
                {
                    StaffOfficialFileSnapshot file = snapshot.FilesInOfficialOrder[index];
                    output.AppendLine(
                        "   - " + file.Key + " / " + file.DisplayName
                        + " / SHA-256 " + file.Sha256
                        + " / " + file.EncodingLabel
                        + " / " + file.ActualPhysicalLineCount + "행");
                }
            }

            output.AppendLine(
                "4. Final18 StaffData ID·구조: " + (built ? "PASS - STAFF01~92 / 92명 / 17열" : "FAIL"));
            output.AppendLine(
                "5. Skill 참조·분포: "
                + (built
                    ? "PASS - 01:34, 02:0, 03:15, 04:13, 05:10, 06:3, 07:0, 08:10, 09:2, 10:5"
                    : "FAIL"));
            output.AppendLine("6. Skill03 완전 동결: " + (built ? "SKILL03_UNCHANGED_PASS" : "FAIL"));
            output.AppendLine("7. Skill04: " + (built ? "PASS - 추가 250% / 최종 3.5배" : "FAIL"));
            output.AppendLine("8. Skill09: " + (built ? "PASS - STAFF27·STAFF68 / 1초 / 240초" : "FAIL"));
            output.AppendLine("9. RoleBase·Chef Passive: " + (built ? "PASS - 50 / 70 / 100 / 200" : "FAIL"));
            output.AppendLine("10. RoleGrowth·LevelRule: " + (built ? "PASS" : "FAIL"));
            output.AppendLine("11. Cost/Summary·Policy: " + (built ? "PASS" : "FAIL"));
            output.AppendLine("12. GOTCHA UpgradeType: " + (built ? "PASS" : "FAIL"));
            output.AppendLine(
                "13. PackageFingerprint: "
                + (built && snapshot != null ? snapshot.PackageFingerprint : "생성 실패"));

            int warningCount = 0;
            int errorCount = resolved ? 0 : 1;
            if (!resolved)
            {
                output.AppendLine("   ERROR: " + resolveError);
            }

            if (diagnostics != null)
            {
                for (int index = 0; index < diagnostics.Count; index++)
                {
                    string diagnostic = diagnostics[index];
                    output.AppendLine("   " + diagnostic);
                    if (diagnostic.StartsWith("WARNING: ", StringComparison.Ordinal))
                    {
                        warningCount++;
                    }
                    else if (diagnostic.StartsWith("ERROR: ", StringComparison.Ordinal))
                    {
                        errorCount++;
                    }
                }
            }

            bool passed = resolved && built && snapshot != null && errorCount == 0;
            output.AppendLine("14. 경고 수: " + warningCount);
            output.AppendLine("15. 오류 수: " + errorCount);
            output.AppendLine("16. 최종 결과: " + (passed ? "PASS" : "FAIL"));
            output.AppendLine();
            output.AppendLine("STAFF DATA PACK VALIDATION: " + (passed ? "PASS" : "FAIL"));

            if (passed)
            {
                UnityEngine.Debug.Log(output.ToString());
            }
            else
            {
                UnityEngine.Debug.LogError(output.ToString());
            }
        }

        internal static bool TryBuildReadOnlySnapshot(
            string selectedFolder,
            out StaffOfficialDataPackageSnapshot snapshot,
            out IReadOnlyList<string> diagnostics)
        {
            snapshot = null;
            ValidationReport report = new ValidationReport();
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                report.Error("공식 데이터 폴더가 선택되지 않았습니다.");
                diagnostics = CreateReadOnlyDiagnostics(report);
                return false;
            }

            GitInfo gitInfo = ReadGitInfo();
            if (!gitInfo.BranchAvailable)
            {
                report.Error("현재 Git 브랜치를 읽지 못했습니다: " + gitInfo.ErrorMessage);
            }
            else if (!string.Equals(gitInfo.Branch, ExpectedBranch, StringComparison.Ordinal))
            {
                report.Error(
                    "현재 Git 브랜치가 지정 브랜치와 다릅니다. 현재: " + gitInfo.Branch
                    + ", 지정: " + ExpectedBranch);
            }

            if (!gitInfo.HeadAvailable)
            {
                report.Error("현재 Git HEAD를 읽지 못했습니다: " + gitInfo.ErrorMessage);
            }

            Dictionary<string, MatchedFile> matchedFiles = new Dictionary<string, MatchedFile>(StringComparer.Ordinal);
            MatchOfficialFiles(selectedFolder, matchedFiles, report);
            BuildHashResult(matchedFiles, report);
            ValidateEncodings(matchedFiles, report);
            ValidatePhysicalLineCounts(matchedFiles, report);

            Dictionary<string, CsvTable> tables = new Dictionary<string, CsvTable>(StringComparer.Ordinal);
            Dictionary<string, string> tableErrors = new Dictionary<string, string>(StringComparer.Ordinal);
            BuildCsvTables(matchedFiles, tables, tableErrors);

            List<FinalStaffRow> finalRows;
            ValidateFinal17(tables, tableErrors, report, out finalRows);

            HashSet<string> skillIds;
            ValidateSkillTypes(tables, tableErrors, finalRows, report, out skillIds);

            Dictionary<string, RoleGrowthRow> roleGrowthRows;
            ValidateRoleGrowth(tables, tableErrors, report, out roleGrowthRows);

            Dictionary<string, RoleBaseRow> roleBaseRows;
            ValidateRoleBase(
                tables,
                tableErrors,
                finalRows,
                roleGrowthRows,
                report,
                out roleBaseRows);

            Dictionary<int, LevelRuleRow> levelRows;
            ValidateLevelRule(tables, tableErrors, report, out levelRows);

            Dictionary<string, CostRuleRow> costRows;
            Dictionary<string, SummaryRow> summaryRows;
            ValidateCostAndSummary(
                tables,
                tableErrors,
                finalRows,
                report,
                out costRows,
                out summaryRows);

            Dictionary<string, string> policies;
            ValidatePolicy(tables, tableErrors, report, out policies);
            ValidateGachaUpgradeType(tables, tableErrors, report);
            ValidateCrossReferences(
                finalRows,
                skillIds,
                roleBaseRows,
                roleGrowthRows,
                costRows,
                summaryRows,
                report);

            if (report.ErrorCount == 0)
            {
                snapshot = TryCreateSnapshot(
                    selectedFolder,
                    gitInfo,
                    OfficialFiles,
                    matchedFiles,
                    tables,
                    report);
            }

            diagnostics = CreateReadOnlyDiagnostics(report);
            if (report.ErrorCount != 0 || snapshot == null)
            {
                snapshot = null;
                return false;
            }

            return true;
        }

        internal static bool TryBuildV8ReadOnlySnapshot(
            string selectedFolder,
            out StaffOfficialDataPackageSnapshot snapshot,
            out IReadOnlyList<string> diagnostics)
        {
            snapshot = null;
            ValidationReport report = new ValidationReport();
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                report.Error("V8 공식 데이터 폴더가 선택되지 않았습니다.");
                diagnostics = CreateReadOnlyDiagnostics(report);
                return false;
            }

            GitInfo gitInfo = ReadGitInfo();
            if (!gitInfo.BranchAvailable)
            {
                report.Error("현재 Git 브랜치를 읽지 못했습니다: " + gitInfo.ErrorMessage);
            }
            else if (!string.Equals(gitInfo.Branch, ExpectedBranch, StringComparison.Ordinal))
            {
                report.Error(
                    "현재 Git 브랜치가 지정 브랜치와 다릅니다. 현재: " + gitInfo.Branch
                    + ", 지정: " + ExpectedBranch);
            }

            if (!gitInfo.HeadAvailable)
            {
                report.Error("현재 Git HEAD를 읽지 못했습니다: " + gitInfo.ErrorMessage);
            }

            Dictionary<string, MatchedFile> matchedFiles =
                new Dictionary<string, MatchedFile>(StringComparer.Ordinal);
            MatchOfficialFiles(
                selectedFolder,
                V8OfficialFiles,
                true,
                matchedFiles,
                report);
            BuildHashResult(V8OfficialFiles, matchedFiles, report);
            ValidateEncodings(V8OfficialFiles, matchedFiles, report);
            ValidatePhysicalLineCounts(V8OfficialFiles, matchedFiles, report);

            Dictionary<string, CsvTable> tables =
                new Dictionary<string, CsvTable>(StringComparer.Ordinal);
            Dictionary<string, string> tableErrors =
                new Dictionary<string, string>(StringComparer.Ordinal);
            BuildCsvTables(V8OfficialFiles, matchedFiles, tables, tableErrors);

            List<FinalStaffRow> finalRows;
            ValidateFinalStaffTable(
                StaffOfficialDataPackageKeys.FinalStaff,
                "Final18 StaffData",
                "Final18 StaffData",
                FinalStaffHeaders,
                true,
                tables,
                tableErrors,
                report,
                out finalRows);

            HashSet<string> skillIds;
            ValidateSkillTypes(
                tables,
                tableErrors,
                finalRows,
                "Final18 StaffData",
                report,
                out skillIds);

            Dictionary<string, RoleGrowthRow> roleGrowthRows;
            ValidateRoleGrowth(tables, tableErrors, report, out roleGrowthRows);

            Dictionary<string, RoleBaseRow> roleBaseRows;
            ValidateRoleBase(
                tables,
                tableErrors,
                finalRows,
                roleGrowthRows,
                "Final18 StaffData",
                report,
                out roleBaseRows);

            Dictionary<int, LevelRuleRow> levelRows;
            ValidateLevelRule(tables, tableErrors, report, out levelRows);

            Dictionary<string, CostRuleRow> costRows;
            Dictionary<string, SummaryRow> summaryRows;
            ValidateCostAndSummary(
                tables,
                tableErrors,
                finalRows,
                "Final18 StaffData",
                report,
                out costRows,
                out summaryRows);

            Dictionary<string, string> policies;
            ValidatePolicy(tables, tableErrors, report, out policies);
            ValidateGachaUpgradeType(tables, tableErrors, report);
            ValidateCrossReferences(
                finalRows,
                skillIds,
                roleBaseRows,
                roleGrowthRows,
                costRows,
                summaryRows,
                "Final18 StaffData",
                report);
            ValidateV8Semantics(tables, tableErrors, finalRows, roleBaseRows, report);

            if (report.ErrorCount == 0)
            {
                snapshot = TryCreateSnapshot(
                    selectedFolder,
                    gitInfo,
                    V8OfficialFiles,
                    matchedFiles,
                    tables,
                    report);
            }

            diagnostics = CreateReadOnlyDiagnostics(report);
            if (report.ErrorCount != 0 || snapshot == null)
            {
                snapshot = null;
                return false;
            }

            return true;
        }

        internal static bool TryBuildCanonicalV8ReadOnlySnapshot(
            out StaffOfficialDataPackageSnapshot snapshot,
            out IReadOnlyList<string> diagnostics)
        {
            snapshot = null;
            string activeFolder;
            StaffOfficialDataSourceKind sourceKind;
            string error;
            if (!StaffOfficialDataPathResolver.TryResolveActiveFolder(
                    out activeFolder,
                    out sourceKind,
                    out error))
            {
                diagnostics = new List<string>
                {
                    "ERROR: V8 Active Folder를 해석하지 못했습니다: " + error
                }.AsReadOnly();
                return false;
            }

            IReadOnlyList<string> buildDiagnostics;
            bool built = TryBuildV8ReadOnlySnapshot(
                activeFolder,
                out snapshot,
                out buildDiagnostics);
            List<string> combinedDiagnostics = new List<string>();
            if (sourceKind == StaffOfficialDataSourceKind.SessionOverride)
            {
                combinedDiagnostics.Add(
                    "WARNING: NON_CANONICAL_OVERRIDE - Session Override 폴더를 사용했습니다: "
                    + activeFolder);
            }

            if (buildDiagnostics != null)
            {
                for (int index = 0; index < buildDiagnostics.Count; index++)
                {
                    combinedDiagnostics.Add(buildDiagnostics[index]);
                }
            }

            diagnostics = combinedDiagnostics.AsReadOnly();
            return built;
        }

        private static StaffOfficialDataPackageSnapshot TryCreateSnapshot(
            string selectedFolder,
            GitInfo gitInfo,
            OfficialFileSpec[] officialFiles,
            Dictionary<string, MatchedFile> matchedFiles,
            Dictionary<string, CsvTable> tables,
            ValidationReport report)
        {
            try
            {
                List<StaffOfficialFileSnapshot> files = new List<StaffOfficialFileSnapshot>();
                for (int index = 0; index < officialFiles.Length; index++)
                {
                    OfficialFileSpec spec = officialFiles[index];
                    MatchedFile matchedFile;
                    CsvTable table;
                    if (!matchedFiles.TryGetValue(spec.Key, out matchedFile)
                        || !tables.TryGetValue(spec.Key, out table)
                        || matchedFile.DecodedText == null)
                    {
                        report.Error(spec.DisplayName + ": 완전한 Snapshot을 만들 수 없습니다.");
                        return null;
                    }

                    files.Add(
                        new StaffOfficialFileSnapshot(
                            spec.Key,
                            spec.DisplayName,
                            matchedFile.Path,
                            matchedFile.Hash,
                            GetEncodingLabel(spec.Encoding),
                            spec.PhysicalLineCount,
                            CountPhysicalLines(matchedFile.DecodedText),
                            table.Headers,
                            table.Rows));
                }

                return new StaffOfficialDataPackageSnapshot(
                    Path.GetFullPath(selectedFolder),
                    gitInfo.Branch,
                    gitInfo.Head,
                    files);
            }
            catch (Exception exception)
            {
                report.Error("공식 데이터 Snapshot 생성에 실패했습니다: " + exception.Message);
                return null;
            }
        }

        private static CheckResult ValidateV8Semantics(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            Dictionary<string, RoleBaseRow> roleBaseRows,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            CsvTable skillTypeTable;
            if (!TryGetTable(
                    StaffOfficialDataPackageKeys.SkillType,
                    "Final02 StaffSkillType",
                    tables,
                    tableErrors,
                    result,
                    report,
                    out skillTypeTable))
            {
                return result;
            }

            Dictionary<string, string> skillDescriptions =
                new Dictionary<string, string>(StringComparer.Ordinal);
            for (int rowIndex = 0; rowIndex < skillTypeTable.Rows.Count; rowIndex++)
            {
                List<string> row = skillTypeTable.Rows[rowIndex];
                string id = skillTypeTable.Get(row, "스킬 TYPE ID").Trim();
                if (!skillDescriptions.ContainsKey(id))
                {
                    skillDescriptions.Add(id, skillTypeTable.Get(row, "스킬").Trim());
                }
            }

            ValidateV8SkillDefinition(
                skillDescriptions,
                "STAFF_SKILL03",
                "(0.5)초마다 손님 호출 버튼 누름",
                result,
                report);
            ValidateV8SkillDefinition(
                skillDescriptions,
                "STAFF_SKILL04",
                "맡은 주방 음식 제작 속도 (250%) 증가",
                result,
                report);
            ValidateV8SkillDefinition(
                skillDescriptions,
                "STAFF_SKILL09",
                "전체 주방 남은 음식 조리시간 50% 감소!",
                result,
                report);

            string[] expectedSkill03Staff =
            {
                "STAFF11", "STAFF12", "STAFF14", "STAFF41", "STAFF42",
                "STAFF43", "STAFF44", "STAFF56", "STAFF57", "STAFF66",
                "STAFF67", "STAFF76", "STAFF77", "STAFF91", "STAFF92"
            };
            string[] expectedSkill04Staff =
            {
                "STAFF17", "STAFF19", "STAFF20", "STAFF29", "STAFF46",
                "STAFF47", "STAFF48", "STAFF59", "STAFF60", "STAFF70",
                "STAFF79", "STAFF87", "STAFF88"
            };
            string[] expectedSkill09Staff = { "STAFF27", "STAFF68" };

            List<FinalStaffRow> skill03Rows = finalRows
                .Where(row => row.SkillId == "STAFF_SKILL03")
                .OrderBy(row => row.Id, StringComparer.Ordinal)
                .ToList();
            List<FinalStaffRow> skill04Rows = finalRows
                .Where(row => row.SkillId == "STAFF_SKILL04")
                .OrderBy(row => row.Id, StringComparer.Ordinal)
                .ToList();
            List<FinalStaffRow> skill09Rows = finalRows
                .Where(row => row.SkillId == "STAFF_SKILL09")
                .OrderBy(row => row.Id, StringComparer.Ordinal)
                .ToList();

            ValidateExactStaffSet(
                "STAFF_SKILL03",
                skill03Rows,
                expectedSkill03Staff,
                result,
                report);
            ValidateExactStaffSet(
                "STAFF_SKILL04",
                skill04Rows,
                expectedSkill04Staff,
                result,
                report);
            ValidateExactStaffSet(
                "STAFF_SKILL09",
                skill09Rows,
                expectedSkill09Staff,
                result,
                report);

            for (int index = 0; index < skill03Rows.Count; index++)
            {
                if (!string.Equals(
                        skill03Rows[index].SkillDescription,
                        "손님 빠르게 모으기!",
                        StringComparison.Ordinal))
                {
                    result.Fail(
                        report,
                        skill03Rows[index].Id + " STAFF_SKILL03 사용자 설명이 다릅니다.");
                }
            }

            for (int index = 0; index < skill04Rows.Count; index++)
            {
                FinalStaffRow row = skill04Rows[index];
                if (row.RoleKey != "CHEF")
                {
                    result.Fail(report, row.Id + " STAFF_SKILL04 역할이 CHEF가 아닙니다.");
                }

                if (!string.Equals(
                        row.SkillDescription,
                        "담당 음식 제작 속도 증가!",
                        StringComparison.Ordinal))
                {
                    result.Fail(report, row.Id + " STAFF_SKILL04 사용자 설명이 다릅니다.");
                }

                if (row.SkillDescription.IndexOf("250%", StringComparison.Ordinal) >= 0
                    || row.SkillDescription.IndexOf("3.5", StringComparison.Ordinal) >= 0
                    || row.SkillDescription.IndexOf("350%", StringComparison.Ordinal) >= 0)
                {
                    result.Fail(report, row.Id + " STAFF_SKILL04 사용자 설명에 금지된 수치 문자열이 있습니다.");
                }
            }

            for (int index = 0; index < skill09Rows.Count; index++)
            {
                FinalStaffRow row = skill09Rows[index];
                if (!string.Equals(
                        row.SkillDescription,
                        "전체 음식 조리시간 단축!",
                        StringComparison.Ordinal)
                    || !Approximately(row.Duration, 1d)
                    || !Approximately(row.Cooldown, 240d))
                {
                    result.Fail(
                        report,
                        row.Id + " STAFF_SKILL09 설명 또는 Duration 1초 / Cooldown 240초가 다릅니다.");
                }
            }

            Dictionary<string, double> expectedChefPassive =
                new Dictionary<string, double>(StringComparer.Ordinal)
                {
                    { "NORMAL", 50d },
                    { "RARE", 70d },
                    { "UNIQUE", 100d },
                    { "SPECIAL", 200d }
                };
            Dictionary<string, int> expectedChefCounts =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    { "NORMAL", 5 },
                    { "RARE", 7 },
                    { "UNIQUE", 9 },
                    { "SPECIAL", 2 }
                };
            Dictionary<string, int> actualChefCounts = expectedChefCounts.Keys.ToDictionary(
                grade => grade,
                grade => 0,
                StringComparer.Ordinal);

            foreach (KeyValuePair<string, double> expected in expectedChefPassive)
            {
                string key = BuildRoleBaseKey("CHEF", expected.Key, "COOKING_EFFICIENCY");
                RoleBaseRow roleBase;
                if (!roleBaseRows.TryGetValue(key, out roleBase)
                    || !Approximately(roleBase.BaseValue, expected.Value))
                {
                    result.Fail(
                        report,
                        "CHEF_PASSIVE_ROLEBASE_MISMATCH: " + key + "는 "
                        + expected.Value.ToString(CultureInfo.InvariantCulture) + "이어야 합니다.");
                }
            }

            for (int index = 0; index < finalRows.Count; index++)
            {
                FinalStaffRow row = finalRows[index];
                if (row.RoleKey != "CHEF")
                {
                    continue;
                }

                if (actualChefCounts.ContainsKey(row.GradeKey))
                {
                    actualChefCounts[row.GradeKey]++;
                }

                double expectedValue;
                if (expectedChefPassive.TryGetValue(row.GradeKey, out expectedValue))
                {
                    string expectedPassive =
                        "주방에서 맡은 음식 제작 시간 속도 ("
                        + expectedValue.ToString("0", CultureInfo.InvariantCulture)
                        + "%) 효율 증가";
                    if (!string.Equals(row.Passive, expectedPassive, StringComparison.Ordinal))
                    {
                        result.Fail(
                            report,
                            "CHEF_PASSIVE_ROLEBASE_MISMATCH: " + row.Id
                            + " 패시브 설명이 등급 RoleBase와 다릅니다.");
                    }
                }
            }

            foreach (KeyValuePair<string, int> expected in expectedChefCounts)
            {
                if (actualChefCounts[expected.Key] != expected.Value)
                {
                    result.Fail(
                        report,
                        "CHEF_PASSIVE_ROLEBASE_MISMATCH: CHEF " + expected.Key
                        + " 인원은 " + expected.Value + "명이어야 하지만 실제 "
                        + actualChefCounts[expected.Key] + "명입니다.");
                }
            }

            result.AddDetail("- SKILL03_UNCHANGED_PASS");
            result.AddDetail("- STAFF_SKILL04 추가 250% / 최종 3.5배 의미 잠금");
            result.AddDetail("- STAFF_SKILL09 STAFF27·STAFF68 / 1초 / 240초 잠금");
            result.AddDetail("- CHEF Passive RoleBase 50 / 70 / 100 / 200 잠금");
            return result;
        }

        private static void ValidateV8SkillDefinition(
            Dictionary<string, string> skillDescriptions,
            string skillId,
            string expectedDescription,
            CheckResult result,
            ValidationReport report)
        {
            string actualDescription;
            if (!skillDescriptions.TryGetValue(skillId, out actualDescription)
                || !string.Equals(actualDescription, expectedDescription, StringComparison.Ordinal))
            {
                result.Fail(
                    report,
                    skillId + " SkillType 설명이 다릅니다. 예상 '" + expectedDescription
                    + "', 실제 '" + (actualDescription ?? string.Empty) + "'");
            }
        }

        private static void ValidateExactStaffSet(
            string skillId,
            List<FinalStaffRow> actualRows,
            string[] expectedIds,
            CheckResult result,
            ValidationReport report)
        {
            string[] actualIds = actualRows.Select(row => row.Id).ToArray();
            string[] orderedExpectedIds = expectedIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (!actualIds.SequenceEqual(orderedExpectedIds, StringComparer.Ordinal))
            {
                result.Fail(
                    report,
                    skillId + " 대상 직원이 다릅니다. 예상: "
                    + string.Join(", ", orderedExpectedIds) + ", 실제: "
                    + string.Join(", ", actualIds));
            }
        }

        private static IReadOnlyList<string> CreateReadOnlyDiagnostics(ValidationReport report)
        {
            List<string> diagnosticCopy = new List<string>();
            for (int index = 0; index < report.Warnings.Count; index++)
            {
                diagnosticCopy.Add("WARNING: " + report.Warnings[index]);
            }

            for (int index = 0; index < report.Errors.Count; index++)
            {
                diagnosticCopy.Add("ERROR: " + report.Errors[index]);
            }

            return diagnosticCopy.AsReadOnly();
        }

        private static CheckResult MatchOfficialFiles(
            string selectedFolder,
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            return MatchOfficialFiles(
                selectedFolder,
                OfficialFiles,
                false,
                matchedFiles,
                report);
        }

        private static CheckResult MatchOfficialFiles(
            string selectedFolder,
            OfficialFileSpec[] officialFiles,
            bool rejectAdditionalCsv,
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            string[] csvPaths;

            try
            {
                csvPaths = Directory.GetFiles(selectedFolder, "*", SearchOption.AllDirectories)
                    .Where(path => string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception)
            {
                result.Fail(report, "선택한 폴더의 CSV 목록을 읽지 못했습니다: " + exception.Message);
                return result;
            }

            Dictionary<string, OfficialFileSpec> specByHash = officialFiles.ToDictionary(
                spec => spec.Hash,
                spec => spec,
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<ScannedCsv>> candidatesByKey = new Dictionary<string, List<ScannedCsv>>(
                StringComparer.Ordinal);

            for (int i = 0; i < officialFiles.Length; i++)
            {
                candidatesByKey.Add(officialFiles[i].Key, new List<ScannedCsv>());
            }

            List<string> ignoredPaths = new List<string>();
            for (int i = 0; i < csvPaths.Length; i++)
            {
                string path = csvPaths[i];
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    string hash = ComputeSha256(bytes);
                    OfficialFileSpec spec;
                    if (specByHash.TryGetValue(hash, out spec))
                    {
                        candidatesByKey[spec.Key].Add(new ScannedCsv(path, hash, bytes));
                    }
                    else
                    {
                        ignoredPaths.Add(path);
                    }
                }
                catch (Exception exception)
                {
                    result.Fail(report, "CSV를 읽거나 SHA-256을 계산하지 못했습니다: " + path + " (" + exception.Message + ")");
                }
            }

            for (int i = 0; i < officialFiles.Length; i++)
            {
                OfficialFileSpec spec = officialFiles[i];
                List<ScannedCsv> candidates = candidatesByKey[spec.Key];
                if (candidates.Count == 0)
                {
                    result.Fail(report, spec.DisplayName + ": 공식 SHA-256과 일치하는 CSV가 없습니다.");
                    continue;
                }

                if (candidates.Count > 1)
                {
                    string duplicatePaths = string.Join(", ", candidates.Select(candidate => candidate.Path).ToArray());
                    result.Fail(
                        report,
                        spec.DisplayName + ": 같은 공식 해시의 CSV가 " + candidates.Count
                        + "개 존재합니다. " + duplicatePaths);
                    continue;
                }

                ScannedCsv scanned = candidates[0];
                matchedFiles.Add(spec.Key, new MatchedFile(spec, scanned.Path, scanned.Hash, scanned.Bytes));
                result.AddDetail("- " + spec.DisplayName + ": PASS - " + scanned.Path);
            }

            if (ignoredPaths.Count == 0)
            {
                result.AddDetail("- 공식 " + officialFiles.Length + "개 외 CSV: 없음");
            }
            else if (rejectAdditionalCsv)
            {
                result.Fail(
                    report,
                    "V8 Strict Package에 공식 " + officialFiles.Length
                    + "개 외 CSV " + ignoredPaths.Count + "개가 존재합니다.");
                result.AddDetail(
                    "- 공식 " + officialFiles.Length + "개 외 CSV(허용되지 않음): "
                    + ignoredPaths.Count + "개");
                for (int i = 0; i < ignoredPaths.Count; i++)
                {
                    result.AddDetail("  · " + ignoredPaths[i]);
                }
            }
            else
            {
                report.Warning("공식 " + officialFiles.Length + "개 외 CSV " + ignoredPaths.Count + "개를 무시했습니다.");
                result.AddDetail(
                    "- 공식 " + officialFiles.Length + "개 외 CSV(검증에서 무시): " + ignoredPaths.Count + "개");
                for (int i = 0; i < ignoredPaths.Count; i++)
                {
                    result.AddDetail("  · " + ignoredPaths[i]);
                }
            }

            return result;
        }

        private static CheckResult BuildHashResult(
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            return BuildHashResult(OfficialFiles, matchedFiles, report);
        }

        private static CheckResult BuildHashResult(
            OfficialFileSpec[] officialFiles,
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            for (int i = 0; i < officialFiles.Length; i++)
            {
                OfficialFileSpec spec = officialFiles[i];
                MatchedFile file;
                if (!matchedFiles.TryGetValue(spec.Key, out file))
                {
                    result.Fail(report, spec.DisplayName + ": SHA-256 검증 대상 파일이 없습니다.");
                    continue;
                }

                bool passed = string.Equals(file.Hash, spec.Hash, StringComparison.OrdinalIgnoreCase);
                if (!passed)
                {
                    result.Fail(report, spec.DisplayName + ": SHA-256이 공식 잠금값과 다릅니다.");
                }

                result.AddDetail(
                    "- " + spec.DisplayName + ": " + (passed ? "PASS" : "FAIL") + " - " + file.Hash);
            }

            return result;
        }

        private static CheckResult ValidateEncodings(
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            return ValidateEncodings(OfficialFiles, matchedFiles, report);
        }

        private static CheckResult ValidateEncodings(
            OfficialFileSpec[] officialFiles,
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            for (int i = 0; i < officialFiles.Length; i++)
            {
                OfficialFileSpec spec = officialFiles[i];
                MatchedFile file;
                if (!matchedFiles.TryGetValue(spec.Key, out file))
                {
                    result.Fail(report, spec.DisplayName + ": 인코딩 검증 대상 파일이 없습니다.");
                    continue;
                }

                string decodedText;
                string error;
                bool passed = TryDecode(file.Bytes, spec.Encoding, out decodedText, out error);
                if (!passed)
                {
                    result.Fail(report, spec.DisplayName + ": 인코딩 검증 실패 - " + error);
                }
                else
                {
                    file.DecodedText = decodedText;
                }

                result.AddDetail(
                    "- " + spec.DisplayName + ": " + (passed ? "PASS" : "FAIL")
                    + " - " + GetEncodingLabel(spec.Encoding));
            }

            return result;
        }

        private static CheckResult ValidatePhysicalLineCounts(
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            return ValidatePhysicalLineCounts(OfficialFiles, matchedFiles, report);
        }

        private static CheckResult ValidatePhysicalLineCounts(
            OfficialFileSpec[] officialFiles,
            Dictionary<string, MatchedFile> matchedFiles,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            for (int i = 0; i < officialFiles.Length; i++)
            {
                OfficialFileSpec spec = officialFiles[i];
                MatchedFile file;
                if (!matchedFiles.TryGetValue(spec.Key, out file) || file.DecodedText == null)
                {
                    result.Fail(report, spec.DisplayName + ": 행 수 검증에 사용할 텍스트가 없습니다.");
                    continue;
                }

                int lineCount = CountPhysicalLines(file.DecodedText);
                bool passed = lineCount == spec.PhysicalLineCount;
                if (!passed)
                {
                    result.Fail(
                        report,
                        spec.DisplayName + ": 물리 행 수가 다릅니다. 예상 " + spec.PhysicalLineCount
                        + ", 실제 " + lineCount);
                }

                result.AddDetail(
                    "- " + spec.DisplayName + ": " + (passed ? "PASS" : "FAIL")
                    + " - " + lineCount + "/" + spec.PhysicalLineCount + "행");
            }

            return result;
        }

        private static void BuildCsvTables(
            Dictionary<string, MatchedFile> matchedFiles,
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors)
        {
            BuildCsvTables(OfficialFiles, matchedFiles, tables, tableErrors);
        }

        private static void BuildCsvTables(
            OfficialFileSpec[] officialFiles,
            Dictionary<string, MatchedFile> matchedFiles,
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors)
        {
            for (int i = 0; i < officialFiles.Length; i++)
            {
                OfficialFileSpec spec = officialFiles[i];
                MatchedFile file;
                if (!matchedFiles.TryGetValue(spec.Key, out file) || file.DecodedText == null)
                {
                    tableErrors[spec.Key] = "공식 파일이 없거나 인코딩 검증을 통과하지 못했습니다.";
                    continue;
                }

                try
                {
                    tables[spec.Key] = CsvTable.Parse(file.DecodedText);
                }
                catch (Exception exception)
                {
                    tableErrors[spec.Key] = exception.Message;
                }
            }
        }

        private static CheckResult ValidateFinal17(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            ValidationReport report,
            out List<FinalStaffRow> finalRows)
        {
            return ValidateFinalStaffTable(
                "Final17",
                "Final17 StaffData",
                "Final17",
                Final17Headers,
                false,
                tables,
                tableErrors,
                report,
                out finalRows);
        }

        private static CheckResult ValidateFinalStaffTable(
            string tableKey,
            string displayName,
            string diagnosticName,
            string[] expectedHeaders,
            bool requireExactHeaderOrder,
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            ValidationReport report,
            out List<FinalStaffRow> finalRows)
        {
            CheckResult result = new CheckResult();
            finalRows = new List<FinalStaffRow>();
            CsvTable table;
            if (!TryGetTable(tableKey, displayName, tables, tableErrors, result, report, out table))
            {
                return result;
            }

            if (!RequireHeaders(table, expectedHeaders, diagnosticName, result, report))
            {
                return result;
            }

            if (requireExactHeaderOrder
                && !ValidateExactHeaders(table, expectedHeaders, diagnosticName, result, report))
            {
                return result;
            }

            ValidateColumnCounts(table, diagnosticName, result, report);
            if (table.Rows.Count != 92)
            {
                result.Fail(
                    report,
                    diagnosticName + " 데이터 행은 92행이어야 하지만 실제로는 "
                    + table.Rows.Count + "행입니다.");
            }

            Dictionary<string, int> idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                int physicalRow = rowIndex + 2;
                bool requiredValuesPresent = true;
                for (int headerIndex = 0; headerIndex < expectedHeaders.Length; headerIndex++)
                {
                    string header = expectedHeaders[headerIndex];
                    if (string.IsNullOrWhiteSpace(table.Get(row, header)))
                    {
                        result.Fail(
                            report,
                            diagnosticName + " " + physicalRow + "행의 필수값 '"
                            + header + "'이 비어 있습니다.");
                        requiredValuesPresent = false;
                    }
                }

                string id = table.Get(row, "ID").Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    int count;
                    idCounts.TryGetValue(id, out count);
                    idCounts[id] = count + 1;
                }

                string roleKey;
                string gradeKey;
                bool roleValid = TryMapRole(table.Get(row, "역할"), out roleKey);
                bool gradeValid = TryMapGrade(table.Get(row, "등급"), out gradeKey);
                if (!roleValid)
                {
                    result.Fail(
                        report,
                        diagnosticName + " " + physicalRow + "행의 역할을 해석할 수 없습니다: "
                        + table.Get(row, "역할"));
                }

                if (!gradeValid)
                {
                    result.Fail(
                        report,
                        diagnosticName + " " + physicalRow + "행의 등급을 해석할 수 없습니다: "
                        + table.Get(row, "등급"));
                }

                double gachaProbability;
                int rarityStars;
                double baseSpeed;
                double duration;
                double cooldown;
                int duplicateToken;
                int tokenPrice;
                bool numbersValid =
                    TryParseDouble(table.Get(row, "가챠 확률"), out gachaProbability)
                    & TryParseInt(table.Get(row, "희귀도(별)"), out rarityStars)
                    & TryParseDouble(table.Get(row, "기본속도"), out baseSpeed)
                    & TryParseSeconds(table.Get(row, "스킬 타임"), out duration)
                    & TryParseSeconds(table.Get(row, "쿨타임"), out cooldown)
                    & TryParseInt(table.Get(row, "중복시 지급 토큰"), out duplicateToken)
                    & TryParseInt(table.Get(row, "토큰 구매 가격"), out tokenPrice);

                if (!numbersValid)
                {
                    result.Fail(report, diagnosticName + " " + physicalRow + "행에 숫자로 읽을 수 없는 값이 있습니다.");
                    continue;
                }

                if (!IsFiniteNonNegative(gachaProbability)
                    || rarityStars < 0
                    || !IsFiniteNonNegative(baseSpeed)
                    || !IsFiniteNonNegative(duration)
                    || !IsFiniteNonNegative(cooldown)
                    || duplicateToken < 0
                    || tokenPrice < 0)
                {
                    result.Fail(report, diagnosticName + " " + physicalRow + "행에 음수 또는 유효하지 않은 숫자가 있습니다.");
                }

                if (requiredValuesPresent && roleValid && gradeValid)
                {
                    finalRows.Add(
                        new FinalStaffRow(
                            id,
                            roleKey,
                            gradeKey,
                            table.Get(row, "스킬").Trim(),
                            table.Get(row, "패시브").Trim(),
                            table.Get(row, "스킬 설명").Trim(),
                            baseSpeed,
                            duration,
                            cooldown));
                }
            }

            foreach (KeyValuePair<string, int> pair in idCounts)
            {
                if (pair.Value > 1)
                {
                    result.Fail(
                        report,
                        diagnosticName + " 직원 ID가 중복되었습니다: " + pair.Key
                        + " (" + pair.Value + "행)");
                }
            }

            for (int number = 1; number <= 92; number++)
            {
                string expectedId = "STAFF" + number.ToString("00", CultureInfo.InvariantCulture);
                int count;
                if (!idCounts.TryGetValue(expectedId, out count) || count != 1)
                {
                    result.Fail(report, diagnosticName + "에 " + expectedId + "가 정확히 1행 존재하지 않습니다.");
                }
            }

            foreach (string id in idCounts.Keys)
            {
                int number;
                if (id.Length != 7
                    || !id.StartsWith("STAFF", StringComparison.Ordinal)
                    || !int.TryParse(id.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture, out number)
                    || number < 1
                    || number > 92)
                {
                    result.Fail(report, diagnosticName + "에 예상 범위를 벗어난 직원 ID가 있습니다: " + id);
                }
            }

            result.AddDetail("- 데이터 행: " + table.Rows.Count + "/92");
            result.AddDetail("- STAFF01~STAFF92 연속성과 중복 ID 검사 완료");
            result.AddDetail("- 필수값·역할·등급·숫자 형식 검사 완료");
            return result;
        }

        private static CheckResult ValidateSkillTypes(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            ValidationReport report,
            out HashSet<string> skillIds)
        {
            return ValidateSkillTypes(
                tables,
                tableErrors,
                finalRows,
                "Final17",
                report,
                out skillIds);
        }

        private static CheckResult ValidateSkillTypes(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            string finalDataName,
            ValidationReport report,
            out HashSet<string> skillIds)
        {
            CheckResult result = new CheckResult();
            skillIds = new HashSet<string>(StringComparer.Ordinal);
            CsvTable table;
            if (!TryGetTable("SkillType", "StaffSkillType", tables, tableErrors, result, report, out table))
            {
                return result;
            }

            string[] headers = { "스킬 TYPE ID", "스킬" };
            if (!RequireHeaders(table, headers, "StaffSkillType", result, report))
            {
                return result;
            }

            ValidateColumnCounts(table, "StaffSkillType", result, report);
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, string> descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            int idLessRows = 0;

            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                string id = table.Get(row, "스킬 TYPE ID").Trim();
                string description = table.Get(row, "스킬").Trim();
                if (string.IsNullOrEmpty(id))
                {
                    idLessRows++;
                    result.Fail(report, "StaffSkillType " + (rowIndex + 2) + "행에 스킬 ID가 없습니다.");
                    continue;
                }

                if (string.IsNullOrEmpty(description))
                {
                    result.Fail(report, "StaffSkillType " + (rowIndex + 2) + "행의 스킬 설명이 비어 있습니다.");
                }

                int count;
                counts.TryGetValue(id, out count);
                counts[id] = count + 1;
                if (!descriptions.ContainsKey(id))
                {
                    descriptions.Add(id, description);
                }
                skillIds.Add(id);
            }

            for (int number = 1; number <= 10; number++)
            {
                string expectedId = "STAFF_SKILL" + number.ToString("00", CultureInfo.InvariantCulture);
                int count;
                counts.TryGetValue(expectedId, out count);
                if (count != 1)
                {
                    result.Fail(report, expectedId + " 정의는 정확히 1행이어야 하지만 실제로는 " + count + "행입니다.");
                }
            }

            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > 1)
                {
                    result.Fail(report, "StaffSkillType 스킬 ID가 중복되었습니다: " + pair.Key);
                }

                if (!IsOfficialSkillId(pair.Key))
                {
                    result.Fail(report, "StaffSkillType에 공식 범위를 벗어난 스킬 ID가 있습니다: " + pair.Key);
                }
            }

            string reservedDescription;
            if (!descriptions.TryGetValue("STAFF_SKILL02", out reservedDescription)
                || !string.Equals(
                    reservedDescription,
                    ReservedUnusedSlotADescription,
                    StringComparison.Ordinal))
            {
                result.Fail(
                    report,
                    "STAFF_SKILL02 설명은 '" + ReservedUnusedSlotADescription + "'이어야 합니다.");
            }

            if (!descriptions.TryGetValue("STAFF_SKILL07", out reservedDescription)
                || !string.Equals(
                    reservedDescription,
                    ReservedUnusedSlotBDescription,
                    StringComparison.Ordinal))
            {
                result.Fail(
                    report,
                    "STAFF_SKILL07 설명은 '" + ReservedUnusedSlotBDescription + "'이어야 합니다.");
            }

            Dictionary<string, int> expectedReferenceCounts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "STAFF_SKILL01", 34 },
                { "STAFF_SKILL02", 0 },
                { "STAFF_SKILL03", 15 },
                { "STAFF_SKILL04", 13 },
                { "STAFF_SKILL05", 10 },
                { "STAFF_SKILL06", 3 },
                { "STAFF_SKILL07", 0 },
                { "STAFF_SKILL08", 10 },
                { "STAFF_SKILL09", 2 },
                { "STAFF_SKILL10", 5 }
            };
            Dictionary<string, int> referenceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string skillId in expectedReferenceCounts.Keys)
            {
                referenceCounts.Add(skillId, 0);
            }

            int undefinedReferenceCount = 0;
            for (int i = 0; i < finalRows.Count; i++)
            {
                if (!skillIds.Contains(finalRows[i].SkillId))
                {
                    undefinedReferenceCount++;
                    result.Fail(
                        report,
                        finalRows[i].Id + "가 참조하는 스킬이 StaffSkillType에 없습니다: " + finalRows[i].SkillId);
                    continue;
                }

                int referenceCount;
                if (referenceCounts.TryGetValue(finalRows[i].SkillId, out referenceCount))
                {
                    referenceCounts[finalRows[i].SkillId] = referenceCount + 1;
                }
                else
                {
                    undefinedReferenceCount++;
                    result.Fail(
                        report,
                        finalRows[i].Id + "가 공식 범위를 벗어난 스킬을 참조합니다: " + finalRows[i].SkillId);
                }
            }

            foreach (KeyValuePair<string, int> pair in expectedReferenceCounts)
            {
                int actualReferenceCount = referenceCounts[pair.Key];
                if (actualReferenceCount != pair.Value)
                {
                    result.Fail(
                        report,
                        pair.Key + " " + finalDataName + " 참조 수가 다릅니다. 예상: " + pair.Value
                        + ", 실제: " + actualReferenceCount);
                }
            }

            int officialDefinitionCount = counts.Count(
                pair => IsOfficialSkillId(pair.Key) && pair.Value == 1);
            int usedSkillCount = referenceCounts.Values.Count(referenceCount => referenceCount > 0);
            int reservedSkillCount = referenceCounts.Values.Count(referenceCount => referenceCount == 0);
            int totalReferenceCount = referenceCounts.Values.Sum();
            if (officialDefinitionCount != 10)
            {
                result.Fail(
                    report,
                    "정식 Skill 정의 수가 다릅니다. 예상: 10, 실제: " + officialDefinitionCount);
            }

            if (usedSkillCount != 8 || reservedSkillCount != 2)
            {
                result.Fail(
                    report,
                    "Skill 사용 상태 수가 다릅니다. 사용 중 예상/실제: 8/" + usedSkillCount
                    + ", 예약 예상/실제: 2/" + reservedSkillCount);
            }

            if (totalReferenceCount != 92 || totalReferenceCount != finalRows.Count)
            {
                result.Fail(
                    report,
                    "전체 Staff Skill 참조 수가 다릅니다. 예상: 92, 실제: " + totalReferenceCount);
            }

            result.AddDetail("- STAFF_SKILL01~STAFF_SKILL10 각각 1행 확인");
            result.AddDetail("- ID 없는 데이터 행: " + idLessRows);
            result.AddDetail("- " + finalDataName + " 스킬 참조 " + finalRows.Count + "행 검사 완료");
            result.AddDetail("- 정식 Skill 정의: " + officialDefinitionCount + "개");
            result.AddDetail("- 사용 중 Skill: " + usedSkillCount + "개");
            result.AddDetail("- 예약 Skill: " + reservedSkillCount + "개");
            result.AddDetail(
                "- STAFF_SKILL02: " + ReservedUnusedSlotACode
                + " / 참조 " + referenceCounts["STAFF_SKILL02"] + "명");
            result.AddDetail(
                "- STAFF_SKILL07: " + ReservedUnusedSlotBCode
                + " / 참조 " + referenceCounts["STAFF_SKILL07"] + "명");
            result.AddDetail("- Skill 참조 전체: " + totalReferenceCount + "/92");
            result.AddDetail("- 누락·미정의 참조: " + undefinedReferenceCount);
            return result;
        }

        private static CheckResult ValidateRoleGrowth(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            ValidationReport report,
            out Dictionary<string, RoleGrowthRow> roleGrowthRows)
        {
            CheckResult result = new CheckResult();
            roleGrowthRows = new Dictionary<string, RoleGrowthRow>(StringComparer.Ordinal);
            CsvTable table;
            if (!TryGetTable("RoleGrowth", "StaffRoleGrowthRule", tables, tableErrors, result, report, out table))
            {
                return result;
            }

            string[] headers =
            {
                "역할",
                "역할 명(kor)",
                "업그레이드 타겟",
                "단위",
                "Lv.1 누적 보정값 (Lv1_CumulativeDelta)",
                "Lv.2 누적 보정값 (Lv2_CumulativeDelta)",
                "Lv.3 누적 보정값 (Lv3_CumulativeDelta)",
                "Lv.4 누적 보정값 (Lv4_CumulativeDelta)",
                "Lv.5 누적 보정값 (Lv5_CumulativeDelta)",
                "계산 타입",
                "설명"
            };
            if (!RequireHeaders(table, headers, "StaffRoleGrowthRule", result, report))
            {
                return result;
            }

            ValidateColumnCounts(table, "StaffRoleGrowthRule", result, report);
            if (table.Rows.Count != 6)
            {
                result.Fail(report, "StaffRoleGrowthRule은 6개 역할 행이어야 하지만 실제로는 " + table.Rows.Count + "행입니다.");
            }

            string[] deltaHeaders =
            {
                headers[4], headers[5], headers[6], headers[7], headers[8]
            };

            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                string role = table.Get(row, "역할").Trim();
                string unit = table.Get(row, "단위").Trim();
                string calculationType = table.Get(row, "계산 타입").Trim();
                if (string.IsNullOrEmpty(role))
                {
                    result.Fail(report, "StaffRoleGrowthRule " + (rowIndex + 2) + "행의 역할이 비어 있습니다.");
                    continue;
                }

                if (!RoleKeys.Contains(role))
                {
                    result.Fail(report, "StaffRoleGrowthRule에 알 수 없는 역할이 있습니다: " + role);
                }

                if (roleGrowthRows.ContainsKey(role))
                {
                    result.Fail(report, "StaffRoleGrowthRule 역할이 중복되었습니다: " + role);
                    continue;
                }

                if (unit != unit.ToUpperInvariant())
                {
                    result.Fail(report, "StaffRoleGrowthRule " + role + "의 단위가 대문자가 아닙니다: " + unit);
                }

                if (!string.Equals(calculationType, "ADD", StringComparison.Ordinal))
                {
                    result.Fail(report, "StaffRoleGrowthRule " + role + "의 계산 타입이 ADD가 아닙니다: " + calculationType);
                }

                double[] deltas = new double[5];
                bool valuesValid = true;
                for (int levelIndex = 0; levelIndex < deltaHeaders.Length; levelIndex++)
                {
                    if (!TryParseDouble(table.Get(row, deltaHeaders[levelIndex]), out deltas[levelIndex]))
                    {
                        valuesValid = false;
                        result.Fail(
                            report,
                            "StaffRoleGrowthRule " + role + "의 Lv." + (levelIndex + 1)
                            + " 누적 보정값을 숫자로 읽을 수 없습니다.");
                    }
                }

                if (valuesValid)
                {
                    roleGrowthRows.Add(role, new RoleGrowthRow(role, unit, deltas));
                }
            }

            for (int i = 0; i < RoleKeys.Length; i++)
            {
                if (!roleGrowthRows.ContainsKey(RoleKeys[i]))
                {
                    result.Fail(report, "StaffRoleGrowthRule에 역할이 없습니다: " + RoleKeys[i]);
                }
            }

            result.AddDetail("- 6개 역할과 중복 여부 검사 완료");
            result.AddDetail("- 단위 대문자 검사 완료");
            result.AddDetail("- Lv.1 기준 누적 보정값을 각 레벨 열에서 직접 읽음");
            return result;
        }

        private static CheckResult ValidateRoleBase(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            Dictionary<string, RoleGrowthRow> roleGrowthRows,
            ValidationReport report,
            out Dictionary<string, RoleBaseRow> roleBaseRows)
        {
            return ValidateRoleBase(
                tables,
                tableErrors,
                finalRows,
                roleGrowthRows,
                "Final17",
                report,
                out roleBaseRows);
        }

        private static CheckResult ValidateRoleBase(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            Dictionary<string, RoleGrowthRow> roleGrowthRows,
            string finalDataName,
            ValidationReport report,
            out Dictionary<string, RoleBaseRow> roleBaseRows)
        {
            CheckResult result = new CheckResult();
            roleBaseRows = new Dictionary<string, RoleBaseRow>(StringComparer.Ordinal);
            CsvTable table;
            if (!TryGetTable("RoleBase", "StaffRoleBaseRule", tables, tableErrors, result, report, out table))
            {
                return result;
            }

            string[] headers = { "RoleKey", "GradeKey", "StatKey", "BaseValue", "Unit", "MinimumValue", "Note" };
            if (!RequireHeaders(table, headers, "StaffRoleBaseRule", result, report))
            {
                return result;
            }

            ValidateColumnCounts(table, "StaffRoleBaseRule", result, report);
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                string role = table.Get(row, "RoleKey").Trim();
                string grade = table.Get(row, "GradeKey").Trim();
                string stat = table.Get(row, "StatKey").Trim();
                string unit = table.Get(row, "Unit").Trim();
                double baseValue;
                double minimumValue;

                if (!RoleKeys.Contains(role))
                {
                    result.Fail(report, "StaffRoleBaseRule에 알 수 없는 역할이 있습니다: " + role);
                }

                if (!GradeKeys.Contains(grade))
                {
                    result.Fail(report, "StaffRoleBaseRule에 알 수 없는 등급이 있습니다: " + grade);
                }

                if (string.IsNullOrEmpty(stat))
                {
                    result.Fail(report, "StaffRoleBaseRule " + (rowIndex + 2) + "행의 StatKey가 비어 있습니다.");
                }

                if (unit != unit.ToUpperInvariant())
                {
                    result.Fail(report, "StaffRoleBaseRule " + role + "/" + grade + "/" + stat + "의 단위가 대문자가 아닙니다.");
                }

                if (!TryParseDouble(table.Get(row, "BaseValue"), out baseValue)
                    || !TryParseDouble(table.Get(row, "MinimumValue"), out minimumValue))
                {
                    result.Fail(report, "StaffRoleBaseRule " + role + "/" + grade + "/" + stat + " 값을 숫자로 읽을 수 없습니다.");
                    continue;
                }

                string key = BuildRoleBaseKey(role, grade, stat);
                if (roleBaseRows.ContainsKey(key))
                {
                    result.Fail(report, "StaffRoleBaseRule 키가 중복되었습니다: " + key);
                    continue;
                }

                roleBaseRows.Add(key, new RoleBaseRow(role, grade, stat, baseValue, unit, minimumValue));
            }

            HashSet<string> roleGradePairs = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < finalRows.Count; i++)
            {
                FinalStaffRow staff = finalRows[i];
                string pair = staff.RoleKey + "|" + staff.GradeKey;
                roleGradePairs.Add(pair);
                string[] requiredStats = GetRequiredStats(staff.RoleKey);
                for (int statIndex = 0; statIndex < requiredStats.Length; statIndex++)
                {
                    string key = BuildRoleBaseKey(staff.RoleKey, staff.GradeKey, requiredStats[statIndex]);
                    if (!roleBaseRows.ContainsKey(key))
                    {
                        result.Fail(report, finalDataName + " 역할·등급에 필요한 RoleBase가 없습니다: " + key);
                    }
                }

                string moveKey = BuildRoleBaseKey(staff.RoleKey, staff.GradeKey, "MOVE_SPEED");
                RoleBaseRow moveRow;
                if (roleBaseRows.TryGetValue(moveKey, out moveRow))
                {
                    if (!Approximately(staff.BaseSpeed, moveRow.BaseValue))
                    {
                        result.Fail(
                            report,
                            staff.Id + " 기본속도 " + staff.BaseSpeed.ToString(CultureInfo.InvariantCulture)
                            + "가 RoleBase " + moveRow.BaseValue.ToString(CultureInfo.InvariantCulture) + "와 다릅니다.");
                    }
                }
                else if ((staff.RoleKey == "MANAGER" || staff.RoleKey == "CHEERLEADER")
                         && !Approximately(staff.BaseSpeed, 0d))
                {
                    result.Fail(report, staff.Id + "는 고정 배치 역할이지만 기본속도가 0이 아닙니다.");
                }
            }

            foreach (RoleBaseRow row in roleBaseRows.Values)
            {
                if (row.Role == "GUARD" && row.Stat == "MOVE_SPEED" && !Approximately(row.BaseValue, 0d))
                {
                    result.Fail(report, "가드 MOVE_SPEED는 0이어야 합니다: " + row.Grade);
                }
            }

            Dictionary<string, double> expectedCleaningTimes = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                { "NORMAL", 1.0d },
                { "RARE", 0.8d },
                { "UNIQUE", 0.6d },
                { "SPECIAL", 0.4d }
            };
            foreach (KeyValuePair<string, double> pair in expectedCleaningTimes)
            {
                string key = BuildRoleBaseKey("CLEANER", pair.Key, "CLEANING_TIME");
                RoleBaseRow row;
                if (!roleBaseRows.TryGetValue(key, out row) || !Approximately(row.BaseValue, pair.Value))
                {
                    result.Fail(
                        report,
                        "청소부 " + pair.Key + " CLEANING_TIME은 "
                        + pair.Value.ToString(CultureInfo.InvariantCulture) + "이어야 합니다.");
                }
            }

            RoleGrowthRow managerGrowth;
            if (!roleGrowthRows.TryGetValue("MANAGER", out managerGrowth))
            {
                result.Fail(report, "매니저 Lv.5 최종값 계산에 필요한 RoleGrowth가 없습니다.");
            }
            else
            {
                foreach (string pair in roleGradePairs)
                {
                    string[] parts = pair.Split('|');
                    if (parts[0] != "MANAGER")
                    {
                        continue;
                    }

                    string key = BuildRoleBaseKey("MANAGER", parts[1], "GUIDE_TIME");
                    RoleBaseRow baseRow;
                    if (roleBaseRows.TryGetValue(key, out baseRow))
                    {
                        double finalValue = baseRow.BaseValue + managerGrowth.CumulativeDeltas[4];
                        if (finalValue < 0.4d - 0.000001d)
                        {
                            result.Fail(
                                report,
                                "매니저 " + parts[1] + " Lv.5 안내시간이 0.4초 미만입니다: "
                                + finalValue.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            result.AddDetail("- RoleKey+GradeKey+StatKey 중복 검사 완료");
            result.AddDetail("- " + finalDataName + " 역할·등급 필수 Stat 커버: " + roleGradePairs.Count + "개 조합");
            result.AddDetail("- 기본 이동속도·가드 0·청소시간·매니저 Lv.5 하한 검사 완료");
            return result;
        }

        private static CheckResult ValidateLevelRule(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            ValidationReport report,
            out Dictionary<int, LevelRuleRow> levelRows)
        {
            CheckResult result = new CheckResult();
            levelRows = new Dictionary<int, LevelRuleRow>();
            CsvTable table;
            if (!TryGetTable("LevelRule", "StaffSkillLevelRule", tables, tableErrors, result, report, out table))
            {
                return result;
            }

            string[] headers = { "강화 레벨", "스킬 지속시간 배율", "스킬 쿨타임 배율", "스킬 타임 규칙", "설명" };
            if (!RequireHeaders(table, headers, "StaffSkillLevelRule", result, report))
            {
                return result;
            }

            ValidateColumnCounts(table, "StaffSkillLevelRule", result, report);
            if (table.Rows.Count != 5)
            {
                result.Fail(report, "StaffSkillLevelRule은 정확히 5행이어야 하지만 실제로는 " + table.Rows.Count + "행입니다.");
            }

            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                int level;
                double durationMultiplier;
                double cooldownMultiplier;
                if (!TryParseInt(table.Get(row, "강화 레벨"), out level)
                    || !TryParseDouble(table.Get(row, "스킬 지속시간 배율"), out durationMultiplier)
                    || !TryParseDouble(table.Get(row, "스킬 쿨타임 배율"), out cooldownMultiplier))
                {
                    result.Fail(report, "StaffSkillLevelRule " + (rowIndex + 2) + "행의 숫자를 읽을 수 없습니다.");
                    continue;
                }

                if (levelRows.ContainsKey(level))
                {
                    result.Fail(report, "StaffSkillLevelRule 강화 레벨이 중복되었습니다: Lv." + level);
                    continue;
                }

                levelRows.Add(level, new LevelRuleRow(level, durationMultiplier, cooldownMultiplier));
            }

            for (int level = 1; level <= 5; level++)
            {
                if (!levelRows.ContainsKey(level))
                {
                    result.Fail(report, "StaffSkillLevelRule에 Lv." + level + " 행이 없습니다.");
                }
            }

            ValidateLevelValue(levelRows, 4, 1.25d, 0.97d, result, report);
            ValidateLevelValue(levelRows, 5, 1.50d, 0.92d, result, report);

            result.AddDetail("- Lv.1~Lv.5 정확히 5행 확인");
            result.AddDetail("- Lv.4 Duration 1.25 / Cooldown 0.97 확인");
            result.AddDetail("- Lv.5 Duration 1.50 / Cooldown 0.92를 Lv.5 행에서 직접 확인");
            return result;
        }

        private static CheckResult ValidateCostAndSummary(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            ValidationReport report,
            out Dictionary<string, CostRuleRow> costRows,
            out Dictionary<string, SummaryRow> summaryRows)
        {
            return ValidateCostAndSummary(
                tables,
                tableErrors,
                finalRows,
                "Final17",
                report,
                out costRows,
                out summaryRows);
        }

        private static CheckResult ValidateCostAndSummary(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            List<FinalStaffRow> finalRows,
            string finalDataName,
            ValidationReport report,
            out Dictionary<string, CostRuleRow> costRows,
            out Dictionary<string, SummaryRow> summaryRows)
        {
            CheckResult result = new CheckResult();
            costRows = new Dictionary<string, CostRuleRow>(StringComparer.Ordinal);
            summaryRows = new Dictionary<string, SummaryRow>(StringComparer.Ordinal);

            CsvTable costTable;
            CsvTable summaryTable;
            bool hasCost = TryGetTable("CostRule", "StaffUpgradeCostRule", tables, tableErrors, result, report, out costTable);
            bool hasSummary = TryGetTable("Summary", "StaffUpgradeSummary", tables, tableErrors, result, report, out summaryTable);
            if (!hasCost || !hasSummary)
            {
                return result;
            }

            string[] costHeaders =
            {
                "등급",
                "등급 명(kor)",
                "lv.1-lv.2_강화 수단",
                "lv.1-lv.2_강화 가격",
                "lv.2-lv.3_강화 수단",
                "lv.2-lv.3_강화 가격",
                "lv.3-lv.4_강화 수단",
                "lv.3-lv.4_강화 가격",
                "lv.4-lv.5_강화 수단",
                "lv.4-lv.5_강화 가격"
            };
            string[] summaryHeaders =
            {
                "등급",
                "등급 명(kor)",
                "lv4강화 까지 소비 전체 코인",
                "Max강화 소비 전체 다이아",
                "강화 전체 비용"
            };

            if (!RequireHeaders(costTable, costHeaders, "StaffUpgradeCostRule", result, report)
                || !RequireHeaders(summaryTable, summaryHeaders, "StaffUpgradeSummary", result, report))
            {
                return result;
            }

            ValidateColumnCounts(costTable, "StaffUpgradeCostRule", result, report);
            ValidateColumnCounts(summaryTable, "StaffUpgradeSummary", result, report);
            if (costTable.Rows.Count != 4)
            {
                result.Fail(report, "StaffUpgradeCostRule은 4개 등급 행이어야 합니다.");
            }

            if (summaryTable.Rows.Count != 4)
            {
                result.Fail(report, "StaffUpgradeSummary는 4개 등급 행이어야 합니다.");
            }

            for (int rowIndex = 0; rowIndex < costTable.Rows.Count; rowIndex++)
            {
                List<string> row = costTable.Rows[rowIndex];
                string grade = costTable.Get(row, "등급").Trim();
                if (!GradeKeys.Contains(grade))
                {
                    result.Fail(report, "StaffUpgradeCostRule에 알 수 없는 등급이 있습니다: " + grade);
                }

                if (costRows.ContainsKey(grade))
                {
                    result.Fail(report, "StaffUpgradeCostRule 등급이 중복되었습니다: " + grade);
                    continue;
                }

                string[] currencies = new string[4];
                int[] amounts = new int[4];
                bool valid = true;
                for (int step = 0; step < 4; step++)
                {
                    currencies[step] = costTable.Get(row, costHeaders[2 + step * 2]).Trim();
                    if (!TryParseInt(costTable.Get(row, costHeaders[3 + step * 2]), out amounts[step])
                        || amounts[step] < 0)
                    {
                        valid = false;
                        result.Fail(report, "StaffUpgradeCostRule " + grade + "의 " + (step + 1) + "단계 가격이 올바르지 않습니다.");
                    }

                    if (string.Equals(currencies[step], "PANDATOKEN", StringComparison.OrdinalIgnoreCase))
                    {
                        valid = false;
                        result.Fail(report, "StaffUpgradeCostRule 강화비용에 PandaToken이 사용되었습니다: " + grade);
                    }
                }

                for (int step = 0; step < 3; step++)
                {
                    if (!string.Equals(currencies[step], "COIN", StringComparison.Ordinal))
                    {
                        valid = false;
                        result.Fail(report, "StaffUpgradeCostRule " + grade + " Lv." + (step + 1) + "→" + (step + 2) + " 재화는 COIN이어야 합니다.");
                    }
                }

                if (!string.Equals(currencies[3], "DIAMOND", StringComparison.Ordinal))
                {
                    valid = false;
                    result.Fail(report, "StaffUpgradeCostRule " + grade + " Lv.4→5 재화는 DIAMOND여야 합니다.");
                }

                if (valid)
                {
                    costRows.Add(grade, new CostRuleRow(grade, currencies, amounts));
                }
            }

            for (int rowIndex = 0; rowIndex < summaryTable.Rows.Count; rowIndex++)
            {
                List<string> row = summaryTable.Rows[rowIndex];
                string grade = summaryTable.Get(row, "등급").Trim();
                int totalCoin;
                int totalDiamond;
                if (!TryParseInt(summaryTable.Get(row, "lv4강화 까지 소비 전체 코인"), out totalCoin)
                    || !TryParseInt(summaryTable.Get(row, "Max강화 소비 전체 다이아"), out totalDiamond))
                {
                    result.Fail(report, "StaffUpgradeSummary " + grade + " 합계를 숫자로 읽을 수 없습니다.");
                    continue;
                }

                if (summaryRows.ContainsKey(grade))
                {
                    result.Fail(report, "StaffUpgradeSummary 등급이 중복되었습니다: " + grade);
                    continue;
                }

                summaryRows.Add(grade, new SummaryRow(grade, totalCoin, totalDiamond));
            }

            for (int i = 0; i < GradeKeys.Length; i++)
            {
                string grade = GradeKeys[i];
                CostRuleRow cost;
                SummaryRow summary;
                if (!costRows.TryGetValue(grade, out cost))
                {
                    result.Fail(report, "StaffUpgradeCostRule에 등급이 없습니다: " + grade);
                    continue;
                }

                if (!summaryRows.TryGetValue(grade, out summary))
                {
                    result.Fail(report, "StaffUpgradeSummary에 등급이 없습니다: " + grade);
                    continue;
                }

                int totalCoin = cost.Amounts[0] + cost.Amounts[1] + cost.Amounts[2];
                int totalDiamond = cost.Amounts[3];
                if (totalCoin != summary.TotalCoin || totalDiamond != summary.TotalDiamond)
                {
                    result.Fail(
                        report,
                        grade + " 강화비용 합계가 Summary와 다릅니다. 계산 " + totalCoin + " COIN + "
                        + totalDiamond + " DIAMOND, Summary " + summary.TotalCoin + " COIN + "
                        + summary.TotalDiamond + " DIAMOND");
                }
            }

            HashSet<string> finalGrades = new HashSet<string>(finalRows.Select(row => row.GradeKey), StringComparer.Ordinal);
            foreach (string grade in finalGrades)
            {
                if (!costRows.ContainsKey(grade))
                {
                    result.Fail(report, finalDataName + " 등급을 CostRule에 매핑할 수 없습니다: " + grade);
                }
            }

            result.AddDetail("- 4개 등급·단계별 비용·Lv.4→5 DIAMOND 검사 완료");
            result.AddDetail("- PandaToken 강화비용 없음 확인");
            result.AddDetail("- CostRule 계산 합계와 Summary 교차검증 완료");
            return result;
        }

        private static CheckResult ValidatePolicy(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            ValidationReport report,
            out Dictionary<string, string> policies)
        {
            CheckResult result = new CheckResult();
            policies = new Dictionary<string, string>(StringComparer.Ordinal);
            CsvTable table;
            if (!TryGetTable("Policy", "StaffUpgradePolicy", tables, tableErrors, result, report, out table))
            {
                return result;
            }

            string[] headers = { "PolicyKey", "Value", "Note" };
            if (!RequireHeaders(table, headers, "StaffUpgradePolicy", result, report))
            {
                return result;
            }

            ValidateColumnCounts(table, "StaffUpgradePolicy", result, report);
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                string key = table.Get(row, "PolicyKey").Trim();
                string value = table.Get(row, "Value").Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                {
                    result.Fail(report, "StaffUpgradePolicy " + (rowIndex + 2) + "행의 PolicyKey 또는 Value가 비어 있습니다.");
                    continue;
                }

                if (policies.ContainsKey(key))
                {
                    result.Fail(report, "StaffUpgradePolicy PolicyKey가 중복되었습니다: " + key);
                    continue;
                }

                policies.Add(key, value);
            }

            Dictionary<string, string> expectedPolicies = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "MaxLevel", "5" },
                { "SkillUpgradeStartLevel", "4" },
                { "MaxUpgradeCurrency", "DIAMOND" },
                { "PandaTokenForUpgrade", "FALSE" },
                { "PandaTokenUseCase", "GACHA_DUPLICATE_AND_DIRECT_PURCHASE" },
                { "CooldownStartRule", "AFTER_SKILL_DURATION_END" },
                { "SpeedAffectsCooldown", "FALSE" },
                { "SkillTimeRoundRule", "ROUND_HALF_UP" },
                { "StaffSkinLegacyPolicy", "KEEP_BUT_NOT_USED" },
                { "SkillTimeBase", "STAFF_DATA_BASE_VALUE" },
                { "UpgradeScoreRequirementEnabled", "FALSE" },
                { "SkillDurationBonusStackRule", "ADDITIVE_BEFORE_ROUND" },
                { "ManagerGuideTimeMinimum", "0.4" }
            };

            foreach (KeyValuePair<string, string> expected in expectedPolicies)
            {
                string actual;
                if (!policies.TryGetValue(expected.Key, out actual))
                {
                    result.Fail(report, "StaffUpgradePolicy에 필수 정책이 없습니다: " + expected.Key);
                }
                else if (!string.Equals(actual, expected.Value, StringComparison.Ordinal))
                {
                    result.Fail(
                        report,
                        expected.Key + " 값이 다릅니다. 예상 " + expected.Value + ", 실제 " + actual);
                }
            }

            result.AddDetail("- PolicyKey 중복 검사 완료");
            result.AddDetail("- 필수 잠금 정책 " + expectedPolicies.Count + "개 값 확인");
            return result;
        }

        private static CheckResult ValidateGachaUpgradeType(
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            CsvTable table;
            if (!TryGetTable(
                    "GachaUpgradeType",
                    "Final03 GOTCHA UpgradeType",
                    tables,
                    tableErrors,
                    result,
                    report,
                    out table))
            {
                return result;
            }

            ValidateColumnCounts(table, "Final03 GOTCHA UpgradeType", result, report);

            bool headersValid = RequireHeaders(
                table,
                GachaUpgradeTypeHeaders,
                "Final03 GOTCHA UpgradeType",
                result,
                report);
            if (table.Headers.Count != GachaUpgradeTypeHeaders.Length)
            {
                result.Fail(
                    report,
                    "Final03 GOTCHA UpgradeType 헤더는 정확히 " + GachaUpgradeTypeHeaders.Length
                    + "개여야 하지만 실제로는 " + table.Headers.Count + "개입니다.");
                headersValid = false;
            }

            int comparableHeaderCount = Math.Min(table.Headers.Count, GachaUpgradeTypeHeaders.Length);
            for (int headerIndex = 0; headerIndex < comparableHeaderCount; headerIndex++)
            {
                string actualHeader = table.Headers[headerIndex] ?? string.Empty;
                string expectedHeader = GachaUpgradeTypeHeaders[headerIndex];
                if (!string.Equals(actualHeader, actualHeader.Trim(), StringComparison.Ordinal))
                {
                    result.Fail(
                        report,
                        "Final03 GOTCHA UpgradeType 헤더 " + (headerIndex + 1) + "에 앞뒤 공백이 있습니다: "
                        + actualHeader);
                    headersValid = false;
                }

                if (!string.Equals(actualHeader, expectedHeader, StringComparison.Ordinal))
                {
                    result.Fail(
                        report,
                        "Final03 GOTCHA UpgradeType 헤더 순서 또는 값이 다릅니다. 위치 " + (headerIndex + 1)
                        + ", 예상 '" + expectedHeader + "', 실제 '" + actualHeader + "'");
                    headersValid = false;
                }
            }

            if (!headersValid)
            {
                return result;
            }

            if (table.Rows.Count != 30)
            {
                result.Fail(
                    report,
                    "Final03 GOTCHA UpgradeType 데이터 행은 30행이어야 하지만 실제로는 "
                    + table.Rows.Count + "행입니다.");
            }

            Dictionary<string, int> idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                List<string> row = table.Rows[rowIndex];
                int physicalRow = rowIndex + 2;

                for (int cellIndex = 0; cellIndex < row.Count; cellIndex++)
                {
                    string cell = row[cellIndex] ?? string.Empty;
                    if (!string.Equals(cell, cell.Trim(), StringComparison.Ordinal))
                    {
                        string columnName = cellIndex < table.Headers.Count
                            ? table.Headers[cellIndex]
                            : "추가 열 " + (cellIndex + 1);
                        result.Fail(
                            report,
                            "Final03 GOTCHA UpgradeType " + physicalRow + "행 '" + columnName
                            + "' 셀에 앞뒤 공백이 있습니다.");
                    }
                }

                string id = table.Get(row, "강화 TYPE ID");
                string detail = table.Get(row, "강화 내용(숫자는 초록색으로 색 변경)");
                string summary = table.Get(row, "강화");
                string unit = table.Get(row, "단위");

                if (string.IsNullOrEmpty(id))
                {
                    result.Fail(report, "Final03 GOTCHA UpgradeType " + physicalRow + "행의 강화 TYPE ID가 비어 있습니다.");
                }
                else
                {
                    int existingCount;
                    idCounts.TryGetValue(id, out existingCount);
                    idCounts[id] = existingCount + 1;
                }

                if (string.IsNullOrEmpty(detail))
                {
                    result.Fail(report, "Final03 GOTCHA UpgradeType " + physicalRow + "행의 상세 설명이 비어 있습니다.");
                }

                if (string.IsNullOrEmpty(summary))
                {
                    result.Fail(report, "Final03 GOTCHA UpgradeType " + physicalRow + "행의 요약이 비어 있습니다.");
                }

                if (string.IsNullOrEmpty(unit))
                {
                    result.Fail(report, "Final03 GOTCHA UpgradeType " + physicalRow + "행의 단위가 비어 있습니다.");
                }

                if (unit.IndexOf('\'') >= 0 || unit.IndexOf('"') >= 0)
                {
                    result.Fail(
                        report,
                        "Final03 GOTCHA UpgradeType " + physicalRow
                        + "행의 단위 셀 값에 작은따옴표 또는 쌍따옴표 문자가 포함되어 있습니다: " + unit);
                }

                int upgradeNumber;
                if (!TryParseGachaUpgradeNumber(id, out upgradeNumber))
                {
                    result.Fail(
                        report,
                        "Final03 GOTCHA UpgradeType " + physicalRow
                        + "행의 ID 형식이 UPGRADE 두 자리 숫자 형식이 아닙니다: " + id);
                    continue;
                }

                if (upgradeNumber < 1 || upgradeNumber > 30)
                {
                    result.Fail(
                        report,
                        "Final03 GOTCHA UpgradeType에 허용 범위 UPGRADE01~UPGRADE30을 벗어난 ID가 있습니다: " + id);
                    continue;
                }

                string expectedUnit = GetExpectedGachaUpgradeUnit(upgradeNumber);
                if (!string.Equals(unit, expectedUnit, StringComparison.Ordinal))
                {
                    result.Fail(
                        report,
                        id + " 단위가 다릅니다. 예상 '" + expectedUnit + "', 실제 '" + unit + "'");
                }

                if (unit == "+sec" || unit == "+SEC" || unit == "+속도%")
                {
                    result.Fail(report, id + "에 과거의 잘못된 단위값이 존재합니다: " + unit);
                }

                GachaUpgradeExpectation expectation = FindGachaUpgradeExpectation(id);
                if (expectation != null)
                {
                    ValidateGachaUpgradeExpectation(
                        expectation,
                        detail,
                        summary,
                        unit,
                        result,
                        report);
                }

                if (id == "UPGRADE25")
                {
                    string allCells = string.Join("|", row.ToArray());
                    if (allCells.IndexOf("매니저", StringComparison.Ordinal) >= 0
                        || allCells.IndexOf("Manager", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Fail(report, "UPGRADE25에 과거 매니저 이동속도 연결 문구가 존재합니다.");
                    }
                }
                else if (id == "UPGRADE28")
                {
                    if (detail.IndexOf("가드 스텝 이동 속도", StringComparison.Ordinal) >= 0
                        || summary.IndexOf("가드 이동 속도 증가", StringComparison.Ordinal) >= 0
                        || unit.IndexOf("이동속도", StringComparison.Ordinal) >= 0)
                    {
                        result.Fail(report, "UPGRADE28에 과거 가드 이동속도 정의가 존재합니다.");
                    }
                }
                else if (id == "UPGRADE29")
                {
                    if (detail.IndexOf("피버 타임 n% 증가", StringComparison.Ordinal) >= 0
                        || unit.IndexOf("시간%증가", StringComparison.Ordinal) >= 0
                        || unit.IndexOf("속도%증가", StringComparison.Ordinal) >= 0)
                    {
                        result.Fail(report, "UPGRADE29에 과거 퍼센트 또는 속도 단위 정의가 존재합니다.");
                    }
                }
            }

            foreach (KeyValuePair<string, int> pair in idCounts)
            {
                if (pair.Value > 1)
                {
                    result.Fail(report, "Final03 GOTCHA UpgradeType ID가 중복되었습니다: " + pair.Key);
                }
            }

            for (int number = 1; number <= 30; number++)
            {
                string expectedId = "UPGRADE" + number.ToString("00", CultureInfo.InvariantCulture);
                int count;
                if (!idCounts.TryGetValue(expectedId, out count) || count != 1)
                {
                    result.Fail(report, expectedId + "가 정확히 1행 존재하지 않습니다.");
                }
            }

            result.AddDetail("- 정확한 헤더 5개와 데이터 30행 검사 완료");
            result.AddDetail("- UPGRADE01~UPGRADE30 연속성·중복·누락 검사 완료");
            result.AddDetail("- 단위 그룹 및 셀 앞뒤 공백·실제 따옴표 문자 검사 완료");
            result.AddDetail("- UPGRADE18~UPGRADE30 상세·요약·단위 의미 검사 완료");
            result.AddDetail("- UPGRADE25 매니저 매핑 및 UPGRADE28 가드 이동속도 재유입 차단 확인");
            return result;
        }

        private static bool TryParseGachaUpgradeNumber(string id, out int number)
        {
            number = 0;
            return !string.IsNullOrEmpty(id)
                   && id.Length == 9
                   && id.StartsWith("UPGRADE", StringComparison.Ordinal)
                   && int.TryParse(
                       id.Substring(7, 2),
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out number);
        }

        private static string GetExpectedGachaUpgradeUnit(int upgradeNumber)
        {
            if (upgradeNumber == 1)
            {
                return "이동속도%";
            }

            if (upgradeNumber <= 9)
            {
                return "시간%단축";
            }

            if (upgradeNumber <= 17)
            {
                return "코인 수익%증가";
            }

            if (upgradeNumber <= 24)
            {
                return "지속시간%증가";
            }

            if (upgradeNumber <= 27)
            {
                return "이동속도%증가";
            }

            if (upgradeNumber == 28)
            {
                return "퇴치속도%증가";
            }

            return upgradeNumber == 29 ? "SEC" : "인원 증가";
        }

        private static GachaUpgradeExpectation FindGachaUpgradeExpectation(string id)
        {
            for (int index = 0; index < GachaUpgradeExpectations.Length; index++)
            {
                if (string.Equals(GachaUpgradeExpectations[index].Id, id, StringComparison.Ordinal))
                {
                    return GachaUpgradeExpectations[index];
                }
            }

            return null;
        }

        private static void ValidateGachaUpgradeExpectation(
            GachaUpgradeExpectation expectation,
            string detail,
            string summary,
            string unit,
            CheckResult result,
            ValidationReport report)
        {
            if (!string.Equals(detail, expectation.Detail, StringComparison.Ordinal))
            {
                result.Fail(
                    report,
                    expectation.Id + " 상세 설명이 다릅니다. 예상 '" + expectation.Detail
                    + "', 실제 '" + detail + "'");
            }

            if (!string.Equals(summary, expectation.Summary, StringComparison.Ordinal))
            {
                result.Fail(
                    report,
                    expectation.Id + " 요약이 다릅니다. 예상 '" + expectation.Summary
                    + "', 실제 '" + summary + "'");
            }

            if (!string.Equals(unit, expectation.Unit, StringComparison.Ordinal))
            {
                result.Fail(
                    report,
                    expectation.Id + " 핵심 의미 단위가 다릅니다. 예상 '" + expectation.Unit
                    + "', 실제 '" + unit + "'");
            }
        }

        private static CheckResult ValidateCrossReferences(
            List<FinalStaffRow> finalRows,
            HashSet<string> skillIds,
            Dictionary<string, RoleBaseRow> roleBaseRows,
            Dictionary<string, RoleGrowthRow> roleGrowthRows,
            Dictionary<string, CostRuleRow> costRows,
            Dictionary<string, SummaryRow> summaryRows,
            ValidationReport report)
        {
            return ValidateCrossReferences(
                finalRows,
                skillIds,
                roleBaseRows,
                roleGrowthRows,
                costRows,
                summaryRows,
                "Final17",
                report);
        }

        private static CheckResult ValidateCrossReferences(
            List<FinalStaffRow> finalRows,
            HashSet<string> skillIds,
            Dictionary<string, RoleBaseRow> roleBaseRows,
            Dictionary<string, RoleGrowthRow> roleGrowthRows,
            Dictionary<string, CostRuleRow> costRows,
            Dictionary<string, SummaryRow> summaryRows,
            string finalDataName,
            ValidationReport report)
        {
            CheckResult result = new CheckResult();
            Dictionary<string, string> skillGradeTimes = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> roleGradePairs = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < finalRows.Count; i++)
            {
                FinalStaffRow staff = finalRows[i];
                roleGradePairs.Add(staff.RoleKey + "|" + staff.GradeKey);
                if (!roleGrowthRows.ContainsKey(staff.RoleKey))
                {
                    result.Fail(report, staff.Id + " 역할을 RoleGrowth에서 해석할 수 없습니다: " + staff.RoleKey);
                }

                string[] requiredStats = GetRequiredStats(staff.RoleKey);
                for (int statIndex = 0; statIndex < requiredStats.Length; statIndex++)
                {
                    string roleBaseKey = BuildRoleBaseKey(staff.RoleKey, staff.GradeKey, requiredStats[statIndex]);
                    if (!roleBaseRows.ContainsKey(roleBaseKey))
                    {
                        result.Fail(report, staff.Id + " 역할·등급을 RoleBase에서 해석할 수 없습니다: " + roleBaseKey);
                    }
                }

                if (!skillIds.Contains(staff.SkillId))
                {
                    result.Fail(report, staff.Id + " 스킬을 SkillType에서 해석할 수 없습니다: " + staff.SkillId);
                }

                if (!costRows.ContainsKey(staff.GradeKey))
                {
                    result.Fail(report, staff.Id + " 등급을 CostRule에서 해석할 수 없습니다: " + staff.GradeKey);
                }

                string skillGradeKey = staff.SkillId + "|" + staff.GradeKey;
                string timePair = staff.Duration.ToString("R", CultureInfo.InvariantCulture)
                                  + "|" + staff.Cooldown.ToString("R", CultureInfo.InvariantCulture);
                string existingPair;
                if (skillGradeTimes.TryGetValue(skillGradeKey, out existingPair))
                {
                    if (!string.Equals(existingPair, timePair, StringComparison.Ordinal))
                    {
                        result.Fail(
                            report,
                            "같은 스킬+등급의 기본 Duration/Cooldown이 다릅니다: " + skillGradeKey
                            + " (" + existingPair + " / " + timePair + ")");
                    }
                }
                else
                {
                    skillGradeTimes.Add(skillGradeKey, timePair);
                }
            }

            for (int i = 0; i < GradeKeys.Length; i++)
            {
                if (!summaryRows.ContainsKey(GradeKeys[i]))
                {
                    result.Fail(report, "Summary 검증에 필요한 등급이 없습니다: " + GradeKeys[i]);
                }
            }

            result.AddDetail("- " + finalDataName + " 역할·등급 " + roleGradePairs.Count + "개 조합 해석 완료");
            result.AddDetail("- 스킬+등급 시간 " + skillGradeTimes.Count + "개 조합 통일 확인");
            result.AddDetail("- CostRule 등급 매핑 완료");
            result.AddDetail("- Summary는 합계 검증에만 사용하고 적용 데이터로 사용하지 않음");
            return result;
        }

        private static bool TryGetTable(
            string key,
            string displayName,
            Dictionary<string, CsvTable> tables,
            Dictionary<string, string> tableErrors,
            CheckResult result,
            ValidationReport report,
            out CsvTable table)
        {
            if (tables.TryGetValue(key, out table))
            {
                return true;
            }

            string error;
            tableErrors.TryGetValue(key, out error);
            result.Fail(report, displayName + " CSV를 파싱할 수 없습니다: " + (error ?? "알 수 없는 오류"));
            return false;
        }

        private static bool RequireHeaders(
            CsvTable table,
            string[] requiredHeaders,
            string tableName,
            CheckResult result,
            ValidationReport report)
        {
            bool passed = true;
            for (int i = 0; i < requiredHeaders.Length; i++)
            {
                if (!table.HasHeader(requiredHeaders[i]))
                {
                    result.Fail(report, tableName + "에 필수 헤더가 없습니다: " + requiredHeaders[i]);
                    passed = false;
                }
            }

            return passed;
        }

        private static bool ValidateExactHeaders(
            CsvTable table,
            string[] expectedHeaders,
            string tableName,
            CheckResult result,
            ValidationReport report)
        {
            bool passed = true;
            if (table.Headers.Count != expectedHeaders.Length)
            {
                result.Fail(
                    report,
                    tableName + " 헤더 수가 다릅니다. 예상 " + expectedHeaders.Length
                    + ", 실제 " + table.Headers.Count);
                passed = false;
            }

            int comparisonCount = Math.Min(table.Headers.Count, expectedHeaders.Length);
            for (int index = 0; index < comparisonCount; index++)
            {
                if (!string.Equals(table.Headers[index], expectedHeaders[index], StringComparison.Ordinal))
                {
                    result.Fail(
                        report,
                        tableName + " 헤더 순서 또는 값이 다릅니다. 위치 " + (index + 1)
                        + ", 예상 '" + expectedHeaders[index] + "', 실제 '" + table.Headers[index] + "'");
                    passed = false;
                }
            }

            return passed;
        }

        private static void ValidateColumnCounts(
            CsvTable table,
            string tableName,
            CheckResult result,
            ValidationReport report)
        {
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
            {
                if (table.Rows[rowIndex].Count != table.Headers.Count)
                {
                    result.Fail(
                        report,
                        tableName + " " + (rowIndex + 2) + "행의 열 수가 헤더와 다릅니다. 헤더 "
                        + table.Headers.Count + ", 실제 " + table.Rows[rowIndex].Count);
                }
            }
        }

        private static void ValidateLevelValue(
            Dictionary<int, LevelRuleRow> levelRows,
            int level,
            double expectedDuration,
            double expectedCooldown,
            CheckResult result,
            ValidationReport report)
        {
            LevelRuleRow row;
            if (!levelRows.TryGetValue(level, out row))
            {
                return;
            }

            if (!Approximately(row.DurationMultiplier, expectedDuration)
                || !Approximately(row.CooldownMultiplier, expectedCooldown))
            {
                result.Fail(
                    report,
                    "Lv." + level + " 스킬 배율이 다릅니다. 예상 Duration "
                    + expectedDuration.ToString(CultureInfo.InvariantCulture) + " / Cooldown "
                    + expectedCooldown.ToString(CultureInfo.InvariantCulture) + ", 실제 Duration "
                    + row.DurationMultiplier.ToString(CultureInfo.InvariantCulture) + " / Cooldown "
                    + row.CooldownMultiplier.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string[] GetRequiredStats(string role)
        {
            switch (role)
            {
                case "WAITER":
                    return new[] { "MOVE_SPEED" };
                case "CLEANER":
                    return new[] { "MOVE_SPEED", "CLEANING_TIME" };
                case "CHEF":
                    return new[] { "MOVE_SPEED", "COOKING_EFFICIENCY" };
                case "MANAGER":
                    return new[] { "GUIDE_TIME" };
                case "CHEERLEADER":
                    return new[] { "AUTO_CALL_INTERVAL" };
                case "GUARD":
                    return new[] { "MOVE_SPEED", "TROUBLEMAKER_REMOVE_TIME" };
                default:
                    return new string[0];
            }
        }

        private static bool TryMapRole(string koreanRole, out string roleKey)
        {
            switch ((koreanRole ?? string.Empty).Trim())
            {
                case "웨이터":
                    roleKey = "WAITER";
                    return true;
                case "청소부":
                    roleKey = "CLEANER";
                    return true;
                case "주방장":
                    roleKey = "CHEF";
                    return true;
                case "매니저":
                    roleKey = "MANAGER";
                    return true;
                case "치어리더":
                    roleKey = "CHEERLEADER";
                    return true;
                case "가드":
                    roleKey = "GUARD";
                    return true;
                default:
                    roleKey = string.Empty;
                    return false;
            }
        }

        private static bool TryMapGrade(string koreanGrade, out string gradeKey)
        {
            switch ((koreanGrade ?? string.Empty).Trim())
            {
                case "노멀":
                    gradeKey = "NORMAL";
                    return true;
                case "레어":
                    gradeKey = "RARE";
                    return true;
                case "유니크":
                    gradeKey = "UNIQUE";
                    return true;
                case "스페셜":
                    gradeKey = "SPECIAL";
                    return true;
                default:
                    gradeKey = string.Empty;
                    return false;
            }
        }

        private static bool IsOfficialSkillId(string id)
        {
            if (string.IsNullOrEmpty(id)
                || id.Length != 13
                || !id.StartsWith("STAFF_SKILL", StringComparison.Ordinal))
            {
                return false;
            }

            int number;
            return int.TryParse(id.Substring(11), NumberStyles.None, CultureInfo.InvariantCulture, out number)
                   && number >= 1
                   && number <= 10;
        }

        private static string BuildRoleBaseKey(string role, string grade, string stat)
        {
            return role + "|" + grade + "|" + stat;
        }

        private static bool TryParseDouble(string rawValue, out double value)
        {
            bool parsed = double.TryParse(
                (rawValue ?? string.Empty).Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
            return parsed && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool TryParseInt(string rawValue, out int value)
        {
            return int.TryParse(
                (rawValue ?? string.Empty).Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static bool TryParseSeconds(string rawValue, out double value)
        {
            string normalized = (rawValue ?? string.Empty).Trim();
            if (normalized.EndsWith("초", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(0, normalized.Length - 1).Trim();
            }

            return TryParseDouble(normalized, out value);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static bool Approximately(double left, double right)
        {
            return Math.Abs(left - right) <= 0.000001d;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    builder.Append(hashBytes[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static bool TryDecode(
            byte[] bytes,
            ExpectedEncoding expectedEncoding,
            out string text,
            out string error)
        {
            text = null;
            error = null;

            try
            {
                if (expectedEncoding == ExpectedEncoding.Utf8Bom)
                {
                    if (bytes.Length < 3 || bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF)
                    {
                        error = "UTF-8 BOM이 없습니다.";
                        return false;
                    }

                    Encoding utf8 = new UTF8Encoding(false, true);
                    text = utf8.GetString(bytes, 3, bytes.Length - 3);
                    return true;
                }

                if (HasUnicodeBom(bytes))
                {
                    error = "CP949 파일에 Unicode BOM이 포함되어 있습니다.";
                    return false;
                }

                Encoding cp949 = Encoding.GetEncoding(
                    949,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback);
                text = cp949.GetString(bytes);
                byte[] roundTripBytes = cp949.GetBytes(text);
                if (!ByteArraysEqual(bytes, roundTripBytes))
                {
                    error = "CP949 디코딩 후 바이트가 원본과 동일하게 복원되지 않습니다.";
                    text = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = expectedEncoding == ExpectedEncoding.Cp949
                    ? "코드 페이지 949를 사용할 수 없거나 잘못된 CP949 바이트가 있습니다: " + exception.Message
                    : "잘못된 UTF-8 바이트가 있습니다: " + exception.Message;
                text = null;
                return false;
            }
        }

        private static bool HasUnicodeBom(byte[] bytes)
        {
            return (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                   || (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                   || (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                   || (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
                   || (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountPhysicalLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int lineCount = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    lineCount++;
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else if (text[i] == '\n')
                {
                    lineCount++;
                }
            }

            char last = text[text.Length - 1];
            if (last == '\r' || last == '\n')
            {
                lineCount--;
            }

            return lineCount;
        }

        private static string GetEncodingLabel(ExpectedEncoding encoding)
        {
            return encoding == ExpectedEncoding.Utf8Bom ? "UTF-8 BOM" : "CP949";
        }

        private static GitInfo ReadGitInfo()
        {
            string projectRoot = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string branch;
            string branchError;
            string head;
            string headError;
            bool branchAvailable = TryRunGit(projectRoot, "branch --show-current", out branch, out branchError);
            bool headAvailable = TryRunGit(projectRoot, "rev-parse HEAD", out head, out headError);
            string error = string.Join(
                " / ",
                new[] { branchError, headError }.Where(message => !string.IsNullOrEmpty(message)).ToArray());
            return new GitInfo(
                branchAvailable,
                branchAvailable ? branch.Trim() : "확인 실패",
                headAvailable,
                headAvailable ? head.Trim() : "확인 실패",
                error);
        }

        private static bool TryRunGit(
            string workingDirectory,
            string arguments,
            out string standardOutput,
            out string error)
        {
            standardOutput = string.Empty;
            error = string.Empty;

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "git 프로세스를 시작하지 못했습니다.";
                        return false;
                    }

                    standardOutput = process.StandardOutput.ReadToEnd();
                    string standardError = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = string.IsNullOrWhiteSpace(standardError)
                            ? "git 명령이 종료 코드 " + process.ExitCode + "로 실패했습니다."
                            : standardError.Trim();
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void AppendCheck(StringBuilder output, int number, string title, CheckResult result)
        {
            output.AppendLine(number + ". " + title + ": " + (result.Passed ? "PASS" : "FAIL"));
            AppendDetails(output, result.Details);
        }

        private static void AppendDetails(StringBuilder output, List<string> details)
        {
            for (int i = 0; i < details.Count; i++)
            {
                output.AppendLine("   " + details[i]);
            }
        }

        private enum ExpectedEncoding
        {
            Utf8Bom,
            Cp949
        }

        private sealed class OfficialFileSpec
        {
            public readonly string Key;
            public readonly string DisplayName;
            public readonly string Hash;
            public readonly int PhysicalLineCount;
            public readonly ExpectedEncoding Encoding;

            public OfficialFileSpec(
                string key,
                string displayName,
                string hash,
                int physicalLineCount,
                ExpectedEncoding encoding)
            {
                Key = key;
                DisplayName = displayName;
                Hash = hash;
                PhysicalLineCount = physicalLineCount;
                Encoding = encoding;
            }
        }

        private sealed class ScannedCsv
        {
            public readonly string Path;
            public readonly string Hash;
            public readonly byte[] Bytes;

            public ScannedCsv(string path, string hash, byte[] bytes)
            {
                Path = path;
                Hash = hash;
                Bytes = bytes;
            }
        }

        private sealed class MatchedFile
        {
            public readonly OfficialFileSpec Spec;
            public readonly string Path;
            public readonly string Hash;
            public readonly byte[] Bytes;
            public string DecodedText;

            public MatchedFile(OfficialFileSpec spec, string path, string hash, byte[] bytes)
            {
                Spec = spec;
                Path = path;
                Hash = hash;
                Bytes = bytes;
            }
        }

        private sealed class GitInfo
        {
            public readonly bool BranchAvailable;
            public readonly string Branch;
            public readonly bool HeadAvailable;
            public readonly string Head;
            public readonly string ErrorMessage;

            public GitInfo(
                bool branchAvailable,
                string branch,
                bool headAvailable,
                string head,
                string errorMessage)
            {
                BranchAvailable = branchAvailable;
                Branch = branch;
                HeadAvailable = headAvailable;
                Head = head;
                ErrorMessage = errorMessage;
            }
        }

        private sealed class ValidationReport
        {
            private readonly List<string> _warnings = new List<string>();
            private readonly List<string> _errors = new List<string>();

            public int WarningCount { get { return _warnings.Count; } }
            public int ErrorCount { get { return _errors.Count; } }
            public List<string> Warnings { get { return _warnings; } }
            public List<string> Errors { get { return _errors; } }

            public void Warning(string message)
            {
                _warnings.Add(message);
            }

            public void Error(string message)
            {
                _errors.Add(message);
            }
        }

        private sealed class CheckResult
        {
            private readonly List<string> _details = new List<string>();
            private bool _passed = true;

            public bool Passed { get { return _passed; } }
            public List<string> Details { get { return _details; } }

            public void AddDetail(string detail)
            {
                _details.Add(detail);
            }

            public void Fail(ValidationReport report, string message)
            {
                _passed = false;
                report.Error(message);
            }
        }

        private sealed class CsvTable
        {
            private readonly Dictionary<string, int> _headerIndices;

            public readonly List<string> Headers;
            public readonly List<List<string>> Rows;

            private CsvTable(List<string> headers, List<List<string>> rows)
            {
                Headers = headers;
                Rows = rows;
                _headerIndices = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int index = 0; index < Headers.Count; index++)
                {
                    string header = Headers[index];
                    if (_headerIndices.ContainsKey(header))
                    {
                        throw new InvalidDataException("CSV 헤더가 중복되었습니다: " + header);
                    }

                    _headerIndices.Add(header, index);
                }
            }

            public bool HasHeader(string header)
            {
                return _headerIndices.ContainsKey(header);
            }

            public string Get(List<string> row, string header)
            {
                int index;
                if (!_headerIndices.TryGetValue(header, out index) || index < 0 || index >= row.Count)
                {
                    return string.Empty;
                }

                return row[index] ?? string.Empty;
            }

            public static CsvTable Parse(string text)
            {
                List<List<string>> records = ParseRfc4180(text);
                if (records.Count == 0)
                {
                    throw new InvalidDataException("CSV가 비어 있습니다.");
                }

                List<string> headers = records[0];
                if (headers.Count == 0)
                {
                    throw new InvalidDataException("CSV 헤더가 없습니다.");
                }

                List<List<string>> rows = records.Skip(1).ToList();
                return new CsvTable(headers, rows);
            }

            private static List<List<string>> ParseRfc4180(string text)
            {
                List<List<string>> records = new List<List<string>>();
                List<string> row = new List<string>();
                StringBuilder field = new StringBuilder();
                bool inQuotes = false;
                bool justClosedQuote = false;
                bool recordTerminated = false;
                bool sawAnyCharacter = false;

                for (int index = 0; index < text.Length; index++)
                {
                    char character = text[index];
                    sawAnyCharacter = true;

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
                                justClosedQuote = true;
                            }
                        }
                        else
                        {
                            field.Append(character);
                        }

                        continue;
                    }

                    if (justClosedQuote && character != ',' && character != '\r' && character != '\n')
                    {
                        throw new InvalidDataException(
                            "닫는 따옴표 뒤에 구분자 또는 줄바꿈이 아닌 문자가 있습니다. 문자 위치: " + index);
                    }

                    if (character == '"')
                    {
                        if (field.Length != 0)
                        {
                            throw new InvalidDataException("필드 중간에 시작 따옴표가 있습니다. 문자 위치: " + index);
                        }

                        inQuotes = true;
                        justClosedQuote = false;
                        recordTerminated = false;
                    }
                    else if (character == ',')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                        justClosedQuote = false;
                        recordTerminated = false;
                    }
                    else if (character == '\r' || character == '\n')
                    {
                        row.Add(field.ToString());
                        field.Length = 0;
                        records.Add(row);
                        row = new List<string>();
                        justClosedQuote = false;
                        recordTerminated = true;

                        if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                        {
                            index++;
                        }
                    }
                    else
                    {
                        field.Append(character);
                        recordTerminated = false;
                    }
                }

                if (inQuotes)
                {
                    throw new InvalidDataException("닫히지 않은 CSV 따옴표가 있습니다.");
                }

                if (sawAnyCharacter && !recordTerminated)
                {
                    row.Add(field.ToString());
                    records.Add(row);
                }

                return records;
            }
        }

        private sealed class GachaUpgradeExpectation
        {
            public readonly string Id;
            public readonly string Detail;
            public readonly string Summary;
            public readonly string Unit;

            public GachaUpgradeExpectation(string id, string detail, string summary, string unit)
            {
                Id = id;
                Detail = detail;
                Summary = summary;
                Unit = unit;
            }
        }

        private sealed class FinalStaffRow
        {
            public readonly string Id;
            public readonly string RoleKey;
            public readonly string GradeKey;
            public readonly string SkillId;
            public readonly string Passive;
            public readonly string SkillDescription;
            public readonly double BaseSpeed;
            public readonly double Duration;
            public readonly double Cooldown;

            public FinalStaffRow(
                string id,
                string roleKey,
                string gradeKey,
                string skillId,
                string passive,
                string skillDescription,
                double baseSpeed,
                double duration,
                double cooldown)
            {
                Id = id;
                RoleKey = roleKey;
                GradeKey = gradeKey;
                SkillId = skillId;
                Passive = passive;
                SkillDescription = skillDescription;
                BaseSpeed = baseSpeed;
                Duration = duration;
                Cooldown = cooldown;
            }
        }

        private sealed class RoleBaseRow
        {
            public readonly string Role;
            public readonly string Grade;
            public readonly string Stat;
            public readonly double BaseValue;
            public readonly string Unit;
            public readonly double MinimumValue;

            public RoleBaseRow(
                string role,
                string grade,
                string stat,
                double baseValue,
                string unit,
                double minimumValue)
            {
                Role = role;
                Grade = grade;
                Stat = stat;
                BaseValue = baseValue;
                Unit = unit;
                MinimumValue = minimumValue;
            }
        }

        private sealed class RoleGrowthRow
        {
            public readonly string Role;
            public readonly string Unit;
            public readonly double[] CumulativeDeltas;

            public RoleGrowthRow(string role, string unit, double[] cumulativeDeltas)
            {
                Role = role;
                Unit = unit;
                CumulativeDeltas = cumulativeDeltas;
            }
        }

        private sealed class LevelRuleRow
        {
            public readonly int Level;
            public readonly double DurationMultiplier;
            public readonly double CooldownMultiplier;

            public LevelRuleRow(int level, double durationMultiplier, double cooldownMultiplier)
            {
                Level = level;
                DurationMultiplier = durationMultiplier;
                CooldownMultiplier = cooldownMultiplier;
            }
        }

        private sealed class CostRuleRow
        {
            public readonly string Grade;
            public readonly string[] Currencies;
            public readonly int[] Amounts;

            public CostRuleRow(string grade, string[] currencies, int[] amounts)
            {
                Grade = grade;
                Currencies = currencies;
                Amounts = amounts;
            }
        }

        private sealed class SummaryRow
        {
            public readonly string Grade;
            public readonly int TotalCoin;
            public readonly int TotalDiamond;

            public SummaryRow(string grade, int totalCoin, int totalDiamond)
            {
                Grade = grade;
                TotalCoin = totalCoin;
                TotalDiamond = totalDiamond;
            }
        }
    }
}
