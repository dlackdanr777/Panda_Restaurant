using UnityEngine;

namespace Muks.Tween
{
    public class TweenTransformMove : TweenData
    {
        [SerializeField] private Vector3 _startPosition;
        [SerializeField] private Vector3 _targetPosition;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            _startPosition = transform.position;
            _targetPosition = (Vector3)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            transform.position = Vector3.LerpUnclamped(_startPosition, _targetPosition, percent);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
                transform.position = _targetPosition;
        }
    }
}
