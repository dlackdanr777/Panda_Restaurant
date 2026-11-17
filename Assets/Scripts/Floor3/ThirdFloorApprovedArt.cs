using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Switches only the authored third-floor art references, preserving their previous enabled states.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ThirdFloorApprovedArt : MonoBehaviour
{
    [SerializeField, Tooltip("ON: approved rooms and small map button. OFF: restore the previous third-floor art.")]
    private bool _useApprovedArt = true;
    [SerializeField, Tooltip("Child containing the approved SpriteRenderers and map hit area.")]
    private GameObject _approvedContent;
    [SerializeField] private SpriteRenderer[] _legacySprites = Array.Empty<SpriteRenderer>();
    [SerializeField, Tooltip("Only the old map hit areas; keep camera drag and fairy colliders out of this list.")]
    private Collider2D[] _legacyColliders = Array.Empty<Collider2D>();

    // Serialized so saving/reloading while approved art is visible never loses the original states.
    [SerializeField, HideInInspector] private bool _hasLegacySnapshot;
    [SerializeField, HideInInspector] private List<SpriteState> _spriteStates = new List<SpriteState>();
    [SerializeField, HideInInspector] private List<ColliderState> _colliderStates = new List<ColliderState>();
    private bool _refreshRequested;
    private bool _applying;
    private GameObject _shownContent;

    [Serializable]
    private struct SpriteState
    {
        public SpriteRenderer Target;
        public bool Enabled;
    }

    [Serializable]
    private struct ColliderState
    {
        public Collider2D Target;
        public bool Enabled;
    }

    public bool UseApprovedArt
    {
        get => _useApprovedArt;
        set
        {
            _useApprovedArt = value;
            Refresh();
            RecordInstanceState(this);
        }
    }

    private void OnEnable() => Refresh();

    // OnValidate may run while Unity is loading data. Defer object changes to the editor update.
    private void OnValidate()
    {
        if (!_applying) _refreshRequested = true;
    }

    private void Update()
    {
        if (_refreshRequested) Refresh();
    }

    private void OnDisable() => Apply(false);
    private void OnDestroy() => OnDisable();

    public void Refresh() => Apply(isActiveAndEnabled && _useApprovedArt && HasOwnedContent());

    private void Apply(bool showApproved)
    {
        if (_applying) return;
        _refreshRequested = false;
        // Recording prefab overrides can schedule another OnValidate. A settled state must
        // not toggle children or recapture disabled renderers on that next editor update.
        if (showApproved && _shownContent == _approvedContent && _approvedContent.activeSelf
            && SnapshotMatchesHiddenTargets()) return;
        if (!showApproved && !_hasLegacySnapshot && _shownContent == null
            && (!HasOwnedContent() || !_approvedContent.activeSelf)) return;

        _applying = true;
        try
        {
            HideApprovedContent();
            RestoreLegacy();
            if (showApproved)
            {
                // Capture before disabling any entry, including duplicated references.
                _spriteStates.Clear();
                _colliderStates.Clear();
                if (_legacySprites != null)
                    foreach (var target in _legacySprites)
                        if (target != null)
                            _spriteStates.Add(new SpriteState { Target = target, Enabled = target.enabled });
                if (_legacyColliders != null)
                    foreach (var target in _legacyColliders)
                        if (target != null)
                            _colliderStates.Add(new ColliderState { Target = target, Enabled = target.enabled });

                _hasLegacySnapshot = true;
                foreach (var state in _spriteStates) SetEnabled(state.Target, false);
                foreach (var state in _colliderStates) SetEnabled(state.Target, false);
                SetContentActive(_approvedContent, true);
                _shownContent = _approvedContent;
            }
            RecordInstanceState(this);
        }
        finally { _applying = false; }
    }

    private bool SnapshotMatchesHiddenTargets()
    {
        if (!_hasLegacySnapshot) return false;
        int index = 0;
        if (_legacySprites != null)
            foreach (var target in _legacySprites)
            {
                if (target == null) continue;
                if (index >= _spriteStates.Count || _spriteStates[index++].Target != target || target.enabled) return false;
            }
        if (index != _spriteStates.Count) return false;
        index = 0;
        if (_legacyColliders != null)
            foreach (var target in _legacyColliders)
            {
                if (target == null) continue;
                if (index >= _colliderStates.Count || _colliderStates[index++].Target != target || target.enabled) return false;
            }
        return index == _colliderStates.Count;
    }

    private bool HasOwnedContent() => _approvedContent != null
        && _approvedContent != gameObject && _approvedContent.transform.IsChildOf(transform);

    private void HideApprovedContent()
    {
        if (_shownContent != null && _shownContent != gameObject)
            SetContentActive(_shownContent, false);
        _shownContent = null;
        if (HasOwnedContent()) SetContentActive(_approvedContent, false);
    }

    private void RestoreLegacy()
    {
        if (!_hasLegacySnapshot) return;
        foreach (var state in _spriteStates)
            if (state.Target != null) SetEnabled(state.Target, state.Enabled);
        foreach (var state in _colliderStates)
            if (state.Target != null) SetEnabled(state.Target, state.Enabled);
        _spriteStates.Clear();
        _colliderStates.Clear();
        _hasLegacySnapshot = false;
    }

    private static void SetContentActive(GameObject target, bool active)
    {
        if (target.activeSelf == active) return;
        target.SetActive(active);
        RecordInstanceState(target);
    }

    private static void SetEnabled(SpriteRenderer target, bool value)
    {
        if (target.enabled == value) return;
        target.enabled = value;
        RecordInstanceState(target);
    }

    private static void SetEnabled(Collider2D target, bool value)
    {
        if (target.enabled == value) return;
        target.enabled = value;
        RecordInstanceState(target);
    }

    private static void RecordInstanceState(UnityEngine.Object target)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
            || target == null || UnityEditor.EditorUtility.IsPersistent(target)) return;
        GameObject owner = target is Component component ? component.gameObject : target as GameObject;
        if (owner == null || !owner.scene.IsValid()
            || !UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target)) return;
        // Preserve the restore snapshot and presentation overrides when the user saves.
        // Do not save scenes/assets, record Undo, or apply changes to the source prefab.
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
#endif
    }
}
