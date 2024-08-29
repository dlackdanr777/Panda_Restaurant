
namespace Muks.Tween

{
    ///<summary> 경과시간에 따라 속도를 어떻게 달리 해줄 것인가? </summary>
    public enum Ease
    {
        /// <summary>일정한 속도 유지</summary>
        Constant,

        /// <summary>천천히 가속 천천히 감속</summary>
        Smoothstep,

        /// <summary>더욱 천천히 가속 더욱 천천히 감속</summary>
        Smootherstep,

        /// <summary>빠르게 위치로 갔다가 제자리로 돌아감</summary>
        Spike,

        /// <summary>천천히 가속</summary>
        InSine,

        /// <summary>천천히 감속</summary>
        OutSine,

        /// <summary>천천히 가속후 천천히 감속</summary>
        InOutSine,

        /// <summary>점점 느리게(^2)</summary>
        InQuad,

        /// <summary>점점 빠르게(^2)</summary>
        OutQuad,

        /// <summary>점점 빠르다가 점점 느리게(^2)</summary>
        InOutQuad,

        /// <summary>점점 느리게(^3)</summary>
        InCubic,

        /// <summary>점점 빠르게(^3)</summary>
        OutCubic,

        /// <summary>점점 빠르다가 점점 느리게(^3)</summary>
        InOutCubic,

        /// <summary>점점 느리게(^4)</summary>
        InQuint,

        /// <summary>점점 빠르게(^4)</summary>
        OutQuint,

        /// <summary>점점 빠르다가 점점 느리게(^4)</summary>
        InOutQuint,

        /// <summary>마지막에 매우 빠르게 이동</summary>
        InExpo,

        /// <summary>초반에 매우 빠르게 이동</summary>
        OutExpo,

        /// <summary>중간에 매우 빠르게 이동</summary>
        InOutExpo,

        /// <summary>천천히 점점 빠르게</summary>
        InCirc,

        /// <summary>천천히 점점 느리게</summary>
        OutCirc,

        /// <summary>천천히 빨라지다가 천천히 점점 느리게</summary>
        InOutCirc,

        /// <summary>한번 튕긴 후 빠르게</summary>
        InBack,

        /// <summary>빠르다가 한번 튕긴후 느리게</summary>
        OutBack,

        /// <summary>한번 튕긴 후 빠르게 이동 후 다시 한번 튕긴 후 느리게</summary>
        InOutBack,

        /// <summary>처음 위치에서 여러번 튕긴후 위치로 이동</summary>
        InElastic,

        /// <summary>빠르게 위치로 가서 여러번 튕김</summary>
        OutElastic,

        /// <summary>중간쯤 위치로 가서 여러번 튕김</summary>
        InOutElastic,

        /// <summary>현재 값에서 여러번 튕기다 해당 값으로 이동</summary>
        InBounce,

        /// <summary>목표 값 이동 후 목표 값에서 여러번 튕김</summary>
        OutBounce,

        /// <summary>현재 값에서 여러번 튕긴 후 중간에 목표 값 이동 후 목표 값에서도 여러번 튕김</summary>
        InOutBounce,
    }

}
