using System;
using System.Collections.Generic;
using UnityEngine;

namespace Muks.Tween
{
    public class Tween : MonoBehaviour
    {
        private static List<Sequence> _sequenceUpdateList = new List<Sequence>();
        private static List<TweenWait> _tweenWaitQueue = new List<TweenWait>();


        /// <summary> Tween Sequence 기능을 사용하기 위해 Sequence Class를 반환하는 함수 </summary>
        public static Sequence Sequence()
        {
            //클래스를 생성하고 UpdateList에 올린다.
            Sequence sequence = new Sequence();
            _sequenceUpdateList.Add(sequence);

            return sequence;
        }

        public static void Wait(float duration, Action onCompleted)
        {
            if(_tweenWaitQueue.Count == 0) 
            {
                GameObject obj = new GameObject("TweenWaitObj");
                TweenWait tween = obj.AddComponent<TweenWait>();
                tween.AddDataSequence(new TweenDataSequence(null, duration, TweenMode.Constant, null));
                tween.OnComplete(() =>
                {
                    onCompleted?.Invoke();
                    tween.enabled = false;
                    _tweenWaitQueue.Add(tween);
                });
                return;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateObj()
        {
            GameObject obj = new GameObject("MuksTween");
            obj.AddComponent<Tween>();
            DontDestroyOnLoad(obj);
        }


        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int i = 0, count = _sequenceUpdateList.Count; i < count; i++)
            {
                _sequenceUpdateList[i].Update(deltaTime);

                if (!_sequenceUpdateList[i].IsEnd)
                    continue;

                    _sequenceUpdateList.RemoveAt(i--);
                    count--;
            }



        }
    }

}
