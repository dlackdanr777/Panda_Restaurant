using Muks.MobileUI;
using Muks.Tween;
using System.Collections;
using UnityEngine;

public class FirstTutorial : MonoBehaviour
{
    [SerializeField] private MobileUINavigation _uiNav;
    [SerializeField] private GameObject _mainSceneUI;
    [SerializeField] private UITutorial _uiTutorial;
    [SerializeField] private UITutorialDescriptionNPC _uiDescriptionNPC;


    [Header("Objects")]
    [SerializeField] private Camera _camera;
    [SerializeField] private Transform _kitchenCameraPos;
    [SerializeField] private Transform _restaurantCameraPos;
    [SerializeField] private GameObject _table1;
    [SerializeField] private GameObject _burner1;
    [SerializeField] private GameObject _cobWeb1;
    [SerializeField] private GameObject _cobWeb2;
    [SerializeField] private GameObject _cobWeb3;
    [SerializeField] private GameObject _cobWeb4;
    [SerializeField] private GameObject _garbage1;
    [SerializeField] private GameObject _garbage2;
    [SerializeField] private GameObject _garbage3;
    [SerializeField] private GameObject _garbage4;
    [SerializeField] private ParticleSystem[] _boomParticles;
    [SerializeField] private ParticleSystem _table1BoomParticle;
    [SerializeField] private ParticleSystem _burner1BoomParticle;
    [SerializeField] private PunchHoleAnimation _punchHole;

    private int _touchCount;

    private void Start()
    {
        if (UserInfo.IsFirstTutorialClear)
        {
            gameObject.SetActive(false);
            return;
        }

        _mainSceneUI.gameObject.SetActive(false);
        _punchHole.gameObject.SetActive(false);
        UserInfo.IsTutorialStart = true;
        UserInfo.GiveFurniture("COUNTER01");
        UserInfo.SetEquipFurniture("COUNTER01");
        UserInfo.GiveFurniture("WALLPAPER01");
        UserInfo.SetEquipFurniture("WALLPAPER01");
        UserInfo.GiveStaff("STAFF11");
        UserInfo.SetEquipStaff("STAFF11");
        StartCoroutine(StartTutorial());
    }


    private IEnumerator StartTutorial()
    {
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Push("UITutorial");
        _uiNav.Push("UITutorialDescription");
        yield return YieldCache.WaitForSeconds(3);
        yield return _uiDescriptionNPC.ShowDescription1Text("안녕하세요.   \n이번에 새로 온 점장님이시죠?");
        yield return _uiDescriptionNPC.ShowDescription1Text("저는 점장님의 원활한 가게 운영을 위해 간단하게 안내해 드릴 \"제인\" 입니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("하나하나 설명해 드릴 테니 천천히 따라오세요!");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("먼저 가게 정리가 필요하겠군요.");
        _punchHole.gameObject.SetActive(true);
        _punchHole.TweenScale(1.5f, 1, 0.35f, Muks.Tween.Ease.OutBack);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("화면 터치로 먼지를 제거해 주세요!");
        _uiTutorial.ScreenButtonSetActive(true);
        _punchHole.gameObject.SetActive(false);
        _uiTutorial.StartTouch(Step2TouchEvent);
        while(_touchCount < 3)
            yield return YieldCache.WaitForSeconds(0.02f);
        _touchCount = 0;
        _uiTutorial.StopTouch();
        _uiTutorial.ScreenButtonSetActive(false);

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
        yield return _uiDescriptionNPC.ShowDescription2Text("역시 식당이라 부르기 힘든 모습이네요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("우선 테이블을 설치해 볼까요?");
        _uiTutorial.ShopButtonSetActive(true);
        _uiTutorial.PunchHoleSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("먼저 상점으로 들어가 테이블을 구매해 주세요.");
        _uiTutorial.ShopMaskSetActive(true);

        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        _uiTutorial.ShopButtonSetActive(false);
        _uiTutorial.ShopMaskSetActive(false);
        _uiTutorial.PunchHoleSetActive(false);

        _uiTutorial.SetTableHoleTargetObjectName("FurnitureTabSlot1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.TableHoleSetActive(true);
        
        while(!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        while(!UserInfo.IsGiveFurniture("TABLE01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Equip Button");
        FurnitureData table1Data = FurnitureDataManager.Instance.GetFurnitureData("TABLE01_01");

        while (!UserInfo.IsEquipFurniture(table1Data))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("다음으로 음식을 조리하기 위해 조리 기구를 설치해 볼까요?");
        yield return _uiDescriptionNPC.ShowDescription2Text("주방 카테고리로 이동해 조리 기구를 구매해 주세요!");
        yield return YieldCache.WaitForSeconds(1);

        _uiNav.Push("UITutorial");
        _uiTutorial.BackHoleSetActive(true);
        while(!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.KitchenHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiTutorial.SetTableHoleTargetObjectName("KitchenTabSlot1");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.TableHoleSetActive(true);
        while(!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveKitchenUtensil("COOKER01_01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Equip Button");
        KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData("COOKER01_01");
        while (!UserInfo.IsEquipKitchenUtensil(kitchenData))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("잘하셨습니다! \n손님을 맞이할 준비를 끝내셨네요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("이제 식당을 한번 확인해 볼까요?");

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.ExitHoleSetActive(true);
        _table1.SetActive(false);
        _burner1.SetActive(false);
        _camera.transform.position = _kitchenCameraPos.position;
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1.5f);
        _camera.TweenMove(_restaurantCameraPos.position, 5, Ease.Smoothstep);
        yield return YieldCache.WaitForSeconds(1f);
        _burner1.SetActive(true);
        _burner1BoomParticle.Emit(1);
        yield return YieldCache.WaitForSeconds(2.5f);
        _table1.SetActive(true);
        _table1BoomParticle.Emit(1);
        yield return YieldCache.WaitForSeconds(4f);

        yield return _uiDescriptionNPC.ShowDescription2Text("현재 손님을 맞이할 준비는 완료됬네요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("하지만 손님들이 아직 우리 가게를 잘 모를거에요.");

        _uiTutorial.AddCustomerButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("우측 하단에 있는 토루루는 손님을 불러 모을 수 있는 특별한 직원이랍니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("토루루를 눌러 손님을 호출해 봅시다!");
        _uiTutorial.AddCustomerHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiTutorial.AddCustomerButtonSetActive(false);
        _uiTutorial.AddCustomerHoleSetActive(false);
        yield return YieldCache.WaitForSeconds(5);
        yield return _uiDescriptionNPC.ShowDescription2Text("축하드려요. 점장님! \n드디어 가게의 첫 번째 손님이네요!");

        _uiTutorial.CustomerGuideButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("카운터에서 빈 테이블로 안내해 주세요!");
        _uiTutorial.CustomerGuideHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(8);
        _uiTutorial.OrderButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("손님이 주먹밥을 주문하셨네요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("레시피를 배우지 않은 상태에선 주문을 받을 수 없습니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("모르는 레시피의 주문을 받으면 손님은 장난을 치는거라고 느끼겠죠?");
        _uiTutorial.ShopButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text("불쾌해진 손님이 식당을 나가기전에 레시피를 배워봅시다!");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.ShopMaskSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiNav.Push("UITutorial");
        _uiTutorial.OrderButtonSetActive(false);
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.RecipeHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.BuyHoleSetActive(true);
        _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        while (!UserInfo.IsGiveRecipe("FOOD01"))
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("잘하셨습니다! \n이걸로 주방에서 주먹밥을 제작할 수 있게 되었어요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("조리 기구의 효율에 따라 음식 제작 시간이 줄어든답니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("더 많은 손님을 만족시키기 위해선 주방을 업그레이드할 필요가 있겠죠?");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.ExitHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.OrderButtonSetActive(true);
        _uiNav.Push("UITutorialDescription");
        yield return _uiDescriptionNPC.ShowDescription2Text("자 이제 주문을 받아봅시다.");
        yield return YieldCache.WaitForSeconds(1f);
        _uiTutorial.OrderHoleSetActive(true);

        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(2f);
        _uiTutorial.SetOrderHoleTargetObjectName("Tutorial Serving Button");
        _uiTutorial.ServingButtonSetActive(true);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("주먹밥이 완성되었군요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("직접 손님에게 음식을 전달해봅시다.");
        yield return YieldCache.WaitForSeconds(1);
        _uiTutorial.OrderHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(3);
        yield return _uiDescriptionNPC.ShowDescription2Text("첫 손님이 만족하신 것 같네요!");
        yield return _uiDescriptionNPC.ShowDescription2Text("손님이 있던 자리에 쓰레기도 치워줘야해요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("쓰레기가 계속 쌓이면 다른 손님들이 불쾌감을 느낍니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("불쾌해진 손님이 식당을 나가기전에 쓰레기를 치워줍시다!");
        _uiTutorial.Table1HoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text("수고하셨어요. 새싹 점장님! \n가장 기초적인 안내는 끝났습니다.");
        yield return _uiDescriptionNPC.ShowDescription2Text("처음에는 걱정했는데, 생각보다 잘 따라오시네요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("그럼, 앞으로 이 가게를 잘 부탁드립니다!");
        _uiTutorial.PunchHoleSetActive(false);
        UserInfo.IsFirstTutorialClear = true;
        _mainSceneUI.gameObject.SetActive(true);

        _uiTutorial.PopEnabled = true;
        _uiDescriptionNPC.PopEnabled = true;
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Pop("UITutorial");
        _uiNav.Pop("UITutorialDescription");
        _uiTutorial.PopEnabled = false;
        _uiDescriptionNPC.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        gameObject.SetActive(false);
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
