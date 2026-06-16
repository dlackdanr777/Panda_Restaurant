using Muks.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FeverTutorial : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UINavigation _tutorialNav;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _descriptionNPC;
 
    private Coroutine _coroutine;

    public void StartTutorial()
    {
        if (UserInfo.IsTutorialStart)
            return;

        if(UserInfo.IsFeverTutorialClear)
            return;

        gameObject.SetActive(true);
        if (_coroutine != null)
            StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(TutorialRoutine());
    }


    private IEnumerator TutorialRoutine()
    {
        _descriptionNPC.OnSkipOkButtonClicked(OnSkipButtonClicked);

        UserInfo.IsTutorialStart = true;
        _tutorialNav.Push("UITutorial");
        _uiTutorial.ScreenButtonSetActive(true);
        _tutorialNav.Push("UITutorialDescription");
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        
        yield return YieldCache.WaitForSeconds(1f);
        yield return _descriptionNPC.ShowDescription4Text("피버 게이지가 전부 찼어요!");
        yield return _descriptionNPC.ShowDescription4Text("피버타임을 활용해보세요.");
        yield return _descriptionNPC.ShowDescription4Text("피버타임에는 모든 스텝의 움직임이\n두 배로 빨라져요!");

        yield return YieldCache.WaitForSeconds(0.02f);
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        UserInfo.IsFeverTutorialClear = true;
        gameObject.SetActive(false);
    }

    private void OnSkipButtonClicked()
    {
        _descriptionNPC.PopEnabled = true;
        _uiTutorial.PopEnabled = true;
        _tutorialNav.Pop("UITutorialDescription");
        _tutorialNav.Pop("UITutorial");
        _descriptionNPC.PopEnabled = false;
        _uiTutorial.PopEnabled = false;

        UserInfo.IsTutorialStart = false;
        UserInfo.IsFeverTutorialClear = true;
        gameObject.SetActive(false);
    }
}
