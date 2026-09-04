using System.Collections;
using Muks.Tween;
using UnityEngine;
using UnityEngine.UI;

public class UIItemStar : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject _star1;
    [SerializeField] private GameObject _star2;
    [SerializeField] private GameObject _star3;
    [SerializeField] private GameObject _star4;
    [SerializeField] private GameObject _star5;

    private GameObject[] _stars;
    private Vector3[] _defaultScales;
    private Color[] _defaultColors;
    private Coroutine _showRoutine;

    private void EnsureDefaults()
    {
        if (_stars != null)
            return;

        _stars = new[] { _star1, _star2, _star3, _star4, _star5 };
        _defaultScales = new Vector3[_stars.Length];
        _defaultColors = new Color[_stars.Length];

        for (int i = 0; i < _stars.Length; i++)
        {
            _defaultScales[i] = _stars[i].transform.localScale;
            Image image = _stars[i].GetComponent<Image>();
            _defaultColors[i] = image == null ? Color.white : image.color;
        }
    }

    public void Clear()
    {
        EnsureDefaults();

        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }

        for (int i = 0; i < _stars.Length; i++)
        {
            ResetVisual(i);
            _stars[i].SetActive(false);
        }
    }

    public void SetStar(Rank rank)
    {
        SetStar(rank, false);
    }

    public void SetStar(Rank rank, bool animate)
    {
        Clear();
        int count = GetStarCount(rank);

        if (animate && gameObject.activeInHierarchy)
            _showRoutine = StartCoroutine(ShowStars(count));
        else
            for (int i = 0; i < count; i++)
                ShowImmediately(i);
    }

    private IEnumerator ShowStars(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int starIndex = i;
            GameObject star = _stars[starIndex];
            Image image = star.GetComponent<Image>();

            star.SetActive(true);
            star.transform.SetAsLastSibling();
            star.transform.localScale = _defaultScales[starIndex] * 0.65f;

            if (image != null)
            {
                image.enabled = true;
                Color startColor = _defaultColors[starIndex];
                startColor.a = 0f;
                image.color = startColor;
                image.TweenAlpha(_defaultColors[starIndex].a, 0.16f, Ease.OutQuad);
            }

            star.TweenScale(_defaultScales[starIndex], 0.2f, Ease.OutBack);
            yield return new WaitForSecondsRealtime(0.045f);
        }

        // TweenTransformScale applies its final value after invoking OnComplete.
        // Reset from the coroutine on a later frame so the tween cannot overwrite it.
        yield return new WaitForSecondsRealtime(0.25f);
        for (int i = 0; i < count; i++)
            ResetVisual(i);

        _showRoutine = null;
    }

    private void ShowImmediately(int index)
    {
        _stars[index].SetActive(true);
        _stars[index].transform.SetAsLastSibling();
        ResetVisual(index);
    }

    private void ResetVisual(int index)
    {
        GameObject star = _stars[index];
        star.TweenStop();
        star.transform.localScale = _defaultScales[index];

        Image image = star.GetComponent<Image>();
        if (image != null)
        {
            image.TweenStop();
            image.enabled = true;
            image.color = _defaultColors[index];
        }

        CanvasGroup canvasGroup = star.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private static int GetStarCount(Rank rank)
    {
        return rank switch
        {
            Rank.Normal1 => 1,
            Rank.Normal2 => 2,
            Rank.Rare => 3,
            Rank.Unique => 4,
            Rank.Special => 5,
            _ => 0
        };
    }
}
