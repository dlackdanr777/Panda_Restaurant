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
        EditorGUILayout.HelpBox("로컬 검수 보상표입니다. 세 확률 구간 중 하나만 선택하며 실제 지급 권한은 없습니다.", MessageType.Info);
        DrawDefaultInspector();
        string report = BuildReport(target as PopupStoreRewardTable, out bool valid);
        EditorGUILayout.HelpBox(report, valid ? MessageType.Info : MessageType.Error);
        if (GUILayout.Button("유효성·기대값 다시 계산 후 Console 기록")) Debug.Log(report, target);
    }

    // Rebuilt from current serialized data every draw/call. No stale cached validation or asset writes.
    public static string BuildReport(PopupStoreRewardTable table, out bool valid)
    {
        valid = false;
        if (table == null) return "보상표 없음";
        string label = table.Schema == PopupStoreRewardSchema.R1Permille ? "R1"
            : table.Schema == PopupStoreRewardSchema.LegacyR0Percent ? "R0" : "알 수 없는";
        if (!table.TryGetEntries(out var entries, out string error)) return label + " 보상표 INVALID: " + error;
        var report = new StringBuilder(label + " 보상표 VALID — 지급 없이 이론적 기대값만 계산\n");
        report.Append("정책 ").Append(table.PolicyId).Append(" / 정수 단위 ").Append(table.Denominator).Append('\n');
        foreach (var entry in entries)
        {
            if (!PopupStoreRewardCalculator.TryCalculateExpectedDiamonds(entry, table.Denominator, out decimal expected, out error))
                return label + " 보상표 INVALID: " + error;
            report.AppendFormat(CultureInfo.InvariantCulture,
                "{0}: 기본 {1}D({4:0.#}%) / 추가 {2}D({5:0.#}%) / 대박 {3}D({6:0.#}%) | 합계 {7:0.#}% | 기대값 {8}D\n",
                entry.Grade, entry.BasicDiamonds, entry.BonusDiamonds, entry.JackpotDiamonds,
                100m * entry.BasicWeight / table.Denominator, 100m * entry.BonusWeight / table.Denominator,
                100m * entry.JackpotWeight / table.Denominator,
                100m * ((long)entry.BasicWeight + entry.BonusWeight + entry.JackpotWeight) / table.Denominator,
                expected.ToString(table.Schema == PopupStoreRewardSchema.R1Permille ? "0.000" : "0.00", CultureInfo.InvariantCulture));
        }
        valid = true;
        return report.ToString().TrimEnd();
    }
}
#endif
