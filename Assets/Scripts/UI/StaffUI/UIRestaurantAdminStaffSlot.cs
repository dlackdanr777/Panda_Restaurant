using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIRestaurantAdminStaffSlot : UIRestaurantAdminSlot
{
    private static readonly Vector2 OwnedPortraitPosition = new Vector2(0f, 15.357f);
    private static readonly Vector2 OwnedPortraitSize = new Vector2(100f, 89.715f);
    private static readonly Vector2 UnownedPortraitPosition = new Vector2(0f, -4.5f);
    private static readonly Vector2 UnownedPortraitSize = new Vector2(112f, 100.48f);

    [Space]
    [Header("Staff")]
    [SerializeField] private UITextAndText _equipGroup;
    [SerializeField] private GameObject _normalFrame;
    [SerializeField] private GameObject _rareFrame;
    [SerializeField] private GameObject _uniqueFrame;
    [SerializeField] private GameObject[] _specialFrames;
    [SerializeField] private Image _rankFrameImage;
    [SerializeField] private Image _unownedShadeImage;
    [SerializeField] private Sprite _normalRankFrameSprite;
    [SerializeField] private Sprite _rareRankFrameSprite;
    [SerializeField] private Sprite _uniqueRankFrameSprite;
    [SerializeField] private Sprite _specialRankFrameSprite;
    [SerializeField] private UIImageAndText _operateButtonImage;
    [SerializeField] private Sprite _operateButtonSprite;
    [SerializeField] private Sprite _gachaButtonSprite;

    private StaffData _boundStaff;
    private StaffSkinData _boundSkin;
    private bool _boundOwned;
    private ERestaurantFloorType _boundFloor;
    private EquipStaffType _boundRole;
    private Sprite _boundSprite;
    private string _boundName;
    private Rank _boundRank;

    internal bool RefreshStaff(StaffData data)
    {
        bool owned = UserInfo.IsGiveStaff(UserInfo.CurrentStage, data);
        var floor = owned ? UserInfo.GetEquipStaffFloorType(UserInfo.CurrentStage, data) : ERestaurantFloorType.Error;
        bool equipped = owned && UserInfo.IsEquipStaff(UserInfo.CurrentStage, data);
        var role = equipped ? UserInfo.GetEquipStaffType(UserInfo.CurrentStage, data) : EquipStaffType.Length;
        var skin = owned ? UserInfo.GetEquipStaffSkin(UserInfo.CurrentStage, data) : null;
        Sprite sprite = data.ThumbnailSprite == null ? data.Sprite : data.ThumbnailSprite;
        string name = data.Name;
        if (ReferenceEquals(_boundStaff, data) && _boundOwned == owned && _boundFloor == floor
            && _boundRole == role && _boundSkin == skin && _boundSprite == sprite
            && _boundName == name && _boundRank == data.Rank) return false;
        _boundStaff = data; _boundOwned = owned; _boundFloor = floor; _boundRole = role;
        _boundSkin = skin; _boundSprite = sprite; _boundName = name; _boundRank = data.Rank;
        SetFrame(data.Rank);
        EquipGroupSetActive(equipped);
        if (equipped) SetEquipText(Utility.StaffTypeStringConverter(role));
        if (!owned) SetGacha(sprite);
        else
        {
            SetOwnedVisual();
            if (floor <= ERestaurantFloorType.Floor3) SetUse(sprite, name, "배치중", floor);
            else SetOperate(sprite, name, "배치하기");
        }
        return true;
    }

    public void SetEquipText(string typeText)
    {
        if (_equipGroup.Text1.text != typeText) _equipGroup.SetText1(typeText);
    }

    public void EquipGroupSetActive(bool value)
    {
        SetActive(_equipGroup.gameObject, value);
    }

    public void SetGacha(Sprite sprite)
    {
        SetOperate(sprite, "???", "뽑기", Utility.GetColor(ColorType.NoGive));
        if (_operateButtonImage != null && _gachaButtonSprite != null && _buttonSprite != _gachaButtonSprite)
        {
            _operateButtonImage.SetSprite(_gachaButtonSprite);
            _buttonSprite = _gachaButtonSprite;
        }

        SetPortraitTint(Utility.GetColor(ColorType.NoGive));
        HideUnownedShade();
        ApplySlotOwnershipVisual(false);
    }

    public void SetOwnedVisual()
    {
        if (_operateButtonImage != null && _operateButtonSprite != null && _buttonSprite != _operateButtonSprite)
        {
            _operateButtonImage.SetSprite(_operateButtonSprite);
            _buttonSprite = _operateButtonSprite;
        }

        SetPortraitTint(Utility.GetColor(ColorType.Give));
        HideUnownedShade();
        ApplySlotOwnershipVisual(true);
    }

    private void ApplySlotOwnershipVisual(bool isOwned)
    {
        if (_unownedShadeImage == null || _unownedShadeImage.transform.parent == null)
            return;

        if (_unownedShadeImage.transform.parent is RectTransform portraitRect)
        {
            Vector2 position = isOwned ? OwnedPortraitPosition : UnownedPortraitPosition;
            Vector2 size = isOwned ? OwnedPortraitSize : UnownedPortraitSize;
            if (portraitRect.anchoredPosition != position) portraitRect.anchoredPosition = position;
            if (portraitRect.sizeDelta != size) portraitRect.sizeDelta = size;
        }

        Transform nameTransform = transform.Find("Name Text");
        if (nameTransform != null)
        {
            TextMeshProUGUI nameText = nameTransform.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                SetActive(nameText.gameObject, isOwned);
        }
    }

    private void SetPortraitTint(Color color)
    {
        if (_unownedShadeImage == null || _unownedShadeImage.transform.parent == null)
            return;

        Image portraitImage = _unownedShadeImage.transform.parent.GetComponent<Image>();
        if (portraitImage != null && portraitImage.color != color)
            portraitImage.color = color;
    }

    private void HideUnownedShade()
    {
        if (_unownedShadeImage == null)
            return;

        if (_unownedShadeImage.enabled) _unownedShadeImage.enabled = false;
        SetActive(_unownedShadeImage.gameObject, false);
    }

    public void SetFrame(Rank rank)
    {
        bool useRe02Frame = _rankFrameImage != null
                            && _normalRankFrameSprite != null
                            && _rareRankFrameSprite != null
                            && _uniqueRankFrameSprite != null
                            && _specialRankFrameSprite != null;

        SetActive(_normalFrame, !useRe02Frame && (rank == Rank.Normal1 || rank == Rank.Normal2));
        SetActive(_rareFrame, !useRe02Frame && rank == Rank.Rare);
        SetActive(_uniqueFrame, !useRe02Frame && rank == Rank.Unique);

        for (int i = 0; i < _specialFrames.Length; ++i)
            SetActive(_specialFrames[i], !useRe02Frame && rank == Rank.Special);

        if (!useRe02Frame)
            return;

        SetActive(_rankFrameImage.gameObject, true);
        Sprite frame = rank switch
        {
            Rank.Rare => _rareRankFrameSprite,
            Rank.Unique => _uniqueRankFrameSprite,
            Rank.Special => _specialRankFrameSprite,
            _ => _normalRankFrameSprite
        };
        if (_rankFrameImage.sprite != frame) _rankFrameImage.sprite = frame;
    }

    private Sprite _buttonSprite;

    private static void SetActive(GameObject target, bool active)
    {
        if (target.activeSelf != active) target.SetActive(active);
    }
}
