using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManagementStaffPreview : MonoBehaviour
{
 [Header("Components")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _effectDescriptionText;
    [SerializeField] private TextMeshProUGUI _skillDescriptionText;




    private StaffData _data;


    public void Init()
    {
        _closeButton.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }


    public void Show(StaffData data)
    {
        _data = data;
        UpdateData();
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }




    private void UpdateData()
    {
        _itemNameText.text = _data.Name;
        _descriptionText.text = _data.Description;
        _effectDescriptionText.text = Utility.GetStaffEffectDescription(_data);
        _skillDescriptionText.text = Utility.GetStaffSkillDescription(_data);
        //SetStar(_data.GachaItemRank);
        _image.sprite = _data.Sprite;
    }


    // private void SetStar(Rank rank)
    // {
    //     _itemStar.SetStar(rank);
    //     switch (rank)
    //     {
    //         case Rank.Normal1:
    //             _normalFrameImage.gameObject.SetActive(true);
    //             _rareFrameImage.gameObject.SetActive(false);
    //             _uniqueFrameImage.gameObject.SetActive(false);
    //             _specialFrameImage.gameObject.SetActive(false);
    //             break;

    //         case Rank.Normal2:
    //             _normalFrameImage.gameObject.SetActive(true);
    //             _rareFrameImage.gameObject.SetActive(false);
    //             _uniqueFrameImage.gameObject.SetActive(false);
    //             _specialFrameImage.gameObject.SetActive(false);
    //             break;

    //         case Rank.Rare:
    //             _normalFrameImage.gameObject.SetActive(false);
    //             _rareFrameImage.gameObject.SetActive(true);
    //             _uniqueFrameImage.gameObject.SetActive(false);
    //             _specialFrameImage.gameObject.SetActive(false);
    //             break;

    //         case Rank.Unique:
    //             _normalFrameImage.gameObject.SetActive(false);
    //             _rareFrameImage.gameObject.SetActive(false);
    //             _uniqueFrameImage.gameObject.SetActive(true);
    //             _specialFrameImage.gameObject.SetActive(false);
    //             break;

    //         case Rank.Special:
    //             _normalFrameImage.gameObject.SetActive(false);
    //             _rareFrameImage.gameObject.SetActive(false);
    //             _uniqueFrameImage.gameObject.SetActive(false);
    //             _specialFrameImage.gameObject.SetActive(true);
    //             break;

    //         default:
    //             _normalFrameImage.gameObject.SetActive(true);
    //             _rareFrameImage.gameObject.SetActive(false);
    //             _uniqueFrameImage.gameObject.SetActive(false);
    //             _specialFrameImage.gameObject.SetActive(false);
    //             break;
    //     }
    // }
}
