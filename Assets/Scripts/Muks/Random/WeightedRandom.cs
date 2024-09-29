using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Muks.WeightedRandom
{

    /// <summary>가중치 랜덤 뽑기 시스템 클래스</summary>
    public class WeightedRandom<T>
    {
        private Dictionary<T, float> _itemDic;
        public int Count => _itemDic.Count;

        public WeightedRandom()
        {
            _itemDic = new Dictionary<T, float>();
        }


        /// <summary>가중치 리스트에 아이템과 수량을 추가함</summary>
        public void Add(T item, float value)
        {
            //음수가 들어오면 리턴한다.
            if (value < 0)
            {
                Debug.LogError("음수는 들어갈 수 없습니다.");
                return;
            }

            if (_itemDic.ContainsKey(item))
                _itemDic[item] += value;

            else
                _itemDic.Add(item, value);
        }


        /// <summary>가중치 리스트에 아이템이 있으면 지정 수량을 빼고, 지정 수량이 더 크면 리스트에서 아이템을 뺌</summary>
        public void Sub(T item, float value)
        {
            //음수가 들어오면 리턴한다.
            if (value < 0)
            {
                Debug.LogError("음수는 들어갈 수 없습니다.");
                return;
            }

            //만약 딕셔너리에 키가 존재하면?
            if (_itemDic.ContainsKey(item))
            {
                //키의 값의 크기가 더 크면?
                if (value < _itemDic[item])
                    _itemDic[item] -= value;

                else
                    Remove(item);
            }
            else
            {
                Debug.LogError("아이템이 존재하지 않습니다.");
            }
        }


        /// <summary> 리스트에서 아이템을 삭제 </summary>
        public void Remove(T item)
        {
            //만약 딕셔너리에 키가 존재하면?
            if (_itemDic.ContainsKey(item))
            {
                //해당 키의 데이터를 삭제한다.
                _itemDic.Remove(item);
            }
            else
            {
                Debug.LogError("아이템이 존재하지 않습니다.");
            }
        }


        /// <summary>현재 리스트에 있는 아이템의 가중치를 모두 더해 반환</summary>
        public int TotalWeight()
        {
            int totalWeight = 0;

            //딕셔너리에 입력된 모든 아이템 가중치 값을 더한다.
            foreach (int value in _itemDic.Values)
            {
                totalWeight += value;
            }

            return totalWeight;
        }


        /// <summary>아이템 리스트에 있는 모든 아이템의 가중치를 비율로 변환하여 반환 (0, 1 사이)</summary>
        public Dictionary<T, float> GetPercent()
        {
            Dictionary<T, float> _tempDic = new Dictionary<T, float>();
            float totalWeight = TotalWeight();

            foreach (var item in _itemDic)
            {
                _tempDic.Add(item.Key, item.Value / totalWeight);
            }

            return _tempDic;
        }


        /// <summary> 아이템 리스트에서 랜덤으로 아이템을 뽑아 반환(뽑힌 아이템의 갯수 -1) </summary>
        public T GetRamdomItemAfterSub()
        {
            //딕셔너리에 들어있는 아이템 갯수가 0이하면
            if (_itemDic.Count <= 0)
            {
                Debug.LogError("리스트에 아이템이 없습니다. 뽑기 불가능");
                return default;
            }

            //가중치 순으로 내림차순 정렬
            _itemDic = _itemDic.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            //총 가중치를 가져온다.
            float weight = 0;
            float totalWeight = TotalWeight();

            //총 가중치 값에 0~1f의 랜덤 값을 곱해 기준점을 구한다.
            float pivot = totalWeight * RandomRange(0.0f, 1.0f);

            //딕셔너리를 순회하며 가중치를 더하다 기준점 이상이 되면 그 아이템을 반환한다.
            foreach (var item in _itemDic)
            {
                weight += item.Value;
                if (pivot <= weight)
                {
                    
                    _itemDic[item.Key] -= 1;

                    if (_itemDic[item.Key] <= 0)
                        Remove(item.Key);

                    return item.Key;
                }
            }
            return default;
        }


        /// <summary> 아이템 리스트에서 랜덤으로 아이템을 뽑아 반환 </summary>
        public T GetRamdomItem()
        {
            //딕셔너리에 들어있는 아이템 갯수가 0이하면 리턴
            if (_itemDic.Count <= 0)
            {
                Debug.LogError("리스트에 아이템이 없습니다. 뽑기 불가능");
                return default;
            }

            //가중치 순으로 내림차순 정렬
            _itemDic = _itemDic.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x=> x.Value);

            //총 가중치를 가져온다.
            float totalWeight = TotalWeight();
            float weight = 0;

            //총 가중치 값에 0~1f의 랜덤 값을 곱해 기준점을 구한다.
            float pivot = totalWeight * RandomRange(0.0f, 1.0f);

            //딕셔너리를 순회하며 가중치를 더하다 기준점 이상이 되면 그 아이템을 반환한다.
            foreach (var item in _itemDic)
            {
                weight += item.Value;
                if (pivot <= weight)
                {
                    return item.Key;
                }
            }
            return default;
        }


        /// <summary> 아이템 리스트를 반환 </summary>
        public Dictionary<T, float> GetList()
        {
            return _itemDic;
        }


        /// <summary>RandomNumberGenerator를 이용, 범위 안의 난수를 반환하는 함수</summary>
        private int RandomRange(int min, int max)
        {
            if (max < min)
                throw new ArgumentOutOfRangeException("The Min value is higher than the Max value.");

            byte[] bytes = new byte[4];

            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            int randInt = BitConverter.ToInt32(bytes, 0);
            int returnInt = Math.Abs(randInt % (max - min + 1)) + min;

            return returnInt;
        }


        /// <summary>RandomNumberGenerator를 이용, 범위 안의 난수를 반환하는 함수</summary>
        private float RandomRange(float min, float max)
        {
            if (max < min)
                throw new ArgumentOutOfRangeException("The Min value is higher than the Max value.");

            byte[] bytes = new byte[4];
            
            using(RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            int randInt = BitConverter.ToInt32(bytes, 0);
            float rand01 = (float)Math.Abs(randInt) / int.MaxValue;
            float randFloat = min + rand01 * (max - min);

            return randFloat;
        }

    }
}
