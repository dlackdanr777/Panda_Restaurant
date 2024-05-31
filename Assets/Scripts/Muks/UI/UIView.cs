using UnityEngine;


namespace Muks.UI
{
    public abstract class UIView : MonoBehaviour
    {
        ///  <summary> Appeared, Disappeared일때 Show(), Hide()실행 가능</summary>
        public VisibleState VisibleState;
        protected UINavigation _uiNav;
        protected RectTransform _rectTransform;


        public void ViewInit(UINavigation uiNav)
        {
            _uiNav = uiNav;
            _rectTransform = GetComponent<RectTransform>();
            Init();
        }

        /// <summary> UI View 초기 설정 함수 </summary> 
        public abstract void Init();


        /// <summary> UI를 불러낼때 실행되는 함수 </summary>
        public abstract void Show();


        /// <summary>UI를 끌때 실행되는 함수</summary>
        public abstract void Hide();
    }
}


