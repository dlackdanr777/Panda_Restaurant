
public class SaveTimeData
{
    private string _id;
    public string Id => _id;

    private int _time;
    public int Time => _time;

    public SaveTimeData(string id, int time)
    {
        _id = id;
        _time = time;
    }

}
