using System;


namespace Muks.DataBind
{
    public class BindData<T>
    {
        public event Action<T> CallBack;

        private T _item;
        public T Item
        {
            get
            {
                return _item;
            }
            set
            {
                _item = value;
                CallBack?.Invoke(_item);
            }
        }
    }
}