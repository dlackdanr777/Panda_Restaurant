public class SaveStaffData
{
    private string _id;
    public string Id => _id;

    private int _level;
    public int Level => _level;

    public SaveStaffData(string id, int level)
    {
        _id = id;
        _level = level;
    }

    public void LevelUp()
    {
        _level += 1;
    }
}
