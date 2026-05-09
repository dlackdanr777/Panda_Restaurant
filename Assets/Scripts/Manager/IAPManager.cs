using BackEnd;
using Muks.BackEnd;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// Unity IAP v5 + 뒤끝 영수증 검증 매니저
///
/// [구매 흐름]
/// 1. UIPaymentSlot 버튼 클릭
/// 2. IAPManager.BuyProduct(productId, onSuccess) 호출
/// 3. Google Play 결제창
/// 4. OnPurchasePending 콜백 → 뒤끝 서버 영수증 검증
/// 5. 검증 성공 → onSuccess(diaAmount) → UIPayment.AddDia()
///
/// [에디터 테스트]
/// - 에디터에서 버튼 클릭 시 실제 결제 없이 즉시 다이아 지급
///
/// [사전 설정]
/// - Google Play Console에 상품 ID 등록 (소비성 상품)
/// - 뒤끝 콘솔 > 결제 > 구글 영수증 검증 활성화
/// </summary>
public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    // ─────────────────────────────────────────
    // Google Play 상품 ID → 다이아 수량 매핑
    // ─────────────────────────────────────────
    public static readonly Dictionary<string, int> ProductDiaMap = new Dictionary<string, int>
    {
        { "dia_12",  12  },   // ₩1,100
        { "dia_65",  65  },   // ₩5,500
        { "dia_140", 140 },   // ₩11,000
        { "dia_450", 450 },   // ₩33,000
        { "dia_800", 800 },   // ₩55,000
    };

    private StoreController _storeController;
    private Action<int> _onPurchaseSuccess;

    public bool IsInitialized { get; private set; }

    // ─────────────────────────────────────────
    #region 초기화

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
#if !UNITY_EDITOR
        await InitializeIAP();
#else
        Debug.Log("[IAPManager] 에디터 모드: IAP 초기화 생략");
        IsInitialized = true;
        await Task.CompletedTask;
#endif
    }

    private async Task InitializeIAP()
    {
        _storeController = UnityIAPServices.StoreController();

        // 구매 이벤트 핸들러 등록
        _storeController.OnPurchasePending += OnPurchasePending;
        _storeController.OnPurchaseFailed  += OnPurchaseFailed;

        // 스토어 연결
        await _storeController.Connect();

        // 상품 목록 등록
        _storeController.OnProductsFetched     += OnProductsFetched;
        _storeController.OnProductsFetchFailed += OnProductsFetchFailed;

        var products = new List<ProductDefinition>();
        foreach (var kv in ProductDiaMap)
            products.Add(new ProductDefinition(kv.Key, ProductType.Consumable));

        _storeController.FetchProducts(products);
    }

    private void OnProductsFetched(List<Product> products)
    {
        IsInitialized = true;
        _storeController.FetchPurchases();
        Debug.Log($"[IAPManager] IAP 초기화 완료 (상품 {products.Count}개)");
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogError($"[IAPManager] 상품 목록 로드 실패: {failure}");
    }

    #endregion

    // ─────────────────────────────────────────
    #region 구매 요청

    /// <summary>특정 상품 구매를 시작합니다. 검증 성공 시 onSuccess(diaAmount)가 호출됩니다.</summary>
    public void BuyProduct(string productId, Action<int> onSuccess)
    {
#if UNITY_EDITOR
        // ── 에디터 테스트 모드 ─────────────────────────────────
        // 실제 결제 없이 즉시 다이아 지급
        if (ProductDiaMap.TryGetValue(productId, out int testDia))
        {
            Debug.Log($"[IAPManager][에디터] 테스트 구매: {productId} → 다이아 {testDia}");
            onSuccess?.Invoke(testDia);
        }
        else
        {
            Debug.LogError($"[IAPManager][에디터] 알 수 없는 상품 ID: {productId}");
        }
#else
        if (!IsInitialized)
        {
            Debug.LogError("[IAPManager] IAP가 초기화되지 않았습니다.");
            PopupManager.Instance?.ShowDisplayText("결제 서비스를 불러오는 중입니다. 잠시 후 다시 시도해주세요.");
            return;
        }

        _onPurchaseSuccess = onSuccess;
        _storeController.Purchase(productId);
        Debug.Log($"[IAPManager] 구매 요청: {productId}");
#endif
    }

    #endregion

    // ─────────────────────────────────────────
    #region 구매 결과 처리

    private void OnPurchasePending(PendingOrder pendingOrder)
    {
        var purchasedInfo = pendingOrder.Info.PurchasedProductInfo;
        if (purchasedInfo == null || purchasedInfo.Count == 0)
        {
            Debug.LogError("[IAPManager] PurchasedProductInfo가 비어있습니다.");
            _storeController.ConfirmPurchase(pendingOrder);
            return;
        }

        string productId = purchasedInfo[0].productId;
        string receipt   = pendingOrder.Info.Receipt;

        Debug.Log($"[IAPManager] 구매 완료, 영수증 검증 시작: {productId}");
        ValidateWithBackend(productId, receipt, pendingOrder);
    }

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        Debug.LogError($"[IAPManager] 구매 실패: {failedOrder.FailureReason}");
        _onPurchaseSuccess = null;
    }

    #endregion

    // ─────────────────────────────────────────
    #region 뒤끝 영수증 검증

    private void ValidateWithBackend(string productId, string receipt, PendingOrder pendingOrder)
    {
        BackendManager.Instance.ProcessBackendAPI(
            "구글 영수증 검증",
            callback => Backend.Receipt.IsValidateGooglePurchase(receipt, productId, bro => callback(bro)),
            bro =>
            {
                Debug.Log($"[IAPManager] 뒤끝 영수증 검증 성공: {productId}");

                if (ProductDiaMap.TryGetValue(productId, out int diaAmount))
                {
                    _onPurchaseSuccess?.Invoke(diaAmount);
                    Debug.Log($"[IAPManager] 다이아 {diaAmount} 지급");
                }
                else
                {
                    Debug.LogError($"[IAPManager] 다이아 매핑 없음: {productId}");
                }

                _onPurchaseSuccess = null;
                _storeController.ConfirmPurchase(pendingOrder);
            },
            state =>
            {
                Debug.LogError($"[IAPManager] 뒤끝 영수증 검증 실패: {state}");
                PopupManager.Instance?.ShowDisplayText("영수증 검증에 실패했습니다. 고객센터에 문의해주세요.");
                _onPurchaseSuccess = null;
                // 검증 실패해도 confirm 해야 pending 상태가 해제됩니다
                _storeController.ConfirmPurchase(pendingOrder);
            },
            maxRetries: 3,
            usePopup: false
        );
    }

    #endregion

    // ─────────────────────────────────────────
    #region 에디터 영수증 검증 테스트

#if UNITY_EDITOR
    [Header("에디터 영수증 검증 테스트")]
    [Tooltip("실기기에서 로그로 출력된 영수증 JSON 붙여넣기")]
    [SerializeField, TextArea(3, 8)] private string _testReceipt;
    [SerializeField] private string _testProductId = "dia_12";

    [ContextMenu("영수증 검증 테스트 실행")]
    private void EditorTestValidate()
    {
        if (string.IsNullOrEmpty(_testReceipt))
        {
            Debug.LogError("[IAPManager][에디터] _testReceipt가 비어있습니다. Inspector에 영수증 JSON을 붙여넣으세요.");
            return;
        }

        Debug.Log($"[IAPManager][에디터] 영수증 검증 테스트 시작: {_testProductId}");

        BackendManager.Instance.ProcessBackendAPI(
            "에디터 영수증 검증 테스트",
            callback => Backend.Receipt.IsValidateGooglePurchase(_testReceipt, _testProductId, bro => callback(bro)),
            bro =>
            {
                Debug.Log($"[IAPManager][에디터] ✅ 검증 성공! productId={_testProductId}");
                if (ProductDiaMap.TryGetValue(_testProductId, out int dia))
                    Debug.Log($"[IAPManager][에디터] 지급될 다이아: {dia}");
            },
            state =>
            {
                Debug.LogError($"[IAPManager][에디터] ❌ 검증 실패: {state}");
            },
            maxRetries: 1,
            usePopup: false
        );
    }
#endif

    #endregion
}