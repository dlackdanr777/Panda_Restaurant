using UnityEngine;


[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    private float _targetAspect = 2.3333f;


    private void Awake()
    {
        AdjstCamera();
    }


    /// <summary>
    /// 카메라 비율을 해상도에 맞춰 변경하는 함수
    /// </summary>
    private void AdjstCamera()
    {
        Camera camera = GetComponent<Camera>();
        float deviceAspect = (float)Screen.width / Screen.height;
        float scaleHeight = deviceAspect / _targetAspect;

        if(scaleHeight < 1.0f)
            camera.orthographicSize = camera.orthographicSize / scaleHeight;
    }



}
