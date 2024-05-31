using System.Collections.Generic;
using UnityEngine;


namespace Muks.UI
{
    /// <summary> UI Navigation.cs들을 관리하는 클래스 (없어도 UINav 독립 사용가능)</summary>
    public class UINavigationCoordinator : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private NavigationData[] _navDatas;

        private LinkedList<NavigationData> _navList = new LinkedList<NavigationData>();


        private void Start()
        {
            Init();
        }


        private void Init()
        {
            for(int i = 0, count = _navDatas.Length; i < count; i++)
            {
                //자료 구조를 순회하며 UINav 대리자에 함수를 넣고, LinkedList에 넣어 관리를 할 수 있도록 한다.
                int index = i;
                _navDatas[index].UiNav.OnFocusHandler += () => OnFocusEvent(_navDatas[index]);
                _navList.AddLast(_navDatas[index]);
            }
        }


        /// <summary>선택한 UI 관리 클래스의 우선순위를 최상위로 두는 함수 </summary>
        private void OnFocusEvent(NavigationData navData)
        {
            _navList.Remove(navData);
            _navList.AddLast(navData);

            if (!navData.FocusEnabled)
                return;

            navData.UiNav.transform.SetAsLastSibling();
        }


        /// <summary> UI Navigation의 우선순위 대로 UI를 닫는 함수 </summary>
        public bool Pop()
        {
            NavigationData navData = _navList.Last.Value;
            UINavigation uiNav = navData.UiNav;

            //만약 최 상위 UI 관리 클래스에 UI가 열려 있지 않다면?
            if(uiNav.Count <= 0)
            {
                //자료구조를 순회
                for(int i = 0, count = _navDatas.Length - 1; i < count; i++)
                {
                    //마지막 자료를 맨 처음으로 둔다.
                    _navList.Remove(navData);
                    _navList.AddFirst(navData);

                    //마지막 자료를 받아와 UI가 열려 있는지 확인. 있으면 닫고 끝, 없으면 다음 자료를 순회
                    navData = _navList.Last.Value;
                    uiNav = navData.UiNav;

                    if (uiNav.Count <= 0)
                        continue;

                    uiNav.Pop();
                    return true;
                }

                return false;
            }

            uiNav.Pop();
            return true;
        }


        /// <summary> 관리중인 UI 전체를 활성화 시키는 함수 </summary>
        public void AllShow()
        {
            foreach(NavigationData navData in _navList)
            {
                navData.UiNav.AllShow();
            }
        }


        /// <summary> 관리중인 UI 전체를 비활성화 시키는 함수 </summary>
        public void AllHide()
        {
            foreach (NavigationData navData in _navList)
            {
                navData.UiNav.AllHide();
            }
        }
    }
}


