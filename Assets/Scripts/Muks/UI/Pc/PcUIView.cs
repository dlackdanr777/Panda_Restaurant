using Muks.UI;
using System;
using UnityEngine.EventSystems;

namespace Muks.PcUI
{
    public abstract class PcUIView : UIView, IPointerDownHandler
    {
        /// <summary> UI창을 클릭했을때 실행될 대리자 </summary>
        public Action OnFocus;


        /// <summary>해당 UI 창을 클릭하면 실행될 함수</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            OnFocus?.Invoke();
        }
    }
}

