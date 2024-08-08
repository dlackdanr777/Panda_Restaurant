using System;

public enum ChallengeType
{
    TYPE01, TYPE02, TYPE03, TYPE04, TYPE05, TYPE06, TYPE07, TYPE08, TYPE09, TYPE10, TYPE11, TYPE12, TYPE13, TYPE14, TYPE15, TYPE16, TYPE17, TYPE18, TYPE19, TYPE20, TYPE21, TYPE22, TYPE23, TYPE24, TYPE25, TYPE26, TYPE27, TYPE28, TYPE29, TYPE30, Length
}

public enum ChallengeShortCutType
{
    ShortCut01, SHORTCUT02, SHORTCUT03, SHORTCUT04, SHORTCUT05, SHORTCUT06, SHORTCUT07, SHORTCUT08, SHORTCUT09, SHORTCUT10, SHORTCUT11, SHORTCUT12, SHORTCUT13, Length
}

public abstract class ChallengeData
{
    protected ChallengeType _type;
    public ChallengeType Type => _type;

    protected string _id;
    public string Id => _id;

    protected string _description;
    public string Description => _description;

    protected int _rewardMoney;
    public int RewardMoney => _rewardMoney;
}


/// <summary>가구 구매 도전과제</summary>
public class Type01ChallengeData : ChallengeData
{
    protected string _needFurnitureId;
    public string NeedFurnitureId => _needFurnitureId;

    public Type01ChallengeData(ChallengeType type, string id, string description, string needFurnitureId, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needFurnitureId = needFurnitureId;
        _rewardMoney = rewardMoney;
    }
}



/// <summary>주방 기구 구매 도전과제</summary>
public class Type02ChallengeData : ChallengeData
{
    protected string[] _needKitchenUtensilId;
    public string[] NeedKitchenUtensilId => _needKitchenUtensilId;

    public Type02ChallengeData(ChallengeType type, string id, string description, string[] needKitchenUtensilId, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needKitchenUtensilId = needKitchenUtensilId;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>레시피 구매 도전과제</summary>
public class Type03ChallengeData : ChallengeData
{
    protected string _buyRecipeId;
    public string BuyRecipeId => _buyRecipeId;

    public Type03ChallengeData(ChallengeType type, string id, string description, string buyRecipeId, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _buyRecipeId = buyRecipeId;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>레시피 제작 도전과제</summary>
public class Type04ChallengeData : ChallengeData
{
    protected string _recipeId;
    public string RecipeId => _recipeId;

    protected int _cookCount;
    public int CookCount => _cookCount; 

    public Type04ChallengeData(ChallengeType type, string id, string description, string recipeId, int cookCount, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _recipeId = recipeId;
        _cookCount = cookCount;
        _rewardMoney = rewardMoney;
    }
}

/// <summary>스탭 채용 도전과제</summary>
public class Type05ChallengeData : ChallengeData
{
    protected string _needStaffId;
    public string NeedStaffId => _needStaffId;

    public Type05ChallengeData(ChallengeType type, string id, string description, string needStaffId, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needStaffId = needStaffId;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>레시피 획득 달성 도전과제</summary>
public class Type06ChallengeData : ChallengeData
{
    protected string _needRecipeId;
    public string NeedRecipeId => _needRecipeId;

    public Type06ChallengeData(ChallengeType type, string id, string description, string needRecipeId, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needRecipeId = needRecipeId;
        _rewardMoney = rewardMoney;
    }
}