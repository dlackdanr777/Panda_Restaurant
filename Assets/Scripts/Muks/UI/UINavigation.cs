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


        /// <summary>�̸��� �޾� ���� �̸��� view�� �����ִ� �Լ�</summary>
        public abstract void Push(string viewName);

        /// <summary> UIView�� Ȯ���� �ش� UI �� �����ִ� �Լ�</summary>
        //public abstract void Push(UIView uiView);

        /// <summary>�̸��� �޾� ���� �̸��� view�� �����ִ� �Լ�(�ִϸ��̼� ����)</summary>
        public abstract void PushNoAnime(string viewName);

        /// <summary>���� ui ���� ���ȴ� ui�� �ҷ����� �Լ�</summary> 
        public abstract void Pop();

        /// <summary> viewName�� Ȯ���� �ش� UI �� ���ߴ� �Լ�</summary>
        public abstract void Pop(string viewName);

        /// <summary> UIView�� Ȯ���� �ش� UI �� ���ߴ� �Լ�</summary>
        //public abstract void Pop(UIView uiView);

        /// <summary> �����ִ� ��� View�� �ݴ� �Լ�</summary>
        public abstract void AllPop();

        /// <summary> viewName�� Ȯ���� �ش� UI �� ���ߴ� �Լ�(��� �ݱ�)</summary>
        public abstract void PopNoAnime(string viewName);

        /// <summary> ������ ��� UIView�� SetActive(true)�Ѵ�. </summary>
        public abstract void AllShow();

        /// <summary> �ѳ��� ��� UIView�� SetActive(false)�Ѵ�. </summary>
        public abstract void AllHide();

        /// <summary>�Ű� ������ �ش��ϴ� UIView Class�� Ȱ��ȭ�� ���¸� ��, �ƴϸ� ������ ��ȯ�ϴ� �Լ�</summary>
        public abstract bool CheckActiveView(string viewName);

        /// <summary> �Ű� ������ �ش��ϴ� UIView Class�� VisibleState���� ��ȯ�ϴ� �Լ� </summary>
        public abstract VisibleState GetVisibleState(string viewName);

        /// <summary> Ȱ��ȭ�� UIView���� ��ȸ�ϸ� �ִϸ��̼��� �������� UI�� �ִ��� Ȯ���ϴ� �Լ�(������ false, ������ true) </summary>
        public abstract bool ViewsVisibleStateCheck();
    }

}
