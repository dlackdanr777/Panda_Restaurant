public class FoodMiniGameData
{
    private string _id;
    public string Id => _id;

    private int _successCount;
    public int SuccessCount => _successCount;

    private int _maxHealth;
    public int MaxHealth => _maxHealth;

    private int _firstHealth;
    public int FirstHealth => _firstHealth;

    private int _addHealth;
    public int AddHealth => _addHealth;

    private int _clearAddTime;
    public int ClearAddTime => _clearAddTime;

    public FoodMiniGameData(string id, int successCount, int maxHealth, int firstHealth, int addHealth, int clearAddTime)
    {
        _id = id;
        _successCount = successCount;
        _maxHealth = maxHealth;
        _firstHealth = firstHealth;
        _addHealth = addHealth;
        _clearAddTime = clearAddTime;
    }
}
