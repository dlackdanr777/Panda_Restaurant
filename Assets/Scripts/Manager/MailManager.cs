using BackEnd;
using LitJson;
using Muks.BackEnd;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>뒤끝 우편(메일함) 시스템을 관리하는 싱글톤 매니저</summary>
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

    /// <summary>메일 목록이 갱신될 때 호출됩니다</summary>
    public event Action OnMailListRefreshed;

    /// <summary>메일 한 건 수령 완료 시 호출됩니다</summary>
    public event Action<MailData> OnMailReceived;

    /// <summary>전체 메일 수령 완료 시 호출됩니다</summary>
    public event Action OnAllMailReceived;

    private List<MailData> _mailList = new List<MailData>();
    public IReadOnlyList<MailData> MailList => _mailList;

    /// <summary>관리자 우편 중 수령 가능한(미수령·미만료) 개수</summary>
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

    // ─────────────────────────────────────────
    #region 메일 목록 조회 (관리자 + 쿠폰 동시 로드)

    /// <summary>관리자 우편과 쿠폰 우편을 모두 비동기로 가져옵니다</summary>
    public void LoadMailListAsync(Action onSuccess = null, Action onFail = null)
    {
        _mailList.Clear();

        // Admin 먼저 로드 → 완료 후 Coupon 로드
        LoadByTypeAsync(PostType.Admin, () =>
        {
            LoadByTypeAsync(PostType.Coupon, () =>
            {
                onSuccess?.Invoke();
                OnMailListRefreshed?.Invoke();
                Debug.Log($"[MailManager] 전체 메일 {_mailList.Count}건 로드 완료 (미수령: {UnreceivedCount}건)");
            },
            // 쿠폰 실패해도 Admin 결과는 유지
            () =>
            {
                onSuccess?.Invoke();
                OnMailListRefreshed?.Invoke();
            });
        }, onFail);
    }

    private void LoadByTypeAsync(PostType postType, Action onSuccess, Action onFail)
    {
        BackendManager.Instance.ProcessBackendAPI(
            $"{postType} 우편 목록 조회",
            callback => Backend.UPost.GetPostList(postType, 100, bro => callback(bro)),
            bro =>
            {
                ParseMailList(postType, bro);
                onSuccess?.Invoke();
            },
            state =>
            {
                Debug.LogError($"[MailManager] {postType} 우편 목록 조회 실패: {state}");
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
            Debug.LogError($"[MailManager] {postType} 우편 파싱 오류: {ex.Message}");
        }
    }

    #endregion

    // ─────────────────────────────────────────
    #region 메일 수령 (단건)

    /// <summary>특정 메일의 보상을 비동기로 수령합니다</summary>
    public void ReceiveMailAsync(MailData mail, Action<MailData> onSuccess = null, Action onFail = null)
    {
        if (mail == null || mail.IsReceived || mail.IsExpired)
        {
            Debug.LogWarning("[MailManager] 수령할 수 없는 메일입니다.");
            onFail?.Invoke();
            return;
        }

        BackendManager.Instance.ProcessBackendAPI(
            "메일 수령",
            callback => Backend.UPost.ReceivePostItem(mail.PostType, mail.InDate, bro => callback(bro)),
            bro =>
            {
                mail.SetReceived();
                GiveRewardFromSingleReceive(bro);
                onSuccess?.Invoke(mail);
                OnMailReceived?.Invoke(mail);
                Debug.Log($"[MailManager] 메일 수령 완료: {mail.Title}");
            },
            state =>
            {
                Debug.LogError($"[MailManager] 메일 수령 실패: {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: true
        );
    }

    #endregion

    // ─────────────────────────────────────────
    #region 메일 전체 수령

    /// <summary>관리자 우편 전체를 비동기로 수령합니다</summary>
    public void ReceiveAllMailAsync(Action onSuccess = null, Action onFail = null)
    {
        ReceiveAllByTypeAsync(PostType.Admin, onSuccess, onFail);
    }

    /// <summary>쿠폰 우편 전체를 비동기로 수령합니다</summary>
    public void ReceiveAllCouponMailAsync(Action onSuccess = null, Action onFail = null)
    {
        ReceiveAllByTypeAsync(PostType.Coupon, onSuccess, onFail);
    }

    private void ReceiveAllByTypeAsync(PostType postType, Action onSuccess, Action onFail)
    {
        // 해당 타입의 미수령 메일 존재 여부 확인
        bool hasUnreceived = false;
        foreach (var m in _mailList)
            if (m.PostType == postType && !m.IsReceived && !m.IsExpired) { hasUnreceived = true; break; }

        if (!hasUnreceived)
        {
            Debug.Log($"[MailManager] 수령할 {postType} 메일이 없습니다.");
            onFail?.Invoke();
            return;
        }

        BackendManager.Instance.ProcessBackendAPI(
            $"{postType} 전체 메일 수령",
            callback => Backend.UPost.ReceivePostItemAll(postType, bro => callback(bro)),
            bro =>
            {
                // ReceivePostItemAll 응답: postItems = [ [우편1아이템...], [우편2아이템...], [] ]
                GiveRewardFromAllReceive(bro);

                foreach (var mail in _mailList)
                    if (mail.PostType == postType && !mail.IsReceived && !mail.IsExpired)
                        mail.SetReceived();

                onSuccess?.Invoke();
                OnAllMailReceived?.Invoke();
                OnMailListRefreshed?.Invoke();
                Debug.Log($"[MailManager] {postType} 전체 메일 수령 완료");
            },
            state =>
            {
                Debug.LogError($"[MailManager] {postType} 전체 메일 수령 실패: {state}");
                onFail?.Invoke();
            },
            maxRetries: 2,
            usePopup: true
        );
    }

    #endregion

    // ─────────────────────────────────────────
    #region 보상 지급

    /// <summary>ReceivePostItem 응답 파싱 → postItems: [{item, itemCount}, ...]</summary>
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
            Debug.LogError($"[MailManager] 단건 수령 보상 파싱 오류: {ex.Message}");
        }
    }

    /// <summary>ReceivePostItemAll 응답 파싱 → postItems: [[우편1 아이템...], [우편2 아이템...]]</summary>
    private void GiveRewardFromAllReceive(BackendReturnObject bro)
    {
        try
        {
            JsonData postItemsOuter = bro.GetReturnValuetoJSON()["postItems"];
            // 외부 배열: 각 우편의 아이템 그룹
            for (int i = 0; i < postItemsOuter.Count; i++)
            {
                JsonData mailItems = postItemsOuter[i];
                for (int j = 0; j < mailItems.Count; j++)
                    GiveItem(mailItems[j]);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MailManager] 전체 수령 보상 파싱 오류: {ex.Message}");
        }
    }

    /// <summary>아이템 한 개 파싱 → itemName 기준으로 보상 지급</summary>
    private void GiveItem(JsonData itemJson)
    {
        if (!itemJson.ContainsKey("item")) return;

        string itemName = itemJson["item"].ContainsKey("itemName")
            ? itemJson["item"]["itemName"].ToString()
            : string.Empty;

        if (string.IsNullOrEmpty(itemName)) return;

        int itemCount = 0;
        if (itemJson.ContainsKey("itemCount"))
            int.TryParse(itemJson["itemCount"].ToString(), out itemCount);

        if (itemName == "코인" || itemName == "Gold")
        {
            UserInfo.AddMoney(itemCount);
            Debug.Log($"[MailManager] 코인 {itemCount} 지급");
        }
        else if (itemName == "다이아" || itemName == "Dia")
        {
            UserInfo.AddDia(itemCount);
            Debug.Log($"[MailManager] 다이아 {itemCount} 지급");
        }
        else
        {
            Debug.Log($"[MailManager] 알 수 없는 아이템: {itemName} x{itemCount}");
        }
    }

    #endregion
}

