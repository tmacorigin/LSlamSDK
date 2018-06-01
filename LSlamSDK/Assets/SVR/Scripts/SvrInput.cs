using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using SnapdragonVR;

public class SvrInput : MonoBehaviour
{
    private SvrManager svrManager = null;
    private SvrController svrController = null;
    private Coroutine handleBack = null;

    public static SvrInput Instance { get; private set; }
    public static SvrController Controller { get { return Instance.svrController; } }

    public delegate void OnRecenterCallback();
    public OnRecenterCallback OnRecenterListener;

    public delegate IEnumerator OnBackCallback();
    public OnBackCallback OnBackListener;

    public enum eInputStyle { Press, Tap, Hold }

    [Tooltip ("Android Back, Unity Escape, Controller Back Button")]
    public eInputStyle backInputType = eInputStyle.Press;
    private float backHoldTimer = 0;

    [Tooltip ("Android Touch, Unity Mouse, Controller Thumb Button")]
    public eInputStyle recenterInputType = eInputStyle.Hold;
    private float recenterHoldTimer = 0;

    [Tooltip("Button press duration in seconds to produce a Hold event")]
    public float ButtonHoldDuration = 1; // Seconds

    void Awake()
    {
        Instance = this;
        svrManager = SvrManager.Instance;
        svrController = GetComponentInChildren<SvrController>();

        Input.backButtonLeavesApp = false;

        OnRecenterListener = HandleRecenter;
        OnBackListener = HandleQuit;
    }

    IEnumerator HandleQuit()
    {
        svrManager.SetOverlayFade(SvrManager.eFadeState.FadeOut);
        yield return new WaitUntil(() => svrManager.IsOverlayFading() == false);

        Application.Quit();

        handleBack = null;
    }

    void HandleRecenter()
    {
        SvrPlugin.Instance.RecenterTracking();
        Controller.Recenter();
    }

    void Update()
    {
        if (!SvrPlugin.Instance.IsInitialized())
            return;

        if (!SvrPlugin.Instance.IsRunning())
            return;

        if (!Input.backButtonLeavesApp &&
            CheckButton(Input.GetKey(KeyCode.Escape) || svrController.GetButton(SvrController.svrControllerButton.Back), backInputType, ref backHoldTimer))
        {
            Debug.Log("SvrInput - Quit");

            if (OnBackListener != null && handleBack == null) handleBack = StartCoroutine(OnBackListener());
        }
        if (CheckButton(Input.GetMouseButton(0) || Input.GetKey((KeyCode)10) || svrController.GetButton(SvrController.svrControllerButton.PrimaryThumbstick), recenterInputType, ref recenterHoldTimer))
        {
            Debug.Log("SvrInput - Recenter");

            if (OnRecenterListener != null) OnRecenterListener();
        }
    }

    public bool CheckButton(bool buttonValue, eInputStyle buttonInputType, ref float buttonHoldTimer)
    {
        if (buttonValue)
        {
            if (buttonInputType == eInputStyle.Press && buttonHoldTimer <= 0)
            {
                Debug.Log("SvrInput - Press");
                return true;
            }
            if (buttonHoldTimer < ButtonHoldDuration)
            {
                buttonHoldTimer += Time.deltaTime;    // Increment timer
                if (buttonInputType == eInputStyle.Hold && buttonHoldTimer > ButtonHoldDuration)
                {
                    Debug.Log("SvrInput - Hold");
                    return true;
                }
            }
        }
        else if (buttonHoldTimer > 0)
        {
            if (buttonInputType == eInputStyle.Tap && buttonHoldTimer < ButtonHoldDuration)
            {
                Debug.Log("SvrInput - Tap");
                return true;
            }
            buttonHoldTimer = 0; // Terminate timer
        }

        return false;
    }

    //void OnGUI()
    //{
    //    Event e = Event.current;
    //    if (e.isKey)
    //        Debug.Log("Detected key code: " + e.keyCode);
    //}

}
