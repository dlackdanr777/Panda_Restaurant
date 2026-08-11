using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffOfficialDataPackageSnapshotValidator
    {
        private const string MenuPath =
            "Tools/Panda Restaurant/Staff/Validate Official Data Snapshot";

        private static readonly string[] OfficialKeys =
        {
            "Final17",
            "RoleBase",
            "RoleGrowth",
            "LevelRule",
            "CostRule",
            "Summary",
            "Policy",
            "SkillType",
            "GachaUpgradeType"
        };

        private static readonly Dictionary<string, int> ExpectedDataRowCounts =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "Final17", 92 },
                { "RoleBase", 34 },
                { "RoleGrowth", 6 },
                { "LevelRule", 5 },
                { "CostRule", 4 },
                { "Summary", 4 },
                { "Policy", 13 },
                { "SkillType", 10 },
                { "GachaUpgradeType", 30 }
            };

        [MenuItem(MenuPath)]
        private static void ValidateOfficialDataSnapshot()
        {
            string initialDirectory = Directory.GetParent(Application.dataPath) != null
                ? Directory.GetParent(Application.dataPath).FullName
                : Application.dataPath;
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Staff 공식 데이터 Snapshot 폴더 선택",
                initialDirectory,
                string.Empty);

            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                Debug.LogWarning(
                    "[Staff Official Data Package Snapshot Validation]\n"
                    + "폴더 선택이 취소되어 검증하지 않았습니다.");
                return;
            }

            StaffOfficialDataPackageSnapshot firstSnapshot;
            IReadOnlyList<string> firstDiagnostics;
            bool firstBuilt = StaffDataPackValidator.TryBuildReadOnlySnapshot(
                selectedFolder,
                out firstSnapshot,
                out firstDiagnostics);

            StaffOfficialDataPackageSnapshot secondSnapshot;
            IReadOnlyList<string> secondDiagnostics;
            bool secondBuilt = StaffDataPackValidator.TryBuildReadOnlySnapshot(
                selectedFolder,
                out secondSnapshot,
                out secondDiagnostics);

            List<string> errors = new List<string>();
            if (!firstBuilt || firstSnapshot == null)
            {
                AddDiagnostics("첫 번째 Snapshot", firstDiagnostics, errors);
            }

            if (!secondBuilt || secondSnapshot == null)
            {
                AddDiagnostics("두 번째 Snapshot", secondDiagnostics, errors);
            }

            bool snapshotPassed = firstBuilt && firstSnapshot != null;
            bool officialFilesPassed = snapshotPassed
                                       && ValidateOfficialFiles(firstSnapshot, errors);
            bool metadataPassed = snapshotPassed
                                  && ValidateMetadata(firstSnapshot, errors);
            bool dataRowCountsPassed = snapshotPassed
                                       && ValidateDataRowCounts(firstSnapshot, errors);
            bool fingerprintPassed = snapshotPassed
                                     && ValidatePackageFingerprint(firstSnapshot, errors);
            bool deterministicPassed = snapshotPassed
                                       && secondBuilt
                                       && secondSnapshot != null
                                       && ValidateDeterministicRebuild(
                                           firstSnapshot,
                                           secondSnapshot,
                                           errors);
            bool immutabilityPassed = snapshotPassed
                                      && ValidateDeepImmutability(firstSnapshot, errors);
            bool policyPassed = snapshotPassed
                                && ValidatePolicyPreservation(firstSnapshot, errors);

            bool passed = snapshotPassed
                          && officialFilesPassed
                          && metadataPassed
                          && dataRowCountsPassed
                          && fingerprintPassed
                          && deterministicPassed
                          && immutabilityPassed
                          && policyPassed
                          && errors.Count == 0;

            StringBuilder output = new StringBuilder();
            output.AppendLine("[Staff Official Data Package Snapshot Validation]");
            output.AppendLine();
            AppendResult(output, 1, "Snapshot 생성", snapshotPassed);
            AppendResult(output, 2, "공식 파일 9개", officialFilesPassed);
            AppendResult(output, 3, "Metadata", metadataPassed);
            AppendResult(output, 4, "데이터 행 수", dataRowCountsPassed);
            AppendResult(output, 5, "PackageFingerprint", fingerprintPassed);
            AppendResult(output, 6, "결정론적 재생성", deterministicPassed);
            AppendResult(output, 7, "깊은 불변성", immutabilityPassed);
            AppendResult(output, 8, "PandaToken·Legacy 정책 원본 보존", policyPassed);
            output.AppendLine("9. 오류 수: " + errors.Count);
            for (int index = 0; index < errors.Count; index++)
            {
                output.AppendLine("   ERROR: " + errors[index]);
            }

            output.AppendLine("10. 최종 결과: " + (passed ? "PASS" : "FAIL"));
            output.AppendLine();
            output.AppendLine(
                "STAFF OFFICIAL DATA SNAPSHOT VALIDATION: " + (passed ? "PASS" : "FAIL"));

            if (passed)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static bool ValidateOfficialFiles(
            StaffOfficialDataPackageSnapshot snapshot,
            List<string> errors)
        {
            bool passed = true;
            if (snapshot.OfficialFileCount != OfficialKeys.Length)
            {
                errors.Add(
                    "공식 파일 수가 다릅니다. 예상 " + OfficialKeys.Length
                    + ", 실제 " + snapshot.OfficialFileCount);
                passed = false;
            }

            if (snapshot.FilesByKey.Count != OfficialKeys.Length)
            {
                errors.Add(
                    "공식 파일 Dictionary 수가 다릅니다. 예상 " + OfficialKeys.Length
                    + ", 실제 " + snapshot.FilesByKey.Count);
                passed = false;
            }

            if (snapshot.FilesInOfficialOrder.Count != OfficialKeys.Length)
            {
                errors.Add("공식 파일 고정 순서 목록의 수가 9개가 아닙니다.");
                passed = false;
            }

            int comparisonCount = Math.Min(snapshot.FilesInOfficialOrder.Count, OfficialKeys.Length);
            for (int index = 0; index < comparisonCount; index++)
            {
                string expectedKey = OfficialKeys[index];
                StaffOfficialFileSnapshot orderedFile = snapshot.FilesInOfficialOrder[index];
                if (!string.Equals(orderedFile.Key, expectedKey, StringComparison.Ordinal))
                {
                    errors.Add(
                        "공식 파일 순서가 다릅니다. 위치 " + index
                        + ", 예상 " + expectedKey + ", 실제 " + orderedFile.Key);
                    passed = false;
                }

                StaffOfficialFileSnapshot foundFile;
                if (!snapshot.TryGetFile(expectedKey, out foundFile) || foundFile == null)
                {
                    errors.Add("공식 파일 Key를 찾지 못했습니다: " + expectedKey);
                    passed = false;
                }
                else if (!ReferenceEquals(orderedFile, foundFile))
                {
                    errors.Add("고정 순서 목록과 Dictionary의 파일이 다릅니다: " + expectedKey);
                    passed = false;
                }
            }

            return passed;
        }

        private static bool ValidateMetadata(
            StaffOfficialDataPackageSnapshot snapshot,
            List<string> errors)
        {
            bool passed = true;
            for (int fileIndex = 0; fileIndex < snapshot.FilesInOfficialOrder.Count; fileIndex++)
            {
                StaffOfficialFileSnapshot file = snapshot.FilesInOfficialOrder[fileIndex];
                if (string.IsNullOrEmpty(file.Key)
                    || string.IsNullOrEmpty(file.DisplayName)
                    || string.IsNullOrEmpty(file.SourcePath))
                {
                    errors.Add("파일 기본 Metadata가 비어 있습니다: " + file.Key);
                    passed = false;
                }

                if (!IsLowercaseSha256(file.Sha256))
                {
                    errors.Add("SHA-256 형식이 올바르지 않습니다: " + file.Key);
                    passed = false;
                }

                if (!string.Equals(file.EncodingLabel, "UTF-8 BOM", StringComparison.Ordinal)
                    && !string.Equals(file.EncodingLabel, "CP949", StringComparison.Ordinal))
                {
                    errors.Add("EncodingLabel이 올바르지 않습니다: " + file.Key);
                    passed = false;
                }

                if (file.ExpectedPhysicalLineCount != file.ActualPhysicalLineCount)
                {
                    errors.Add(
                        "물리 행 수 Metadata가 다릅니다: " + file.Key
                        + " (예상 " + file.ExpectedPhysicalLineCount
                        + ", 실제 " + file.ActualPhysicalLineCount + ")");
                    passed = false;
                }

                if (file.Headers.Count == 0)
                {
                    errors.Add("Header가 비어 있습니다: " + file.Key);
                    passed = false;
                }

                for (int rowIndex = 0; rowIndex < file.Rows.Count; rowIndex++)
                {
                    if (file.Rows[rowIndex].Count != file.Headers.Count)
                    {
                        errors.Add(
                            file.Key + "의 " + (rowIndex + 2)
                            + "행 열 수가 Header 수와 다릅니다.");
                        passed = false;
                    }
                }
            }

            return passed;
        }

        private static bool ValidateDataRowCounts(
            StaffOfficialDataPackageSnapshot snapshot,
            List<string> errors)
        {
            bool passed = true;
            for (int index = 0; index < OfficialKeys.Length; index++)
            {
                string key = OfficialKeys[index];
                StaffOfficialFileSnapshot file;
                if (!snapshot.TryGetFile(key, out file) || file == null)
                {
                    errors.Add("행 수를 확인할 공식 파일이 없습니다: " + key);
                    passed = false;
                    continue;
                }

                int expected = ExpectedDataRowCounts[key];
                if (file.Rows.Count != expected)
                {
                    errors.Add(
                        key + " 데이터 행 수가 다릅니다. 예상 " + expected
                        + ", 실제 " + file.Rows.Count);
                    passed = false;
                }
            }

            return passed;
        }

        private static bool ValidatePackageFingerprint(
            StaffOfficialDataPackageSnapshot snapshot,
            List<string> errors)
        {
            bool passed = true;
            if (!IsLowercaseSha256(snapshot.PackageFingerprint))
            {
                errors.Add("PackageFingerprint가 소문자 SHA-256 64자리 형식이 아닙니다.");
                passed = false;
            }

            StaffOfficialDataPackageSnapshot changedContextSnapshot =
                new StaffOfficialDataPackageSnapshot(
                    snapshot.SourceFolder + "_CONTEXT_CHECK",
                    snapshot.GitBranch,
                    snapshot.GitHead + "_CONTEXT_CHECK",
                    snapshot.FilesInOfficialOrder);
            if (!string.Equals(
                    snapshot.PackageFingerprint,
                    changedContextSnapshot.PackageFingerprint,
                    StringComparison.Ordinal))
            {
                errors.Add("SourceFolder 또는 Git HEAD가 PackageFingerprint에 영향을 줍니다.");
                passed = false;
            }

            if (snapshot.FilesInOfficialOrder.Count == 0)
            {
                errors.Add("PackageFingerprint 내용 변경 검사를 수행할 파일이 없습니다.");
                return false;
            }

            StaffOfficialFileSnapshot originalFile = snapshot.FilesInOfficialOrder[0];
            string changedSha256 = originalFile.Sha256[0] == '0'
                ? "1" + originalFile.Sha256.Substring(1)
                : "0" + originalFile.Sha256.Substring(1);
            StaffOfficialFileSnapshot changedFile = new StaffOfficialFileSnapshot(
                originalFile.Key,
                originalFile.DisplayName,
                originalFile.SourcePath,
                changedSha256,
                originalFile.EncodingLabel,
                originalFile.ExpectedPhysicalLineCount,
                originalFile.ActualPhysicalLineCount,
                originalFile.Headers,
                originalFile.Rows);
            List<StaffOfficialFileSnapshot> changedFiles =
                new List<StaffOfficialFileSnapshot>(snapshot.FilesInOfficialOrder);
            changedFiles[0] = changedFile;
            StaffOfficialDataPackageSnapshot changedContentSnapshot =
                new StaffOfficialDataPackageSnapshot(
                    snapshot.SourceFolder,
                    snapshot.GitBranch,
                    snapshot.GitHead,
                    changedFiles);
            if (string.Equals(
                    snapshot.PackageFingerprint,
                    changedContentSnapshot.PackageFingerprint,
                    StringComparison.Ordinal))
            {
                errors.Add("공식 파일 SHA-256 변경이 PackageFingerprint에 반영되지 않습니다.");
                passed = false;
            }

            return passed;
        }

        private static bool ValidateDeterministicRebuild(
            StaffOfficialDataPackageSnapshot first,
            StaffOfficialDataPackageSnapshot second,
            List<string> errors)
        {
            if (!string.Equals(first.PackageFingerprint, second.PackageFingerprint, StringComparison.Ordinal))
            {
                errors.Add("동일 폴더의 두 Snapshot PackageFingerprint가 다릅니다.");
                return false;
            }

            if (!SnapshotsHaveEqualData(first, second))
            {
                errors.Add("동일 폴더에서 재생성한 두 Snapshot의 원본 데이터가 다릅니다.");
                return false;
            }

            return true;
        }

        private static bool ValidateDeepImmutability(
            StaffOfficialDataPackageSnapshot source,
            List<string> errors)
        {
            if (source.FilesInOfficialOrder.Count == 0)
            {
                errors.Add("불변성 검사에 사용할 파일이 없습니다.");
                return false;
            }

            StaffOfficialFileSnapshot sourceFile = source.FilesInOfficialOrder[0];
            StaffOfficialFileSnapshot testFile = new StaffOfficialFileSnapshot(
                sourceFile.Key,
                sourceFile.DisplayName,
                sourceFile.SourcePath,
                sourceFile.Sha256,
                sourceFile.EncodingLabel,
                sourceFile.ExpectedPhysicalLineCount,
                sourceFile.ActualPhysicalLineCount,
                sourceFile.Headers,
                sourceFile.Rows);
            StaffOfficialDataPackageSnapshot testSnapshot = new StaffOfficialDataPackageSnapshot(
                source.SourceFolder,
                source.GitBranch,
                source.GitHead,
                new[] { testFile });

            bool passed = true;
            IDictionary<string, StaffOfficialFileSnapshot> filesDictionary =
                testSnapshot.FilesByKey as IDictionary<string, StaffOfficialFileSnapshot>;
            passed &= VerifyNotSupported(
                "Files Dictionary Add",
                filesDictionary == null
                    ? null
                    : (Action)(() => filesDictionary.Add("__IMMUTABILITY_TEST__", testFile)),
                errors);

            IList<string> headers = testFile.Headers as IList<string>;
            passed &= VerifyNotSupported(
                "Headers Add",
                headers == null ? null : (Action)(() => headers.Add("__IMMUTABILITY_TEST__")),
                errors);

            IList<IReadOnlyList<string>> rows = testFile.Rows as IList<IReadOnlyList<string>>;
            passed &= VerifyNotSupported(
                "Rows Add",
                rows == null ? null : (Action)(() => rows.Add(testFile.Rows[0])),
                errors);

            IList<string> row = testFile.Rows[0] as IList<string>;
            passed &= VerifyNotSupported(
                "개별 Row Add",
                row == null ? null : (Action)(() => row.Add("__IMMUTABILITY_TEST__")),
                errors);

            passed &= ValidateReadOnlyTypeShape(typeof(StaffOfficialDataPackageSnapshot), errors);
            passed &= ValidateReadOnlyTypeShape(typeof(StaffOfficialFileSnapshot), errors);
            return passed;
        }

        private static bool ValidatePolicyPreservation(
            StaffOfficialDataPackageSnapshot snapshot,
            List<string> errors)
        {
            bool passed = true;
            StaffOfficialFileSnapshot policyFile;
            if (!snapshot.TryGetFile("Policy", out policyFile) || policyFile == null)
            {
                errors.Add("Policy Snapshot을 찾지 못했습니다.");
                return false;
            }

            passed &= ValidatePolicyValue(
                policyFile,
                "PandaTokenForUpgrade",
                "FALSE",
                errors);
            passed &= ValidatePolicyValue(
                policyFile,
                "PandaTokenUseCase",
                "GACHA_DUPLICATE_AND_DIRECT_PURCHASE",
                errors);
            passed &= ValidatePolicyValue(
                policyFile,
                "StaffSkinLegacyPolicy",
                "KEEP_BUT_NOT_USED",
                errors);

            StaffOfficialFileSnapshot final17File;
            if (!snapshot.TryGetFile("Final17", out final17File) || final17File == null)
            {
                errors.Add("Final17 Snapshot을 찾지 못했습니다.");
                return false;
            }

            string[] requiredHeaders = { "가챠 확률", "중복시 지급 토큰", "토큰 구매 가격" };
            for (int index = 0; index < requiredHeaders.Length; index++)
            {
                if (IndexOf(final17File.Headers, requiredHeaders[index]) < 0)
                {
                    errors.Add("Final17 원본 Header가 보존되지 않았습니다: " + requiredHeaders[index]);
                    passed = false;
                }
            }

            return passed;
        }

        private static bool ValidatePolicyValue(
            StaffOfficialFileSnapshot policyFile,
            string policyKey,
            string expectedValue,
            List<string> errors)
        {
            int keyIndex = IndexOf(policyFile.Headers, "PolicyKey");
            int valueIndex = IndexOf(policyFile.Headers, "Value");
            if (keyIndex < 0 || valueIndex < 0)
            {
                errors.Add("Policy Snapshot의 PolicyKey 또는 Value Header가 없습니다.");
                return false;
            }

            for (int rowIndex = 0; rowIndex < policyFile.Rows.Count; rowIndex++)
            {
                IReadOnlyList<string> row = policyFile.Rows[rowIndex];
                if (keyIndex < row.Count
                    && string.Equals(row[keyIndex], policyKey, StringComparison.Ordinal))
                {
                    if (valueIndex < row.Count
                        && string.Equals(row[valueIndex], expectedValue, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    string actual = valueIndex < row.Count ? row[valueIndex] : string.Empty;
                    errors.Add(
                        policyKey + " 원본 값이 다릅니다. 예상 " + expectedValue
                        + ", 실제 " + actual);
                    return false;
                }
            }

            errors.Add("Policy Snapshot에서 원본 정책을 찾지 못했습니다: " + policyKey);
            return false;
        }

        private static bool VerifyNotSupported(
            string testName,
            Action mutationAttempt,
            List<string> errors)
        {
            if (mutationAttempt == null)
            {
                errors.Add(testName + " 검사를 수행할 ICollection 인터페이스가 없습니다.");
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
                errors.Add(testName + " 시 NotSupportedException이 아닌 오류가 발생했습니다: " + exception.Message);
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
                    errors.Add(type.Name + "." + property.Name + "이 수정 가능한 컬렉션 타입을 반환합니다.");
                    passed = false;
                }
            }

            FieldInfo[] publicFields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            for (int index = 0; index < publicFields.Length; index++)
            {
                FieldInfo field = publicFields[index];
                if (!field.IsInitOnly && !field.IsLiteral)
                {
                    errors.Add(type.Name + "." + field.Name + "이 수정 가능한 public field입니다.");
                    passed = false;
                }
            }

            return passed;
        }

        private static bool IsMutableConcreteCollection(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }

            Type genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(List<>) || genericType == typeof(Dictionary<,>);
        }

        private static bool SnapshotsHaveEqualData(
            StaffOfficialDataPackageSnapshot first,
            StaffOfficialDataPackageSnapshot second)
        {
            if (first.OfficialFileCount != second.OfficialFileCount)
            {
                return false;
            }

            for (int fileIndex = 0; fileIndex < first.FilesInOfficialOrder.Count; fileIndex++)
            {
                StaffOfficialFileSnapshot left = first.FilesInOfficialOrder[fileIndex];
                StaffOfficialFileSnapshot right = second.FilesInOfficialOrder[fileIndex];
                if (!string.Equals(left.Key, right.Key, StringComparison.Ordinal)
                    || !string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
                    || !string.Equals(left.SourcePath, right.SourcePath, StringComparison.Ordinal)
                    || !string.Equals(left.Sha256, right.Sha256, StringComparison.Ordinal)
                    || !string.Equals(left.EncodingLabel, right.EncodingLabel, StringComparison.Ordinal)
                    || left.ExpectedPhysicalLineCount != right.ExpectedPhysicalLineCount
                    || left.ActualPhysicalLineCount != right.ActualPhysicalLineCount
                    || !ListsHaveEqualValues(left.Headers, right.Headers)
                    || left.Rows.Count != right.Rows.Count)
                {
                    return false;
                }

                for (int rowIndex = 0; rowIndex < left.Rows.Count; rowIndex++)
                {
                    if (!ListsHaveEqualValues(left.Rows[rowIndex], right.Rows[rowIndex]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool ListsHaveEqualValues(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (int index = 0; index < left.Count; index++)
            {
                if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static int IndexOf(IReadOnlyList<string> values, string expected)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], expected, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
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

        private static void AddDiagnostics(
            string label,
            IReadOnlyList<string> diagnostics,
            List<string> errors)
        {
            if (diagnostics == null || diagnostics.Count == 0)
            {
                errors.Add(label + " 생성에 실패했지만 진단 정보가 없습니다.");
                return;
            }

            for (int index = 0; index < diagnostics.Count; index++)
            {
                if (diagnostics[index].StartsWith("ERROR: ", StringComparison.Ordinal))
                {
                    errors.Add(label + " - " + diagnostics[index].Substring("ERROR: ".Length));
                }
            }
        }

        private static void AppendResult(
            StringBuilder output,
            int number,
            string title,
            bool passed)
        {
            output.AppendLine(number + ". " + title + ": " + (passed ? "PASS" : "FAIL"));
        }
    }
}
