using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

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
    public const string PrefabResourcePath = "UI/GachaCollection/TokenExchangeView";

    [SerializeField] private GachaCollectionUiTheme _theme;
    private Func<TokenExchangeSnapshot> _read;
    private Action<string, int, Action<bool, string>> _purchase;
    private Action<TokenExchangeTab, int, Action<bool, string>> _refreshCatalog;
    private Action _closed;
    [SerializeField] private RectTransform _board, _content, _balanceFrame, _priceRow, _priceTokenRow;
    [SerializeField] private TextMeshProUGUI _balance, _name, _description, _quantity, _price, _buyLabel, _refreshLabel;
    [SerializeField] private Image _priceToken, _art;
    [SerializeField] private Button _closeButton, _buy, _refresh;
    private string _requestError;
    [SerializeField] private List<ProductCard> _cards = new List<ProductCard>();

    [Header("State appearance (multiplied by the authored frame color)")]
    [SerializeField] private Color _normalTint = Color.white;
    [SerializeField] private Color _selectedTint = new Color32(255, 234, 173, 255);
    [SerializeField] private Color _emptyTint = new Color(1, 1, 1, .4f);
    [Header("Status text")]
    [SerializeField] private string _buyText = "교환하기", _busyText = "확인 중…";
    [SerializeField] private string _emptySlotText = "판매 상품 없음", _emptySelectionText = "판매 상품이 없습니다";
    [SerializeField] private string _emptyDescriptionText = "다음 진열을 기다려 주세요.";
    [SerializeField] private string _quantityPrefix = "교환 수량  ", _refreshPrefix = "남은 새로고침 ";
    [SerializeField] private string _insufficientText = "잔액부족", _ownedText = "보유중", _unavailableText = "교환불가";
    [SerializeField] private string _requestFailedText = "요청을 완료할 수 없습니다.";
    [Header("Presentation (relative to the saved transforms)")]
    [SerializeField] private float _entranceHeight = 1250f, _entranceOvershoot = 14f, _entranceSwing = 2.2f;
    [SerializeField, Min(0)] private float _stampSeconds = .3f;
    [SerializeField] private float _stampStartScale = 1.7f, _stampImpactScale = .94f;

    [Serializable]
    private sealed class ProductCard
    {
        [NonSerialized] public string Id;
        public Button Button;
        public RectTransform PriceGroup;
        public Image Frame, Artwork, Token;
        public Image[] Stars;
        public TextMeshProUGUI Name, Price, Availability;
        public RectTransform Stamp;
        public CanvasGroup StampGroup;
        [NonSerialized] public string OfferKey;
        [NonSerialized] public bool SoldOut;
        [NonSerialized] public bool StampSoundPending;
        [NonSerialized] public float StampStarted = -1;
        [NonSerialized] public Vector3 BaseStampScale;
        [NonSerialized] public Quaternion BaseStampRotation;
        [NonSerialized] public float BaseStampAlpha;
        [NonSerialized] public Color BaseFrameColor;
    }
    private TokenExchangeSnapshot _snapshot;
    private string _selectedId;
    private bool _requesting, _entering;
    private int _requestVersion;
    private float _refreshAt, _openedAt;
    private readonly HashSet<string> _confirmedStampSounds = new HashSet<string>();
    private readonly object _chainAudioOwner = new object(), _stampAudioOwner = new object();
    private readonly List<(Button button, UnityAction action)> _listeners = new List<(Button, UnityAction)>();
    private bool _posesCaptured;
    private Vector2 _boardPosition;
    private Vector3 _boardScale;
    private Quaternion _boardRotation;
    private TokenExchangeViewportFitter _viewportFitter;
    public bool IsConfigured { get; private set; }
    public string ConfigurationError { get; private set; }
    public GachaCollectionUiTheme Theme => _theme;
    public bool IsOpen => gameObject.activeSelf;
    public bool IsEntering => _entering;
    public string SelectedProductId => _selectedId;
    public RectTransform Board => _board;

    public static TokenExchangeView Attach(Transform parent, GachaCollectionUiTheme theme,
        Func<TokenExchangeSnapshot> snapshot, Action<string, Action<bool, string>> requestPurchase, Action closed = null)
        => Attach(parent, theme, snapshot, requestPurchase == null ? null :
            (Action<string, int, Action<bool, string>>)((id, version, reply) => requestPurchase(id, reply)), null, closed);

    // Compatibility adapter for previews and callers without a scene reference. Never builds UI.
    public static TokenExchangeView Attach(Transform parent, GachaCollectionUiTheme theme,
        Func<TokenExchangeSnapshot> snapshot, Action<string, int, Action<bool, string>> requestPurchase,
        Action<TokenExchangeTab, int, Action<bool, string>> requestRefresh, Action closed = null)
    {
        if (parent == null) { Debug.LogError("TokenExchangeView: parent is missing."); return null; }
        var existing = parent.GetComponentsInChildren<TokenExchangeView>(true);
        if (existing.Length > 1)
        { Debug.LogError("TokenExchangeView: multiple exchange views are connected beneath " + parent.name, parent); return null; }
        TokenExchangeView view;
        bool created = existing.Length == 0;
        if (!created) view = existing[0];
        else
        {
            var prefab = Resources.Load<TokenExchangeView>(PrefabResourcePath);
            if (prefab == null)
            { Debug.LogError("TokenExchangeView: missing prefab Resources/" + PrefabResourcePath); return null; }
            if (!prefab.ValidateLayout(out string error))
            { Debug.LogError("TokenExchangeView: invalid prefab Resources/" + PrefabResourcePath + ": " + error, prefab); return null; }
            view = Instantiate(prefab, parent, false);
            view.gameObject.name = "Token Exchange";
        }
        if (view.Bind(theme, snapshot, requestPurchase, requestRefresh, closed)) return view;
        if (created)
        {
            if (Application.isPlaying) Destroy(view.gameObject);
            else DestroyImmediate(view.gameObject);
        }
        return null;
    }

    // Check the references the view actually uses, independent of names and layout components.
    public bool ValidateLayout(out string error)
    {
        var errors = new List<string>();
        var used = new HashSet<Component>();
        var fitter = GetComponent<TokenExchangeViewportFitter>();
        if (fitter != null && !fitter.ValidateLayout(out string fitError)) errors.Add(fitError);
        void Check(Component value, string field, Transform owner)
        {
            if (value == null) { errors.Add(field + " is missing"); return; }
            if (owner == null || !value.transform.IsChildOf(owner)) errors.Add(field + " belongs to another object hierarchy");
            if (!used.Add(value)) errors.Add(field + " is a duplicate reference");
        }
        Check(_board, nameof(_board), transform); Check(_content, nameof(_content), _board);
        Check(_balanceFrame, nameof(_balanceFrame), transform);
        Check(_priceRow, nameof(_priceRow), _board); Check(_priceTokenRow, nameof(_priceTokenRow), _priceRow);
        Check(_balance, nameof(_balance), _balanceFrame); Check(_name, nameof(_name), _board);
        Check(_description, nameof(_description), _board); Check(_quantity, nameof(_quantity), _board);
        Check(_price, nameof(_price), _priceTokenRow); Check(_priceToken, nameof(_priceToken), _priceTokenRow);
        Check(_art, nameof(_art), _board); Check(_buy, nameof(_buy), _board);
        Check(_buyLabel, nameof(_buyLabel), _buy != null ? _buy.transform : null);
        Check(_refreshLabel, nameof(_refreshLabel), _board); Check(_closeButton, nameof(_closeButton), _board);
        Check(_refresh, nameof(_refresh), _board);
        if (_theme == null) errors.Add("_theme is missing");
        else if (_theme.NormalProductFrame == null || _theme.RareProductFrame == null ||
                 _theme.UniqueProductFrame == null || _theme.SpecialProductFrame == null)
            errors.Add("_theme product frame references are missing");
        if (_cards == null || _cards.Count != 6) errors.Add("_cards must contain the six display slots");
        if (_cards != null)
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i]; string path = "_cards[" + i + "]";
                if (card == null) { errors.Add(path + " is missing"); continue; }
                Check(card.Button, path + ".Button", _content);
                var owner = card.Button != null ? card.Button.transform : null;
                Check(card.Frame, path + ".Frame", owner); Check(card.Artwork, path + ".Artwork", owner);
                Check(card.Token, path + ".Token", owner); Check(card.PriceGroup, path + ".PriceGroup", owner);
                Check(card.Name, path + ".Name", owner); Check(card.Price, path + ".Price", owner);
                Check(card.Availability, path + ".Availability", owner); Check(card.Stamp, path + ".Stamp", owner);
                Check(card.StampGroup, path + ".StampGroup", card.Stamp);
                if (card.Stars == null || card.Stars.Length != 5) errors.Add(path + ".Stars must contain five images");
                if (card.Stars != null)
                    for (int j = 0; j < card.Stars.Length; j++) Check(card.Stars[j], path + ".Stars[" + j + "]", owner);
            }
        error = string.Join("; ", errors);
        return errors.Count == 0;
    }

    public bool Bind(GachaCollectionUiTheme theme, Func<TokenExchangeSnapshot> snapshot,
        Action<string, int, Action<bool, string>> requestPurchase,
        Action<TokenExchangeTab, int, Action<bool, string>> requestRefresh, Action closed = null)
    {
        // A service rebind must never capture a partially animated pose as the authored pose.
        ResetSession();
        UnbindCallbacks();
        if (theme != null) _theme = theme;
        if (!ValidateLayout(out string error)) return RejectConfiguration(error);
        if (snapshot == null || requestPurchase == null) return RejectConfiguration("snapshot or purchase callback is missing");
        CaptureAuthoredPoses();
        _read = snapshot; _purchase = requestPurchase; _refreshCatalog = requestRefresh; _closed = closed;
        _viewportFitter = GetComponent<TokenExchangeViewportFitter>();
        IsConfigured = true; ConfigurationError = null;
        BindCallbacks();
        var sound = SoundManager.TryGetExistingInstance();
        if (sound != null)
        { sound.PreloadAudioClip(_theme.ExchangeChainSound); sound.PreloadAudioClip(_theme.SoldOutStampSound); }
        return true;
    }

    private bool RejectConfiguration(string error)
    {
        IsConfigured = false;
        UnbindCallbacks();
        SetVisible(false);
        _read = null; _purchase = null; _refreshCatalog = null;
        if (ConfigurationError != error)
            Debug.LogError("TokenExchangeView '" + name + "' (Resources/" + PrefabResourcePath + "): " + error, this);
        ConfigurationError = error;
        return false;
    }

    private void CaptureAuthoredPoses()
    {
        if (_posesCaptured) return;
        _boardPosition = _board.anchoredPosition; _boardRotation = _board.localRotation; _boardScale = _board.localScale;
        foreach (var card in _cards)
        {
            card.BaseStampScale = card.Stamp.localScale; card.BaseStampRotation = card.Stamp.localRotation;
            card.BaseStampAlpha = card.StampGroup.alpha; card.BaseFrameColor = card.Frame.color;
        }
        _posesCaptured = true;
    }

    private void RestoreAuthoredPoses()
    {
        if (!_posesCaptured) return;
        if (_board != null && _board.IsChildOf(transform))
        { _board.anchoredPosition = _boardPosition; _board.localRotation = _boardRotation; _board.localScale = _boardScale; }
        if (_cards != null) foreach (var card in _cards)
        {
            if (card == null) continue;
            if (card.Stamp != null && card.Stamp.IsChildOf(transform))
            { card.Stamp.localScale = card.BaseStampScale; card.Stamp.localRotation = card.BaseStampRotation; }
            if (card.StampGroup != null && card.StampGroup.transform.IsChildOf(transform)) card.StampGroup.alpha = card.BaseStampAlpha;
            card.StampStarted = -1;
        }
    }

    private void BindCallbacks()
    {
        void Listen(Button button, UnityAction action)
        { button.onClick.AddListener(action); _listeners.Add((button, action)); }
        GachaButtonInputSound.Bind(_closeButton, SoundEffectType.ButtonExitSound);
        GachaButtonInputSound.Bind(_buy); GachaButtonInputSound.Bind(_refresh);
        Listen(_closeButton, () => SetVisible(false)); Listen(_buy, RequestPurchase); Listen(_refresh, RequestRefresh);
        for (int i = 0; i < _cards.Count; i++)
        {
            int index = i;
            GachaButtonInputSound.Bind(_cards[index].Button, canPlay: () => index < _cards.Count && !_cards[index].SoldOut);
            Listen(_cards[index].Button, () => SelectCard(index));
        }
    }

    private void UnbindCallbacks()
    {
        foreach (var listener in _listeners)
            if (listener.button != null) listener.button.onClick.RemoveListener(listener.action);
        _listeners.Clear();
    }

    public bool SetVisible(bool visible)
    {
        if (!visible)
        {
            bool wasOpen = IsOpen;
            _entering = false; RestoreAuthoredPoses();
            gameObject.SetActive(false);
            if (wasOpen) _closed?.Invoke();
            return true;
        }
        if (!IsConfigured) return false;
        if (!ValidateLayout(out string error)) return RejectConfiguration(error);
        if (IsOpen) return true;
        _openedAt = Time.realtimeSinceStartup; _refreshAt = 0; _entering = true;
        gameObject.SetActive(true); transform.SetAsLastSibling(); Refresh();
        TickPresentation(_openedAt);
        SoundManager.TryGetExistingInstance()?.PlayPresentationEffect(EffectType.None,
            _theme.ExchangeChainSound, _theme.ExchangeChainVolume, owner: _chainAudioOwner, maxConcurrent: 1);
        return true;
    }
    /// <summary>A service replacement invalidates view callbacks without cancelling an in-flight save.</summary>
    public void ResetSession()
    {
        ++_requestVersion; _requesting = false; _snapshot = null; _selectedId = null; _refreshAt = 0; _requestError = null;
        if (_cards != null) foreach (var card in _cards)
        {
            if (card == null) continue;
            card.OfferKey = null; card.SoldOut = false; card.StampStarted = -1;
            if (card.Stamp != null && card.Stamp.IsChildOf(transform)) card.Stamp.gameObject.SetActive(false);
        }
        SetVisible(false);
    }

    public void SelectProduct(string id)
    {
        if (_requesting || _entering || _snapshot == null || !_snapshot.Products.Any(p => p.Id == id)) return;
        _selectedId = id; _requestError = null; RebuildCards();
    }

    public void Refresh()
    {
        if (!IsConfigured || _read == null) return;
        _snapshot = _read() ?? new TokenExchangeSnapshot { Busy = true };
        _balance.text = _snapshot.PandaTokens.ToString("N0");
        RebuildCards();
    }

    private void RebuildCards()
    {
        if (_snapshot == null) return;
        var products = (_snapshot.Products ?? Array.Empty<TokenExchangeProductView>()).Take(6).ToList();
        var selected = products.FirstOrDefault(p => p.Id == _selectedId) ?? products.FirstOrDefault();
        _selectedId = selected?.Id;
        bool locked = _entering || _requesting || _snapshot.Busy;
        for (int i = 0; i < _cards.Count; i++)
        {
            ProductCard card = _cards[i];
            card.Button.gameObject.SetActive(true);
            TokenExchangeProductView product = i < products.Count ? products[i] : null;
            card.Id = product?.Id;
            card.Button.name = product == null ? "Unavailable Slot " + i : "Product " + product.Id;
            card.Button.interactable = !_entering && !_requesting && product != null;
            card.Frame.sprite = _theme.ProductFrame(product?.Rank ?? Rank.Normal1);
            card.Frame.color = card.BaseFrameColor * (product == null ? _emptyTint :
                product.Id == _selectedId ? _selectedTint : _normalTint);
            card.Artwork.sprite = product?.Sprite; card.Artwork.enabled = card.Artwork.sprite != null;
            card.Name.text = product?.Name ?? _emptySlotText;
            card.Price.text = product?.Price.ToString("N0") ?? "";
            card.Token.enabled = product != null;
            card.Availability.text = product == null ? "" : CompactAvailability(product);
            string offerKey = product == null ? null : product.DisplayVersion + ":" + product.Id;
            bool soldOut = product?.SoldOut == true;
            bool visible = IsOpen && gameObject.activeInHierarchy;
            bool animate = visible && offerKey == card.OfferKey && soldOut && !card.SoldOut;
            if (offerKey != card.OfferKey || !soldOut || !visible) card.StampStarted = -1;
            if (animate) card.StampStarted = Time.realtimeSinceStartup;
            if (offerKey != card.OfferKey || !soldOut) card.StampSoundPending = false;
            // The service's successful save callback is the only source of this token.
            // A retained/restored SOLD OUT snapshot by itself cannot produce sound.
            if (soldOut && card.StampStarted >= 0 && _confirmedStampSounds.Remove(offerKey))
                card.StampSoundPending = true;
            card.OfferKey = offerKey; card.SoldOut = soldOut;
            card.Stamp.gameObject.SetActive(soldOut);
            UpdateStamp(card, Time.realtimeSinceStartup);
            for (int star = 0; star < card.Stars.Length; star++)
                card.Stars[star].enabled = product != null && product.Category != TokenExchangeTab.Tickets && star <= (int)product.Rank;
        }
        _name.text = selected?.Name ?? _emptySelectionText;
        _description.text = _requestError ?? selected?.Description ?? _emptyDescriptionText;
        _quantity.text = selected == null ? "" : _quantityPrefix + selected.Quantity;
        SetSelectedPrice(selected?.Price.ToString("N0"));
        _art.sprite = selected?.Sprite; _art.enabled = _art.sprite != null;
        _buy.interactable = !locked && selected != null && selected.CanPurchase && !selected.SoldOut;
        _buyLabel.text = _requesting || _snapshot.Busy ? _busyText : _buyText;
        _refresh.interactable = !locked && _refreshCatalog != null && _snapshot.CanRefresh;
        _refreshLabel.text = _refreshPrefix + _snapshot.RemainingRefreshes + "/" + _snapshot.RefreshLimit;
    }

    private void SetSelectedPrice(string value)
    {
        _price.text = value ?? "";
        _price.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_priceTokenRow);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_priceRow);
    }

    private void SelectCard(int index)
    {
        if (_entering || _requesting || _snapshot == null || index >= _cards.Count || _cards[index].Id == null)
            return;
        _selectedId = _cards[index].Id;
        _requestError = null;
        RebuildCards();
    }

    private void UpdateStamp(ProductCard card, float now)
    {
        if (!card.SoldOut) return;
        float t = card.StampStarted < 0 ? 1 : PresentationProgress(now - card.StampStarted, _stampSeconds);
        float scale = t < .65f ? Mathf.Lerp(_stampStartScale, _stampImpactScale, t / .65f) : Mathf.Lerp(_stampImpactScale, 1, (t - .65f) / .35f);
        card.Stamp.localScale = card.BaseStampScale * scale;
        card.Stamp.localRotation = card.BaseStampRotation;
        card.StampGroup.alpha = card.BaseStampAlpha * Mathf.Clamp01(t * 5);
        if (card.StampSoundPending && t >= .65f)
        {
            card.StampSoundPending = false;
            if (isActiveAndEnabled)
                SoundManager.TryGetExistingInstance()?.PlayPresentationEffect(EffectType.None,
                    _theme.SoldOutStampSound, _theme.SoldOutStampVolume, owner: _stampAudioOwner);
        }
    }

    private string CompactAvailability(TokenExchangeProductView product)
    {
        if (product.CanPurchase || product.SoldOut) return "";
        string status = product.Status ?? "";
        if (status.Contains("부족")) return _insufficientText;
        if (status.Contains("보유") || status.Contains("해금")) return _ownedText;
        return _unavailableText;
    }

    private void RequestPurchase()
    {
        if (!IsConfigured || _entering || _requesting || _snapshot == null || _snapshot.Busy) return;
        TokenExchangeProductView selected = _snapshot.Products.FirstOrDefault(p => p.Id == _selectedId);
        if (selected == null || !selected.CanPurchase || selected.SoldOut) return;
        string offer = selected.DisplayVersion + ":" + selected.Id;
        BeginRequest(reply => _purchase(selected.Id, selected.DisplayVersion, reply),
            () => { if (isActiveAndEnabled) _confirmedStampSounds.Add(offer); });
    }

    private void RequestRefresh()
    {
        if (_entering || _requesting || _snapshot == null || _snapshot.Busy || _refreshCatalog == null ||
            !_snapshot.CanRefresh) return;
        BeginRequest(reply => _refreshCatalog(TokenExchangeTab.Mixed, _snapshot.DisplayVersion, reply));
    }

    private void BeginRequest(Action<Action<bool, string>> request, Action confirmed = null)
    {
        _requestError = null; _requesting = true; int version = ++_requestVersion; RebuildCards(); bool replied = false;
        try
        {
            request((success, message) =>
            {
                if (this == null || replied || version != _requestVersion) return;
                replied = true; _requesting = false;
                if (success) confirmed?.Invoke();
                _requestError = success ? null : string.IsNullOrEmpty(message) ? _requestFailedText : message;
                Refresh();
            });
        }
        catch (Exception exception)
        {
            if (version != _requestVersion) return;
            _requesting = false; _requestError = exception.Message; Refresh();
        }
    }

    private void OnDisable()
    {
        _entering = false;
        RestoreAuthoredPoses();
        _confirmedStampSounds.Clear();
        if (_cards != null) foreach (var card in _cards) if (card != null) card.StampSoundPending = false;
        var sound = SoundManager.TryGetExistingInstance();
        if (sound != null)
        { sound.StopPresentationEffects(_chainAudioOwner); sound.StopPresentationEffects(_stampAudioOwner); }
    }

    private void OnDestroy() => UnbindCallbacks();

    private static float PresentationProgress(float elapsed, float seconds)
        => seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) ? 1 : Mathf.Clamp01(elapsed / seconds);

    /// <summary>Runtime and the isolated editor preview share the same unscaled presentation clock.</summary>
    public void TickPresentation(float now)
    {
        if (!IsConfigured || !IsOpen) return;
        _viewportFitter?.Refresh();
        if (_entering)
        {
            float t = PresentationProgress(now - _openedAt, _theme.EntranceSeconds);
            float y, angle;
            if (t < .58f) { float fall = t / .58f; y = Mathf.Lerp(_entranceHeight, -_entranceOvershoot, fall * fall); angle = 0; }
            else
            {
                float settle = (t - .58f) / .42f;
                y = -_entranceOvershoot * Mathf.Exp(-5 * settle) * Mathf.Cos(settle * Mathf.PI * 3);
                angle = _entranceSwing * Mathf.Sin(settle * Mathf.PI * 4) * Mathf.Exp(-4 * settle);
            }
            _board.anchoredPosition = _boardPosition + new Vector2(0, t >= 1 ? 0 : y);
            _board.localRotation = _boardRotation * Quaternion.Euler(0, 0, t >= 1 ? 0 : angle);
            if (t >= 1) { _entering = false; RebuildCards(); }
        }
        if (now >= _refreshAt) { _refreshAt = now + .5f; Refresh(); }
        foreach (var card in _cards) UpdateStamp(card, now);
    }

    private void Update() { if (Application.isPlaying) TickPresentation(Time.realtimeSinceStartup); }
}
