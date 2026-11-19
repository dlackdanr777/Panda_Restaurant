using System;
using System.Collections.Generic;
using UnityEngine;

// Presentation-only room spacing within the existing habitat, views and brain.
public sealed partial class EnhancementFairyHabitat
{
    private sealed partial class View
    {
        public bool Dispersing;
        public bool InitialHouseDispersal;
        public Vector2 DispersalTarget;
        public float DispersalDeadline, HouseResidentSeconds, SpacingHalfWidth;
        public float HouseOverlapSeconds;
        public float ExitLaneBlockedSeconds;
        public int HouseExitSide;
        public bool HasPlannedExit;
        public bool SpacingYielding;
        public Vector2 PlannedExitTarget;
    }

    private Func<EnhancementFairyBrain, Vector2, Vector2> _spacingTargetResolver;
    private readonly Dictionary<string, float> _houseRequestDeadlines = new Dictionary<string, float>(StringComparer.Ordinal);
    public string LastHouseExitDecision { get; private set; } = "no pending emergence";
    public float HouseExitBlockedSeconds => _houseRequests.Count > 0
        && _houseRequestDeadlines.TryGetValue(_houseRequests.Peek(), out float deadline) && _houseSettings != null
        ? Mathf.Max(0f, _houseSettings.SafeExitClearanceTimeoutSeconds - (deadline - _clock)) : 0f;

    public float GetHouseZoneSeconds(string id) { foreach (var view in _views) if (view.Brain?.ItemId == id) return view.HouseResidentSeconds; return 0f; }
    public float GetHouseOverlapSeconds(string id) { foreach (var view in _views) if (view.Brain?.ItemId == id) return view.HouseOverlapSeconds; return 0f; }
    public int GetHouseExitSide(string id) { foreach (var view in _views) if (view.Brain?.ItemId == id) return view.HouseExitSide; return 0; }
    public float GetSpacingHalfWidth(string id) { foreach (var view in _views) if (view.Brain?.ItemId == id) return view.SpacingHalfWidth; return 0f; }

    public int DispersingCount
    {
        get { int count = 0; foreach (var view in _views) if (view.Dispersing) count++; return count; }
    }
    public int HouseResidentCount
    {
        get
        {
            if (!HasActiveHouse || !IsVisible) return 0;
            Vector2 center = HouseSpacingCenter;
            int count = 0;
            foreach (var view in _views) if (IsHouseResident(view, center)) count++;
            return count;
        }
    }
    public int HouseCrowdCount
    {
        get
        {
            if (!HasActiveHouse || !IsVisible) return 0;
            Vector2 center = HouseSpacingCenter;
            int count = 0;
            foreach (var view in _views)
                if (view.Brain != null && view.Root != null && view.Root.gameObject.activeInHierarchy
                    && BodyTouchesHouse(view, center)) count++;
            return count;
        }
    }
    public bool IsItemDispersing(string itemId)
    {
        foreach (var view in _views) if (view.Brain != null && view.Brain.ItemId == itemId) return view.Dispersing;
        return false;
    }
    public Vector2 GetDispersalTarget(string itemId)
    {
        foreach (var view in _views)
            if (view.Brain != null && view.Brain.ItemId == itemId && view.Dispersing) return view.DispersalTarget;
        return Vector2.zero;
    }

    private Vector2 HouseSpacingCenter => transform.InverseTransformPoint(_houseEntrance.position);
    private static bool IsDispersing(View view) => view.Dispersing;

    private void InitializeSpacing(View view)
    {
        view.Dispersing = false;
        view.InitialHouseDispersal = false;
        view.DispersalTarget = Vector2.zero;
        view.DispersalDeadline = view.HouseResidentSeconds = 0f;
        view.HouseOverlapSeconds = 0f;
        view.ExitLaneBlockedSeconds = 0f;
        view.HouseExitSide = 0;
        view.HasPlannedExit = false;
        view.PlannedExitTarget = Vector2.zero;
        view.SpacingHalfWidth = EstimateHalfWidth(view.Body.sprite);
        if (_spacingTargetResolver == null) _spacingTargetResolver = ResolveSpacedWalkingTarget;
        view.Brain.TargetResolver = _spacingTargetResolver;
    }

    private void BeginHouseDispersal(View view)
    {
        if (!HasActiveHouse || !SpacingEligible(view)) return;
        if (view.HasPlannedExit)
        {
            Vector2 planned = view.PlannedExitTarget;
            view.HasPlannedExit = false;
            BeginSpacingWalk(view, planned, _houseSettings.SafeTapDispersalSpeedMultiplier);
            view.InitialHouseDispersal = view.Dispersing;
            return;
        }
        int side = SideOf(view.Brain.GroundPosition.x, HouseSpacingCenter.x, view.DisplaySequence);
        if (!TryChooseOutwardTarget(view, view.SpacingHalfWidth, view.Brain.GroundPosition, side, false, out Vector2 target)) return;
        BeginSpacingWalk(view, target);
    }

    private void BeginSpacingWalk(View view, Vector2 target, float speedMultiplier = 1f)
    {
        view.InitialHouseDispersal = false;
        view.DispersalTarget = target;
        view.DispersalDeadline = _clock + Mathf.Min(180f,
            Vector2.Distance(view.Brain.GroundPosition, target) / Mathf.Max(.001f, FurnitureWalkSpeed) + 30f);
        view.Brain.BeginDirectedWalk(target, speedMultiplier);
        view.Dispersing = view.Brain.IsDirectedWalking;
    }

    private float EstimateHalfWidth(Sprite sprite)
    {
        if (sprite == null) return _settings.SpriteHeight * .5f;
        var size = sprite.bounds.size; // No Sprite.vertices array allocation while an exit waits.
        float height = Mathf.Max(.01f, _settings.SpriteHeight);
        float width = Mathf.Abs(size.x) / Mathf.Max(.01f, Mathf.Abs(size.y)) * height;
        float tilt = Mathf.Clamp(Mathf.Max(Mathf.Abs(_settings.WalkTiltDegrees),
            Mathf.Max(Mathf.Abs(_settings.LookTiltDegrees), Mathf.Abs(_settings.IdleTiltDegrees))), 0f, 45f) * Mathf.Deg2Rad;
        return Mathf.Max(.05f, width * .5f + height * .5f * Mathf.Sin(tilt));
    }

    private static int SideOf(float x, float center, long sequence)
        => x < center - .001f ? -1 : x > center + .001f ? 1 : (sequence & 1) == 0 ? -1 : 1;

    private void DrawnInterval(View view, out float min, out float max)
    {
        float center = view.Brain.GroundPosition.x;
        min = center - view.SpacingHalfWidth;
        max = center + view.SpacingHalfWidth;
        if (view.Body == null || !view.Body.enabled || view.Body.sprite == null) return;
        Bounds bounds = view.Body.bounds;
        float a = transform.InverseTransformPoint(bounds.min).x;
        float b = transform.InverseTransformPoint(bounds.max).x;
        if (!float.IsNaN(a) && !float.IsNaN(b) && !float.IsInfinity(a) && !float.IsInfinity(b))
        { min = Mathf.Min(a, b); max = Mathf.Max(a, b); }
    }

    private bool BodyTouchesHouse(View view, Vector2 center)
    {
        DrawnInterval(view, out float min, out float max);
        float radius = _houseSettings.SafeHouseCrowdingRadius;
        return min <= center.x + radius && max >= center.x - radius
            && Mathf.Abs(view.Brain.GroundPosition.y - center.y) <= radius;
    }

    private bool BodiesOverlap(View first, View second)
    {
        DrawnInterval(first, out float a, out float b);
        DrawnInterval(second, out float c, out float d);
        return Mathf.Min(b, d) - Mathf.Max(a, c) > .02f;
    }

    private bool HasBodyOverlap(View view)
    {
        foreach (var other in _views)
            if (!ReferenceEquals(view, other) && SpacingEligible(other) && BodiesOverlap(view, other)) return true;
        return false;
    }

    private void PruneHouseRequests()
    {
        int count = _houseRequests.Count;
        for (int i = 0; i < count; i++)
        {
            string id = _houseRequests.Dequeue();
            bool invalid = !_owned.Contains(id) || !_catalog.ContainsKey(id) || HasView(id)
                || _arrivals.IsPending(id) || _catalog[id].Sprite == null;
            bool expired = _houseRequestDeadlines.TryGetValue(id, out float deadline) && _clock >= deadline;
            if (invalid || expired)
            {
                _houseQueued.Remove(id);
                _houseRequestDeadlines.Remove(id);
                LastHouseExitDecision = expired ? "cancelled: pending exit deadline expired" : "cancelled: pending ownership/birth/view changed";
            }
            else _houseRequests.Enqueue(id);
        }
    }

    private bool TryPrepareHouseExit(string id, out Vector2 landing, out Vector2 target, out int side)
    {
        landing = target = Vector2.zero;
        side = 0;
        Vector2 center = HouseSpacingCenter;
        Rect bounds = _settings.SafeGroundArea;
        float half = EstimateHalfWidth(_catalog[id].Sprite);
        int preferred = (_houseExitSequence & 1) == 0 ? -1 : 1;
        bool found = false, bestHasClearTarget = false;
        float bestCrowding = float.PositiveInfinity, bestClearance = float.NegativeInfinity;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int direction = attempt == 0 ? preferred : -preferred;
            if (!TryChooseBurstLanding(half, direction, out Vector2 candidate, out _)) continue;
            if (!TryChooseOutwardTarget(null, half, candidate, direction, true, out Vector2 destination))
            {
                // A farther fan-out point can leave too little space beyond it in
                // the short room. Keep its original near-door plan as a fallback.
                candidate = new Vector2(center.x + direction * _houseSettings.SafeEntranceSpread,
                    Mathf.Clamp(center.y, bounds.yMin, bounds.yMax));
                if (candidate.x - half < bounds.xMin || candidate.x + half > bounds.xMax
                    || BlocksFurniture(half, candidate)
                    || !TryChooseOutwardTarget(null, half, candidate, direction, true, out destination)) continue;
            }
            float clearance = TargetClearance(null, half, destination);
            bool hasClearTarget = clearance >= 0f;
            float crowding = OutwardRoomCrowding(direction, half);
            // Compare BOTH geometrically viable lanes before admission. Alternation
            // breaks ties only; it must not pack the short left room while a much
            // wider right room has an unoccupied destination.
            if (found)
            {
                if (bestHasClearTarget && !hasClearTarget) continue;
                if (bestHasClearTarget == hasClearTarget && (crowding > bestCrowding + .001f
                    || Mathf.Abs(crowding - bestCrowding) <= .001f && clearance <= bestClearance)) continue;
            }
            found = true;
            bestHasClearTarget = hasClearTarget;
            bestCrowding = crowding;
            bestClearance = clearance;
            landing = candidate;
            target = destination;
            side = direction;
        }
        // Brief doorway overlap is allowed for the tap burst. Width, destination
        // reservations and room density guide the exit, then existing directed
        // walking/spacing separates the guests without delaying the next spawn.
        if (found) return true;
        LastHouseExitDecision = "waiting: no in-bounds outward exit plan";
        return false;
    }

    private bool TryChooseBurstLanding(float halfWidth, int side, out Vector2 landing, out float clearance)
    {
        Vector2 center = HouseSpacingCenter;
        Rect bounds = _settings.SafeGroundArea;
        float first = _houseSettings.SafeEntranceSpread;
        float last = Mathf.Min(first + _houseSettings.SafeHouseCrowdingRadius,
            side < 0 ? center.x - bounds.xMin - halfWidth : bounds.xMax - center.x - halfWidth);
        landing = center;
        clearance = float.NegativeInfinity;
        if (last < first) return false;
        bool found = false;
        for (int i = 0; i < 9; i++)
        {
            var candidate = new Vector2(center.x + side * Mathf.Lerp(first, last, i / 8f),
                Mathf.Clamp(center.y, bounds.yMin, bounds.yMax));
            if (BlocksFurniture(halfWidth, candidate)) continue;
            float gap = LandingClearance(halfWidth, candidate);
            if (gap >= 0f) { landing = candidate; clearance = gap; return true; }
            if (!found || gap > clearance + .001f)
            { found = true; landing = candidate; clearance = gap; }
        }
        return found;
    }

    private float LandingClearance(float halfWidth, Vector2 landing)
    {
        float clearance = _settings.SafeGroundArea.width;
        foreach (var other in _views)
        {
            if (other.Brain == null || other.Root == null || !other.Root.gameObject.activeInHierarchy) continue;
            float gap = Mathf.Max(_houseSettings.SafeMinimumFairySpacing,
                halfWidth + other.SpacingHalfWidth + _houseSettings.SafeSpriteSpacingPadding);
            clearance = Mathf.Min(clearance, Mathf.Abs(landing.x - other.Brain.GroundPosition.x) - gap);
            if (other.HouseMotion == HouseMotion.Emerging)
                clearance = Mathf.Min(clearance, Mathf.Abs(landing.x - other.HouseEnd.x) - gap);
        }
        return clearance;
    }

    private float OutwardRoomCrowding(int side, float incomingHalfWidth)
    {
        Vector2 center = HouseSpacingCenter;
        Rect bounds = _settings.SafeGroundArea;
        float available = (side < 0 ? center.x - bounds.xMin : bounds.xMax - center.x)
            - _houseSettings.SafeHouseCrowdingRadius;
        float padding = _houseSettings.SafeSpriteSpacingPadding;
        float occupied = incomingHalfWidth * 2f + padding;
        foreach (var view in _views)
        {
            if (view.Brain == null || view.Root == null || !view.Root.gameObject.activeInHierarchy) continue;
            float x = view.HouseMotion == HouseMotion.Emerging ? view.HouseEnd.x : view.Brain.GroundPosition.x;
            if (SideOf(x, center.x, view.DisplaySequence) == side)
                occupied += view.SpacingHalfWidth * 2f + padding;
        }
        return occupied / Mathf.Max(.01f, available);
    }

    private bool TryChooseOutwardTarget(View owner, float halfWidth, Vector2 origin, int side,
        bool newEmergence, out Vector2 selected)
    {
        Rect bounds = _settings.SafeGroundArea;
        Vector2 center = HouseSpacingCenter;
        float padding = _houseSettings.SafeSpriteSpacingPadding;
        float travelMargin = newEmergence
            ? Mathf.Max(_houseSettings.SafeMinimumFairySpacing, halfWidth + PendingExitHalfWidth + padding)
            : Mathf.Max(.25f, halfWidth);
        float firstDistance = Mathf.Max(_houseSettings.SafeHouseCrowdingRadius + halfWidth + padding,
            side * (origin.x - center.x) + travelMargin);
        if (_houseRequests.Count > 0)
            firstDistance = Mathf.Max(firstDistance,
                _houseSettings.SafeEntranceSpread + halfWidth + PendingExitHalfWidth + padding + .15f);
        float roomDistance = side < 0 ? center.x - bounds.xMin - halfWidth : bounds.xMax - center.x - halfWidth;
        selected = origin;
        if (roomDistance < firstDistance) return false;
        bool found = false;
        float bestClearance = float.NegativeInfinity;
        // Prefer nearby free space, then expand only OUTWARD on this same side.
        // A full local band must never send a left exit back through the house.
        for (int pass = 0; pass < 2; pass++)
        {
            float minDistance = pass == 0 ? firstDistance : Mathf.Max(firstDistance, _houseSettings.SafeDispersionSearchDistance);
            float lastDistance = pass == 0 ? Mathf.Min(_houseSettings.SafeDispersionSearchDistance, roomDistance) : roomDistance;
            if (lastDistance < minDistance) continue;
            for (int i = 0; i < 65; i++)
            {
                float distance = Mathf.Lerp(minDistance, lastDistance, i / 64f);
                var candidate = new Vector2(center.x + side * distance,
                    Mathf.Clamp(origin.y, bounds.yMin, bounds.yMax));
                if (BlocksFurniture(halfWidth, candidate)) continue;
                float clearance = TargetClearance(owner, halfWidth, candidate);
                if (clearance >= 0f) { selected = candidate; return true; }
                // A distant stopping point need not have an empty static slot for
                // every body in the entire room: existing guests keep walking.
                if (!found || clearance > bestClearance)
                { found = true; selected = candidate; bestClearance = clearance; }
            }
        }
        return found;
    }

    private float PendingExitHalfWidth
    {
        get
        {
            float half = 0f;
            foreach (string id in _houseRequests)
                if (_catalog.TryGetValue(id, out var entry)) half = Mathf.Max(half, EstimateHalfWidth(entry.Sprite));
            return half;
        }
    }

    private bool BlocksPendingExit(View view)
    {
        if (_houseRequests.Count == 0 || !SpacingEligible(view)) return false;
        float gap = Mathf.Max(_houseSettings.SafeMinimumFairySpacing,
            view.SpacingHalfWidth + PendingExitHalfWidth + _houseSettings.SafeSpriteSpacingPadding);
        float distance = Mathf.Abs(view.Brain.GroundPosition.x - HouseSpacingCenter.x);
        return Mathf.Abs(distance - _houseSettings.SafeEntranceSpread) < gap;
    }

    private bool SpacingEligible(View view) => view.Brain != null && view.Root != null && view.Body != null
        && view.Root.gameObject.activeInHierarchy && view.Body.enabled && view.Body.sprite != null
        && view.HouseMotion == HouseMotion.None
        && !view.Replacing && view.ArrivalRemaining <= 0f && view.PriorityUntil <= _clock
        && !_arrivals.IsPending(view.Brain.ItemId) && _owned.Contains(view.Brain.ItemId);

    private bool IsHouseResident(View view, Vector2 center)
    {
        // Moving bodies still occupy the house area. A state label never resets dwell.
        return SpacingEligible(view) && BodyTouchesHouse(view, center);
    }

    private void UpdateHouseSpacing(float deltaTime)
    {
        if (!HasActiveHouse || !IsVisible)
        {
            if (DispersingCount > 0) CancelAllSpacing();
            return;
        }
        Vector2 center = HouseSpacingCenter;
        int residentsKept = 0;
        // _views preserves display order. Keep the same earliest residents rather
        // than swapping the two permitted residents on every frame.
        foreach (var view in _views)
        {
            bool near = SpacingEligible(view) && BodyTouchesHouse(view, center);
            view.HouseResidentSeconds = near ? view.HouseResidentSeconds + deltaTime : 0f;
            view.HouseOverlapSeconds = near && HasBodyOverlap(view) ? view.HouseOverlapSeconds + deltaTime : 0f;
            bool blocksExit = BlocksPendingExit(view);
            view.ExitLaneBlockedSeconds = blocksExit ? view.ExitLaneBlockedSeconds + deltaTime : 0f;
            view.SpacingYielding = false;
            if (view.Dispersing)
            {
                if (!SpacingEligible(view) || !view.Brain.IsDirectedWalking || _clock >= view.DispersalDeadline)
                    EndSpacingWalk(view);
                else continue;
            }
            if (!near && !blocksExit) continue;
            bool overlapsEarlier = false;
            foreach (var earlier in _views)
            {
                if (ReferenceEquals(earlier, view)) break;
                if (!earlier.Dispersing && IsHouseResident(earlier, center) && BodiesOverlap(view, earlier))
                { overlapsEarlier = true; break; }
            }
            if (_houseRequests.Count == 0 && residentsKept < _houseSettings.SafeHouseResidentLimit && !overlapsEarlier)
            { residentsKept++; continue; }
            if (Mathf.Max(view.HouseResidentSeconds, view.ExitLaneBlockedSeconds)
                < _houseSettings.SafeHouseResidentGraceSeconds) continue;
            BeginHouseDispersal(view);
        }
    }

    private void EndSpacingWalk(View view)
    {
        view.Dispersing = false;
        view.InitialHouseDispersal = false;
        view.SpacingYielding = false;
        view.DispersalTarget = Vector2.zero;
        view.DispersalDeadline = 0f;
        if (view.Brain != null) view.Brain.CancelDirectedWalk();
    }

    private void CancelSpacing(View view)
    {
        EndSpacingWalk(view);
        view.HasPlannedExit = false;
        view.PlannedExitTarget = Vector2.zero;
        view.HouseResidentSeconds = view.HouseOverlapSeconds = 0f;
        view.ExitLaneBlockedSeconds = 0f;
    }

    private bool ShouldYieldSpacingWalk(View view)
    {
        if (!view.Dispersing || !SpacingEligible(view)) return false;
        // A new guest keeps its ordinary outward walk until its whole body has
        // cleared the doorway area. Apply the existing sprite gap outside it.
        if (view.InitialHouseDispersal && BodyTouchesHouse(view, HouseSpacingCenter)) return false;
        float x = view.Brain.GroundPosition.x;
        float direction = Mathf.Sign(view.DispersalTarget.x - x);
        foreach (var other in _views)
        {
            if (ReferenceEquals(view, other) || !SpacingEligible(other)
                || (other.Brain.Action != EnhancementFairyAction.Walk && other.Brain.Action != EnhancementFairyAction.Visit)) continue;
            // The one-time initial outward walk spaces behind fellow launches,
            // but may briefly pass ordinary walkers instead of
            // building a room-long slow queue back into the doorway.
            if (view.InitialHouseDispersal && !other.InitialHouseDispersal) continue;
            float ahead = direction * (other.Brain.GroundPosition.x - x);
            // Strict position order is transitive. An epsilon-based tie can create
            // a three-fairy waiting cycle when their positions are almost equal.
            if (ahead < 0f || (ahead == 0f && other.DisplaySequence > view.DisplaySequence)) continue;
            if (Mathf.Sign(other.Brain.MovementTarget.x - other.Brain.GroundPosition.x) != direction) continue;
            // Already-overlapping new guests keep walking toward their distinct
            // reserved targets instead of freezing together at the area boundary.
            if (view.InitialHouseDispersal && other.InitialHouseDispersal && BodiesOverlap(view, other)) continue;
            float gap = Mathf.Max(_houseSettings.SafeMinimumFairySpacing,
                view.SpacingHalfWidth + other.SpacingHalfWidth + _houseSettings.SafeSpriteSpacingPadding);
            if (ahead < gap)
            {
                // Let the leading walker open a real sprite-sized gap. Do not move or
                // re-target the follower, and never delay birth/furniture/return overrides.
                view.SpacingYielding = true;
                return true;
            }
        }
        return false;
    }

    private void CancelAllSpacing()
    {
        foreach (var view in _views) CancelSpacing(view);
    }

    private Vector2 ResolveSpacedWalkingTarget(EnhancementFairyBrain brain, Vector2 proposed)
    {
        if (!HasActiveHouse || !IsVisible) return proposed;
        View owner = null;
        foreach (var view in _views) if (ReferenceEquals(view.Brain, brain)) { owner = view; break; }
        if (owner == null || !SpacingEligible(owner)) return proposed;
        bool excludeHouse = _houseRequests.Count > 0 || CountOtherResidents(owner) >= _houseSettings.SafeHouseResidentLimit;
        int side = SideOf(brain.GroundPosition.x, HouseSpacingCenter.x, owner.DisplaySequence);
        bool crossing = excludeHouse && side * (proposed.x - HouseSpacingCenter.x) < 0f;
        if (!crossing && (!excludeHouse || Mathf.Abs(proposed.x - HouseSpacingCenter.x)
                > NormalHouseExclusionDistance(owner))
            && !BlocksFurniture(owner, proposed) && TargetClearance(owner, proposed) >= 0f) return proposed;
        return TryChooseSpacedTarget(owner, proposed, out Vector2 replacement, excludeHouse)
            ? replacement : brain.GroundPosition;
    }

    private int CountOtherResidents(View owner)
    {
        int count = 0;
        Vector2 center = HouseSpacingCenter;
        foreach (var view in _views) if (!ReferenceEquals(view, owner) && IsHouseResident(view, center)) count++;
        return count;
    }

    private float NormalHouseExclusionDistance(View owner)
    {
        float distance = _houseSettings.SafeHouseCrowdingRadius + owner.SpacingHalfWidth
            + _houseSettings.SafeSpriteSpacingPadding;
        if (_houseRequests.Count > 0)
            distance = Mathf.Max(distance, _houseSettings.SafeEntranceSpread + owner.SpacingHalfWidth
                + PendingExitHalfWidth + _houseSettings.SafeSpriteSpacingPadding + .15f);
        return distance;
    }

    private bool TryChooseSpacedTarget(View owner, Vector2 preferred,
        out Vector2 selected, bool excludeHouse = true)
    {
        Rect bounds = _settings.SafeGroundArea;
        Vector2 center = HouseSpacingCenter;
        selected = owner.Brain.GroundPosition;
        float bestClearance = float.NegativeInfinity, bestCost = float.PositiveInfinity;
        bool found = false;
        float localRadius = Mathf.Max(Mathf.Abs(_settings.WalkRadius.x), _houseSettings.SafeMinimumFairySpacing * 2f);
        int side = SideOf(owner.Brain.GroundPosition.x, center.x, owner.DisplaySequence);
        // Fixed candidates and existing view/target reservations require no per-frame
        // arrays, physics, pathfinder, new fairies or repeated target re-rolls.
        const int samples = 65;
        {
            float min = Mathf.Max(bounds.xMin + owner.SpacingHalfWidth, preferred.x - localRadius);
            float max = Mathf.Min(bounds.xMax - owner.SpacingHalfWidth, preferred.x + localRadius);
            if (min > max) return false;
            for (int i = 0; i < samples; i++)
            {
                float x = Mathf.Lerp(min, max, i / (float)(samples - 1));
                var candidate = new Vector2(x, Mathf.Clamp(owner.Brain.GroundPosition.y, bounds.yMin, bounds.yMax));
                if (excludeHouse && side * (x - center.x) < 0f) continue;
                if (excludeHouse && Mathf.Abs(x - center.x)
                    <= NormalHouseExclusionDistance(owner)) continue;
                if (BlocksFurniture(owner, candidate)) continue;
                float clearance = TargetClearance(owner, candidate);
                float cost = Mathf.Abs(x - preferred.x) * .1f - Mathf.Min(3f, clearance) * .2f;
                bool clear = clearance >= 0f, oldClear = bestClearance >= 0f;
                if (!found || (clear && !oldClear) || (clear == oldClear
                    && (clear ? cost < bestCost : clearance > bestClearance + .001f
                        || (Mathf.Abs(clearance - bestClearance) <= .001f && cost < bestCost))))
                {
                    found = true;
                    selected = candidate;
                    bestClearance = clearance;
                    bestCost = cost;
                }
            }
        }
        return found;
    }

    private float TargetClearance(View owner, Vector2 candidate)
        => TargetClearance(owner, owner.SpacingHalfWidth, candidate);

    private float TargetClearance(View owner, float halfWidth, Vector2 candidate)
    {
        float clearance = _settings.SafeGroundArea.width;
        foreach (var other in _views)
        {
            if (ReferenceEquals(owner, other) || other.Brain == null || other.Root == null
                || !other.Root.gameObject.activeInHierarchy) continue;
            float separation = Mathf.Max(_houseSettings.SafeMinimumFairySpacing,
                halfWidth + other.SpacingHalfWidth + _houseSettings.SafeSpriteSpacingPadding);
            clearance = Mathf.Min(clearance, Mathf.Abs(candidate.x - other.Brain.GroundPosition.x) - separation);
            if (other.HouseMotion == HouseMotion.Emerging)
            {
                clearance = Mathf.Min(clearance, Mathf.Abs(candidate.x - other.HouseEnd.x) - separation);
                if (other.HasPlannedExit)
                    clearance = Mathf.Min(clearance, Mathf.Abs(candidate.x - other.PlannedExitTarget.x) - separation);
            }
            else if (other.Dispersing)
                clearance = Mathf.Min(clearance, Mathf.Abs(candidate.x - other.DispersalTarget.x) - separation);
            else if (other.HouseMotion == HouseMotion.None
                && other.ArrivalRemaining <= 0f && (other.Brain.Action == EnhancementFairyAction.Walk
                    || other.Brain.Action == EnhancementFairyAction.Visit))
                clearance = Mathf.Min(clearance, Mathf.Abs(candidate.x - other.Brain.MovementTarget.x) - separation);
        }
        return clearance;
    }

    private bool BlocksFurniture(View owner, Vector2 candidate)
        => BlocksFurniture(owner.SpacingHalfWidth, candidate);

    private bool BlocksFurniture(float halfWidth, Vector2 candidate)
    {
        if (_playArea == null || !_playArea.isActiveAndEnabled || _playArea.Points == null) return false;
        float radius = Mathf.Max(.6f, halfWidth + _houseSettings.SafeSpriteSpacingPadding);
        foreach (var point in _playArea.Points)
        {
            if (point == null || !point.Enabled || !point.HasValidReferences) continue;
            if (Mathf.Abs(candidate.x - FurnitureLocal(point.Approach).x) < radius
                || Mathf.Abs(candidate.x - FurnitureLocal(point.Activity).x) < radius
                || Mathf.Abs(candidate.x - FurnitureLocal(point.Exit).x) < radius) return true;
        }
        return false;
    }
}
