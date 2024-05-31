
namespace Muks.Tween

{
    ///<summary> 경과시간에 따라 속도를 어떻게 달리 해줄 것인가? </summary>
    public enum TweenMode
    {
        /// <summary>일정한 속도 유지</summary>
        Constant,

        /// <summary>속도가 점점 증가함</summary>
        Quadratic,

        /// <summary>천천히 가속 천천히 감속</summary>
        Smoothstep,

        /// <summary>더욱 천천히 가속 더욱 천천히 감속</summary>
        Smootherstep,

        /// <summary>빠르게 위치로 갔다가 제자리로 돌아감</summary>
        Spike,

        /// <summary>마지막쯤에 빠르게 이동</summary>
        EaseInQuint,

        /// <summary>초반에 빠르게 이동</summary>
        EaseOutQuint,

        /// <summary>중간에 빠르게 이동</summary>
        EaseInOutQuint,

        /// <summary>마지막에 매우 빠르게 이동</summary>
        EaseInExpo,

        /// <summary>초반에 매우 빠르게 이동</summary>
        EaseOutExpo,

        /// <summary>중간에 매우 빠르게 이동</summary>
        EaseInOutExpo,

        /// <summary>처음 위치에서 여러번 튕긴후 위치로 이동</summary>
        EaseInElastic,

        /// <summary>빠르게 위치로 가서 여러번 튕김</summary>
        EaseOutElastic,

        /// <summary>중간쯤 위치로 가서 여러번 튕김</summary>
        EaseInOutElastic,

        /// <summary>한번 뒤로 갔다가 스무스 하게 해당 값으로 이동</summary>
        EaseInBack,

        /// <summary>위치로 가서 한번 튕긴 후 해당 값으로 이동</summary>
        EaseOutBack,

        /// <summary>한번 뒤로 갔다가 위치로 이동 후 마지막에 한번 더 튕긴 후 해당 값으로 이동</summary>
        EaseInOutBack,

        /// <summary>현재 값에서 여러번 튕기다 해당 값으로 이동</summary>
        EaseInBounce,

        /// <summary>목표 값 이동 후 목표 값에서 여러번 튕김</summary>
        EaseOutBounce,

        /// <summary>현재 값에서 여러번 튕긴 후 중간에 목표 값 이동 후 목표 값에서도 여러번 튕김</summary>
        EaseInOutBounce,

        /// <summary>Sin 그래프 이동</summary>
        Sinerp,

        /// <summary>Cos 그래프 이동</summary>
        Coserp,
    }

}
