public class SaveCustomerData
{
    private string _id;
    public string Id => _id;

    private string _skinId;
    public string SkinId => _skinId;

    private int _visitCount;
    public int VisitCount => _visitCount;


    public SaveCustomerData(string id, string skinId, int visitCount)
    {
        _id = id;
        _skinId = string.IsNullOrWhiteSpace(skinId) ? string.Empty : skinId;
        _visitCount = visitCount;
    }


    public void AddVisitCount()
    {
        _visitCount += 1;
    }

    public void AddVisitCount(int count)
    {
        _visitCount += count;
    }

    public void SetSkinId(string skinId)
    {
        if (string.IsNullOrWhiteSpace(skinId))
        {
            _skinId = string.Empty;
            return;
        }

        CustomerSkinData data = SkinDataManager.Instance.GetCustomerSkinData(skinId);
        if (data == null)
        {
            throw new System.Exception($"존재하지 않는 스킨 ID입니다: {skinId}");
        }

        if (data.EquipId != _id)
        {
            DebugLog.LogError("스킨 ID와 고객 ID가 일치하지 않습니다.");
            return;
        }

        _skinId = skinId;
    }
    
    public string GetSkinId()
    {
        return string.IsNullOrWhiteSpace(_skinId) ? string.Empty : _skinId;
    }
}