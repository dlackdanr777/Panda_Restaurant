using UnityEngine;

namespace Muks.Tween
{
    public class TweenTransformScale : TweenData
    {
        private Vector3 _startScale;
        private Vector3 _targetScale;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);
            _startScale = transform.localScale;
            _targetScale = (Vector3)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            transform.localScale = Vector3.LerpUnclamped(_startScale, _targetScale, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                transform.localScale = _targetScale;
        }
    }
}
