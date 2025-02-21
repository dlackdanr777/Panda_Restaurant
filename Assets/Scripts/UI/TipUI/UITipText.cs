using UnityEngine;
using System.Collections;
using TMPro;

public class UITipText : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _tipText;
    private Coroutine _moneyAnimeRoutine;
    private int _currentTip;


    private void Awake()
    {
        _tipText.text = Utility.ConvertToMoney(UserInfo.GetTip(UserInfo.CurrentStage));
        _currentTip = UserInfo.GetTip(UserInfo.CurrentStage);

        UserInfo.OnChangeTipHandler += OnChangeMoneyEvent;
    }

    private void OnEnable()
    {
        OnChangeMoneyEvent();
    }

    private void OnChangeMoneyEvent()
    {
        if (!gameObject.activeInHierarchy)
        {
            _currentTip = UserInfo.GetTip(UserInfo.CurrentStage);
            return;
        }

        int addMoney = UserInfo.GetTip(UserInfo.CurrentStage) - _currentTip;

        if (addMoney == 0)
            return;

        _currentTip = UserInfo.GetTip(UserInfo.CurrentStage);

        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(addMoney));
    }

    private IEnumerator AddMoneyAnime(int addMoney)
    {
        int startMoney = UserInfo.GetTip(UserInfo.CurrentStage) - addMoney;
        int targetMoney = UserInfo.GetTip(UserInfo.CurrentStage);
        float time = 0;

        while (time < 1)
        {
            _tipText.text = Utility.ConvertToMoney(Mathf.FloorToInt(Mathf.Lerp(startMoney, targetMoney, time)));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _tipText.text = Utility.ConvertToMoney(UserInfo.GetTip(UserInfo.CurrentStage));
    }
}
