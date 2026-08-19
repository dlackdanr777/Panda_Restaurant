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
                coinSource.Contains("data.TotalPrice * (_feverSystem.IsFeverStart ? 2f : 1f)"),
                "StartCoinAnime must preserve the independent Fever 2x price multiplier.");
            Require(
                endEatSource.Contains("StaffSkillEffectType.RestaurantTipPayoutPercent"),
                "EndEat must preserve the Skill06 payment tip Registry.");
            Require(
                endEatSource.Contains("StaffPaymentTipCalculator.CalculateFoodPaymentTipPayout"),
                "EndEat must preserve the Skill06 final payment tip calculation.");
            Require(endEatSource.Contains("int finalPaymentTip"), "EndEat must preserve finalPaymentTip.");
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
                _output.AppendLine("45. 오류 수: " + _errors.Count);
                for (int index = 0; index < _errors.Count; index++)
                {
                    _output.AppendLine("ERROR: " + _errors[index]);
                }

                bool passed = _errors.Count == 0;
                _output.AppendLine(
                    "46. 최종 결과: STAFF SKILL RUNTIME FOUNDATION VALIDATION: "
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
