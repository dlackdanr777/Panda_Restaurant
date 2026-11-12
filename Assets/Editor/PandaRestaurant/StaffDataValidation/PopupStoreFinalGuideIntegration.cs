#if UNITY_EDITOR
/// <summary>Retained v3 provenance paths; explicit authoring now uses the approved v4 replacement.</summary>
public static class PopupStoreFinalGuideIntegration
{
    public const string Folder = "Assets/Sprites/PopupStore/FinalGuideV3/";
    public const string BoundsPath = Folder + "OriginalSpriteVisualBounds.asset";
    public static void Apply() => PopupStoreCleanPartsIntegration.Apply();
}
#endif
