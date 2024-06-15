using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStaffPreview : MonoBehaviour
{
    [SerializeField] private Image _staffImage;
    [SerializeField] private TextMeshProUGUI _effectDescription;


    public void SetStaff(StaffData data)
    {
        if(data == null)
        {
            _staffImage.gameObject.SetActive(false);
            _effectDescription.gameObject.SetActive(false);
            return;
        }

        _staffImage.gameObject.SetActive(true);
        _effectDescription.gameObject.SetActive(true);
        _staffImage.sprite = data.Sprite;
        _effectDescription.text = data.Description;
    }
}
