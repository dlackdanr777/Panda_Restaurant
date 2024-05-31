using UnityEngine;

namespace Muks.Tween
{
    public class TweenTransformRotate : TweenData
    {
        private Vector3 _startEulerAngles;
        private Vector3 _targetEulerAngles;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            _startEulerAngles = transform.eulerAngles;
            _targetEulerAngles = (Vector3)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            transform.eulerAngles = Vector3.LerpUnclamped(_startEulerAngles, _targetEulerAngles, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                transform.eulerAngles = _targetEulerAngles;
        }
    }
}
