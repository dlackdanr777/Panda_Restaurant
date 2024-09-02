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
        public static TweenData TweenMove(this Component target, Vector3 targetPosition, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformMove tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformMove>();

            TweenDataSequence tmpData = new TweenDataSequence(targetPosition, duration, ease, null);

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
        public static TweenData TweenMove(this GameObject target, Vector3 targetPosition, float duration, Ease ease = Ease.Constant)
        {
            return TweenMove(target.transform, targetPosition, duration, ease);
        }


        /// <summary>목표 X값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
        public static TweenData TweenMoveX(this Component target, float targetPosX, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformMoveX tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformMoveX>();

            TweenDataSequence tmpData = new TweenDataSequence(targetPosX, duration, ease, null);

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

        /// <summary>목표 X값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
        public static TweenData TweenMoveX(this GameObject target, float targetPosX, float duration, Ease ease = Ease.Constant)
        {
            return TweenMoveX(target.transform, targetPosX, duration, ease);
        }


        /// <summary>목표 Y값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
        public static TweenData TweenMoveY(this Component target, float targetPosY, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformMoveY tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformMoveY>();

            TweenDataSequence tmpData = new TweenDataSequence(targetPosY, duration, ease, null);

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

        /// <summary>목표 Y값으로 지속 시간동안 오브젝트를 이동시키는 함수</summary>
        public static TweenData TweenMoveY(this GameObject target, float targetPosY, float duration, Ease ease = Ease.Constant)
        {
            return TweenMoveY(target.transform, targetPosY, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 오브젝트를 회전시키는 함수</summary>
        public static TweenData TweenRotate(this Component target, Vector3 targetEulerAngles, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformRotate tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformRotate>();

            TweenDataSequence tmpData = new TweenDataSequence(targetEulerAngles, duration, ease, null);

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
        public static TweenData TweenRotate(this GameObject target, Vector3 targetEulerAngles, float duration, Ease ease = Ease.Constant)
        {
            return TweenRotate(target.transform, targetEulerAngles, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 오브젝트의 크기를 조절하는 함수</summary>
        public static TweenData TweenScale(this Component target, Vector3 targetScale, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTransformScale tweenData))
                tweenData = target.gameObject.AddComponent<TweenTransformScale>();

            TweenDataSequence tmpData = new TweenDataSequence(targetScale, duration, ease, null);

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
        public static TweenData TweenScale(this GameObject target, Vector3 targetScale, float duration, Ease ease = Ease.Constant)
        {
            return TweenScale(target.transform, targetScale, duration, ease);
        }

        /// <summary>목표 값으로 지속 시간동안 오브젝트를 점프하며 이동하는 함수</summary>
        public static TweenData TweenJump(this Component target, Vector3 targetPos, float jumpHeight, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenJump tweenData))
                tweenData = target.gameObject.AddComponent<TweenJump>();

            TweenDataSequence tmpData = new TweenDataSequence(targetPos, duration, ease, null);

            tweenData.IsLoop = false;
            tweenData.AddDataSequence(tmpData);
            tweenData.SetHeight(jumpHeight);

            if (!tweenData.enabled)
            {
                tweenData.ElapsedDuration = 0;
                tweenData.TotalDuration = 0;
                tweenData.enabled = true;
            }

            return tweenData;
        }

        /// <summary>목표 값으로 지속 시간동안 오브젝트를 점프하며 이동하는 함수</summary>
        public static TweenData TweenJump(this GameObject target, Vector3 targetPos, float jumpHeight, float duration, Ease ease = Ease.Constant)
        {
            return TweenJump(target, targetPos, jumpHeight, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 UI 사이즈를 조절하는 함수</summary>
        public static TweenData TweenSizeDelta(this RectTransform target, Vector2 targetSizeDelta, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenRectTransformSizeDelta tweenData))
                tweenData = target.gameObject.AddComponent<TweenRectTransformSizeDelta>();

            TweenDataSequence tmpData = new TweenDataSequence(targetSizeDelta, duration, ease, target);

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
        public static TweenData TweenAnchoredPosition(this RectTransform target, Vector2 targetAnchoredPosition, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenRectTransformAnchoredPosition tweenData))
                tweenData = target.gameObject.AddComponent<TweenRectTransformAnchoredPosition>();

            TweenDataSequence tmpData = new TweenDataSequence(targetAnchoredPosition, duration, ease, target);

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
        public static TweenData TweenColor(this Text target, Color targetColor, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTextColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenTextColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, ease, target);

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
        public static TweenData TweenAlpha(this Text target, float targetAlpha, float duration, Ease ease = Ease.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 Text.text 값을 변경하는 함수</summary>
        public static TweenData TweenText(this Text target, string targetString, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTextContents tweenData))
                tweenData = target.gameObject.AddComponent<TweenTextContents>();

            TweenDataSequence tmpData = new TweenDataSequence(targetString, duration, ease, target);

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

        /// <summary>목표 값으로 한글자 출력 시간 * 총 글자수 동안 text를 변경하는 함수</summary>
        public static TweenData TweenCharacter(this Text target, string targetString, float characterInterval, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTextContents tweenData))
                tweenData = target.gameObject.AddComponent<TweenTextContents>();

            float duration = targetString.Length * characterInterval;
            TweenDataSequence tmpData = new TweenDataSequence(targetString, duration, ease, target);

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
        public static TweenData TweenColor(this TextMeshProUGUI target, Color targetColor, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTMPColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenTMPColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, ease, target);

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
        public static TweenData TweenAlpha(this TextMeshProUGUI target, float targetAlpha, float duration, Ease ease = Ease.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 TMP.text를 변경하는 함수</summary>
        public static TweenData TweenText(this TextMeshProUGUI target, string targetString, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTMPContents tweenData))
                tweenData = target.gameObject.AddComponent<TweenTMPContents>();

            TweenDataSequence tmpData = new TweenDataSequence(targetString, duration, ease, target);

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

        /// <summary>목표 값으로 한글자 출력 시간 * 총 글자수 동안 TMP.text를 변경하는 함수</summary>
        public static TweenData TweenCharacter(this TextMeshProUGUI target, string targetString, float characterInterval, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenTMPContents tweenData))
                tweenData = target.gameObject.AddComponent<TweenTMPContents>();

            float duration = targetString.Length * characterInterval;
            TweenDataSequence tmpData = new TweenDataSequence(targetString, duration, ease, target);

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
        public static TweenData TweenColor(this Image target, Color targetColor, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenImageColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenImageColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, ease, target);

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
        public static TweenData TweenAlpha(this Image target, float targetAlpha, float duration, Ease ease = Ease.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 FillAmount 값을 변경하는 함수</summary>
        public static TweenData TweenFillAmount(this Image target, float targetFill, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenImageFillAmount tweenData))
                tweenData = target.gameObject.AddComponent<TweenImageFillAmount>();

            TweenDataSequence tmpData = new TweenDataSequence(targetFill, duration, ease, target);

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
        public static TweenData TweenColor(this SpriteRenderer target, Color targetColor, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenSpriteRendererColor tweenData))
                tweenData = target.gameObject.AddComponent<TweenSpriteRendererColor>();

            TweenDataSequence tmpData = new TweenDataSequence(targetColor, duration, ease, target);

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
        public static TweenData TweenAlpha(this SpriteRenderer target, float targetAlpha, float duration, Ease ease = Ease.Constant)
        {
            Color targetColor = new Color(target.color.r, target.color.g, target.color.b, targetAlpha);
            return TweenColor(target, targetColor, duration, ease);
        }


        /// <summary>목표 값으로 지속 시간동안 캔버스 그룹 알파 값을 변경하는 함수</summary>
        public static TweenData TweenAlpha(this CanvasGroup target, float targetAlpha, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenCanvasGroupAlpha tweenData))
                tweenData = target.gameObject.AddComponent<TweenCanvasGroupAlpha>();

            TweenDataSequence tmpData = new TweenDataSequence(targetAlpha, duration, ease, target);

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
        public static TweenData TweenSpacing(this HorizontalOrVerticalLayoutGroup target, float targetValue, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenLayoutGroupSpacing tweenData))
                tweenData = target.gameObject.AddComponent<TweenLayoutGroupSpacing>();

            TweenDataSequence tmpData = new TweenDataSequence(targetValue, duration, ease, target);

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
        public static TweenData TweenOrthographicSize(this Camera target, float targetSize, float duration, Ease ease = Ease.Constant)
        {
            if (!target.TryGetComponent(out TweenCameraSize tweenData))
                tweenData = target.gameObject.AddComponent<TweenCameraSize>();

            TweenDataSequence tmpData = new TweenDataSequence(targetSize, duration, ease, target);

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

