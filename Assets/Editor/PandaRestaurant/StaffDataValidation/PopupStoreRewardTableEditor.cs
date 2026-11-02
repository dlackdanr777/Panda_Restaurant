#if UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only diagnostics apart from deliberate edits through Unity's ordinary serialized Inspector.</summary>
[CustomEditor(typeof(PopupStoreRewardTable))]
public sealed class PopupStoreRewardTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("R0 테스트 보상표입니다. 확률 구간은 합산 지급하지 않습니다. 실제 추첨·지급 기능은 없습니다.", MessageType.Info);
        DrawDefaultInspector();
        string report = BuildReport(target as PopupStoreRewardTable, out bool valid);
        EditorGUILayout.HelpBox(report, valid ? MessageType.Info : MessageType.Error);
        if (GUILayout.Button("유효성·기대값 다시 계산 후 Console 기록")) Debug.Log(report, target);
    }

    // Rebuilt from current serialized data every draw/call. No stale cached validation or asset writes.
    public static string BuildReport(PopupStoreRewardTable table, out bool valid)
    {
        valid = false;
        if (table == null) return "R0 보상표 없음";
        if (!table.TryGetEntries(out var entries, out string error)) return "R0 보상표 INVALID: " + error;
        var report = new StringBuilder("R0 보상표 VALID — 지급 없이 이론적 기대값만 계산\n");
        foreach (var entry in entries)
        {
            if (!PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, out decimal expected, out error))
                return "R0 보상표 INVALID: " + error;
            report.AppendFormat(CultureInfo.InvariantCulture,
                "{0}: 기본 {1}D({4}%) / 추가 {2}D({5}%) / 대박 {3}D({6}%) | 합계 {7}% | 기대값 {8:0.00}D\n",
                entry.Grade, entry.BasicDiamonds, entry.BonusDiamonds, entry.JackpotDiamonds,
                entry.BasicWeight, entry.BonusWeight, entry.JackpotWeight,
                (long)entry.BasicWeight + entry.BonusWeight + entry.JackpotWeight, expected);
        }
        valid = true;
        return report.ToString().TrimEnd();
    }
}
#endif
