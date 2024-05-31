using UnityEngine;
using UnityEngine.UI;


namespace Muks.DataBind
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteRendererGetter : MonoBehaviour
    {
        [SerializeField] private string _bindId;

        private SpriteRenderer _spriteRenderer;
        private BindData<Sprite> _data;


        private void Awake()
        {
            if (string.IsNullOrEmpty(_bindId))
            {
                Debug.LogErrorFormat("Invalid text data ID. {0}", gameObject.name);
                enabled = false;
            }

            _spriteRenderer = GetComponent<SpriteRenderer>();
            _data = DataBind.GetSpriteBindData(_bindId);
            _data.CallBack += UpdateSprite;
        }


        private void OnEnable()
        {
            _spriteRenderer.sprite = _data.Item;
        }


        private void OnDestroy()
        {
            _data.CallBack -= UpdateSprite;
            _data = null;
            _spriteRenderer = null;
        }


        private void UpdateSprite(Sprite sprite)
        {
            if (enabled)
                _spriteRenderer.sprite = sprite;
        }
    }
}

