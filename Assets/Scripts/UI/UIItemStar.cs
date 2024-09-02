using UnityEngine;

public class UIItemStar : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject _star1;
    [SerializeField] private GameObject _star2;
    [SerializeField] private GameObject _star3;
    [SerializeField] private GameObject _star4;
    [SerializeField] private GameObject _star5;

    public void SetStar(GachaItemRank rank)
    {
        switch (rank)
        {
            case GachaItemRank.Normal1:
                _star1.SetActive(true);
                _star2.SetActive(false);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                break;

            case GachaItemRank.Normal2:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                break;

            case GachaItemRank.Rare:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(false);
                _star5.SetActive(false);
                break;

            case GachaItemRank.Unique:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(true);
                _star5.SetActive(false);
                break;

            case GachaItemRank.Special:
                _star1.SetActive(true);
                _star2.SetActive(true);
                _star3.SetActive(true);
                _star4.SetActive(true);
                _star5.SetActive(true);
                break;

            default:
                _star1.SetActive(false);
                _star2.SetActive(false);
                _star3.SetActive(false);
                _star4.SetActive(false);
                _star5.SetActive(false);
                break;

        }
    }
}
