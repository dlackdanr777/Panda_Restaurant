using UnityEngine;

/// <summary>Presentation only. These settings never change item effects or floor unlocks.</summary>
[CreateAssetMenu(menuName = "Panda Restaurant/Enhancement Fairy Settings")]
public sealed class EnhancementFairySettings : ScriptableObject
{
    public const string ResourcePath = "Fairies/EnhancementFairySettings";

    [Min(1)] public int MaxActive = 12;
    [Min(1f)] public float RotationSeconds = 12f;
    [Min(1f)] public float NewArrivalPrioritySeconds = 8f;
    [Tooltip("Local ground coordinates on the existing Stage1 Floor3 root.")]
    public Rect GroundArea = new Rect(-8f, 8f, 16f, 1.8f);
    [Min(0.1f)] public float SpriteHeight = 1.65f;
    [Min(0.1f)] public float WalkSpeed = 1.05f;
    [Min(0f)] public float HopHeight = 0.55f;
    [Min(0.1f)] public float RestMinSeconds = 1.8f;
    [Min(0.1f)] public float RestMaxSeconds = 5.6f;
    [Range(0f, 1f)] public float HopProbability = 0.14f;
    [Range(0f, 1f)] public float VisitProbability = 0.16f;
    [Range(0f, 1f)] public float WalkProbability = 0.30f;
    [Range(0f, 1f)] public float LookProbability = 0.23f;
    [Range(0f, 1f)] public float TurnProbability = 0.07f;
    [Range(0f, 1f)] public float RestProbability = 0.10f;
    [Header("Personality and action durations (seconds)")]
    [Min(0.1f)] public float CalmEnergy = 0.7f;
    [Min(0f)] public float PersonalityEnergyStep = 0.32f;
    public Vector2 InitialWaitSeconds = new Vector2(0.5f, 5.6f);
    public Vector2 WalkSeconds = new Vector2(1.2f, 3.8f);
    public Vector2 VisitSeconds = new Vector2(1.5f, 3f);
    public Vector2 LookSeconds = new Vector2(0.8f, 1.7f);
    public Vector2 TurnSeconds = new Vector2(0.7f, 1.2f);
    [Min(0.1f)] public float HopSeconds = 0.65f;
    [Range(1, 2)] public int MaximumHops = 2;
    [Header("Movement distances (world units)")]
    public Vector2 WalkRadius = new Vector2(3f, 1f);
    public Vector2 VisitRadius = new Vector2(1f, 0.4f);
    [Min(0.01f)] public float ArrivalDistance = 0.1f;
    [Header("Sprite motion")]
    public float IdleLift = 0.06f;
    [Min(0f)] public float IdleBobAmplitude = 0.035f;
    [Min(0f)] public float IdleBobRate = 2.3f;
    [Min(0f)] public float IdleTiltDegrees = 2f;
    [Min(0f)] public float WalkBobAmplitude = 0.09f;
    [Min(0f)] public float WalkBobRate = 7f;
    [Min(0f)] public float WalkTiltDegrees = 5f;
    [Min(0f)] public float WalkTiltRate = 10f;
    [Min(0f)] public float LookTiltDegrees = 9f;
    [Min(0f)] public float LookRate = 4f;
    [Range(0f, 0.5f)] public float HopSquash = 0.09f;
    [Header("Arrival and rotation visuals")]
    [Min(0.1f)] public float ArrivalSeconds = 0.65f;
    [Min(0.1f)] public float RotationFadeSeconds = 0.4f;
    [Min(0.1f)] public float ArrivalPuffSize = 1.9f;
    [Min(0.01f)] public float ArrivalStarSize = 0.18f;
    public Vector2 ArrivalStarRadius = new Vector2(0.8f, 0.55f);
    [Range(0f, 1f)] public float ArrivalPuffOpacity = 0.75f;
    [Range(0.1f, 1f)] public float ArrivalScaleInFraction = 0.3333333f;
    [Range(0f, 0.3f)] public float ArrivalScaleOvershoot = 0.12f;
    public string SortingLayer = "Staff";
    [Range(0, 31)] public int VisualLayer = 2;
    public Sprite ArrivalSprite;
    public Sprite SparkleSprite;

    public Rect SafeGroundArea => new Rect(GroundArea.x, GroundArea.y,
        Mathf.Max(0.1f, GroundArea.width), Mathf.Max(0.1f, GroundArea.height));
}
