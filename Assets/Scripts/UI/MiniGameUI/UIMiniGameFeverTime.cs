using System;
using UnityEngine;
using UnityEngine.UI;

public class UIMiniGameFeverTime : MonoBehaviour
{
    [Serializable]
    public struct FeverTimeSpriteData
    {
        [SerializeField] private Image _image;
        public Image Image => _image;

        [SerializeField] private Sprite _normalSprite;
        public Sprite NormalSprite => _normalSprite;

        [SerializeField] private Sprite _feverSprite;
        public Sprite FeverSprite => _feverSprite;
    }

    [Serializable]
    public struct FeverTimeSetActiveData
    {
        [SerializeField] private GameObject _gameObject;
        public GameObject GameObject => _gameObject;

        [SerializeField] private bool _feverSetActive;
        public bool FeverSetActive => _feverSetActive;
    }

    [SerializeField] private FeverTimeSpriteData[] _feverTimeDatas;
    [SerializeField] private FeverTimeSetActiveData[] _feverTimeSetActiveDatas;

    public void SetNormalSprite()
    {
        for(int i = 0, cnt = _feverTimeDatas.Length; i < cnt; ++i)
        {
            _feverTimeDatas[i].Image.sprite = _feverTimeDatas[i].NormalSprite;
        }

        for(int i = 0, cnt = _feverTimeSetActiveDatas.Length; i < cnt; ++i)
        {
            _feverTimeSetActiveDatas[i].GameObject.SetActive(!_feverTimeSetActiveDatas[i].FeverSetActive);
        }
    }


    public void SetFeverSprite()
    {
        for (int i = 0, cnt = _feverTimeDatas.Length; i < cnt; ++i)
        {
            _feverTimeDatas[i].Image.sprite = _feverTimeDatas[i].FeverSprite;
        }

        for (int i = 0, cnt = _feverTimeSetActiveDatas.Length; i < cnt; ++i)
        {
            _feverTimeSetActiveDatas[i].GameObject.SetActive(_feverTimeSetActiveDatas[i].FeverSetActive);
        }
    }
}
