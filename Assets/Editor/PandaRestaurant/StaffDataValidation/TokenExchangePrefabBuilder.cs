using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class TokenExchangePrefabBuilder
{
    private const string MenuPath = "Tools/Panda Restaurant/Gacha Collection/Create Token Exchange Prefab";
    private const string UpgradeMenuPath = "Tools/Panda Restaurant/Gacha Collection/Apply Responsive Token Price Layout";
    private const string PaymentChoiceMenuPath = "Tools/Panda Restaurant/Gacha Collection/Create Payment Choice Prefab";
    private const string PaymentCostLayoutMenuPath = "Tools/Panda Restaurant/Gacha Collection/Apply Payment Cost Layout";
    private const string PrefabPath = "Assets/Resources/UI/GachaCollection/TokenExchangeView.prefab";
    private const string PaymentChoicePrefabPath = "Assets/Resources/UI/GachaCollection/GachaPaymentChoiceView.prefab";

    [MenuItem(MenuPath)]
    private static void CreatePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("Token Exchange 프리팹이 이미 있습니다. 기존 프리팹을 선택했습니다.");
            return;
        }

        GachaCollectionUiTheme theme = GachaCollectionUiTheme.Load();
        if (theme == null)
        {
            EditorUtility.DisplayDialog("Token Exchange Prefab", "GachaCollectionUiTheme 리소스를 찾을 수 없습니다.", "확인");
            return;
        }

        EnsureFolder("Assets/Resources/UI/GachaCollection");
        var host = new GameObject("Token Exchange Prefab Source", typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) host.layer = uiLayer;

        try
        {
            var parent = (RectTransform)host.transform;
            parent.sizeDelta = new Vector2(1920, 1080);
            TokenExchangeView view = TokenExchangeView.Attach(parent, theme,
                () => new TokenExchangeSnapshot(), (id, reply) => { });
            view.gameObject.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Token Exchange 프리팹을 저장하지 못했습니다.");

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Token Exchange 프리팹 생성 완료: " + PrefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Token Exchange Prefab", exception.Message, "확인");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [MenuItem(PaymentChoiceMenuPath)]
    private static void CreatePaymentChoicePrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PaymentChoicePrefabPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("Gacha Payment Choice 프리팹이 이미 있습니다. 기존 프리팹을 선택했습니다.");
            return;
        }

        GachaCollectionUiTheme theme = GachaCollectionUiTheme.Load();
        if (theme == null)
        {
            EditorUtility.DisplayDialog("Payment Choice Prefab", "GachaCollectionUiTheme 리소스를 찾을 수 없습니다.", "확인");
            return;
        }

        EnsureFolder("Assets/Resources/UI/GachaCollection");
        var host = new GameObject("Payment Choice Prefab Source", typeof(RectTransform));
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) host.layer = uiLayer;

        try
        {
            var parent = (RectTransform)host.transform;
            parent.sizeDelta = new Vector2(1920, 1080);
            GachaPaymentChoiceView view = GachaPaymentChoiceView.Attach(parent, theme);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(view.gameObject, PaymentChoicePrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Gacha Payment Choice 프리팹을 저장하지 못했습니다.");

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Gacha Payment Choice 프리팹 생성 완료: " + PaymentChoicePrefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Payment Choice Prefab", exception.Message, "확인");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    [MenuItem(PaymentCostLayoutMenuPath)]
    private static void ApplyPaymentCostLayout()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PaymentChoicePrefabPath))
        {
            EditorUtility.DisplayDialog("Payment Choice Prefab", "먼저 Create Payment Choice Prefab 메뉴로 프리팹을 생성하세요.", "확인");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PaymentChoicePrefabPath);
        try
        {
            var view = root.GetComponent<GachaPaymentChoiceView>();
            if (view == null) throw new InvalidOperationException("GachaPaymentChoiceView를 찾을 수 없습니다.");

            Transform diamondTransform = root.transform.Find("Payment Frame/Choose Diamonds/Diamond Cost");
            Transform ticketTransform = root.transform.Find("Payment Frame/Choose Machine Tickets/Ticket Cost");
            if (diamondTransform == null || ticketTransform == null)
                throw new InvalidOperationException("Diamond Cost 또는 Ticket Cost를 찾을 수 없습니다.");

            ConfigureRect((RectTransform)diamondTransform, new Vector2(.5f, .5f),
                new Vector2(11, 4.1949005f), new Vector2(72, 22.5556f));
            ConfigureRect((RectTransform)ticketTransform, new Vector2(.5f, .5f),
                new Vector2(11, 4.4450912f), new Vector2(72, 22.4578f));
            EditorUtility.SetDirty(view);
            if (PrefabUtility.SaveAsPrefabAsset(root, PaymentChoicePrefabPath) == null)
                throw new InvalidOperationException("결제 비용 레이아웃을 프리팹에 저장하지 못했습니다.");

            AssetDatabase.SaveAssets();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaymentChoicePrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Diamond Cost와 Ticket Cost 레이아웃을 프리팹에 적용했습니다.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Payment Choice Prefab", exception.Message, "확인");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem(UpgradeMenuPath)]
    private static void ApplyResponsiveTokenPriceLayout()
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            EditorUtility.DisplayDialog("Token Exchange Prefab", "먼저 Create Token Exchange Prefab 메뉴로 프리팹을 생성하세요.", "확인");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            TokenExchangeView view = root.GetComponent<TokenExchangeView>();
            Transform outerRow = root.transform.Find("Wooden Exchange Board/Selected Price Row");
            if (view == null || outerRow == null)
                throw new InvalidOperationException("Token Exchange 프리팹에서 가격 행을 찾을 수 없습니다.");

            Transform iconTransform = outerRow.Find("Price Panda Token");
            Transform priceTransform = outerRow.Find("Token Price");
            Transform tokenPriceRow = outerRow.Find("Token Price Row");
            if (tokenPriceRow == null)
            {
                var rowObject = new GameObject("Token Price Row", typeof(RectTransform));
                tokenPriceRow = rowObject.transform;
                tokenPriceRow.SetParent(outerRow, false);
            }

            if (iconTransform == null) iconTransform = tokenPriceRow.Find("Price Panda Token");
            if (priceTransform == null) priceTransform = tokenPriceRow.Find("Token Price");
            if (iconTransform == null || priceTransform == null)
                throw new InvalidOperationException("가격 행에 토큰 아이콘 또는 가격 텍스트가 없습니다.");

            iconTransform.SetParent(tokenPriceRow, false);
            priceTransform.SetParent(tokenPriceRow, false);
            ConfigureRect((RectTransform)tokenPriceRow, new Vector2(.5f, .5f), Vector2.zero, new Vector2(0, 40));
            ConfigureRect((RectTransform)iconTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(40, 40));
            ConfigureRect((RectTransform)priceTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(173, 38));

            HorizontalLayoutGroup outerLayout = GetOrAdd<HorizontalLayoutGroup>(outerRow.gameObject);
            outerLayout.spacing = 0;
            outerLayout.childAlignment = TextAnchor.MiddleCenter;
            outerLayout.childControlWidth = false;
            outerLayout.childControlHeight = false;
            outerLayout.childForceExpandWidth = false;
            outerLayout.childForceExpandHeight = false;

            HorizontalLayoutGroup innerLayout = GetOrAdd<HorizontalLayoutGroup>(tokenPriceRow.gameObject);
            innerLayout.spacing = 5;
            innerLayout.childAlignment = TextAnchor.MiddleCenter;
            innerLayout.childControlWidth = true;
            innerLayout.childControlHeight = false;
            innerLayout.childForceExpandWidth = false;
            innerLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(tokenPriceRow.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            LayoutElement tokenLayout = GetOrAdd<LayoutElement>(iconTransform.gameObject);
            tokenLayout.preferredWidth = 40;
            tokenLayout.preferredHeight = 40;
            tokenLayout.flexibleWidth = 0;
            tokenLayout.flexibleHeight = 0;

            TextMeshProUGUI price = priceTransform.GetComponent<TextMeshProUGUI>();
            if (price == null) throw new InvalidOperationException("Token Price에 TextMeshProUGUI가 없습니다.");
            price.alignment = TextAlignmentOptions.MidlineLeft;
            price.enableAutoSizing = false;
            price.textWrappingMode = TextWrappingModes.NoWrap;
            ContentSizeFitter textFitter = price.GetComponent<ContentSizeFitter>();
            if (textFitter != null) UnityEngine.Object.DestroyImmediate(textFitter);

            var serializedView = new SerializedObject(view);
            serializedView.FindProperty("_priceTokenRow").objectReferenceValue = tokenPriceRow;
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            UpgradeProductPriceGroups(root.transform, view);
            EditorUtility.SetDirty(view);
            if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                throw new InvalidOperationException("가격 레이아웃을 프리팹에 저장하지 못했습니다.");

            AssetDatabase.SaveAssets();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Token Price Row 레이아웃을 프리팹에 적용했습니다.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Token Exchange Prefab", exception.Message, "확인");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void UpgradeProductPriceGroups(Transform root, TokenExchangeView view)
    {
        Transform grid = root.Find("Wooden Exchange Board/Product Grid");
        if (grid == null) throw new InvalidOperationException("Product Grid를 찾을 수 없습니다.");

        SerializedObject serializedView = new SerializedObject(view);
        SerializedProperty cards = serializedView.FindProperty("_cards");
        int cardIndex = 0;
        foreach (Transform product in grid)
        {
            if (product.GetComponent<Button>() == null) continue;
            if (cardIndex >= cards.arraySize)
                throw new InvalidOperationException("상품 카드 참조 수가 프리팹 구조와 다릅니다.");

            Transform group = product.Find("Price Group");
            if (group == null)
            {
                var groupObject = new GameObject("Price Group", typeof(RectTransform));
                group = groupObject.transform;
                group.SetParent(product, false);
            }

            Transform token = product.Find("Panda Token Price") ?? group.Find("Panda Token Price");
            Transform priceTransform = product.Find("Price") ?? group.Find("Price");
            Transform availabilityTransform = product.Find("Availability") ?? group.Find("Availability");
            Transform artwork = product.Find("Artwork");
            if (token == null || priceTransform == null || availabilityTransform == null)
                throw new InvalidOperationException(product.name + "에 가격, 토큰 또는 상태 표시가 없습니다.");

            if (artwork is RectTransform artworkRect)
            {
                ConfigureRect(artworkRect, new Vector2(.5f, .5f), new Vector2(-1.5f, 11.7f), new Vector2(120, 100));
            }
            Transform productName = product.Find("Product Name");
            if (productName is RectTransform nameRect)
            {
                nameRect.anchorMin = nameRect.anchorMax = new Vector2(.5f, .5f);
                nameRect.pivot = new Vector2(.5f, .5f);
                nameRect.anchoredPosition = new Vector2(0, -58.2f);
                nameRect.sizeDelta = new Vector2(120, 24);
            }
            for (int starIndex = 0; starIndex < 5; starIndex++)
            {
                Transform star = product.Find("Rank Star " + (starIndex + 1));
                if (star is not RectTransform starRect) continue;
                starRect.anchorMin = starRect.anchorMax = new Vector2(.5f, .5f);
                starRect.pivot = new Vector2(.5f, 1f);
                starRect.anchoredPosition = new Vector2(-40 + starIndex * 20, -28);
                starRect.sizeDelta = new Vector2(20, 20);
            }

            token.SetParent(group, false);
            priceTransform.SetParent(group, false);
            availabilityTransform.SetParent(group, false);
            ConfigureRect((RectTransform)group, new Vector2(.5f, .5f), new Vector2(0, -114.55f), new Vector2(0, 39.7f));

            HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(group.gameObject);
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(group.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            LayoutElement tokenLayout = GetOrAdd<LayoutElement>(token.gameObject);
            tokenLayout.preferredWidth = tokenLayout.preferredHeight = 30;
            tokenLayout.flexibleWidth = tokenLayout.flexibleHeight = 0;
            TextMeshProUGUI price = priceTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI availability = availabilityTransform.GetComponent<TextMeshProUGUI>();
            if (price == null || availability == null)
                throw new InvalidOperationException(product.name + "의 가격 또는 상태 텍스트가 없습니다.");
            price.alignment = TextAlignmentOptions.Left;
            price.enableAutoSizing = true;
            price.fontSize = 20;
            price.fontSizeMin = 14;
            price.fontSizeMax = 20;
            price.textWrappingMode = TextWrappingModes.NoWrap;
            price.overflowMode = TextOverflowModes.Ellipsis;
            availability.textWrappingMode = TextWrappingModes.NoWrap;
            availability.alignment = TextAlignmentOptions.Right;

            cards.GetArrayElementAtIndex(cardIndex).FindPropertyRelative("PriceGroup").objectReferenceValue = group;
            cardIndex++;
        }

        if (cardIndex != cards.arraySize)
            throw new InvalidOperationException("모든 상품 카드에 Price Group을 연결하지 못했습니다.");
        serializedView.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }


    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int separator = path.LastIndexOf('/');
        if (separator > 0) EnsureFolder(path.Substring(0, separator));
        AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
    }
}