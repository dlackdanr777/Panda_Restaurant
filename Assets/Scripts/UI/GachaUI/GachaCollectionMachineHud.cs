using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using E = GachaCollectionUiElements;

/// <summary>Cosmetic progress follows retained results. Refresh always retains the authoritative final counter.</summary>
public sealed class GachaCollectionMachineHud : MonoBehaviour
{
    public const string EntryButtonResourcePath = "UI/GachaCollection/TokenExchangeEntryButton";
    private GachaCollectionUiTheme _theme;
    private TMP_FontAsset _font;
    private TextMeshProUGUI _progress;
    private RectTransform _gauge;
    private RectTransform _fill;
    private Button _exchange;
    private UnityAction _exchangeClick;
    private bool _exchangeAvailable = true;
    private RectTransform _catalogFrame, _catalogTitle, _catalogExit;
    private UIGachaSlotList _catalog;
    private Vector2 _catalogSize, _catalogPosition, _titlePosition, _exitPosition, _catalogParentSize;
    private readonly Vector3[] _catalogCorners = new Vector3[4];
    private readonly Vector3[] _exchangeCorners = new Vector3[4];
    private int _committed, _target;
    private float _start, _from, _until;
    private bool _guaranteed;
    private readonly Queue<ProgressStep> _pending = new Queue<ProgressStep>();
    private struct ProgressStep { public int Before, After; public bool Guaranteed; }
    public RectTransform Rect => (RectTransform)transform;
    public int CommittedCounter => _committed;
    public float DisplayedProgress => _fill == null ? 0 : _fill.anchorMax.x;
    public int PendingProgressCount => _pending.Count + (_until != 0 ? 1 : 0);
    public bool IsGaugeVisible => _gauge != null && _gauge.gameObject.activeSelf;
    public Button ExchangeButton => _exchange;

    public static GachaCollectionMachineHud Attach(RectTransform parent, GachaCollectionUiTheme theme,
        Action openExchange, Action drawTicket, Button exchangeButton = null, bool instantiateEntryWhenMissing = true)
    {
        if (parent == null || theme == null || theme.PandaToken == null) throw new ArgumentException("머신 UI 테마가 필요합니다.");
        var rect = E.Rect("Collection Machine HUD", parent, 0, 105, 1060, 199);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,1);
        rect.pivot = new Vector2(.5f,1);
        var view = rect.gameObject.AddComponent<GachaCollectionMachineHud>();
        view._theme = theme; view._font = E.FontFrom(parent, theme);
        view.Build();
        if (exchangeButton == null && instantiateEntryWhenMissing)
        {
            var prefab = Resources.Load<Button>(EntryButtonResourcePath);
            if (prefab != null)
            {
                exchangeButton = Instantiate(prefab, parent, false);
                exchangeButton.name = prefab.name;
            }
            else Debug.LogError("토큰 교환소 진입 버튼 Prefab이 없습니다: Resources/" + EntryButtonResourcePath, parent);
        }
        view.BindExchangeButton(exchangeButton, openExchange);
        view.Refresh(0, 0, false);
        return view;
    }

    private void Build()
    {
        _gauge = E.Rect("Special Guarantee Gauge", transform, 0, 0, 1060, 60);
        E.Panel("Gauge Border", _gauge, 0, 0, 1060, 60, _theme.Ink, 27);
        E.Panel("Gauge Track", _gauge, 6, 6, 1048, 48, new Color32(255,247,220,255), 22);
        var region = E.Rect("Gauge Fill Region", _gauge, 8, 8, 1044, 44);
        var fill = E.Panel("Special Guarantee Fill", region, 0, 0, 0, 0, _theme.Accent, 20);
        _fill = fill.rectTransform; _fill.anchorMin = Vector2.zero; _fill.anchorMax = Vector2.one;
        _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        _progress = E.Label("Special Guarantee Progress", _gauge, _font, "", 12, 5, 1036, 31, 27, _theme.Ink);
        E.Label("Bonus Guarantee Notice", _gauge, _font, "10+1의 보너스는 보장 횟수에서 제외", 12, 36, 1036, 17, 15, _theme.Ink);
    }

    public void BindExchangeButton(Button button, Action openExchange)
    {
        if (_exchange != null && _exchangeClick != null) _exchange.onClick.RemoveListener(_exchangeClick);
        if (_exchange != null && _exchange != button) _exchange.gameObject.SetActive(false);
        _exchange = button;
        _exchangeClick = null;
        if (_exchange == null) return;
        _exchangeClick = () => openExchange?.Invoke();
        _exchange.onClick.AddListener(_exchangeClick);
        GachaButtonInputSound.Bind(_exchange);
        _exchange.interactable = _exchangeAvailable;
    }

    public void SetExchangeAvailable(bool available)
    {
        _exchangeAvailable = available;
        if (_exchange == null) return;
        _exchange.interactable = available;
        if (!available) _exchange.gameObject.SetActive(false);
    }

    public void BindCatalogLayout(RectTransform catalogFrame, RectTransform title = null, UIGachaSlotList catalog = null, RectTransform exit = null)
    {
        if (catalogFrame == null) return;
        _catalogFrame = catalogFrame; _catalogTitle = title; _catalog = catalog;
        _catalogExit = exit;
        _catalogSize = catalogFrame.sizeDelta; _catalogPosition = catalogFrame.anchoredPosition;
        if (title != null) _titlePosition = title.anchoredPosition;
        if (exit != null) _exitPosition = exit.anchoredPosition;
    }

    private void LateUpdate() => RefreshCatalogLayout();

    // Preserve full card sizes on short, wide viewports; only resize/reposition the frame.
    private void RefreshCatalogLayout()
    {
        var view = transform.parent as RectTransform;
        if (_catalog == null || _catalogFrame == null) return;
        var parent = _catalogFrame.parent as RectTransform;
        if (parent == null || view == null || parent.rect.size == _catalogParentSize) return;
        _catalogParentSize = parent.rect.size;
        _catalogFrame.sizeDelta = _catalogSize; _catalogFrame.anchoredPosition = _catalogPosition;
        if (_catalogTitle != null) _catalogTitle.anchoredPosition = _titlePosition;
        if (_catalogExit != null) _catalogExit.anchoredPosition = _exitPosition;
        _catalogFrame.GetWorldCorners(_catalogCorners);
        float oldTop = view.InverseTransformPoint(_catalogCorners[2]).y;
        _catalogFrame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(_catalogFrame.rect.height, _catalog.FourRowFrameHeight(_catalogFrame)));
        _catalogFrame.GetWorldCorners(_catalogCorners);
        float bottom = view.InverseTransformPoint(_catalogCorners[0]).y;
        float top = view.InverseTransformPoint(_catalogCorners[2]).y;
        // The editable entry is outside this HUD. Measure its size in the view's coordinates,
        // leaving its authored anchor, position, rotation and scale untouched.
        float entryHeight = 0;
        if (_exchange != null)
        {
            ((RectTransform)_exchange.transform).GetWorldCorners(_exchangeCorners);
            float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            foreach (var corner in _exchangeCorners)
            {
                float y = view.InverseTransformPoint(corner).y;
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            entryHeight = maxY - minY;
        }
        float shift = Mathf.Max(0, view.rect.yMin + entryHeight + 28 - bottom);
        shift = Mathf.Min(shift, view.rect.yMax - 70 - top);
        _catalogFrame.position += view.TransformVector(new Vector3(0, shift, 0));
        if (_catalogTitle != null) _catalogTitle.position += view.TransformVector(new Vector3(0, top + shift - oldTop, 0));
        if (_catalogExit != null)
        {
            var board = RectTransformUtility.CalculateRelativeRectTransformBounds(view, _catalogFrame);
            var close = RectTransformUtility.CalculateRelativeRectTransformBounds(view, _catalogExit);
            if (board.Intersects(close))
                _catalogExit.position += view.TransformVector(new Vector3(board.min.x - close.max.x - 12, 0, 0));
        }
    }

    public void SetResultPresentation(bool presenting)
    {
        _gauge.gameObject.SetActive(!presenting);
        if (_exchange != null) _exchange.gameObject.SetActive(!presenting && _exchangeAvailable);
        if (!presenting) SnapToCommitted();
    }

    public void Refresh(int pity, int tickets, bool busy, bool canUseTicket = true)
    {
        _committed = Mathf.Clamp(pity, 0, 99);
        if (_exchange != null) _exchange.interactable = !busy && _exchangeAvailable;
        if (_until == 0) SnapToCommitted();
    }

    public void PresentResult(int before, int after, bool guaranteedSpecial)
    {
        var step = new ProgressStep { Before = before, After = after, Guaranteed = guaranteedSpecial };
        if (_until != 0) { _pending.Enqueue(step); return; }
        Begin(step);
    }

    private void Begin(ProgressStep step)
    {
        _start = Time.unscaledTime; _from = Mathf.Clamp(step.Before,0,99);
        _target = Mathf.Clamp(step.After,0,99); _guaranteed = step.Guaranteed;
        _until = _start + (step.Guaranteed ? .8f : .16f);
        Show(_from);
    }

    public void SnapToCommitted()
    {
        _until = 0; _guaranteed = false; _pending.Clear();
        _progress.rectTransform.localScale = Vector3.one; Show(_committed);
    }

    private void Update()
    {
        if (_until == 0) return;
        float elapsed = Time.unscaledTime - _start;
        if (_guaranteed && elapsed < .55f)
        {
            Show(Mathf.Lerp(_from,100,Mathf.Clamp01(elapsed / .2f)));
            _progress.text = "스페셜 보장!  100 / 100";
            _progress.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(elapsed / .55f * Mathf.PI) * .07f);
        }
        else
        {
            _progress.rectTransform.localScale = Vector3.one;
            Show(_guaranteed ? _target : Mathf.Lerp(_from,_target,Mathf.Clamp01(elapsed / .16f)));
        }
        if (Time.unscaledTime >= _until)
        {
            if (_pending.Count > 0) Begin(_pending.Dequeue());
            else SnapToCommitted();
        }
    }

    private void Show(float progress)
    {
        _fill.anchorMax = new Vector2(Mathf.Clamp01(progress / 100f),1);
        _progress.text = "스페셜 보장  " + Mathf.RoundToInt(progress) + " / 100" + (progress >= 99 && progress < 100 ? "  · 다음 뽑기 확정!" : "");
    }
    private void OnDisable()
    {
        if (_fill != null) SnapToCommitted();
        if (_exchange != null) _exchange.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_exchange != null && _exchangeClick != null) _exchange.onClick.RemoveListener(_exchangeClick);
    }
}
