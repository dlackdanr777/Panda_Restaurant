using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using E = GachaCollectionUiElements;

/// <summary>Cosmetic progress follows retained results. Refresh always retains the authoritative final counter.</summary>
public sealed class GachaCollectionMachineHud : MonoBehaviour
{
    private GachaCollectionUiTheme _theme;
    private TMP_FontAsset _font;
    private TextMeshProUGUI _progress, _tickets;
    private RectTransform _fill;
    private Button _exchange, _ticket;
    private int _committed, _target;
    private float _start, _from, _until;
    private bool _guaranteed;
    private readonly Queue<ProgressStep> _pending = new Queue<ProgressStep>();
    private struct ProgressStep { public int Before, After; public bool Guaranteed; }
    public RectTransform Rect => (RectTransform)transform;
    public int CommittedCounter => _committed;
    public float DisplayedProgress => _fill == null ? 0 : _fill.anchorMax.x;
    public int PendingProgressCount => _pending.Count + (_until != 0 ? 1 : 0);

    public static GachaCollectionMachineHud Attach(RectTransform parent, GachaCollectionUiTheme theme,
        Action openExchange, Action drawTicket)
    {
        if (parent == null || theme == null || theme.PandaToken == null) throw new ArgumentException("머신 UI 테마가 필요합니다.");
        var rect = E.Rect("Collection Machine HUD", parent, 0, 105, 1060, 199);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,1);
        rect.pivot = new Vector2(.5f,1);
        var view = rect.gameObject.AddComponent<GachaCollectionMachineHud>();
        view._theme = theme; view._font = E.FontFrom(parent, theme);
        view.Build(openExchange, drawTicket); view.Refresh(0, 0, false);
        return view;
    }

    private void Build(Action openExchange, Action drawTicket)
    {
        E.Panel("Gauge Border", transform, 0, 0, 1060, 60, _theme.Ink, 27);
        E.Panel("Gauge Track", transform, 6, 6, 1048, 48, new Color32(255,247,220,255), 22);
        var region = E.Rect("Gauge Fill Region", transform, 8, 8, 1044, 44);
        var fill = E.Panel("Special Guarantee Fill", region, 0, 0, 0, 0, _theme.Accent, 20);
        _fill = fill.rectTransform; _fill.anchorMin = Vector2.zero; _fill.anchorMax = Vector2.one;
        _fill.offsetMin = _fill.offsetMax = Vector2.zero;
        _progress = E.Label("Special Guarantee Progress", transform, _font, "", 12, 5, 1036, 31, 27, _theme.Ink);
        E.Label("Bonus Guarantee Notice", transform, _font, "10+1의 보너스는 보장 횟수에서 제외", 12, 36, 1036, 17, 15, _theme.Ink);
        _exchange = E.Button("Open Token Exchange", transform, _font, "토큰 교환소", -234, 77, 344, 64,
            _theme.Cream, _theme.Ink, () => openExchange?.Invoke());
        E.Icon("Actual Panda Token", _exchange.transform, _theme.PandaToken, 14, 8, 48, 48);
        var exchangeLabel = _exchange.GetComponentInChildren<TextMeshProUGUI>();
        exchangeLabel.rectTransform.offsetMin = new Vector2(63, -58);
        exchangeLabel.rectTransform.offsetMax = new Vector2(333, -3);
        _ticket = E.Button("Use One Machine Ticket", transform, _font, "뽑기권 1장 사용", 946, 77, 354, 64,
            _theme.Cream, _theme.Ink, () => drawTicket?.Invoke());
        E.Icon("Machine Ticket", _ticket.transform, _theme.Ticket, 12, 9, 48, 45);
        var ticketLabel = _ticket.GetComponentInChildren<TextMeshProUGUI>();
        ticketLabel.rectTransform.offsetMin = new Vector2(62, -58);
        ticketLabel.rectTransform.offsetMax = new Vector2(343, -3);
        _tickets = E.Label("Machine Ticket Balance", transform, _font, "", 946, 148, 225, 45, 22, _theme.Cream);
    }

    public void SetResultPresentation(bool presenting)
    {
        // The single result's badge reaches above the native card; keep its top edge clear.
        Rect.anchoredPosition = new Vector2(0, presenting ? -45 : -105);
        _exchange.gameObject.SetActive(!presenting);
        _ticket.gameObject.SetActive(!presenting);
        _tickets.gameObject.SetActive(!presenting);
    }

    public void Refresh(int pity, int tickets, bool busy, bool canUseTicket = true)
    {
        _committed = Mathf.Clamp(pity, 0, 99);
        _tickets.text = "보유 뽑기권  " + tickets;
        _exchange.interactable = !busy;
        _ticket.interactable = !busy && tickets > 0 && canUseTicket;
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
    private void OnDisable() { if (_fill != null) SnapToCommitted(); }
}
