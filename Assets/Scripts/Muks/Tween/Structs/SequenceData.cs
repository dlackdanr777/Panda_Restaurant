using Muks.Tween;
using System;
using System.Collections.Generic;

/// <summary>시퀀스의 작업을 순차적으로 하기 위한 정보를 모아둔 구조체 </summary>
public struct SequenceData
{
    public List<TweenData> TweenDataList;
    public float WaitTime;
    public Action Callback;
}
