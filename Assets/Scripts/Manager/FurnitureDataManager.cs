using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class FurnitureDataManager : MonoBehaviour
{
    public static FurnitureDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("FurnitureDataManager");
                _instance = obj.AddComponent<FurnitureDataManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }
    private static FurnitureDataManager _instance;

    private static List<FurnitureData> _furnitureDataList = new List<FurnitureData>();
    private static List<FurnitureData>[] _furnitureDataListType = new List<FurnitureData>[(int)FurnitureType.Length];
    private static Dictionary<string, FurnitureData> _furnitureDataDic = new Dictionary<string, FurnitureData>();

    private static Dictionary<string, Sprite> _spriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _thumbnailSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _leftChairSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _rightChairSpriteDic = new Dictionary<string, Sprite>();

    private static Dictionary<string, Sprite> _leftChairArmrestSpriteDic = new Dictionary<string, Sprite>();
    private static Dictionary<string, Sprite> _rightChairArmrestSpriteDic = new Dictionary<string, Sprite>();

    private static Dictionary<string, List<Sprite>> _animationSpriteDic = new Dictionary<string, List<Sprite>>();

    public FurnitureData GetFurnitureData(string id)
    {
        if (!_furnitureDataDic.TryGetValue(id, out FurnitureData data))
            throw new System.Exception($"해당 ID가 존재하지 않습니다: {id}");

        return data;
    }

    public List<FurnitureData> GetFurnitureDataList()
    {
        return _furnitureDataList;
    }

    public List<FurnitureData> GetFurnitureDataList(FurnitureType type)
    {
        return _furnitureDataListType[(int)type];
    }

    public List<FurnitureData> GetSortFurnitureDataList(FurnitureType type)
    {
        return UserInfo.FurnitureSortType switch
        {
            ShopSortType.NameAscending => _furnitureDataListType[(int)type].OrderBy(data => data.Name).ToList(),
            ShopSortType.NameDescending => _furnitureDataListType[(int)type].OrderByDescending(data => data.Name).ToList(),
            ShopSortType.PriceAscending => ShopItemSort.SortByPrice(_furnitureDataListType[(int)type], true),
            ShopSortType.PriceDescending => ShopItemSort.SortByPrice(_furnitureDataListType[(int)type], false),
            ShopSortType.None => _furnitureDataListType[(int)type],
            _ => null
        };
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        InitData();
    }

    private static void InitData()
    {
        _furnitureDataDic.Clear();
        _furnitureDataList.Clear();
        _spriteDic.Clear();
        _thumbnailSpriteDic.Clear();
        _leftChairSpriteDic.Clear();
        _rightChairSpriteDic.Clear();

        for (int i = 0; i < (int)FurnitureType.Length; i++)
        {
            _furnitureDataListType[i] = new List<FurnitureData>();
        }

        LoadFurnitureSprites("FurnitureData/Sprites");
        LoadAnimationSprites();

        string basePath = "FurnitureData/CSVData/";
        TableDataParse(basePath + "TableData", EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "CounterData", FurnitureType.Counter, EquipEffectType.AddMaxTip);
        FurnitureDataParse(basePath + "RackData", FurnitureType.Rack, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "FrameData", FurnitureType.Frame, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "FlowerData", FurnitureType.Flower, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "AccData", FurnitureType.Acc, EquipEffectType.AddTipPerMinute);
        FurnitureDataParse(basePath + "WallpaperData", FurnitureType.Wallpaper, EquipEffectType.AddTipPerMinute);
    }


    private static void LoadFurnitureSprites(string basePath)
    {
        string[] setFolders = { "01", "02", "03", "04", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19" };

        foreach (string setFolder in setFolders)
        {
            string fullPath = $"{basePath}/{setFolder}";
            Sprite[] sprites = Resources.LoadAll<Sprite>(fullPath);

            foreach (var sprite in sprites)
            {
                string spriteName = sprite.name;
                string key = Utility.CutStringUpToChar(spriteName, '_'); // 기본 키 생성
                string afterUnderStr = Utility.GetStringAfterChar(spriteName, '_'); // 언더바 이후 문자열
                bool isThumbnail = spriteName.Contains("썸네일"); // 썸네일 여부 확인
                bool isChair = afterUnderStr.Contains("의자") || afterUnderStr.Contains("체어");
                //DebugLog.Log(spriteName);
                if (isThumbnail)
                {
                    _thumbnailSpriteDic.Add(key, sprite);
                }

                else if (isChair)
                {
                    if (afterUnderStr.Contains("L") || spriteName.Contains("좌"))
                    {
                        if (afterUnderStr.Contains("팔걸이"))
                        {
                            _leftChairArmrestSpriteDic.Add(key, sprite);
                        }
                        else
                        {
                            _leftChairSpriteDic.Add(key, sprite);
                        }
                    }

                    else if (afterUnderStr.Contains("R") || spriteName.Contains("우"))
                    {
                        if (afterUnderStr.Contains("팔걸이"))
                        {
                            _rightChairArmrestSpriteDic.Add(key, sprite);
                        }
                        else
                        {
                            _rightChairSpriteDic.Add(key, sprite);
                        }
                    }
                    else
                    {
                        _leftChairSpriteDic.Add(key, sprite);
                    }

                }

                else
                {
                    _spriteDic.Add(key, sprite);
                }
            }
        }
    }

    private static void FurnitureDataParse(string loadPath, FurnitureType type, EquipEffectType effectType)
    {
        // 🔹 Resources 폴더에서 CSV 데이터 불러오기
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

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);

            string setId = (row[3].Trim());
            int addScore = int.Parse(row[4].Trim());

            int effectValue = int.Parse(row[5].Trim());
            int unlockScore = int.Parse(row[6].Trim());
            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int price = int.Parse(row[8].Trim());
            UnlockConditionType unlockType = row.Length < 10 ? UnlockConditionType.None : Utility.GetUnlockConditionType(row[9].Trim());
            string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[10].Trim();
            if (unlockType == UnlockConditionType.None || !int.TryParse(row[11].Trim(), out int unlockCount))
            {
                unlockCount = 0;
            }

            string keyId = CutStringUpToChar(id, '_');
            // 🔹 스프라이트 가져오기 (딕셔너리에서 가져오므로 성능 향상)
            if (!_spriteDic.TryGetValue(keyId, out Sprite sprite))
            {
                Debug.LogError($"스프라이트가 없습니다: {id}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(keyId, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            List<Sprite> animationSpriteList = null;
            if (_animationSpriteDic.TryGetValue(keyId, out List<Sprite> animationSprites))
            {
                animationSpriteList = animationSprites;
            }

            // 🔹 FurnitureData 생성
            FurnitureData furnitureData = new FurnitureData(
                sprite,
                thumbnailSprite,
                animationSpriteList,
                id,
                setId,
                name,
                moneyType,
                unlockScore,
                price,
                type,
                foodType,
                addScore,
                effectType, // 엑셀에 해당 데이터가 없다면 기본값 사용
                effectValue,
                unlockType,
                unlockId,
                unlockCount
            );

            // 🔹 리스트 및 딕셔너리에 추가
            _furnitureDataDic.Add(id, furnitureData);
            _furnitureDataList.Add(furnitureData);
            _furnitureDataListType[(int)furnitureData.Type].Add(furnitureData);
        }
    }

    private static void TableDataParse(string loadPath, EquipEffectType effectType)
    {
        // 🔹 Resources 폴더에서 CSV 데이터 불러오기
        TextAsset csvData = Resources.Load<TextAsset>(loadPath);
        if (csvData == null)
        {
            Debug.LogError($"파일을 찾을 수 없습니다: {loadPath}");
            return;
        }

        string[] data = csvData.text.Split('\n');

        for (int i = 1; i < data.Length; i++)
        {
            string[] row = data[i].Split(',');
            string id = row[0].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("Id값이 이상합니다: " + id);
                continue;
            }

            string name = row[1].Trim();
            string attribute = row[2].Trim();
            FoodType foodType = Utility.GetFoodType(attribute);
            string setId = (row[3].Trim());
            DebugLog.Log(row[4].Trim());
            int table1AddScore = int.Parse(row[4].Trim());
            int table1EffectValue = int.Parse(row[5].Trim());

            MoneyType moneyType = row[7].Trim() == "게임 머니" || row[7].Trim() == "코인" ? MoneyType.Gold : MoneyType.Dia;
            int tableBuyScore = int.Parse(row[6].Trim());
            int tableBuyPrice = int.Parse(row[8].Trim());
            FurnitureType tableType = GetTableTypeFromId(id);
            UnlockConditionType unlockType = row.Length < 10 ? UnlockConditionType.None : Utility.GetUnlockConditionType(row[9].Trim());
            string unlockId = unlockType == UnlockConditionType.None ? string.Empty : row[10].Trim();
            if (unlockType == UnlockConditionType.None || !int.TryParse(row[11].Trim(), out int unlockCount))
            {
                unlockCount = 0;
            }

            string keyId = CutStringUpToChar(id, '_');
            // 🔹 스프라이트 가져오기 (딕셔너리에서 가져오므로 성능 향상)
            if (!_spriteDic.TryGetValue(keyId, out Sprite sprite))
            {
                DebugLog.LogError($"스프라이트가 없습니다: {keyId}");
                continue;
            }

            if (!_thumbnailSpriteDic.TryGetValue(keyId, out Sprite thumbnailSprite))
            {
                thumbnailSprite = sprite;
            }

            if (!_leftChairSpriteDic.TryGetValue(keyId, out Sprite leftChairSprite))
            {
                DebugLog.LogError($"좌측 의자 스프라이트가 없습니다: {keyId}");
                continue;
            }

            if (!_rightChairSpriteDic.TryGetValue(keyId, out Sprite rightChairSprite))
            {
                DebugLog.LogError($"우측 의자 스프라이트가 없습니다: {keyId}");
                rightChairSprite = null;
            }

            bool isChairForward = false;
            _leftChairArmrestSpriteDic.TryGetValue(keyId, out Sprite leftChairArmrestSprite);
            _rightChairArmrestSpriteDic.TryGetValue(keyId, out Sprite rightChairArmrestSprite);

            List<Sprite> animationSpriteList = null;
            if (_animationSpriteDic.TryGetValue(keyId, out List<Sprite> animationSprites))
            {
                animationSpriteList = animationSprites;
            }

            FurnitureData tableData = new TableFurnitureData(
                sprite,
                thumbnailSprite,
                animationSpriteList,
                id,
                setId,
                name,
                moneyType,
                tableBuyScore,
                tableBuyPrice,
                tableType,
                foodType,
                table1AddScore,
                effectType,
                table1EffectValue,
                unlockType,
                unlockId,
                unlockCount,
                leftChairSprite,
                rightChairSprite,
                leftChairArmrestSprite,
                rightChairArmrestSprite,
                isChairForward
            );

            _furnitureDataDic.Add(id, tableData);
            _furnitureDataList.Add(tableData);
            _furnitureDataListType[(int)tableData.Type].Add(tableData);

        }
    }
    
    private static void LoadAnimationSprites()
    {
        // 스프라이트 리소스 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>("FurnitureData/Sprites/Animation");

        // ID별로 스프라이트들을 그룹화하기 위한 임시 딕셔너리
        Dictionary<string, List<(Sprite sprite, int index)>> tempSpriteDic = new Dictionary<string, List<(Sprite, int)>>();

        foreach (var sprite in sprites)
        {
            string spriteName = sprite.name.Trim();
            DebugLog.Log($"로딩된 스프라이트 이름: {spriteName}");
            // SKIN_STAFF01-01 형태에서 ID와 인덱스 분리
            string[] parts = spriteName.Split('-');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"스프라이트 이름 형식이 올바르지 않습니다: {spriteName}");
                continue;
            }

            string id = parts[0].Trim(); // SKIN_STAFF01
            if (!int.TryParse(parts[1].Trim(), out int index))
            {
                Debug.LogWarning($"스프라이트 인덱스를 파싱할 수 없습니다: {spriteName}");
                continue;
            }

            // 임시 딕셔너리에 추가
            if (!tempSpriteDic.ContainsKey(id))
            {
                tempSpriteDic[id] = new List<(Sprite, int)>();
            }
            tempSpriteDic[id].Add((sprite, index));
        }

        // 각 ID별로 인덱스 순서대로 정렬하여 최종 딕셔너리에 저장
        foreach (var kvp in tempSpriteDic)
        {
            string id = kvp.Key;
            var spriteList = kvp.Value;
            
            // 인덱스 기준으로 오름차순 정렬
            spriteList.Sort((a, b) => a.index.CompareTo(b.index));
            
            // Sprite 배열로 변환
            Sprite[] sortedSprites = new Sprite[spriteList.Count];
            for (int i = 0; i < spriteList.Count; i++)
            {
                sortedSprites[i] = spriteList[i].sprite;
            }
            
            // 최종 딕셔너리에 저장
            if (_animationSpriteDic.ContainsKey(id))
            {
                Debug.LogWarning($"중복된 스프라이트 ID가 있습니다: {id}");
                continue;
            }

            _animationSpriteDic.Add(id, sortedSprites.ToList());
            DebugLog.Log($"Idle 스프라이트 로드 완료: {id} ({sortedSprites.Length}개)");
            sortedSprites = null;
            spriteList = null;
        }
    }


    private static FurnitureType GetTableTypeFromId(string id)
    {
        try
        {
            // 언더바 다음 문자열 추출
            string afterUnderscore = Utility.GetStringAfterChar(id, '_');

            // 숫자 부분만 추출 (예: "01썸네일" -> "01")
            string numberPart = "";
            for (int i = 0; i < afterUnderscore.Length; i++)
            {
                if (char.IsDigit(afterUnderscore[i]))
                {
                    numberPart += afterUnderscore[i];
                }
                else
                {
                    break; // 숫자가 아닌 문자를 만나면 중단
                }
            }

            if (int.TryParse(numberPart, out int tableNumber))
            {
                int typeIndex = tableNumber - 1;

                if (typeIndex >= 0 && typeIndex <= 4)
                {
                    return (FurnitureType)typeIndex;
                }
            }

            DebugLog.LogError($"id에서 테이블 타입을 파싱할 수 없습니다: {id}, 기본값 Table1 사용");
            return FurnitureType.Table1; // 기본값
        }
        catch (System.Exception e)
        {
            DebugLog.LogError($"id 파싱 중 오류 발생: {id}, 오류: {e.Message}");
            return FurnitureType.Table1; // 기본값
        }
    }


        private static string CutStringUpToChar(string str, char delimiter)
    {
        str = str.ToUpper();
        int index = str.IndexOf(delimiter);

        if (index >= 0)
            return str.Substring(0, index);
        else
            return str;
    }
}
