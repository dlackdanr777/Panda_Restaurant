using Muks.UI;
using System.Collections.Generic;
using UnityEngine;


namespace Muks.PcUI
{
    public class PcUINavigation : UINavigation
    {

        [Header("Views")]
        [Tooltip("최상위 lootUIView")]
        [SerializeField] private PcViewDicStruct _rootUiView;

        [Tooltip("이곳에서 관리할 UIView")]
        [SerializeField] private PcViewDicStruct[] _uiViews;

        public override int Count => _activeViewList.Count;
        private bool _isViewsInactive;
        public override bool IsViewsInactive => _isViewsInactive;

        /// <summary> ViewDicStruct에서 설정한 Name을 Key로, UIView를 값으로 저장해놓는 딕셔너리 </summary>
        private Dictionary<string, PcUIView> _viewDic = new Dictionary<string, PcUIView>();
        private LinkedList<PcUIView> _activeViewList = new LinkedList<PcUIView>();



        private void Start()
        {
            Init();
        }


        private void Init()
        {
            _viewDic.Clear();
            _rootUiView.UIView?.ViewInit(this);

            //uiViewList에 저장된 값을 딕셔너리에 저장
            for (int i = 0, count = _uiViews.Length; i < count; i++)
            {
                string name = _uiViews[i].Name;
                PcUIView uiView = _uiViews[i].UIView;
                _viewDic.Add(name, uiView);

                uiView.ViewInit(this);

                //UI View의 OnPointerDown()실행시 실행될 기능 구현
                uiView.OnFocus += () =>
                {

                    //링크드 리스트에서 해당 UIView를 제거 후 맨앞으로 배치
                    //SetAsLastSibling()으로 상단에 UI가 보일 수 있도록 함
                    _activeViewList.Remove(uiView);
                    _activeViewList.AddFirst(uiView);
                    uiView.transform.SetAsLastSibling();
                    OnFocusHandler?.Invoke();
                };
            }
        }


        /// <summary>이름을 받아 해당하는 UIView를 열어주는 함수</summary>
        public override void Push(string viewName)
        {
            if (_viewDic.TryGetValue(viewName, out PcUIView uiView))
            {
                //애니메이션이 진행중인 View가 있으면 Push, Pop을 막는다.
                if (!ViewsVisibleStateCheck())
                    return;

                if (!_activeViewList.Contains(uiView))
                {
                    _activeViewList.AddFirst(uiView);
                    uiView.Show();
                }
                else
                {
                    _activeViewList.Remove(uiView);
                    _activeViewList.AddFirst(uiView);
                }

                uiView.transform.SetAsLastSibling();
                OnFocusHandler?.Invoke();
            }
        }


        /// <summary>포커스중인 UI를 닫는 함수</summary>
        public override void Pop()
        {
            //애니메이션이 진행중인 View가 있으면 Push, Pop을 막는다.
            if (!ViewsVisibleStateCheck())
                return;

            if (_activeViewList.First == null)
                return;

            _activeViewList.First.Value.Hide();
            _activeViewList.RemoveFirst();
            OnFocusHandler?.Invoke();
        }


        /// <summary> viewName을 확인해 해당 UI를 닫는 함수</summary>
        public override void Pop(string viewName)
        {
            //애니메이션이 진행중인 View가 있으면 Push, Pop을 막는다.
            if (!ViewsVisibleStateCheck())
                return;

            if (_viewDic.TryGetValue(viewName, out PcUIView uiView))
            {
                if (!_activeViewList.Contains(uiView))
                    return;

                _activeViewList.Remove(uiView);
                uiView.Hide();
                OnFocusHandler?.Invoke();
            }
        }


        /// <summary> 꺼놨던 모든 UIView를 SetActive(true)한다. </summary>
        public override void AllShow()
        {
            _rootUiView.UIView.gameObject.SetActive(true);

            foreach (PcUIView view in _activeViewList)
            {
                view.gameObject.SetActive(true);
            }

            _isViewsInactive = false;
            OnFocusHandler?.Invoke();
        }


        /// <summary> 켜놨던 모든 UIView를 SetActive(false)한다. </summary>
        public override void AllHide()
        {
            _rootUiView.UIView.gameObject.SetActive(false);

            foreach (PcUIView view in _activeViewList)
            {
                view.gameObject.SetActive(false);
            }

            _isViewsInactive = true;
            OnFocusHandler?.Invoke();
        }


        public override VisibleState GetVisibleState(string viewName)
        {
            if (_viewDic.TryGetValue(viewName, out PcUIView view))
            {
                return view.VisibleState;
            }

            Debug.LogErrorFormat("{0}에 대응되는 UIView가 존재하지 않습니다.", viewName);
            return default;
        }


        /// <summary>매개 변수에 해당하는 UIView Class가 활성화된 상태면 참, 아니면 거짓을 반환하는 함수</summary>
        public override bool CheckActiveView(string viewName)
        {
            if (_viewDic.TryGetValue(viewName, out PcUIView uiView))
            {
                if (_activeViewList.Contains(uiView))
                    return true;
            }

            Debug.LogErrorFormat("{0}에 해당하는 UIView가 존재하지 않습니다.");
            return false;
        }


        /// <summary>열려있는 UI의 VisibleState를 확인 후 bool값을 리턴하는 함수</summary>
        private bool ViewsVisibleStateCheck()
        {
            foreach (PcUIView view in _viewDic.Values)
            {
                if (view.VisibleState == VisibleState.Disappearing || view.VisibleState == VisibleState.Appearing)
                {
                    Debug.Log("UI가 열리거나 닫히는 중 입니다.");
                    return false;
                }
            }

            return true;
        }
    }
}

