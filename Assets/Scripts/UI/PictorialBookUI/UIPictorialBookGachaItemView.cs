using Muks.Tween;
using NUnit.Framework.Constraints;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIPictorialBookGachaItemView : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject _blackImage;
    [SerializeField] private ParticleSystem _flashEffect;
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
    [SerializeField] private TextMeshProUGUI _effectDescriptionText;
    [SerializeField] private UIImageAndText _addScoreLayout;
    [SerializeField] private UIImageAndText _tipPerMinuteLayout;
    [SerializeField] private UIButtonAndText _upgradeButton;
    [SerializeField] private UITextAndText _giveCountGroup;

    [Space]
    [Header("Audios")]
    [SerializeField] private AudioClip _upgradeSound;


    private GachaItemData _data;


    public void Init()
    {
        _upgradeButton.AddListener(OnUpgradeButtonClicked);
    }


    public void SetData(GachaItemData data)
    {
        _data = data;
        UpdateData();

        _itemImage.TweenStop();
        _itemImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        Color itemColor = UserInfo.IsGiveGachaItem(data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        itemColor.a = 0;
        _itemImage.color = itemColor;
        _itemImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _itemImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
    }

    public void ChoiceView()
    {
        if (_data == null)
            return;

        UpdateData();

        _itemImage.TweenStop();
        _itemImage.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        Color itemColor = UserInfo.IsGiveGachaItem(_data.Id) ? Utility.GetColor(ColorType.Give) : Utility.GetColor(ColorType.NoGive);
        itemColor.a = 0;
        _itemImage.color = itemColor;
        _itemImage.TweenAlpha(1, 0.25f, Ease.OutQuint);
        _itemImage.TweenScale(Vector3.one, 0.25f, Ease.OutBack);
    }

    private void UpgradeView()
    {
        UpdateData();

        _itemImage.TweenStop();
        _itemImage.transform.localScale = new Vector3(1f, 1f, 1f);
        _itemImage.TweenScale(new Vector3(0.9f, 0.9f, 0.9f), 0.35f, Ease.OutBack);
        _itemImage.TweenScale(Vector3.one, 0.15f, Ease.OutBack);
        _descriptionText.text = _data.Description;
    }


    private void UpdateData()
    {
        if (_data == null)
        {
            _blackImage.gameObject.SetActive(true);
            _itemImage.gameObject.SetActive(false);
            _addScoreLayout.gameObject.SetActive(false);
            _tipPerMinuteLayout.gameObject.SetActive(false);
            _giveItemFillAmount.SetActive(false);
            _upgradeButton.gameObject.SetActive(false);
            _giveCountGroup.gameObject.SetActive(false);
            SetStar(GachaItemRank.Length);
            _itemNameText.text = string.Empty;
            _descriptionText.text = string.Empty;
            _data = null;
            return;
        }

        if (UserInfo.IsGiveGachaItem(_data))
        {
            _blackImage.gameObject.SetActive(false);
            _giveItemFillAmount.SetActive(true);
            _upgradeButton.gameObject.SetActive(true);
            _giveCountGroup.gameObject.SetActive(true);
            _itemNameText.text = _data.Name;
            int requiredItemCount = UserInfo.GetUpgradeRequiredItemCount(_data);
            int giveItemCount = UserInfo.GetGiveItemCount(_data);
            int level = UserInfo.GetGachaItemLevel(_data);
            _giveCountGroup.SetText1(giveItemCount.ToString());
            _itemCountText.text = requiredItemCount == 0 ? "최대 업그레이드" : giveItemCount + "/" + requiredItemCount;
            _giveItemFillAmount.SetFillAmount(requiredItemCount == 0 ? 1 : giveItemCount <= 0 ? 0 : (float)giveItemCount / requiredItemCount);
            if (UserInfo.IsGachaItemUpgradeEnabled(_data))
            {
                _upgradeButton.SetText(level <= 0 ? "Lv.Max" : "Lv." + level);
                _upgradeButton.Interactable(UserInfo.IsGachaItemUpgradeRequirementMet(_data));
            }
            else
            {
                _upgradeButton.SetText(level <= 0 ? "Lv.Max" : "Lv." + level);
                _upgradeButton.Interactable(false);
            }

        }
        else
        {
            _blackImage.gameObject.SetActive(true);
            _giveItemFillAmount.SetActive(false);
            _upgradeButton.gameObject.SetActive(false);
            _giveCountGroup.gameObject.SetActive(false);
            _itemNameText.text = "???";
        }

        _itemImage.gameObject.SetActive(true);
        _addScoreLayout.gameObject.SetActive(true);
        _tipPerMinuteLayout.gameObject.SetActive(true);

        _descriptionText.text = _data.Description;
        _addScoreLayout.SetText(_data.AddScore.ToString());
        _tipPerMinuteLayout.SetText(Utility.ConvertToMoney(_data.TipPerMinute));
        _effectDescriptionText.text = Utility.GetGachaItemEffectDescription(_data);
        SetStar(_data.GachaItemRank);
        _itemImage.sprite = _data.Sprite;
        Utility.ChangeImagePivot(_itemImage);
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
            SoundManager.Instance.PlayEffectAudio(EffectType.UI, _upgradeSound);
            _flashEffect.Emit(1);
            UpgradeView();
        }
        else
        {
            PopupManager.Instance.ShowDisplayText("알 수 없는 오류 발생");
        }
    }
}
