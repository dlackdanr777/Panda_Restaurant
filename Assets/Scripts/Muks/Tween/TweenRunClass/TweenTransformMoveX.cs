using UnityEngine;

namespace Muks.Tween
{
    public class TweenTransformMoveX : TweenData
    {
        [SerializeField] private float _targetPositionX;
        private Vector3 _startPos;

        protected override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);
            _startPos = transform.position;
            _targetPositionX = (float)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            float targetPosX = Mathf.LerpUnclamped(_startPos.x, _targetPositionX, percent);
            transform.position = new Vector3(targetPosX, transform.position.y, transform.position.z);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != Ease.Spike)
                transform.position = new Vector3(_targetPositionX, transform.position.y, transform.position.z);
        }
    }
}
