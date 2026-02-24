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
    [SerializeField] private CameraController _cameraController;
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

    private void Awake()
    {
        if (UserInfo.IsFirstTutorialClear)
        {
            gameObject.SetActive(false);
            return;
        }

        _mainSceneUI.gameObject.SetActive(false);
        _punchHole.gameObject.SetActive(false);
        UserInfo.IsTutorialStart = true;

        UserInfo.GiveStaff(EStage.Stage1, "STAFF11");
        UserInfo.SetEquipStaff(EStage.Stage1, ERestaurantFloorType.Floor1, EquipStaffType.Marketer, "STAFF11");
        UserInfo.AddMoney(5000);
        SequentialCommandManager.Instance.EnqueueCommand(StartTutorial, () => true, () => UserInfo.IsFirstTutorialClear, 0, 0.3f);
    }


    private void StartTutorial()
    {
        StartCoroutine(StartTutorialRoutine());
    }


    private IEnumerator StartTutorialRoutine()
    {
        _uiDescriptionNPC.OnSkipOkButtonClicked(OnSkipButtonClicked);
        yield return YieldCache.WaitForSeconds(0.02f);
        _uiNav.Push("UITutorial");
        _uiNav.Push("UITutorialDescription");
        yield return YieldCache.WaitForSeconds(3);
        yield return _uiDescriptionNPC.ShowDescription1Text("안녕하세요!");
        yield return _uiDescriptionNPC.ShowDescription1Text("앞으로 레스토랑 운영을 도와드릴\n제인입니다!");
        yield return _uiDescriptionNPC.ShowDescription1Text("레스토랑 운영은 처음이시죠?\n걱정 마세요!");
        yield return _uiDescriptionNPC.ShowDescription1Text("기본부터 차근차근 알려드릴게요!");
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription2Text("우선 청소가 필요해보여요.");
        yield return _uiDescriptionNPC.ShowDescription2Text("이런 더러운 곳은 아무도\n오지 않을거에요.");
        _punchHole.gameObject.SetActive(true);
        _punchHole.TweenScale(1.5f, 1, 0.35f, Muks.Tween.Ease.OutBack);
        yield return YieldCache.WaitForSeconds(1f);
        yield return _uiDescriptionNPC.ShowDescription2Text($"{Utility.SetStringColor("더러운 곳을 터치", ColorType.Positive)}해서\n깨끗하게 치워볼까요?");
        _uiTutorial.ScreenButtonSetActive(true);
        _punchHole.gameObject.SetActive(false);
        _uiTutorial.StartTouch(Step2TouchEvent);
        _touchCount = 0;
        while (_touchCount < 3)
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
        yield return _uiDescriptionNPC.ShowDescription1Text("이제 가게가 깔끔해졌어요!");
        yield return _uiDescriptionNPC.ShowDescription1Text("그러면 이제 손님을 불러야겠죠?");
        _uiTutorial.PunchHoleSetActive(true);
        _uiTutorial.AddCustomerButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription1Text($"오른쪽 하단에 \"{Utility.SetStringColor("호출버튼", ColorType.Positive)}\" 을 눌러볼까요?");
        UserInfo.GiveRecipe("FOOD01");
        _uiTutorial.AddCustomerHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        _uiTutorial.AddCustomerButtonSetActive(false);
        _uiTutorial.AddCustomerHoleSetActive(false);
        yield return YieldCache.WaitForSeconds(5);
        yield return _uiDescriptionNPC.ShowDescription1Text("손님이 도착했어요!");


        _uiTutorial.CustomerGuideButtonSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription2Text($"카운터 위에 \"{Utility.SetStringColor("벨 버튼", ColorType.Positive)}\"을 눌러서 손님을 안내해볼까요?");
        _uiTutorial.CustomerGuideHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return YieldCache.WaitForSeconds(8);

        _uiTutorial.OrderButtonSetActive(true);
        _uiTutorial.OrderHoleSetActive(false);
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription3Text("손님이 주문을 했어요!");
        yield return _uiDescriptionNPC.ShowDescription3Text("음식을 눌러서\n주문을 받아보세요!");
        _uiTutorial.OrderHoleSetActive(true);
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);

        yield return _uiDescriptionNPC.ShowDescription3Text("주문된 음식은 자동으로\n주방으로 배정됩니다.");
        yield return _uiDescriptionNPC.ShowDescription3Text("주방으로 가볼까요?");
        _uiTutorial.CookTimerSetActive(true);
        _cameraController.MoveCamera(RestaurantType.Kitchen);
        yield return YieldCache.WaitForSeconds(3);

        yield return _uiDescriptionNPC.ShowDescription1Text("조리는 시간이 지나면\n자동으로 완료돼요.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"주방장이나 유저가 직접 터치하면\n{Utility.SetStringColor("조리시간을 단축", ColorType.Positive)} 시킬 수 있습니다.");
        _cameraController.MoveCamera(RestaurantType.Hall);
        yield return YieldCache.WaitForSeconds(1);
       _uiTutorial.CookTimerSetActive(false);
        _uiTutorial.ServingButtonSetActive(true);
        _uiTutorial.ServingHoleSetActive(true);
        yield return _uiDescriptionNPC.ShowDescription1Text($"{Utility.SetStringColor("서빙 버튼", ColorType.Positive)}을 눌러서 음식을 손님에게 전달해보세요!");
        while (!_uiTutorial.IsButtonClicked)
            yield return YieldCache.WaitForSeconds(0.02f);
        _uiTutorial.ServingHoleSetActive(false);
        _uiTutorial.ServingButtonSetActive(false);

        yield return YieldCache.WaitForSeconds(8);
        yield return _uiDescriptionNPC.ShowDescription1Text("손님이 만족한 것 같아요!");
        yield return _uiDescriptionNPC.ShowDescription1Text($"손님이 떠난 자리에 {Utility.SetStringColor("코인과 쓰레기", ColorType.Positive)}를 치워야해요.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"바닥의 쓰레기는 \"{Utility.SetStringColor("가게 만족도", ColorType.Positive)}\"를 떨어트려요.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"만족도가 낮으면, {Utility.SetStringColor("예민한 손님", ColorType.Positive)}은 그냥\n떠나버린답니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("테이블 위에 그릇도 치워야 해요!");
        yield return _uiDescriptionNPC.ShowDescription1Text("치운 그릇은 주방에 싱크대로 모입니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("싱크대가 꽉차면 더 이상 그릇을 치울 수 없어요.");
        yield return _uiDescriptionNPC.ShowDescription1Text("주의해서 싱크대를 확인해주세요!");
        yield return _uiDescriptionNPC.ShowDescription1Text($"터치로 {Utility.SetStringColor("테이블에 그릇", ColorType.Positive)}을 치워야 손님을 받을 수 있습니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("이것으로 기본 튜토리얼을 마무리 하겠습니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("당신만의 레스토랑을 운영해보세요!");


        // yield return _uiDescriptionNPC.ShowDescription2Text("훌륭해요!");
        // yield return _uiDescriptionNPC.ShowDescription2Text("아직은 좀 심심하죠?");
        // yield return _uiDescriptionNPC.ShowDescription2Text("역시 식당이라 부르기 힘든 모습이네요.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("우선 테이블을 설치해 볼까요?");
        // _uiTutorial.ShopButtonSetActive(true);
        // _uiTutorial.PunchHoleSetActive(true);
        // yield return _uiDescriptionNPC.ShowDescription2Text("먼저 상점으로 들어가 테이블을 구매해 주세요.");
        // _uiTutorial.ShopMaskSetActive(true);

        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // _uiNav.Push("UITutorial");
        // _uiTutorial.ShopButtonSetActive(false);
        // _uiTutorial.ShopMaskSetActive(false);
        // _uiTutorial.PunchHoleSetActive(false);

        // _uiTutorial.SetTableHoleTargetObjectName("FurnitureTabSlot1");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.PunchHoleSetActive(true);
        // _uiTutorial.TableHoleSetActive(true);
        
        // while(!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // _uiNav.Push("UITutorial");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.BuyHoleSetActive(true);
        // _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        // while(!UserInfo.IsGiveFurniture(EStage.Stage1, "TABLE01_01"))
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.BuyHoleSetActive(true);
        // _uiTutorial.SetBuyHoleTargetObjectName("Equip Button");
        // FurnitureData table1Data = FurnitureDataManager.Instance.GetFurnitureData("TABLE01_01");

        // while (!UserInfo.IsEquipFurniture(EStage.Stage1, ERestaurantFloorType.Floor1, table1Data))
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiNav.Push("UITutorialDescription");
        // yield return _uiDescriptionNPC.ShowDescription2Text("다음으로 음식을 조리하기 위해 조리 기구를 설치해 볼까요?");
        // yield return _uiDescriptionNPC.ShowDescription2Text("주방 카테고리로 이동해 조리 기구를 구매해 주세요!");
        // yield return YieldCache.WaitForSeconds(1);

        // _uiNav.Push("UITutorial");
        // _uiTutorial.BackHoleSetActive(true);
        // while(!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.KitchenHoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // _uiTutorial.SetTableHoleTargetObjectName("KitchenTabSlot1");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.TableHoleSetActive(true);
        // while(!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // _uiNav.Push("UITutorial");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.BuyHoleSetActive(true);
        // _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        // while (!UserInfo.IsGiveKitchenUtensil(EStage.Stage1, "COOKER01_01"))
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.BuyHoleSetActive(true);
        // _uiTutorial.SetBuyHoleTargetObjectName("Equip Button");
        // KitchenUtensilData kitchenData = KitchenUtensilDataManager.Instance.GetKitchenUtensilData("COOKER01_01");
        // while (!UserInfo.IsEquipKitchenUtensil(EStage.Stage1, ERestaurantFloorType.Floor1, kitchenData))
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiNav.Push("UITutorialDescription");
        // yield return _uiDescriptionNPC.ShowDescription2Text("잘하셨습니다! \n손님을 맞이할 준비를 끝내셨네요!");
        // yield return _uiDescriptionNPC.ShowDescription2Text("이제 식당을 한번 확인해 볼까요?");

        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.ExitHoleSetActive(true);
        // _table1.SetActive(false);
        // _burner1.SetActive(false);
        // _camera.transform.position = _kitchenCameraPos.position;
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1.5f);
        // _camera.TweenMove(_restaurantCameraPos.position, 5, Ease.Smoothstep);
        // yield return YieldCache.WaitForSeconds(1f);
        // _burner1.SetActive(true);
        // _burner1BoomParticle.Emit(1);
        // yield return YieldCache.WaitForSeconds(2.5f);
        // _table1.SetActive(true);
        // _table1BoomParticle.Emit(1);
        // yield return YieldCache.WaitForSeconds(4f);

        // yield return _uiDescriptionNPC.ShowDescription1Text("이제 가게가 깔끔해졌어요!");
        // yield return _uiDescriptionNPC.ShowDescription1Text("그러면 이제 손님을 불러야겠죠?");
        // _uiTutorial.AddCustomerButtonSetActive(true);
        // yield return _uiDescriptionNPC.ShowDescription1Text($"오른쪽 하단에 \"{Utility.SetStringColor("호출버튼", ColorType.Positive)}\" 을 눌러볼까요?");
        // _uiTutorial.AddCustomerHoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // _uiTutorial.AddCustomerButtonSetActive(false);
        // _uiTutorial.AddCustomerHoleSetActive(false);
        // yield return YieldCache.WaitForSeconds(5);
        // yield return _uiDescriptionNPC.ShowDescription1Text("손님이 도착했어요!");

        // _uiTutorial.CustomerGuideButtonSetActive(true);
        // yield return _uiDescriptionNPC.ShowDescription2Text("카운터에서 빈 테이블로 안내해 주세요!");
        // _uiTutorial.CustomerGuideHoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(8);
        // _uiTutorial.OrderButtonSetActive(true);
        // yield return YieldCache.WaitForSeconds(1);
        // yield return _uiDescriptionNPC.ShowDescription2Text("손님이 주먹밥을 주문하셨네요!");
        // yield return _uiDescriptionNPC.ShowDescription2Text("레시피를 배우지 않은 상태에선 주문을 받을 수 없습니다.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("모르는 레시피의 주문을 받으면 손님은 장난을 치는거라고 느끼겠죠?");
        // _uiTutorial.ShopButtonSetActive(true);
        // yield return _uiDescriptionNPC.ShowDescription2Text("불쾌해진 손님이 식당을 나가기전에 레시피를 배워봅시다!");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.ShopMaskSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // _uiNav.Push("UITutorial");
        // _uiTutorial.OrderButtonSetActive(false);
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.RecipeHoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.BuyHoleSetActive(true);
        // _uiTutorial.SetBuyHoleTargetObjectName("Buy Button");
        // while (!UserInfo.IsGiveRecipe("FOOD01"))
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1);
        // _uiNav.Push("UITutorialDescription");
        // yield return _uiDescriptionNPC.ShowDescription2Text("잘하셨습니다! \n이걸로 주방에서 주먹밥을 제작할 수 있게 되었어요!");
        // yield return _uiDescriptionNPC.ShowDescription2Text("조리 기구의 효율에 따라 음식 제작 시간이 줄어든답니다.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("더 많은 손님을 만족시키기 위해선 주방을 업그레이드할 필요가 있겠죠?");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.ExitHoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1f);
        // _uiTutorial.OrderButtonSetActive(true);
        // _uiNav.Push("UITutorialDescription");
        // yield return _uiDescriptionNPC.ShowDescription2Text("자 이제 주문을 받아봅시다.");
        // yield return YieldCache.WaitForSeconds(1f);
        // _uiTutorial.OrderHoleSetActive(true);

        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(2f);
        // _uiTutorial.SetOrderHoleTargetObjectName("Tutorial Serving Button");
        // _uiTutorial.ServingButtonSetActive(true);
        // yield return YieldCache.WaitForSeconds(1f);
        // yield return _uiDescriptionNPC.ShowDescription2Text("주먹밥이 완성되었군요!");
        // yield return _uiDescriptionNPC.ShowDescription2Text("직접 손님에게 음식을 전달해봅시다.");
        // yield return YieldCache.WaitForSeconds(1);
        // _uiTutorial.OrderHoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(4.5f);
        // yield return _uiDescriptionNPC.ShowDescription2Text("첫 손님이 만족하신 것 같네요!");
        // yield return _uiDescriptionNPC.ShowDescription2Text("손님이 있던 자리에 쓰레기도 치워줘야해요.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("쓰레기가 계속 쌓이면 다른 손님들이 불쾌감을 느낍니다.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("불쾌해진 손님이 식당을 나가기전에 쓰레기를 치워줍시다!");
        // _uiTutorial.Table1HoleSetActive(true);
        // while (!_uiTutorial.IsButtonClicked)
        //     yield return YieldCache.WaitForSeconds(0.02f);

        // yield return YieldCache.WaitForSeconds(1f);
        // yield return _uiDescriptionNPC.ShowDescription2Text("수고하셨어요. 새싹 점장님! \n가장 기초적인 안내는 끝났습니다.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("처음에는 걱정했는데, 생각보다 잘 따라오시네요.");
        // yield return _uiDescriptionNPC.ShowDescription2Text("그럼, 앞으로 이 가게를 잘 부탁드립니다!");
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

        _touchCount += 1;
        for(int i = 0, cnt = _boomParticles.Length; i < cnt; ++i)
        {
            _boomParticles[i].Emit(1);
        }
    }


    private void OnSkipButtonClicked()
    {
        UserInfo.GiveRecipe("FOOD01");
        _uiTutorial.PopEnabled = true;
        _uiDescriptionNPC.PopEnabled = true;
        _uiNav.Pop("UITutorial");
        _uiNav.Pop("UITutorialDescription");
        _uiTutorial.PopEnabled = false;
        _uiDescriptionNPC.PopEnabled = false;
        UserInfo.IsTutorialStart = false;
        UserInfo.IsFirstTutorialClear = true;
        _mainSceneUI.gameObject.SetActive(true);
        _table1.SetActive(true);
        _burner1.SetActive(true);
        gameObject.SetActive(false);
    }

}
