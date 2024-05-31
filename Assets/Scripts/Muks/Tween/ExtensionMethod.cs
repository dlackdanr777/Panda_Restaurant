using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Muks.Tween
{

    public static class ExtensionMethod
    {
        /// <summary>일시정지된 모든 Tween 클래스 시작</summary>
        public static void TweenRestart(this Component target)
        {
            TweenData[] tweens = target.GetComponents<TweenData>();

            foreach (TweenData tween in tweens)
            {
                tween.enabled = true;
            }
        }

        /// <summary>일시정지된 모든 Tween 클래스 시작</summary>
        public static void TweenRestart(this GameObject target)
        {
            TweenRestart(target.transform);
        }


        /// <summary>해당 오브젝트의 모든 Tween 정지 함수</summary>
        public static void TweenStop(this Component target)
        {
            TweenData[] tweens = target.GetComponents<TweenData>();

            foreach (TweenData tween in tweens)
            {
                tween.Clear();
            }
        }

        /// <summary>해당 오브젝트의 모든 Tween 정지 함수</summary>
        public static void TweenStop(this GameObject target)
        {
            TweenStop(target.transform);
        }


        /// <summary>해당 오브젝트의 모든 Tween 일시 정지 함수(Play() 호출 전까지 정지)</summary>
        public static void TweenPause(this Component target)
        {
            TweenData[] tweens = target.GetComponents<TweenData>();

            foreach (TweenData tween in tweens)
            {
                tween.enabled = false;
            }
        }

        /// <summary>해당 오브젝트의 모든 Tween 일시 정지 함수(Play() 호출 전까지 정지)</summary>
        public static void TweenPause(this GameObject target)
        {
            TweenPause(target.transform);
        }


        /// <summary>목표 값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
        public static TweenData TweenMove(this Component target, Vector3 targetPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformMove tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformMove>();

            TweenDataSequence tmpData = new TweenDataSequence(targetPosition, duration, tweenMode, null);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }

        /// <summary>목표 값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
        public static TweenData TweenMove(this GameObject target, Vector3 targetPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            return TweenMove(target.transform, targetPosition, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 오브젝트를 회전시키는 함수</summary>
        public static TweenData TweenRotate(this Component target, Vector3 targetEulerAngles, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformRotate tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformRotate>();

            TweenDataSequence tmpData = new TweenDataSequence(targetEulerAngles, duration, tweenMode, null);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }

        /// <summary>목표 값으로 지속 시간동안 오브젝트를 회전시키는 함수</summary>
        public static TweenData TweenRotate(this GameObject target, Vector3 targetEulerAngles, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            return TweenRotate(target.transform, targetEulerAngles, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 오브젝트의 크기를 조절하는 함수</summary>
        public static TweenData TweenScale(this Component target, Vector3 targetScale, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformScale tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformScale>();

            TweenDataSequence tmpData = new TweenDataSequence(targetScale, duration, tweenMode, null);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }

        /// <summary>목표 값으로 지속 시간동안 오브젝트의 크기를 조절하는 함수</summary>
        public static TweenData TweenScale(this GameObject target, Vector3 targetScale, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            return TweenScale(target.transform, targetScale, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 UI 사이즈를 조절하는 함수</summary>
        public static TweenData TweenSizeDelta(this RectTransform target, Vector2 targetSizeDelta, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenRectTransformSizeDelta tweenData))
                tweenData = target.gameObject.AddComponent<TweenRectTransformSizeDelta>();

            TweenDataSequence tmpData = new TweenDataSequence(targetSizeDelta, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 UI의 위치를 이동시키는 함수</summary>
        public static TweenData TweenAnchoredPosition(this RectTransform target, Vector2 targetAnchoredPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenRectTransformAnchoredPosition tweenData))
                tweenData = target.gameObject.AddComponent<TweenRectTransformAnchoredPosition>();

            TweenDataSequence tmpData = new TweenDataSequence(targetAnchoredPosition, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 텍스트 컬러 값을 변경하는 함수</summary>
        public static TweenData TweenColor(this Text target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTextColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenTextColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 텍스트 알파 값을 변경하는 함수</summary>
        public static TweenData TweenAlpha(this Text target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 Text.text 값을 변경하는 함수</summary>
        public static TweenData TweenText(this Text target, string targetString, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTextContents tweenData))
                tweenData = target.gameObject.AddComponent<TweenTextContents>();

            TweenDataSequence tmpData = new TweenDataSequence(targetString, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }




        /// <summary>목표 값으로 지속 시간동안 TMP 컬러 값을 변경하는 함수</summary>
        public static TweenData TweenColor(this TextMeshProUGUI target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTMPColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenTMPColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 TMP 알파 값을 변경하는 함수</summary>
        public static TweenData TweenAlpha(this TextMeshProUGUI target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 TMP.text를 변경하는 함수</summary>
        public static TweenData TweenText(this TextMeshProUGUI target, string targetString, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenTMPContents tweenData))
                tweenData = target.gameObject.AddComponent<TweenTMPContents>();

            TweenDataSequence tmpData = new TweenDataSequence(targetString, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 이미지 컬러 값을 변경하는 함수</summary>
        public static TweenData TweenColor(this Image target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenImageColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenImageColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 이미지 알파 값을 변경하는 함수</summary>
        public static TweenData TweenAlpha(this Image target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 스프라이트 렌더러 컬러 값을 변경하는 함수</summary>
        public static TweenData TweenColor(this SpriteRenderer target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenSpriteRendererColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenSpriteRendererColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 스프라이트 렌더러 알파 값을 변경하는 함수</summary>
        public static TweenData TweenAlpha(this SpriteRenderer target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, tweenMode);
        }


        /// <summary>목표 값으로 지속 시간동안 캔버스 그룹 알파 값을 변경하는 함수</summary>
        public static TweenData TweenAlpha(this CanvasGroup target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenCanvasGroupAlpha tweenData))
                tweenData = target.gameObject.AddComponent<TweenCanvasGroupAlpha>();

            TweenDataSequence tmpData = new TweenDataSequence(targetAlpha, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 LayoutGroup의 spacing 수치를 조절하는 함수</summary>
        public static TweenData TweenSpacing(this HorizontalOrVerticalLayoutGroup target, float targetValue, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenLayoutGroupSpacing tweenData))
                tweenData = target.gameObject.AddComponent<TweenLayoutGroupSpacing>();

            TweenDataSequence tmpData = new TweenDataSequence(targetValue, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }


        /// <summary>목표 값으로 지속 시간동안 카메라 사이즈 값을 변경하는 함수</summary>
        public static TweenData TweenOrthographicSize(this Camera target, float targetSize, float duration, TweenMode tweenMode = TweenMode.Constant)
        {
            if (!target.TryGetComponent(out TweenCameraSize tweenData))
                tweenData = target.gameObject.AddComponent<TweenCameraSize>();

            TweenDataSequence tmpData = new TweenDataSequence(targetSize, duration, tweenMode, target);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }

    }
}

    //===============================사용법이 수정됨에 따라 기존 클래스 주석 처리===============================

    /*    /// <summary>트윈 애니메이션을 위한 정적 클래스</summary>
        public static class Tween
        {

            /// <summary>해당 오브젝트의 일시정지된 모든 Tween 실행 함수</summary>
            public static void Play(Component target)
            {
                TweenData[] tweens = target.GetComponents<TweenData>();

                foreach (TweenData tween in tweens)
                {
                    tween.enabled = true;
                }
            }

            /// <summary>해당 오브젝트의 일시정지된 모든 Tween 실행 함수</summary>
            public static void Play(GameObject target)
            {
                Play(target.transform);
            }


            /// <summary>해당 오브젝트의 모든 Tween 정지 함수</summary>
            public static void Stop(Component target)
            {
                TweenData[] tweens = target.GetComponents<TweenData>();

                foreach (TweenData tween in tweens)
                {
                    tween.Clear();
                }
            }

            /// <summary>해당 오브젝트의 모든 Tween 정지 함수</summary>
            public static void Stop(GameObject target)
            {
                Stop(target.transform);
            }


            /// <summary>해당 오브젝트의 모든 Tween 일시 정지 함수(Play() 호출 전까지 정지)</summary>
            public static void Pause(Component target)
            {
                TweenData[] tweens = target.GetComponents<TweenData>();

                foreach (TweenData tween in tweens)
                {
                    tween.enabled = false;
                }
            }

            /// <summary>해당 오브젝트의 모든 Tween 일시 정지 함수(Play() 호출 전까지 정지)</summary>
            public static void Pause(GameObject target)
            {
                Pause(target.transform);
            }


            //===================================== 이곳부터 애니메이션 실행 함수 =====================================//

            /// <summary>목표 값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
            public static TweenData TransformMove(Component target, Vector3 targetPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTransformMove tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTransformMove>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetPosition;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
            public static TweenData TransformMove(GameObject target, Vector3 targetPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TransformMove(target.transform, targetPosition, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 오브젝트를 회전시키는 함수</summary>
            public static TweenData TransformRotate(Component target, Vector3 targetEulerAngles, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTransformRotate tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTransformRotate>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetEulerAngles;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 오브젝트를 회전시키는 함수</summary>
            public static TweenData TransformRotate(GameObject target, Vector3 targetEulerAngles, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TransformRotate(target.transform, targetEulerAngles, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 오브젝트의 크기를 조절하는 함수</summary>
            public static TweenData TransformScale(Component target, Vector3 targetScale, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTransformScale tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTransformScale>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetScale;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 오브젝트의 크기를 조절하는 함수</summary>
            public static TweenData TransformScale(GameObject target, Vector3 targetScale, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TransformScale(target.transform, targetScale, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 UI 사이즈를 조절하는 함수</summary>
            public static TweenData RectTransfromSizeDelta(Component target, Vector2 targetSizeDelta, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenRectTransformSizeDelta tweenData))
                    tweenData = target.gameObject.AddComponent<TweenRectTransformSizeDelta>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetSizeDelta;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 UI 사이즈를 조절하는 함수</summary>
            public static TweenData RectTransfromSizeDelta(GameObject target, Vector2 targetSizeDelta, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return RectTransfromSizeDelta(target.transform, targetSizeDelta, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 UI의 위치를 이동시키는 함수</summary>
            public static TweenData RectTransfromAnchoredPosition(Component target, Vector2 targetAnchoredPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenRectTransformAnchoredPosition tweenData))
                    tweenData = target.gameObject.AddComponent<TweenRectTransformAnchoredPosition>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetAnchoredPosition;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 UI의 위치를 이동시키는 함수</summary>
            public static TweenData RectTransfromAnchoredPosition(GameObject target, Vector2 targetAnchoredPosition, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return RectTransfromAnchoredPosition(target.transform, targetAnchoredPosition, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 텍스트 컬러 값을 변경하는 함수</summary>
            public static TweenData TextColor(Component target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTextColor tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTextColor>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetColor;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 텍스트 컬러 값을 변경하는 함수</summary>
            public static TweenData TextColor(GameObject target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TextColor(target.transform, targetColor, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 텍스트 알파 값을 변경하는 함수</summary>
            public static TweenData TextAlpha(Component target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTextAlpha tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTextAlpha>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetAlpha;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 텍스트 알파 값을 변경하는 함수</summary>
            public static TweenData TextAlpha(GameObject target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TextAlpha(target.transform, targetAlpha, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 TMP 컬러 값을 변경하는 함수</summary>
            public static TweenData TMPColor(Component target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTMPColor tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTMPColor>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetColor;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 TMP 컬러 값을 변경하는 함수</summary>
            public static TweenData TMPColor(GameObject target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TMPColor(target.transform, targetColor, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 TMP 알파 값을 변경하는 함수</summary>
            public static TweenData TMPAlpha(Component target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenTMPAlpha tweenData))
                    tweenData = target.gameObject.AddComponent<TweenTMPAlpha>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetAlpha;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 TMP 알파 값을 변경하는 함수</summary>
            public static TweenData TMPAlpha(GameObject target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return TMPAlpha(target.transform, targetAlpha, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 이미지 컬러 값을 변경하는 함수</summary>
            public static TweenData IamgeColor(Component target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenImageColor tweenData))
                    tweenData = target.gameObject.AddComponent<TweenImageColor>();

                DataSequence tempData = new DataSequence();
                tempData.TargetValue = targetColor;
                tempData.Duration = duration;
                tempData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tempData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 이미지 컬러 값을 변경하는 함수</summary>
            public static TweenData IamgeColor(GameObject target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return IamgeColor(target.transform, targetColor, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 이미지 알파 값을 변경하는 함수</summary>
            public static TweenData IamgeAlpha(Component target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenImageAlpha tweenData))
                    tweenData = target.gameObject.AddComponent<TweenImageAlpha>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetAlpha;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 이미지 알파 값을 변경하는 함수</summary>
            public static TweenData IamgeAlpha(GameObject target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return IamgeAlpha(target.transform, targetAlpha, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 스프라이트 렌더러 컬러 값을 변경하는 함수</summary>
            public static TweenData SpriteRendererColor(Component target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenSpriteRendererColor tweenData))
                    tweenData = target.gameObject.AddComponent<TweenSpriteRendererColor>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetColor;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 스프라이트 렌더러 컬러 값을 변경하는 함수</summary>
            public static TweenData SpriteRendererColor(GameObject target, Color targetColor, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return SpriteRendererColor(target.transform, targetColor, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 스프라이트 렌더러 알파 값을 변경하는 함수</summary>
            public static TweenData SpriteRendererAlpha(Component target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenSpriteRendererAlpha tweenData))
                    tweenData = target.gameObject.AddComponent<TweenSpriteRendererAlpha>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetAlpha;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 스프라이트 렌더러 알파 값을 변경하는 함수</summary>
            public static TweenData SpriteRendererAlpha(GameObject target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return SpriteRendererAlpha(target.transform, targetAlpha, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 캔버스 그룹 알파 값을 변경하는 함수</summary>
            public static TweenData CanvasGroupAlpha(Component target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenCanvasGroupAlpha tweenData))
                    tweenData = target.gameObject.AddComponent<TweenCanvasGroupAlpha>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetAlpha;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 캔버스 그룹 알파 값을 변경하는 함수</summary>
            public static TweenData CanvasGroupAlpha(GameObject target, float targetAlpha, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return CanvasGroupAlpha(target.transform, targetAlpha, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 LayoutGroup의 spacing 수치를 조절하는 함수</summary>
            public static TweenData LayoutGroupSpacing(Component target, float targetValue, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenLayoutGroupSpacing tweenData))
                    tweenData = target.gameObject.AddComponent<TweenLayoutGroupSpacing>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetValue;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 LayoutGroup의 spacing 수치를 조절하는 함수</summary>
            public static TweenData LayoutGroupSpacing(GameObject target, float targetValue, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return LayoutGroupSpacing(target.transform, targetValue, duration, tweenMode);
            }


            /// <summary>목표 값으로 지속 시간동안 카메라 사이즈 값을 변경하는 함수</summary>
            public static TweenData CameraOrthographicSize(Component target, float targetSize, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                if (!target.TryGetComponent(out TweenCameraSize tweenData))
                    tweenData = target.gameObject.AddComponent<TweenCameraSize>();

                DataSequence tmpData = new DataSequence();
                tmpData.TargetValue = targetSize;
                tmpData.Duration = duration;
                tmpData.TweenMode = tweenMode;

                tweenData.IsLoop = false;
                tweenData.AddDataSequence(tmpData);

                if (!tweenData.enabled)
                {
                    tweenData.ElapsedDuration = 0;
                    tweenData.TotalDuration = 0;
                    tweenData.enabled = true;
                }

                return tweenData;
            }

            /// <summary>목표 값으로 지속 시간동안 카메라 사이즈 값을 변경하는 함수</summary>
            public static TweenData CameraOrthographicSize(GameObject target, float targetSize, float duration, TweenMode tweenMode = TweenMode.Constant)
            {
                return CameraOrthographicSize(target.transform, targetSize, duration, tweenMode);
            }


         *//*   /// <summary>목표 값으로 지속 시간동안 2D Light 알파 값을 변경하는 함수</summary>
            public static TweenData Light2DIntensity(GameObject targetObject, float targetIntensity, float duration, TweenMode tweenMode = TweenMode.Constant, Action onComplete = null, Action onUpdate = null)
            {
                TweenLight2DIntensity objToLight2D = !targetObject.GetComponent<TweenLight2DIntensity>()
                    ? targetObject.AddComponent<TweenLight2DIntensity>()
                    : targetObject.GetComponent<TweenLight2DIntensity>();

                DataSequence tempData = new DataSequence();
                tempData.TargetValue = targetIntensity;
                tempData.Duration = duration;
                tempData.TweenMode = tweenMode;
                tempData.OnComplete = onComplete;
                tempData.OnUpdate = onUpdate;

                objToLight2D.IsLoop = false;
                objToLight2D.AddDataSequence(tempData);

                if (!objToLight2D.enabled)
                {
                    objToLight2D.ElapsedDuration = 0;
                    objToLight2D.TotalDuration = 0;
                    objToLight2D.enabled = true;
                }

                return objToLight2D;
            }*//*

        }*/

