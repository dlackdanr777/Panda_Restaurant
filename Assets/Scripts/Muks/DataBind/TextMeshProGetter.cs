using TMPro;
using UnityEngine;

namespace Muks.DataBind
{

    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextMeshProGetter : MonoBehaviour
    {
        [SerializeField] private string _bindId;

        private BindData<string> _data;
        private TextMeshProUGUI _text;


        private void Awake()
        {
            if (string.IsNullOrEmpty(_bindId))
            {
                Debug.LogErrorFormat("Invalid text data ID. {0}", gameObject.name);
                enabled = false;
            }

            _text = GetComponent<TextMeshProUGUI>();
            _data = DataBind.GetTextBindData(_bindId);
            _data.CallBack += UpdateText;

        }


        private void OnEnable()
        {
            _text.text = _data.Item;
        }


        private void UpdateText(string text)
        {
            if (enabled)
                _text.text = text;
        }


        private void OnDestroy()
        {
            _data.CallBack -= UpdateText;
            _data = null;
            _text = null;
        }
    }
}

