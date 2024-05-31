using System;
using System.Collections.Generic;
using UnityEngine;

namespace Muks.PathFinding
{
    /// <summary> ť�� ������ѳ��� �ش� ť�� ���������� �����ϴ� Ŭ���� </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        public static MainThreadDispatcher Instance => _instance;
        private static MainThreadDispatcher _instance;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateObj()
        {
            if (_instance != null)
                return;

            GameObject obj = new GameObject("MainThreadDispatcher");
            _instance = obj.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(obj);
        }


        public void Update()
        {
            lock (_executionQueue)
            {
                while (0 < _executionQueue.Count)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
    }
}

