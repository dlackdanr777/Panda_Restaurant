using System;
using UnityEngine;


namespace Muks.UI
{

    public abstract class UINavigation : MonoBehaviour
    {
        public Action OnShowUIHandler;
        public Action OnHideUIHandler;
        public Action OnFocusHandler;
        public abstract int Count { get; }
        public abstract bool IsViewsInactive{ get; }

        public abstract UIView FirstView { get; }


        /// <summary>이름을 받아 현재 이름의 view를 열어주는 함수</summary>
        public abstract void Push(string viewName);

        /// <summary> UIView를 확인해 해당 UI 를 열어주는 함수</summary>
        //public abstract void Push(UIView uiView);

        /// <summary>이름을 받아 현재 이름의 view를 열어주는 함수(애니메이션 없이)</summary>
        public abstract void PushNoAnime(string viewName);

        /// <summary>현재 ui 전에 열렸던 ui를 불러오는 함수</summary> 
        public abstract void Pop();

        /// <summary> viewName을 확인해 해당 UI 를 감추는 함수</summary>
        public abstract void Pop(string viewName);

        /// <summary> UIView를 확인해 해당 UI 를 감추는 함수</summary>
        //public abstract void Pop(UIView uiView);

        /// <summary> 열려있는 모든 View를 닫는 함수</summary>
        public abstract void AllPop();

        /// <summary> viewName을 확인해 해당 UI 를 감추는 함수(즉시 닫기)</summary>
        public abstract void PopNoAnime(string viewName);

        /// <summary> 꺼놨던 모든 UIView를 SetActive(true)한다. </summary>
        public abstract void AllShow();

        /// <summary> 켜놨던 모든 UIView를 SetActive(false)한다. </summary>
        public abstract void AllHide();

        /// <summary>매개 변수에 해당하는 UIView Class가 활성화된 상태면 참, 아니면 거짓을 반환하는 함수</summary>
        public abstract bool CheckActiveView(string viewName);

        /// <summary> 매개 변수에 해당하는 UIView Class의 VisibleState값을 반환하는 함수 </summary>
        public abstract VisibleState GetVisibleState(string viewName);

        /// <summary> 활성화된 UIView들을 순회하며 애니메이션이 진행중인 UI가 있는지 확인하는 함수(있으면 false, 없으면 true) </summary>
        public abstract bool ViewsVisibleStateCheck();
    }

}
