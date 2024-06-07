using System;

public struct CookingData
{
    private string _id;
    public string Id => _id;

    private float _cookingTime;
    public float CookingTime => _cookingTime;   

    private Action _onCompleted;
    public Action OnCompleted => _onCompleted;

    public CookingData(string id, float cookingTime, Action onCompleted)
    {
        _id = id;
        _cookingTime = cookingTime;
        _onCompleted = onCompleted;
    }

    public bool IsDefault()
    {
        return _id == default(string) && _cookingTime == default(float);
    }
}
