using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(WorldToSceenPosition), typeof(UIImageFillAmount))]
public class UIBurnerTimer : MonoBehaviour
{
    [SerializeField] private Image _foodImage;

    private WorldToSceenPosition _worldToSceen;
    private UIImageFillAmount _imageFillAmount;

    private void Awake()
    {
        _worldToSceen = GetComponent<WorldToSceenPosition>();
        _imageFillAmount = GetComponent<UIImageFillAmount>();
    }

    public void SetImage(Sprite sprite)
    {
        _foodImage.sprite = sprite;
    }

    public void SetActive(bool value)
    {
        gameObject.SetActive(value);
    }


    public void SetWorldTransform(Transform tr)
    {
        _worldToSceen.SetWorldTransform(tr);
    }

    public void SetFillAmount(float value)
    {
        _imageFillAmount.SetFillAmount(value);
    }

}
