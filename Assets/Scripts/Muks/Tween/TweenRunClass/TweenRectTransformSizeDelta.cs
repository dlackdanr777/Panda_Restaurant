using UnityEngine;

namespace Muks.Tween
{
    public class TweenRectTransformSizeDelta : TweenData
    {
        private Vector2 _startSizeDelta;
        private Vector2 _targetSizeDelta;
        private RectTransform _rectTransform;


        public override void SetData(TweenDataSequence dataSequence)
        {
            base.SetData(dataSequence);

            if (_rectTransform == null)
                _rectTransform = (RectTransform)dataSequence.Component;

            _startSizeDelta = _rectTransform.sizeDelta;
            _targetSizeDelta = (Vector2)dataSequence.TargetValue;
        }


        protected override void Update()
        {
            base.Update();

            float percent = _percentHandler[_tweenMode](ElapsedDuration, TotalDuration);

            float width = Mathf.LerpUnclamped(_startSizeDelta.x, _targetSizeDelta.x, percent);
            float height = Mathf.LerpUnclamped(_startSizeDelta.y, _targetSizeDelta.y, percent);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }


        protected override void TweenCompleted()
        {
            if (_tweenMode != TweenMode.Spike)
            {
                _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _targetSizeDelta.x);
                _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _targetSizeDelta.y);
            }  
        }
    }
}
