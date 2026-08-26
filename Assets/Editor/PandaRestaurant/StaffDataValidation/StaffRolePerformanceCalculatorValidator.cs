using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffRolePerformanceCalculatorValidator
    {
        private const string MenuPath = "Tools/Panda Restaurant/Staff/Validate Role Performance Calculator";
        private const float ComparisonTolerance = 0.0001f;
        private const float ChefQaWarningMultiplier = 6.0f;

        [MenuItem(MenuPath)]
        private static void ValidateRolePerformanceCalculator()
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("[Staff Role Performance Calculator Validation]");
            output.AppendLine();

            int errorCount = 0;
            errorCount += RunSection(1, "공용 영구 업무배율", ValidatePermanentRoleWorkMultiplier, output);
            errorCount += RunSection(2, "웨이터 W-A 공식", ValidateWaiterFormula, output);
            errorCount += RunSection(3, "청소부 C-B 공식", ValidateCleanerFormula, output);
            errorCount += RunSection(4, "청소부 0.2초 하한", ValidateCleanerMinimumDuration, output);
            errorCount += RunSection(5, "주방장 H-A 역할배율", ValidateChefRoleMultiplier, output);
            errorCount += RunSection(6, "주방장 최종 조리배율", ValidateChefTotalMultiplier, output);
            errorCount += RunSection(7, "주방장 6배 QA 경고 감지", ValidateChefQaWarning, output);
            errorCount += RunSection(8, "불변조건", ValidateInvariants, output);
            errorCount += RunSection(9, "입력 예외 처리", ValidateInputExceptions, output);

            output.AppendLine("10. 오류 수: " + errorCount.ToString(CultureInfo.InvariantCulture));
            string finalResult = errorCount == 0
                ? "STAFF ROLE PERFORMANCE CALCULATOR VALIDATION: PASS"
                : "STAFF ROLE PERFORMANCE CALCULATOR VALIDATION: FAIL";
            output.AppendLine("11. 최종 결과: " + finalResult);

            if (errorCount == 0)
            {
                Debug.Log(output.ToString());
            }
            else
            {
                Debug.LogError(output.ToString());
            }
        }

        private static void ValidatePermanentRoleWorkMultiplier(SectionReport report)
        {
            float actual = StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, 8f, 0.20f);
            report.AssertApproximately("LevelOne 6, Current 8, Gacha 20%", 1.6f, actual);
        }

        private static void ValidateWaiterFormula(SectionReport report)
        {
            float w1 = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 6f, 0f, 0f);
            float w2 = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 8f, 0f, 0f);
            float w3 = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 9f, 0.20f, 1.50f);
            float w4 = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 12f, 0.20f, 1.50f);
            float w3MultiplicativeResult = (6f / 9f) / 1.2f / 2.5f;

            report.AssertApproximately("W1", 1.0f, w1);
            report.AssertApproximately("W2", 0.75f, w2);
            report.AssertApproximately("W3 W-A", 0.24691358f, w3);
            report.AssertApproximately("W4 W-A", 0.18518519f, w4);
            report.AssertCondition(
                Math.Abs(w3 - w3MultiplicativeResult) > ComparisonTolerance,
                "W3 W-A와 W-B가 구분됨: W-A=" + FormatFloat(w3)
                    + ", W-B=" + FormatFloat(w3MultiplicativeResult),
                "W3 W-A 결과가 W-B 곱연산 결과와 구분되지 않습니다.");
        }

        private static void ValidateCleanerFormula(SectionReport report)
        {
            float c1 = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0f, 0f);
            float c2 = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0.20f, 1.50f);
            float c3 = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(0.4f, 10f, 12f, 0.20f, 1.50f);

            report.AssertApproximately("C1", 0.75f, c1);
            report.AssertApproximately("C2", 0.25f, c2);
            report.AssertApproximately("C3 하한 적용 결과", 0.2f, c3);
            report.AddDetail("C3 하한 적용 전 공식값: 약 0.11111111초");
        }

        private static void ValidateCleanerMinimumDuration(SectionReport report)
        {
            report.AssertApproximately(
                "C4 공개 하한 상수",
                0.2f,
                StaffRolePerformanceCalculator.CleanerMinimumDurationSeconds);

            float clampedResult = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(
                0.01f,
                10f,
                100f,
                10f,
                10f);
            report.AssertApproximately("극단 정상 입력 하한", 0.2f, clampedResult);
        }

        private static void ValidateChefRoleMultiplier(SectionReport report)
        {
            float h1 = StaffRolePerformanceCalculator.CalculateChefRoleCookMultiplier(0.50f, 6f, 6f, 0f);
            float h2 = StaffRolePerformanceCalculator.CalculateChefRoleCookMultiplier(0.50f, 6f, 8f, 0.20f);
            float h3 = StaffRolePerformanceCalculator.CalculateChefRoleCookMultiplier(1.50f, 10f, 12f, 0.20f);

            report.AssertApproximately("H1 역할배율", 1.5f, h1);
            report.AssertApproximately("H2 역할배율", 1.8f, h2);
            report.AssertApproximately("H3 역할배율", 3.16f, h3);
        }

        private static void ValidateChefTotalMultiplier(SectionReport report)
        {
            float h2Total = StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(
                0.50f,
                6f,
                8f,
                0.20f,
                2.5f);
            float h3Total = StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(
                1.50f,
                10f,
                12f,
                0.20f,
                2.5f);

            report.AssertApproximately("H2 최종 조리배율", 4.5f, h2Total);
            report.AssertApproximately("H3 최종 조리배율", 7.9f, h3Total);
            report.AssertCondition(
                Math.Abs(h3Total - ChefQaWarningMultiplier) > ComparisonTolerance,
                "H3가 6.0으로 Clamp되지 않음: " + FormatFloat(h3Total),
                "H3가 QA 기준 6.0으로 Clamp된 것으로 보입니다.");
        }

        private static void ValidateChefQaWarning(SectionReport report)
        {
            float h3Total = StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(
                1.50f,
                10f,
                12f,
                0.20f,
                2.5f);

            report.AssertCondition(
                h3Total > ChefQaWarningMultiplier,
                "예상된 QA 경고 대상 감지: " + FormatFloat(h3Total)
                    + "배 > " + FormatFloat(ChefQaWarningMultiplier) + "배 (계산 실패 아님)",
                "H3 결과에서 6배 초과 QA 경고 조건을 감지하지 못했습니다.");
        }

        private static void ValidateInvariants(SectionReport report)
        {
            float waiterAtSpeed6 = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 6f, 0f, 0f);
            float waiterAtSpeed8 = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 8f, 0f, 0f);
            report.AssertCondition(
                waiterAtSpeed8 <= waiterAtSpeed6,
                "CurrentSpeed 증가 시 웨이터 시간이 증가하지 않음",
                "CurrentSpeed 증가 후 웨이터 시간이 증가했습니다.");

            float cleanerAtSpeed6 = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 6f, 0f, 0f);
            float cleanerAtSpeed8 = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0f, 0f);
            report.AssertCondition(
                cleanerAtSpeed8 <= cleanerAtSpeed6,
                "CurrentSpeed 증가 시 청소시간이 증가하지 않음",
                "CurrentSpeed 증가 후 청소시간이 증가했습니다.");

            float permanentWithoutGacha = StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, 8f, 0f);
            float permanentWithGacha = StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, 8f, 0.20f);
            float waiterWithoutGacha = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 8f, 0f, 0f);
            float waiterWithGacha = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 8f, 0.20f, 0f);
            float cleanerWithoutGacha = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0f, 0f);
            float cleanerWithGacha = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0.20f, 0f);
            float chefWithoutGacha = StaffRolePerformanceCalculator.CalculateChefRoleCookMultiplier(0.50f, 6f, 8f, 0f);
            float chefWithGacha = StaffRolePerformanceCalculator.CalculateChefRoleCookMultiplier(0.50f, 6f, 8f, 0.20f);
            report.AssertCondition(
                permanentWithGacha >= permanentWithoutGacha
                    && waiterWithGacha <= waiterWithoutGacha
                    && cleanerWithGacha <= cleanerWithoutGacha
                    && chefWithGacha >= chefWithoutGacha,
                "GachaRate 증가 시 모든 역할의 영구 성장 효과가 약해지지 않음",
                "GachaRate 증가 후 하나 이상의 영구 성장 효과가 약해졌습니다.");

            float waiterWithoutFever = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 8f, 0f, 0f);
            float waiterWithFever = StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 8f, 0f, 1.50f);
            float cleanerWithoutFever = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0f, 0f);
            float cleanerWithFever = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(1f, 6f, 8f, 0f, 1.50f);
            report.AssertCondition(
                waiterWithFever <= waiterWithoutFever && cleanerWithFever <= cleanerWithoutFever,
                "FeverRate 증가 시 웨이터·청소부 시간이 증가하지 않음",
                "FeverRate 증가 후 웨이터 또는 청소부 시간이 증가했습니다.");

            float chefChannel1 = StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(0.50f, 6f, 8f, 0.20f, 1f);
            float chefChannel2 = StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(0.50f, 6f, 8f, 0.20f, 2.5f);
            report.AssertCondition(
                chefChannel2 >= chefChannel1,
                "CookingChannelMultiplier 증가 시 최종 조리배율이 감소하지 않음",
                "CookingChannelMultiplier 증가 후 최종 조리배율이 감소했습니다.");

            ValidatePublicApiShape(report);

            float minimumCleanerResult = StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(
                0.01f,
                1f,
                100f,
                100f,
                100f);
            report.AssertCondition(
                minimumCleanerResult >= StaffRolePerformanceCalculator.CleanerMinimumDurationSeconds,
                "정상 입력에서 청소시간이 0.2초 미만으로 내려가지 않음",
                "정상 입력에서 청소시간이 0.2초 미만으로 내려갔습니다.");

            float uncappedChefResult = StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(
                1.50f,
                10f,
                12f,
                0.20f,
                2.5f);
            report.AssertCondition(
                uncappedChefResult > ChefQaWarningMultiplier,
                "주방장 총배율이 6배를 넘어도 하드캡되지 않음",
                "주방장 총배율이 6배 이하로 하드캡되었습니다.");
        }

        private static void ValidatePublicApiShape(SectionReport report)
        {
            HashSet<string> expectedMethodNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "CalculatePermanentRoleWorkMultiplier",
                "CalculateWaiterWorkDurationSeconds",
                "CalculateCleanerFinalDurationSeconds",
                "CalculateChefRoleCookMultiplier",
                "CalculateChefTotalCookMultiplier"
            };

            MethodInfo[] methods = typeof(StaffRolePerformanceCalculator).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (methods.Length == expectedMethodNames.Count)
            {
                report.AddDetail("공개 계산 메서드가 정확히 5개임");
            }
            else
            {
                report.AddApiShapeError(
                    "공개 계산 메서드 수가 예상과 다릅니다: "
                    + methods.Length.ToString(CultureInfo.InvariantCulture));
            }

            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                if (!expectedMethodNames.Contains(method.Name))
                {
                    report.AddApiShapeError("예상하지 않은 공개 메서드가 있습니다: " + method.Name);
                }

                ParameterInfo[] parameters = method.GetParameters();
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    string parameterName = parameters[parameterIndex].Name ?? string.Empty;
                    string lowerParameterName = parameterName.ToLowerInvariant();
                    if (lowerParameterName.IndexOf("temporary", StringComparison.Ordinal) >= 0
                        || lowerParameterName.IndexOf("skill", StringComparison.Ordinal) >= 0
                        || lowerParameterName.IndexOf("movespeed", StringComparison.Ordinal) >= 0
                        || string.Equals(lowerParameterName, "speedmul", StringComparison.Ordinal))
                    {
                        report.AddApiShapeError("임시 이동속도 스킬로 해석될 수 있는 공개 인자가 있습니다: "
                            + method.Name + "." + parameterName);
                    }
                }
            }

            if (!report.HasApiShapeError)
            {
                report.AddDetail("계산기 API에 임시 이동속도 스킬 입력 인자가 없음");
            }
        }

        private static void ValidateInputExceptions(SectionReport report)
        {
            report.ExpectArgumentOutOfRange(
                "BaseDuration 0",
                () => { StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(0f, 6f, 6f, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "BaseCleaningDuration 0",
                () => { StaffRolePerformanceCalculator.CalculateCleanerFinalDurationSeconds(0f, 6f, 6f, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "ReferenceSpeed 0",
                () => { StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 0f, 6f, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "LevelOneSpeed 0",
                () => { StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(0f, 6f, 0f); });
            report.ExpectArgumentOutOfRange(
                "CurrentSpeed 0",
                () => { StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "GachaRate 음수",
                () => { StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, 6f, -0.1f); });
            report.ExpectArgumentOutOfRange(
                "FeverRate 음수",
                () => { StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, 6f, 0f, -0.1f); });
            report.ExpectArgumentOutOfRange(
                "BaseCookingEfficiencyRate 음수",
                () => { StaffRolePerformanceCalculator.CalculateChefRoleCookMultiplier(-0.1f, 6f, 6f, 0f); });
            report.ExpectArgumentOutOfRange(
                "CookingChannelMultiplier 0",
                () => { StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(0.5f, 6f, 6f, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "NaN",
                () => { StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, float.NaN, 0f); });
            report.ExpectArgumentOutOfRange(
                "PositiveInfinity",
                () => { StaffRolePerformanceCalculator.CalculateChefTotalCookMultiplier(0.5f, 6f, 6f, 0f, float.PositiveInfinity); });
            report.ExpectArgumentOutOfRange(
                "NegativeInfinity",
                () => { StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1f, 6f, float.NegativeInfinity, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "decimal 입력 범위 초과",
                () => { StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(6f, float.MaxValue, 0f); });
            report.ExpectArgumentOutOfRange(
                "decimal 계산 범위 초과",
                () => { StaffRolePerformanceCalculator.CalculateWaiterWorkDurationSeconds(1e20f, 1e20f, 1f, 0f, 0f); });
            report.ExpectArgumentOutOfRange(
                "decimal 계산 정밀도 미만",
                () => { StaffRolePerformanceCalculator.CalculatePermanentRoleWorkMultiplier(1e20f, 1e-20f, 0f); });
        }

        private static int RunSection(
            int sectionNumber,
            string sectionName,
            Action<SectionReport> validation,
            StringBuilder output)
        {
            SectionReport report = new SectionReport();
            try
            {
                validation(report);
            }
            catch (Exception exception)
            {
                report.AddError("예상하지 않은 예외: " + exception.GetType().Name + " - " + exception.Message);
            }

            output.AppendLine(
                sectionNumber.ToString(CultureInfo.InvariantCulture)
                + ". " + sectionName + ": " + (report.ErrorCount == 0 ? "PASS" : "FAIL"));

            for (int detailIndex = 0; detailIndex < report.Details.Count; detailIndex++)
            {
                output.AppendLine("   - " + report.Details[detailIndex]);
            }

            for (int errorIndex = 0; errorIndex < report.Errors.Count; errorIndex++)
            {
                output.AppendLine("   - ERROR: " + report.Errors[errorIndex]);
            }

            return report.ErrorCount;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }

        private sealed class SectionReport
        {
            public List<string> Details { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();
            public int ErrorCount => Errors.Count;
            public bool HasApiShapeError { get; private set; }

            public void AddDetail(string message)
            {
                Details.Add(message);
            }

            public void AddError(string message)
            {
                Errors.Add(message);
            }

            public void AddApiShapeError(string message)
            {
                HasApiShapeError = true;
                AddError(message);
            }

            public void AssertApproximately(string label, float expected, float actual)
            {
                AddDetail(label + ": expected=" + FormatFloat(expected) + ", actual=" + FormatFloat(actual));
                if (float.IsNaN(actual)
                    || float.IsInfinity(actual)
                    || Math.Abs(expected - actual) > ComparisonTolerance)
                {
                    AddError(label + " 값이 허용 오차 " + FormatFloat(ComparisonTolerance) + "를 벗어났습니다.");
                }
            }

            public void AssertCondition(bool condition, string successMessage, string errorMessage)
            {
                if (condition)
                {
                    AddDetail(successMessage);
                }
                else
                {
                    AddError(errorMessage);
                }
            }

            public void ExpectArgumentOutOfRange(string label, Action action)
            {
                try
                {
                    action();
                    AddError(label + " 입력에서 ArgumentOutOfRangeException이 발생하지 않았습니다.");
                }
                catch (ArgumentOutOfRangeException)
                {
                    AddDetail(label + ": ArgumentOutOfRangeException PASS");
                }
                catch (Exception exception)
                {
                    AddError(label + " 입력에서 다른 예외가 발생했습니다: " + exception.GetType().Name);
                }
            }
        }
    }
}
