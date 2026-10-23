using System;
using System.Collections.Generic;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class PopupStoreOrderProgressSlot
{
    [SerializeField] private Image[] _circles = new Image[5];
    public IReadOnlyList<Image> Circles => _circles;
}

/// <summary>Leading-match guidance only. Never selects a customer, fulfils an order or consumes materials.</summary>
[DisallowMultipleComponent]
public sealed class PopupStoreOrderProgress : MonoBehaviour
{
    [SerializeField] private PopupStoreSalesCounter _counter;
    [SerializeField] private PopupStoreOrderDisplay _orders;
    [SerializeField] private PopupStoreOrderProgressSlot[] _slots = new PopupStoreOrderProgressSlot[3];
    [SerializeField] private AudioClip _matchSound;
    [SerializeField, Range(0, 1)] private float _soundVolume = .35f;
    [SerializeField, Min(0)] private float _soundDuration = .75f;
    [SerializeField, Min(1)] private float _pulseScale = 1.05f;
    [SerializeField, Min(0)] private float _pulseDuration = .18f;

    private PopupStoreSalesCounter _boundCounter;
    private PopupStoreOrderDisplay _boundOrders;
    private CircleView[][] _views;
    private readonly int[] _matched = new int[3];
    private SoundManager _playingSoundManager;
    private float _soundStopAt;
    private string _lastError;

    public bool IsReady { get; private set; }
    // One notification per newly extended prefix batch. EditMode does not play native audio/tweens.
    public event Action FeedbackTriggered;
    public int GetMatchedCount(int slotIndex)
        => IsReady && slotIndex >= 0 && slotIndex < _matched.Length ? _matched[slotIndex] : 0;

    public bool Refresh()
    {
        Unbind();
        StopSound();
        HideCircles(_views);
        Array.Clear(_matched, 0, _matched.Length);
        IsReady = false;
        if (!TryCapture(out var views, out string error))
        {
            HideCircles(views);
            if (_lastError != error) Debug.LogError("PopupStoreOrderProgress: " + error, this);
            _lastError = error;
            return false;
        }
        _views = views;
        _boundCounter = _counter;
        _boundOrders = _orders;
        _boundCounter.Changed += OnCounterChanged;
        _boundOrders.Refreshed += OnOrdersRefreshed;
        IsReady = true;
        _lastError = null;
        Evaluate(false);
        return true;
    }

    [ContextMenu("Refresh Progress")]
    private void RefreshProgress() => Refresh();

    private bool TryCapture(out CircleView[][] views, out string error)
    {
        views = new CircleView[3][];
        error = null;
        var popup = GetComponent<UIPopupStore>();
        if (popup == null || _counter == null || _orders == null || _orders.gameObject != gameObject
            || _counter.GetComponentInParent<UIPopupStore>(true) != popup)
        { error = "Counter and order display must belong to this popup store."; return false; }
        var orderSlots = _orders.Slots;
        if (_slots == null || _slots.Length != 3 || orderSlots.Count != 3)
        { error = "Exactly three progress slots must correspond to the three order slots."; return false; }
        if (!Finite(_pulseScale) || _pulseScale < 1 || !NonNegative(_pulseDuration)
            || !NonNegative(_soundDuration) || !NonNegative(_soundVolume) || _soundVolume > 1)
        { error = "Feedback settings must have finite non-negative durations, volume 0-1 and scale at least one."; return false; }
        var unique = new HashSet<Image>();
        for (int i = 0; i < 3; i++)
        {
            var circles = _slots[i]?.Circles;
            var icons = orderSlots[i]?.MaterialIcons;
            if (circles == null || circles.Count != 5 || icons == null || icons.Count != 5)
            { error = "Each progress slot requires five circles matching its order icons."; return false; }
            views[i] = new CircleView[5];
            for (int j = 0; j < 5; j++)
            {
                var circle = circles[j];
                if (!Owned(circle) || icons[j] == null || circle.transform.parent != icons[j].transform
                    || circle.sprite == null || !unique.Add(circle))
                { error = "Each distinct circle must be a child of its corresponding order icon and have a sprite."; return false; }
                views[i][j] = new CircleView { Image = circle, BaseScale = circle.transform.localScale };
            }
        }
        return true;
    }

    private void OnCounterChanged(PopupStoreSalesCounter.ChangeReason reason)
    {
        bool added = reason == PopupStoreSalesCounter.ChangeReason.Added;
        if (!added) StopSound();
        Evaluate(added);
    }

    private void OnOrdersRefreshed()
    {
        StopSound();
        if (_boundOrders == null || !_boundOrders.IsReady)
        {
            HideCircles(_views);
            Array.Clear(_matched, 0, _matched.Length);
            return;
        }
        // Recapture only the circle bindings; order icon references may have changed in the Inspector.
        Refresh();
    }

    private void Evaluate(bool allowFeedback)
    {
        if (!IsReady) return;
        bool available = isActiveAndEnabled && _boundCounter != null && _boundCounter.IsReady
            && _boundCounter.isActiveAndEnabled && _boundOrders != null && _boundOrders.IsReady
            && _boundOrders.isActiveAndEnabled;
        bool increased = false;
        for (int i = 0; i < 3; i++)
        {
            int previous = _matched[i];
            int next = available ? PopupStoreOrderMatch.CountPrefix(_boundOrders.GetDisplayedMaterials(i), _boundCounter.Materials) : 0;
            _matched[i] = next;
            for (int j = 0; j < 5; j++)
            {
                var circle = _views[i][j];
                ResetPulse(circle);
                circle.Image.raycastTarget = false;
                circle.Image.gameObject.SetActive(j < next);
                if (allowFeedback && j >= previous && j < next)
                {
                    increased = true;
                    Pulse(circle);
                }
            }
        }
        if (allowFeedback && increased) PlayFeedback();
    }

    private void Pulse(CircleView view)
    {
        if (!Application.isPlaying || _pulseDuration <= 0 || _pulseScale <= 1) return;
        view.Image.transform.localScale = view.BaseScale * _pulseScale;
        view.Pulsing = true;
        view.Image.rectTransform.TweenScale(view.BaseScale, _pulseDuration, Ease.OutQuad);
    }

    private void PlayFeedback()
    {
        if (Application.isPlaying && _matchSound != null && _soundVolume > 0 && _soundDuration > 0)
        {
            StopSound();
            var manager = SoundManager.TryGetExistingInstance();
            if (manager != null && manager.PlayPresentationEffect(EffectType.None, _matchSound, _soundVolume,
                1f, this, 1) != null)
            {
                _playingSoundManager = manager;
                _soundStopAt = Time.unscaledTime + _soundDuration;
            }
        }
        FeedbackTriggered?.Invoke();
    }

    private void ResetPulse(CircleView view)
    {
        if (view == null || !Owned(view.Image)) return;
        if (view.Pulsing) view.Image.rectTransform.TweenStop();
        view.Pulsing = false;
        view.Image.transform.localScale = view.BaseScale;
    }

    private void HideCircles(CircleView[][] views)
    {
        if (views == null) return;
        foreach (var slot in views)
        {
            if (slot == null) continue;
            foreach (var view in slot)
            {
                if (view == null || !Owned(view.Image)) continue;
                ResetPulse(view);
                view.Image.gameObject.SetActive(false);
            }
        }
    }

    private void StopSound()
    {
        if (_playingSoundManager != null) _playingSoundManager.StopPresentationEffects(this);
        _playingSoundManager = null;
        _soundStopAt = 0;
    }

    private void Unbind()
    {
        if (_boundCounter != null) _boundCounter.Changed -= OnCounterChanged;
        if (_boundOrders != null) _boundOrders.Refreshed -= OnOrdersRefreshed;
        _boundCounter = null;
        _boundOrders = null;
    }

    private bool Owned(Component component)
        => component != null && component.GetComponentInParent<PopupStoreOrderProgress>(true) == this;
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool NonNegative(float value) => Finite(value) && value >= 0;
    private void OnEnable() => Refresh();
    private void OnDisable()
    {
        Unbind();
        StopSound();
        HideCircles(_views);
        Array.Clear(_matched, 0, _matched.Length);
        IsReady = false;
    }
    private void OnDestroy() { Unbind(); StopSound(); }
    private void Update()
    {
        if (_playingSoundManager != null && Time.unscaledTime >= _soundStopAt) StopSound();
    }

    private sealed class CircleView
    {
        internal Image Image;
        internal Vector3 BaseScale;
        internal bool Pulsing;
    }
}
