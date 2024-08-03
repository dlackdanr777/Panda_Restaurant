using UnityEngine;

namespace Muks.Tween
{
    public struct TweenDataSequence
    {
        private int _id;
        public int Id => _id;

        private object _targetValue;
        public object TargetValue => _targetValue;

        private float _duration;
        public float Duration => _duration;

        private Ease _ease;
        public Ease Ease => _ease;

        private Component _component;
        public Component Component => _component;


        public TweenDataSequence(object targetValue, float  duration,  Ease ease, Component component)
        {
            _id = -1;
            _targetValue =  targetValue;
            _duration = duration;
            _ease = ease;
            _component = component;
        }


        public void SetId(int id)
        {
            _id = id;
        } 
    }
}
