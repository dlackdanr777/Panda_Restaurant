using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Muks.MobileUI;
using Muks.UI;
using UnityEngine.UI;

public class UIChallenge : MobileUIView
{
    [Header("Components")]
    [SerializeField] private UIChallengeTab _uiDaily;
    [SerializeField] private UIChallengeTab _uiAllTime;

    public override void Init()
    {
        _uiDaily.Init(ChallengeManager.Instance.GetMainChallenge());

        gameObject.SetActive(false);
    }


    public override void Show()
    {
        throw new System.NotImplementedException();
    }


    public override void Hide()
    {
        throw new System.NotImplementedException();
    }




}
