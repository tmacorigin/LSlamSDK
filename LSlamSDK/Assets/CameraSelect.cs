using System.Collections;
using System.Collections.Generic;
using Intel.RealSense.Tracking;
using UnityEngine;


public class CameraSelect : MonoBehaviour {

    
    //四元素方式表示 p r
    public Transform m_transform;


    TrackingManager tm2;
    public enum SLAM_TYPE
    {
        SLAM_TYPE_NONE = 0,
        SLAM_TYPE_TM2 = 1,
        SLAM_TYPE_XVISIO= 2,
    };
    //当前使用的slam  默认 TM2
    public SLAM_TYPE slamType = SLAM_TYPE.SLAM_TYPE_TM2;

    IPoseListener m_poseListener;

    public DeviceIndex source;

    public bool UsePosition = true;
    public bool UseRotation = true;
    public Confidence MinConfidence = Confidence.LOW;
    public float predictionTime = 0;
    Intel.RealSense.Tracking.Pose m_pose;
    public Transform Mycamera;


    /* tm2 slam start */
    /* tm2 slam end*/
    // Use this for initialization

    void Start()
    {
        if ( SLAM_TYPE.SLAM_TYPE_TM2 == slamType )
        {
            Tm2_Start();
        }
    }

    IEnumerator Tm2_Start () {

        tm2 = (TrackingManager)Instantiate(Resources.Load("TM"));


        m_transform = transform;

        var waitConnect = new WaitUntil(tm2.IsDeviceConnected);
        var waitConnectCtrl = new WaitUntil(() => m_poseListener != null || !tm2.IsDeviceConnected());
        var waitDisconnect = new WaitUntil(() => m_poseListener == null || !tm2.IsDeviceConnected());

        // handle disconnect
        while (true)
        {
            yield return waitConnect;
            Debug.LogFormat(this, "Device {0} ready, Waiting for {1} poses", tm2.device, source);

            if (source == DeviceIndex.HMD)
            {
                m_poseListener = tm2.device;

#if UNITY_2017_1_OR_NEWER
                Application.onBeforeRender += Update;
#else
					Camera.onPreCull += onPreCull;
#endif

            }
            else
            {
                //					yield return new WaitUntil (tm.device.IsControllerConnected((int)source));

                tm2.device.onControllerDiscovery += (IControllerDevice ctrl) => {
                    Debug.Log("onControllerDiscovery: " + ctrl);

                    var info = ctrl.Info;
                    Debug.LogFormat("VendorData: {0}", info.VendorData);

                    ctrl.onConnect += (int index) => {
                        Debug.Log("onConnect: " + ctrl);
                        Debug.LogFormat("id={0}, source={1}", index, source);

                        if (index != (int)source)
                            return;

                        m_poseListener = ctrl;

                        ctrl.onDisconnect += (int index2) => {
                            if (index2 != (int)source)
                                return;
                            Debug.LogFormat("onDisconnect: index={0}", index2);
                            m_poseListener = null;
                        };
                    };
                };

                yield return waitConnectCtrl;

                if (m_poseListener == null)
                {
                    Debug.LogFormat("device disconnected while waiting for controller {0}", source);
                    continue;
                }

                Debug.LogFormat("WaitForPose: {0}, source={1}", m_poseListener, source);
            }

            yield return waitDisconnect;

            Debug.LogFormat("{0} ({1}) disconnected", m_poseListener, source);

#if UNITY_2017_1_OR_NEWER
            Application.onBeforeRender -= Update;
#else
				Camera.onPreCull -= onPreCull;
#endif

            m_poseListener = null;
        }

    }
	
	// Update is called once per frame
	void Update () {
		
	}
}
