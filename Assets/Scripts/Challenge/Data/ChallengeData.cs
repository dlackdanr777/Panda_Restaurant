public enum ChallengeType
{
    TYPE01, TYPE02, TYPE03, TYPE04, TYPE05, TYPE06, TYPE07, TYPE08, TYPE09, TYPE10, TYPE11, TYPE12, TYPE13, TYPE14, TYPE15, TYPE16, TYPE17, TYPE18, TYPE19, TYPE20, TYPE21, TYPE22, TYPE23, TYPE24, TYPE25, TYPE26, TYPE27, TYPE28, TYPE29, TYPE30, TYPE31, TYPE32, TYPE33, TYPE34, TYPE35, TYPE36, TYPE37, TYPE38, TYPE39, TYPE40, Length
}

public abstract class ChallengeData
{
    protected string _id;
    public string Id => _id;

    protected string _description;
    public string Description => _description;

    protected int _rewardMoney;
    public int RewardMoney => _rewardMoney;
}


/// <summary>���� ���� ��������</summary>
public class Type01ChallengeData : ChallengeData
{
    protected string _needFurnitureId;
    public string NeedFurnitureId => _needFurnitureId;

    public Type01ChallengeData(string id, string description, string needFurnitureId, int rewardMoney)
    {
        _id = id;
        _description = description;
        _needFurnitureId = needFurnitureId;
        _rewardMoney = rewardMoney;
    }
}



/// <summary>�ֹ� �ⱸ ���� ��������</summary>
public class Type02ChallengeData : ChallengeData
{
    protected string[] _needKitchenUtensilId;
    public string[] NeedKitchenUtensilId => _needKitchenUtensilId;

    public Type02ChallengeData(string id, string description, string[] needKitchenUtensilId, int rewardMoney)
    {
        _id = id;
        _description = description;
        _needKitchenUtensilId = needKitchenUtensilId;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������ ���� ��������</summary>
public class Type03ChallengeData : ChallengeData
{
    protected string _buyRecipeId;
    public string BuyRecipeId => _buyRecipeId;

    public Type03ChallengeData(string id, string description, string buyRecipeId, int rewardMoney)
    {
        _id = id;
        _description = description;
        _buyRecipeId = buyRecipeId;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������ ���� ��������</summary>
public class Type04ChallengeData : ChallengeData
{
    protected string _recipeId;
    public string RecipeId => _recipeId;

    protected int _cookCount;
    public int CookCount => _cookCount; 

    public Type04ChallengeData(string id, string description, string recipeId, int cookCount, int rewardMoney)
    {
        _id = id;
        _description = description;
        _recipeId = recipeId;
        _cookCount = cookCount;
        _rewardMoney = rewardMoney;
    }
}

/// <summary>���� ä�� ��������</summary>
public class Type05ChallengeData : ChallengeData
{
    protected string _needStaffId;
    public string NeedStaffId => _needStaffId;

    public Type05ChallengeData(string id, string description, string needStaffId, int rewardMoney)
    {
        _id = id;
        _description = description;
        _needStaffId = needStaffId;
        _rewardMoney = rewardMoney;
    }
}