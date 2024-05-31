using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Muks.DataBind
{
    [RequireComponent(typeof(Button))]
    public class ButtonGetter : MonoBehaviour
    {
        [SerializeField] private string _bindId;

        private BindData<UnityAction> _data;
        private Button _button;
        private UnityAction _action;


        private void Awake()
        {
            if (string.IsNullOrEmpty(_bindId))
            {
                Debug.LogWarningFormat("Invalid text data ID. {0}", gameObject.name);
                enabled = false;
            }

            _button = GetComponent<Button>();
            _data = DataBind.GetUnityActionBindData(_bindId);
            _data.CallBack += UpdateAction;
        }


        private void OnEnable()
        {
            if (_data.Item == null)
                return;

            if (_action != null)
                _button.onClick.RemoveListener(_action);

            _action = _data.Item;
            _button.onClick.AddListener(_action);
        }


        private void UpdateAction(UnityAction action)
        {
            if (!enabled)
                return;

            if (action == null)
                return;

            if (_action != null)
                _button.onClick.RemoveListener(_action);

            _action = action;
            _button.onClick.AddListener(action);
        }


        private void OnDestroy()
        {
            _data.CallBack -= UpdateAction;
            _data = null;
            _action = null;
            _button = null;
        }

/*
        private int GetListenerNum()
        {
            FieldInfo field = typeof(UnityEventBase).GetField("m_Calls", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            object invokeCallList = field.GetValue(_button.onClick);
            PropertyInfo property = invokeCallList.GetType().GetProperty("Count");
            return (int)property.GetValue(invokeCallList);
        }
*/
    }
}