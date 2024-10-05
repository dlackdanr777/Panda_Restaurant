using UnityEngine;

public class TestData
{
    private string _str;
    public string Str => _str;
    private int _int;
    public int Int => _int;
    
    public TestData(string str, int i)
    {
        _str = str;
        _int = i;
    }
}
