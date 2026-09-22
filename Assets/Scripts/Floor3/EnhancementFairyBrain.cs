using System;
using UnityEngine;

public enum EnhancementFairyAction { Rest, Look, Walk, Hop, Visit, Turn }

/// <summary>A separate deterministic PRNG per item; never touches UnityEngine.Random or gacha RNG.</summary>
public sealed class EnhancementFairyBrain
{
    private readonly System.Random _random;
    private readonly EnhancementFairySettings _settings;
    private readonly Rect _bounds;
    private readonly float _energy;
    private Vector2 _target;
    private float _remaining;
    private float _actionElapsed;
    private float _phase;
    private int _hopCount;

    public string ItemId { get; }
    public Vector2 GroundPosition { get; private set; }
    public float VisualHeight { get; private set; }
    public float Tilt { get; private set; }
    public float Squash { get; private set; }
    public bool FacingLeft { get; private set; }
    public EnhancementFairyAction Action { get; private set; }
    public float ElapsedSeconds { get; private set; }

    public EnhancementFairyBrain(string itemId, EnhancementFairySettings settings)
    {
        ItemId = itemId;
        _settings = settings;
        _bounds = settings.SafeGroundArea;
        int seed = StableSeed(itemId);
        _random = new System.Random(seed);
        _energy = Mathf.Max(0.1f, settings.CalmEnergy + (seed % 3) * settings.PersonalityEnergyStep);
        GroundPosition = new Vector2(Range(_bounds.xMin, _bounds.xMax), Range(_bounds.yMin, _bounds.yMax));
        _target = GroundPosition;
        _phase = Range(0f, Mathf.PI * 2f);
        _remaining = Duration(settings.InitialWaitSeconds);
        FacingLeft = _random.Next(2) == 0;
    }

    public void Step(float deltaTime, Vector2 neighbour, bool hasNeighbour)
    {
        if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        float dt = Mathf.Min(deltaTime, 0.1f);
        ElapsedSeconds += dt;
        _remaining -= dt;
        _actionElapsed += dt;
        if (_remaining <= 0f) ChooseAction(neighbour, hasNeighbour);

        if (Action == EnhancementFairyAction.Walk || Action == EnhancementFairyAction.Visit)
        {
            var next = Vector2.MoveTowards(GroundPosition, _target, Mathf.Max(0.1f, _settings.WalkSpeed) * _energy * dt);
            if (Mathf.Abs(next.x - GroundPosition.x) > 0.002f) FacingLeft = next.x < GroundPosition.x;
            GroundPosition = Clamp(next);
            float arrive = Mathf.Max(0.01f, _settings.ArrivalDistance);
            if (Vector2.SqrMagnitude(GroundPosition - _target) < arrive * arrive) Rest();
        }

        float wave = Mathf.Sin(ElapsedSeconds * (_settings.IdleBobRate + _energy) + _phase);
        VisualHeight = _settings.IdleLift + wave * _settings.IdleBobAmplitude;
        Tilt = wave * _settings.IdleTiltDegrees;
        Squash = 0f;
        if (Action == EnhancementFairyAction.Hop)
        {
            float t = Mathf.Clamp01(_actionElapsed / Mathf.Max(0.1f, _settings.HopSeconds));
            VisualHeight += Mathf.Abs(Mathf.Sin(t * Mathf.PI * _hopCount)) * Mathf.Max(0f, _settings.HopHeight);
            Squash = Mathf.Sin(t * Mathf.PI * _hopCount * 2f) * Mathf.Clamp(_settings.HopSquash, 0f, 0.5f);
        }
        else if (Action == EnhancementFairyAction.Walk || Action == EnhancementFairyAction.Visit)
        {
            Tilt = Mathf.Sin(ElapsedSeconds * _settings.WalkTiltRate + _phase) * _settings.WalkTiltDegrees;
            VisualHeight += Mathf.Abs(Mathf.Sin(ElapsedSeconds * _settings.WalkBobRate + _phase)) * _settings.WalkBobAmplitude;
        }
        else if (Action == EnhancementFairyAction.Look)
            Tilt = Mathf.Sin(_actionElapsed * _settings.LookRate) * _settings.LookTiltDegrees;
    }

    private void ChooseAction(Vector2 neighbour, bool hasNeighbour)
    {
        _actionElapsed = 0f;
        float hop = Mathf.Max(0f, _settings.HopProbability * _energy);
        float visit = hop + Mathf.Max(0f, _settings.VisitProbability);
        float walk = visit + Mathf.Max(0f, _settings.WalkProbability);
        float look = walk + Mathf.Max(0f, _settings.LookProbability);
        float turn = look + Mathf.Max(0f, _settings.TurnProbability);
        float total = turn + Mathf.Max(0f, _settings.RestProbability);
        if (total <= 0f) { Rest(); return; }
        float roll = Range(0f, total);
        if (roll < hop)
        {
            Action = EnhancementFairyAction.Hop;
            _remaining = Mathf.Max(0.1f, _settings.HopSeconds);
            _hopCount = _random.Next(1, Mathf.Clamp(_settings.MaximumHops, 1, 2) + 1);
        }
        else if (roll < visit && hasNeighbour)
        {
            Action = EnhancementFairyAction.Visit;
            _target = Clamp(neighbour + Offset(_settings.VisitRadius));
            _remaining = Duration(_settings.VisitSeconds);
        }
        else if (roll < walk)
        {
            Action = EnhancementFairyAction.Walk;
            _target = Clamp(GroundPosition + Offset(_settings.WalkRadius));
            _remaining = Duration(_settings.WalkSeconds);
        }
        else if (roll < look)
        {
            Action = EnhancementFairyAction.Look;
            _remaining = Duration(_settings.LookSeconds);
        }
        else if (roll < turn)
        {
            Action = EnhancementFairyAction.Turn;
            FacingLeft = !FacingLeft;
            _remaining = Duration(_settings.TurnSeconds);
        }
        else Rest();
    }

    private void Rest()
    {
        Action = EnhancementFairyAction.Rest;
        _remaining = Range(Mathf.Max(0.1f, _settings.RestMinSeconds), Mathf.Max(_settings.RestMinSeconds, _settings.RestMaxSeconds)) / _energy;
        _actionElapsed = 0f;
    }

    private Vector2 Clamp(Vector2 p) => new Vector2(Mathf.Clamp(p.x, _bounds.xMin, _bounds.xMax), Mathf.Clamp(p.y, _bounds.yMin, _bounds.yMax));
    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    private float Duration(Vector2 range) => Range(Mathf.Max(0.1f, Mathf.Min(range.x, range.y)), Mathf.Max(0.1f, Mathf.Max(range.x, range.y)));
    private Vector2 Offset(Vector2 radius) => new Vector2(Range(-Mathf.Abs(radius.x), Mathf.Abs(radius.x)), Range(-Mathf.Abs(radius.y), Mathf.Abs(radius.y)));

    public static int StableSeed(string id)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in id ?? string.Empty) { hash ^= c; hash *= 16777619; }
            return (int)(hash & 0x7fffffff);
        }
    }
}
