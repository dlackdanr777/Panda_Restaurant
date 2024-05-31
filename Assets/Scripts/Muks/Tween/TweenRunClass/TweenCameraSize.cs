using UnityEngine;

namespace Muks.Tween
{
    public class TweenCameraSize : TweenData
    {
        private float _startSize;
        private float _targetSize;
        private Camera _camera;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_camera == null)
                _camera = (Camera)dataSequence.Component;

            _startSize = _camera.orthographicSize;
            _targetSize = (float)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            _camera.orthographicSize = Mathf.LerpUnclamped(_startSize, _targetSize, percent);
        }


        protected override void TweenCompleted()
        {
            if(_tweenMode != TweenMode.Spike)
                _camera.orthographicSize = _targetSize;
        }
    }
}
