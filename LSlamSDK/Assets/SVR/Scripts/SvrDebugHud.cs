using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SvrDebugHud : MonoBehaviour 
{
    public SvrManager svrManager;

    public GameObject PositionWarning;
    public GameObject FramesPerSecond;
    public GameObject Orientation;
    public GameObject Position;

    private Text _warningText;
    private Text _fpsText;
    private Text _orientationText;
    private Text _positionText;

    private float _framesPerSecond = 0;

    private void Awake()
    {
        _warningText = PositionWarning.GetComponent<Text>();
        _fpsText = FramesPerSecond.GetComponent<Text>();
        _orientationText = Orientation.GetComponent<Text>();
        _positionText = Position.GetComponent<Text>();
    }

    private void Start()
    {
        svrManager = SvrManager.Instance;
        Debug.Assert(svrManager != null, "SvrManager object not found");
        if (svrManager != null)
        {
            StartCoroutine(CalculateFramesPerSecond());
        }
        if (_warningText != null) _warningText.gameObject.SetActive(false);
    }

    private void LateUpdate () 
    {
        if (svrManager == null)
            return;

        var headTransform = svrManager.head;

        transform.position = headTransform.position;
        transform.rotation = headTransform.rotation;
		
        Quaternion orientation = headTransform.localRotation;
        if (_orientationText != null && _orientationText.isActiveAndEnabled)
        {
            _orientationText.text = string.Format("{0:F2}, {1:F2}, {2:F2}, {3:F2}", orientation.x, orientation.y, orientation.z, orientation.w);
            _orientationText.color = (svrManager.status.pose & (int)SvrPlugin.TrackingMode.kTrackingOrientation) == 0 ? Color.red : Color.green;
        }

        Vector3 position = headTransform.localPosition;
        if (_positionText != null && _positionText.isActiveAndEnabled)
        {
            _positionText.text = string.Format("{0:F2}, {1:F2}, {2:F2}", position.x, position.y, position.z);
            _positionText.color = (svrManager.status.pose & (int)SvrPlugin.TrackingMode.kTrackingPosition) == 0 && svrManager.settings.trackPosition && (SvrPlugin.Instance.GetTrackingMode() & (int)SvrPlugin.TrackingMode.kTrackingPosition) != 0 ? Color.red : Color.green;
        }

        if (_fpsText != null && _fpsText.isActiveAndEnabled)
        {
            int fps = Mathf.RoundToInt(_framesPerSecond);
            int refreshRate = Mathf.RoundToInt(SvrPlugin.Instance.deviceInfo.displayRefreshRateHz);
            _fpsText.text = string.Format("{0} / {1} FPS", fps, refreshRate);
            _fpsText.color = fps < refreshRate ? Color.yellow : Color.green;
        }

        if (_warningText != null && svrManager.settings.trackPosition && svrManager.settings.trackPosition && (SvrPlugin.Instance.GetTrackingMode() & (int)SvrPlugin.TrackingMode.kTrackingPosition) != 0)
        {
            var isValid = (svrManager.status.pose & (int)SvrPlugin.TrackingMode.kTrackingPosition) != 0;
            _warningText.gameObject.SetActive(!isValid);
        }
    }

    private IEnumerator CalculateFramesPerSecond()
    {
        int lastFrameCount = 0;

        while (true)
        {
            yield return new WaitForSecondsRealtime(1.0f);

            var currentFrameCount = svrManager.FrameCount;
            var elapsedFrames = currentFrameCount - lastFrameCount;
            _framesPerSecond = elapsedFrames / 1.0f;
            lastFrameCount = currentFrameCount;
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

}
