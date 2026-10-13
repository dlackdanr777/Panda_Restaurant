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

        try
        {
            GameObject prefab = TokenExchangeInitialAssetAuthoring.CreateViewPrefab();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("Token Exchange 프리팹 생성 완료: " + PrefabPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Token Exchange Prefab", exception.Message, "확인");
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
        // The saved prefab already owns these layouts. Keep this old menu as a safe locator,
        // not as an operation that resets a designer's artwork, text, stars or price settings.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Token Exchange Prefab", "먼저 Create Token Exchange Prefab 메뉴로 최초 프리팹을 생성하세요.", "확인");
            return;
        }
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("가격 레이아웃은 프리팹의 LayoutGroup, ContentSizeFitter, LayoutElement에서 편집합니다. 기존 편집값은 변경하지 않았습니다.");
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
