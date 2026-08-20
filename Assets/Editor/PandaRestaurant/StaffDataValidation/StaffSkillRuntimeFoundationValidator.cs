using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffSkillRuntimeFoundationValidator
    {
        private const string MenuPath =
            "Tools/Panda Restaurant/Staff/Validate Skill Runtime Foundation";
        private const float FloatTolerance = 0.0001f;

        [MenuItem(MenuPath)]
        private static void Validate()
        {
            ValidationReport report = new ValidationReport();
            report.Run(1, "Source Token", ValidateSourceToken);
            report.Run(2, "Registry 기본 상태", ValidateRegistryInitialState);
            report.Run(3, "HIGHEST_ONLY", ValidateHighestOnly);
            report.Run(4, "Source 갱신", ValidateSourceUpdate);
            report.Run(5, "동일값 Source", ValidateEqualValueSources);
            report.Run(6, "Source 전체 제거", ValidateRemoveAllForSource);
            report.Run(7, "0% 제거", ValidateZeroPercentRemoval);
            report.Run(8, "ClearAll", ValidateClearAll);
            report.Run(9, "입력 예외", ValidateInvalidInputs);
            report.Run(10, "Multiplier", ValidateMultipliers);
            report.Run(11, "순서 독립", ValidateOrderIndependence);
            report.Run(12, "Context ID", ValidateContextIds);
            report.Run(13, "Activation Sequence", ValidateActivationSequence);
            report.Run(14, "Stale Token", ValidateStaleTokenSafety);
            report.Run(15, "Context 상태", ValidateContextState);
            report.Run(16, "Skill03 Timer", ValidateCustomerCallTimer);
            report.Run(17, "Context Percent", ValidateContextPercents);
            report.Run(18, "구조 불변성", ValidateStructure);
            report.Run(19, "비파괴", ValidateNonDestructiveDesign);
            report.Run(20, "Coordinator 정상 취소", ValidateCoordinatorCancellation);
            report.Run(21, "Coordinator 중복 취소", ValidateCoordinatorDuplicateCancellation);
            report.Run(22, "Deactivate 예외 정리", ValidateCoordinatorDeactivateException);
            report.Run(23, "Stale Token 정리", ValidateCoordinatorStaleToken);
            report.Run(24, "Null Action", ValidateCoordinatorNullAction);
            report.Run(25, "Coordinator 입력 예외", ValidateCoordinatorInputExceptions);
            report.Run(26, "GameManager Registry 소유 구조", ValidateGameManagerRegistryStructure);
            report.Run(27, "Staff Runtime Context 연결 구조", ValidateStaffRuntimeStructure);
            report.Run(28, "Skill01 PersonalMove Context", ValidateSkill01PersonalMoveContext);
            report.Run(29, "Skill03 Staff별 Timer", ValidateSkill03PerStaffTimer);
            report.Run(30, "Skill ScriptableObject 구조", ValidateSkillScriptableObjectStructure);
            report.Run(31, "Staff MoveSpeedMul 구조", ValidateStaffMoveSpeedStructure);
            report.Run(32, "Skill04 AssignedCooking Context", ValidateSkill04AssignedCookingContext);
            report.Run(33, "Skill04 조리 배율 Helper", ValidateSkill04CalculationHelper);
            report.Run(34, "Skill04 Class 구조", ValidateSkill04ClassStructure);
            report.Run(35, "Burner StaffWorking 구조", ValidateBurnerWorkingProperty);
            report.Run(36, "Skill04 비파괴 구조", ValidateSkill04NonDestructiveStructure);
            report.Run(37, "Skill06 Registry", ValidateSkill06Registry);
            report.Run(38, "Payment Tip Calculator", ValidatePaymentTipCalculator);
            report.Run(39, "Skill06 Class 구조", ValidateSkill06ClassStructure);
            report.Run(40, "Skill06 결제 Boundary", ValidateSkill06PaymentBoundary);
            report.Run(41, "Skill05 Registry", ValidateSkill05Registry);
            report.Run(42, "FoodPriceUpSkill Class 구조", ValidateSkill05ClassStructure);
            report.Run(43, "Food Price Getter 구조", ValidateSkill05FoodPriceGetterStructure);
            report.Run(44, "주문 가격 Boundary", ValidateSkill05OrderPriceBoundary);
            report.Run(45, "음식 기본 팁률 50%", ValidateBaseFoodTipPolicy);
            report.Run(46, "음식 팁 공식·격리 구조", ValidateFoodTipIsolationStructure);
            report.Run(47, "Fever Context·Token", ValidateFeverContextAndToken);
            report.Run(48, "Fever Clock·자동 호출", ValidateFeverClockAndAutoCall);
            report.Run(49, "GameManager Fever Context 소유", ValidateGameManagerFeverContextOwnership);
            report.Run(50, "FeverSystem 생명주기·Clock 구조", ValidateFeverSystemLifecycleStructure);
            report.Run(51, "Fever Gauge UI 실시간 감소", ValidateFeverGaugeRealtimeUi);
            report.Run(52, "Fever 구매 UI·결제 잠금", ValidateFeverPurchaseLock);
            report.Run(53, "Fever 배율 Calculator", ValidateFeverMultiplierCalculator);
            report.Run(54, "Legacy Fever Bridge 제거", ValidateLegacyFeverBridgeRemoval);
            report.Run(55, "Fever 음식가격 Context 경계", ValidateFeverFoodPriceBoundary);
            report.Run(56, "일반 손님 이동 채널", ValidateNormalCustomerMoveChannel);
            report.Run(57, "Staff 이동·역할 행동 채널", ValidateStaffMoveAndRoleActionChannels);
            report.Run(58, "조리 채널·최종 상한", ValidateCookingChannelAndCap);
            report.Print();
        }

        private static void ValidateSourceToken()
        {
            StaffSkillSourceToken token = new StaffSkillSourceToken(25, 4);
            StaffSkillSourceToken same = new StaffSkillSourceToken(25, 4);
            StaffSkillSourceToken otherSequence = new StaffSkillSourceToken(25, 5);
            StaffSkillSourceToken otherContext = new StaffSkillSourceToken(26, 4);
            Dictionary<StaffSkillSourceToken, string> values =
                new Dictionary<StaffSkillSourceToken, string>();

            values.Add(token, "source");
            Require(token.IsValid, "A token with positive values must be valid.");
            Require(token == same && token.Equals(same), "Equal tokens must compare equal.");
            Require(token.GetHashCode() == same.GetHashCode(), "Equal token hashes must match.");
            Require(token != otherSequence, "Different activation sequences must not compare equal.");
            Require(token != otherContext, "Different context IDs must not compare equal.");
            Require(values[same] == "source", "Token must be a stable dictionary key.");
            Require(token.ToString() == "25:4", "Token string must use the context:sequence format.");
            Require(!default(StaffSkillSourceToken).IsValid, "The default token must be invalid.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new StaffSkillSourceToken(0, 1),
                "A zero context ID must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new StaffSkillSourceToken(-1, 1),
                "A negative context ID must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new StaffSkillSourceToken(1, 0),
                "A zero sequence must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new StaffSkillSourceToken(1, -1),
                "A negative sequence must be rejected.");
        }

        private static void ValidateRegistryInitialState()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            foreach (StaffSkillEffectType effectType in Enum.GetValues(typeof(StaffSkillEffectType)))
            {
                RequireNear(0f, registry.GetHighestPercent(effectType), "Initial highest percent must be zero.");
                RequireNear(1f, registry.GetMultiplier(effectType), "Initial multiplier must be one.");
                Require(registry.GetSourceCount(effectType) == 0, "Initial source count must be zero.");
            }

            Require(registry.TotalSourceCount == 0, "Initial total source count must be zero.");
        }

        private static void ValidateHighestOnly()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken sourceA = new StaffSkillSourceToken(1, 1);
            StaffSkillSourceToken sourceB = new StaffSkillSourceToken(2, 1);
            StaffSkillEffectType type = StaffSkillEffectType.FoodPricePercent;

            registry.RegisterOrUpdate(type, sourceA, 30f, "A");
            RequireNear(30f, registry.GetHighestPercent(type), "Source A must produce 30 percent.");
            registry.RegisterOrUpdate(type, sourceB, 50f, "B");
            RequireNear(50f, registry.GetHighestPercent(type), "The highest source must win.");
            Require(registry.Remove(type, sourceB), "Removing source B must succeed.");
            RequireNear(30f, registry.GetHighestPercent(type), "Source A must resume after B is removed.");
            Require(registry.Remove(type, sourceA), "Removing source A must succeed.");
            RequireNear(0f, registry.GetHighestPercent(type), "No sources must produce zero percent.");
            Require(!registry.Remove(type, sourceA), "Removing an absent source must be a no-op.");
        }

        private static void ValidateSourceUpdate()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken source = new StaffSkillSourceToken(3, 1);
            StaffSkillEffectType type = StaffSkillEffectType.RestaurantTipPayoutPercent;

            registry.RegisterOrUpdate(type, source, 30f, "before");
            registry.RegisterOrUpdate(type, source, 60f, "after");
            RequireNear(60f, registry.GetHighestPercent(type), "Updating a source must replace its percent.");
            Require(registry.GetSourceCount(type) == 1, "Updating a source must not add an entry.");
            Require(registry.TotalSourceCount == 1, "Updating a source must preserve the total count.");
            Require(registry.ContainsSource(type, source), "Updated source must remain registered.");
        }

        private static void ValidateEqualValueSources()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken sourceA = new StaffSkillSourceToken(4, 1);
            StaffSkillSourceToken sourceB = new StaffSkillSourceToken(5, 1);
            StaffSkillEffectType type = StaffSkillEffectType.NormalCustomerMovePercent;

            registry.RegisterOrUpdate(type, sourceA, 50f, "A");
            registry.RegisterOrUpdate(type, sourceB, 50f, "B");
            Require(registry.GetSourceCount(type) == 2, "Equal-value sources must both be retained.");
            registry.Remove(type, sourceA);
            RequireNear(50f, registry.GetHighestPercent(type), "Removing A must leave B active.");
            Require(registry.GetSourceCount(type) == 1, "One equal-value source must remain.");
            registry.Remove(type, sourceB);
            RequireNear(0f, registry.GetHighestPercent(type), "Removing both sources must return zero.");
        }

        private static void ValidateRemoveAllForSource()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken target = new StaffSkillSourceToken(6, 1);
            StaffSkillSourceToken other = new StaffSkillSourceToken(7, 1);

            registry.RegisterOrUpdate(StaffSkillEffectType.FoodPricePercent, target, 50f, "target");
            registry.RegisterOrUpdate(StaffSkillEffectType.GlobalCookingSpeedPercent, target, 50f, "target");
            registry.RegisterOrUpdate(StaffSkillEffectType.AllStaffMovePercent, target, 50f, "target");
            registry.RegisterOrUpdate(StaffSkillEffectType.FoodPricePercent, other, 30f, "other");

            Require(registry.RemoveAllForSource(target) == 3, "Target source must be removed from every effect.");
            Require(!registry.ContainsSource(StaffSkillEffectType.FoodPricePercent, target), "Target must be absent.");
            Require(!registry.ContainsSource(StaffSkillEffectType.GlobalCookingSpeedPercent, target), "Target must be absent.");
            Require(!registry.ContainsSource(StaffSkillEffectType.AllStaffMovePercent, target), "Target must be absent.");
            Require(registry.ContainsSource(StaffSkillEffectType.FoodPricePercent, other), "Other source must remain.");
            Require(registry.RemoveAllForSource(target) == 0, "Removing an absent source must return zero.");
        }

        private static void ValidateZeroPercentRemoval()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken source = new StaffSkillSourceToken(8, 1);
            StaffSkillEffectType type = StaffSkillEffectType.AllStaffMovePercent;

            registry.RegisterOrUpdate(type, source, 50f, "source");
            registry.RegisterOrUpdate(type, source, 0f, "source");
            Require(!registry.ContainsSource(type, source), "A zero-percent update must remove the source.");
            Require(registry.GetSourceCount(type) == 0, "Zero-percent removal must reduce the count.");
            Require(registry.TotalSourceCount == 0, "Zero-percent removal must reduce the total count.");
            registry.RegisterOrUpdate(type, source, 0f, "source");
            Require(registry.TotalSourceCount == 0, "Registering an absent zero-percent source must be a no-op.");
        }

        private static void ValidateClearAll()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken sourceA = new StaffSkillSourceToken(9, 1);
            StaffSkillSourceToken sourceB = new StaffSkillSourceToken(10, 1);

            registry.RegisterOrUpdate(StaffSkillEffectType.FoodPricePercent, sourceA, 50f, "A");
            registry.RegisterOrUpdate(StaffSkillEffectType.RestaurantTipPayoutPercent, sourceB, 50f, "B");
            registry.ClearAll();
            registry.ClearAll();
            Require(registry.TotalSourceCount == 0, "ClearAll must remove every source and be repeatable.");
            foreach (StaffSkillEffectType effectType in Enum.GetValues(typeof(StaffSkillEffectType)))
            {
                RequireNear(0f, registry.GetHighestPercent(effectType), "ClearAll must reset every effect.");
            }
        }

        private static void ValidateInvalidInputs()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken valid = new StaffSkillSourceToken(11, 1);
            StaffSkillEffectType type = StaffSkillEffectType.FoodPricePercent;
            registry.RegisterOrUpdate(type, valid, 40f, "baseline");

            AssertRegistryUnchangedAfterException(
                registry,
                type,
                () => registry.RegisterOrUpdate(type, default(StaffSkillSourceToken), 50f, "invalid"),
                "Invalid tokens must be rejected.");
            AssertRegistryUnchangedAfterException(
                registry,
                type,
                () => registry.RegisterOrUpdate(type, valid, -1f, "negative"),
                "Negative percentages must be rejected.");
            AssertRegistryUnchangedAfterException(
                registry,
                type,
                () => registry.RegisterOrUpdate(type, valid, float.NaN, "nan"),
                "NaN percentages must be rejected.");
            AssertRegistryUnchangedAfterException(
                registry,
                type,
                () => registry.RegisterOrUpdate(type, valid, float.PositiveInfinity, "positive infinity"),
                "Positive infinity must be rejected.");
            AssertRegistryUnchangedAfterException(
                registry,
                type,
                () => registry.RegisterOrUpdate(type, valid, float.NegativeInfinity, "negative infinity"),
                "Negative infinity must be rejected.");
            AssertRegistryUnchangedAfterException(
                registry,
                type,
                () => registry.RegisterOrUpdate((StaffSkillEffectType)999, valid, 50f, "undefined"),
                "Undefined effect types must be rejected.");
        }

        private static void ValidateMultipliers()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillSourceToken source = new StaffSkillSourceToken(12, 1);
            StaffSkillEffectType type = StaffSkillEffectType.GlobalCookingSpeedPercent;

            registry.RegisterOrUpdate(type, source, 50f, "50");
            RequireNear(1.5f, registry.GetMultiplier(type), "50 percent must produce 1.5.");
            registry.RegisterOrUpdate(type, source, 100f, "100");
            RequireNear(2f, registry.GetMultiplier(type), "100 percent must produce 2.0.");
            registry.RegisterOrUpdate(type, source, 150f, "150");
            RequireNear(2.5f, registry.GetMultiplier(type), "150 percent must produce 2.5.");
        }

        private static void ValidateOrderIndependence()
        {
            StaffSkillSourceToken sourceA = new StaffSkillSourceToken(13, 1);
            StaffSkillSourceToken sourceB = new StaffSkillSourceToken(14, 1);
            StaffSkillSourceToken sourceC = new StaffSkillSourceToken(15, 1);
            StaffSkillEffectType type = StaffSkillEffectType.NormalCustomerMovePercent;
            StaffSkillEffectRegistry first = new StaffSkillEffectRegistry();
            StaffSkillEffectRegistry second = new StaffSkillEffectRegistry();

            first.RegisterOrUpdate(type, sourceA, 30f, "A");
            first.RegisterOrUpdate(type, sourceB, 50f, "B");
            first.RegisterOrUpdate(type, sourceC, 40f, "C");
            first.Remove(type, sourceA);

            second.RegisterOrUpdate(type, sourceC, 40f, "C");
            second.RegisterOrUpdate(type, sourceA, 30f, "A");
            second.RegisterOrUpdate(type, sourceB, 50f, "B");
            second.Remove(type, sourceA);

            RequireNear(
                first.GetHighestPercent(type),
                second.GetHighestPercent(type),
                "Registration order must not affect the highest percent.");
            Require(
                first.GetSourceCount(type) == second.GetSourceCount(type),
                "Registration order must not affect the source count.");
            Require(first.TotalSourceCount == second.TotalSourceCount, "Total counts must match.");
        }

        private static void ValidateContextIds()
        {
            HashSet<long> ids = new HashSet<long>();
            for (int index = 0; index < 8; index++)
            {
                StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
                Require(context.RuntimeContextId > 0, "Context IDs must be positive.");
                Require(ids.Add(context.RuntimeContextId), "Context IDs must be unique.");
            }
        }

        private static void ValidateActivationSequence()
        {
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken first = context.BeginActivation("first");
            Require(first.ActivationSequence >= 1, "The first sequence must be positive.");
            Cancel(context, first);

            long afterFirst = context.ActivationSequence;
            StaffSkillSourceToken second = context.BeginActivation("second");
            Require(second.ActivationSequence == afterFirst + 1, "The next sequence must increase.");
            Require(second != first, "Consecutive activations must have different tokens.");
            Cancel(context, second);

            long beforeReset = context.ActivationSequence;
            context.ResetLocalState();
            context.ResetLocalState();
            Require(context.ActivationSequence == beforeReset, "Reset must not change the sequence.");
            StaffSkillSourceToken third = context.BeginActivation("third");
            Require(third.ActivationSequence == beforeReset + 1, "Activation after reset must keep increasing.");
        }

        private static void ValidateStaleTokenSafety()
        {
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken oldToken = context.BeginActivation("old");
            context.SetPersonalMoveBonusPercent(oldToken, 25f);
            Cancel(context, oldToken);

            StaffSkillSourceToken currentToken = context.BeginActivation("current");
            context.SetPersonalMoveBonusPercent(currentToken, 100f);
            context.SetAssignedCookingBonusPercent(currentToken, 150f);
            context.AdvanceCustomerCallTimer(currentToken, 0.2f, 0.5f);

            Require(!context.TryBeginCancellation(oldToken), "A stale token must not start cancellation.");
            context.MarkDeactivationCompleted(oldToken);
            context.CompleteCancellation(oldToken);
            context.SetPersonalMoveBonusPercent(oldToken, 10f);
            context.SetAssignedCookingBonusPercent(oldToken, 10f);
            Require(context.AdvanceCustomerCallTimer(oldToken, 1f, 0.5f) == 0, "A stale timer update must no-op.");

            Require(context.IsCurrentToken(currentToken), "Stale operations must leave the current token active.");
            RequireNear(100f, context.PersonalMoveBonusPercent, "Stale operations must not change move percent.");
            RequireNear(150f, context.AssignedCookingBonusPercent, "Stale operations must not change cooking percent.");
            RequireNear(0.2f, context.CustomerCallElapsedTime, "Stale operations must not change the timer.");
        }

        private static void ValidateContextState()
        {
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            Require(!context.IsActive, "A new context must be inactive.");
            Require(!context.CurrentActivationToken.IsValid, "A new context must have no token.");

            StaffSkillSourceToken token = context.BeginActivation("STAFF01/STAFF_SKILL01");
            Require(context.IsActive, "BeginActivation must activate the context.");
            Require(!context.IsCancelling, "A new activation must not be cancelling.");
            Require(!context.DeactivationCompleted, "A new activation must not be deactivated.");
            Require(context.ActiveSkillDebugId == "STAFF01/STAFF_SKILL01", "Debug ID must be stored.");
            Require(context.IsCurrentToken(token), "The new token must be current.");
            RequireThrows<InvalidOperationException>(
                () => context.BeginActivation("duplicate"),
                "A second active activation must be rejected.");

            Require(context.TryBeginCancellation(token), "Current token must start cancellation.");
            Require(context.IsCancelling, "Cancellation flag must be set.");
            Require(!context.TryBeginCancellation(token), "Duplicate cancellation must be a no-op.");
            context.MarkDeactivationCompleted(token);
            context.MarkDeactivationCompleted(token);
            Require(context.DeactivationCompleted, "Deactivation completion must be recorded.");
            context.CompleteCancellation(token);
            context.CompleteCancellation(token);
            Require(!context.IsActive, "Completion must deactivate the context.");
            Require(!context.IsCancelling, "Completion must clear cancellation.");
            Require(!context.DeactivationCompleted, "Completion must clear temporary flags.");
            Require(!context.CurrentActivationToken.IsValid, "Completion must clear the token.");
            Require(context.ActiveSkillDebugId == string.Empty, "Completion must clear the debug ID.");
        }

        private static void ValidateCustomerCallTimer()
        {
            StaffSkillRuntimeContext contextA = new StaffSkillRuntimeContext();
            StaffSkillRuntimeContext contextB = new StaffSkillRuntimeContext();
            StaffSkillSourceToken tokenA = contextA.BeginActivation("A");
            StaffSkillSourceToken tokenB = contextB.BeginActivation("B");

            Require(contextA.AdvanceCustomerCallTimer(tokenA, 0.2f, 0.5f) == 0, "0.2 seconds must not trigger.");
            Require(contextB.AdvanceCustomerCallTimer(tokenB, 0.1f, 0.5f) == 0, "Context B must be independent.");
            Require(contextA.AdvanceCustomerCallTimer(tokenA, 0.3f, 0.5f) == 1, "0.2 + 0.3 must trigger once.");
            RequireNear(0f, contextA.CustomerCallElapsedTime, "A complete interval must leave no remainder.");
            RequireNear(0.1f, contextB.CustomerCallElapsedTime, "Context B timer must remain unchanged.");
            Require(contextA.AdvanceCustomerCallTimer(tokenA, 1.2f, 0.5f) == 2, "1.2 seconds must trigger twice.");
            RequireNear(0.2f, contextA.CustomerCallElapsedTime, "1.2 seconds must preserve a 0.2 remainder.");

            float beforeInvalid = contextA.CustomerCallElapsedTime;
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, -0.1f, 0.5f),
                "Negative delta must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, float.NaN, 0.5f),
                "NaN delta must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, float.PositiveInfinity, 0.5f),
                "Infinite delta must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, 0.1f, 0f),
                "Zero interval must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, 0.1f, -0.5f),
                "Negative interval must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, 0.1f, float.NaN),
                "NaN interval must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => contextA.AdvanceCustomerCallTimer(tokenA, 0.1f, float.PositiveInfinity),
                "Infinite interval must be rejected.");
            RequireNear(beforeInvalid, contextA.CustomerCallElapsedTime, "Invalid inputs must not change the timer.");

            long sequence = contextA.ActivationSequence;
            contextA.ResetLocalState();
            RequireNear(0f, contextA.CustomerCallElapsedTime, "Reset must clear the timer.");
            Require(contextA.ActivationSequence == sequence, "Reset must preserve the sequence.");
        }

        private static void ValidateContextPercents()
        {
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken first = context.BeginActivation("percent");
            context.SetPersonalMoveBonusPercent(first, 100f);
            context.SetAssignedCookingBonusPercent(first, 150f);
            RequireNear(100f, context.PersonalMoveBonusPercent, "Personal move percent must be stored.");
            RequireNear(150f, context.AssignedCookingBonusPercent, "Assigned cooking percent must be stored.");

            AssertPercentUnchangedAfterException(
                context,
                () => context.SetPersonalMoveBonusPercent(first, -1f),
                "Negative personal percent must be rejected.");
            AssertPercentUnchangedAfterException(
                context,
                () => context.SetPersonalMoveBonusPercent(first, float.NaN),
                "NaN personal percent must be rejected.");
            AssertPercentUnchangedAfterException(
                context,
                () => context.SetAssignedCookingBonusPercent(first, float.PositiveInfinity),
                "Infinite cooking percent must be rejected.");
            AssertPercentUnchangedAfterException(
                context,
                () => context.SetAssignedCookingBonusPercent(first, float.NegativeInfinity),
                "Negative infinite cooking percent must be rejected.");

            Cancel(context, first);
            RequireNear(0f, context.PersonalMoveBonusPercent, "Cancellation must clear personal move percent.");
            RequireNear(0f, context.AssignedCookingBonusPercent, "Cancellation must clear cooking percent.");

            StaffSkillSourceToken second = context.BeginActivation("new");
            context.SetPersonalMoveBonusPercent(second, 40f);
            context.SetAssignedCookingBonusPercent(second, 60f);
            context.SetPersonalMoveBonusPercent(first, 5f);
            context.SetAssignedCookingBonusPercent(first, 5f);
            RequireNear(40f, context.PersonalMoveBonusPercent, "Stale token must not change personal percent.");
            RequireNear(60f, context.AssignedCookingBonusPercent, "Stale token must not change cooking percent.");
            context.SetPersonalMoveBonusPercent(second, 0f);
            context.SetAssignedCookingBonusPercent(second, 0f);
            RequireNear(0f, context.PersonalMoveBonusPercent, "Zero personal percent must be allowed.");
            RequireNear(0f, context.AssignedCookingBonusPercent, "Zero cooking percent must be allowed.");
        }

        private static void ValidateStructure()
        {
            Type[] runtimeTypes =
            {
                typeof(StaffSkillSourceToken),
                typeof(StaffSkillEffectRegistry),
                typeof(StaffSkillRuntimeContext)
            };

            foreach (Type type in runtimeTypes)
            {
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                foreach (PropertyInfo property in properties)
                {
                    MethodInfo setter = property.GetSetMethod(false);
                    Require(setter == null, type.Name + "." + property.Name + " must not have a public setter.");
                    Require(!IsMutableCollectionType(property.PropertyType), type.Name + " must not expose mutable collections.");
                }

                FieldInfo[] publicFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                foreach (FieldInfo field in publicFields)
                {
                    Require(field.IsInitOnly || field.IsLiteral, type.Name + "." + field.Name + " must be immutable.");
                }

                FieldInfo[] allFields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in allFields)
                {
                    Require(
                        !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType),
                        type.Name + "." + field.Name + " must not store a Unity object.");
                    Require(!IsForbiddenRuntimeType(field.FieldType), type.Name + "." + field.Name + " stores a forbidden type.");
                }

                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                foreach (MethodInfo method in methods)
                {
                    Require(!IsMutableCollectionType(method.ReturnType), type.Name + "." + method.Name + " must not return mutable collections.");
                }
            }
        }

        private static void ValidateNonDestructiveDesign()
        {
            Type[] pureTypes =
            {
                typeof(StaffSkillSourceToken),
                typeof(StaffSkillEffectType),
                typeof(StaffSkillEffectRegistry),
                typeof(StaffSkillRuntimeContext),
                typeof(StaffSkillCancellationReason),
                typeof(StaffSkillCancellationCoordinator)
            };

            foreach (Type type in pureTypes)
            {
                Require(!typeof(UnityEngine.Object).IsAssignableFrom(type), type.Name + " must remain a pure type.");
                Require(type.Assembly == typeof(StaffSkillSourceToken).Assembly, type.Name + " must remain in the Runtime assembly.");
            }

            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            Require(registry.TotalSourceCount == 0, "Pure construction must not create effect state.");
            Require(!context.IsActive, "Pure construction must not start an activation.");
        }

        private static void ValidateCoordinatorCancellation()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken token = context.BeginActivation("normal-cancellation");
            context.SetPersonalMoveBonusPercent(token, 100f);
            context.SetAssignedCookingBonusPercent(token, 150f);
            context.AdvanceCustomerCallTimer(token, 0.25f, 0.5f);
            registry.RegisterOrUpdate(
                StaffSkillEffectType.AllStaffMovePercent,
                token,
                50f,
                "normal-cancellation");
            registry.RegisterOrUpdate(
                StaffSkillEffectType.FoodPricePercent,
                token,
                50f,
                "normal-cancellation");

            int deactivateCount = 0;
            Exception deactivateException;
            bool cancelled = StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                token,
                () => deactivateCount++,
                out deactivateException);

            Require(cancelled, "The current token must be cancelled.");
            Require(deactivateCount == 1, "Deactivate must run exactly once.");
            Require(deactivateException == null, "Successful Deactivate must not return an exception.");
            Require(registry.TotalSourceCount == 0, "Cancellation must remove every registry source for the token.");
            Require(!context.IsActive, "Cancellation must leave the context inactive.");
            Require(!context.CurrentActivationToken.IsValid, "Cancellation must invalidate the current token.");
            RequireNear(0f, context.PersonalMoveBonusPercent, "Cancellation must clear personal move percent.");
            RequireNear(0f, context.AssignedCookingBonusPercent, "Cancellation must clear cooking percent.");
            RequireNear(0f, context.CustomerCallElapsedTime, "Cancellation must clear the customer call timer.");
        }

        private static void ValidateCoordinatorDuplicateCancellation()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken token = context.BeginActivation("duplicate-cancellation");
            registry.RegisterOrUpdate(
                StaffSkillEffectType.GlobalCookingSpeedPercent,
                token,
                50f,
                "duplicate-cancellation");

            int deactivateCount = 0;
            Exception firstException;
            Exception secondException;
            bool firstCancelled = StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                token,
                () => deactivateCount++,
                out firstException);
            bool secondCancelled = StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                token,
                () => deactivateCount++,
                out secondException);

            Require(firstCancelled, "The first cancellation must succeed.");
            Require(!secondCancelled, "The duplicate cancellation must be a safe no-op.");
            Require(deactivateCount == 1, "Duplicate cancellation must not repeat Deactivate.");
            Require(firstException == null && secondException == null, "Duplicate cancellation must not return an exception.");
            Require(registry.TotalSourceCount == 0, "Duplicate cancellation must leave the registry empty.");
            Require(!context.IsActive, "Duplicate cancellation must leave the context inactive.");
        }

        private static void ValidateCoordinatorDeactivateException()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken token = context.BeginActivation("deactivate-exception");
            context.SetPersonalMoveBonusPercent(token, 100f);
            registry.RegisterOrUpdate(
                StaffSkillEffectType.NormalCustomerMovePercent,
                token,
                100f,
                "deactivate-exception");
            InvalidOperationException expectedException =
                new InvalidOperationException("Expected Deactivate failure.");

            Exception deactivateException;
            bool cancelled = StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                token,
                () => { throw expectedException; },
                out deactivateException);

            Require(cancelled, "Cancellation must complete when Deactivate throws.");
            Require(ReferenceEquals(expectedException, deactivateException), "The Deactivate exception must be returned unchanged.");
            Require(registry.TotalSourceCount == 0, "Deactivate failure must not leave registry sources.");
            Require(!context.IsActive, "Deactivate failure must not leave the context active.");
            RequireNear(0f, context.PersonalMoveBonusPercent, "Deactivate failure must clear temporary context values.");
        }

        private static void ValidateCoordinatorStaleToken()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken tokenA = context.BeginActivation("activation-a");
            Exception firstException;
            StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                tokenA,
                null,
                out firstException);
            Require(firstException == null, "Activation A cleanup must not return an exception.");

            StaffSkillSourceToken tokenB = context.BeginActivation("activation-b");
            registry.RegisterOrUpdate(
                StaffSkillEffectType.FoodPricePercent,
                tokenA,
                40f,
                "stale-a");
            registry.RegisterOrUpdate(
                StaffSkillEffectType.FoodPricePercent,
                tokenB,
                50f,
                "current-b");

            int staleDeactivateCount = 0;
            Exception staleException;
            bool staleCancelled = StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                tokenA,
                () => staleDeactivateCount++,
                out staleException);

            Require(!staleCancelled, "A stale token must not cancel the current activation.");
            Require(staleDeactivateCount == 0, "A stale token must not run Deactivate.");
            Require(staleException == null, "A stale cancellation must not return an exception.");
            Require(!registry.ContainsSource(StaffSkillEffectType.FoodPricePercent, tokenA), "A stale source must be removed.");
            Require(registry.ContainsSource(StaffSkillEffectType.FoodPricePercent, tokenB), "The current source must remain registered.");
            Require(context.IsCurrentToken(tokenB), "Activation B must remain current and active.");

            Exception cleanupException;
            StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                tokenB,
                null,
                out cleanupException);
            Require(cleanupException == null, "Activation B cleanup must not return an exception.");
        }

        private static void ValidateCoordinatorNullAction()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken token = context.BeginActivation("null-action");
            registry.RegisterOrUpdate(
                StaffSkillEffectType.RestaurantTipPayoutPercent,
                token,
                50f,
                "null-action");

            Exception deactivateException;
            bool cancelled = StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                token,
                null,
                out deactivateException);

            Require(cancelled, "A null action must still cancel the current token.");
            Require(deactivateException == null, "A null action must not create an exception.");
            Require(registry.TotalSourceCount == 0, "A null action must still clear registry sources.");
            Require(!context.IsActive, "A null action must still clear the context.");
        }

        private static void ValidateCoordinatorInputExceptions()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken token = context.BeginActivation("input-exceptions");
            registry.RegisterOrUpdate(
                StaffSkillEffectType.AllStaffMovePercent,
                token,
                50f,
                "input-exceptions");
            Exception ignoredException = null;

            RequireThrows<ArgumentNullException>(
                () => StaffSkillCancellationCoordinator.TryCancel(
                    null,
                    registry,
                    token,
                    null,
                    out ignoredException),
                "A null context must be rejected.");
            RequireThrows<ArgumentNullException>(
                () => StaffSkillCancellationCoordinator.TryCancel(
                    context,
                    null,
                    token,
                    null,
                    out ignoredException),
                "A null registry must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => StaffSkillCancellationCoordinator.TryCancel(
                    context,
                    registry,
                    default(StaffSkillSourceToken),
                    null,
                    out ignoredException),
                "An invalid token must be rejected.");

            Require(context.IsCurrentToken(token), "Invalid inputs must not change the active context.");
            Require(registry.ContainsSource(StaffSkillEffectType.AllStaffMovePercent, token), "Invalid inputs must not change registry state.");

            Exception cleanupException;
            StaffSkillCancellationCoordinator.TryCancel(
                context,
                registry,
                token,
                null,
                out cleanupException);
            Require(cleanupException == null, "Input test cleanup must not return an exception.");
        }

        private static void ValidateGameManagerRegistryStructure()
        {
            PropertyInfo registryProperty = typeof(GameManager).GetProperty(
                "StaffSkillEffectRegistry",
                BindingFlags.Instance | BindingFlags.Public);
            Require(registryProperty != null, "GameManager.StaffSkillEffectRegistry must exist.");
            Require(registryProperty.PropertyType == typeof(StaffSkillEffectRegistry), "GameManager Registry property type is incorrect.");
            Require(registryProperty.GetSetMethod(true) == null, "GameManager Registry property must not have a setter.");

            MethodInfo tryGetMethod = typeof(GameManager).GetMethod(
                "TryGetExistingInstance",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(GameManager).MakeByRefType() },
                null);
            Require(tryGetMethod != null, "GameManager.TryGetExistingInstance must exist.");
            Require(tryGetMethod.ReturnType == typeof(bool), "TryGetExistingInstance must return bool.");

            FieldInfo[] fields = typeof(GameManager).GetFields(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                Require(
                    !(field.IsStatic && field.FieldType == typeof(StaffSkillEffectRegistry)),
                    "GameManager must not store the Registry in a static field.");
            }
        }

        private static void ValidateStaffRuntimeStructure()
        {
            ValidateReadOnlyProperty(
                typeof(Staff),
                "RuntimeSkillContext",
                typeof(StaffSkillRuntimeContext));
            ValidateReadOnlyProperty(
                typeof(Staff),
                "SkillEffectRegistry",
                typeof(StaffSkillEffectRegistry));
            ValidateReadOnlyProperty(
                typeof(Staff),
                "CurrentSkillSourceToken",
                typeof(StaffSkillSourceToken));

            FieldInfo contextField = typeof(Staff).GetField(
                "_skillRuntimeContext",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(contextField != null, "Staff must own a Runtime Context field.");
            Require(contextField.FieldType == typeof(StaffSkillRuntimeContext), "Staff Runtime Context field type is incorrect.");
            Require(contextField.IsInitOnly, "Staff Runtime Context field must remain readonly.");
        }

        private static void ValidateSkill01PersonalMoveContext()
        {
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken first = context.BeginActivation("STAFF21/STAFF_SKILL01");

            context.SetPersonalMoveBonusPercent(first, 100f);
            RequireNear(100f, context.PersonalMoveBonusPercent, "Skill01 must register a 100 percent personal move bonus.");
            RequireNear(
                2f,
                1f + context.PersonalMoveBonusPercent * 0.01f,
                "A 100 percent personal move bonus must mean a 2.0 multiplier.");

            context.SetPersonalMoveBonusPercent(first, 0f);
            RequireNear(0f, context.PersonalMoveBonusPercent, "Skill01 deactivation must register zero percent.");

            context.SetPersonalMoveBonusPercent(first, 100f);
            Cancel(context, first);
            RequireNear(0f, context.PersonalMoveBonusPercent, "Cancellation must clear the Skill01 personal move bonus.");

            StaffSkillSourceToken second = context.BeginActivation("STAFF21/STAFF_SKILL01/reassigned");
            context.SetPersonalMoveBonusPercent(second, 25f);
            context.SetPersonalMoveBonusPercent(first, 100f);
            RequireNear(25f, context.PersonalMoveBonusPercent, "A stale token must not change the Skill01 personal move bonus.");
            Cancel(context, second);
        }

        private static void ValidateSkill03PerStaffTimer()
        {
            MethodInfo advanceTimerMethod = typeof(StaffSkillRuntimeContext).GetMethod(
                "AdvanceCustomerCallTimer",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(StaffSkillSourceToken), typeof(float), typeof(float) },
                null);
            Require(advanceTimerMethod != null, "Skill03 timer API must accept only token, delta time, and interval.");

            StaffSkillRuntimeContext contextA = new StaffSkillRuntimeContext();
            StaffSkillRuntimeContext contextB = new StaffSkillRuntimeContext();
            StaffSkillSourceToken tokenA = contextA.BeginActivation("STAFF11/STAFF_SKILL03/A");
            StaffSkillSourceToken tokenB = contextB.BeginActivation("STAFF11/STAFF_SKILL03/B");

            Require(contextA.AdvanceCustomerCallTimer(tokenA, 0.2f, 0.5f) == 0, "Skill03 must preserve the first 0.2 seconds.");
            Require(contextA.AdvanceCustomerCallTimer(tokenA, 0.3f, 0.5f) == 1, "Skill03 0.2 + 0.3 seconds must trigger once.");
            RequireNear(0f, contextA.CustomerCallElapsedTime, "A complete Skill03 interval must leave no remainder.");
            RequireNear(0f, contextB.CustomerCallElapsedTime, "A second Staff timer must remain independent.");

            Require(contextA.AdvanceCustomerCallTimer(tokenA, 1.2f, 0.5f) == 2, "Skill03 1.2 seconds must trigger twice.");
            RequireNear(0.2f, contextA.CustomerCallElapsedTime, "Skill03 must preserve the 0.2 second remainder.");

            Cancel(contextA, tokenA);
            RequireNear(0f, contextA.CustomerCallElapsedTime, "Skill03 cancellation must clear the timer.");
            StaffSkillSourceToken nextTokenA = contextA.BeginActivation("STAFF11/STAFF_SKILL03/A2");
            Require(contextA.AdvanceCustomerCallTimer(nextTokenA, 0.1f, 0.5f) == 0, "A new Skill03 activation must start independently.");
            Require(contextA.AdvanceCustomerCallTimer(tokenA, 1f, 0.5f) == 0, "A stale Skill03 token must trigger zero calls.");
            RequireNear(0.1f, contextA.CustomerCallElapsedTime, "A stale Skill03 token must not change the current timer.");
            Cancel(contextA, nextTokenA);
            RequireNear(0f, contextA.CustomerCallElapsedTime, "Skill03 cancellation must leave the timer at zero.");
            Cancel(contextB, tokenB);
        }

        private static void ValidateSkillScriptableObjectStructure()
        {
            ValidateDeclaredInstanceFields(typeof(SpeedUpSkill), "_speedUpMul");
            ValidateDeclaredInstanceFields(typeof(TouchAddCustomerButtonSkill), "_touchInterval");

            FieldInfo timerField = typeof(TouchAddCustomerButtonSkill).GetField(
                "_timer",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(timerField == null, "TouchAddCustomerButtonSkill must not store a shared _timer field.");
        }

        private static void ValidateStaffMoveSpeedStructure()
        {
            ValidateReadOnlyProperty(
                typeof(Staff),
                "RuntimeSkillContext",
                typeof(StaffSkillRuntimeContext));
            ValidateReadOnlyProperty(
                typeof(Staff),
                "CurrentSkillSourceToken",
                typeof(StaffSkillSourceToken));
            ValidateReadOnlyProperty(
                typeof(Staff),
                "MoveSpeedMul",
                typeof(float));
        }

        private static void ValidateSkill04AssignedCookingContext()
        {
            StaffSkillRuntimeContext context = new StaffSkillRuntimeContext();
            StaffSkillSourceToken firstToken = context.BeginActivation("STAFF_SKILL04");

            context.SetAssignedCookingBonusPercent(firstToken, 150f);
            RequireNear(
                150f,
                context.AssignedCookingBonusPercent,
                "Skill04 must register a 150 percent assigned cooking bonus.");
            RequireNear(
                2.5f,
                1f + context.AssignedCookingBonusPercent * 0.01f,
                "Skill04 150 percent must mean a 2.5 multiplier.");

            StaffSkillRuntimeContext independentContext = new StaffSkillRuntimeContext();
            StaffSkillSourceToken independentToken = independentContext.BeginActivation("STAFF_SKILL04_INDEPENDENT");
            independentContext.SetAssignedCookingBonusPercent(independentToken, 75f);
            RequireNear(
                150f,
                context.AssignedCookingBonusPercent,
                "A second Context must not change the first Skill04 Context.");
            RequireNear(
                75f,
                independentContext.AssignedCookingBonusPercent,
                "Skill04 Context values must remain independent.");
            Cancel(independentContext, independentToken);

            context.SetAssignedCookingBonusPercent(firstToken, 0f);
            RequireNear(
                0f,
                context.AssignedCookingBonusPercent,
                "Skill04 deactivation must register zero percent.");

            Cancel(context, firstToken);
            RequireNear(
                0f,
                context.AssignedCookingBonusPercent,
                "Skill04 cancellation must leave the assigned cooking bonus at zero.");

            StaffSkillSourceToken secondToken = context.BeginActivation("STAFF_SKILL04_NEXT");
            context.SetAssignedCookingBonusPercent(secondToken, 40f);
            context.SetAssignedCookingBonusPercent(firstToken, 150f);
            RequireNear(
                40f,
                context.AssignedCookingBonusPercent,
                "A stale token must not change the Skill04 assigned cooking bonus.");
            Cancel(context, secondToken);
        }

        private static void ValidateSkill04CalculationHelper()
        {
            MethodInfo method = typeof(KitchenUtensilGroup).GetMethod(
                "CalculateAssignedCookingSpeedMultiplier",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "Skill04 cooking multiplier helper is missing.");
            Require(method.ReturnType == typeof(float), "Skill04 cooking multiplier helper must return float.");

            ParameterInfo[] parameters = method.GetParameters();
            Require(parameters.Length == 3, "Skill04 cooking multiplier helper must have three parameters.");
            Require(parameters[0].ParameterType == typeof(bool), "Skill04 helper parameter 1 must be bool.");
            Require(parameters[1].ParameterType == typeof(bool), "Skill04 helper parameter 2 must be bool.");
            Require(parameters[2].ParameterType == typeof(float), "Skill04 helper parameter 3 must be float.");

            RequireNear(1f, InvokeSkill04Multiplier(method, false, false, 150f), "No assigned Staff must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, false, true, 150f), "Working without an assigned Staff must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, false, 150f), "A moving Staff must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, 0f), "Zero percent must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, -1f), "A negative percent must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, float.NaN), "NaN must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, float.PositiveInfinity), "Positive infinity must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, float.NegativeInfinity), "Negative infinity must produce 1.0.");
            RequireNear(2.5f, InvokeSkill04Multiplier(method, true, true, 150f), "150 percent must produce 2.5.");
            RequireNear(11f, InvokeSkill04Multiplier(method, true, true, 1000f), "1000 percent must produce 11.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, 1000.01f), "An out-of-range finite percent must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, 1001f), "1001 percent must produce 1.0.");
            RequireNear(1f, InvokeSkill04Multiplier(method, true, true, float.MaxValue), "float.MaxValue must produce 1.0.");
        }

        private static float InvokeSkill04Multiplier(
            MethodInfo method,
            bool hasAssignedStaff,
            bool isStaffWorking,
            float assignedCookingBonusPercent)
        {
            object result = method.Invoke(
                null,
                new object[] { hasAssignedStaff, isStaffWorking, assignedCookingBonusPercent });
            Require(result is float, "Skill04 cooking multiplier helper returned an invalid value.");
            return (float)result;
        }

        private static void ValidateSkill04ClassStructure()
        {
            Type skillType = typeof(AssignedCookingSpeedUpSkill);
            Require(skillType.BaseType == typeof(SkillBase), "AssignedCookingSpeedUpSkill must inherit SkillBase.");
            Require(!skillType.IsAbstract, "AssignedCookingSpeedUpSkill must not be abstract.");
            ValidateDeclaredInstanceFields(skillType, "_assignedCookingSpeedUpPercent");

            FieldInfo percentField = skillType.GetField(
                "_assignedCookingSpeedUpPercent",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(percentField != null, "Skill04 assigned cooking percent field is missing.");
            Require(percentField.FieldType == typeof(float), "Skill04 assigned cooking percent field must be float.");
            Require(
                percentField.GetCustomAttribute<SerializeField>() != null,
                "Skill04 assigned cooking percent field must be serialized.");

            RangeAttribute range = percentField.GetCustomAttribute<RangeAttribute>();
            Require(range != null, "Skill04 assigned cooking percent field must have a Range attribute.");
            RequireNear(0f, range.min, "Skill04 assigned cooking percent minimum must be zero.");
            RequireNear(1000f, range.max, "Skill04 assigned cooking percent maximum must be 1000.");

            CreateAssetMenuAttribute menu = skillType.GetCustomAttribute<CreateAssetMenuAttribute>();
            Require(menu != null, "AssignedCookingSpeedUpSkill must have CreateAssetMenu.");
            Require(menu.fileName == "AssignedCookingSpeedUpSkill", "Skill04 CreateAssetMenu filename changed.");
            Require(
                menu.menuName == "Scriptable Object/Skill/AssignedCookingSpeedUpSkill",
                "Skill04 CreateAssetMenu path changed.");

            ValidateReadOnlyProperty(skillType, "FirstValue", typeof(float));
            ValidateReadOnlyProperty(skillType, "SecondValue", typeof(float));
        }

        private static void ValidateBurnerWorkingProperty()
        {
            ValidateReadOnlyProperty(
                typeof(BurnerKitchenUtensil),
                "IsStaffWorking",
                typeof(bool));

            FieldInfo workingField = typeof(BurnerKitchenUtensil).GetField(
                "_isStaffWorking",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(workingField != null, "Burner private _isStaffWorking field must remain present.");
            Require(workingField.FieldType == typeof(bool), "Burner _isStaffWorking field must remain bool.");
            Require(workingField.IsPrivate, "Burner _isStaffWorking field must remain private.");
        }

        private static void ValidateSkill04NonDestructiveStructure()
        {
            FieldInfo[] skillFields = typeof(AssignedCookingSpeedUpSkill).GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
            Require(skillFields.Length == 1, "Skill04 must have exactly one declared instance field.");
            Require(skillFields[0].FieldType == typeof(float), "Skill04 must not store Runtime object references.");

            MethodInfo helper = typeof(KitchenUtensilGroup).GetMethod(
                "CalculateAssignedCookingSpeedMultiplier",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(helper != null && helper.IsStatic, "Skill04 cooking multiplier helper must remain static and pure.");

            PropertyInfo workingProperty = typeof(BurnerKitchenUtensil).GetProperty(
                "IsStaffWorking",
                BindingFlags.Instance | BindingFlags.Public);
            Require(
                workingProperty != null && workingProperty.GetSetMethod(true) == null,
                "Burner StaffWorking state must remain read-only outside the existing setter method.");
        }

        private static void ValidateSkill06Registry()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillEffectType effectType = StaffSkillEffectType.RestaurantTipPayoutPercent;
            StaffSkillSourceToken sourceA = new StaffSkillSourceToken(101, 1);
            StaffSkillSourceToken sourceB = new StaffSkillSourceToken(102, 1);
            StaffSkillSourceToken sourceC = new StaffSkillSourceToken(103, 1);
            StaffSkillSourceToken staleSource = new StaffSkillSourceToken(104, 1);

            RequireNear(0f, registry.GetHighestPercent(effectType), "Skill06 initial highest percent must be zero.");
            RequireNear(1f, registry.GetMultiplier(effectType), "Skill06 initial multiplier must be 1.0.");

            registry.RegisterOrUpdate(effectType, sourceA, 50f, "STAFF_SKILL06:A");
            RequireNear(50f, registry.GetHighestPercent(effectType), "Skill06 source A must register 50 percent.");
            RequireNear(1.5f, registry.GetMultiplier(effectType), "Skill06 50 percent must produce 1.5.");

            registry.RegisterOrUpdate(effectType, sourceB, 50f, "STAFF_SKILL06:B");
            Require(registry.GetSourceCount(effectType) == 2, "Two Skill06 sources must both remain registered.");
            RequireNear(1.5f, registry.GetMultiplier(effectType), "Two 50 percent sources must remain 1.5.");

            registry.RegisterOrUpdate(effectType, sourceC, 75f, "STAFF_SKILL06:C");
            RequireNear(75f, registry.GetHighestPercent(effectType), "The highest Skill06 source must be 75 percent.");
            RequireNear(1.75f, registry.GetMultiplier(effectType), "Skill06 75 percent must produce 1.75.");

            Require(registry.Remove(effectType, sourceC), "Removing Skill06 source C must succeed.");
            RequireNear(50f, registry.GetHighestPercent(effectType), "Removing the highest source must restore 50 percent.");
            Require(registry.Remove(effectType, sourceA), "Removing Skill06 source A must succeed.");
            Require(registry.ContainsSource(effectType, sourceB), "Skill06 source B must remain registered.");
            RequireNear(1.5f, registry.GetMultiplier(effectType), "Remaining Skill06 source B must keep 1.5.");

            Require(!registry.Remove(effectType, staleSource), "Removing an absent stale source must be a no-op.");
            Require(registry.ContainsSource(effectType, sourceB), "Removing a stale source must not remove source B.");
            Require(registry.RemoveAllForSource(sourceB) == 1, "RemoveAllForSource must remove Skill06 source B.");
            RequireNear(1f, registry.GetMultiplier(effectType), "Removing every Skill06 source must restore 1.0.");

            registry.RegisterOrUpdate(effectType, sourceA, 50f, "STAFF_SKILL06:A");
            registry.RegisterOrUpdate(effectType, sourceC, 75f, "STAFF_SKILL06:C");
            registry.ClearAll();
            Require(registry.GetSourceCount(effectType) == 0, "Skill06 ClearAll must remove every source.");
            Require(registry.TotalSourceCount == 0, "Skill06 ClearAll must leave the Registry empty.");
            RequireNear(0f, registry.GetHighestPercent(effectType), "Skill06 ClearAll must restore zero percent.");
            RequireNear(1f, registry.GetMultiplier(effectType), "Skill06 ClearAll must restore 1.0.");
        }

        private static void ValidatePaymentTipCalculator()
        {
            Type calculatorType = typeof(TableManager).Assembly.GetType("StaffPaymentTipCalculator");
            Require(calculatorType != null, "StaffPaymentTipCalculator type is missing.");
            Require(calculatorType.IsAbstract && calculatorType.IsSealed, "StaffPaymentTipCalculator must be static.");

            MethodInfo method = calculatorType.GetMethod(
                "CalculateFoodPaymentTipPayout",
                BindingFlags.Static | BindingFlags.NonPublic);
            Require(method != null, "CalculateFoodPaymentTipPayout is missing.");
            Require(method.IsStatic, "CalculateFoodPaymentTipPayout must be static.");
            Require(!method.IsPublic, "CalculateFoodPaymentTipPayout does not need to be public.");
            Require(method.ReturnType == typeof(int), "CalculateFoodPaymentTipPayout must return int.");

            ParameterInfo[] parameters = method.GetParameters();
            Require(parameters.Length == 2, "CalculateFoodPaymentTipPayout must have two parameters.");
            Require(parameters[0].ParameterType == typeof(int), "Payment Tip parameter 1 must be int.");
            Require(parameters[1].ParameterType == typeof(float), "Payment Tip parameter 2 must be float.");

            Require(InvokePaymentTipCalculator(method, 0, 50f) == 0, "Base 0 with 50 percent must produce 0.");
            Require(InvokePaymentTipCalculator(method, 1, 50f) == 1, "Base 1 with 50 percent must produce 1.");
            Require(InvokePaymentTipCalculator(method, 2, 50f) == 3, "Base 2 with 50 percent must produce 3.");
            Require(InvokePaymentTipCalculator(method, 3, 50f) == 4, "Base 3 with 50 percent must produce 4.");
            Require(InvokePaymentTipCalculator(method, 100, 50f) == 150, "Base 100 with 50 percent must produce 150.");
            Require(InvokePaymentTipCalculator(method, 2, 1000f) == 22, "Base 2 with 1000 percent must produce 22.");
            Require(InvokePaymentTipCalculator(method, 100, 1000.01f) == 100, "An out-of-range percent must be neutral.");
            Require(InvokePaymentTipCalculator(method, 100, -1f) == 100, "A negative percent must be neutral.");
            Require(InvokePaymentTipCalculator(method, 100, float.NaN) == 100, "NaN percent must be neutral.");
            Require(InvokePaymentTipCalculator(method, 100, float.PositiveInfinity) == 100, "Positive infinity must be neutral.");
            Require(InvokePaymentTipCalculator(method, 100, float.NegativeInfinity) == 100, "Negative infinity must be neutral.");
            Require(InvokePaymentTipCalculator(method, -1, 50f) == 0, "A negative Base Tip must produce zero.");
            Require(
                InvokePaymentTipCalculator(method, int.MaxValue, 50f) == int.MaxValue,
                "An overflowing Payment Tip must saturate at int.MaxValue.");

            FieldInfo[] calculatorFields = calculatorType.GetFields(
                BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in calculatorFields)
            {
                Require(field.IsStatic, "StaffPaymentTipCalculator must not store instance state.");
                Require(
                    !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType),
                    "StaffPaymentTipCalculator must not store Unity Object references.");
            }
        }

        private static int InvokePaymentTipCalculator(
            MethodInfo method,
            int baseTip,
            float payoutBonusPercent)
        {
            object result = method.Invoke(null, new object[] { baseTip, payoutBonusPercent });
            Require(result is int, "CalculateFoodPaymentTipPayout returned an invalid value.");
            return (int)result;
        }

        private static void ValidateSkill06ClassStructure()
        {
            Type skillType = typeof(FoodPaymentTipUpSkill);
            Require(skillType.BaseType == typeof(SkillBase), "FoodPaymentTipUpSkill must inherit SkillBase.");
            Require(!skillType.IsAbstract, "FoodPaymentTipUpSkill must not be abstract.");
            ValidateDeclaredInstanceFields(skillType, "_foodPaymentTipUpPercent");

            FieldInfo percentField = skillType.GetField(
                "_foodPaymentTipUpPercent",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(percentField != null, "Skill06 payment tip percent field is missing.");
            Require(percentField.FieldType == typeof(float), "Skill06 payment tip percent field must be float.");
            Require(
                percentField.GetCustomAttribute<SerializeField>() != null,
                "Skill06 payment tip percent field must be serialized.");

            RangeAttribute range = percentField.GetCustomAttribute<RangeAttribute>();
            Require(range != null, "Skill06 payment tip percent field must have a Range attribute.");
            RequireNear(0f, range.min, "Skill06 payment tip percent minimum must be zero.");
            RequireNear(1000f, range.max, "Skill06 payment tip percent maximum must be 1000.");

            CreateAssetMenuAttribute menu = skillType.GetCustomAttribute<CreateAssetMenuAttribute>();
            Require(menu != null, "FoodPaymentTipUpSkill must have CreateAssetMenu.");
            Require(menu.fileName == "FoodPaymentTipUpSkill", "Skill06 CreateAssetMenu filename changed.");
            Require(
                menu.menuName == "Scriptable Object/Skill/FoodPaymentTipUpSkill",
                "Skill06 CreateAssetMenu path changed.");

            ValidateReadOnlyProperty(skillType, "FirstValue", typeof(float));
            ValidateReadOnlyProperty(skillType, "SecondValue", typeof(float));

            string source = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Staff/StaffSkill/FoodPaymentTipUpSkill.cs"));
            Require(
                source.Contains("private float _foodPaymentTipUpPercent = 50f;"),
                "Skill06 payment tip percent default must be 50f.");
            Require(
                source.Contains("public override float FirstValue => _foodPaymentTipUpPercent;"),
                "Skill06 FirstValue must return the serialized percent field.");
            Require(
                source.Contains("public override float SecondValue => 0;"),
                "Skill06 SecondValue must return zero.");
            Require(
                source.Contains(
                    "public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)\n"
                    + "    {\n"
                    + "    }"),
                "Skill06 ActivateUpdate must remain empty.");
            Require(!source.Contains("GameManager.Instance"), "Skill06 must use the Staff cached Registry.");
            Require(source.Contains("StaffSkillEffectType.RestaurantTipPayoutPercent"), "Skill06 EffectType is missing.");
            Require(source.Contains("RegisterOrUpdate("), "Skill06 Activate must register its Source.");
            Require(source.Contains("effectRegistry.Remove("), "Skill06 Deactivate must remove its Source.");

            FieldInfo[] fields = skillType.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
            Require(fields.Length == 1, "Skill06 must have exactly one declared instance field.");
            Require(fields[0].FieldType == typeof(float), "Skill06 must not store Runtime object references.");
        }

        private static void ValidateSkill06PaymentBoundary()
        {
            string tableSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Table/TableManager.cs"));
            string endEatSource = ExtractSourceSection(
                tableSource,
                "private void EndEat(TableData data)",
                "private void DirtyTable(TableData data)");

            int baseTipIndex = endEatSource.IndexOf("int basePaymentTip = data.TotalTip;", StringComparison.Ordinal);
            int stageIndex = endEatSource.IndexOf("EStage paymentStage = UserInfo.CurrentStage;", StringComparison.Ordinal);
            int percentIndex = endEatSource.IndexOf("float payoutBonusPercent = 0f;", StringComparison.Ordinal);
            int managerIndex = endEatSource.IndexOf("GameManager.TryGetExistingInstance", StringComparison.Ordinal);
            int effectIndex = endEatSource.IndexOf(
                "StaffSkillEffectType.RestaurantTipPayoutPercent",
                StringComparison.Ordinal);
            int calculatorIndex = endEatSource.IndexOf(
                "StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout",
                StringComparison.Ordinal);
            int finalTipIndex = endEatSource.IndexOf("int finalPaymentTip", StringComparison.Ordinal);
            int coinIndex = endEatSource.IndexOf("StartCoinAnime(data);", StringComparison.Ordinal);

            Require(baseTipIndex >= 0, "EndEat must capture basePaymentTip.");
            Require(stageIndex > baseTipIndex, "EndEat must capture paymentStage after Base Tip.");
            Require(percentIndex > stageIndex, "EndEat must initialize payoutBonusPercent after paymentStage.");
            Require(managerIndex > percentIndex, "EndEat must use TryGetExistingInstance for the Registry Snapshot.");
            Require(effectIndex > managerIndex, "EndEat must read RestaurantTipPayoutPercent from the existing Manager.");
            Require(finalTipIndex > effectIndex, "EndEat must define finalPaymentTip after the Registry Snapshot.");
            Require(calculatorIndex > finalTipIndex, "EndEat must calculate finalPaymentTip with the pure Calculator.");
            Require(coinIndex > calculatorIndex, "EndEat must calculate the final tip before StartCoinAnime.");
            Require(
                CountOccurrences(endEatSource, "StaffSkillEffectType.RestaurantTipPayoutPercent") == 1,
                "EndEat must read RestaurantTipPayoutPercent exactly once.");
            Require(
                CountOccurrences(tableSource, "StaffSkillEffectType.RestaurantTipPayoutPercent") == 1,
                "Only EndEat may read RestaurantTipPayoutPercent in TableManager.");
            Require(
                endEatSource.Contains("UserInfo.AddTip(paymentStage, finalPaymentTip);"),
                "EndEat must add the captured final tip to the captured Stage.");
            Require(
                !tableSource.Contains("UserInfo.AddTip(UserInfo.CurrentStage, tip);"),
                "The old delayed CurrentStage payment path must be removed.");
            Require(
                !ExtractSourceSection(
                        tableSource,
                        "public void OnCustomerSeating(TableData data)",
                        "public void OnCustomerOrder(TableData data)")
                    .Contains("RestaurantTipPayoutPercent"),
                "OnCustomerSeating must not read the Skill06 Registry.");

            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/UserData/UserInfo.cs",
                "RestaurantTipPayoutPercent");
            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/UserData/StageInfo.cs",
                "RestaurantTipPayoutPercent");
            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/Scenes/MainScene.cs",
                "RestaurantTipPayoutPercent");

            string autoTipSource = ReadProjectText(
                "Assets/Scripts/Staff/StaffSkill/AutoTipCollectSkill.cs");
            Require(
                !autoTipSource.Contains("RestaurantTipPayoutPercent"),
                "AutoTipCollectSkill must remain isolated from Skill06.");
            Require(autoTipSource.Contains("GameManager.Instance.MaxTipVolume"), "AutoTipCollectSkill capacity check changed.");
            Require(autoTipSource.Contains("UserInfo.TipCollection(UserInfo.CurrentStage);"), "AutoTipCollectSkill collection call changed.");
        }

        private static void ValidateSkill05Registry()
        {
            StaffSkillEffectRegistry registry = new StaffSkillEffectRegistry();
            StaffSkillEffectType effectType = StaffSkillEffectType.FoodPricePercent;
            StaffSkillSourceToken sourceA = new StaffSkillSourceToken(501, 1);
            StaffSkillSourceToken sourceB = new StaffSkillSourceToken(502, 1);
            StaffSkillSourceToken sourceC = new StaffSkillSourceToken(503, 1);
            StaffSkillSourceToken staleSource = new StaffSkillSourceToken(504, 1);

            RequireNear(0f, registry.GetHighestPercent(effectType), "Skill05 initial percent must be zero.");
            RequireNear(1f, registry.GetMultiplier(effectType), "Skill05 initial multiplier must be 1.0.");

            registry.RegisterOrUpdate(effectType, sourceA, 50f, "STAFF_SKILL05:A");
            RequireNear(50f, registry.GetHighestPercent(effectType), "Skill05 source A must register 50 percent.");
            RequireNear(1.5f, registry.GetMultiplier(effectType), "Skill05 50 percent must produce 1.5.");

            registry.RegisterOrUpdate(effectType, sourceB, 50f, "STAFF_SKILL05:B");
            Require(registry.GetSourceCount(effectType) == 2, "Skill05 equal sources must remain independent.");
            RequireNear(1.5f, registry.GetMultiplier(effectType), "Two Skill05 50 percent sources must remain 1.5.");

            registry.RegisterOrUpdate(effectType, sourceC, 75f, "STAFF_SKILL05:C");
            RequireNear(75f, registry.GetHighestPercent(effectType), "Skill05 highest percent must be 75.");
            RequireNear(1.75f, registry.GetMultiplier(effectType), "Skill05 75 percent must produce 1.75.");

            Require(registry.Remove(effectType, sourceC), "Skill05 highest source must be removable.");
            RequireNear(50f, registry.GetHighestPercent(effectType), "Skill05 must fall back to 50 percent.");
            Require(registry.Remove(effectType, sourceA), "Skill05 source A must be removable.");
            Require(registry.ContainsSource(effectType, sourceB), "Skill05 source B must remain registered.");
            RequireNear(1.5f, registry.GetMultiplier(effectType), "Remaining Skill05 source B must keep 1.5.");

            Require(!registry.Remove(effectType, staleSource), "Removing an absent stale Skill05 source must be a no-op.");
            Require(registry.ContainsSource(effectType, sourceB), "A stale removal must not remove the current Skill05 source.");
            Require(registry.RemoveAllForSource(sourceB) == 1, "RemoveAllForSource must remove Skill05 source B.");
            Require(registry.GetSourceCount(effectType) == 0, "Every Skill05 source must be removed.");
            RequireNear(1f, registry.GetMultiplier(effectType), "Removing every Skill05 source must restore 1.0.");

            registry.RegisterOrUpdate(effectType, sourceA, 50f, "STAFF_SKILL05:A");
            registry.RegisterOrUpdate(effectType, sourceC, 75f, "STAFF_SKILL05:C");
            registry.ClearAll();
            Require(registry.GetSourceCount(effectType) == 0, "Skill05 ClearAll must remove every source.");
            Require(registry.TotalSourceCount == 0, "Skill05 ClearAll must leave the Registry empty.");
            RequireNear(1f, registry.GetMultiplier(effectType), "Skill05 ClearAll must restore 1.0.");
        }

        private static void ValidateSkill05ClassStructure()
        {
            Type skillType = typeof(FoodPriceUpSkill);
            Require(skillType.BaseType == typeof(SkillBase), "FoodPriceUpSkill must inherit SkillBase directly.");
            Require(!skillType.IsAbstract, "FoodPriceUpSkill must not be abstract.");
            ValidateDeclaredInstanceFields(skillType, "_foodPriceUpPercent");

            FieldInfo percentField = skillType.GetField(
                "_foodPriceUpPercent",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(percentField != null, "Skill05 food price percent field is missing.");
            Require(percentField.FieldType == typeof(float), "Skill05 food price percent field must be float.");
            Require(
                percentField.GetCustomAttribute<SerializeField>() != null,
                "Skill05 food price percent field must be serialized.");

            RangeAttribute range = percentField.GetCustomAttribute<RangeAttribute>();
            Require(range != null, "Skill05 food price percent field must have a Range attribute.");
            RequireNear(0f, range.min, "Skill05 food price percent minimum must be zero.");
            RequireNear(1000f, range.max, "Skill05 food price percent maximum must be 1000.");

            CreateAssetMenuAttribute menu = skillType.GetCustomAttribute<CreateAssetMenuAttribute>();
            Require(menu != null, "FoodPriceUpSkill must have CreateAssetMenu.");
            Require(menu.fileName == "FoodPriceUpSkill", "Skill05 CreateAssetMenu filename changed.");
            Require(
                menu.menuName == "Scriptable Object/Skill/FoodPriceUpSkill",
                "Skill05 CreateAssetMenu path changed.");

            ValidateReadOnlyProperty(skillType, "FirstValue", typeof(float));
            ValidateReadOnlyProperty(skillType, "SecondValue", typeof(float));

            string source = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Staff/StaffSkill/FoodPriceUpSkill.cs"));
            string metaSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Staff/StaffSkill/FoodPriceUpSkill.cs.meta"));
            Require(
                CountOccurrences(metaSource, "guid: 2166c039b7672614f9452cc7ec63189a") == 1,
                "FoodPriceUpSkill Script GUID changed.");
            Require(
                source.Contains("private float _foodPriceUpPercent = 50f;"),
                "Skill05 food price percent default must be 50f.");
            Require(
                source.Contains("public override float FirstValue => _foodPriceUpPercent;"),
                "Skill05 FirstValue must return the serialized percent field.");
            Require(
                source.Contains("public override float SecondValue => 0;"),
                "Skill05 SecondValue must return zero.");
            Require(
                source.Contains(
                    "public override void ActivateUpdate(Staff staff, TableManager tableManager, KitchenSystem kitchenSystem, CustomerController customerController)\n"
                    + "    {\n"
                    + "    }"),
                "Skill05 ActivateUpdate must remain empty.");
            Require(source.Contains("StaffSkillEffectType.FoodPricePercent"), "Skill05 EffectType is missing.");
            Require(source.Contains("RegisterOrUpdate("), "Skill05 Activate must register its Source.");
            Require(source.Contains("effectRegistry.Remove("), "Skill05 Deactivate must remove its Source.");
            Require(!source.Contains("GameManager.Instance"), "Skill05 must use the Staff cached Registry.");
            Require(!source.Contains("AddFoodPriceMul"), "Skill05 must not use the Legacy additive price channel.");
            Require(!source.Contains("-_foodPriceUpPercent"), "Skill05 must not restore by subtracting a negative percent.");
            Require(!source.Contains("Debug.Log"), "Skill05 must not add unrelated Runtime logs.");

            FieldInfo[] fields = skillType.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
            Require(fields.Length == 1, "Skill05 must have exactly one declared instance field.");
            Require(fields[0].FieldType == typeof(float), "Skill05 must not store Runtime object references.");
            Require(
                !typeof(UnityEngine.Object).IsAssignableFrom(fields[0].FieldType),
                "Skill05 must not store Unity Object references.");
        }

        private static void ValidateSkill05FoodPriceGetterStructure()
        {
            string gameManagerSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Manager/GameManager.cs"));
            string foodPriceSource = ExtractSourceSection(
                gameManagerSource,
                "public float GetFoodPriceMul(ERestaurantFloorType floor, FoodType type)",
                "public void AppendPromotionCustomer(int value)");
            string cookingSpeedSource = ExtractSourceSection(
                gameManagerSource,
                "public float GetCookingSpeedMul(ERestaurantFloorType floor, FoodType type)",
                "public float GetFoodPriceMul(ERestaurantFloorType floor, FoodType type)");
            string staffSpeedSource = ExtractSourceSection(
                gameManagerSource,
                "public float GetStaffSpeedMul(StaffGroupType type)",
                "public float GetStaffMoveSpeedMul(StaffGroupType type)");

            int baseIndex = foodPriceSource.IndexOf("float foodPriceMul =", StringComparison.Ordinal);
            int lastBaseChannelIndex = foodPriceSource.IndexOf("_foodTypePriceMul", StringComparison.Ordinal);
            int registryIndex = foodPriceSource.IndexOf("_staffSkillEffectRegistry.GetMultiplier(", StringComparison.Ordinal);
            int returnIndex = foodPriceSource.IndexOf("return foodPriceMul * staffSkillMultiplier;", StringComparison.Ordinal);

            Require(baseIndex >= 0, "GetFoodPriceMul must preserve the existing base price calculation.");
            Require(foodPriceSource.Contains("_foodPriceMul"), "GetFoodPriceMul must preserve the Legacy price field.");
            Require(foodPriceSource.Contains("_addSetFoodPriceMul"), "GetFoodPriceMul must preserve the set price channel.");
            Require(foodPriceSource.Contains("_addGachaItemAllFoodPriceMul"), "GetFoodPriceMul must preserve the all-food Gacha channel.");
            Require(foodPriceSource.Contains("_addGachaItemFoodPriceMulDic"), "GetFoodPriceMul must preserve the FoodType Gacha channel.");
            Require(lastBaseChannelIndex >= 0, "GetFoodPriceMul must preserve the existing FoodType price channel.");
            Require(registryIndex > lastBaseChannelIndex, "Skill05 Registry multiplier must be read after every base price channel.");
            Require(returnIndex > registryIndex, "GetFoodPriceMul must multiply the completed base price by Skill05.");
            Require(
                CountOccurrences(foodPriceSource, "StaffSkillEffectType.FoodPricePercent") == 1,
                "GetFoodPriceMul must read FoodPricePercent exactly once.");
            Require(
                CountOccurrences(foodPriceSource, "_staffSkillEffectRegistry.GetMultiplier(") == 1,
                "GetFoodPriceMul must query the Registry multiplier exactly once.");
            Require(
                CountOccurrences(gameManagerSource, "StaffSkillEffectType.FoodPricePercent") == 1,
                "Only GetFoodPriceMul may use FoodPricePercent in GameManager.");
            Require(!foodPriceSource.Contains("_foodPriceMul ="), "GetFoodPriceMul must not write Registry values into the Legacy price field.");
            Require(!foodPriceSource.Contains("+ 0.5f"), "GetFoodPriceMul must not add a hard-coded Skill05 bonus.");
            Require(!foodPriceSource.Contains("RestaurantTipPayoutPercent"), "GetFoodPriceMul must remain isolated from Skill06.");
            Require(!cookingSpeedSource.Contains("FoodPricePercent"), "GetCookingSpeedMul must remain isolated from Skill05.");
            Require(!staffSpeedSource.Contains("FoodPricePercent"), "GetStaffSpeedMul must remain isolated from Skill05.");
        }

        private static void ValidateSkill05OrderPriceBoundary()
        {
            string tableSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Table/TableManager.cs"));
            string seatingSource = ExtractSourceSection(
                tableSource,
                "public void OnCustomerSeating(TableData data)",
                "public void OnCustomerOrder(TableData data)");
            string endEatSource = ExtractSourceSection(
                tableSource,
                "private void EndEat(TableData data)",
                "private void DirtyTable(TableData data)");
            string coinSource = ExtractSourceSection(
                tableSource,
                "private void StartCoinAnime(TableData data)",
                "private void StartGarbageAnime(TableData data)");

            Require(
                CountOccurrences(seatingSource, "GameManager.Instance.GetFoodPriceMul(") == 1,
                "OnCustomerSeating must snapshot the Skill05-aware food price exactly once.");
            Require(
                seatingSource.Contains("int totalPrice = (int)(cookingData.Price"),
                "OnCustomerSeating must preserve integer truncation for the order price.");
            Require(
                seatingSource.Contains("data.TotalPrice += totalPrice;"),
                "OnCustomerSeating must preserve the accumulated order price Snapshot.");
            Require(
                CountOccurrences(tableSource, "GameManager.Instance.GetFoodPriceMul(") == 1,
                "Only OnCustomerSeating may calculate the food price multiplier in TableManager.");
            Require(!tableSource.Contains("StaffSkillEffectType.FoodPricePercent"), "TableManager must not query the Skill05 Registry directly.");
            Require(!endEatSource.Contains("FoodPricePercent"), "EndEat must not recalculate Skill05 food prices.");
            Require(!coinSource.Contains("FoodPricePercent"), "StartCoinAnime must not recalculate Skill05 food prices.");
            Require(
                coinSource.Contains("float feverFoodPriceMultiplier = 1f;"),
                "StartCoinAnime must use a safe neutral Fever food-price multiplier.");
            Require(
                coinSource.Contains("GameManager.TryGetExistingInstance(out existingGameManager)"),
                "StartCoinAnime must query only an existing GameManager for Fever price.");
            Require(
                CountOccurrences(coinSource, "FeverRuntimeContext.FoodPriceMultiplier") == 1,
                "StartCoinAnime must read the FeverRuntimeContext food-price multiplier exactly once.");
            Require(
                coinSource.Contains("data.TotalPrice * feverFoodPriceMultiplier"),
                "StartCoinAnime must apply the FeverRuntimeContext food-price multiplier to the captured TotalPrice.");
            Require(
                !coinSource.Contains("_feverSystem.IsFeverStart ? 2f : 1f"),
                "StartCoinAnime must not use the legacy FeverSystem price ternary.");
            Require(
                !coinSource.Contains("data.TotalTip * feverFoodPriceMultiplier"),
                "StartCoinAnime must not apply the Fever food-price multiplier to tips.");
            Require(
                endEatSource.Contains("StaffSkillEffectType.RestaurantTipPayoutPercent"),
                "EndEat must preserve the Skill06 payment tip Registry.");
            Require(
                endEatSource.Contains("StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout"),
                "EndEat must preserve the Skill06 final payment tip calculation.");
            Require(endEatSource.Contains("int finalPaymentTip"), "EndEat must preserve finalPaymentTip.");
        }

        private static void ValidateBaseFoodTipPolicy()
        {
            Type gameManagerType = typeof(GameManager);
            FieldInfo multiplierField = gameManagerType.GetField(
                "BaseFoodTipMultiplier",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(multiplierField != null, "BaseFoodTipMultiplier constant is missing.");
            Require(multiplierField.FieldType == typeof(float), "BaseFoodTipMultiplier must be float.");
            Require(multiplierField.IsPrivate, "BaseFoodTipMultiplier must be private.");
            Require(multiplierField.IsStatic, "BaseFoodTipMultiplier must be static.");
            Require(multiplierField.IsLiteral, "BaseFoodTipMultiplier must be const.");
            Require(
                multiplierField.GetCustomAttribute<SerializeField>() == null,
                "BaseFoodTipMultiplier must not be serialized.");
            Require(
                !typeof(UnityEngine.Object).IsAssignableFrom(multiplierField.FieldType),
                "BaseFoodTipMultiplier must not be a Unity Object.");

            object rawValue = multiplierField.GetRawConstantValue();
            Require(rawValue is float, "BaseFoodTipMultiplier constant value must be float.");
            float multiplier = (float)rawValue;
            RequireNear(0.5f, multiplier, "BaseFoodTipMultiplier must be 0.5f.");

            PropertyInfo tipMulProperty = gameManagerType.GetProperty(
                "TipMul",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(tipMulProperty != null, "GameManager.TipMul property is missing.");
            Require(tipMulProperty.PropertyType == typeof(float), "GameManager.TipMul must return float.");
            Require(tipMulProperty.GetIndexParameters().Length == 0, "GameManager.TipMul must not be an indexer.");
            MethodInfo getter = tipMulProperty.GetGetMethod();
            Require(getter != null && getter.IsPublic, "GameManager.TipMul must have a public getter.");
            Require(!getter.IsStatic, "GameManager.TipMul must remain an instance property.");
            Require(tipMulProperty.GetSetMethod(true) == null, "GameManager.TipMul must not have a setter.");

            string gameManagerSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Manager/GameManager.cs"));
            string tipMulSource = ExtractSourceSection(
                gameManagerSource,
                "// FOOD_TIP_POLICY_2026_08_19_V1",
                "public int TipPerMinute");
            Require(
                CountOccurrences(gameManagerSource, "FOOD_TIP_POLICY_2026_08_19_V1") == 1,
                "Food tip policy marker must exist exactly once.");
            Require(
                tipMulSource.Contains("private const float BaseFoodTipMultiplier = 0.5f;"),
                "BaseFoodTipMultiplier must be a private 0.5f literal constant.");
            Require(
                tipMulSource.Contains("public float TipMul => BaseFoodTipMultiplier;"),
                "GameManager.TipMul must return BaseFoodTipMultiplier directly.");
            Require(!tipMulSource.Contains("=> 1"), "GameManager.TipMul must not return the old hard-coded 1.");
            Require(
                !tipMulSource.Contains("_addEquipStaffTipMul"),
                "GameManager.TipMul must not retain the legacy equipment-tip comment.");

            ValidateBaseFoodTipCase(0, multiplier, 1f, 0);
            ValidateBaseFoodTipCase(1, multiplier, 1f, 0);
            ValidateBaseFoodTipCase(2, multiplier, 1f, 1);
            ValidateBaseFoodTipCase(3, multiplier, 1f, 1);
            ValidateBaseFoodTipCase(100, multiplier, 1f, 50);
            ValidateBaseFoodTipCase(100, multiplier, 1.03f, 51);
            ValidateBaseFoodTipCase(100, multiplier, 1.05f, 52);
            ValidateBaseFoodTipCase(1500, multiplier, 1f, 750);
            ValidateBaseFoodTipCase(1500, multiplier, 1.05f, 787);
        }

        private static void ValidateBaseFoodTipCase(
            int sellPrice,
            float baseMultiplier,
            float satisfactionMultiplier,
            int expected)
        {
            int actual = Mathf.FloorToInt(sellPrice * baseMultiplier * satisfactionMultiplier);
            Require(
                actual == expected,
                "Base food tip calculation changed. SellPrice=" + sellPrice
                + ", Satisfaction=" + satisfactionMultiplier
                + ", Expected=" + expected
                + ", Actual=" + actual + ".");
        }

        private static void ValidateFoodTipIsolationStructure()
        {
            string tableSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Table/TableManager.cs"));
            string seatingSource = ExtractSourceSection(
                tableSource,
                "public void OnCustomerSeating(TableData data)",
                "public void OnCustomerOrder(TableData data)");
            string endEatSource = ExtractSourceSection(
                tableSource,
                "private void EndEat(TableData data)",
                "private void DirtyTable(TableData data)");
            string coinSource = ExtractSourceSection(
                tableSource,
                "private void StartCoinAnime(TableData data)",
                "private void StartGarbageAnime(TableData data)");
            string tipCalculationSource = ExtractSourceSection(
                seatingSource,
                "int tip =",
                "data.TotalTip += tip;");
            string priceCalculationSource = ExtractSourceSection(
                seatingSource,
                "int totalPrice =",
                "data.TotalPrice += totalPrice;");

            int sellPriceIndex = tipCalculationSource.IndexOf("foodData.GetSellPrice(foodLevel)", StringComparison.Ordinal);
            int tipMultiplierIndex = tipCalculationSource.IndexOf("GameManager.Instance.TipMul", StringComparison.Ordinal);
            int satisfactionIndex = tipCalculationSource.IndexOf("_satisfactionSystem.AddCustomerTipMul(", StringComparison.Ordinal);
            int totalTipIndex = seatingSource.IndexOf("data.TotalTip += tip;", StringComparison.Ordinal);
            int totalPriceIndex = seatingSource.IndexOf("int totalPrice =", StringComparison.Ordinal);
            int foodPriceMultiplierIndex = priceCalculationSource.IndexOf(
                "GameManager.Instance.GetFoodPriceMul(",
                StringComparison.Ordinal);

            Require(tipCalculationSource.Contains("Mathf.FloorToInt("), "Order tip must use Mathf.FloorToInt.");
            Require(sellPriceIndex >= 0, "Order tip must use the recipe-level sell price.");
            Require(tipMultiplierIndex > sellPriceIndex, "Base food tip multiplier must follow the sell price.");
            Require(satisfactionIndex > tipMultiplierIndex, "Satisfaction tip multiplier must remain after TipMul.");
            Require(totalTipIndex > satisfactionIndex, "Each floored order tip must accumulate into data.TotalTip.");
            Require(totalPriceIndex > totalTipIndex, "Food price calculation must remain separate from order tip accumulation.");
            Require(foodPriceMultiplierIndex >= 0, "Food price multiplier must remain in the price calculation.");
            Require(
                CountOccurrences(seatingSource, "GameManager.Instance.TipMul") == 1,
                "OnCustomerSeating must read TipMul exactly once per order.");
            Require(
                CountOccurrences(tableSource, "GameManager.Instance.TipMul") == 1,
                "Only OnCustomerSeating may read TipMul in TableManager.");
            Require(!seatingSource.Contains("FoodPricePercent"), "Order tip must not query Skill05 directly.");
            Require(!seatingSource.Contains("_feverSystem"), "Order tip must not query Fever.");
            Require(!seatingSource.Contains("IsFeverStart"), "Order tip must not depend on Fever state.");
            Require(
                !tipCalculationSource.Contains("GetFoodPriceMul"),
                "Order tip must not include the food price multiplier.");
            Require(
                !priceCalculationSource.Contains("TipMul"),
                "Food price calculation must not include the base food tip multiplier.");

            Require(endEatSource.Contains("int basePaymentTip = data.TotalTip;"), "EndEat must preserve basePaymentTip.");
            Require(endEatSource.Contains("EStage paymentStage = UserInfo.CurrentStage;"), "EndEat must preserve paymentStage.");
            Require(
                CountOccurrences(endEatSource, "StaffSkillEffectType.RestaurantTipPayoutPercent") == 1,
                "EndEat must read the Skill06 payout percent exactly once.");
            Require(
                CountOccurrences(endEatSource, "StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout") == 1,
                "EndEat must calculate the final payment tip exactly once.");
            Require(endEatSource.Contains("int finalPaymentTip"), "EndEat must preserve the payment tip Snapshot.");
            Require(
                endEatSource.Contains("UserInfo.AddTip(paymentStage, finalPaymentTip);"),
                "EndEat must pay the captured tip to the captured Stage.");
            Require(!endEatSource.Contains("TipMul"), "EndEat must not reapply the base food tip multiplier.");

            Require(
                coinSource.Contains("float feverFoodPriceMultiplier = 1f;"),
                "StartCoinAnime must preserve the neutral Fever food-price default.");
            Require(
                coinSource.Contains("GameManager.TryGetExistingInstance(out existingGameManager)"),
                "StartCoinAnime must query only an existing GameManager for Fever price.");
            Require(
                CountOccurrences(coinSource, "FeverRuntimeContext.FoodPriceMultiplier") == 1,
                "StartCoinAnime must read the Fever Context food-price multiplier exactly once.");
            Require(
                coinSource.Contains("data.TotalPrice * feverFoodPriceMultiplier"),
                "StartCoinAnime must apply Fever only to the captured food price.");
            Require(
                !coinSource.Contains("_feverSystem.IsFeverStart ? 2f : 1f"),
                "StartCoinAnime must not use the legacy FeverSystem price ternary.");
            Require(!coinSource.Contains("data.TotalTip *"), "StartCoinAnime must not multiply tips by Fever.");
            Require(!coinSource.Contains("TipMul"), "StartCoinAnime must not apply the base food tip multiplier.");
            Require(
                !tipCalculationSource.Contains("FeverRuntimeContext.FoodPriceMultiplier"),
                "Order tip calculation must not consume the Fever food-price multiplier.");
            Require(
                !endEatSource.Contains("FeverRuntimeContext.FoodPriceMultiplier"),
                "Skill06 payment-tip calculation must not consume the Fever food-price multiplier.");

            RequireProjectSourceDoesNotContain("Assets/Scripts/Scenes/MainScene.cs", "TipMul");
            RequireProjectSourceDoesNotContain("Assets/Scripts/UserData/StageInfo.cs", "TipMul");
            RequireProjectSourceDoesNotContain("Assets/Scripts/UserData/UserInfo.cs", "TipMul");
            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/Staff/StaffSkill/AutoTipCollectSkill.cs",
                "TipMul");
            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/Staff/StaffPaymentTipCalculator.cs",
                "BaseFoodTipMultiplier");
            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/Staff/StaffPaymentTipCalculator.cs",
                "TipMul");
            RequireProjectSourceDoesNotContain(
                "Assets/Scripts/Staff/StaffSkill/FoodPriceUpSkill.cs",
                "TipMul");
        }

        private static void ValidateFeverContextAndToken()
        {
            FeverRuntimeContext contextA = new FeverRuntimeContext();
            FeverRuntimeContext contextB = new FeverRuntimeContext();

            Require(contextA.ContextId > 0, "Fever Context A ID must be positive.");
            Require(contextB.ContextId > 0, "Fever Context B ID must be positive.");
            Require(contextA.ContextId != contextB.ContextId, "Fever Context IDs must be unique.");
            Require(!default(FeverRuntimeToken).IsValid, "The default Fever token must be invalid.");
            Require(!contextA.IsActive, "A new Fever Context must be inactive.");
            Require(!contextA.CurrentToken.IsValid, "A new Fever Context token must be invalid.");
            RequireNear(0f, contextA.DurationSeconds, "A new Fever duration must be zero.");
            RequireNear(0f, contextA.ElapsedSeconds, "A new Fever elapsed time must be zero.");
            RequireNear(0f, contextA.RemainingRatio, "A new Fever remaining ratio must be zero.");
            RequireNear(0f, contextA.AutoCallRemainderSeconds, "A new Fever remainder must be zero.");
            ValidateFeverMultipliers(contextA, 1f, "Inactive Fever");

            long initialSequence = contextA.ActivationSequence;
            Require(
                !contextA.TryActivate(0f, out FeverRuntimeToken zeroDurationToken),
                "Zero Fever duration must be rejected.");
            Require(!zeroDurationToken.IsValid, "Rejected zero duration must return an invalid token.");
            Require(
                !contextA.TryActivate(-1f, out FeverRuntimeToken negativeDurationToken),
                "Negative Fever duration must be rejected.");
            Require(!negativeDurationToken.IsValid, "Rejected negative duration must return an invalid token.");
            Require(
                !contextA.TryActivate(float.NaN, out FeverRuntimeToken nanDurationToken),
                "NaN Fever duration must be rejected.");
            Require(!nanDurationToken.IsValid, "Rejected NaN duration must return an invalid token.");
            Require(
                !contextA.TryActivate(float.PositiveInfinity, out FeverRuntimeToken infiniteDurationToken),
                "Infinite Fever duration must be rejected.");
            Require(!infiniteDurationToken.IsValid, "Rejected infinite duration must return an invalid token.");
            Require(!contextA.IsActive, "Rejected Fever activations must not activate the Context.");
            Require(
                contextA.ActivationSequence == initialSequence,
                "Rejected Fever activations must not change the sequence.");

            Require(
                contextA.TryActivate(10f, out FeverRuntimeToken tokenA),
                "A finite positive Fever duration must activate.");
            Require(tokenA.IsValid, "A successful Fever activation must return a valid token.");
            Require(contextA.IsCurrentToken(tokenA), "The activated Fever token must be current.");
            Require(contextA.CurrentToken == tokenA, "The Context must expose the current Fever token.");
            RequireNear(10f, contextA.DurationSeconds, "The activated Fever duration must be stored.");
            RequireNear(0f, contextA.ElapsedSeconds, "A new Fever activation must start at zero elapsed time.");
            RequireNear(1f, contextA.RemainingRatio, "A new Fever activation must start at ratio 1.0.");
            ValidateFeverMultipliers(contextA, 2f, "Active Fever");

            FeverRuntimeToken equalToken = new FeverRuntimeToken(
                tokenA.ContextId,
                tokenA.ActivationSequence);
            FeverRuntimeToken differentSequence = new FeverRuntimeToken(
                tokenA.ContextId,
                tokenA.ActivationSequence + 1);
            Require(tokenA == equalToken && tokenA.Equals(equalToken), "Equal Fever tokens must compare equal.");
            Require(tokenA.GetHashCode() == equalToken.GetHashCode(), "Equal Fever token hashes must match.");
            Require(tokenA != differentSequence, "Different Fever activation sequences must not compare equal.");
            Require(
                tokenA.ToString() == tokenA.ContextId + ":" + tokenA.ActivationSequence,
                "Fever token text must use the context:sequence format.");
            Dictionary<FeverRuntimeToken, string> tokenDictionary =
                new Dictionary<FeverRuntimeToken, string>();
            tokenDictionary.Add(tokenA, "fever");
            Require(tokenDictionary[equalToken] == "fever", "Fever token must be a stable dictionary key.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new FeverRuntimeToken(0, 1),
                "A zero Fever Context ID must be rejected.");
            RequireThrows<ArgumentOutOfRangeException>(
                () => new FeverRuntimeToken(1, 0),
                "A zero Fever activation sequence must be rejected.");

            FeverRuntimeAdvanceResult quarterSecond = contextA.Advance(tokenA, 0.25f);
            Require(quarterSecond.IsCurrentActivation, "The current Fever token must advance.");
            long activeSequence = contextA.ActivationSequence;
            float elapsedBeforeDuplicate = contextA.ElapsedSeconds;
            float remainderBeforeDuplicate = contextA.AutoCallRemainderSeconds;
            Require(
                !contextA.TryActivate(5f, out FeverRuntimeToken duplicateToken),
                "A duplicate Fever activation must be rejected.");
            Require(!duplicateToken.IsValid, "A duplicate Fever activation must return an invalid token.");
            Require(contextA.CurrentToken == tokenA, "Duplicate activation must preserve the current Fever token.");
            Require(
                contextA.ActivationSequence == activeSequence,
                "Duplicate activation must preserve the Fever sequence.");
            RequireNear(
                elapsedBeforeDuplicate,
                contextA.ElapsedSeconds,
                "Duplicate activation must preserve Fever elapsed time.");
            RequireNear(
                remainderBeforeDuplicate,
                contextA.AutoCallRemainderSeconds,
                "Duplicate activation must preserve the auto-call remainder.");

            Require(contextB.TryActivate(5f, out FeverRuntimeToken tokenB), "Context B must activate.");
            Require(!contextA.IsCurrentToken(tokenB), "A different Fever Context token must be rejected.");
            Require(!contextA.Deactivate(tokenB), "A different Context token must not deactivate Fever.");
            Require(contextA.CurrentToken == tokenA, "A foreign token must not change the active Fever token.");
            Require(contextB.Deactivate(tokenB), "Context B cleanup must succeed.");

            long contextIdBeforeReset = contextA.ContextId;
            Require(contextA.Deactivate(tokenA), "The current Fever token must deactivate.");
            Require(!contextA.IsActive, "Deactivated Fever Context must be inactive.");
            Require(!contextA.CurrentToken.IsValid, "Deactivated Fever token must be invalid.");
            ValidateFeverMultipliers(contextA, 1f, "Deactivated Fever");
            Require(!contextA.Deactivate(tokenA), "Duplicate Fever deactivation must return false.");

            contextA.Reset();
            contextA.Reset();
            Require(contextA.ContextId == contextIdBeforeReset, "Reset must preserve the Fever Context ID.");
            Require(contextA.ActivationSequence == activeSequence, "Reset must preserve the Fever sequence.");
            Require(contextA.TryActivate(3f, out FeverRuntimeToken nextToken), "Fever must reactivate after Reset.");
            Require(
                nextToken.ActivationSequence == activeSequence + 1,
                "A new Fever activation must increment the sequence.");
            Require(!contextA.Deactivate(tokenA), "A stale Fever token must not deactivate a new activation.");
            FeverRuntimeAdvanceResult staleResult = contextA.Advance(tokenA, 1f);
            Require(!staleResult.IsCurrentActivation, "A stale Fever token must not advance a new activation.");
            RequireNear(0f, contextA.ElapsedSeconds, "A stale Fever token must not change elapsed time.");
            Require(contextA.Deactivate(nextToken), "The current reactivation token must clean up.");

            Type contextType = typeof(FeverRuntimeContext);
            Require(contextType.IsSealed, "FeverRuntimeContext must be sealed.");
            foreach (PropertyInfo property in contextType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Require(
                    property.GetSetMethod() == null,
                    "FeverRuntimeContext public property must not have a public setter: " + property.Name);
            }

            foreach (FieldInfo field in contextType.GetFields(
                         BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.NonPublic
                         | BindingFlags.DeclaredOnly))
            {
                Require(
                    !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType),
                    "FeverRuntimeContext must not reference a Unity Object: " + field.Name);
                Require(
                    !IsMutableCollectionType(field.FieldType),
                    "FeverRuntimeContext must not contain a mutable collection: " + field.Name);
                Require(
                    field.FieldType != typeof(Action),
                    "FeverRuntimeContext must not contain an Action delegate: " + field.Name);
            }

            string contextSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/FeverSystem/FeverRuntimeContext.cs"));
            Require(!contextSource.Contains("using UnityEngine"), "FeverRuntimeContext must not depend on UnityEngine.");
            Require(
                CountOccurrences(contextSource, "FEVER_POLICY_2026_08_19_V2") == 1,
                "The Fever V2 policy marker must exist exactly once.");
        }

        private static void ValidateFeverClockAndAutoCall()
        {
            FeverRuntimeContext splitContext = new FeverRuntimeContext();
            Require(splitContext.TryActivate(5f, out FeverRuntimeToken splitToken), "Split clock must activate.");
            FeverRuntimeAdvanceResult splitFirst = splitContext.Advance(splitToken, 0.2f);
            Require(splitFirst.AutoCallOpportunityCount == 0, "0.2 seconds must not complete an auto-call interval.");
            FeverRuntimeAdvanceResult splitSecond = splitContext.Advance(splitToken, 0.3f);
            Require(splitSecond.AutoCallOpportunityCount == 1, "0.2 + 0.3 seconds must produce one opportunity.");
            RequireNear(0f, splitSecond.AutoCallRemainderSeconds, "0.2 + 0.3 seconds must leave no remainder.");

            FeverRuntimeContext bulkContext = new FeverRuntimeContext();
            Require(bulkContext.TryActivate(5f, out FeverRuntimeToken bulkToken), "Bulk clock must activate.");
            FeverRuntimeAdvanceResult bulkResult = bulkContext.Advance(bulkToken, 1.2f);
            Require(bulkResult.AutoCallOpportunityCount == 2, "1.2 seconds must produce two opportunities.");
            RequireNear(0.2f, bulkResult.AutoCallRemainderSeconds, "1.2 seconds must preserve a 0.2 remainder.");
            float elapsedBeforePause = bulkContext.ElapsedSeconds;
            float remainderBeforePause = bulkContext.AutoCallRemainderSeconds;
            FeverRuntimeAdvanceResult pauseResult = bulkContext.Advance(bulkToken, 0f);
            Require(pauseResult.AutoCallOpportunityCount == 0, "A zero delta must produce no opportunity.");
            RequireNear(0f, pauseResult.ConsumedDeltaSeconds, "A zero delta must consume no time.");
            RequireNear(elapsedBeforePause, bulkContext.ElapsedSeconds, "A zero delta must preserve elapsed time.");
            RequireNear(remainderBeforePause, bulkContext.AutoCallRemainderSeconds, "A zero delta must preserve remainder.");

            FeverRuntimeContext limitedContext = new FeverRuntimeContext();
            Require(limitedContext.TryActivate(1f, out FeverRuntimeToken limitedToken), "Limited clock must activate.");
            FeverRuntimeAdvanceResult limitedResult = limitedContext.Advance(limitedToken, 2f);
            Require(limitedResult.IsCurrentActivation, "The current limited clock must advance.");
            Require(limitedResult.DurationCompleted, "The limited clock must report completion.");
            Require(limitedResult.AutoCallOpportunityCount == 2, "One consumed second must produce two opportunities.");
            RequireNear(1f, limitedResult.ConsumedDeltaSeconds, "Advance must consume only remaining duration.");
            RequireNear(1f, limitedContext.ElapsedSeconds, "Elapsed time must stop at the duration.");
            RequireNear(0f, limitedResult.RemainingRatio, "A completed duration must have ratio zero.");
            FeverRuntimeAdvanceResult afterCompletion = limitedContext.Advance(limitedToken, 100f);
            Require(afterCompletion.IsCurrentActivation, "Completion must not auto-deactivate the Context.");
            Require(afterCompletion.DurationCompleted, "A completed active Context must remain completed.");
            Require(afterCompletion.AutoCallOpportunityCount == 0, "No intervals may be created after completion.");
            RequireNear(0f, afterCompletion.ConsumedDeltaSeconds, "No time may be consumed after completion.");

            FeverRuntimeContext tenSecondContext = new FeverRuntimeContext();
            Require(tenSecondContext.TryActivate(10f, out FeverRuntimeToken tenSecondToken), "Ten-second clock must activate.");
            FeverRuntimeAdvanceResult tenSecondResult = tenSecondContext.Advance(tenSecondToken, 10f);
            Require(tenSecondResult.AutoCallOpportunityCount == 20, "Ten seconds must produce exactly twenty opportunities.");
            Require(tenSecondResult.DurationCompleted, "Ten seconds must complete a ten-second duration.");

            FeverRuntimeContext invalidDeltaContext = new FeverRuntimeContext();
            Require(invalidDeltaContext.TryActivate(5f, out FeverRuntimeToken invalidDeltaToken), "Invalid-delta clock must activate.");
            invalidDeltaContext.Advance(invalidDeltaToken, 0.25f);
            AssertFeverClockUnchangedAfterException(
                invalidDeltaContext,
                () => invalidDeltaContext.Advance(invalidDeltaToken, -1f),
                "Negative Fever delta must be rejected.");
            AssertFeverClockUnchangedAfterException(
                invalidDeltaContext,
                () => invalidDeltaContext.Advance(invalidDeltaToken, float.NaN),
                "NaN Fever delta must be rejected.");
            AssertFeverClockUnchangedAfterException(
                invalidDeltaContext,
                () => invalidDeltaContext.Advance(invalidDeltaToken, float.PositiveInfinity),
                "Infinite Fever delta must be rejected.");

            Require(invalidDeltaContext.Deactivate(invalidDeltaToken), "Invalid-delta Context must clean up.");
            FeverRuntimeAdvanceResult inactiveResult = invalidDeltaContext.Advance(invalidDeltaToken, 1f);
            Require(!inactiveResult.IsCurrentActivation, "Inactive Fever Advance must be rejected.");
            RequireNear(0f, invalidDeltaContext.ElapsedSeconds, "Inactive Advance must not change elapsed time.");
            RequireNear(0f, invalidDeltaContext.AutoCallRemainderSeconds, "Inactive Advance must not change remainder.");

            Require(invalidDeltaContext.TryActivate(5f, out FeverRuntimeToken currentToken), "Context must reactivate.");
            FeverRuntimeAdvanceResult staleResult = invalidDeltaContext.Advance(invalidDeltaToken, 1f);
            Require(!staleResult.IsCurrentActivation, "Stale Fever Advance must be rejected.");
            RequireNear(0f, invalidDeltaContext.ElapsedSeconds, "Stale Advance must not change elapsed time.");
            Require(invalidDeltaContext.IsCurrentToken(currentToken), "Stale Advance must preserve the current token.");
            Require(invalidDeltaContext.Deactivate(currentToken), "Reactivated Context must clean up.");
        }

        private static void ValidateGameManagerFeverContextOwnership()
        {
            Type gameManagerType = typeof(GameManager);
            FieldInfo contextField = gameManagerType.GetField(
                "_feverRuntimeContext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Require(contextField != null, "GameManager Fever Context field is missing.");
            Require(contextField.FieldType == typeof(FeverRuntimeContext), "GameManager Fever Context field type is incorrect.");
            Require(contextField.IsPrivate, "GameManager Fever Context field must be private.");
            Require(!contextField.IsStatic, "GameManager Fever Context field must not be static.");
            Require(contextField.IsInitOnly, "GameManager Fever Context field must be readonly.");
            Require(
                contextField.GetCustomAttribute<SerializeField>() == null,
                "GameManager Fever Context field must not be serialized.");

            int contextFieldCount = 0;
            foreach (FieldInfo field in gameManagerType.GetFields(
                         BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.NonPublic
                         | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType == typeof(FeverRuntimeContext))
                {
                    contextFieldCount++;
                }
            }

            Require(contextFieldCount == 1, "GameManager must own exactly one FeverRuntimeContext field.");

            PropertyInfo contextProperty = gameManagerType.GetProperty(
                "FeverRuntimeContext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Require(contextProperty != null, "GameManager FeverRuntimeContext property is missing.");
            Require(contextProperty.PropertyType == typeof(FeverRuntimeContext), "GameManager FeverRuntimeContext property type is incorrect.");
            Require(contextProperty.GetGetMethod() != null, "GameManager FeverRuntimeContext getter must be public.");
            Require(!contextProperty.GetGetMethod().IsStatic, "GameManager FeverRuntimeContext property must not be static.");
            Require(contextProperty.GetSetMethod(true) == null, "GameManager FeverRuntimeContext property must be read-only.");

            string source = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Manager/GameManager.cs"));
            string chanceSceneSource = ExtractSourceSection(
                source,
                "public void ChanceScene()",
                "private void Awake()");
            string quitSource = ExtractSourceSection(
                source,
                "private void OnApplicationQuit()",
                "private void OnDestroy()");
            string destroySource = ExtractSourceSection(
                source,
                "private void OnDestroy()",
                "private void OnGiveFurnitureEffectCheck()");

            Require(
                CountOccurrences(source, "new FeverRuntimeContext()") == 1,
                "GameManager must construct exactly one FeverRuntimeContext.");
            Require(
                CountOccurrences(source, "_feverRuntimeContext.Reset();") == 3,
                "GameManager must Reset Fever Context in exactly three lifecycle paths.");
            Require(chanceSceneSource.Contains("_staffSkillEffectRegistry.ClearAll();"), "ChanceScene must preserve Staff Registry ClearAll.");
            Require(chanceSceneSource.Contains("_feverRuntimeContext.Reset();"), "ChanceScene must Reset Fever Context.");
            Require(chanceSceneSource.Contains("_totalAddSpeedMul = 0;"), "ChanceScene must preserve the legacy speed reset.");
            Require(quitSource.Contains("_staffSkillEffectRegistry.ClearAll();"), "OnApplicationQuit must preserve Staff Registry ClearAll.");
            Require(quitSource.Contains("_feverRuntimeContext.Reset();"), "OnApplicationQuit must Reset Fever Context.");
            Require(destroySource.Contains("_staffSkillEffectRegistry.ClearAll();"), "OnDestroy must preserve Staff Registry ClearAll.");
            Require(destroySource.Contains("_feverRuntimeContext.Reset();"), "OnDestroy must Reset Fever Context.");
            Require(source.Contains("public void SetGameSpeed(float value)"), "Legacy SetGameSpeed must remain.");
            Require(source.Contains("[SerializeField] private float _totalAddSpeedMul = 0;"), "Legacy total speed field must remain.");
            Require(source.Contains("public float AddCustomerSpeedMul =>"), "AddCustomerSpeedMul must remain.");
            Require(source.Contains("public float GetStaffSpeedMul(StaffGroupType type)"), "GetStaffSpeedMul must remain.");
            Require(source.Contains("public float GetCookingSpeedMul(ERestaurantFloorType floor, FoodType type)"), "GetCookingSpeedMul must remain.");
        }

        private static void ValidateFeverSystemLifecycleStructure()
        {
            string source = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/FeverSystem/FerverSystem.cs"));
            string awakeSource = ExtractSourceSection(source, "private void Awake()", "private void Start()");
            string disableSource = ExtractSourceSection(source, "private void OnDisable()", "private void OnApplicationQuit()");
            string quitSource = ExtractSourceSection(source, "private void OnApplicationQuit()", "private void OnDestroy()");
            string destroySource = ExtractSourceSection(source, "private void OnDestroy()", "private void OnChangeSceneEvent()");
            string sceneSource = ExtractSourceSection(source, "private void OnChangeSceneEvent()", "private void OnEquipFurnitureEvent(");
            string routineSource = ExtractSourceSection(source, "private IEnumerator StartFeverRoutine(", "private void StopFeverRuntime(");
            int cleanupStartIndex = source.IndexOf("private void StopFeverRuntime(", StringComparison.Ordinal);
            Require(cleanupStartIndex >= 0, "Fever cleanup method is missing.");
            string cleanupSource = source.Substring(cleanupStartIndex);

            Require(!source.Contains("private bool _isFeverStart"), "Legacy local Fever state field must be removed.");
            Require(source.Contains("private FeverRuntimeToken _activeFeverToken;"), "FeverSystem must store an activation token.");
            Require(source.Contains("_feverRuntimeContext.TryActivate("), "FeverSystem must use TryActivate.");
            Require(routineSource.Contains("context.Advance(activeToken, deltaSeconds)"), "Fever routine must advance by token.");
            Require(cleanupSource.Contains("_feverRuntimeContext?.Deactivate(token);"), "Fever cleanup must deactivate by token.");
            Require(
                cleanupSource.Contains("&& !_feverRuntimeContext.IsCurrentToken(token)"),
                "Fever cleanup must isolate an older stale token from a newer active Fever.");
            Require(routineSource.Contains("yield return null;"), "Fever routine must advance once per frame.");
            Require(routineSource.Contains("float deltaSeconds = Time.deltaTime;"), "Fever routine must use Time.deltaTime.");
            Require(!source.Contains("WaitForSeconds(0.02f)"), "Legacy fixed Fever wait must be removed.");
            Require(!source.Contains("timer += 0.02f"), "Legacy fixed Fever duration increment must be removed.");
            Require(!source.Contains("addTabTimer += 0.02f"), "Legacy fixed auto-call increment must be removed.");
            Require(routineSource.Contains("result.AutoCallOpportunityCount"), "Fever routine must consume every auto-call opportunity.");
            Require(routineSource.Contains("CustomerController.IsMaxCount"), "Fever auto-call must preserve the wait-line maximum guard.");
            Require(routineSource.Contains("result.RemainingRatio"), "Fever UI must use the Context remaining ratio.");
            Require(routineSource.Contains("result.DurationCompleted"), "Fever routine must use Context duration completion.");
            Require(routineSource.Contains("if (!result.IsCurrentActivation)"), "Fever routine must reject stale Advance results.");
            Require(routineSource.Contains("finally"), "Fever routine must use a finally cleanup path.");
            Require(disableSource.Contains("StopFeverRuntime("), "OnDisable must use common Fever cleanup.");
            Require(quitSource.Contains("StopFeverRuntime("), "OnApplicationQuit must use common Fever cleanup.");
            Require(destroySource.Contains("StopFeverRuntime("), "OnDestroy must use common Fever cleanup.");
            Require(sceneSource.Contains("StopFeverRuntime("), "Scene change must use common Fever cleanup.");
            Require(awakeSource.Contains("_gameManager = GameManager.Instance;"), "Awake must cache GameManager.");
            Require(
                awakeSource.Contains("_feverRuntimeContext = _gameManager.FeverRuntimeContext;"),
                "Awake must cache the GameManager Fever Context.");
            Require(!cleanupSource.Contains("GameManager.Instance"), "Fever cleanup must not create or fetch GameManager.");
            Require(source.Contains("OnStartFeverHandler?.Invoke();"), "Fever start event must remain.");
            Require(source.Contains("OnEndFeverHandler?.Invoke();"), "Fever end event must remain.");
            Require(CountOccurrences(source, "_mainScene.PlayMainMusic();") == 2, "Fever music calls must remain at start and end.");
        }

        private static void ValidateFeverGaugeRealtimeUi()
        {
            string uiFeverSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/FeverSystem/FeverUI/UIFever.cs"));
            string realtimeGaugeSource = ExtractSourceSection(
                uiFeverSource,
                "public void OnChangeGaugeNoAnime(float gaugeValue)",
                "public void OnChangeGauge()");
            string normalGaugeSource = ExtractSourceSection(
                uiFeverSource,
                "public void OnChangeGauge()",
                "private void OnFeverButtonClicked()");

            int clampIndex = realtimeGaugeSource.IndexOf(
                "float ratio = Mathf.Clamp01(gaugeValue);",
                StringComparison.Ordinal);
            int mappingIndex = realtimeGaugeSource.IndexOf(
                "float fillAmount = ratio <= 0f ? 0f : 0.3f + ratio * 0.7f;",
                StringComparison.Ordinal);
            int directSetterIndex = realtimeGaugeSource.IndexOf(
                "_fillAmountImage.SetFillAmountNoAnime(fillAmount);",
                StringComparison.Ordinal);

            Require(clampIndex >= 0, "Realtime Fever gauge input must use Mathf.Clamp01.");
            Require(mappingIndex > clampIndex, "Realtime Fever gauge must preserve the 0 / 0.3 + ratio * 0.7 mapping.");
            Require(directSetterIndex > mappingIndex, "Realtime Fever gauge must use the direct no-animation setter.");
            Require(
                !realtimeGaugeSource.Contains("SetFillAmonut("),
                "Realtime Fever gauge must not restart the animated fill setter.");
            Require(
                !realtimeGaugeSource.Contains("Tween"),
                "Realtime Fever gauge must not create or restart a Tween.");
            Require(
                normalGaugeSource.Contains("_fillAmountImage.SetFillAmonut(fillAmount);"),
                "Normal Fever gauge charging must preserve its animated setter.");

            string tweenSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/UI/UITweenFillAmountImage.cs"));
            string animatedSetterSource = ExtractSourceSection(
                tweenSource,
                "public void SetFillAmonut(float value)",
                "public void SetFillAmountNoAnime(float value)");
            string directSetterSource = tweenSource.Substring(
                tweenSource.IndexOf("public void SetFillAmountNoAnime(float value)", StringComparison.Ordinal));
            Require(animatedSetterSource.Contains("_fillAmountImage.TweenStop();"), "Animated fill setter must preserve TweenStop.");
            Require(animatedSetterSource.Contains("_fillAmountImage.TweenFillAmount("), "Animated fill setter must preserve TweenFillAmount.");
            Require(directSetterSource.Contains("_fillAmountImage.TweenStop();"), "Direct fill setter must stop an existing Tween.");
            Require(directSetterSource.Contains("_fillAmountImage.fillAmount = value;"), "Direct fill setter must assign fillAmount directly.");
        }

        private static void ValidateFeverPurchaseLock()
        {
            string uiFeverSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/FeverSystem/FeverUI/UIFever.cs"));
            string initSource = ExtractSourceSection(
                uiFeverSource,
                "public void Init(FeverSystem ferverSystem)",
                "private void OnDestroy()");
            string clickSource = ExtractSourceSection(
                uiFeverSource,
                "private void OnFeverButtonClicked()",
                "private void StartFeverEvent()");
            string startSource = ExtractSourceSection(
                uiFeverSource,
                "private void StartFeverEvent()",
                "private void EndFeverEvent()");
            string endSource = ExtractSourceSection(
                uiFeverSource,
                "private void EndFeverEvent()",
                "private void OnRemoveTimeEvent(");
            string updateSource = ExtractSourceSection(
                uiFeverSource,
                "private void OnUpdateAdButtonEvent()",
                "private void FeverAdButtonSetActive()");
            string repeatingSource = ExtractSourceSection(
                uiFeverSource,
                "private void FeverAdButtonSetActive()",
                "private void RefreshFeverPurchaseButtonState()");
            string refreshSource = ExtractSourceSection(
                uiFeverSource,
                "private void RefreshFeverPurchaseButtonState()",
                "private void OnAdButtonClicked()");

            Require(
                CountOccurrences(uiFeverSource, "RefreshFeverPurchaseButtonState()") == 7,
                "UIFever must route every purchase-button state path through one common function.");
            Require(initSource.Contains("RefreshFeverPurchaseButtonState();"), "UIFever.Init must refresh purchase state.");
            Require(clickSource.Contains("RefreshFeverPurchaseButtonState();"), "Fever click must refresh purchase state after activation.");
            Require(startSource.Contains("RefreshFeverPurchaseButtonState();"), "Fever start must refresh purchase state.");
            Require(endSource.Contains("RefreshFeverPurchaseButtonState();"), "Fever end must refresh purchase state.");
            Require(updateSource.Contains("RefreshFeverPurchaseButtonState();"), "Ad-button update must refresh purchase state.");
            Require(repeatingSource.Contains("RefreshFeverPurchaseButtonState();"), "Repeated purchase update must use the common function.");
            Require(refreshSource.Contains("_ferverSystem != null"), "Purchase state must be null-safe for FeverSystem.");
            Require(refreshSource.Contains("UserInfo.IsFeverTutorialClear"), "Purchase state must require tutorial completion.");
            Require(refreshSource.Contains("&& !_ferverSystem.IsFeverStart;"), "Purchase state must reject active Fever.");
            Require(refreshSource.Contains("_feverAdButton.Interactable(canPurchase);"), "Purchase state must update Interactable.");
            Require(refreshSource.Contains("_feverAdButton.gameObject.SetActive(canPurchase);"), "Purchase state must update GameObject visibility.");
            Require(
                !uiFeverSource.Contains("_feverAdButton.gameObject.SetActive(true)"),
                "UIFever must not contain an unconditional purchase-button activation.");
            Require(
                !uiFeverSource.Contains("SetActive(UserInfo.IsFeverTutorialClear)"),
                "Repeated purchase state must not use tutorial completion alone.");
            Require(!uiFeverSource.Contains("GameManager.Instance"), "UIFever purchase state must not create or fetch GameManager.");

            string popupSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/UI/Ad/UIAdPopup.cs"));
            string helperSource = ExtractSourceSection(
                popupSource,
                "private static bool IsFeverRuntimeActive()",
                "private void FixedUpdate()");
            string showFeverSource = ExtractSourceSection(
                popupSource,
                "public void ShowFeverPopup(WatchAdButton watchAdButton)",
                "public void ShowCustomerPopup(WatchAdButton watchAdButton)");
            string fixedUpdateSource = ExtractSourceSection(
                popupSource,
                "private void FixedUpdate()",
                "private void PushUIAd()");
            string adClickSource = ExtractSourceSection(
                popupSource,
                "private void AdButtonClicked()",
                "private void OnAdRewarded()");
            string diaClickSource = ExtractSourceSection(
                popupSource,
                "private void OnDiaButtonClicked()",
                "private void OnDestroy()");
            string rewardSource = ExtractSourceSection(
                popupSource,
                "private void OnAdRewarded()",
                "private void OnAdFailed()");

            Require(
                helperSource.Contains("GameManager.TryGetExistingInstance(out existingGameManager)"),
                "Fever popup guard must use the existing GameManager only.");
            Require(
                helperSource.Contains("existingGameManager.FeverRuntimeContext.IsActive"),
                "Fever popup guard must read the Runtime Context active state.");
            Require(!popupSource.Contains("GameManager.Instance"), "UIAdPopup must not create or fetch GameManager.");
            Require(
                CountOccurrences(popupSource, "IsFeverRuntimeActive()") == 5,
                "Fever Runtime guard must exist only in the helper and four pre-request paths.");
            Require(!rewardSource.Contains("IsFeverRuntimeActive"), "Reward handler must not silently discard an earned reward.");

            int showGuardIndex = showFeverSource.IndexOf("if (IsFeverRuntimeActive())", StringComparison.Ordinal);
            int showTypeIndex = showFeverSource.IndexOf("_currentAdType = AdType.Fever;", StringComparison.Ordinal);
            int pushIndex = showFeverSource.IndexOf("PushUIAd();", StringComparison.Ordinal);
            Require(showGuardIndex >= 0, "ShowFeverPopup must guard active Fever.");
            Require(showTypeIndex > showGuardIndex, "ShowFeverPopup guard must run before changing AdType.");
            Require(pushIndex > showGuardIndex, "ShowFeverPopup guard must run before opening the popup.");
            Require(showFeverSource.Contains("피버 타임 중에는 게이지를 충전할 수 없습니다."), "ShowFeverPopup must explain the active-Fever lock.");

            int adGuardIndex = adClickSource.IndexOf(
                "if (_currentAdType == AdType.Fever && IsFeverRuntimeActive())",
                StringComparison.Ordinal);
            int adCountIndex = adClickSource.IndexOf("ConstValue.AD_FEVER_COUNT", StringComparison.Ordinal);
            int adRequestIndex = adClickSource.IndexOf("_currentWatchAdButton.OnClickAd();", StringComparison.Ordinal);
            Require(adGuardIndex >= 0, "Fever ad click must guard active Fever.");
            Require(adCountIndex > adGuardIndex, "Fever active guard must run before ad-count checks.");
            Require(adRequestIndex > adGuardIndex, "Fever active guard must run before requesting an ad.");

            int diaGuardIndex = diaClickSource.IndexOf(
                "if (_currentAdType == AdType.Fever && IsFeverRuntimeActive())",
                StringComparison.Ordinal);
            int diaCountIndex = diaClickSource.IndexOf("ConstValue.AD_FEVER_COUNT", StringComparison.Ordinal);
            int diaValidationIndex = diaClickSource.IndexOf("UserInfo.IsDiaValid(needDia)", StringComparison.Ordinal);
            int diaChargeIndex = diaClickSource.IndexOf("UserInfo.AddDia(-needDia);", StringComparison.Ordinal);
            int diaRewardIndex = diaClickSource.IndexOf("_currentWatchAdButton.DiaRewarded();", StringComparison.Ordinal);
            Require(diaGuardIndex >= 0, "Fever dia click must guard active Fever.");
            Require(diaCountIndex > diaGuardIndex, "Fever active guard must run before dia-count checks.");
            Require(diaValidationIndex > diaGuardIndex, "Fever active guard must run before dia validation.");
            Require(diaChargeIndex > diaGuardIndex, "Fever active guard must run before dia deduction.");
            Require(diaRewardIndex > diaChargeIndex, "Dia reward must remain after successful dia deduction.");

            int fixedGuardIndex = fixedUpdateSource.IndexOf(
                "if (_currentAdType == AdType.Fever && IsFeverRuntimeActive())",
                StringComparison.Ordinal);
            int fixedCooldownIndex = fixedUpdateSource.IndexOf(
                "if (_currentAdType == AdType.Fever)",
                StringComparison.Ordinal);
            Require(fixedGuardIndex >= 0, "Open Fever popup must guard active Fever every FixedUpdate.");
            Require(fixedCooldownIndex > fixedGuardIndex, "Active Fever guard must run before cooldown UI logic.");
            Require(fixedUpdateSource.Contains("SetAdButtonInteractable(false);"), "Active Fever must disable the ad button.");
            Require(fixedUpdateSource.Contains("SetDiaButtonInteractable(false);"), "Active Fever must disable the dia button.");
            Require(fixedUpdateSource.Contains("return;"), "Active Fever FixedUpdate guard must not re-enable buttons later in the frame.");
        }

        private static void ValidateFeverMultiplierCalculator()
        {
            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(1f, 1f, 1f),
                "Normal customer base multiplier is incorrect.");
            RequireNear(
                2f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(1f, 1f, 2f),
                "Normal customer Fever multiplier is incorrect.");
            RequireNear(
                3f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(1f, 2f, 2f),
                "Normal customer Skill08 and Fever must clamp to 3.");
            RequireNear(
                0f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(0f, 1f, 1f),
                "A valid zero normal-customer multiplier must remain zero.");

            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(1f, 1f, 1f, 1f),
                "Staff base move multiplier is incorrect.");
            RequireNear(
                2f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(1f, 2f, 1f, 1f),
                "Skill01 Staff move multiplier is incorrect.");
            RequireNear(
                2f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(1f, 1f, 1f, 2f),
                "Fever Staff move multiplier is incorrect.");
            RequireNear(
                3f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(1f, 2f, 1f, 2f),
                "Skill01 and Fever must clamp Staff movement to 3.");
            RequireNear(
                3f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(1f, 2f, 1.5f, 2f),
                "Skill01, Skill10, and Fever must clamp Staff movement to 3.");
            RequireNear(
                0.5f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(0f, 1f, 1f, 1f),
                "A valid zero Staff multiplier must clamp to 0.5.");

            RequireNear(
                2.5f,
                FeverRuntimeMultiplierCalculator.CalculateRoleActionMultiplier(1.25f, 2f),
                "Role action Fever multiplication is incorrect.");

            RequireNear(
                2f,
                FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier(
                    1f, 1f, 1f, 1f, 2f, 1f, 1f),
                "Fever cooking multiplier is incorrect.");
            RequireNear(
                5f,
                FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier(
                    1f, 1f, 2.5f, 1.5f, 2f, 1f, 1f),
                "Skill04, Skill09, and Fever must clamp cooking to 5.");
            RequireNear(
                5f,
                FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier(
                    1f, 1f, 2.5f, 1.5f, 2f, 2f, 1.1f),
                "Burner and same-food multipliers must remain inside the final cooking cap.");
            RequireNear(
                0f,
                FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier(
                    0f, 1f, 1f, 1f, 1f, 1f, 1f),
                "A valid zero cooking multiplier must remain zero.");

            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(float.NaN, 1f, 1f),
                "NaN normal-customer input must return the neutral multiplier.");
            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(1f, float.PositiveInfinity, 1f),
                "Infinite normal-customer input must return the neutral multiplier.");
            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier(1f, 1f, -1f),
                "Negative normal-customer input must return the neutral multiplier.");
            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier(1f, float.NaN, 1f, 1f),
                "Invalid Staff input must return the neutral multiplier.");
            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateRoleActionMultiplier(float.PositiveInfinity, 1f),
                "Invalid role-action input must return the neutral multiplier.");
            RequireNear(
                1f,
                FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier(
                    1f, 1f, 1f, 1f, 1f, -1f, 1f),
                "Invalid cooking input must return the neutral multiplier.");

            Type calculatorType = typeof(FeverRuntimeMultiplierCalculator);
            Require(calculatorType.IsAbstract && calculatorType.IsSealed, "Fever multiplier calculator must be static.");
            foreach (FieldInfo field in calculatorType.GetFields(
                         BindingFlags.Static
                         | BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.NonPublic
                         | BindingFlags.DeclaredOnly))
            {
                Require(field.IsLiteral, "Fever multiplier calculator must not contain a Runtime field: " + field.Name);
                Require(
                    !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType),
                    "Fever multiplier calculator must not reference a Unity Object: " + field.Name);
                Require(
                    !IsMutableCollectionType(field.FieldType),
                    "Fever multiplier calculator must not contain a mutable collection: " + field.Name);
            }

            foreach (PropertyInfo property in calculatorType.GetProperties(
                         BindingFlags.Static
                         | BindingFlags.Instance
                         | BindingFlags.Public
                         | BindingFlags.NonPublic
                         | BindingFlags.DeclaredOnly))
            {
                Require(property.GetSetMethod(true) == null, "Fever multiplier calculator property must be read-only: " + property.Name);
            }

            string source = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/FeverSystem/FeverRuntimeMultiplierCalculator.cs"));
            Require(
                CountOccurrences(source, "FEVER_POLICY_2026_08_19_V2") == 1,
                "The Fever V2 calculator policy marker must exist exactly once.");
            Require(!source.Contains("using UnityEngine"), "Fever multiplier calculator must not depend on UnityEngine.");
            Require(source.Contains("(double)existingMultiplier"), "Movement multiplication must start in double precision.");
            Require(source.Contains("(double)existingCookingMultiplier"), "Cooking multiplication must start in double precision.");
            Require(!source.Contains("params "), "Fever multiplier calculator must not allocate a params array.");
            Require(!source.Contains("List<"), "Fever multiplier calculator must not contain a List.");
            Require(!source.Contains("Dictionary<"), "Fever multiplier calculator must not contain a Dictionary.");
            Require(!source.Contains("System.Linq"), "Fever multiplier calculator must not use LINQ.");
            Require(!source.Contains("Func<"), "Fever multiplier calculator must not contain a Func delegate.");
            Require(!source.Contains("Action<"), "Fever multiplier calculator must not contain an Action delegate.");
        }

        private static void ValidateLegacyFeverBridgeRemoval()
        {
            string feverSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/FeverSystem/FerverSystem.cs"));
            Require(!feverSource.Contains("SetLegacyFeverSpeed"), "Legacy Fever speed bridge method and calls must be removed.");
            Require(!feverSource.Contains("SetGameSpeed"), "FeverSystem must not call SetGameSpeed.");
            Require(
                !feverSource.Contains("FEVER_LEGACY_SPEED_BRIDGE_REMOVE_IN_FEVER_01_C"),
                "The completed legacy bridge marker must be removed.");
            Require(feverSource.Contains("_feverRuntimeContext.TryActivate("), "Fever Context activation must remain.");
            Require(feverSource.Contains("context.Advance(activeToken, deltaSeconds)"), "Fever Context clock must remain.");
            Require(feverSource.Contains("_feverRuntimeContext?.Deactivate(token);"), "Fever Context cleanup must remain.");
            Require(feverSource.Contains("float deltaSeconds = Time.deltaTime;"), "Fever actual-delta clock must remain.");
            Require(feverSource.Contains("result.AutoCallOpportunityCount"), "Fever auto-call opportunities must remain.");
            Require(feverSource.Contains("private void StopFeverRuntime("), "Common Fever cleanup must remain.");

            string gameManagerSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Manager/GameManager.cs"));
            Require(gameManagerSource.Contains("public void SetGameSpeed(float value)"), "GameManager legacy SetGameSpeed definition must remain.");
            Require(gameManagerSource.Contains("private float _totalAddSpeedMul = 0;"), "GameManager legacy speed field must remain.");

            string runtimeRoot = Path.Combine(Application.dataPath, "Scripts");
            string[] runtimeFiles = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            int runtimeCallCount = 0;
            foreach (string runtimeFile in runtimeFiles)
            {
                runtimeCallCount += CountOccurrences(File.ReadAllText(runtimeFile), ".SetGameSpeed(");
            }

            Require(runtimeCallCount == 0, "Project Runtime SetGameSpeed call count must be zero.");
        }

        private static void ValidateFeverFoodPriceBoundary()
        {
            string tableSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Table/TableManager.cs"));
            string startCoinSource = ExtractSourceSection(
                tableSource,
                "private void StartCoinAnime(TableData data)",
                "private void StartGarbageAnime(TableData data)");
            string endEatSource = ExtractSourceSection(
                tableSource,
                "private void EndEat(TableData data)",
                "private void DirtyTable(TableData data)");

            Require(
                startCoinSource.Contains("GameManager.TryGetExistingInstance(out existingGameManager)"),
                "Food payment must query only an existing GameManager.");
            Require(
                CountOccurrences(startCoinSource, "FeverRuntimeContext.FoodPriceMultiplier") == 1,
                "Food payment must read the Fever food-price multiplier exactly once.");
            Require(!startCoinSource.Contains("GameManager.Instance"), "Food payment must not create or fetch GameManager.");
            Require(!startCoinSource.Contains("IsFeverStart ? 2f : 1f"), "Food payment must not use the legacy Fever ternary.");
            Require(
                startCoinSource.Contains("data.TotalPrice * feverFoodPriceMultiplier"),
                "Fever must multiply only the snapshotted food price.");
            Require(!startCoinSource.Contains("data.TotalTip *"), "Fever must not multiply the food payment tip.");
            Require(startCoinSource.Contains("data.TotalTip = 0;"), "Food payment tip cleanup must remain.");
            Require(
                tableSource.Contains("cookingData.Price * data.CurrentCustomer.CurrentFoodPriceMul"),
                "Skill05 order-time price snapshot must remain.");
            Require(
                endEatSource.Contains("int basePaymentTip = data.TotalTip;"),
                "Skill06 payment-tip snapshot must remain.");
            Require(
                endEatSource.Contains("StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout("),
                "Skill06 payment-tip calculator must remain.");

            string gameManagerSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Manager/GameManager.cs"));
            Require(
                gameManagerSource.Contains("private const float BaseFoodTipMultiplier = 0.5f;"),
                "The 50 percent base food-tip policy must remain.");
        }

        private static void ValidateNormalCustomerMoveChannel()
        {
            string normalSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/NPC/NormalCustomer.cs"));
            string moveSource = ExtractSourceSection(
                normalSource,
                "protected override IEnumerator MoveRoutine(List<Vector2> nodeList, Action onCompleted = null)",
                "private void StopCoroutines()");

            Require(
                moveSource.Contains("FeverRuntimeMultiplierCalculator.CalculateNormalCustomerMoveMultiplier("),
                "Normal customer movement must use the Fever multiplier calculator.");
            Require(moveSource.Contains("gameManager.AddCustomerSpeedMul"), "Existing customer movement multiplier must remain.");
            Require(
                CountOccurrences(moveSource, "StaffSkillEffectType.NormalCustomerMovePercent") == 1,
                "Skill08 Registry multiplier must be read exactly once.");
            Require(
                CountOccurrences(moveSource, "FeverRuntimeContext.NormalCustomerMoveMultiplier") == 1,
                "Normal-customer Fever multiplier must be read exactly once.");
            Require(moveSource.Contains("Time.deltaTime"), "Normal customer movement must retain Time.deltaTime.");
            Require(moveSource.Contains("_moveSpeed"), "Normal customer movement must retain the skin-adjusted move speed.");
            Require(moveSource.Contains("* 0.7f"), "Normal customer movement must retain the 0.7 factor.");
            Require(moveSource.Contains("currentPos.x += direction.x * step;"), "Normal customer GC-free movement must remain.");
            Require(
                FeverRuntimeMultiplierCalculator.MaxNormalCustomerMoveMultiplier == 3f,
                "Normal customer movement cap must be 3.");

            string[] excludedCustomerPaths =
            {
                "Assets/Scripts/NPC/Customer.cs",
                "Assets/Scripts/NPC/SpecialCustomer.cs",
                "Assets/Scripts/NPC/GatecrasherCustomer.cs"
            };
            foreach (string excludedPath in excludedCustomerPaths)
            {
                string excludedSource = ReadProjectText(excludedPath);
                Require(!excludedSource.Contains("FeverRuntimeContext"), excludedPath + " must not consume Fever Context.");
                Require(!excludedSource.Contains("FeverRuntimeMultiplierCalculator"), excludedPath + " must not consume the Fever calculator.");
                Require(!excludedSource.Contains("NormalCustomerMovePercent"), excludedPath + " must not consume Skill08.");
            }
        }

        private static void ValidateStaffMoveAndRoleActionChannels()
        {
            string staffSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Staff/Staff.cs"));
            string speedSource = ExtractSourceSection(staffSource, "public float SpeedMul", "public float MoveSpeedMul");
            string moveSource = ExtractSourceSection(staffSource, "public float MoveSpeedMul", "public float WorkSpeedMul");
            string workSource = ExtractSourceSection(staffSource, "public float WorkSpeedMul", "public float GuardEliminationSpeedMul");
            string guardSource = ExtractSourceSection(staffSource, "public float GuardEliminationSpeedMul", "protected Coroutine _useSkillRoutine;");

            Require(
                moveSource.Contains("FeverRuntimeMultiplierCalculator.CalculateStaffMoveMultiplier("),
                "Staff movement must use the Fever multiplier calculator.");
            Require(moveSource.Contains("gameManager.GetStaffMoveSpeedMul(_staffGroupType)"), "Existing Staff movement multiplier must remain.");
            Require(moveSource.Contains("_skillRuntimeContext.PersonalMoveBonusPercent"), "Skill01 personal movement multiplier must remain.");
            Require(
                CountOccurrences(moveSource, "StaffSkillEffectType.AllStaffMovePercent") == 1,
                "Skill10 Registry multiplier must be read exactly once.");
            Require(
                CountOccurrences(moveSource, "FeverRuntimeContext.StaffMoveMultiplier") == 1,
                "Staff Fever movement multiplier must be read exactly once.");
            Require(
                FeverRuntimeMultiplierCalculator.MaxStaffMoveMultiplier == 3f,
                "Staff movement cap must be 3.");
            Require(!speedSource.Contains("FeverRuntimeContext"), "Staff SpeedMul must not consume Fever Context.");
            Require(!speedSource.Contains("FeverRuntimeMultiplierCalculator"), "Staff SpeedMul must not consume the Fever calculator.");
            Require(!workSource.Contains("FeverRuntimeContext"), "Staff WorkSpeedMul must not consume Fever Context.");
            Require(!guardSource.Contains("FeverRuntimeContext"), "Guard elimination speed must not consume Fever Context.");

            string managerSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Staff/StaffAction/ManagerAction.cs"));
            Require(managerSource.Contains("staff.SpeedMul"), "Manager action must preserve the existing Staff speed multiplier.");
            Require(managerSource.Contains("FeverRuntimeContext.ManagerGuideMultiplier"), "Manager action must consume its Fever channel.");
            Require(
                managerSource.Contains("FeverRuntimeMultiplierCalculator.CalculateRoleActionMultiplier("),
                "Manager action must use the role-action calculator.");

            string marketerSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Staff/StaffAction/MarketerAction.cs"));
            Require(marketerSource.Contains("staff.SpeedMul"), "Marketer action must preserve the existing Staff speed multiplier.");
            Require(marketerSource.Contains("FeverRuntimeContext.MarketerCallMultiplier"), "Marketer action must consume its Fever channel.");
            Require(
                marketerSource.Contains("FeverRuntimeMultiplierCalculator.CalculateRoleActionMultiplier("),
                "Marketer action must use the role-action calculator.");
            Require(marketerSource.Contains("_customerController.AddTabCount();"), "Marketer customer-call action must remain.");

            string[] excludedActionPaths =
            {
                "Assets/Scripts/Staff/StaffAction/WaiterAction.cs",
                "Assets/Scripts/Staff/StaffAction/CleanerAction.cs",
                "Assets/Scripts/Staff/StaffAction/GuardAction.cs",
                "Assets/Scripts/Staff/StaffAction/ChefAction.cs"
            };
            foreach (string excludedPath in excludedActionPaths)
            {
                string excludedSource = ReadProjectText(excludedPath);
                Require(!excludedSource.Contains("FeverRuntimeContext"), excludedPath + " must not consume Fever Context.");
                Require(!excludedSource.Contains("FeverRuntimeMultiplierCalculator"), excludedPath + " must not consume the Fever calculator.");
                Require(!excludedSource.Contains("IsFeverStart"), excludedPath + " must not consume Fever state.");
            }
        }

        private static void ValidateCookingChannelAndCap()
        {
            string kitchenSource = NormalizeSourceLineEndings(
                ReadProjectText("Assets/Scripts/Kitchen/KitchenUtensilGroup.cs"));
            Require(
                kitchenSource.Contains("FeverRuntimeMultiplierCalculator.CalculateCookingMultiplier("),
                "Kitchen cooking must use the Fever multiplier calculator.");
            Require(kitchenSource.Contains("gameManager.GetCookingSpeedMul("), "Existing global cooking multiplier must remain.");
            Require(
                kitchenSource.Contains("CalculateAssignedCookingSpeedMultiplier("),
                "Skill04 assigned-cooking multiplier must remain.");
            Require(
                CountOccurrences(kitchenSource, "StaffSkillEffectType.GlobalCookingSpeedPercent") == 1,
                "Skill09 Registry multiplier must be read exactly once.");
            Require(
                CountOccurrences(kitchenSource, "FeverRuntimeContext.CookingMultiplier") == 1,
                "Fever cooking multiplier must be read exactly once.");
            Require(kitchenSource.Contains("burnerData.AddCookSpeedMul * 0.01f"), "Assigned Chef role multiplier must remain.");
            Require(kitchenSource.Contains("assignedStaff.SpeedMul"), "Assigned Chef role speed must remain.");
            Require(kitchenSource.Contains("_burnerKitchenUtensils[i].CookSpeedMul"), "Burner multiplier must remain.");
            Require(kitchenSource.Contains("sameFoodTypeMultiplier = 1.1f;"), "Same-food 1.1 multiplier must remain.");
            Require(kitchenSource.Contains("Time.deltaTime * finalCookingMultiplier"), "Cooking must apply the final multiplier to delta time.");
            Require(!kitchenSource.Contains("PersonalMoveBonusPercent"), "Cooking must not consume Skill01 personal movement.");
            Require(!kitchenSource.Contains("AllStaffMovePercent"), "Cooking must not consume Skill10 Staff movement.");
            Require(
                FeverRuntimeMultiplierCalculator.MaxCookingMultiplier == 5f,
                "Final cooking multiplier cap must be 5.");

            string sinkSource = ReadProjectText("Assets/Scripts/Kitchen/SinkKitchenUtensil.cs");
            Require(!sinkSource.Contains("FeverRuntimeContext"), "Sink must not consume Fever Context.");
            Require(!sinkSource.Contains("FeverRuntimeMultiplierCalculator"), "Sink must not consume the Fever calculator.");
            Require(!sinkSource.Contains("IsFeverStart"), "Sink must not consume Fever state.");
        }

        private static void ValidateFeverMultipliers(
            FeverRuntimeContext context,
            float expected,
            string stateName)
        {
            RequireNear(expected, context.FoodPriceMultiplier, stateName + " food price multiplier is incorrect.");
            RequireNear(expected, context.NormalCustomerMoveMultiplier, stateName + " customer move multiplier is incorrect.");
            RequireNear(expected, context.StaffMoveMultiplier, stateName + " Staff move multiplier is incorrect.");
            RequireNear(expected, context.ManagerGuideMultiplier, stateName + " manager guide multiplier is incorrect.");
            RequireNear(expected, context.MarketerCallMultiplier, stateName + " marketer call multiplier is incorrect.");
            RequireNear(expected, context.CookingMultiplier, stateName + " cooking multiplier is incorrect.");
        }

        private static void AssertFeverClockUnchangedAfterException(
            FeverRuntimeContext context,
            Action action,
            string message)
        {
            FeverRuntimeToken tokenBefore = context.CurrentToken;
            float durationBefore = context.DurationSeconds;
            float elapsedBefore = context.ElapsedSeconds;
            float remainderBefore = context.AutoCallRemainderSeconds;
            float ratioBefore = context.RemainingRatio;
            RequireThrows<ArgumentOutOfRangeException>(action, message);
            Require(context.CurrentToken == tokenBefore, message + " Current token changed.");
            RequireNear(durationBefore, context.DurationSeconds, message + " Duration changed.");
            RequireNear(elapsedBefore, context.ElapsedSeconds, message + " Elapsed time changed.");
            RequireNear(remainderBefore, context.AutoCallRemainderSeconds, message + " Remainder changed.");
            RequireNear(ratioBefore, context.RemainingRatio, message + " Remaining ratio changed.");
        }

        private static string ReadProjectText(string relativePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            Require(!string.IsNullOrEmpty(projectRoot), "Unity project root could not be resolved.");
            string fullPath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Require(File.Exists(fullPath), "Required source file is missing: " + relativePath);
            return File.ReadAllText(fullPath);
        }

        private static string NormalizeSourceLineEndings(string source)
        {
            return source.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private static string ExtractSourceSection(
            string source,
            string startMarker,
            string endMarker)
        {
            int startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
            Require(startIndex >= 0, "Source start marker is missing: " + startMarker);
            int endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            Require(endIndex > startIndex, "Source end marker is missing: " + endMarker);
            return source.Substring(startIndex, endIndex - startIndex);
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static void RequireProjectSourceDoesNotContain(
            string relativePath,
            string forbiddenValue)
        {
            Require(
                !ReadProjectText(relativePath).Contains(forbiddenValue),
                relativePath + " must not contain " + forbiddenValue + ".");
        }

        private static void ValidateDeclaredInstanceFields(
            Type ownerType,
            params string[] expectedFieldNames)
        {
            HashSet<string> remainingNames = new HashSet<string>(expectedFieldNames);
            FieldInfo[] fields = ownerType.GetFields(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);

            foreach (FieldInfo field in fields)
            {
                Require(
                    remainingNames.Remove(field.Name),
                    ownerType.Name + "." + field.Name + " is an unexpected instance field.");
            }

            Require(
                remainingNames.Count == 0,
                ownerType.Name + " is missing an expected serialized field.");
        }

        private static void ValidateReadOnlyProperty(
            Type ownerType,
            string propertyName,
            Type expectedType)
        {
            PropertyInfo property = ownerType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Require(property != null, ownerType.Name + "." + propertyName + " must exist.");
            Require(property.PropertyType == expectedType, ownerType.Name + "." + propertyName + " type is incorrect.");
            Require(property.GetSetMethod(true) == null, ownerType.Name + "." + propertyName + " must not have a setter.");
        }

        private static void AssertRegistryUnchangedAfterException(
            StaffSkillEffectRegistry registry,
            StaffSkillEffectType baselineType,
            Action action,
            string message)
        {
            int totalBefore = registry.TotalSourceCount;
            int typeCountBefore = registry.GetSourceCount(baselineType);
            float highestBefore = registry.GetHighestPercent(baselineType);
            RequireThrows<ArgumentOutOfRangeException>(action, message);
            Require(registry.TotalSourceCount == totalBefore, message + " Total count changed.");
            Require(registry.GetSourceCount(baselineType) == typeCountBefore, message + " Type count changed.");
            RequireNear(highestBefore, registry.GetHighestPercent(baselineType), message + " Highest changed.");
        }

        private static void AssertPercentUnchangedAfterException(
            StaffSkillRuntimeContext context,
            Action action,
            string message)
        {
            float moveBefore = context.PersonalMoveBonusPercent;
            float cookingBefore = context.AssignedCookingBonusPercent;
            RequireThrows<ArgumentOutOfRangeException>(action, message);
            RequireNear(moveBefore, context.PersonalMoveBonusPercent, message + " Move percent changed.");
            RequireNear(cookingBefore, context.AssignedCookingBonusPercent, message + " Cooking percent changed.");
        }

        private static void Cancel(
            StaffSkillRuntimeContext context,
            StaffSkillSourceToken token)
        {
            Require(context.TryBeginCancellation(token), "Cancellation must begin for the current token.");
            context.MarkDeactivationCompleted(token);
            context.CompleteCancellation(token);
        }

        private static bool IsMutableCollectionType(Type type)
        {
            if (type == typeof(string))
            {
                return false;
            }

            if (typeof(IList).IsAssignableFrom(type) || typeof(IDictionary).IsAssignableFrom(type))
            {
                return true;
            }

            if (!type.IsGenericType)
            {
                return false;
            }

            Type definition = type.GetGenericTypeDefinition();
            return definition == typeof(List<>)
                   || definition == typeof(Dictionary<,>)
                   || definition == typeof(IList<>)
                   || definition == typeof(IDictionary<,>);
        }

        private static bool IsForbiddenRuntimeType(Type type)
        {
            string typeName = type.Name;
            return typeName == "GameObject"
                   || typeName == "Staff"
                   || typeName == "SkillBase"
                   || typeName == "StaffData"
                   || typeName == "SerializedObject";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void RequireNear(float expected, float actual, string message)
        {
            if (Math.Abs(expected - actual) > FloatTolerance)
            {
                throw new InvalidOperationException(
                    message + " Expected: " + expected + ", actual: " + actual + ".");
            }
        }

        private static void RequireThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message + " Expected " + typeof(TException).Name + ".");
        }

        private sealed class ValidationReport
        {
            private readonly StringBuilder _output = new StringBuilder();
            private readonly List<string> _errors = new List<string>();

            internal ValidationReport()
            {
                _output.AppendLine("[Staff Skill Runtime Foundation Validation]");
            }

            internal void Run(int index, string name, Action validation)
            {
                try
                {
                    validation();
                    _output.AppendLine(index + ". " + name + ": PASS");
                }
                catch (Exception exception)
                {
                    _errors.Add(index + ". " + name + ": " + exception.Message);
                    _output.AppendLine(index + ". " + name + ": FAIL");
                }
            }

            internal void Print()
            {
                _output.AppendLine("59. 오류 수: " + _errors.Count);
                for (int index = 0; index < _errors.Count; index++)
                {
                    _output.AppendLine("ERROR: " + _errors[index]);
                }

                bool passed = _errors.Count == 0;
                _output.AppendLine(
                    "60. 최종 결과: STAFF SKILL RUNTIME FOUNDATION VALIDATION: "
                    + (passed ? "PASS" : "FAIL"));

                if (passed)
                {
                    Debug.Log(_output.ToString());
                }
                else
                {
                    Debug.LogError(_output.ToString());
                }
            }
        }
    }
}
