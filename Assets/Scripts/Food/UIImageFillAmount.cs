using UnityEngine;
using UnityEngine.UI;

public class UIImageFillAmount : MonoBehaviour
{
    [SerializeField] private Image _fillImage;

    public void SetFillAmount(float amount)
    {
        _fillImage.fillAmount = amount;
    }

    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }
}
