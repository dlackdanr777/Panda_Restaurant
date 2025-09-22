using System;


[Flags]
public enum SalesLocationType
{
    None = 0,
    Shop = 1 << 0,
    Gacha = 1 << 1,
}
