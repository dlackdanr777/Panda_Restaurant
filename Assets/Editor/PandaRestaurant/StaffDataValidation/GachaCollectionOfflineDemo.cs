#if UNITY_EDITOR
using UnityEditor;

/// <summary>The demo owns disposable preview scenes and never replaces the user's scene setup.</summary>
public static class GachaCollectionOfflineDemo
{
    [MenuItem("Tools/Panda Restaurant/Gacha Collection/Open Integrated Offline Demo")]
    public static void Open() => GachaCollectionPreviewWindow.Open();
}
#endif
