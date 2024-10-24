using Muks.Tween;
using NUnit.Framework.Constraints;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBookGachaItemView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject _blackImage;
    [SerializeField] private Image _specialFrameImage;
    [SerializeField] private Image _uniqueFrameImage;
    [SerializeField] private Image _rareFrameImage;
    [SerializeField] private Image _normalFrameImage;
    [SerializeField] private Image _itemImage;
    [SerializeField] private UIItemStar _itemStar;
    [SerializeField] private UIImageFillAmount _giveItemFillAmount;
    [SerializeField] private TextMeshProUGUI _itemCountText;
    [SerializeField] private TextMeshProUGUI _itemNameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private UIImageAndText _addScoreLayout;
    [SerializeField] private UIImageAndText _tipPerMinuteLayout;
    [SerializeField] private UIButtonAndText _upgradeButton;

    private GachaItemData _data;


    public void Init()
    {
        _upgradeButton.AddListener(OnUpgradeButtonClicked);
    }


    public void SetData(GachaItemData data)
    {
        if (data == _data)
            return;

        if (data == null)
        {
            _blackImage.gameObject.SetActive(true);
            _itemImage.gameObject.SetActive(false);
            _addScoreLayout.gameObject.SetActive(false);
            _tipPerMinuteLayout.gameObject.SetActive(false);
            _giveItemFillAmount.SetActive(false);
            _upgradeButton.gameObject.SetActive(false); 
            SetStar(GachaItemRank.Length);
            _itemNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _data = null;

            return;
        }

        _data = data;

        if(UserInfo.IsGiveGachaItem(data))
        {
            _blackImage.gameObject.SetActive(false);
            _giveItemFillAmount.SetActive(true);
            _upgradeButton.gameObject.SetActive(true);
            _itemNameText.text = data.Name;
            int requiredItemCount = UserInfo.GetUpgradeRequiredItemCount(data);
            int giveItemCount = UserInfo.GetGiveItemCount(data);
            _itemCountText.text = giveItemCount + "/" + requiredItemCount;
            _giveItemFillAmount.SetFillAmount(giveItemCount <= 0 ? 0 : (float)giveItemCount / requiredItemCount);

            if(UserInfo.IsGachaItemUpgradeEnabled(data))
            {
                _upgradeButton.SetText("업그레이드");
                _upgradeButton.Interactable(UserInfo.IsGachaItemUpgradeRequirementMet(data));
            }
            else
            {
                _upgradeButton.SetText("최대 업그레이드");
                _upgradeButton.Interactable(false);
            }

        }
        else
        {
            _blackImage.gameObject.SetActive(true);
            _giveItemFillAmount.SetActive(false);
            _itemNameText.text = "???";
        }

        _itemImage.gameObject.SetActive(true);
        _addScoreLayout.gameObject.SetActive(true);
        _tipPerMinuteLayout.gameObject.SetActive(true);

        _addScoreLayout.SetText(data.AddScore.ToString());
        _tipPerMinuteLayout.SetText(Utility.ConvertToMoney(data.TipPerMinute));

        SetStar(data.GachaItemRank);
        _itemImage.sprite = _data.Sprite;


        Utility.ChangeImagePivot(_itemImage);
        _itemImage.TweenStop();
        _itemImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        Color itemColor = UserInfo.IsGiveGachaItem(data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        itemColor.a = 0;
        _itemImage.color = itemColor;
        _itemImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _itemImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
        _descriptionText.text = data.Description;

    }

    public void ChoiceView()
    {
        if (_data == null)
            return;

        _itemImage.sprite = _data.Sprite;
        Utility.ChangeImagePivot(_itemImage);
        _itemImage.TweenStop();
        _itemImage.TweenStop();
        _itemImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        Color itemColor = UserInfo.IsGiveGachaItem(_data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        itemColor.a = 0;
        _itemImage.color = itemColor;
        _itemImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _itemImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
    }


    private void SetStar(GachaItemRank rank)
    {
        _itemStar.SetStar(rank);
        switch (rank)
        {
            case GachaItemRank.Normal1:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Normal2:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Rare:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(true);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Unique:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(true);
                _specialFrameImage.gameObject.SetActive(false);
                break;

            case GachaItemRank.Special:
                _normalFrameImage.gameObject.SetActive(false);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(true);
                break;

            default:
                _normalFrameImage.gameObject.SetActive(true);
                _rareFrameImage.gameObject.SetActive(false);
                _uniqueFrameImage.gameObject.SetActive(false);
                _specialFrameImage.gameObject.SetActive(false);
                break;
        }
    }

    private void OnUpgradeButtonClicked()
    {
        if (_data == null)
            throw new System.Exception("아이템이 null인 상태로 강화 버튼을 클릭했습니다.");

        if(UserInfo.UpgradeGachaItem(_data))
        {
            TimedDisplayManager.Instance.ShowText("아이템 업그레이드 성공!");
        }
        else
        {
            TimedDisplayManager.Instance.ShowText("알 수 없는 오류 발생");
        }
    }
}
