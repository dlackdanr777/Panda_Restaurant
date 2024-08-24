using Muks.Tween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICustomerView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _npcImage;
    [SerializeField] private TextMeshProUGUI _npcNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _needItemText;
    [SerializeField] private TextMeshProUGUI _needItemDescription;
    [SerializeField] private TextMeshProUGUI _effectText;
    [SerializeField] private TextMeshProUGUI _effectDescription;

    private CustomerData _data;


    public void SetData(CustomerData data)
    {
        if (data == _data)
            return;

        _normalFrameImage.gameObject.SetActive(true);
        _rareFrameImage.gameObject.SetActive(false);
        _uniqueFrameImage.gameObject.SetActive(false);
        _specialFrameImage.gameObject.SetActive(false);

        if (data == null)
        {
            _npcImage.gameObject.SetActive(false);
            _needItemText.gameObject.SetActive(false);
            _effectText.gameObject.SetActive(false);
            _npcNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _needItemDescription.text = string.Empty;
            _effectDescription.text = string.Empty;
            _data = null;
            return;
        }

        _data = data;
        _npcImage.gameObject.SetActive(true);
        _needItemText.gameObject.SetActive(true);
        _effectText.gameObject.SetActive(true);

        _npcImage.sprite = data.Sprite;
        _npcImage.TweenStop();
        _npcImage.color = new Color(_npcImage.color.r, _npcImage.color.g, _npcImage.color.b, 0);
        _npcImage.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
        _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);

        _npcNameText.text = data.Name;
        _descriptionText.text = data.Description;
        _needItemDescription.text = string.Empty; //TODO: 향후 수정
        _effectDescription.text = data.Skill.Description;
    }


    public void ChoiceView()
    {
        if (_data == null)
            return;

        _npcImage.sprite = _data.Sprite;
        _npcImage.TweenStop();
        _npcImage.color = new Color(_npcImage.color.r, _npcImage.color.g, _npcImage.color.b, 0);
        _npcImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        _npcImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _npcImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
    }
}
