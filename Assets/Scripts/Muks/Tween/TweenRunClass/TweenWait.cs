using System;
using UnityEngine;
using System.Collections;

namespace Muks.Tween
{
    public class TweenWait : MonoBehaviour
    {
        private float _totalDuration;
        private float _elapsedDuration;
        private Action _onCompleted;

        public void Stop()
        {
            CompleteTween();
        }

        internal void Clear()
        {
            _totalDuration = 0f;
            _elapsedDuration = 0f;
            _onCompleted = null;
        }

        internal void SetData(float totalDuration, Action onCompleted)
        {
            _totalDuration = totalDuration;
            _onCompleted = onCompleted;
            _elapsedDuration = 0;
        }
        
        protected void Update()
        {
            // 부모 Update 호출하지 않고 직접 처리
            if (!enabled || !gameObject.activeInHierarchy)
                return;

            _elapsedDuration += Time.deltaTime;
            if (_elapsedDuration >= _totalDuration)
            {
                StartCoroutine(CompleteWithDelay());
            }


        }

        private IEnumerator CompleteWithDelay()
        {
            // 한 프레임 대기 (타이밍 문제 방지)
            yield return null;
            _onCompleted?.Invoke();
            CompleteTween();
        }
        
        private void CompleteTween()
        {
            enabled = false;
            Clear();
            Tween.DequeueTweenWait(this);
        }
    }
}

