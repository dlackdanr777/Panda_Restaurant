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
    [SerializeField] private TableFurniture _table1;
    [SerializeField] private SinkKitchenUtensil _sink;
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
    [SerializeField] private AudioClip _cleaningSound;

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
        yield return _uiDescriptionNPC.ShowDescription1Text($"주방장이나 유저가 직접 터치하면");
        yield return _uiDescriptionNPC.ShowDescription1Text($"{Utility.SetStringColor("조리시간을 단축", ColorType.Positive)} 시킬 수 있습니다.");
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
        yield return _uiDescriptionNPC.ShowDescription1Text($"손님이 떠난 자리에 {Utility.SetStringColor("코인과 쓰레기", ColorType.Positive)}를 치워볼까요?");
        yield return _uiDescriptionNPC.ShowDescription1Text($"먼저 테이블을 치워주세요.");
        _uiTutorial.CustomHoleSetActive(true, 400, _table1.name, _table1.transform);
        bool isTableTouched = false;
        _table1.OnTouchEventHandler += () => isTableTouched = true;
        while (!isTableTouched)
        {
            if(!_uiTutorial.GetCustomHoleActive())
                _uiTutorial.CustomHoleSetActiveImmediate(true, 400, _table1.name, _table1.transform);
             yield return YieldCache.WaitForSeconds(0.02f);
        }
        _uiTutorial.CustomHoleHide();
        _table1.OnTouchEventHandler -= () => isTableTouched = true;

        yield return _uiDescriptionNPC.ShowDescription1Text($"다음으로 바닥의 쓰레기를 치워볼까요?");
        {
            var pool = ObjectPoolManager.Instance.GetEnabledGarbagePool();
            if (pool.Count > 0)
                _uiTutorial.CustomHoleSetActive(true, 200, pool[0].gameObject.name, pool[0].gameObject.transform);
        }
        while (ObjectPoolManager.Instance.GetEnabledGarbageCount() > 0)
        {
            if (!_uiTutorial.GetCustomHoleActive())
            {
                var pool = ObjectPoolManager.Instance.GetEnabledGarbagePool();
                if (pool.Count > 0)
                    _uiTutorial.CustomHoleSetActiveImmediate(true, 200, pool[0].gameObject.name, pool[0].gameObject.transform);
            }
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _uiTutorial.CustomHoleHide();

        {
            var pool = ObjectPoolManager.Instance.GetEnabledCoinPool();
            yield return _uiDescriptionNPC.ShowDescription1Text($"마지막으로 바닥의 동전을 치워볼까요?");
            if (pool.Count > 0)
                _uiTutorial.CustomHoleSetActive(true, 200, pool[0].gameObject.name, pool[0].gameObject.transform);
        }
        while (ObjectPoolManager.Instance.GetEnabledCoinCount() > 0)
        {
            if (!_uiTutorial.GetCustomHoleActive())
            {
                var pool = ObjectPoolManager.Instance.GetEnabledCoinPool();
                if (pool.Count > 0)
                    _uiTutorial.CustomHoleSetActiveImmediate(true, 200, pool[0].gameObject.name, pool[0].gameObject.transform);
            }
            yield return YieldCache.WaitForSeconds(0.02f);
        }
        _uiTutorial.CustomHoleHide();
            
         
yield return YieldCache.WaitForSeconds(1);

        yield return _uiDescriptionNPC.ShowDescription1Text($"바닥의 쓰레기는 \"{Utility.SetStringColor("가게 만족도", ColorType.Positive)}\"를 떨어트려요.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"만족도가 낮으면, {Utility.SetStringColor("예민한 손님", ColorType.Positive)}은 그냥\n떠나버린답니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"{Utility.SetStringColor("그릇을 치우지 않은 테이블에는 손님을 받을 수가 없어요.", ColorType.Positive)}");
        yield return _uiDescriptionNPC.ShowDescription1Text("정리하신 테이블 위의 그릇은 싱크대로 모입니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("싱크대로 가볼까요?");
        _cameraController.MoveCamera(RestaurantType.Kitchen);
        yield return YieldCache.WaitForSeconds(2);

        yield return _uiDescriptionNPC.ShowDescription1Text($"식당에서 치운 그릇은 싱크대에 쌓이게 됩니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"싱크대가 꽉차면 그릇을 치울 수 없으니 그 전에 그릇을 치워주세요.");
        yield return _uiDescriptionNPC.ShowDescription1Text($"싱크대를 꾹 누르면 설거지를 할 수 있어요.");

        while (0 < _sink.GetSinkBowlCount())
        {
            _sink.TouchDownEvent();
            DebugLog.Log(_sink.GetSinkBowlCount());
            yield return YieldCache.WaitForSeconds(0.01f);
        }
        _sink.TouchUpEvent();
        yield return YieldCache.WaitForSeconds(1);
        yield return _uiDescriptionNPC.ShowDescription1Text($"완벽합니다!");
        _cameraController.MoveCamera(RestaurantType.Hall);
        yield return YieldCache.WaitForSeconds(2);   
        yield return _uiDescriptionNPC.ShowDescription1Text("이것으로 기본 튜토리얼을 마무리 하겠습니다.");
        yield return _uiDescriptionNPC.ShowDescription1Text("모두의 레스토랑을 즐겁게 운영해봅시다!");


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
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _cleaningSound);
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
        _table1.gameObject.SetActive(true);
        _burner1.SetActive(true);
        gameObject.SetActive(false);
    }

}
