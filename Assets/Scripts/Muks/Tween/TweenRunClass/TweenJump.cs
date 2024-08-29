using UnityEngine;

namespace Muks.Tween
{
    public class TweenJump : TweenData
    {
        [SerializeField] private Vector3 _startPosition;
        [SerializeField] private Vector3 _targetPosition;

        private float _jumpHelght;

        internal void SetHeight(float height)
        {
            _jumpHelght = height;
        }


        protected override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            _startPosition = transform.position;
            _targetPosition = (Vector3)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();
            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            Vector3 currentPosition = Vector3.LerpUnclamped(_startPosition, _targetPosition, percent);

            float parabola = 4 *_jumpHelght * percent * (1 - percent);
            currentPosition.y = Mathf.LerpUnclamped(_startPosition.y, _targetPosition.y, percent) + parabola;

            transform.position = currentPosition;
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != Ease.Spike)
                transform.position = _targetPosition;
        }
    }
}
