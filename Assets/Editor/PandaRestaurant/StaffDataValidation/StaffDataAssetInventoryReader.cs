using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal static class StaffDataAssetInventoryReader
    {
        internal const string StaffDataFolder = "Assets/Resources/StaffData";
        internal const string SkillFolder = "Assets/Scripts/Datas/Staff/Skill";

        internal static bool TryBuildReadOnlyInventory(
            out StaffDataAssetInventorySnapshot snapshot,
            out IReadOnlyList<string> diagnostics)
        {
            snapshot = null;
            List<string> errors = new List<string>();
            Dictionary<string, HashSet<string>> referencedStaffIdsBySkillGuid =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            List<StaffDataAssetSnapshot> staff = ReadStaffDataAssets(
                referencedStaffIdsBySkillGuid,
                errors);
            List<StaffSkillAssetSnapshot> skills = ReadSkillAssets(
                referencedStaffIdsBySkillGuid,
                errors);

            ValidateIdentityUniqueness(staff, skills, errors);
            if (errors.Count == 0)
            {
                try
                {
                    snapshot = new StaffDataAssetInventorySnapshot(staff, skills);
                }
                catch (Exception exception)
                {
                    errors.Add("Inventory Snapshot 생성에 실패했습니다: " + exception.Message);
                }
            }

            List<string> diagnosticCopy = new List<string>();
            for (int index = 0; index < errors.Count; index++)
            {
                diagnosticCopy.Add("ERROR: " + errors[index]);
            }

            diagnostics = diagnosticCopy.AsReadOnly();
            if (errors.Count != 0 || snapshot == null)
            {
                snapshot = null;
                return false;
            }

            return true;
        }

        internal static IReadOnlyList<string> FindTargetAssetPaths()
        {
            List<string> paths = new List<string>();
            string[] staffGuids = AssetDatabase.FindAssets("t:StaffData", new[] { StaffDataFolder });
            for (int index = 0; index < staffGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(staffGuids[index]);
                if (IsAssetFileInFolder(path, StaffDataFolder))
                {
                    paths.Add(path);
                }
            }

            string[] skillGuids = AssetDatabase.FindAssets(string.Empty, new[] { SkillFolder });
            for (int index = 0; index < skillGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(skillGuids[index]);
                if (IsAssetFileInFolder(path, SkillFolder))
                {
                    paths.Add(path);
                }
            }

            return paths.Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList().AsReadOnly();
        }

        private static List<StaffDataAssetSnapshot> ReadStaffDataAssets(
            Dictionary<string, HashSet<string>> referencedStaffIdsBySkillGuid,
            List<string> errors)
        {
            List<StaffDataAssetSnapshot> result = new List<StaffDataAssetSnapshot>();
            string[] guids = AssetDatabase.FindAssets("t:StaffData", new[] { StaffDataFolder });
            string[] paths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => IsAssetFileInFolder(path, StaffDataFolder))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                StaffData asset = AssetDatabase.LoadAssetAtPath<StaffData>(path);
                if (asset == null)
                {
                    errors.Add("StaffData 에셋을 읽지 못했습니다: " + path);
                    continue;
                }

                StaffDataAssetSnapshot staffSnapshot = ReadStaffDataAsset(
                    path,
                    asset,
                    referencedStaffIdsBySkillGuid,
                    errors);
                if (staffSnapshot != null)
                {
                    result.Add(staffSnapshot);
                }
            }

            return result;
        }

        private static StaffDataAssetSnapshot ReadStaffDataAsset(
            string assetPath,
            StaffData asset,
            Dictionary<string, HashSet<string>> referencedStaffIdsBySkillGuid,
            List<string> errors)
        {
            string context = "StaffData " + assetPath;
            SerializedObject serializedObject = new SerializedObject(asset);
            serializedObject.Update();

            string concreteTypeName = asset.GetType().Name;
            RoleDefinition role;
            if (!TryGetRoleDefinition(concreteTypeName, out role))
            {
                errors.Add(context + "의 알 수 없는 파생 클래스입니다: " + concreteTypeName);
                return null;
            }

            SerializedProperty idProperty = FindRequired(serializedObject, "_id", context, errors);
            SerializedProperty nameProperty = FindRequired(serializedObject, "_name", context, errors);
            SerializedProperty descriptionProperty = FindRequired(serializedObject, "_description", context, errors);
            SerializedProperty rankProperty = FindRequired(serializedObject, "_rank", context, errors);
            SerializedProperty speedProperty = FindRequired(serializedObject, "_speed", context, errors);
            SerializedProperty salesLocationProperty = FindRequired(
                serializedObject,
                "_salesLocationType",
                context,
                errors);
            SerializedProperty moneyTypeProperty = FindRequired(
                serializedObject,
                "_moneyType",
                context,
                errors);
            SerializedProperty buyScoreProperty = FindRequired(serializedObject, "_buyScore", context, errors);
            SerializedProperty buyPriceProperty = FindRequired(serializedObject, "_buyPrice", context, errors);
            SerializedProperty skillProperty = FindRequired(serializedObject, "_skill", context, errors);
            SerializedProperty spriteProperty = FindRequired(serializedObject, "_sprite", context, errors);
            SerializedProperty thumbnailProperty = FindRequired(
                serializedObject,
                "_thumbnailSprite",
                context,
                errors);
            SerializedProperty animatorProperty = FindRequired(
                serializedObject,
                "_animatorController",
                context,
                errors);
            SerializedProperty idleSpritesProperty = FindRequired(
                serializedObject,
                "_idleSprites",
                context,
                errors);

            SerializedProperty levelArrayProperty = FindRequired(
                serializedObject,
                role.LevelArrayPropertyPath,
                context,
                errors);
            bool hasExpectedLevelArray = levelArrayProperty != null && levelArrayProperty.isArray;
            if (levelArrayProperty != null && !levelArrayProperty.isArray)
            {
                errors.Add(context + "의 레벨 Property가 배열이 아닙니다: " + role.LevelArrayPropertyPath);
            }

            if (HasNullProperty(
                    idProperty,
                    nameProperty,
                    descriptionProperty,
                    rankProperty,
                    speedProperty,
                    salesLocationProperty,
                    moneyTypeProperty,
                    buyScoreProperty,
                    buyPriceProperty,
                    skillProperty,
                    spriteProperty,
                    thumbnailProperty,
                    animatorProperty,
                    idleSpritesProperty,
                    levelArrayProperty))
            {
                return null;
            }

            string id = idProperty.stringValue ?? string.Empty;
            StaffAssetReferenceSnapshot skillReference = ReadReference(skillProperty, "_skill", context, errors);
            StaffAssetReferenceSnapshot spriteReference = ReadReference(spriteProperty, "_sprite", context, errors);
            StaffAssetReferenceSnapshot thumbnailReference = ReadReference(
                thumbnailProperty,
                "_thumbnailSprite",
                context,
                errors);
            StaffAssetReferenceSnapshot animatorReference = ReadReference(
                animatorProperty,
                "_animatorController",
                context,
                errors);
            List<StaffAssetReferenceSnapshot> idleReferences = ReadReferenceArray(
                idleSpritesProperty,
                "_idleSprites",
                context,
                errors);

            bool hasMissingRequiredReference = IsRequiredReferenceInvalid(skillReference)
                                               || IsRequiredReferenceInvalid(spriteReference)
                                               || IsRequiredReferenceInvalid(thumbnailReference)
                                               || animatorReference.IsMissing
                                               || idleReferences.Any(reference => reference.IsMissing);
            if (IsRequiredReferenceInvalid(skillReference))
            {
                errors.Add(context + "의 Skill 참조가 비었거나 Missing 상태입니다.");
            }

            if (IsRequiredReferenceInvalid(spriteReference))
            {
                errors.Add(context + "의 Sprite 참조가 비었거나 Missing 상태입니다.");
            }

            if (IsRequiredReferenceInvalid(thumbnailReference))
            {
                errors.Add(context + "의 Thumbnail 참조가 비었거나 Missing 상태입니다.");
            }

            if (animatorReference.IsMissing || idleReferences.Any(reference => reference.IsMissing))
            {
                errors.Add(context + "의 공통 시각 참조에 Missing Reference가 있습니다.");
            }

            string skillConcreteTypeName = string.Empty;
            float skillDuration = 0f;
            float skillCooldown = 0f;
            SkillBase skillAsset = skillProperty.objectReferenceValue as SkillBase;
            if (skillProperty.objectReferenceValue != null && skillAsset == null)
            {
                errors.Add(context + "의 _skill이 SkillBase가 아닙니다.");
            }
            else if (skillAsset != null)
            {
                skillConcreteTypeName = skillAsset.GetType().Name;
                ReadSkillTiming(skillAsset, context, errors, out skillDuration, out skillCooldown);
                if (!IsPathInFolder(skillReference.AssetPath, SkillFolder))
                {
                    errors.Add(context + "의 Skill이 지정 폴더 밖을 참조합니다: " + skillReference.AssetPath);
                }

                if (!string.IsNullOrEmpty(skillReference.AssetGuid) && !string.IsNullOrEmpty(id))
                {
                    HashSet<string> referencedStaffIds;
                    if (!referencedStaffIdsBySkillGuid.TryGetValue(
                            skillReference.AssetGuid,
                            out referencedStaffIds))
                    {
                        referencedStaffIds = new HashSet<string>(StringComparer.Ordinal);
                        referencedStaffIdsBySkillGuid.Add(skillReference.AssetGuid, referencedStaffIds);
                    }

                    if (!referencedStaffIds.Add(id))
                    {
                        errors.Add(
                            "동일 Staff가 같은 Skill에 중복 등록되었습니다: " + id
                            + " -> " + skillReference.AssetGuid);
                    }
                }
            }

            List<StaffLevelAssetSnapshot> levels = ReadLevels(
                levelArrayProperty,
                role,
                context,
                errors);

            StaffAssetReferenceSnapshot backSpriteReference = null;
            StaffAssetReferenceSnapshot handSpriteReference = null;
            float handOffsetX = 0f;
            float handOffsetY = 0f;
            StaffAssetReferenceSnapshot uiSpriteReference = null;
            StaffAssetReferenceSnapshot animationSpriteReference = null;
            int particleCount = 0;
            List<StaffAssetReferenceSnapshot> particleSpriteReferences =
                new List<StaffAssetReferenceSnapshot>();
            bool hasChefAddSpeedField = false;

            if (string.Equals(concreteTypeName, "ChefData", StringComparison.Ordinal))
            {
                SerializedProperty backSpriteProperty = FindRequired(
                    serializedObject,
                    "_backSprite",
                    context,
                    errors);
                SerializedProperty handSpriteProperty = FindRequired(
                    serializedObject,
                    "_handSprite",
                    context,
                    errors);
                SerializedProperty handOffsetProperty = FindRequired(
                    serializedObject,
                    "_handOffset",
                    context,
                    errors);
                if (backSpriteProperty != null)
                {
                    backSpriteReference = ReadReference(backSpriteProperty, "_backSprite", context, errors);
                    hasMissingRequiredReference |= backSpriteReference.IsMissing;
                }

                if (handSpriteProperty != null)
                {
                    handSpriteReference = ReadReference(handSpriteProperty, "_handSprite", context, errors);
                    hasMissingRequiredReference |= handSpriteReference.IsMissing;
                }

                if (handOffsetProperty != null)
                {
                    Vector2 handOffset = handOffsetProperty.vector2Value;
                    handOffsetX = handOffset.x;
                    handOffsetY = handOffset.y;
                }

                hasChefAddSpeedField = levelArrayProperty.arraySize > 0
                                        && levelArrayProperty.GetArrayElementAtIndex(0)
                                            .FindPropertyRelative("_addSpeed") != null;
            }
            else if (string.Equals(concreteTypeName, "MarketerData", StringComparison.Ordinal))
            {
                SerializedProperty uiSpriteProperty = FindRequired(
                    serializedObject,
                    "_uiSprite",
                    context,
                    errors);
                SerializedProperty animationSpriteProperty = FindRequired(
                    serializedObject,
                    "_animationSprite",
                    context,
                    errors);
                SerializedProperty particleCountProperty = FindRequired(
                    serializedObject,
                    "_particleCount",
                    context,
                    errors);
                SerializedProperty particleSpritesProperty = FindRequired(
                    serializedObject,
                    "_particleSprites",
                    context,
                    errors);
                if (uiSpriteProperty != null)
                {
                    uiSpriteReference = ReadReference(uiSpriteProperty, "_uiSprite", context, errors);
                    hasMissingRequiredReference |= uiSpriteReference.IsMissing;
                }

                if (animationSpriteProperty != null)
                {
                    animationSpriteReference = ReadReference(
                        animationSpriteProperty,
                        "_animationSprite",
                        context,
                        errors);
                    hasMissingRequiredReference |= animationSpriteReference.IsMissing;
                }

                if (particleCountProperty != null)
                {
                    particleCount = particleCountProperty.intValue;
                }

                if (particleSpritesProperty != null)
                {
                    particleSpriteReferences = ReadReferenceArray(
                        particleSpritesProperty,
                        "_particleSprites",
                        context,
                        errors);
                    hasMissingRequiredReference |= particleSpriteReferences.Any(reference => reference.IsMissing);
                }
            }

            MonoScript script = MonoScript.FromScriptableObject(asset);
            string scriptAssetPath = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
            string scriptGuid = string.IsNullOrEmpty(scriptAssetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(scriptAssetPath);
            if (script == null || string.IsNullOrEmpty(scriptGuid))
            {
                errors.Add(context + "의 Script 정보를 읽지 못했습니다.");
            }

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return new StaffDataAssetSnapshot(
                assetPath,
                AssetDatabase.AssetPathToGUID(assetPath),
                fileName,
                scriptAssetPath,
                scriptGuid,
                asset.name,
                id,
                nameProperty.stringValue,
                descriptionProperty.stringValue,
                concreteTypeName,
                role.RoleKey,
                GetEnumName(rankProperty),
                rankProperty.intValue,
                speedProperty.floatValue,
                GetEnumName(salesLocationProperty),
                salesLocationProperty.intValue,
                GetEnumName(moneyTypeProperty),
                moneyTypeProperty.intValue,
                buyScoreProperty.intValue,
                buyPriceProperty.intValue,
                role.LevelArrayPropertyPath,
                levels,
                skillReference,
                skillConcreteTypeName,
                skillDuration,
                skillCooldown,
                spriteReference,
                thumbnailReference,
                animatorReference,
                idleReferences,
                backSpriteReference,
                handSpriteReference,
                handOffsetX,
                handOffsetY,
                uiSpriteReference,
                animationSpriteReference,
                particleCount,
                particleSpriteReferences,
                hasExpectedLevelArray,
                hasChefAddSpeedField,
                hasMissingRequiredReference,
                string.Equals(fileName, id, StringComparison.Ordinal));
        }

        private static List<StaffLevelAssetSnapshot> ReadLevels(
            SerializedProperty levelArrayProperty,
            RoleDefinition role,
            string context,
            List<string> errors)
        {
            List<StaffLevelAssetSnapshot> result = new List<StaffLevelAssetSnapshot>();
            if (levelArrayProperty == null || !levelArrayProperty.isArray)
            {
                return result;
            }

            for (int index = 0; index < levelArrayProperty.arraySize; index++)
            {
                SerializedProperty element = levelArrayProperty.GetArrayElementAtIndex(index);
                string levelContext = context + " Level " + (index + 1);
                SerializedProperty minScore = FindRelativeRequired(
                    element,
                    "_upgradeMinScore",
                    levelContext,
                    errors);
                SerializedProperty upgradeMoney = FindRelativeRequired(
                    element,
                    "_upgradeMoneyData",
                    levelContext,
                    errors);
                SerializedProperty moneyType = upgradeMoney == null
                    ? null
                    : FindRelativeRequired(upgradeMoney, "_moneyType", levelContext, errors);
                SerializedProperty price = upgradeMoney == null
                    ? null
                    : FindRelativeRequired(upgradeMoney, "_price", levelContext, errors);
                if (minScore == null || moneyType == null || price == null)
                {
                    continue;
                }

                result.Add(
                    new StaffLevelAssetSnapshot(
                        index + 1,
                        minScore.intValue,
                        GetEnumName(moneyType),
                        moneyType.intValue,
                        price.intValue,
                        GetOptionalFloat(element, role.AddSpeedProperty),
                        GetOptionalFloat(element, role.CleaningTimeProperty),
                        GetOptionalFloat(element, role.FoodSpeedAddPercentProperty),
                        GetOptionalFloat(element, role.CustomerGuideTimeProperty),
                        GetOptionalFloat(element, role.MarketingTimeProperty),
                        GetOptionalFloat(element, role.ActionTimeProperty)));
            }

            return result;
        }

        private static List<StaffSkillAssetSnapshot> ReadSkillAssets(
            Dictionary<string, HashSet<string>> referencedStaffIdsBySkillGuid,
            List<string> errors)
        {
            List<StaffSkillAssetSnapshot> result = new List<StaffSkillAssetSnapshot>();
            string[] paths = AssetDatabase.FindAssets(string.Empty, new[] { SkillFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => IsAssetFileInFolder(path, SkillFolder))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            HashSet<string> discoveredSkillGuids = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                string context = "Skill " + path;
                string guid = AssetDatabase.AssetPathToGUID(path);
                discoveredSkillGuids.Add(guid);
                UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                Type mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                SkillBase skillAsset = loadedAsset as SkillBase;
                ScriptableObject scriptableObject = loadedAsset as ScriptableObject;
                MonoScript script = scriptableObject == null
                    ? null
                    : MonoScript.FromScriptableObject(scriptableObject);
                bool hasMissingScript = loadedAsset == null || mainAssetType == null || script == null;
                if (hasMissingScript)
                {
                    errors.Add(context + "에 Missing Script가 있습니다.");
                }

                if (skillAsset == null)
                {
                    errors.Add(context + "가 정상 SkillBase 에셋이 아닙니다.");
                    continue;
                }

                SerializedObject serializedObject = new SerializedObject(skillAsset);
                serializedObject.Update();
                SerializedProperty descriptionProperty = FindRequired(
                    serializedObject,
                    "_description",
                    context,
                    errors);
                SerializedProperty durationProperty = FindRequired(
                    serializedObject,
                    "_duration",
                    context,
                    errors);
                SerializedProperty cooldownProperty = FindRequired(
                    serializedObject,
                    "_cooldown",
                    context,
                    errors);
                if (descriptionProperty == null || durationProperty == null || cooldownProperty == null)
                {
                    continue;
                }

                bool hasMissingSerializedReference = HasMissingSerializedReference(serializedObject);
                if (hasMissingSerializedReference)
                {
                    errors.Add(context + "에 Missing Serialized Reference가 있습니다.");
                }

                string scriptAssetPath = script == null ? string.Empty : AssetDatabase.GetAssetPath(script);
                string scriptGuid = string.IsNullOrEmpty(scriptAssetPath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(scriptAssetPath);
                HashSet<string> referencedIds;
                IEnumerable<string> staffIds;
                if (referencedStaffIdsBySkillGuid.TryGetValue(guid, out referencedIds))
                {
                    staffIds = referencedIds;
                }
                else
                {
                    staffIds = Array.Empty<string>();
                }

                result.Add(
                    new StaffSkillAssetSnapshot(
                        path,
                        guid,
                        Path.GetFileNameWithoutExtension(path),
                        skillAsset.name,
                        scriptAssetPath,
                        scriptGuid,
                        skillAsset.GetType().Name,
                        descriptionProperty.stringValue,
                        durationProperty.floatValue,
                        cooldownProperty.floatValue,
                        staffIds,
                        hasMissingScript,
                        hasMissingSerializedReference));
            }

            foreach (string referencedGuid in referencedStaffIdsBySkillGuid.Keys)
            {
                if (!discoveredSkillGuids.Contains(referencedGuid))
                {
                    errors.Add("Staff가 참조하지만 지정 Skill 폴더에서 찾지 못한 GUID입니다: " + referencedGuid);
                }
            }

            return result;
        }

        private static void ReadSkillTiming(
            SkillBase skillAsset,
            string context,
            List<string> errors,
            out float duration,
            out float cooldown)
        {
            duration = 0f;
            cooldown = 0f;
            SerializedObject serializedObject = new SerializedObject(skillAsset);
            serializedObject.Update();
            SerializedProperty durationProperty = FindRequired(
                serializedObject,
                "_duration",
                context + " Skill",
                errors);
            SerializedProperty cooldownProperty = FindRequired(
                serializedObject,
                "_cooldown",
                context + " Skill",
                errors);
            if (durationProperty != null)
            {
                duration = durationProperty.floatValue;
            }

            if (cooldownProperty != null)
            {
                cooldown = cooldownProperty.floatValue;
            }
        }

        private static StaffAssetReferenceSnapshot ReadReference(
            SerializedProperty property,
            string fieldPath,
            string context,
            List<string> errors)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                errors.Add(context + "의 Property가 Object Reference가 아닙니다: " + fieldPath);
                return new StaffAssetReferenceSnapshot(
                    fieldPath,
                    false,
                    false,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

            UnityEngine.Object referencedObject = property.objectReferenceValue;
            bool isAssigned = referencedObject != null || property.objectReferenceInstanceIDValue != 0;
            bool isMissing = referencedObject == null && property.objectReferenceInstanceIDValue != 0;
            string assetPath = referencedObject == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(referencedObject);
            string assetGuid = string.IsNullOrEmpty(assetPath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(assetPath);
            return new StaffAssetReferenceSnapshot(
                fieldPath,
                isAssigned,
                isMissing,
                assetPath,
                assetGuid,
                referencedObject == null ? string.Empty : referencedObject.name,
                referencedObject == null ? string.Empty : referencedObject.GetType().Name);
        }

        private static List<StaffAssetReferenceSnapshot> ReadReferenceArray(
            SerializedProperty arrayProperty,
            string fieldPath,
            string context,
            List<string> errors)
        {
            List<StaffAssetReferenceSnapshot> result = new List<StaffAssetReferenceSnapshot>();
            if (!arrayProperty.isArray)
            {
                errors.Add(context + "의 Property가 배열이 아닙니다: " + fieldPath);
                return result;
            }

            for (int index = 0; index < arrayProperty.arraySize; index++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(index);
                result.Add(
                    ReadReference(
                        element,
                        fieldPath + ".Array.data[" + index + "]",
                        context,
                        errors));
            }

            return result;
        }

        private static bool HasMissingSerializedReference(SerializedObject serializedObject)
        {
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

        private static void ValidateIdentityUniqueness(
            IReadOnlyList<StaffDataAssetSnapshot> staff,
            IReadOnlyList<StaffSkillAssetSnapshot> skills,
            List<string> errors)
        {
            HashSet<string> staffIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> staffGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < staff.Count; index++)
            {
                if (string.IsNullOrEmpty(staff[index].Id))
                {
                    errors.Add("빈 Staff ID가 있습니다: " + staff[index].AssetPath);
                }
                else if (!staffIds.Add(staff[index].Id))
                {
                    errors.Add("Staff ID가 중복되었습니다: " + staff[index].Id);
                }

                if (string.IsNullOrEmpty(staff[index].AssetGuid))
                {
                    errors.Add("StaffData GUID가 비어 있습니다: " + staff[index].AssetPath);
                }
                else if (!staffGuids.Add(staff[index].AssetGuid))
                {
                    errors.Add("StaffData GUID가 중복되었습니다: " + staff[index].AssetGuid);
                }
            }

            HashSet<string> skillGuids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < skills.Count; index++)
            {
                if (string.IsNullOrEmpty(skills[index].AssetGuid))
                {
                    errors.Add("Skill GUID가 비어 있습니다: " + skills[index].AssetPath);
                }
                else if (!skillGuids.Add(skills[index].AssetGuid))
                {
                    errors.Add("Skill GUID가 중복되었습니다: " + skills[index].AssetGuid);
                }
            }
        }

        private static SerializedProperty FindRequired(
            SerializedObject serializedObject,
            string propertyPath,
            string context,
            List<string> errors)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                errors.Add(context + "에 필수 Property가 없습니다: " + propertyPath);
            }

            return property;
        }

        private static SerializedProperty FindRelativeRequired(
            SerializedProperty parent,
            string relativePath,
            string context,
            List<string> errors)
        {
            SerializedProperty property = parent.FindPropertyRelative(relativePath);
            if (property == null)
            {
                errors.Add(context + "에 필수 Property가 없습니다: " + relativePath);
            }

            return property;
        }

        private static float? GetOptionalFloat(SerializedProperty parent, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            SerializedProperty property = parent.FindPropertyRelative(relativePath);
            return property == null ? (float?)null : property.floatValue;
        }

        private static string GetEnumName(SerializedProperty property)
        {
            int enumIndex = property.enumValueIndex;
            string[] enumNames = property.enumNames;
            return enumIndex >= 0 && enumIndex < enumNames.Length
                ? enumNames[enumIndex]
                : property.intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool TryGetRoleDefinition(string concreteTypeName, out RoleDefinition role)
        {
            switch (concreteTypeName)
            {
                case "WaiterData":
                    role = new RoleDefinition("WAITER", "_waiterLevelData", "_addSpeed");
                    return true;
                case "CleanerData":
                    role = new RoleDefinition(
                        "CLEANER",
                        "_cleanerLevelData",
                        "_addSpeed",
                        "_cleaningTime");
                    return true;
                case "ChefData":
                    role = new RoleDefinition(
                        "CHEF",
                        "_chefLevelData",
                        "_addSpeed",
                        null,
                        "_foodSpeedAddPercent");
                    return true;
                case "ManagerData":
                    role = new RoleDefinition(
                        "MANAGER",
                        "_managerLevelData",
                        null,
                        null,
                        null,
                        "_customerGuideTime");
                    return true;
                case "MarketerData":
                    role = new RoleDefinition(
                        "CHEERLEADER",
                        "_marketerLevelData",
                        null,
                        null,
                        null,
                        null,
                        "_marketingTime");
                    return true;
                case "GuardData":
                    role = new RoleDefinition(
                        "GUARD",
                        "_guardLevelData",
                        null,
                        null,
                        null,
                        null,
                        null,
                        "_actionTime");
                    return true;
                default:
                    role = null;
                    return false;
            }
        }

        private static bool IsAssetFileInFolder(string assetPath, string folder)
        {
            return IsPathInFolder(assetPath, folder)
                   && string.Equals(Path.GetExtension(assetPath), ".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPathInFolder(string assetPath, string folder)
        {
            return !string.IsNullOrEmpty(assetPath)
                   && (string.Equals(assetPath, folder, StringComparison.Ordinal)
                       || assetPath.StartsWith(folder + "/", StringComparison.Ordinal));
        }

        private static bool IsRequiredReferenceInvalid(StaffAssetReferenceSnapshot reference)
        {
            return reference == null || !reference.IsAssigned || reference.IsMissing;
        }

        private static bool HasNullProperty(params SerializedProperty[] properties)
        {
            for (int index = 0; index < properties.Length; index++)
            {
                if (properties[index] == null)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RoleDefinition
        {
            internal readonly string RoleKey;
            internal readonly string LevelArrayPropertyPath;
            internal readonly string AddSpeedProperty;
            internal readonly string CleaningTimeProperty;
            internal readonly string FoodSpeedAddPercentProperty;
            internal readonly string CustomerGuideTimeProperty;
            internal readonly string MarketingTimeProperty;
            internal readonly string ActionTimeProperty;

            internal RoleDefinition(
                string roleKey,
                string levelArrayPropertyPath,
                string addSpeedProperty = null,
                string cleaningTimeProperty = null,
                string foodSpeedAddPercentProperty = null,
                string customerGuideTimeProperty = null,
                string marketingTimeProperty = null,
                string actionTimeProperty = null)
            {
                RoleKey = roleKey;
                LevelArrayPropertyPath = levelArrayPropertyPath;
                AddSpeedProperty = addSpeedProperty;
                CleaningTimeProperty = cleaningTimeProperty;
                FoodSpeedAddPercentProperty = foodSpeedAddPercentProperty;
                CustomerGuideTimeProperty = customerGuideTimeProperty;
                MarketingTimeProperty = marketingTimeProperty;
                ActionTimeProperty = actionTimeProperty;
            }
        }
    }
}
