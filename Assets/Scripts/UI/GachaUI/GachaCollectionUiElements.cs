using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class GachaCollectionUiElements
{
    internal static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    internal static GachaRoundedPanel Panel(string name, Transform parent, float x, float y, float width,
        float height, Color color, float radius = 18f)
    {
        var rect = Rect(name, parent, x, y, width, height);
        var panel = rect.gameObject.AddComponent<GachaRoundedPanel>();
        panel.Radius = radius; panel.color = color; panel.raycastTarget = false;
        return panel;
    }
    internal static Image Icon(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
    {
        var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
        image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }
    internal static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, string value,
        float x, float y, float width, float height, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Max(14, size * .62f); text.enableAutoSizing = true;
        text.color = color; text.alignment = align; text.raycastTarget = false;
        return text;
    }
    internal static Button Button(string name, Transform parent, TMP_FontAsset font, string label,
        float x, float y, float width, float height, Color fill, Color ink, UnityEngine.Events.UnityAction action)
    {
        var panel = Panel(name, parent, x, y, width, height, fill);
        panel.raycastTarget = true;
        var outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = ink; outline.effectDistance = new Vector2(2, -3);
        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel;
        var colors = button.colors; colors.disabledColor = new Color(.64f,.64f,.64f,1); button.colors = colors;
        Label("Label", panel.transform, font, label, 8, 3, width - 16, height - 6, 28, ink);
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
    internal static TMP_FontAsset FontFrom(Transform parent, GachaCollectionUiTheme theme)
    {
        TMP_Text text = parent.GetComponentInChildren<TMP_Text>(true);
        return text != null && text.font != null ? text.font : theme.Font;
    }
    internal static Button ArtButton(string name, Transform parent, TMP_FontAsset font, string label, Sprite sprite,
        float x, float y, float width, float height, Color ink, UnityEngine.Events.UnityAction action)
    {
        var graphic = Icon(name, parent, sprite, x, y, width, height);
        // Image.preserveAspect aligns inside its rect using the pivot. Keep the same outer
        // bounds but center the artwork, so a wide hit area and its caption share a center.
        graphic.rectTransform.pivot = new Vector2(.5f, .5f);
        graphic.rectTransform.anchoredPosition = new Vector2(x + width * .5f, -y - height * .5f);
        graphic.raycastTarget = true;
        var button = graphic.gameObject.AddComponent<Button>();
        button.targetGraphic = graphic;
        Label("Label", graphic.transform, font, label, 5, 3, width - 10, height - 6, 25, ink);
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
}
