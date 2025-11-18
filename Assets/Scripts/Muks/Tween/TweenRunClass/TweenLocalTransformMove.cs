using UnityEngine;

namespace Muks.Tween
{
    public class TweenLocalTransformMove : TweenData
    {
        [SerializeField] private Vector3 _startPosition;
        [SerializeField] private Vector3 _targetPosition;


        protected override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            _startPosition = transform.localPosition;
            _targetPosition = (Vector3)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();
            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            transform.localPosition = Vector3.LerpUnclamped(_startPosition, _targetPosition, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != Ease.Spike)
                transform.localPosition = _targetPosition;
        }
    }
}
