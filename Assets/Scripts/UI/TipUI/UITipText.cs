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
        _tipText.text = Utility.ConvertToMoney(UserInfo.Tip);
        _currentTip = UserInfo.Tip;

        UserInfo.OnChangeTipHandler += OnChangeMoneyEvent;
    }

    private void OnChangeMoneyEvent()
    {
        if (!gameObject.activeSelf)
        {
            _currentTip = UserInfo.Tip;
            return;
        }

        int addMoney = UserInfo.Tip - _currentTip;

        if (addMoney == 0)
            return;

        _currentTip = UserInfo.Tip;

        if (_moneyAnimeRoutine != null)
            StopCoroutine(_moneyAnimeRoutine);

        _moneyAnimeRoutine = StartCoroutine(AddMoneyAnime(addMoney));
    }

    private IEnumerator AddMoneyAnime(int addMoney)
    {
        int startMoney = UserInfo.Tip - addMoney;
        int targetMoney = UserInfo.Tip;
        float time = 0;

        while (time < 1)
        {
            _tipText.text = Utility.ConvertToMoney(Mathf.Lerp(startMoney, targetMoney, time));
            time += 0.02f * 2.5f;
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _tipText.text = Utility.ConvertToMoney(UserInfo.Tip);
    }
}
