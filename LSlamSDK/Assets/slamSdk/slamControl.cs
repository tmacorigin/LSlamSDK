using Intel.RealSense.Tracking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class slamControl : MonoBehaviour {

    public enum SLAM_Type
    {
        SLAM_TYPE_NULL,
        SLAM_TYPE_TM2,
        SLAM_TYPE_XVISIO
    };

    //slam 类型
    public SLAM_Type slamType = SLAM_Type.SLAM_TYPE_TM2;

    //监听slam位置的 listener
    public Transform listener ;

    TrackingTransformer objname;


    TrackingTransformer tm2 = null;
    // Use this for initialization
    void Start () {
        tm2 = GameObject.Find("TM2CameraRig/HMD").GetComponent<TrackingTransformer>() ;
    }
	
	// Update is called once per frame
	void Update () {
        if ( ( tm2 != null ) && ( listener!= null ) )
        { 
            listener.position = tm2.getPosition();
            listener.rotation = tm2.getRotation();
        }
	}
}
