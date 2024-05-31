using UnityEngine;
using UnityEngine.UI;


namespace Muks.DataBind
{
    [RequireComponent(typeof(Image))]
    public class ImageGetter : MonoBehaviour
    {
        [SerializeField] private string _bindId;

        private Image _image;
        private BindData<Sprite> _data;


        private void Awake()
        {
            if (string.IsNullOrEmpty(_bindId))
            {
                Debug.LogErrorFormat("Invalid text data ID. {0}", gameObject.name);
                enabled = false;
            }

            _image = GetComponent<Image>();
            _data = DataBind.GetSpriteBindData(_bindId);
            _data.CallBack += UpdateSprite;
        }


        private void OnEnable()
        {
            _image.sprite = _data.Item;
        }


        private void OnDestroy()
        {
            _data.CallBack -= UpdateSprite;
            _image = null;
            _data = null;
        }


        private void UpdateSprite(Sprite sprite)
        {
            if (enabled)
                _image.sprite = sprite;
        }
    }
}

