using Muks.Tween;
using UnityEngine;


[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    private float _targetAspect = 2.3333f;


    private void Awake()
    {
        AdjstCamera();
    }


    // Update is called once per frame
    private void AdjstCamera()
    {
        Camera camera = GetComponent<Camera>();
        float deviceAspect = (float)Screen.width / Screen.height;
        float scaleHeight = deviceAspect / _targetAspect;

        if(scaleHeight < 1.0f)
            camera.orthographicSize = camera.orthographicSize / scaleHeight;
    }



}
