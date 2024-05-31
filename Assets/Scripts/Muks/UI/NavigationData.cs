using System;
using UnityEngine;

namespace Muks.UI
{
    [Serializable]
    public class NavigationData
    {
        /// <summary>특정 상황에서 UI를 맨 앞으로 내보낼 것인가 여부 </summary>
        [SerializeField] private bool _focusEnabled = true;
        public bool FocusEnabled => _focusEnabled;

        [SerializeField] private UINavigation _uiNav;
        public UINavigation UiNav => _uiNav;

    }
}
