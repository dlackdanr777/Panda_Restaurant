using UnityEngine;

namespace Muks.Tween
{
    public class TweenTransformMoveY : TweenData
    {
        [SerializeField] private float _targetPositionY;
        private Vector3 _startPos;

        protected override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);
            _startPos = transform.position;
            _targetPositionY = (float)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);
            float targetPosY = Mathf.LerpUnclamped(_startPos.y, _targetPositionY, percent);
            transform.position = new Vector3(transform.position.x, targetPosY, transform.position.z);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != Ease.Spike)
                transform.position = new Vector3(transform.position.x, _targetPositionY, transform.position.z);
        }
    }
}
