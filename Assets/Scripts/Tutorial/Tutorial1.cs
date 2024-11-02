using Muks.MobileUI;
using System.Collections;
using UnityEngine;

public class Tutorial1 : MonoBehaviour
{
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private GameObject _mainSceneUI;
    [SerializeField] private UITutorial1 _uiTutorial1;
    [SerializeField] private UITutorialDescriptionNPC _uiDescriptionNPC;


    [Header("Objects")]
    [SerializeField] private GameObject _cobWeb1;
    [SerializeField] private GameObject _cobWeb2;
    [SerializeField] private GameObject _cobWeb3;
    [SerializeField] private GameObject _cobWeb4;
    [SerializeField] private GameObject _garbage1;
    [SerializeField] private GameObject _garbage2;
    [SerializeField] private GameObject _garbage3;
    [SerializeField] private GameObject _garbage4;
    [SerializeField] private ParticleSystem[] _boomParticles;
    [SerializeField] private PunchHoleAnimation _punchHole;

    private int _touchCount;

    private void Start()
    {
        if (UserInfo.Tutorial1Clear)
        {
            gameObject.SetActive(false);
            return;
        }

        _mainSceneUI.gameObject.SetActive(false);
        _punchHole.gameObject.SetActive(false);
        StartCoroutine(StartTutorial());
    }


    private IEnumerator StartTutorial()
    {
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Push("UITutorial1");
        _uiNav.Push("UITutorialDescription");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription1Text("안녕하세요.   \n이번에 새로 온 점장님이시죠?");
        yield return _uiDescriptionNPC.ShowDescription1Text("저는 점장님의 원활한 가게 운영을 위해 \n간단하게 안내해 드릴 \"제인\" 입니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("하나하나 설명해 드릴 테니 천천히 따라오세요!");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("먼저 가게 정리가 필요하겠군요.");
        _punchHole.gameObject.SetActive(true);
        _punchHole.TweenScale(1.5f, 1, 0.35f, Muks.Tween.Ease.OutBack);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("화면 터치로 먼지를 제거해 주세요!");
        _uiTutorial1.ScreenButtonSetActive(true);
        _punchHole.gameObject.SetActive(false);
        _uiTutorial1.StartTouch(Step2TouchEvent);
        while(_touchCount < 3)
            yield return YieldCache.WaitForSeconds(0.02f);
        _touchCount = 0;
        _uiTutorial1.StopTouch();
        _uiTutorial1.ScreenButtonSetActive(false);

        _garbage1.gameObject.SetActive(false);
        _garbage2.gameObject.SetActive(false);
        _garbage3.gameObject.SetActive(false);
        _garbage4.gameObject.SetActive(false);
        _cobWeb1.gameObject.SetActive(false);
        _cobWeb2.gameObject.SetActive(false);
        _cobWeb3.gameObject.SetActive(false);
        _cobWeb4.gameObject.SetActive(false);

        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("훌륭해요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("아직은 좀 심심하죠?");
        yield return _uiDescriptionNPC.ShowDescription2Text("역시 식당이라고 \n부르기 힘든 모습이네요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("우선 테이블을 \n설치해 볼까요?");
        _uiTutorial1.ShopButtonSetActive(true);
        _uiTutorial1.PunchHoleSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("먼저 상점으로 들어가 \n테이블을 구매해 주세요.");
        _uiTutorial1.ShopMaskSetActive(true);

        while (!_uiTutorial1.IsShopButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        _uiTutorial1.ShopButtonSetActive(false);
        _uiTutorial1.ShopMaskSetActive(false);
        _uiTutorial1.PunchHoleSetActive(false);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.PunchHoleSetActive(true);
        _uiTutorial1.TableHoleSetActive(true);
        
        while(!_uiTutorial1.IsTableHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Buy Button");
        while(!UserInfo.IsGiveFurniture("TABLE01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Equip Button");
        FurnitureData table1Data = FurnitureDataManager.Instance.GetFurnitureData("TABLE01_01");

        while (!UserInfo.IsEquipFurniture(table1Data))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("다음으로 음식을 조리하기 위해 \n조리 기구를 설치해 볼까요?");
        yield return _uiDescriptionNPC.ShowDescription2Text("주방 카테고리로 이동해 \n조리 기구를 구매해 주세요!");
        yield return YieldCache.WaitForSeconds(1);

        _uiNav.Push("UITutorial1");
        _uiTutorial1.BackHoleSetActive(true);
        while(!_uiTutorial1.IsBackHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.KitchenHoleSetActive(true);
        while (!_uiTutorial1.IsKitchenHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.TableHoleSetActive(true);
        while(!_uiTutorial1.IsTableHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveKitchenUtensil("COOKER01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.BuyHoleSetActive(true);
        _uiTutorial1.SetBuyHoleTargetObjectName("Equip Button");
        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData("COOKER01_01");
        while (!UserInfo.IsEquipKitchenUtensil(kitchenData))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("잘하셨습니다! \n손님을 맞이할 준비를 끝내셨네요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("이제 레스토랑을 \n한번 확인해 볼까요?");

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial1.ExitHoleSetActive(true);
        while(!_uiTutorial1.IsExitHoleClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1.3f);
        yield return _uiDescriptionNPC.ShowDescription2Text("현재 손님을 맞이할 \n준비는 완료됬네요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("하지만 손님들이 \n아직 우리 가게를 잘 모를거에요.");
    }

    private void Step2TouchEvent()
    {
        if (3 < _touchCount)
            return;

        _touchCount++;

        for(int i = 0, cnt = _boomParticles.Length; i < cnt; ++i)
        {
            _boomParticles[i].Emit(1);
        }
    }

}
