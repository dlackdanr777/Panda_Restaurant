using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utility
{
    public static string ConvertToNumber(float value)
    {
        string text = 1000 <= value ? (value / 1000).ToString("F1") + 'K' : ((int)value).ToString();
        return text;
    }
}
