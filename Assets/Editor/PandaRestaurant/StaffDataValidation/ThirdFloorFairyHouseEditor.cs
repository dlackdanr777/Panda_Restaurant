#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(ThirdFloorFairyHouse))]
public sealed class ThirdFloorFairyHouseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var house = (ThirdFloorFairyHouse)target;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Base Active Settings", house.BaseSettings, typeof(EnhancementFairySettings), false);
            EditorGUILayout.IntField("Base Active Limit", house.BaseActiveLimit);
        }
        EditorGUILayout.HelpBox("Base Active Limit references the existing MaxActive setting. Extra capacity is temporary and never changes that asset.", MessageType.Info);
    }
}
#endif
