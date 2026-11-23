using UnityEngine;

/// <summary>Temporary visual physics, rendered and pooled by the existing habitat.</summary>
public sealed partial class EnhancementFairyHabitat : IWorldPointerGestureHandler
{
    private sealed partial class View
    {
        public EnhancementFairyPhysicsSolver.Body PhysicsBody;
        public float PhysicsCooldownUntil;
        public float PhysicsSessionStartedAt = float.NegativeInfinity;
#if UNITY_EDITOR
        public int ObservedPhysicsBounces;
#endif
    }

    private readonly EnhancementFairyPhysicsSolver _physics = new EnhancementFairyPhysicsSolver();
    private readonly EnhancementFairyPhysicsSolver.Body[] _physicsBodies = new EnhancementFairyPhysicsSolver.Body[64];
    private readonly Vector2[] _pointerSamples = new Vector2[16];
    private readonly float[] _pointerSampleTimes = new float[16];
    private Vector2 _latestPointerPosition;
    private float _latestPointerTime;
    private float _lastPointerMovementTime;
    private int _pointerSampleCount, _gesturePointer;
    private View _gestureView;
    private Vector2 _gestureStartScreen, _grabOffset;
    private float _gestureStartedAt;
    private ThirdFloorApprovedArt _physicsApprovedArt;
    private bool _cleaningPhysics;
    private bool _gestureDidDrag;
#if UNITY_EDITOR
    private int _observedDrags, _observedThrows, _observedCollisionActivations, _observedBounces, _observedPhysicsReturns, _observedPeakPhysics;
    private Vector2 _observedReleaseVelocity;
    private string _lastPhysicsPickDiagnostic = "No pickup attempted";
#endif

    public int PhysicalFairyCount
    {
        get { int count = 0; foreach (var view in _views) if (IsPhysical(view)) count++; return count; }
    }
    public string DraggedFairyItemId => _gestureView != null && IsPhysical(_gestureView) ? _gestureView.Brain?.ItemId : null;
    public string PendingGestureItemId => _gestureView != null && !IsPhysical(_gestureView) ? _gestureView.Brain?.ItemId : null;
    public bool TryGetPhysicsBody(string itemId, out EnhancementFairyPhysicsSolver.Body body)
    {
        foreach (var view in _views)
            if (view.Brain != null && view.Brain.ItemId == itemId) { body = view.PhysicsBody; return body != null; }
        body = null;
        return false;
    }

    private static bool IsPhysical(View view) => view?.PhysicsBody != null
        && view.PhysicsBody.Mode != EnhancementFairyPhysicsSolver.BodyMode.Autonomous;
    private bool PhysicsOwns(View view) => ReferenceEquals(view, _gestureView) || IsPhysical(view);

    private void InitializePhysics(View view)
    {
        if (view.PhysicsBody == null) view.PhysicsBody = new EnhancementFairyPhysicsSolver.Body();
        var body = view.PhysicsBody;
        body.Id = view.Root.GetInstanceID();
        body.Mode = EnhancementFairyPhysicsSolver.BodyMode.Autonomous;
        body.Enabled = body.Eligible = body.Finished = body.JustActivated = body.DragExpired = body.Settling = false;
        body.Velocity = Vector2.zero;
        body.Age = body.Angle = body.Squash = 0f;
        view.PhysicsCooldownUntil = 0f;
        view.PhysicsSessionStartedAt = float.NegativeInfinity;
#if UNITY_EDITOR
        view.ObservedPhysicsBounces = 0;
#endif
        SyncPhysicsBody(view);
    }

    private bool CanRunPhysics()
    {
        if (_settings?.Physics == null || !_settings.Physics.Enabled || !IsVisible || !isActiveAndEnabled
            || IsItemCardOpen || IsItemCardPending || _approachingFloor3
            || (_physicsApprovedArt != null && (!_physicsApprovedArt.isActiveAndEnabled || !_physicsApprovedArt.UseApprovedArt))) return false;
        if (!_runtime) return true;
        if (Input.touchCount > 1) return false;
        if (_cameraController == null || _worldCamera == null || _cameraController.IsCameraMoving
            || _cameraController.IsWorldInputBlocked || _cameraController.CurrentFloor != ERestaurantFloorType.Floor3
            || _waitingForUiHide || HasClosingViews() || UserInfo.IsTutorialStart
            || PopupManager.IsCustomerEncounterPresentationBlocked
            || (_navigation != null && _navigation.GetOpenViewCount() > 0)) return false;
        foreach (var ui in _sceneViews)
            if (ui != null && ui.gameObject.activeInHierarchy && ui.VisibleState != VisibleState.Disappeared) return false;
        return true;
    }

    private bool PhysicsEligible(View view)
    {
        if (view?.Brain == null || view.Root == null || view.Body == null || !view.Body.enabled || view.Body.sprite == null
            || !view.Root.gameObject.activeInHierarchy || view.Body.color.a < .5f || view.HouseMotion != HouseMotion.None
            || view.Replacing || view.ArrivalRemaining > 0f || view.PriorityUntil > _clock || view.PhysicsCooldownUntil > _clock
            || _arrivals.IsPending(view.Brain.ItemId) || !_owned.Contains(view.Brain.ItemId)) return false;
        var body = view.PhysicsBody;
        if (body == null) return false;
        // The entire sprite must already fit: accepting an edge hit must never snap it inside a wall.
        Vector2 center = IsPhysical(view) ? body.Position : new Vector2(view.Brain.GroundPosition.x,
            _settings.Physics.SafeBounds.yMin + body.HalfExtents.y + view.Brain.VisualHeight);
        Rect room = _settings.Physics.SafeBounds;
        return center.x >= room.xMin + body.HalfExtents.x && center.x <= room.xMax - body.HalfExtents.x
            && center.y >= room.yMin + body.HalfExtents.y - .01f && center.y <= room.yMax - body.HalfExtents.y;
    }

    private void SyncPhysicsBody(View view)
    {
        var body = view.PhysicsBody;
        if (body == null || view.Brain == null || _settings?.Physics == null) return;
        if (view.Body == null || view.Root == null) { body.Enabled = body.Eligible = false; return; }
        if (!IsPhysical(view))
        {
            Vector2 size = view.Body.sprite != null ? (Vector2)view.Body.sprite.bounds.size * view.Scale : Vector2.one;
            float radians = _settings.Physics.SafeAngularReactionLimit * Mathf.Deg2Rad;
            // Conservative visual extents include squash and tilt; actual foot anchoring remains in Render.
            body.HalfExtents = new Vector2((size.x + size.y * Mathf.Sin(radians)) * .57f,
                (size.y + size.x * Mathf.Sin(radians)) * .57f);
            body.Radius = Mathf.Max(.12f, Mathf.Min(size.x, size.y) * .5f * _settings.Physics.SafeCollisionRadiusScale);
            body.Mass = Mathf.Clamp(body.Radius * body.Radius, .2f, 3f);
            body.Position = new Vector2(view.Brain.GroundPosition.x,
                _settings.Physics.SafeBounds.yMin + body.HalfExtents.y + view.Brain.VisualHeight);
            body.Velocity = Vector2.zero;
        }
        body.Enabled = view.Root != null && view.Root.gameObject.activeInHierarchy && view.Body != null && view.Body.enabled;
        // A pending press owns this fairy until it becomes a drag or a short tap.
        // An incoming airborne fairy must not convert it to Physics in that gap.
        body.Eligible = PhysicsEligible(view) && (!ReferenceEquals(view, _gestureView) || IsPhysical(view));
    }

    public bool TryBegin(int pointerId, Vector2 screenPosition, float unscaledTime)
    {
#if UNITY_EDITOR
        _lastPhysicsPickDiagnostic = "input/presentation unavailable";
#endif
        if (!CanRunPhysics() || !_runtime || _gestureView != null || !_worldCamera.pixelRect.Contains(screenPosition)
            || !TryPhysicsLocal(screenPosition, out Vector2 point)) return false;
        // A visible fairy in front of a prop can own a drag. Only its actual mesh
        // takes this priority; exposed props and transparent padding retain their taps.
        View nearest = null;
        float distance = float.PositiveInfinity;
#if UNITY_EDITOR
        _lastPhysicsPickDiagnostic = "no direct fairy mesh hit";
#endif
        foreach (var view in _views)
        {
            SyncPhysicsBody(view);
            if (view.Body == null || !view.Body.enabled || !IsDirectFairyMeshHit(view, screenPosition)) continue;
            if (!PhysicsEligible(view))
            {
#if UNITY_EDITOR
                _lastPhysicsPickDiagnostic = view.PhysicsCooldownUntil > _clock
                    ? "cooldown remaining=" + (view.PhysicsCooldownUntil - _clock).ToString("F2")
                    : "fairy protected, unavailable or outside physical bounds";
#endif
                continue;
            }
            if (!IsPhysical(view) && PhysicalFairyCount >= _settings.Physics.SafeMaximumPhysicalFairies)
            {
#if UNITY_EDITOR
                _lastPhysicsPickDiagnostic = "physical capacity=" + PhysicalFairyCount + "/" + _settings.Physics.SafeMaximumPhysicalFairies;
#endif
                continue;
            }
            float candidate = ((Vector2)view.Body.bounds.center - (Vector2)transform.TransformPoint(point)).sqrMagnitude;
            if (candidate < distance) { nearest = view; distance = candidate; }
        }
        if (nearest == null) return false;
#if UNITY_EDITOR
        _lastPhysicsPickDiagnostic = "captured=" + nearest.Brain.ItemId + "; active=" + PhysicalFairyCount;
#endif
        // An already airborne fairy is captured immediately, but still cannot produce a card on release.
        _gestureView = nearest;
        _gestureDidDrag = IsPhysical(nearest);
        _gesturePointer = pointerId;
        _gestureStartScreen = screenPosition;
        _gestureStartedAt = _clock;
        _grabOffset = nearest.PhysicsBody.Position - point;
        _pointerSampleCount = 0;
        AddPointerSample(point, unscaledTime);
        if (IsPhysical(nearest)) { CancelSpacing(nearest); _physics.BeginDrag(nearest.PhysicsBody, _settings.Physics); }
        else nearest.PhysicsBody.Eligible = false;
        return true;
    }

    // A visible interactive object is not empty floor merely because it is temporarily
    // protected, cooling down, under a prop, or unable to obtain another physics slot.
    public bool BlocksCameraDrag(Vector2 screenPosition)
    {
        // Target reservation is deliberately independent of current simulation eligibility.
        // A temporarily unavailable fairy must never become a background camera swipe.
        if (!_runtime || _settings?.Physics == null || !_settings.Physics.Enabled || !isActiveAndEnabled || !IsVisible
            || _cameraController == null || _worldCamera == null || _cameraController.CurrentFloor != ERestaurantFloorType.Floor3
            || (_physicsApprovedArt != null && (!_physicsApprovedArt.isActiveAndEnabled || !_physicsApprovedArt.UseApprovedArt))
            || !_worldCamera.pixelRect.Contains(screenPosition)
            || !TryPhysicsLocal(screenPosition, out Vector2 point)) return false;
        if (_playArea != null && _playArea.IsFurnitureTapTarget(screenPosition)) return true;
        if (_houseOwner is ThirdFloorFairyHouse house && house.isActiveAndEnabled && house.TouchCollider != null
            && house.TouchCollider.enabled && house.TouchCollider.gameObject.activeInHierarchy
            && house.TouchCollider.OverlapPoint(transform.TransformPoint(point))) return true;
        foreach (var view in _views)
        {
            if (view.Brain != null && view.Root != null && view.Root.gameObject.activeInHierarchy
                && view.Body != null && view.Body.enabled && view.Body.sprite != null && view.Body.color.a > .01f
                && _owned.Contains(view.Brain.ItemId))
            {
                // Match the existing card's small touch allowance. Missing a tiny mesh
                // by a few pixels must not turn a fairy pickup attempt into a floor swipe.
                Vector3 lower = _worldCamera.WorldToScreenPoint(view.Body.bounds.min);
                Vector3 upper = _worldCamera.WorldToScreenPoint(view.Body.bounds.max);
                float minimum = Mathf.Clamp(Screen.dpi * .7f / 2.54f, 44f, 96f);
                Vector2 size = new Vector2(Mathf.Max(minimum, upper.x - lower.x), Mathf.Max(minimum, upper.y - lower.y));
                Vector2 center = (lower + upper) * .5f;
                if (lower.z > 0f && new Rect(center - size * .5f, size).Contains(screenPosition)) return true;
            }
        }
        return false;
    }

    public void Move(int pointerId, Vector2 screenPosition, float unscaledTime)
    {
        if (_gestureView == null || pointerId != _gesturePointer) return;
        if (!CanRunPhysics() || !TryPhysicsLocal(screenPosition, out Vector2 point)) { Cancel(); return; }
        var view = _gestureView;
        if (!IsPhysical(view) && Vector2.Distance(screenPosition, _gestureStartScreen) >= _settings.Physics.GetDragThreshold(Screen.dpi))
        {
            if (!PhysicsEligible(view)) { Cancel(); return; }
            _gestureDidDrag = true;
            // The pending slot normally guarantees this. Keep the hard cap even
            // if another caller changes presentation state between pointer frames.
            if (PhysicalFairyCount >= _settings.Physics.SafeMaximumPhysicalFairies) return;
            CancelSpacing(view);
            if (float.IsNegativeInfinity(view.PhysicsSessionStartedAt)) view.PhysicsSessionStartedAt = _clock;
            view.PhysicsBody.Eligible = true;
            _physics.BeginDrag(view.PhysicsBody, _settings.Physics);
#if UNITY_EDITOR
            _observedDrags++;
            view.ObservedPhysicsBounces = 0;
#endif
        }
        AddPointerSample(point, unscaledTime);
        if (IsPhysical(view)) _physics.SetDragTarget(view.PhysicsBody, point + _grabOffset, _settings.Physics);
    }

    public void End(int pointerId, Vector2 screenPosition, float unscaledTime)
    {
        if (_gestureView == null || pointerId != _gesturePointer) return;
        Move(pointerId, screenPosition, unscaledTime);
        var view = _gestureView;
        if (view == null) return;
        bool dragged = _gestureDidDrag;
        _gestureView = null;
        if (dragged)
        {
            _physics.Release(view.PhysicsBody, RecentPointerVelocity(unscaledTime), _settings.Physics);
#if UNITY_EDITOR
            _observedThrows++;
            _observedReleaseVelocity = view.PhysicsBody.Velocity;
#endif
        }
        else TryOpenItemCard(screenPosition);
        _pointerSampleCount = 0;
        _gestureDidDrag = false;
    }

    public void Cancel() => CancelAllPhysics();

    private bool TryPhysicsLocal(Vector2 screen, out Vector2 local)
    {
        local = Vector2.zero;
        if (_worldCamera == null) return false;
        var ray = _worldCamera.ScreenPointToRay(screen);
        var plane = new Plane(transform.forward, transform.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        local = transform.InverseTransformPoint(ray.GetPoint(distance));
        return EnhancementFairyPhysicsSolver.Finite(local);
    }

    private void AddPointerSample(Vector2 point, float time)
    {
        if (!EnhancementFairyPhysicsSolver.Finite(time) || !EnhancementFairyPhysicsSolver.Finite(point)) return;
        if (_pointerSampleCount > 0 && time < _latestPointerTime) return;
        if (_pointerSampleCount == 0 || (point - _latestPointerPosition).sqrMagnitude > .00000001f)
            _lastPointerMovementTime = time;
        _latestPointerPosition = point;
        _latestPointerTime = time;
        // Keep at least 0.12 seconds of history even at high frame rates. Recording
        // every held/stationary frame used to erase the flick before pointer-up.
        if (_pointerSampleCount > 0 && time - _pointerSampleTimes[_pointerSampleCount - 1] < 1f / 120f) return;
        if (_pointerSampleCount == _pointerSamples.Length)
        {
            for (int i = 1; i < _pointerSampleCount; i++) { _pointerSamples[i - 1] = _pointerSamples[i]; _pointerSampleTimes[i - 1] = _pointerSampleTimes[i]; }
            _pointerSampleCount--;
        }
        _pointerSamples[_pointerSampleCount] = point;
        _pointerSampleTimes[_pointerSampleCount++] = time;
    }

    private Vector2 RecentPointerVelocity(float now)
    {
        // A fully stopped hand drops the fairy even if the last moving frame was
        // coalesced between history samples. Do not interpolate motion past that stop.
        if (_pointerSampleCount == 0 || now - _latestPointerTime > .12f
            || now - _lastPointerMovementTime >= .12f) return Vector2.zero;
        float start = Mathf.Max(_pointerSampleTimes[0], now - .12f);
        int first = 0;
        while (first + 1 < _pointerSampleCount && _pointerSampleTimes[first + 1] <= start) first++;
        Vector2 origin = _pointerSamples[first];
        float nextTime = first + 1 < _pointerSampleCount ? _pointerSampleTimes[first + 1] : _latestPointerTime;
        Vector2 nextPosition = first + 1 < _pointerSampleCount ? _pointerSamples[first + 1] : _latestPointerPosition;
        if (nextTime > _pointerSampleTimes[first])
            origin = Vector2.Lerp(origin, nextPosition, (start - _pointerSampleTimes[first]) / (nextTime - _pointerSampleTimes[first]));
        float seconds = now - start;
        return seconds > .0001f ? (_latestPointerPosition - origin) / seconds : Vector2.zero;
    }

    private void AdvancePhysics(float dt)
    {
        if (!CanRunPhysics()) { CancelAllPhysics(); return; }
        if (_gestureView != null && _clock - _gestureStartedAt >= _settings.Physics.SafeMaximumDragSeconds)
        { CancelAllPhysics(); return; }
        int count = Mathf.Min(_views.Count, _physicsBodies.Length);
        for (int i = 0; i < count; i++)
        {
            View view = _views[i];
            if (IsPhysical(view) && !float.IsNegativeInfinity(view.PhysicsSessionStartedAt)
                && _clock - view.PhysicsSessionStartedAt >= _settings.Physics.SafeMaximumDragSeconds + _settings.Physics.SafeMaximumPhysicalDuration)
                CancelPhysicsView(view);
            if (IsPhysical(view) && (view.Root == null || !view.Root.gameObject.activeInHierarchy || view.Body == null || !view.Body.enabled || view.HouseMotion != HouseMotion.None
                || view.ArrivalRemaining > 0f || !_owned.Contains(view.Brain.ItemId))) CancelPhysicsView(view);
            SyncPhysicsBody(view);
            _physicsBodies[i] = view.PhysicsBody;
        }
        _physics.Step(_physicsBodies, count, dt, _settings.Physics,
            _gestureView != null && !IsPhysical(_gestureView) ? 1 : 0);
        for (int i = 0; i < count; i++)
        {
            View view = _views[i];
            var body = view.PhysicsBody;
            if (body == null) continue;
#if UNITY_EDITOR
            if (body.JustActivated && body.Mode == EnhancementFairyPhysicsSolver.BodyMode.Physics) _observedCollisionActivations++;
            if (body.BounceCount > view.ObservedPhysicsBounces) _observedBounces += body.BounceCount - view.ObservedPhysicsBounces;
            view.ObservedPhysicsBounces = body.BounceCount;
#endif
            if (body.JustActivated)
            {
                CancelSpacing(view);
                if (float.IsNegativeInfinity(view.PhysicsSessionStartedAt)) view.PhysicsSessionStartedAt = _clock;
                body.JustActivated = false;
            }
            if (body.Finished) FinishPhysicsView(view);
        }
#if UNITY_EDITOR
        _observedPeakPhysics = Mathf.Max(_observedPeakPhysics, PhysicalFairyCount);
#endif
    }

    private void FinishPhysicsView(View view)
    {
        var body = view.PhysicsBody;
        if (view.Brain != null) view.Brain.PlaceForBirth(new Vector2(body.Position.x, _settings.SafeGroundArea.yMin));
        body.Finished = body.JustActivated = body.DragExpired = false;
        view.PhysicsSessionStartedAt = float.NegativeInfinity;
#if UNITY_EDITOR
        _observedPhysicsReturns++;
#endif
        // Prevent repeated grabs/chain impacts from indefinitely postponing an already due house return.
        view.PhysicsCooldownUntil = _clock + (_houseSession && _clock >= _houseReturnDueAt ? 20f : .25f);
    }

    private void CancelPhysicsView(View view)
    {
        if (ReferenceEquals(view, _gestureView))
        {
            _gestureView = null;
            _pointerSampleCount = 0;
            _cameraController?.CancelWorldPointerGesture(this);
        }
        if (IsPhysical(view) && _settings?.Physics != null)
        {
            _physics.Cancel(view.PhysicsBody, _settings.Physics);
            FinishPhysicsView(view);
            if (view.Root != null && view.Body != null) Render(view);
        }
        else if (view.PhysicsBody != null)
        {
            view.PhysicsBody.Squash = view.PhysicsBody.Angle = 0f;
            view.PhysicsBody.Velocity = Vector2.zero;
        }
    }

    private void CancelAllPhysics()
    {
        if (_cleaningPhysics) return;
        _cleaningPhysics = true;
        _gestureView = null;
        _gestureDidDrag = false;
        _pointerSampleCount = 0;
        _cameraController?.CancelWorldPointerGesture(this);
        foreach (var view in _views) CancelPhysicsView(view);
        _physics.Reset();
        _cleaningPhysics = false;
    }

    private void OnApplicationPause(bool paused) { if (paused) CancelAllPhysics(); }
    private void OnApplicationFocus(bool focused) { if (!focused) CancelAllPhysics(); }
}
