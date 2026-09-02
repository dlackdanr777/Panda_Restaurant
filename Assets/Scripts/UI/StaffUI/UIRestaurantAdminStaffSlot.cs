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

    public void SetEquipText(string typeText)
    {
        _equipGroup.SetText1(typeText);
    }

    public void EquipGroupSetActive(bool value)
    {
        _equipGroup.gameObject.SetActive(value);
    }

    public void SetGacha(Sprite sprite)
    {
        SetOperate(sprite, "???", "뽑기");
        if (_operateButtonImage != null && _gachaButtonSprite != null)
            _operateButtonImage.SetSprite(_gachaButtonSprite);

        SetPortraitTint(Utility.GetColor(ColorType.NoGive));
        HideUnownedShade();
        ApplySlotOwnershipVisual(false);
    }

    public void SetOwnedVisual()
    {
        if (_operateButtonImage != null && _operateButtonSprite != null)
            _operateButtonImage.SetSprite(_operateButtonSprite);

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
            portraitRect.anchoredPosition = isOwned ? OwnedPortraitPosition : UnownedPortraitPosition;
            portraitRect.sizeDelta = isOwned ? OwnedPortraitSize : UnownedPortraitSize;
        }

        Transform nameTransform = transform.Find("Name Text");
        if (nameTransform != null)
        {
            TextMeshProUGUI nameText = nameTransform.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.gameObject.SetActive(isOwned);
        }
    }

    private void SetPortraitTint(Color color)
    {
        if (_unownedShadeImage == null || _unownedShadeImage.transform.parent == null)
            return;

        Image portraitImage = _unownedShadeImage.transform.parent.GetComponent<Image>();
        if (portraitImage != null)
            portraitImage.color = color;
    }

    private void HideUnownedShade()
    {
        if (_unownedShadeImage == null)
            return;

        _unownedShadeImage.enabled = false;
        _unownedShadeImage.gameObject.SetActive(false);
    }

    public void SetFrame(Rank rank)
    {
        bool useRe02Frame = _rankFrameImage != null
                            && _normalRankFrameSprite != null
                            && _rareRankFrameSprite != null
                            && _uniqueRankFrameSprite != null
                            && _specialRankFrameSprite != null;

        _normalFrame.SetActive(!useRe02Frame && (rank == Rank.Normal1 || rank == Rank.Normal2));
        _rareFrame.SetActive(!useRe02Frame && rank == Rank.Rare);
        _uniqueFrame.SetActive(!useRe02Frame && rank == Rank.Unique);

        for (int i = 0; i < _specialFrames.Length; ++i)
            _specialFrames[i].SetActive(!useRe02Frame && rank == Rank.Special);

        if (!useRe02Frame)
            return;

        _rankFrameImage.gameObject.SetActive(true);
        _rankFrameImage.sprite = rank switch
        {
            Rank.Rare => _rareRankFrameSprite,
            Rank.Unique => _uniqueRankFrameSprite,
            Rank.Special => _specialRankFrameSprite,
            _ => _normalRankFrameSprite
        };
    }
}
