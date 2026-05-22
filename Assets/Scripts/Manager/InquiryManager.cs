using BackEnd;
using Muks.BackEnd;
using System;
using UnityEngine;

/// <summary>
/// 유저 신고 / 문의를 뒤끝 UserInquiry 테이블에 저장하는 싱글톤 매니저.
/// 
/// [뒤끝 콘솔 설정 필요]
/// - 테이블명: UserInquiry
/// - 필드: senderUUID, senderNickname, category, message,
///         createdAt, status, clientVersion, platform, deviceInfo
/// </summary>
public class InquiryManager : MonoBehaviour
{
    public static InquiryManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("InquiryManager");
                _instance = obj.AddComponent<InquiryManager>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    private static InquiryManager _instance;

    // ─── 상수 ───────────────────────────────────────────
    private const string TABLE_NAME      = "UserInquiry";
    private const float  SUBMIT_COOLDOWN = 10f;   // 연속 전송 방지 (초)

    // ─── 상태 ───────────────────────────────────────────
    private float _lastSubmitTime = -9999f;

    /// <summary>쿨타임 중인지 여부</summary>
    public bool IsOnCooldown => Time.realtimeSinceStartup - _lastSubmitTime < SUBMIT_COOLDOWN;

    /// <summary>쿨타임 남은 시간 (초)</summary>
    public float CooldownRemaining => Mathf.Max(0f, SUBMIT_COOLDOWN - (Time.realtimeSinceStartup - _lastSubmitTime));

    // ─── category 상수 (기획서 9-4) ─────────────────────
    public const string CATEGORY_USER_REPORT    = "user_report";
    public const string CATEGORY_BUG_REPORT     = "bug_report";
    public const string CATEGORY_PAYMENT_ISSUE  = "payment_issue";
    public const string CATEGORY_REWARD_MISSING = "reward_missing";
    public const string CATEGORY_AD_ISSUE       = "ad_issue";
    public const string CATEGORY_ACCOUNT_ISSUE  = "account_issue";
    public const string CATEGORY_ETC            = "etc";

    // ─── 생명주기 ────────────────────────────────────────
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────────────
    #region 문의 / 신고 전송

    /// <summary>
    /// 일반 문의를 비동기로 뒤끝 UserInquiry 테이블에 저장합니다.
    /// </summary>
    /// <param name="category">기획서 9-4 카테고리 값 (CATEGORY_* 상수 사용 권장)</param>
    /// <param name="message">문의 본문</param>
    /// <param name="onSuccess">성공 콜백</param>
    /// <param name="onFail">실패 콜백</param>
    public void SubmitInquiryAsync(string category, string message,
        Action onSuccess = null, Action onFail = null)
    {
        SubmitInternalAsync(
            category:        category,
            message:         message,
            targetUUID:      null,
            targetNickname:  null,
            onSuccess:       onSuccess,
            onFail:          onFail
        );
    }

    /// <summary>
    /// 특정 유저에 대한 신고를 비동기로 저장합니다.
    /// targetUUID / targetNickname은 클라이언트에서 고정값으로 전달해야 합니다.
    /// </summary>
    public void SubmitUserReportAsync(string message,
        string targetUUID, string targetNickname,
        Action onSuccess = null, Action onFail = null)
    {
        SubmitInternalAsync(
            category:       CATEGORY_USER_REPORT,
            message:        message,
            targetUUID:     targetUUID,
            targetNickname: targetNickname,
            onSuccess:      onSuccess,
            onFail:         onFail
        );
    }

    // ─── 내부 공통 로직 ──────────────────────────────────

    private void SubmitInternalAsync(string category, string message,
        string targetUUID, string targetNickname,
        Action onSuccess, Action onFail)
    {
        if (!BackendManager.Instance.IsLogin)
        {
            Debug.LogWarning("[InquiryManager] 로그인 상태가 아닙니다.");
            PopupManager.Instance?.ShowDisplayText("로그인 후 이용해주세요.");
            onFail?.Invoke();
            return;
        }

        if (IsOnCooldown)
        {
            int sec = Mathf.CeilToInt(CooldownRemaining);
            PopupManager.Instance?.ShowDisplayText($"{sec}초 후에 다시 전송해주세요.");
            onFail?.Invoke();
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            PopupManager.Instance?.ShowDisplayText("문의 내용을 입력해주세요.");
            onFail?.Invoke();
            return;
        }

        Param param = new Param();

        // 발신자 정보 (자동)
        param.Add("senderUUID",     string.IsNullOrEmpty(UserInfo.GamerId) ? Backend.UserInDate : UserInfo.GamerId);
        param.Add("senderNickname", Backend.UserNickName ?? string.Empty);

        // 신고 대상 (없으면 빈 문자열)
        param.Add("targetUUID",     targetUUID     ?? string.Empty);
        param.Add("targetNickname", targetNickname ?? string.Empty);

        // 문의 내용
        param.Add("category", string.IsNullOrWhiteSpace(category) ? CATEGORY_ETC : category.Trim());
        param.Add("message",  message.Trim());

        // 처리 상태 (기획서 9-3)
        param.Add("status", "waiting");

        // 자동 저장 정보 (기획서 10-1)
        param.Add("createdAt",     DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        param.Add("clientVersion", Application.version);
        param.Add("platform",      Application.platform.ToString());
        param.Add("deviceInfo",    SystemInfo.deviceModel);

        BackendManager.Instance.InsertGameDataAsync(
            TABLE_NAME,
            param,
            bro =>
            {
                _lastSubmitTime = Time.realtimeSinceStartup;
                Debug.Log($"[InquiryManager] 문의 접수 완료 (category: {category})");
                onSuccess?.Invoke();
            },
            state =>
            {
                Debug.LogError($"[InquiryManager] 문의 접수 실패: {state}");
                onFail?.Invoke();
            }
        );
    }

    #endregion
}
