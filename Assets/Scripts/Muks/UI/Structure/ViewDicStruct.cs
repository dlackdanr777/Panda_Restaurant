using System;
using UnityEngine;

namespace Muks.MobileUI
{
    [Serializable]
    public struct MobileViewDicStruct
    {
        [Tooltip("Key")]
        public string Name;

        [Tooltip("Value")]
        public MobileUIView UIView;
    }
}


namespace Muks.PcUI
{
    [Serializable]
    public struct PcViewDicStruct
    {
        [Tooltip("Key")]
        public string Name;

        [Tooltip("Value")]
        public PcUIView UIView;
    }
}