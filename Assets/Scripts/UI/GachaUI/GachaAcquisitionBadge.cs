using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Muks.Tween;

/// <summary>Presentation of a retained acquisition flag. Never reads the user's current inventory.</summary>
public sealed class GachaAcquisitionBadge : MonoBehaviour
{
    private RectTransform _rect;
    private RectTransform _frame;
    private float _started;
    public bool IsNew { get; private set; }

    public static GachaAcquisitionBadge Bind(Transform card, TMP_FontAsset font, bool isNew, RectTransform frame = null)
    {
        GachaAcquisitionBadge badge = card.GetComponentInChildren<GachaAcquisitionBadge>(true);
        if (badge == null)
        {
            var root = new GameObject("Confirmed New Badge", typeof(RectTransform), typeof(GachaRoundedPanel));
            root.layer = card.gameObject.layer;
            root.transform.SetParent(card, false);
            var panel = root.GetComponent<GachaRoundedPanel>();
            panel.color = new Color32(217, 89, 72, 255);
            panel.Radius = 16;
            panel.raycastTarget = false;
            panel.maskable = false;
            var outline = root.AddComponent<Outline>();
            outline.effectColor = new Color32(87, 49, 35, 255);
            outline.effectDistance = new Vector2(3, -3);
            var labelObject = new GameObject("신규!", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.layer = card.gameObject.layer;
            labelObject.transform.SetParent(root.transform, false);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = "신규!";
            label.fontSize = 30;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(255, 250, 223, 255);
            label.raycastTarget = false;
            label.maskable = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            badge = root.AddComponent<GachaAcquisitionBadge>();
        }
        badge._rect = (RectTransform)badge.transform;
        badge._rect.anchorMin = badge._rect.anchorMax = Vector2.one;
        badge._rect.pivot = new Vector2(.5f, .5f);
        badge._frame = frame;
        badge._rect.sizeDelta = frame == null ? new Vector2(116, 50) : new Vector2(80, 34);
        badge.GetComponentInChildren<TextMeshProUGUI>(true).fontSize = frame == null ? 30 : 22;
        badge.PositionOverFrame();
        badge.IsNew = isNew;
        badge._started = Time.unscaledTime;
        badge._rect.localScale = Vector3.one;
        badge.enabled = isNew;
        badge.gameObject.SetActive(isNew);
        badge.transform.SetAsLastSibling();
        return badge;
    }

    private void PositionOverFrame()
    {
        if (_rect == null) return;
        if (_frame == null) { _rect.anchoredPosition = new Vector2(-48, -12); return; }
        var parent = (RectTransform)_rect.parent;
        Vector2 corner = parent.InverseTransformPoint(_frame.TransformPoint(_frame.rect.max));
        // A slot's 220px hit area is shorter than its visible card. Anchor to the actual frame corner.
        _rect.anchoredPosition = corner - parent.rect.max + new Vector2(-36, 8);
    }

    private void OnRectTransformDimensionsChange() => PositionOverFrame();

    private void OnEnable() { _started = Time.unscaledTime; }
    private void Update()
    {
        if (_rect == null) return;
        float t = Mathf.Clamp01((Time.unscaledTime - _started) / .2f);
        _rect.localScale = Vector3.one * Mathf.Lerp(.88f, 1f, 1f - (1f - t) * (1f - t));
        if (t >= 1f) enabled = false;
    }
}

/// <summary>Native rotating child effects exist inside both authored rank frames. Explicitly reset both on reuse.</summary>
public static class GachaCardHaloPolicy
{
    public static bool HasHalo(Rank rank) => rank == Rank.Special;

    public static void Apply(Image uniqueFrame, Image specialFrame, Rank rank)
    {
        Set(uniqueFrame, false);
        Set(specialFrame, HasHalo(rank));
    }

    private static void Set(Image frame, bool active)
    {
        if (frame == null) return;
        foreach (RotationGameObject rotation in frame.GetComponentsInChildren<RotationGameObject>(true))
        {
            rotation.Stop();
            foreach (TweenData tween in rotation.GetComponents<TweenData>()) tween.Clear();
            rotation.transform.localRotation = Quaternion.identity;
            rotation.enabled = active;
            rotation.gameObject.SetActive(active);
            if (active) rotation.Play();
        }
    }
}
