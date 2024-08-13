using System;

public enum ChallengeType
{
    TYPE01, TYPE02, TYPE03, TYPE04, TYPE05, TYPE06, TYPE07, TYPE08, TYPE09, TYPE10, TYPE11, TYPE12, TYPE13, TYPE14, TYPE15, TYPE16, TYPE17, TYPE18, TYPE19, TYPE20, TYPE21, TYPE22, TYPE23, TYPE24, TYPE25, TYPE26, TYPE27, TYPE28, TYPE29, TYPE30, TYPE31, TYPE32, TYPE33, TYPE34, TYPE35, Length
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

    protected MoneyType _moneyType;
    public MoneyType MoneyType => _moneyType;

    protected int _rewardMoney;
    public int RewardMoney => _rewardMoney;
}


/// <summary>���� ���� ��������</summary>
public class Type01ChallengeData : ChallengeData
{
    protected string[] _needFurnitureIds;
    public string[] NeedFurnitureIds => _needFurnitureIds;

    public Type01ChallengeData(ChallengeType type, string id, string description, string[] needFurnitureIds, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needFurnitureIds = needFurnitureIds;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}



/// <summary>�ֹ� �ⱸ ���� ��������</summary>
public class Type02ChallengeData : ChallengeData
{
    protected string[] _needKitchenUtensilId;
    public string[] NeedKitchenUtensilId => _needKitchenUtensilId;

    public Type02ChallengeData(ChallengeType type, string id, string description, string[] needKitchenUtensilId, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needKitchenUtensilId = needKitchenUtensilId;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������ ���� ��������</summary>
public class Type03ChallengeData : ChallengeData
{
    protected string _buyRecipeId;
    public string BuyRecipeId => _buyRecipeId;

    public Type03ChallengeData(ChallengeType type, string id, string description, string buyRecipeId, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _buyRecipeId = buyRecipeId;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������ ���� ��������</summary>
public class Type04ChallengeData : ChallengeData
{
    protected string _needRecipeId;
    public string NeedRecipeId => _needRecipeId;

    public Type04ChallengeData(ChallengeType type, string id, string description, string needRecipeId, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needRecipeId = needRecipeId;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}

/// <summary>���� ä�� ��������</summary>
public class Type05ChallengeData : ChallengeData
{
    protected string _needStaffId;
    public string NeedStaffId => _needStaffId;

    public Type05ChallengeData(ChallengeType type, string id, string description, string needStaffId, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _needStaffId = needStaffId;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������ ȹ�� ���� �޼� ��������</summary>
public class Type06ChallengeData : ChallengeData
{
    protected int _recipeCount;
    public int RecipeCount => _recipeCount;

    public Type06ChallengeData(ChallengeType type, string id, string description, int recipeCount, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _recipeCount = recipeCount;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}

/// <summary>���� ���� Ƚ�� �޼� ��������</summary>
public class Type07ChallengeData : ChallengeData
{
    protected string _recipeId;
    public string RecipeId => _recipeId;

    protected int _cookCount;
    public int CookCount => _cookCount;

    public Type07ChallengeData(ChallengeType type, string id, string description, string recipeId, int cookCount, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _recipeId = recipeId;
        _cookCount = cookCount;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>���� ���� �޼� ��������</summary>
public class Type08ChallengeData : ChallengeData
{

    protected int _rank;
    public int Rank => _rank;

    public Type08ChallengeData(ChallengeType type, string id, string description, int rank, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _rank = rank;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>�湮 �մ� ���� ��������</summary>
public class Type09ChallengeData : ChallengeData
{

    protected int _csutomerCount;
    public int CustomerCount => _csutomerCount;

    public Type09ChallengeData(ChallengeType type, string id, string description, int csutomerCount, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _csutomerCount = csutomerCount;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>�մ� ���� �޼� ��������</summary>
public class Type10ChallengeData : ChallengeData
{

    protected int _csutomerCount;
    public int CustomerCount => _csutomerCount;

    public Type10ChallengeData(ChallengeType type, string id, string description, int csutomerCount, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _csutomerCount = csutomerCount;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>���� �� �޼� ��������</summary>
public class Type11ChallengeData : ChallengeData
{
    protected int _moneyCount;
    public int MoneyCount => _moneyCount;

    public Type11ChallengeData(ChallengeType type, string id, string description, int moneyCount, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _moneyCount = moneyCount;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>���� ȫ�� Ƚ�� �޼� ��������</summary>
public class Type12ChallengeData : ChallengeData
{
    protected int _promotionCount;
    public int PromotionCount => _promotionCount;

    public Type12ChallengeData(ChallengeType type, string id, string description, int promotionCount, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _promotionCount = promotionCount;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>���� �� �ֹ� ���� ���� ���� �޼� ��������</summary>
public class Type13ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type13ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������� ���� ��Ʈ ���� �޼� ��������</summary>
public class Type14ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type14ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>�ֹ� ���� ��Ʈ ���� �޼� ��������</summary>
public class Type15ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type15ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>Ư�� ������� ���� ��Ʈ �޼� ��������</summary>
public class Type16ChallengeData : ChallengeData
{
    protected string _setId;
    public string SetId => _setId;

    public Type16ChallengeData(ChallengeType type, string id, string description, string setId, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _setId = setId;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>Ư�� �ֹ� ���� ��Ʈ �޼� ��������</summary>
public class Type17ChallengeData : ChallengeData
{
    protected string _setId;
    public string SetId => _setId;

    public Type17ChallengeData(ChallengeType type, string id, string description, string setId, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _setId = setId;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>������� ���� ���� ���� �޼� ��������</summary>
public class Type18ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type18ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>�ֹ� ���� ���� ���� �޼� ��������</summary>
public class Type19ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type19ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>���� ������ û�� �޼� ��������</summary>
public class Type28ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type28ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>(���� ��������) �Ϸ� �մ� �湮 Ƚ�� �޼� ��������</summary>
public class Type31ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type31ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>(���� ��������) �Ϸ� ���� ȹ�淮 �޼� ��������</summary>
public class Type32ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type32ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>(���� ��������) ���� ���� Ƚ�� �޼� ��������</summary>
public class Type33ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type33ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>(���� ��������) û�� Ƚ�� �޼� ��������</summary>
public class Type34ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type34ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}


/// <summary>(���� ��������) ���� ��û Ƚ�� �޼� ��������</summary>
public class Type35ChallengeData : ChallengeData
{
    protected int _count;
    public int Count => _count;

    public Type35ChallengeData(ChallengeType type, string id, string description, int count, MoneyType moneyType, int rewardMoney)
    {
        _type = type;
        _id = id;
        _description = description;
        _count = count;
        _moneyType = moneyType;
        _rewardMoney = rewardMoney;
    }
}