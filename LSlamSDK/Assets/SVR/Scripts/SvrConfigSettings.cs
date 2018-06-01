using UnityEngine;
using System.Collections;

public class SvrConfigSettings : MonoBehaviour
{
    public Transform reticle;

    void Awake()
    {
    }

    IEnumerator Start()
    {
        if (SvrManager.Instance == null || SvrConfigOptions.Instance == null)
        {
            yield break;
        }

        //if (SvrConfigOptions.Instance.HorizonElevation != 0f)
        //{
        //    SvrManager.Instance.settings.horizonElevation = SvrConfigOptions.Instance.HorizonElevation;
        //}

        //if (SvrConfigOptions.Instance.StereoConvergence != 0f)
        //{
        //    SvrManager.Instance.settings.stereoConvergence = SvrConfigOptions.Instance.StereoConvergence;
        //}

        yield return new WaitUntil(() => SvrManager.Instance.Initialized);

        if (SvrConfigOptions.Instance.UseFixedViewport)
        {
            DisableSvrInput();
            SetSvrCameraView(SvrConfigOptions.Instance.FixedViewportPosition, SvrConfigOptions.Instance.FixedViewportEulerAnglesRotation);
        }

        if (SvrConfigOptions.Instance.OverrideRenderTextureMSAA != 0)
        {
            SetSvrRenderTextureAntialiasing(SvrConfigOptions.Instance.OverrideRenderTextureMSAA);
        }

        if (SvrConfigOptions.Instance.FreezeAnimations)
        {
            FreezeAllAnimationsAtTime(Mathf.Max(0, SvrConfigOptions.Instance.FreezeAnimationsAtTimeInSecs));
        }

        if (SvrConfigOptions.Instance.DisableAudio)
        {
            DisableAudio();
        }

        if (SvrConfigOptions.Instance.FoveationEnabled)
        {
            SetFoveatedRendering(SvrConfigOptions.Instance.FoveationGain, SvrConfigOptions.Instance.FoveationArea);

            if (reticle != null && SvrConfigOptions.Instance.FocusEnabled) reticle.gameObject.SetActive(true);
        }

    }

    void Update()
    {
        if (!SvrManager.Instance)
        {
            return;
        }

        if (SvrConfigOptions.Instance.FocusEnabled)
        {
            UpdateFocus();
        }
    }

    private void FreezeAllAnimationsAtTime(float timeInSec)
    {
        Animator[] animators = GameObject.FindObjectsOfType<Animator>();
        foreach (Animator animator in animators)
        {
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.Update(timeInSec);
        }

        Time.timeScale = 0;
    }

    private void DisableSvrInput()
    {
        if (!SvrManager.Instance)
        {
            return;
        }

        SvrManager.Instance.DisableInput = true;
    }

    private void SetSvrCameraView(Vector3 position, Vector3 eulerAnglesRotation)
    {
        if(!SvrManager.Instance)
        {
            return;
        }

        SvrManager.Instance.transform.position = position;
        SvrManager.Instance.transform.eulerAngles = eulerAnglesRotation;
    }

    private void SetSvrRenderTextureAntialiasing(int mode)
    {
        if (!SvrManager.Instance)
        {
            return;
        }

        switch (mode)
        {
            case 1:
                SvrOverrideSettings.EyeAntiAliasing = SvrOverrideSettings.eAntiAliasing.k1;
                break;
            case 2:
                SvrOverrideSettings.EyeAntiAliasing = SvrOverrideSettings.eAntiAliasing.k2;
                break;
            case 4:
                SvrOverrideSettings.EyeAntiAliasing = SvrOverrideSettings.eAntiAliasing.k4;
                break;

            default:
                Debug.LogError("Antialiasing: " + mode + " not supported!");
                break;
        }
    }

    private void DisableAudio()
    {

        AudioSource [] audioSources = GameObject.FindObjectsOfType<AudioSource>();
        foreach(AudioSource audio in audioSources)
        {
            audio.enabled = false;
        }
    }

    private void SetFoveatedRendering(Vector2 gain, float area)
    {
        SvrManager.Instance.settings.foveationGain = gain;
        SvrManager.Instance.settings.foveationArea = area;
    }

    private float focusTime = 0;
    private Vector2 focusPosition = Vector2.zero;
    private float focusLength = 3;
    private void UpdateFocus()
    {
        var amplitude = SvrConfigOptions.Instance.FocusAmplitude;
        var frequency = SvrConfigOptions.Instance.FocusFrequency;

        focusPosition.x = Mathf.Cos(focusTime * frequency.x) * amplitude.x;
        focusPosition.y = Mathf.Cos(focusTime * frequency.y) * amplitude.y;

        focusTime += Time.deltaTime;

        SvrManager.Instance.FocalPoint = focusPosition;

        if (reticle != null)
        {
            var position = reticle.localPosition;
            position.x = focusPosition.x;
            position.y = focusPosition.y;
            position.z = 1;
            position *= focusLength;
            reticle.localPosition = position;
        }
    }

}
