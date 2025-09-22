using Muks.UI;
using UnityEngine;
namespace Muks.MobileUI
{

    public abstract class MobileUIView : UIView
    {
        private Canvas _canvas;
        public Canvas Canvas => _canvas;

        public virtual void OnValidate()
        {
            if (_canvas != null)
                return;

            if (transform.TryGetComponent<Canvas>(out Canvas canvas))
            {
                _canvas = canvas;
            }
        }
    }
}


