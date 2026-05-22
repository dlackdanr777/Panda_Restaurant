using System.Collections;
using UnityEngine;

/// <summary>
/// 메일함 알람 뱃지 컴포넌트.
/// 미수령 메일이 있으면 알람 오브젝트를 활성화합니다.
/// 주기적으로 서버에서 메일 목록을 갱신하거나,
/// MailManager 이벤트를 통해 즉시 반응합니다.
/// </summary>
public class UIMailAlarm : MonoBehaviour
{
    [Header("알람 오브젝트")]
    [SerializeField] private GameObject _alarmObject;   // 빨간 점 등 알람 표시 오브젝트

    [Header("주기적 갱신")]
    [SerializeField] private bool _enablePolling = true;
    [SerializeField] private float _pollIntervalSeconds = 300f;  // 기본 5분

    private Coroutine _pollingCoroutine;

    private void Start()
    {
        MailManager.Instance.OnAlarmChanged += SetAlarm;

        // 씬 시작 시 서버에서 메일 목록을 로드하여 알람 갱신
        MailManager.Instance.LoadMailListAsync();

        if (_enablePolling)
            _pollingCoroutine = StartCoroutine(PollingRoutine());
    }

    private void OnDestroy()
    {
        if (MailManager.Instance != null)
            MailManager.Instance.OnAlarmChanged -= SetAlarm;

        if (_pollingCoroutine != null)
            StopCoroutine(_pollingCoroutine);
    }

    /// <summary>알람 오브젝트 활성/비활성</summary>
    public void SetAlarm(bool hasAlarm)
    {
        if (_alarmObject != null)
            _alarmObject.SetActive(hasAlarm);
    }

    /// <summary>주기적으로 서버에서 메일 목록 갱신 요청</summary>
    private IEnumerator PollingRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_pollIntervalSeconds);
            MailManager.Instance.LoadMailListAsync();
        }
    }
}
