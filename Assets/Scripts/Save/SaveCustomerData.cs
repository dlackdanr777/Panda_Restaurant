public class SaveCustomerData
{
    private string _id;
    public string Id => _id;

    private int _visitCount;
    public int VisitCount => _visitCount;


    public SaveCustomerData(string id, int visitCount)
    {
        _id = id;
        _visitCount = visitCount;
    }


    public void AddVisitCount()
    {
        _visitCount += 1;
    }
}