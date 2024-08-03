using Muks.Tween;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIMoney : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _moneyText;

    [Space]
    [Header("Animations")]
    [SerializeField] private RectTransform _animeParent;
    [SerializeField] private float _moveY;
    [SerializeField] private float _moveDuration;
    [SerializeField] private Ease _moveEase;
    [SerializeField] private Color _startColor;

    private int _currentMoney;



    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        _moneyText.text = Utility.ConvertToNumber(UserInfo.Money);
        _currentMoney = UserInfo.Money;

        UserInfo.OnChangeMoneyHandler += OnChangeMoneyEvent;
    }


    private void OnChangeMoneyEvent()
    {
        
        int textMoney = UserInfo.Money - _currentMoney;

        if (textMoney == 0)
            return;
        _currentMoney = UserInfo.Money;
        _moneyText.text = Utility.ConvertToNumber(UserInfo.Money);
        string sign = textMoney < 0 ? "-" : "+";
        Vector3 spawnPos = _moneyText.transform.position + new Vector3(0, 5, 0);
        TextMeshProUGUI tmp = ObjectPoolManager.Instance.SpawnTMP(spawnPos, Quaternion.identity, _animeParent);

        tmp.text = sign + Utility.ConvertToNumber(textMoney);
        tmp.fontSize = _moneyText.fontSize;
        tmp.color = _startColor;
        tmp.TweenAlpha(0, _moveDuration);
        tmp.TweenMoveY(spawnPos.y + _moveY, _moveDuration, _moveEase).OnComplete(() => ObjectPoolManager.Instance.DespawnTmp(tmp));
    }
}
