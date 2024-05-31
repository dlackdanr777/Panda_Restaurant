using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Muks.UI;


namespace Muks.MobileUI
{

    public class MobileUINavigation : UINavigation
    {

        [Header("Views")]
        [Tooltip("최상위 lootUIView")]
        [SerializeField] private MobileViewDicStruct _rootUiView;

        [Tooltip("이 클래스에서 관리할 UIViews")]
        [SerializeField] private MobileViewDicStruct[] _uiViews;

        public override int Count => _activeViewList.Count;

        private bool _isViewsInactive;
        public override bool IsViewsInactive => _isViewsInactive;

        private List<MobileUIView> _activeViewList = new List<MobileUIView>();
        private Dictionary<string, MobileUIView> _viewDic = new Dictionary<string, MobileUIView>();



        private void Start()
        {
            Init();
        }


        private void Init()
        {
            _viewDic.Clear();
            _rootUiView.UIView?.ViewInit(this);

            for (int i = 0, count = _uiViews.Length; i < count; i++)
            {
                string name = _uiViews[i].Name;
                MobileUIView uiView = _uiViews[i].UIView;
                _viewDic.Add(name, uiView);
                uiView.ViewInit(this);
            }
        }


        /// <summary>이름을 받아 현재 이름의 view를 열어주는 함수</summary>
        public override void Push(string viewName)
        {
            //애니메이션이 진행중인 View가 있으면 Push, Pop을 막는다.
            if (!ViewsVisibleStateCheck())
                return;

            if (_viewDic.TryGetValue(viewName, out MobileUIView uiView))
            {
                if (!_activeViewList.Contains(uiView))
                {
                    _activeViewList.Add(uiView);
                    uiView.Show();
                }
                else
                {
                    _activeViewList.Remove(uiView);
                    _activeViewList.Add(uiView);
                    uiView.gameObject.SetActive(true);
                }

                uiView.transform.SetAsLastSibling();
                OnFocusHandler?.Invoke();
                return;
            }

            Debug.LogError("딕셔너리에 해당 이름을 가진 UIView클래스가 없습니다.");
        }



        /// <summary>현재 ui 전에 열렸던 ui를 불러오는 함수</summary> 
        public override void Pop()
        {
            //애니메이션이 진행중인 View가 있으면 Push, Pop을 막는다.
            if (!ViewsVisibleStateCheck())
                return;

            if (_activeViewList.Count <= 0)
            {
                Debug.LogError("열려 있는 UI가 없습니다.");
                return;
            }

            MobileUIView selectView = _activeViewList[Count - 1];
            selectView.Hide();
            _activeViewList.RemoveAt(Count - 1);
            OnFocusHandler?.Invoke();

            if (1 <= _activeViewList.Count)
                _activeViewList.Last().transform.SetAsLastSibling();
        }


        /// <summary> viewName을 확인해 해당 UI 를 감추는 함수</summary>
        public override void Pop(string viewName)
        {
            //애니메이션이 진행중인 View가 있으면 Push, Pop을 막는다.
            if (!ViewsVisibleStateCheck())
                return;

            if (_activeViewList.Count <= 0)
                return;

            MobileUIView view = _activeViewList.Find(x => x == _viewDic[viewName]);
            if (view == null)
            {
                Debug.LogError("해당 uiView가 열려있지 않습니다.");
                return;
            }

            view.Hide();
            _activeViewList.Remove(view);
            OnFocusHandler?.Invoke();
        }



        /// <summary> 꺼놨던 모든 UIView를 SetActive(true)한다. </summary>
        public override void AllShow()
        {
            _rootUiView.UIView.gameObject.SetActive(true);

            foreach (MobileUIView view in _activeViewList)
            {
                view.gameObject.SetActive(true);
            }

            _isViewsInactive = false;
            OnFocusHandler?.Invoke();
        }


        public override void AllHide()
        {
            _rootUiView.UIView.gameObject.SetActive(false);

            foreach (MobileUIView view in _activeViewList)
            {
                view.gameObject.SetActive(false);
            }

            _isViewsInactive = true;
            OnFocusHandler?.Invoke();
        }



        /// <summary>매개 변수에 해당하는 UIView Class가 활성화된 상태면 참, 아니면 거짓을 반환하는 함수</summary>
        public override bool CheckActiveView(string viewName)
        {
            if (_viewDic.TryGetValue(viewName, out MobileUIView uiView))
            {
                if (_activeViewList.Contains(uiView))
                    return true;
            }

            else
            {
                Debug.LogErrorFormat("{0}에 해당하는 UIView가 존재하지 않습니다.");
                return false;
            }

            return false;
        }


        public override VisibleState GetVisibleState(string viewName)
        {
            if (_viewDic.TryGetValue(viewName, out MobileUIView view))
            {
                return view.VisibleState;
            }

            Debug.LogErrorFormat("{0}에 대응되는 UIView가 존재하지 않습니다.", viewName);
            return default;
        }


        /// <summary>열려있는 UI의 VisibleState를 확인 후 bool값을 리턴하는 함수</summary>
        private bool ViewsVisibleStateCheck()
        {
            foreach (MobileUIView view in _viewDic.Values)
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
