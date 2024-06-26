using System;
using System.Collections.Generic;
using UnityEngine;

namespace Muks.Tween
{
    public class Tween : MonoBehaviour
    {
        private static List<Sequence> _sequenceUpdateList = new List<Sequence>();
        private static Queue<TweenWait> _tweenWaitQueue = new Queue<TweenWait>();

        private static GameObject _waitQueueParent;

        /// <summary> Tween Sequence 기능을 사용하기 위해 Sequence Class를 반환하는 함수 </summary>
        public static Sequence Sequence()
        {
            //클래스를 생성하고 UpdateList에 올린다.
            Sequence sequence = new Sequence();
            _sequenceUpdateList.Add(sequence);

            return sequence;
        }

        public static TweenWait Wait(float duration, Action onCompleted)
        {
            TweenWait tween = null;
            if (_tweenWaitQueue.Count == 0)
            {
                GameObject obj = new GameObject("TweenWaitObj");
                obj.transform.parent = _waitQueueParent.transform;
                tween = obj.AddComponent<TweenWait>();
            }
            else
            {
                tween = _tweenWaitQueue.Dequeue();
            }

            tween.Clear();
            tween.enabled = true;
            tween.AddDataSequence(new TweenDataSequence(null, duration, TweenMode.Constant, null));
            tween.OnComplete(() =>
            {
                onCompleted?.Invoke();
                tween.enabled = false;
                _tweenWaitQueue.Enqueue(tween);
            });

            return tween;
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateObj()
        {
            GameObject obj = new GameObject("MuksTween");
            obj.AddComponent<Tween>();
            _waitQueueParent = new GameObject("WaitQueueParnet");
            _waitQueueParent.transform.parent = obj.transform;
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
