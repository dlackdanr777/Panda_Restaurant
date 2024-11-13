using UnityEngine;
using UnityEngine.UI;

public class UICustomerTutorialTab : MonoBehaviour
{
    [SerializeField] private GameObject[] _pages;
    [SerializeField] private Button _leftArrowButton;
    [SerializeField] private Button _rightArrowButton;
    [SerializeField] private ParticleSystem[] _particles;

    private int _currentPage;


    public void Init()
    {
        _currentPage = 0;
        OnTurnOverPage(0);
        StopTab();
        _leftArrowButton.onClick.RemoveAllListeners();
        _rightArrowButton.onClick.RemoveAllListeners();
        _leftArrowButton.onClick.AddListener(OnLeftArrowButtonClicked);
        _rightArrowButton.onClick.AddListener(OnRightArrowButtonClicked);
    }


    public void StartTab()
    {
        OnTurnOverPage(0);
        for(int i = 0, cnt = _particles.Length; i < cnt; ++i)
        {
            _particles[i].Play();
        }
    }

    public void StopTab()
    {
        for (int i = 0, cnt = _particles.Length; i < cnt; ++i)
        {
            _particles[i].Stop();
        }
    }


    private void OnTurnOverPage(int page)
    {
        _currentPage = Mathf.Clamp(page, 0, _pages.Length - 1);
        for (int i = 0, cnt = _pages.Length; i < cnt; ++i)
        {
            _pages[i].SetActive(false);
        }

        _pages[_currentPage].SetActive(true);
        ArrowButtonSetActive();

    }

    private void OnLeftArrowButtonClicked()
    {
        OnTurnOverPage(_currentPage - 1);
    }

    private void OnRightArrowButtonClicked()
    {
        OnTurnOverPage(_currentPage + 1);
    }


    private void ArrowButtonSetActive()
    {
        _leftArrowButton.gameObject.SetActive(0 < _currentPage);
        _rightArrowButton.gameObject.SetActive(_currentPage < _pages.Length - 1);
    }
}
