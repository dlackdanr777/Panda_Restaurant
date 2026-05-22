using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>?ㅻ걹 ?고렪(硫붿씪?? ?쒖뒪?쒖쓣 愿由ы븯???깃???留ㅻ땲?</summary>
public class MailManager : MonoBehaviour
{
    public static MailManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("MailManager");
                _instance = obj.AddComponent<MailManager>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    private static MailManager _instance;

    /// <summary>硫붿씪 紐⑸줉??媛깆떊?????몄텧?⑸땲??/summary>
    public event Action OnMailListRefreshed;

    /// <summary>誘몄닔??硫붿씪 議댁옱 ?щ?媛 諛붾????몄텧?⑸땲??(true = ?뚮엺 ?덉쓬)</summary>
    public event Action<bool> OnAlarmChanged;

    /// <summary>硫붿씪 ??嫄??섎졊 ?꾨즺 ???몄텧?⑸땲??/summary>
    public event Action<MailData> OnMailReceived;

    /// <summary>?꾩껜 硫붿씪 ?섎졊 ?꾨즺 ???몄텧?⑸땲??/summary>
    public event Action OnAllMailReceived;

    private const string HISTORY_TABLE = "MailHistory";

    private List<MailData> _mailList = new List<MailData>();
    public IReadOnlyList<MailData> MailList => _mailList;

    /// <summary>愿由ъ옄 ?고렪 以??섎졊 媛?ν븳(誘몄닔?뮤룸?留뚮즺) 媛쒖닔</summary>
    public int UnreceivedCount
    {
        get
        {
            int count = 0;
            foreach (var mail in _mailList)
                if (!mail.IsReceived && !mail.IsExpired) count++;
            return count;
        }
    }

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>硫붿씪 紐⑸줉 媛깆떊 + ?뚮엺 ?대깽?몃? ?④퍡 諛쒖깮?쒗궢?덈떎</summary>
    private void InvokeRefreshed()
    {
        OnMailListRefreshed?.Invoke();
        OnAlarmChanged?.Invoke(UnreceivedCount > 0);
    }

    // ?????????????????????????????????????????
    #region 硫붿씪 紐⑸줉 議고쉶 (愿由ъ옄 + 荑좏룿 ?숈떆 濡쒕뱶)

    /// <summary>愿由ъ옄 ?고렪怨?荑좏룿 ?고렪??紐⑤몢 鍮꾨룞湲곕줈 媛?몄샃?덈떎</summary>
    public void LoadMailListAsync(Action onSuccess = null, Action onFail = null)
    {
        _mailList.Clear();

        // Admin ??Coupon ???섎졊 ?대젰 ?쒖쑝濡?濡쒕뱶
        LoadByTypeAsync(PostType.Admin, () =>
        {
            LoadByTypeAsync(PostType.Coupon, () =>
            {
                LoadReceivedHistoryAsync(() =>
                {
                    onSuccess?.Invoke();
                    InvokeRefreshed();
                    Debug.Log($"[MailManager] ?꾩껜 硫붿씪 {_mailList.Count}嫄?濡쒕뱶 ?꾨즺 (誘몄닔?? {UnreceivedCount}嫄?");
                });
            },
            // 荑좏룿 ?ㅽ뙣?대룄 Admin + ?대젰 濡쒕뱶
            () =>
            {
                LoadReceivedHistoryAsync(() =>
                {
                    onSuccess?.Invoke();
                    InvokeRefreshed();
                });
            });
        }, onFail);
    }

    private void LoadReceivedHistoryAsync(Action onDone)
    {
        Where where = new Where();
        where.Equal("owner_inDate", Backend.UserInDate);

        BackendManager.Instance.ProcessBackendAPI(
            $"{HISTORY_TABLE} 議고쉶",
            callback => Backend.GameData.Get(HISTORY_TABLE, where, bro => callback(bro)),
            bro =>
            {
                try
                {
                    JsonData rows = bro.FlattenRows();
                    DateTime cutoff = DateTime.UtcNow.AddDays(-30);

                    for (int i = 0; i < rows.Count; i++)
                    {
                        JsonData row = rows[i];
                        string histInDate = row.ContainsKey("inDate") ? row["inDate"].ToString() : string.Empty;
                        string origInDate = row.ContainsKey("originalInDate") ? row["originalInDate"].ToString() : string.Empty;

                        // 30???댁긽 吏???대젰? GameData?먯꽌 ?쒓굅
                        if (row.ContainsKey("receivedAt") &&
                            DateTime.TryParse(row["receivedAt"].ToString(), out DateTime receivedAt) &&
                            receivedAt < cutoff)
                        {
                            if (!string.IsNullOrEmpty(histInDate))
                                BackendManager.Instance.ProcessBackendAPI(
                                    "?대젰 留뚮즺 ??젣",
                                    cb => Backend.GameData.DeleteV2(HISTORY_TABLE, histInDate, Backend.UserInDate, br => cb(br)),
                                    _ => Debug.Log($"[MailManager] 留뚮즺 ?대젰 ??젣: {histInDate}"),
                                    _ => { },
                                    maxRetries: 0, usePopup: false
                                );
                            continue;
                        }

                        // UPost 紐⑸줉???대? ?덈뒗 硫붿씪? 以묐났 異붽? ????
                        bool exists = false;
                        for (int j = 0; j < _mailList.Count; j++)
                            if (_mailList[j].InDate == origInDate) { exists = true; break; }

                        if (!exists)
                        {
                            MailData histMail = MailData.CreateFromHistory(histInDate, row);
                            _mailList.Add(histMail);
                        }
                    }
                    Debug.Log($"[MailManager] ?섎졊 ?대젰 濡쒕뱶 ?꾨즺 ({rows.Count}嫄?");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MailManager] ?섎졊 ?대젰 ?뚯떛 ?ㅻ쪟: {ex.Message}");
                }
                onDone?.Invoke();
            },
            state =>
            {
                // NotFoundException = ?뚯씠釉?誘몄깮???????앹뾽 ?놁씠 臾댁떆
                Debug.LogWarning($"[MailManager] ?섎졊 ?대젰 濡쒕뱶 ?ㅽ뙣(?뚯씠釉??놁쓣 ???덉쓬): {state}");
                onDone?.Invoke();
            },
            maxRetries: 0,
            usePopup: false
        );
    }

    private void SaveMailToHistory(MailData mail)
    {
        Param param = mail.ToHistoryParam();
        BackendManager.Instance.ProcessBackendAPI(
            "수령 이력 저장",
            callback => Backend.GameData.Insert(HISTORY_TABLE, param, bro => callback(bro)),
            bro =>
            {
                string histInDate = bro.GetInDate();
                mail.SetHistoryInDate(histInDate);
                Debug.Log($"[MailManager] ?섎졊 ?대젰 ????꾨즺: {mail.Title}");
            },
            state => Debug.LogWarning($"[MailManager] ?섎졊 ?대젰 ????ㅽ뙣: {state}"),
            maxRetries: 1,
            usePopup: false
        );
    }

    private void LoadByTypeAsync(PostType postType, Action onSuccess, Action onFail)
    {
        BackendManager.Instance.ProcessBackendAPI(
            $"{postType} ?고렪 紐⑸줉 議고쉶",
            callback => Backend.UPost.GetPostList(postType, 100, bro => callback(bro)),
            bro =>
            {
                ParseMailList(postType, bro);
                onSuccess?.Invoke();
            },
            state =>
            {
                Debug.LogError($"[MailManager] {postType} ?고렪 紐⑸줉 議고쉶 ?ㅽ뙣: {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: false
        );
    }

    private void ParseMailList(PostType postType, BackendReturnObject bro)
    {
        try
        {
            JsonData json = bro.GetReturnValuetoJSON()["postList"];
            for (int i = 0; i < json.Count; i++)
            {
                MailData mail = new MailData(postType, json[i]);
                if (!string.IsNullOrEmpty(mail.InDate))
                    _mailList.Add(mail);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailManager] {postType} ?고렪 ?뚯떛 ?ㅻ쪟: {ex.Message}");
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 硫붿씪 ?섎졊 (?④굔)

    /// <summary>?뱀젙 硫붿씪??蹂댁긽??鍮꾨룞湲곕줈 ?섎졊?⑸땲??/summary>
    public void ReceiveMailAsync(MailData mail, Action<MailData> onSuccess = null, Action onFail = null)
    {
        if (mail == null || mail.IsReceived || mail.IsExpired)
        {
            Debug.LogWarning("[MailManager] ?섎졊?????녿뒗 硫붿씪?낅땲??");
            onFail?.Invoke();
            return;
        }

        BackendManager.Instance.ProcessBackendAPI(
            "硫붿씪 ?섎졊",
            callback => Backend.UPost.ReceivePostItem(mail.PostType, mail.InDate, bro => callback(bro)),
            bro =>
            {
                mail.SetReceived();
                GiveRewardFromSingleReceive(bro);
                SaveMailToHistory(mail);
                onSuccess?.Invoke(mail);
                OnMailReceived?.Invoke(mail);                InvokeRefreshed();                Debug.Log($"[MailManager] 硫붿씪 ?섎졊 ?꾨즺: {mail.Title}");
            },
            state =>
            {
                Debug.LogError($"[MailManager] 硫붿씪 ?섎졊 ?ㅽ뙣: {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: true
        );
    }

    #endregion

    // ?????????????????????????????????????????
    #region 硫붿씪 ?꾩껜 ?섎졊

    /// <summary>愿由ъ옄 ?고렪 ?꾩껜瑜?鍮꾨룞湲곕줈 ?섎졊?⑸땲??/summary>
    public void ReceiveAllMailAsync(Action onSuccess = null, Action onFail = null)
    {
        ReceiveAllByTypeAsync(PostType.Admin, onSuccess, onFail);
    }

    /// <summary>荑좏룿 ?고렪 ?꾩껜瑜?鍮꾨룞湲곕줈 ?섎졊?⑸땲??/summary>
    public void ReceiveAllCouponMailAsync(Action onSuccess = null, Action onFail = null)
    {
        ReceiveAllByTypeAsync(PostType.Coupon, onSuccess, onFail);
    }

    private void ReceiveAllByTypeAsync(PostType postType, Action onSuccess, Action onFail)
    {
        // ?대떦 ??낆쓽 誘몄닔??硫붿씪 議댁옱 ?щ? ?뺤씤
        bool hasUnreceived = false;
        foreach (var m in _mailList)
            if (m.PostType == postType && !m.IsReceived && !m.IsExpired) { hasUnreceived = true; break; }

        if (!hasUnreceived)
        {
            Debug.Log($"[MailManager] ?섎졊??{postType} 硫붿씪???놁뒿?덈떎.");
            onFail?.Invoke();
            return;
        }

        BackendManager.Instance.ProcessBackendAPI(
            $"{postType} ?꾩껜 硫붿씪 ?섎졊",
            callback => Backend.UPost.ReceivePostItemAll(postType, bro => callback(bro)),
            bro =>
            {
                // ReceivePostItemAll ?묐떟: postItems = [ [?고렪1?꾩씠??..], [?고렪2?꾩씠??..], [] ]
                GiveRewardFromAllReceive(bro);

                foreach (var mail in _mailList)
                    if (mail.PostType == postType && !mail.IsReceived && !mail.IsExpired)
                    {
                        mail.SetReceived();
                        SaveMailToHistory(mail);
                    }

                onSuccess?.Invoke();
                OnAllMailReceived?.Invoke();
                InvokeRefreshed();
                Debug.Log($"[MailManager] {postType} ?꾩껜 硫붿씪 ?섎졊 ?꾨즺");
            },
            state =>
            {
                Debug.LogError($"[MailManager] {postType} ?꾩껜 硫붿씪 ?섎졊 ?ㅽ뙣: {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: true
        );
    }

    #endregion

    // ?????????????????????????????????????????
    #region 硫붿씪 ??젣

    /// <summary>?쎌?(?섎졊?? 硫붿씪??鍮꾨룞湲곕줈 ??젣?⑸땲??/summary>
    public void DeleteMailAsync(MailData mail, Action onSuccess = null, Action onFail = null)
    {
        if (mail == null)
        {
            onFail?.Invoke();
            return;
        }

        // ?섎졊 ?대젰(GameData)?먯꽌 ??硫붿씪 ??GameData ?덉퐫????젣
        if (mail.IsFromHistory && !string.IsNullOrEmpty(mail.HistoryInDate))
        {
            BackendManager.Instance.ProcessBackendAPI(
                "?섎졊 ?대젰 ??젣",
                callback => Backend.GameData.DeleteV2(HISTORY_TABLE, mail.HistoryInDate, Backend.UserInDate, bro => callback(bro)),
                bro =>
                {
                    _mailList.Remove(mail);
                    onSuccess?.Invoke();
                    InvokeRefreshed();
                    Debug.Log($"[MailManager] ?섎졊 ?대젰 ??젣 ?꾨즺: {mail.Title}");
                },
                state =>
                {
                    Debug.LogWarning($"[MailManager] ?섎졊 ?대젰 ??젣 ?ㅽ뙣: {state}");
                    _mailList.Remove(mail);
                    onSuccess?.Invoke();
                    InvokeRefreshed();
                },
                maxRetries: 0,
                usePopup: false
            );
            return;
        }

        // ?쇰컲 UPost 硫붿씪 ??DeleteUserPost ?쒕룄 (?섎졊 ???쒕쾭媛 ?뚮퉬 泥섎━?덉쑝硫??ㅽ뙣?섏?留?濡쒖뺄 ?쒓굅)
        BackendManager.Instance.ProcessBackendAPI(
            "硫붿씪 ??젣",
            callback => Backend.UPost.DeleteUserPost(mail.InDate, bro => callback(bro)),
            bro =>
            {
                _mailList.Remove(mail);
                onSuccess?.Invoke();
                InvokeRefreshed();
                Debug.Log($"[MailManager] 硫붿씪 ??젣 ?꾨즺: {mail.Title}");
            },
            state =>
            {
                Debug.LogWarning($"[MailManager] 硫붿씪 ??젣 ?ㅽ뙣(?쒕쾭 ?뚮퉬 泥섎━??: {state}");
                _mailList.Remove(mail);
                onSuccess?.Invoke();
                InvokeRefreshed();
            },
            maxRetries: 0,
            usePopup: false
        );
    }

    /// <summary>?섎졊 ?꾨즺??硫붿씪 ?꾩껜瑜???젣?⑸땲??/summary>
    public void DeleteAllReadMailAsync(Action onSuccess = null)
    {
        List<MailData> readMails = new List<MailData>();
        foreach (var mail in _mailList)
            if (mail.IsReceived) readMails.Add(mail);

        if (readMails.Count == 0)
        {
            onSuccess?.Invoke();
            return;
        }

        int remaining = readMails.Count;
        foreach (var mail in readMails)
        {
            DeleteMailAsync(mail, onSuccess: () =>
            {
                remaining--;
                if (remaining <= 0) onSuccess?.Invoke();
            });
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 蹂댁긽 ?고렪 諛쒖넚 (?먯떊?먭쾶)

    /// <summary>
    /// MailRewardConfig 湲곕컲?쇰줈 ?먯떊?먭쾶 蹂댁긽 ?고렪??諛쒖넚?⑸땲??
    /// ?? ?쒗넗由ъ뼹 ?대━?? ?낆쟻 ?ъ꽦, ?대깽??蹂댁긽 ??
    /// </summary>
    public void SendRewardMailToSelfAsync(MailRewardConfig config, Action onSuccess = null, Action onFail = null)
    {
        if (config == null)
        {
            Debug.LogError("[MailManager] SendRewardMailToSelfAsync: config媛 null?낅땲??");
            onFail?.Invoke();
            return;
        }

        if (string.IsNullOrEmpty(config.TableName) ||
            string.IsNullOrEmpty(config.Column) ||
            string.IsNullOrEmpty(config.RowInDate))
        {
            Debug.LogError($"[MailManager] SendRewardMailToSelfAsync: config({config.name}) ?꾨뱶媛 鍮꾩뼱?덉뒿?덈떎.");
            onFail?.Invoke();
            return;
        }

        PostItem postItem = new PostItem
        {
            TableName  = config.TableName,
            Column     = config.Column,
            RowInDate  = config.RowInDate
        };

        string receiverInDate = Backend.UserInDate;

        BackendManager.Instance.ProcessBackendAPI(
            $"蹂댁긽 ?고렪 諛쒖넚: {config.Description}",
            callback => Backend.UPost.SendUserPost(receiverInDate, postItem, bro => callback(bro)),
            bro =>
            {
                onSuccess?.Invoke();
                Debug.Log($"[MailManager] 蹂댁긽 ?고렪 諛쒖넚 ?꾨즺: {config.Description}");
            },
            state =>
            {
                Debug.LogError($"[MailManager] 蹂댁긽 ?고렪 諛쒖넚 ?ㅽ뙣: {config.Description} / {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: false
        );
    }

    /// <summary>
    /// ?щ윭 蹂댁긽???쒖감?곸쑝濡??먯떊?먭쾶 諛쒖넚?⑸땲??
    /// 紐⑤몢 ?깃났?댁빞 onSuccess媛 ?몄텧?⑸땲??
    /// </summary>
    public void SendRewardMailsToSelfAsync(MailRewardConfig[] configs, Action onSuccess = null, Action onFail = null)
    {
        if (configs == null || configs.Length == 0)
        {
            onFail?.Invoke();
            return;
        }

        int remaining = configs.Length;
        bool anyFailed = false;

        foreach (var config in configs)
        {
            SendRewardMailToSelfAsync(
                config,
                onSuccess: () =>
                {
                    remaining--;
                    if (remaining <= 0)
                    {
                        if (anyFailed) onFail?.Invoke();
                        else onSuccess?.Invoke();
                    }
                },
                onFail: () =>
                {
                    anyFailed = true;
                    remaining--;
                    if (remaining <= 0) onFail?.Invoke();
                }
            );
        }
    }

    #endregion

    // ?????????????????????????????????????????
    #region 蹂댁긽 吏湲?

    /// <summary>ReceivePostItem ?묐떟 ?뚯떛 ??postItems: [{item, itemCount}, ...]</summary>
    private void GiveRewardFromSingleReceive(BackendReturnObject bro)
    {
        try
        {
            JsonData postItems = bro.GetReturnValuetoJSON()["postItems"];
            for (int i = 0; i < postItems.Count; i++)
                GiveItem(postItems[i]);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailManager] ?④굔 ?섎졊 蹂댁긽 ?뚯떛 ?ㅻ쪟: {ex.Message}");
        }
    }

    /// <summary>ReceivePostItemAll ?묐떟 ?뚯떛 ??postItems: [[?고렪1 ?꾩씠??..], [?고렪2 ?꾩씠??..]]</summary>
    private void GiveRewardFromAllReceive(BackendReturnObject bro)
    {
        try
        {
            JsonData postItemsOuter = bro.GetReturnValuetoJSON()["postItems"];
            // ?몃? 諛곗뿴: 媛??고렪???꾩씠??洹몃９
            for (int i = 0; i < postItemsOuter.Count; i++)
            {
                JsonData mailItems = postItemsOuter[i];
                for (int j = 0; j < mailItems.Count; j++)
                    GiveItem(mailItems[j]);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailManager] ?꾩껜 ?섎졊 蹂댁긽 ?뚯떛 ?ㅻ쪟: {ex.Message}");
        }
    }

    /// <summary>?꾩씠????媛??뚯떛 ??itemId 湲곗??쇰줈 蹂댁긽 吏湲?/summary>
    private void GiveItem(JsonData itemJson)
    {
        if (!itemJson.ContainsKey("item")) return;

        string itemId = itemJson["item"].ContainsKey("itemName")
            ? itemJson["item"]["itemName"].ToString()
            : string.Empty;

        // {"itemID":"Gold","chartFileName":"..."} 형태면 itemID만 추출
        if (!string.IsNullOrEmpty(itemId) && itemId.StartsWith("{"))
        {
            try
            {
                JsonData nameJson = JsonMapper.ToObject(itemId);
                if (nameJson.ContainsKey("itemID"))
                    itemId = nameJson["itemID"].ToString();
            }
            catch { }
        }

        // itemName이 없으면 itemID 직접 시도
        if (string.IsNullOrEmpty(itemId) && itemJson["item"].ContainsKey("itemID"))
            itemId = itemJson["item"]["itemID"].ToString();

        int itemCount = 0;
        if (itemJson.ContainsKey("itemCount"))
            int.TryParse(itemJson["itemCount"].ToString(), out itemCount);

        // ?? 怨⑤뱶 / ?ㅼ씠?????????????????????????????????????????
        if (itemId == "골드" || itemId == "Gold")
        {
            UserInfo.AddMoney(itemCount);
            Debug.Log($"[MailManager] 골드 {itemCount} 지급");
            return;
        }
        if (itemId == "다이아" || itemId == "Dia")
        {
            UserInfo.AddDia(itemCount);
            Debug.Log($"[MailManager] 다이아 {itemCount} 지급");
            return;
        }

        // ?? 媛梨??꾩씠???????????????????????????????????????????
        if (ItemManager.Instance.IsGachaItem(itemId))
        {
            for (int i = 0; i < itemCount; i++)
                UserInfo.GiveGachaItem(itemId);
            Debug.Log($"[MailManager] 가챠아이템 {itemId} x{itemCount} 지급");
            return;
        }

        // ?? 怨좉컼 ?ㅽ궓 (SKIN_CUSTOMER ?묐몢?? ????????????????????
        if (itemId.StartsWith("SKIN_CUSTOMER", System.StringComparison.OrdinalIgnoreCase))
        {
            UserInfo.GiveCustomerSkin(itemId);
            Debug.Log($"[MailManager] 고객 스킨 {itemId} 지급");
            return;
        }

        // ?? 吏곸썝 ?ㅽ궓 (SKIN_STAFF ?묐몢?? ???????????????????????
        if (itemId.StartsWith("SKIN_STAFF", System.StringComparison.OrdinalIgnoreCase))
        {
            UserInfo.GiveStaffSkin(itemId);
            Debug.Log($"[MailManager] 직원 스킨 {itemId} 지급");
            return;
        }

        // ?? NPC / 吏곸썝 (STAFF ?묐몢?? ????????????????????????????
        if (itemId.StartsWith("STAFF", System.StringComparison.OrdinalIgnoreCase))
        {
            UserInfo.GiveStaff(UserInfo.CurrentStage, itemId);
            Debug.Log($"[MailManager] 직원 {itemId} 지급");
            return;
        }

        // ?? ? 媛援???????????????????????????????????????????????
        try
        {
            FurnitureData furnitureData = FurnitureDataManager.Instance.GetFurnitureData(itemId);
            if (furnitureData != null)
            {
                UserInfo.GiveFurniture(UserInfo.CurrentStage, itemId);
                Debug.Log($"[MailManager] 가구 {itemId} 지급");
                return;
            }
        }
        catch { }

        // ?? 二쇰갑 媛援??????????????????????????????????????????????
        try
        {
            KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData(itemId);
            if (kitchenData != null)
            {
                UserInfo.GiveKitchenUtensil(UserInfo.CurrentStage, itemId);
                Debug.Log($"[MailManager] 주방가구 {itemId} 지급");
                return;
            }
        }
        catch { }

        Debug.LogWarning($"[MailManager] 알 수 없는 아이템 ID: {itemId} x{itemCount}");
    }

    #endregion
}


