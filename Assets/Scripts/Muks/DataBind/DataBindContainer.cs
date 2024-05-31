using System;
using System.Collections.Generic;

namespace Muks.DataBind 
{
    /// <summary> BindData 클래스를 저장,불러오기를 하는 클래스 </summary>
    public class DataBindContainer<T>
    {

        private Dictionary<string, BindData<T>> _dataContainerDic = new Dictionary<string, BindData<T>>();


        /// <summary> 데이터를 연결하고 값을 저장하는 함수 </summary>
        public void SetValue(string bindId, T data)
        {
            if (!_dataContainerDic.TryGetValue(bindId, out BindData<T> bindData))
            {
                bindData = new BindData<T>();
                _dataContainerDic.Add(bindId, bindData);
            }

            bindData.Item = data;
        }


        /// <summary> id에 맞는 값을 리턴하는 함수 </summary>
        public T GetValue(string bindId)
        {
            if (!_dataContainerDic.TryGetValue(bindId, out BindData<T> textData))
                throw new Exception("bindData not exist");

            return textData.Item;
        }


        /// <summary> 데이터를 연결해주는 클래스를 리턴하는 함수 </summary>
        public BindData<T> GetBindData(string bindId)
        {
            if (!_dataContainerDic.TryGetValue(bindId, out BindData<T> textData))
            {
                textData = new BindData<T>();
                _dataContainerDic.Add(bindId, textData);
                //UnityEngine.Debug.LogWarning("bindData not exist");
                return textData;
            }

            return textData;
        }
    }
}