using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITitleAndDescription : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private TextMeshProUGUI _description;

    public void SetText(string titleText, string description)
    {
        _title.text = titleText;
        _description.text = description;
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
