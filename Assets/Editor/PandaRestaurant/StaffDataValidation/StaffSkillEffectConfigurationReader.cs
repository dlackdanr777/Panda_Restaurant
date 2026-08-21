using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace PandaRestaurant.Editor.StaffDataValidation
{
    internal sealed class StaffSkillEffectConfigurationSnapshot
    {
        internal string AssetPath { get; }
        internal string ConcreteTypeName { get; }
        internal string FieldPath { get; }
        internal bool FieldExists { get; }
        internal string NormalizedValue { get; }

        internal StaffSkillEffectConfigurationSnapshot(
            string assetPath,
            string concreteTypeName,
            string fieldPath,
            bool fieldExists,
            string normalizedValue)
        {
            AssetPath = assetPath ?? string.Empty;
            ConcreteTypeName = concreteTypeName ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            FieldExists = fieldExists;
            NormalizedValue = normalizedValue ?? string.Empty;
        }
    }

    internal static class StaffSkillEffectConfigurationReader
    {
        internal static bool TryReadFloat(
            string assetPath,
            string fieldPath,
            out StaffSkillEffectConfigurationSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                error = "Skill effect asset path is blank.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(fieldPath))
            {
                error = "Skill effect field path is blank: " + assetPath;
                return false;
            }

            Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                error = "Skill effect asset cannot be loaded: " + assetPath;
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.FindProperty(fieldPath);
            if (property == null)
            {
                error = "Skill effect field is missing: " + assetPath + " | " + fieldPath;
                return false;
            }

            if (property.propertyType != SerializedPropertyType.Float)
            {
                error = "Skill effect field is not a float: " + assetPath + " | " + fieldPath
                        + " | actual=" + property.propertyType;
                return false;
            }

            snapshot = new StaffSkillEffectConfigurationSnapshot(
                assetPath,
                asset.GetType().Name,
                fieldPath,
                true,
                property.floatValue.ToString("0.########", CultureInfo.InvariantCulture));
            return true;
        }
    }
}
