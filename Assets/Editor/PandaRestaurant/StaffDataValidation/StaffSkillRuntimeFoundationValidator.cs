using System;
using System.Collections;
using System.Collections.Generic;
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
                typeof(StaffSkillCancellationReason)
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
                _output.AppendLine("20. 오류 수: " + _errors.Count);
                for (int index = 0; index < _errors.Count; index++)
                {
                    _output.AppendLine("ERROR: " + _errors[index]);
                }

                bool passed = _errors.Count == 0;
                _output.AppendLine(
                    "21. 최종 결과: STAFF SKILL RUNTIME FOUNDATION VALIDATION: "
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
