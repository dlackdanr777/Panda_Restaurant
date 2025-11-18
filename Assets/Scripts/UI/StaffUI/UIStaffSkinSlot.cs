using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class UIStaffSkinSlot : MonoBehaviour
{
    [SerializeField] private Image _skinImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Button _button;
    [SerializeField] private Image _normalLayer;
    [SerializeField] private Image _rareLayer;
    [SerializeField] private Image _uniqueLayer;
    [SerializeField] private Image[] _specialLayers;

    private StaffSkinData _skinData;
    private StaffData _customerData;

    private Action<StaffData, StaffSkinData> _onClickAction;

    public void Init(Action<StaffData, StaffSkinData> onClickAction)
    {
        _onClickAction = onClickAction;
        _button.onClick.AddListener(OnButtonClicked);

        UserInfo.OnGiveStaffSkinHandler += OnGiveStaffSkin;
    }

    private void SetData(StaffData data)
    {
        if (data == null)
        {
            _customerData = null;
            _skinImage.sprite = null;
            _nameText.text = string.Empty;
            return;
        }

        _customerData = data;
        _skinImage.sprite = data.ThumbnailSprite;
        _skinImage.color = Utility.GetColor(ColorType.None);
        _nameText.text = data.Name;
    }

    public void SetData(StaffData data, StaffSkinData skinData)
    {
        _normalLayer.gameObject.SetActive(false);
        _rareLayer.gameObject.SetActive(false);
        foreach (var layer in _specialLayers)
        {
            layer.gameObject.SetActive(false);
        }
        _uniqueLayer.gameObject.SetActive(false);

        if (skinData == null)
        {
            _normalLayer.gameObject.SetActive(true);
            _skinData = null;
            SetData(data);
            return;
        }
        _customerData = data;
        _skinData = skinData;
        _skinImage.sprite = skinData.ThumbnailSprite;
        DebugLog.Log($"Set Skin Slot: {data.Name} - {skinData.Name} {UserInfo.IsGiveStaffSkin(skinData.Id)}");
        _skinImage.color = UserInfo.IsGiveStaffSkin(skinData.Id) ? Utility.GetColor(ColorType.None) : Utility.GetColor(ColorType.NoGive);
        _nameText.text = skinData.Name;

        switch (skinData.Rank)
        {
            case Rank.Normal1:
                _normalLayer.gameObject.SetActive(true);
                break;
            case Rank.Normal2:
                _normalLayer.gameObject.SetActive(true);
                break;
            case Rank.Rare:
                _rareLayer.gameObject.SetActive(true);
                break;
            case Rank.Unique:
                _uniqueLayer.gameObject.SetActive(true);
                break;
            case Rank.Special:
                foreach (var layer in _specialLayers)
                {
                    layer.gameObject.SetActive(true);
                }
                break;
        }
    }

    private void OnButtonClicked()
    {
        _onClickAction?.Invoke(_customerData, _skinData);
    }

    private void OnGiveStaffSkin()
    {
        if (_customerData == null && !gameObject.activeInHierarchy)
            return;

        SetData(_customerData, _skinData);
    }

    private void OnDestroy()
    {
        UserInfo.OnGiveStaffSkinHandler -= OnGiveStaffSkin;
    }
}
