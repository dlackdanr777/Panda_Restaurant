using UnityEngine;
using Muks.RecyclableScrollView;
using System.Collections.Generic;

public class TestScrollView : RecyclableScrollView<TestData>
{
    private void Awake()
    {
        List<TestData> testData = new List<TestData>();


        for(int i = 0; i < 100; i++)
        {
            TestData a = new TestData(i.ToString(), i + 1);
            testData.Add(a);
        }

        Init(testData);
    }
}
