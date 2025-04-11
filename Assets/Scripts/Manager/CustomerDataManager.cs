using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CustomerDataManager : MonoBehaviour
{
    public static CustomerDataManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("CustomerDataManager");
                _instance = obj.AddComponent<CustomerDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static CustomerDataManager _instance;

    private static List<CustomerData> _customerDataList = new List<CustomerData>();
    private static List<NormalCustomerData> _normalCustomerDataList = new List<NormalCustomerData>();
    private static List<SpecialCustomerData> _specialCustomerDataList = new List<SpecialCustomerData>();
    private static List<GatecrasherCustomerData> _gatecrasherCustomerDataList = new List<GatecrasherCustomerData>();
    private static Dictionary<string, CustomerData> _customerDataDic = new Dictionary<string, CustomerData>();


    private static Dictionary<string, Sprite> _customerSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _specialCustomerTouchSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, RuntimeAnimatorController> _gatecrasherCustomerAnimatorDic = new Dictionary<string, RuntimeAnimatorController>();
    private static RuntimeAnimatorController _normalCustomerAnimator;

    private static Dictionary<string, CustomerSkill> _customerSkillDic = new Dictionary<string, CustomerSkill>();

    public CustomerData GetCustomerData(string id)
    {
        if (!_customerDataDic.TryGetValue(id, out CustomerData data))
            throw new System.Exception("해당 id값이 존재하지 않습니다.");

        return data;
    }

    public List<CustomerData> GetCustomerDataList()
    {
        return _customerDataList;
    }


    public List<CustomerData> GetAppearNormalCustomerList()
    {
        List<CustomerData> returnList = new List<CustomerData>();
        for(int i = 0, cnt = _normalCustomerDataList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetCustomerEnableState(_customerDataList[i].Id))
                continue;

            returnList.Add(_normalCustomerDataList[i]);
        }

        return returnList;
    }

    public List<SpecialCustomerData> GetAppearSpecialCustomerDataList()
    {
        List<SpecialCustomerData> returnList = new List<SpecialCustomerData>();
        for (int i = 0, cnt = _specialCustomerDataList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetCustomerEnableState(_specialCustomerDataList[i].Id))
                continue;

            returnList.Add(_specialCustomerDataList[i]);
        }

        return returnList;
    }

    public List<GatecrasherCustomerData> GetAppearGatecrasherCustomerDataList()
    {
        List<GatecrasherCustomerData> returnList = new List<GatecrasherCustomerData>();
        for (int i = 0, cnt = _gatecrasherCustomerDataList.Count; i < cnt; ++i)
        {
            if (!UserInfo.GetCustomerEnableState(_gatecrasherCustomerDataList[i].Id))
                continue;

            returnList.Add(_gatecrasherCustomerDataList[i]);
        }

        return returnList;
    }


    public List<CustomerData> GetSortCustomerList()
    {
        return UserInfo.CustomerSortType switch
        {
            GradeSortType.NameAscending => _customerDataList.OrderBy(data => data.Name).ToList(),
            GradeSortType.NameDescending => _customerDataList.OrderByDescending(data => data.Name).ToList(),
            GradeSortType.GradeAscending => _customerDataList.OrderBy(data => data.Name).ToList(),
            GradeSortType.GradeDescending => _customerDataList.OrderByDescending(data => data.Name).ToList(),
            GradeSortType.None => _customerDataList,
            _ => null
        };
    }


    private static void CheckEnableCustomer()
    {
        for (int i = 0, cnt = _customerDataList.Count; i < cnt; ++i)
        {
            if (_customerDataList[i] == null || string.IsNullOrWhiteSpace(_customerDataList[i].Id))
                continue;

            if (UserInfo.GetCustomerEnableState(_customerDataList[i].Id))
                continue;

            CustomerVisitState state = UserInfo.GetCustomerVisitState(_customerDataList[i]);

            if (!state.IsScoreValid || !state.IsGiveRecipe || !state.IsGiveItem)
                continue;

            DebugLog.Log(_customerDataList[i].Name + " 활성화");
            UserInfo.CustomerEnabled(_customerDataList[i]);
            UserInfo.AddNotification(_customerDataList[i].Id);

        }
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(_instance);
        Init();
    }


    private static void Init()
    {
        _customerDataDic.Clear();
        _customerDataList.Clear();
        _specialCustomerDataList.Clear();
        _gatecrasherCustomerDataList.Clear();

        LoadNormalCustomerSprites();
        LoadSpecialCustomerSprites();
        LoadGatecrasherCustomerSprites();
        LoadGatecrasherCustomerAnimator();
        LoadCustomerSkill();

        NormalCustomerDataParse("CustomerData/CSVData/NormalCustomerData");
        SpecialCustomerDataParse("CustomerData/CSVData/SpecialCustomerData");
        GatecrasherCustomerDataParse("CustomerData/CSVData/GatecrasherCustomerData");

        UserInfo.OnChangeMoneyHandler += CheckEnableCustomer;
        UserInfo.OnGiveGachaItemHandler += CheckEnableCustomer;
        UserInfo.OnGiveRecipeHandler += CheckEnableCustomer;
        GameManager.Instance.OnChangeScoreHandler += CheckEnableCustomer;
    }

    private static void LoadNormalCustomerSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("CustomerData/Sprites/NormalCustomer");

        foreach (var sprite in sprites)
        {
            string key = Utility.CutStringUpToChar(sprite.name, '_').ToUpper();
            // 중복 체크
            if (_customerSpriteDic.ContainsKey(key))
            {
                Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                continue;
            }
            _customerSpriteDic.Add(key, sprite);
            DebugLog.Log(key);
        }
    }

    private static void LoadSpecialCustomerSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("CustomerData/Sprites/SpecialCustomer");

        foreach (var sprite in sprites)
        {
            string key = Utility.CutStringUpToChar(sprite.name, '_').ToUpper();
            string afterUnderStr = Utility.GetStringAfterChar(sprite.name, '_'); // 언더바 이후 문자열
            bool isTouch = afterUnderStr.Contains("터치") || afterUnderStr.Contains("반응");

            if(isTouch)
            {
                if(_specialCustomerTouchSpriteDic.ContainsKey(key))
                {
                    Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                    continue;
                }
                _specialCustomerTouchSpriteDic.Add(key, sprite);
            }
            else
            {
                if (_customerSpriteDic.ContainsKey(key))
                {
                    Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                    continue;
                }
                _customerSpriteDic.Add(key, sprite);
            }
 
            DebugLog.Log(key);
        }
    }


    private static void LoadGatecrasherCustomerSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("CustomerData/Sprites/GatecrasherCustomer");

        foreach (var sprite in sprites)
        {
            string key = Utility.CutStringUpToChar(sprite.name, '_').ToUpper();

                if (_customerSpriteDic.ContainsKey(key))
                {
                    Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                    continue;
                }
                _customerSpriteDic.Add(key, sprite);
            

            DebugLog.Log(key);
        }
    }

    private static void LoadGatecrasherCustomerAnimator()
    {
        // 스프라이트 리소스 로드
        RuntimeAnimatorController[] animators = Resources.LoadAll<RuntimeAnimatorController>("CustomerData/Animator/GatecrasherCustomer");

        foreach (var animator in animators)
        {
            string key = Utility.CutStringUpToChar(animator.name, '_').ToUpper();
            if(key.Contains("NORMAL"))
            {
                _normalCustomerAnimator = animator;
            }
            else
            {
                if (_gatecrasherCustomerAnimatorDic.ContainsKey(key))
                {
                    Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                    continue;
                }
                _gatecrasherCustomerAnimatorDic.Add(key, animator);
            }
           
            DebugLog.Log(key);
        }
    }

    private static void LoadCustomerSkill()
    {
        CustomerSkill[] skills = Resources.LoadAll<CustomerSkill>("CustomerData/Skill");
        foreach(var skill in skills)
        {
            string key = Utility.CutStringUpToChar(skill.name, '_').ToUpper();
            if (_customerSkillDic.ContainsKey(key))
            {
                Debug.LogWarning($"중복된 스프라이트 키가 있습니다: {key}");
                continue;
            }
            _customerSkillDic.Add(key, skill);
        }
    }


    private static void NormalCustomerDataParse(string loadPath)
    {
        // ? Resources 폴더에서 CSV 데이터 불러오기
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        for (int i = 1; i < data.Length; i++) // 첫 번째 줄은 헤더라서 건너뜀
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                break;
            }
            DebugLog.Log("CSV 데이터:" + id);
            string name = row[1].Trim();
            string attribute = row[2].Trim(); //사용하지 않음

            string description = row[3].Trim();
            float moveSpeed = float.TryParse(row[4].Trim(), out float speed) ? speed : 6f;
            int waitTime = int.TryParse(row[5].Trim(), out int wait) ? wait : throw new System.Exception("대기 시간이 이상합니다:" + row[5].Trim());
            int orderWaitTime = int.TryParse(row[6].Trim(), out int orderWait) ? orderWait : throw new System.Exception("음식 대기 시간이 이상합니다:" + row[6].Trim());
            CustomerTendencyType tendencyType = GetTendencyType(row[7].Trim());
            string requiredFood = row[8].Trim();
            string visitCount100Food = row[9].Trim();
            string visitCount200Food = row[10].Trim();
            string visitCount300Food = row[11].Trim();
            string visitCount400Food = row[12].Trim();
            string visitCount500Food = row[13].Trim();


            int visitMinScore = int.TryParse(row[14].Trim(), out int minScore) ? minScore : 0;
            string visitGiveFurnitureId = row[15].Trim(); //사용하지 않음
            string requiredItem = row[16].Trim();
            CustomerSkill skill = GetCustomerSkill(row[17].Trim().ToUpper());
            if (!_customerSpriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            NormalCustomerData customerData = new NormalCustomerData(sprite, id, name, description, moveSpeed, visitMinScore, requiredFood, requiredItem, visitCount100Food, visitCount200Food, visitCount300Food, visitCount400Food, visitCount500Food, tendencyType, orderWaitTime, waitTime, skill);

            _customerDataDic.Add(id, customerData);
            _customerDataList.Add(customerData);
            _normalCustomerDataList.Add(customerData);
        }
    }


    private static void SpecialCustomerDataParse(string loadPath)
    {
        // ? Resources 폴더에서 CSV 데이터 불러오기
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        for (int i = 1; i < data.Length; i++) // 첫 번째 줄은 헤더라서 건너뜀
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                break;
            }
            DebugLog.Log("CSV 데이터:" + id);
            string name = row[1].Trim();
            string attribute = row[2].Trim(); //사용하지 않음

            string description = row[3].Trim();
            float moveSpeed = float.TryParse(row[4].Trim(), out float speed) ? speed : 6f;
            string requiredDish = row[5].Trim();
            int visitMinScore = int.TryParse(row[6].Trim(), out int minScore) ? minScore : 0;
            string visitGiveFurnitureId = row[7].Trim(); //사용하지 않음
            string requiredItem = row[8].Trim();
            float spawnChance = float.Parse(row[9].Trim());
            int activeDuration = int.Parse(row[10].Trim());
            int touchCount = int.Parse(row[11].Trim());
            int touchAddMoney = int.Parse(row[12].Trim());

            if (!_customerSpriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if(!_specialCustomerTouchSpriteDic.TryGetValue(id, out Sprite touchSprite))
            {
                Debug.LogError($"터치 스프라이트가 없습니다: {id}");
                touchSprite = sprite;
            }

            SpecialCustomerData customerData = new SpecialCustomerData(sprite, touchSprite, id, name, description, moveSpeed, visitMinScore, requiredDish, requiredItem, activeDuration, touchCount, touchAddMoney, spawnChance);

            _customerDataDic.Add(id, customerData);
            _customerDataList.Add(customerData);
            _specialCustomerDataList.Add(customerData);
        }
    }


    private static void GatecrasherCustomerDataParse(string loadPath)
    {
        // ? Resources 폴더에서 CSV 데이터 불러오기
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        for (int i = 1; i < data.Length; i++) // 첫 번째 줄은 헤더라서 건너뜀
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                break;
            }
            DebugLog.Log("CSV 데이터:" + id);
            string name = row[1].Trim();
            string attribute = row[2].Trim();

            string description = row[3].Trim();
            float moveSpeed = float.TryParse(row[4].Trim(), out float speed) ? speed : 6f;
            string requiredDish = row[5].Trim();
            int visitMinScore = int.TryParse(row[6].Trim(), out int minScore) ? minScore : 0;
            string visitGiveFurnitureId = row[7].Trim(); //사용하지 않음
            string requiredItem = row[8].Trim();
            float spawnChance = float.Parse(row[9].Trim());
            int activeDuration = int.Parse(row[10].Trim());
            int touchCount = int.Parse(row[11].Trim());

            if (!_customerSpriteDic.TryGetValue(id, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_gatecrasherCustomerAnimatorDic.TryGetValue(id, out RuntimeAnimatorController animator))
            {
                Debug.LogError($"애니메이션이 없습니다: {id}");
                continue;
            }


            GatecrasherCustomerData customerData = null;
            if(attribute.Contains("진상1"))
            {
                GatecrasherCustomer1Data gatecrasherCustomer1Data = new GatecrasherCustomer1Data(sprite, id, name, description, moveSpeed, visitMinScore, requiredDish, requiredItem, activeDuration, touchCount, spawnChance, _normalCustomerAnimator, animator);
                customerData = gatecrasherCustomer1Data;
            }
            else if(attribute.Contains("진상2"))
            {
                GatecrasherCustomer2Data gatecrasherCustomer2Data = new GatecrasherCustomer2Data(sprite, id, name, description, moveSpeed, visitMinScore, requiredDish, requiredItem, activeDuration, touchCount, spawnChance, animator);
                customerData = gatecrasherCustomer2Data;
            }
            
            if(customerData == null)
            {
                Debug.LogError($"속성 타입이 이상합니다: (Id: {id}, 속성: {attribute})");
                continue;
            }

            _customerDataDic.Add(id, customerData);
            _customerDataList.Add(customerData);
            _gatecrasherCustomerDataList.Add(customerData);
        }
    }


    private static CustomerTendencyType GetTendencyType(string type)
    {
        return type.Trim() switch
        {
            "무난한 손님" => CustomerTendencyType.Normal,
            "예민한 손님" => CustomerTendencyType.Sensitive,
            "초예민 손님" => CustomerTendencyType.HighlySensitive,
            _ => throw new System.Exception("잘못된 타입입니다." + type)
        };
    }


    private static CustomerSkill GetCustomerSkill(string skillType)
    {
        if(_customerSkillDic.TryGetValue(skillType, out CustomerSkill skill))
        {
            return skill;
        }
        Debug.Log(skillType + " 스킬이 없습니다.");
        return null;
    }


    private void OnDestroy()
    {
        UserInfo.OnChangeMoneyHandler -= CheckEnableCustomer;
        UserInfo.OnGiveGachaItemHandler -= CheckEnableCustomer;
        UserInfo.OnGiveRecipeHandler -= CheckEnableCustomer;
        GameManager.Instance.OnChangeScoreHandler -= CheckEnableCustomer;
    }
}
