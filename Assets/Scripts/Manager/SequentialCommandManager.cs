using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SequentialCommandManager : MonoBehaviour
{
    public static SequentialCommandManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SequentialCommandManager");
                _instance = obj.AddComponent<SequentialCommandManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }

    private static SequentialCommandManager _instance;

    private Canvas _dontTouchCanvasPrefab;
    private Canvas _dontTouchCanvas;
    private SortedSet<Command> _commandQueue;
    private bool _isExecuting = false;
    private int _commandCounter = 0; // ���� �켱���� ó����

    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _dontTouchCanvasPrefab = Resources.Load<Canvas>("SequentialCommandManager/DontTouchCanvas");
        _dontTouchCanvas = Instantiate(_dontTouchCanvasPrefab, transform);
        _dontTouchCanvas.gameObject.SetActive(false);

        // �켱���� ť�� ���� (�켱���� ���� ���� ���� ����ǵ��� ����)
        _commandQueue = new SortedSet<Command>(new CommandComparer());
        LoadingSceneManager.OnLoadSceneHandler += ResetCommand;
    }



    public void EnqueueCommand(Action execute, Func<bool> canExecute, Func<bool> isFinished, int priority, float interval = 1)
    {
        interval = Mathf.Clamp(interval, 0f, interval);
        _commandQueue.Add(new Command(execute, canExecute, isFinished, priority, interval, _commandCounter++)); // ���� �켱���� ��� ���� ����

        // ���ο� ����� �߰��Ǹ� ��ġ ���� ��ũ�� Ȱ��ȭ
        _dontTouchCanvas.gameObject.SetActive(true);

        TryExecuteNext();
    }

    private void TryExecuteNext()
    {
        if (_isExecuting || _commandQueue.Count == 0)
            return;

        StartCoroutine(ExecuteCommand(_commandQueue.Min));
        _commandQueue.Remove(_commandQueue.Min);
    }

    private IEnumerator ExecuteCommand(Command command)
    {
        _isExecuting = true;
        yield return YieldCache.WaitForSeconds(command.Interval);

        // ���� �߿��� ��ġ ���� ��ũ�� ��Ȱ��ȭ
        _dontTouchCanvas.gameObject.SetActive(false);

        yield return new WaitUntil(command.CanExecute);

        command.Execute();

        yield return YieldCache.WaitForSeconds(0.1f);
        yield return new WaitUntil(command.IsFinished);

        _isExecuting = false;

        if (_commandQueue.Count > 0)
        {
            _dontTouchCanvas.gameObject.SetActive(true);
            TryExecuteNext();
        }
        else
        {
            _dontTouchCanvas.gameObject.SetActive(false);
        }
    }

    private void ResetCommand()
    {
        _commandQueue.Clear();
        StopAllCoroutines();
        _dontTouchCanvas.gameObject.SetActive(false);
    }

    private class Command
    {
        public readonly Action Execute;
        public readonly Func<bool> CanExecute;
        public readonly Func<bool> IsFinished;
        public readonly int Priority;
        public readonly int Order;
        public readonly float Interval;


        public Command(Action execute, Func<bool> canExecute, Func<bool> isFinished, int priority, float interval, int order)
        {
            Execute = execute;
            CanExecute = canExecute;
            IsFinished = isFinished;
            Priority = priority;
            Interval = interval;
            Order = order;
        }
    }

    private class CommandComparer : IComparer<Command>
    {
        public int Compare(Command x, Command y)
        {
            int priorityComparison = x.Priority.CompareTo(y.Priority);
            if (priorityComparison == 0)
            {
                return x.Order.CompareTo(y.Order);
            }
            return priorityComparison;
        }
    }
}
