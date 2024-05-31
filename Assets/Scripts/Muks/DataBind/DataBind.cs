using System;
using UnityEngine;
using UnityEngine.Events;

namespace Muks.DataBind
{
    /// <summary> 다양한 데이터를 id값으로 묶어 데이터를 저장, 전달하는 클래스 </summary>
    public static class DataBind
    {
        private static DataBindContainer<string> _textBindDic = new DataBindContainer<string>();
        private static DataBindContainer<UnityAction> _unityActionBindDic = new DataBindContainer<UnityAction>();
        private static DataBindContainer<Sprite> _spriteBindDic = new DataBindContainer<Sprite>();


        /// <summary> 데이터를 연결하고 값을 저장하는 함수 </summary>
        public static void SetTextValue(string bindId, string data)
        {
            _textBindDic.SetValue(bindId, data);
        }

        /// <summary> id에 맞는 값을 리턴하는 함수 </summary>
        public static string GetTextValue(string bindId)
        {
            return _textBindDic.GetValue(bindId);
        }

        /// <summary> 데이터를 연결해주는 클래스를 리턴하는 함수 </summary>
        public static BindData<string> GetTextBindData(string bindId)
        {
            return _textBindDic.GetBindData(bindId);
        }



        /// <summary> 데이터를 연결하고 값을 저장하는 함수 </summary>
        public static void SetUnityActionValue(string bindId, UnityAction action)
        {
            _unityActionBindDic.SetValue(bindId, action);
        }

        /// <summary> id에 맞는 값을 리턴하는 함수 </summary>
        public static UnityAction GetUnityActionValue(string bindId)
        {
            return _unityActionBindDic.GetValue(bindId);
        }

        /// <summary> 데이터를 연결해주는 클래스를 리턴하는 함수 </summary>
        public static BindData<UnityAction> GetUnityActionBindData(string bindId)
        {
            return _unityActionBindDic.GetBindData(bindId);
        }



        /// <summary> 데이터를 연결하고 값을 저장하는 함수 </summary>
        public static void SetSpriteValue(string bindId, Sprite sprite)
        {
            _spriteBindDic.SetValue(bindId, sprite);
        }

        /// <summary> id에 맞는 값을 리턴하는 함수 </summary>
        public static Sprite GetSpriteValue(string bindId)
        {
            return _spriteBindDic.GetValue(bindId);
        }

        /// <summary> 데이터를 연결해주는 클래스를 리턴하는 함수 </summary>
        public static BindData<Sprite> GetSpriteBindData(string bindId)
        {
            return _spriteBindDic.GetBindData(bindId);
        }
    }
}
