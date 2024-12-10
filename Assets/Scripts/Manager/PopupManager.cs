using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance
    {
        get
        {
            if(_instance == null)
            {
                GameObject obj = new GameObject("PopupManager");
                _instance = obj.AddComponent<PopupManager>();
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }
    }



    private static PopupManager _instance;
    private static Canvas _canvas;
    private static UITimeDisplay _timeDisplay;
    private static UIPopup _uiPopup;




    public void ShowDisplayText(string description)
    {
        _timeDisplay.Show(description);
    }

    public void ShowTextLackMoney()
    {
        _timeDisplay.Show("코인이 부족합니다...");
    }

    public void ShowTextLackDia()
    {
        _timeDisplay.Show("다이아가 부족합니다...");
    }

    public void ShowTextLackScore()
    {
        _timeDisplay.Show("평점이 부족합니다...");
    }

    public void ShowTextError()
    {
        _timeDisplay.Show("다시 시도해 주세요.");
    }


    private void Awake()
    {
        if (_instance != null)
            return;

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Init();
    }


    private static void Init()
    {
        Canvas canvasPrefab = Resources.Load<Canvas>("UI/PopupManager Canvas");
        UITimeDisplay prefab = Resources.Load<UITimeDisplay>("UI/UITimeDisplay");
        UIPopup popupPrefab = Resources.Load<UIPopup>("UI/UIPopup");
        _canvas = Instantiate(canvasPrefab, _instance.transform);
        _timeDisplay = Instantiate(prefab, _canvas.transform);
        _timeDisplay.Init();

        _uiPopup = Instantiate(popupPrefab, _canvas.transform);
        _uiPopup.Init();
    }

}
