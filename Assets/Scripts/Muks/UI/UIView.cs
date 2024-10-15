using UnityEngine;


namespace Muks.UI
{
    public abstract class UIView : MonoBehaviour
    {
        ///  <summary> Appeared, Disappeared�϶� Show(), Hide()���� ����</summary>
        public VisibleState VisibleState;
        protected UINavigation _uiNav;
        protected RectTransform _rectTransform;
        protected bool _popEnabled = true;
        public bool PopEnabled => _popEnabled;

        public void ViewInit(UINavigation uiNav)
        {
            _uiNav = uiNav;
            _rectTransform = GetComponent<RectTransform>();
            Init();
        }

        /// <summary> UI View �ʱ� ���� �Լ� </summary> 
        public abstract void Init();


        /// <summary> UI�� �ҷ����� ����Ǵ� �Լ� </summary>
        public abstract void Show();


        /// <summary>UI�� ���� ����Ǵ� �Լ�</summary>
        public abstract void Hide();
    }
}


