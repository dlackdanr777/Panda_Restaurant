using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using E = GachaCollectionUiElements;

public enum TokenExchangeTab { Tickets, Staff, Items, Mixed }

/// <summary>A retained display projection; the service validates the product, price and display version.</summary>
public sealed class TokenExchangeProductView
{
    public string Id, Name, Description, Status;
    public TokenExchangeTab Category;
    public Sprite Sprite;
    public Rank Rank;
    public long Price;
    public int Quantity, DisplayVersion;
    public bool CanPurchase, SoldOut;
}

public sealed class TokenExchangeSnapshot
{
    public long PandaTokens;
    public bool Busy, CanRefresh;
    public int DisplayVersion, RemainingRefreshes, RefreshLimit = 3;
    public string RefreshMessage;
    public IReadOnlyList<TokenExchangeProductView> Products = Array.Empty<TokenExchangeProductView>();
}

/// <summary>One hanging modal. The view never rolls a catalog, grants a product or alters a wallet.</summary>
public sealed class TokenExchangeView : MonoBehaviour
{
    private GachaCollectionUiTheme _theme;
    private TMP_FontAsset _font;
    private Func<TokenExchangeSnapshot> _read;
    private Action<string, int, Action<bool, string>> _purchase;
    private Action<TokenExchangeTab, int, Action<bool, string>> _refreshCatalog;
    private Action _closed;
    private RectTransform _board, _content, _balanceFrame;
    private TextMeshProUGUI _balance, _name, _description, _quantity, _price, _buyLabel, _refreshLabel;
    private string _requestError;
    private Image _art;
    private Button _buy, _refresh;
    private readonly List<ProductCard> _cards = new List<ProductCard>();
    private sealed class ProductCard
    {
        public string Id;
        public Button Button;
        public Image Frame, Artwork, Token;
        public Image[] Stars;
        public TextMeshProUGUI Name, Price, Availability;
        public RectTransform Stamp;
        public CanvasGroup StampGroup;
        public string OfferKey;
        public bool SoldOut;
        public float StampStarted = -1;
    }
    private TokenExchangeSnapshot _snapshot;
    private string _selectedId;
    private bool _requesting, _entering;
    private int _requestVersion;
    private float _refreshAt, _openedAt;
    public bool IsOpen => gameObject.activeSelf;
    public bool IsEntering => _entering;
    public string SelectedProductId => _selectedId;
    public RectTransform Board => _board;

    public static TokenExchangeView Attach(Transform parent, GachaCollectionUiTheme theme,
        Func<TokenExchangeSnapshot> snapshot, Action<string, Action<bool, string>> requestPurchase, Action closed = null)
        => Attach(parent, theme, snapshot, (id, version, reply) => requestPurchase(id, reply), null, closed);

    public static TokenExchangeView Attach(Transform parent, GachaCollectionUiTheme theme,
        Func<TokenExchangeSnapshot> snapshot, Action<string, int, Action<bool, string>> requestPurchase,
        Action<TokenExchangeTab, int, Action<bool, string>> requestRefresh, Action closed = null)
    {
        if (parent == null || theme == null || theme.PandaToken == null || snapshot == null || requestPurchase == null)
            throw new ArgumentException("교환소의 정식 UI 테마와 거래 연결이 필요합니다.");
        var root = E.Rect("Token Exchange", parent, 0, 0, 0, 0);
        root.gameObject.SetActive(false); E.Stretch(root);
        var view = root.gameObject.AddComponent<TokenExchangeView>();
        view._theme = theme; view._font = E.FontFrom(parent, theme); view._read = snapshot;
        view._purchase = requestPurchase; view._refreshCatalog = requestRefresh; view._closed = closed;
        view.Build();
        return view;
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            if (!IsOpen) return;
            _entering = false;
            _board.anchoredPosition = Vector2.zero; _board.localRotation = Quaternion.identity;
            gameObject.SetActive(false); _closed?.Invoke();
            return;
        }
        if (IsOpen) return;
        _openedAt = Time.realtimeSinceStartup; _entering = true;
        gameObject.SetActive(true); transform.SetAsLastSibling(); Refresh(); Fit();
        TickPresentation(_openedAt);
    }

    /// <summary>A service replacement invalidates view callbacks without cancelling an in-flight save.</summary>
    public void ResetSession()
    {
        ++_requestVersion; _requesting = false; _snapshot = null; _selectedId = null; _refreshAt = 0; _requestError = null;
        foreach (var card in _cards)
        { card.OfferKey = null; card.SoldOut = false; card.StampStarted = -1; card.Stamp.gameObject.SetActive(false); }
        SetVisible(false);
    }

    public void SelectProduct(string id)
    {
        if (_requesting || _entering || _snapshot == null || !_snapshot.Products.Any(p => p.Id == id)) return;
        _selectedId = id; _requestError = null; RebuildCards();
    }

    public void Refresh()
    {
        _snapshot = _read() ?? new TokenExchangeSnapshot { Busy = true };
        _balance.text = _snapshot.PandaTokens.ToString("N0");
        RebuildCards();
    }

    private void Build()
    {
        var shade = E.Panel("Input Blocking Backdrop", transform, 0, 0, 0, 0, new Color(0, 0, 0, .78f), 0);
        E.Stretch(shade.rectTransform); shade.raycastTarget = true;
        _board = E.Rect("Wooden Exchange Board", transform, 0, 0, 1380, 925);
        _board.anchorMin = _board.anchorMax = _board.pivot = new Vector2(.5f, .5f);
        _board.anchoredPosition = Vector2.zero;
        // The chain's upper length extends offscreen. Every link retains the original art's aspect ratio.
        E.Icon("Left Hanging Chain", _board, _theme.LeftChain, 250, -390, 54, 536);
        E.Icon("Right Hanging Chain", _board, _theme.RightChain, 1110, -390, 54, 536);
        E.Icon("Original Exchange Wood Frame", _board, _theme.WoodFrame, 0, 125, 1380, 799.1f);
        E.ExchangeTitle("Original Wooden Title", _board, _theme, _font, 455, 73, 470, 162);
        _balanceFrame = E.Icon("External Panda Token Balance", transform, _theme.CurrencyPill, 0, 0, 250, 68.5558f).rectTransform;
        _balanceFrame.anchorMin = _balanceFrame.anchorMax = new Vector2(.5f, .5f);
        E.Icon("Balance Token", _balanceFrame, _theme.PandaToken, -5.8f, -7.35f, 80, 80);
        _balance = E.Label("Available Panda Tokens", _balanceFrame, _font, "0", 88.45f, 10.24f, 142.22f, 49.84f,
            38, new Color32(110, 56, 16, 255));
        _balance.fontStyle = FontStyles.Bold;
        E.ArtButton("Close Exchange", _board, _font, "", _theme.ExchangeClose, 1290, 151, 62, 62,
            _theme.Ink, () => SetVisible(false));

        SlicePanel("Native Selected Product Frame", 108, 248, 470, 609);
        _art = E.Icon("Selected Product Image", _board, null, 215, 278, 258, 224);
        _name = E.Label("Selected Product Name", _board, _font, "", 138, 517, 410, 57, 33, _theme.Ink);
        SlicePanel("Native Description Frame", 141, 580, 404, 155);
        _description = E.Label("Selected Product Description", _board, _font, "", 156, 594, 374, 125, 24,
            _theme.Ink, TextAlignmentOptions.TopLeft);
        _description.textWrappingMode = TextWrappingModes.Normal;
        _description.enableAutoSizing = false;
        _description.overflowMode = TextOverflowModes.Ellipsis;
        _quantity = E.Label("Quantity", _board, _font, "", 153, 742, 380, 29, 21, _theme.Ink);
        E.Icon("Price Panda Token", _board, _theme.PandaToken, 228, 778, 35, 35);
        _price = E.Label("Token Price", _board, _font, "", 270, 776, 173, 38, 29, _theme.Ink, TextAlignmentOptions.Left);
        _buy = E.ArtButton("Exchange Selected Product", _board, _font, "교환하기", _theme.ExchangeBuy,
            219, 817, 248, 57, _theme.Ink, RequestPurchase);
        _buyLabel = _buy.GetComponentInChildren<TextMeshProUGUI>();

        _content = E.Rect("Product Grid", _board, 693, 275, 596, 524);
        for (int i = 0; i < 6; i++) _cards.Add(CreateCard(i));
        _refreshLabel = E.Label("Remaining Refreshes", _board, _font, "", 885, 848, 310, 40, 25,
            _theme.Cream, TextAlignmentOptions.Right);
        _refresh = E.ArtButton("Refresh Display", _board, _font, "", _theme.ExchangeRefresh,
            1213, 832, 72, 72, _theme.Ink, RequestRefresh);
    }

    private Image SlicePanel(string name, float x, float y, float width, float height, float pixelScale = 8)
    {
        // The theme references an unchanged native PNG copied with a local slice border. Creating
        // a sprite from another sprite's texture + rect would sample the wrong region when packed.
        Image image = E.Icon(name, _board, _theme.DetailFrame, x, y, width, height);
        image.type = Image.Type.Sliced; image.preserveAspect = false; image.pixelsPerUnitMultiplier = pixelScale;
        return image;
    }

    private void RebuildCards()
    {
        if (_snapshot == null) return;
        var products = (_snapshot.Products ?? Array.Empty<TokenExchangeProductView>()).Take(6).ToList();
        var selected = products.FirstOrDefault(p => p.Id == _selectedId) ?? products.FirstOrDefault();
        _selectedId = selected?.Id;
        bool locked = _entering || _requesting || _snapshot.Busy;
        const int slots = 6;
        while (_cards.Count < slots) _cards.Add(CreateCard(_cards.Count));
        for (int i = 0; i < _cards.Count; i++)
        {
            ProductCard card = _cards[i];
            card.Button.gameObject.SetActive(i < slots);
            if (i >= slots) continue;
            TokenExchangeProductView product = i < products.Count ? products[i] : null;
            card.Id = product?.Id;
            card.Button.name = product == null ? "Unavailable Slot " + i : "Product " + product.Id;
            card.Button.interactable = !_entering && !_requesting && product != null;
            card.Frame.sprite = _theme.ProductFrame(product?.Rank ?? Rank.Normal1);
            card.Frame.color = product == null ? new Color(1, 1, 1, .4f) :
                product.Id == _selectedId ? new Color32(255, 234, 173, 255) : Color.white;
            card.Artwork.sprite = product?.Sprite; card.Artwork.enabled = card.Artwork.sprite != null;
            card.Name.text = product?.Name ?? "판매 상품 없음";
            card.Price.text = product?.Price.ToString("N0") ?? "";
            card.Token.enabled = product != null;
            card.Availability.text = product == null ? "" : CompactAvailability(product);
            string offerKey = product == null ? null : product.DisplayVersion + ":" + product.Id;
            bool soldOut = product?.SoldOut == true;
            bool animate = offerKey == card.OfferKey && soldOut && !card.SoldOut;
            if (offerKey != card.OfferKey || !soldOut) card.StampStarted = -1;
            if (animate) card.StampStarted = Time.realtimeSinceStartup;
            card.OfferKey = offerKey; card.SoldOut = soldOut;
            card.Stamp.gameObject.SetActive(soldOut);
            UpdateStamp(card, Time.realtimeSinceStartup);
            for (int star = 0; star < card.Stars.Length; star++)
                card.Stars[star].enabled = product != null && product.Category != TokenExchangeTab.Tickets && star <= (int)product.Rank;
        }
        _name.text = selected?.Name ?? "판매 상품이 없습니다";
        _description.text = _requestError ?? selected?.Description ?? "다음 진열을 기다려 주세요.";
        _quantity.text = selected == null ? "" : "교환 수량  " + selected.Quantity;
        _price.text = selected?.Price.ToString("N0") ?? "";
        _art.sprite = selected?.Sprite; _art.enabled = _art.sprite != null;
        _buy.interactable = !locked && selected != null && selected.CanPurchase && !selected.SoldOut;
        _buyLabel.text = _requesting || _snapshot.Busy ? "확인 중…" : "교환하기";
        _refresh.interactable = !locked && _refreshCatalog != null && _snapshot.CanRefresh;
        _refreshLabel.text = "남은 새로고침 " + _snapshot.RemainingRefreshes + "/" + _snapshot.RefreshLimit;
    }

    private ProductCard CreateCard(int index)
    {
        var frame = E.Icon("Product Slot " + index, _content, _theme.NormalProductFrame,
            (index % 3) * 202, (index / 3) * 265, 188, 188);
        frame.raycastTarget = true;
        var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = frame;
        button.onClick.AddListener(() =>
        {
            if (_entering || _requesting || index >= _cards.Count || _cards[index].Id == null) return;
            _selectedId = _cards[index].Id; _requestError = null; RebuildCards();
        });
        var card = new ProductCard
        {
            Button = button, Frame = frame,
            Artwork = E.Icon("Artwork", frame.transform, null, 40, 35, 105, 90),
            Name = E.Label("Product Name", frame.transform, _font, "", 34, 144, 120, 24, 20, _theme.Ink),
            Price = E.Label("Price", frame.transform, _font, "", 54, 193, 79, 31, 24, _theme.Cream, TextAlignmentOptions.Left),
            Token = E.Icon("Panda Token Price", frame.transform, _theme.PandaToken, 21, 194, 28, 28),
            Availability = E.Label("Availability", frame.transform, _font, "", 134, 201, 55, 21, 14,
                _theme.Cream, TextAlignmentOptions.Right),
            Stars = new Image[5]
        };
        for (int i = 0; i < card.Stars.Length; i++)
            card.Stars[i] = E.Icon("Rank Star " + (i + 1), frame.transform, _theme.Star, 44 + i * 20, 122, 20, 20);
        card.Name.textWrappingMode = TextWrappingModes.NoWrap;
        card.Name.overflowMode = TextOverflowModes.Ellipsis;
        card.Stamp = E.Rect("SOLD OUT Stamp", frame.transform, 94, 92, 170, 56);
        card.Stamp.pivot = Vector2.one * .5f;
        card.Stamp.localRotation = Quaternion.Euler(0, 0, 12);
        card.StampGroup = card.Stamp.gameObject.AddComponent<CanvasGroup>();
        card.StampGroup.blocksRaycasts = false; card.StampGroup.interactable = false;
        var stampInk = new Color32(186, 42, 32, 255);
        E.Panel("Stamp Paper", card.Stamp, 0, 0, 170, 56, new Color(1,1,1,.9f), 3);
        E.Panel("Top Ink", card.Stamp, 0, 0, 170, 4, stampInk, 0);
        E.Panel("Bottom Ink", card.Stamp, 0, 52, 170, 4, stampInk, 0);
        E.Panel("Left Ink", card.Stamp, 0, 0, 4, 56, stampInk, 0);
        E.Panel("Right Ink", card.Stamp, 166, 0, 4, 56, stampInk, 0);
        E.Label("SOLD OUT", card.Stamp, _theme.SoldOutFont, "SOLD OUT", 6, 5, 158, 46, 32, stampInk);
        card.Stamp.gameObject.SetActive(false);
        return card;
    }

    private static void UpdateStamp(ProductCard card, float now)
    {
        if (!card.SoldOut) return;
        float t = card.StampStarted < 0 ? 1 : Mathf.Clamp01((now - card.StampStarted) / .3f);
        float scale = t < .65f ? Mathf.Lerp(1.7f, .94f, t / .65f) : Mathf.Lerp(.94f, 1, (t - .65f) / .35f);
        card.Stamp.localScale = Vector3.one * scale;
        card.StampGroup.alpha = Mathf.Clamp01(t * 5);
    }

    private static string CompactAvailability(TokenExchangeProductView product)
    {
        if (product.CanPurchase || product.SoldOut) return "";
        string status = product.Status ?? "";
        if (status.Contains("부족")) return "잔액부족";
        if (status.Contains("보유") || status.Contains("해금")) return "보유중";
        return "교환불가";
    }

    private void RequestPurchase()
    {
        if (_entering || _requesting || _snapshot == null || _snapshot.Busy) return;
        TokenExchangeProductView selected = _snapshot.Products.FirstOrDefault(p => p.Id == _selectedId);
        if (selected == null || !selected.CanPurchase || selected.SoldOut) return;
        BeginRequest(reply => _purchase(selected.Id, selected.DisplayVersion, reply), "교환이 완료되었습니다!");
    }

    private void RequestRefresh()
    {
        if (_entering || _requesting || _snapshot == null || _snapshot.Busy || _refreshCatalog == null ||
            !_snapshot.CanRefresh) return;
        BeginRequest(reply => _refreshCatalog(TokenExchangeTab.Mixed, _snapshot.DisplayVersion, reply), "새 진열이 준비되었습니다!");
    }

    private void BeginRequest(Action<Action<bool, string>> request, string successText)
    {
        _requestError = null; _requesting = true; int version = ++_requestVersion; RebuildCards(); bool replied = false;
        try
        {
            request((success, message) =>
            {
                if (this == null || replied || version != _requestVersion) return;
                replied = true; _requesting = false;
                _requestError = success ? null : string.IsNullOrEmpty(message) ? "요청을 완료할 수 없습니다." : message;
                Refresh();
            });
        }
        catch (Exception exception)
        {
            if (version != _requestVersion) return;
            _requesting = false; _requestError = exception.Message; Refresh();
        }
    }

    /// <summary>Runtime and the isolated editor preview share the same unscaled presentation clock.</summary>
    public void TickPresentation(float now)
    {
        if (!IsOpen) return;
        Fit();
        if (_entering)
        {
            float t = Mathf.Clamp01((now - _openedAt) / Mathf.Clamp(_theme.EntranceSeconds, .6f, .9f));
            float y, angle;
            if (t < .58f) { float fall = t / .58f; y = Mathf.Lerp(1250, -14, fall * fall); angle = 0; }
            else
            {
                float settle = (t - .58f) / .42f;
                y = -14 * Mathf.Exp(-5 * settle) * Mathf.Cos(settle * Mathf.PI * 3);
                angle = 2.2f * Mathf.Sin(settle * Mathf.PI * 4) * Mathf.Exp(-4 * settle);
            }
            _board.anchoredPosition = new Vector2(0, t >= 1 ? 0 : y);
            _board.localRotation = Quaternion.Euler(0, 0, t >= 1 ? 0 : angle);
            if (t >= 1) { _entering = false; RebuildCards(); }
        }
        if (now >= _refreshAt) { _refreshAt = now + .5f; Refresh(); }
        foreach (var card in _cards) UpdateStamp(card, now);
    }

    private void Update() { if (Application.isPlaying) TickPresentation(Time.realtimeSinceStartup); }
    private void Fit()
    {
        if (_board == null) return;
        Vector2 size = ((RectTransform)transform).rect.size;
        float scale = Mathf.Min(size.x * .94f / 1650f, size.y * .94f / 925f);
        _board.localScale = _balanceFrame.localScale = Vector3.one * scale;
        // Reserve the currency rail when fitting the complete composition to the viewport.
        _board.anchorMin = _board.anchorMax = new Vector2(.5f - 135f * scale / Mathf.Max(1, size.x), .5f);
        _balanceFrame.anchoredPosition = new Vector2(575f, 262.5f) * scale;
    }
}
