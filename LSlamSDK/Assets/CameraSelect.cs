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

    public Confidence MinConfidence = Confidence.LOW;
    //slam数据优化
    public float predictionTime = 0;
    Intel.RealSense.Tracking.Pose m_pose;
    //相关关联的camera 位置
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
           
             {
                m_poseListener = tm2.device;

#if UNITY_2017_1_OR_NEWER
                Application.onBeforeRender += Update;
#else
					Camera.onPreCull += onPreCull;
#endif

            }
            

            yield return waitDisconnect;

            Debug.LogFormat("{0}  disconnected", m_poseListener);

#if UNITY_2017_1_OR_NEWER
            Application.onBeforeRender -= Update;
#else
				Camera.onPreCull -= onPreCull;
#endif

            m_poseListener = null;
        }

    }

    void OnDestroy()
    {
        m_poseListener = null;
    }

    // Update is called once per frame
    void Update()
    {
        if (m_poseListener == null)
            return;
        UpdatePose();
    }

    void onPreCull(Camera cam)
    {
        Update();
    }

    static readonly Quaternion q = Quaternion.Euler(-90, 180, 0);

    void UpdatePose()
    {
        if (!m_poseListener.TryGetPose(ref m_pose))
            return;

        if (m_pose.trackerConfidence < MinConfidence)
            return;

        if (predictionTime > 0)
            PredictPose(ref m_pose, predictionTime);

        var pos = m_pose.translation;
        var rot = m_pose.rotation;

        rot.x = -rot.x;
        rot.w = -rot.w;
        rot = q * rot;

        pos.Set(pos.x, pos.z, pos.y);

        m_transform.localPosition = pos;
        m_transform.localRotation = rot;

        Mycamera.position = pos;
        Mycamera.rotation = rot;




    }

    private void PredictPose(ref Intel.RealSense.Tracking.Pose pose, float dt)
    {

        // yinon: limit prediction to 100ms ahead
        if (dt > 0.1f)
            dt = 0.1f;
        // "aliasing"
        Vector3 T = pose.translation;
        Vector3 vw = pose.angularVelocity;
        Vector3 vt = pose.velocity;
        Vector3 aw = pose.angularAcceleration;
        Vector3 at = pose.acceleration;

        pose.translation.x = dt * (dt / 2.0f * at.x + vt.x) + T.x;
        pose.translation.y = dt * (dt / 2.0f * at.y + vt.y) + T.y;
        pose.translation.z = dt * (dt / 2.0f * at.z + vt.z) + T.z;

        Vector3 W = new Vector3(dt * (dt / 2.0f * aw.x + vw.x),
                                 dt * (dt / 2.0f * aw.y + vw.y),
                                 dt * (dt / 2.0f * aw.z + vw.z));

        Quaternion quatW = quaternionExp(W);

        pose.rotation = (quatW * pose.rotation);
    }

    Quaternion quaternionExp(Vector3 v)
    {
        Vector3 w = new Vector3(v.x / 2.0f, v.y / 2.0f, v.z / 2.0f);
        float th2 = w.x * w.x + w.y * w.y + w.z * w.z;
        float th = Mathf.Sqrt(th2);
        float c = Mathf.Cos(th);
        float s = th2 < Mathf.Sqrt(120 * Mathf.Epsilon) ? 1.0f - 1.0f / 6.0f * th2 : Mathf.Sin(th) / th;
        Quaternion Q = new Quaternion(s * w.x, s * w.y, s * w.z, c);
        return Q;
    }
}
