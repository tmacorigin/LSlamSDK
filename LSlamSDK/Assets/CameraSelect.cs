using System.Collections;
using System.Collections.Generic;
using Intel.RealSense.Tracking;
using UnityEngine;


public class CameraSelect : MonoBehaviour {

    //四元素方式表示 p r
    private Vector3     position;
    private Vector3     rotation;
    public Vector3    Position { get; set; }
    public Quaternion Rotation { get; set; }

    public enum SLAM_TYPE
    {
        SLAM_TYPE_NONE = 0,
        SLAM_TYPE_TM2 = 1,
        SLAM_TYPE_XVISIO= 2,
    };
    //当前使用的slam  默认 TM2
    public SLAM_TYPE slamType = SLAM_TYPE.SLAM_TYPE_TM2;

    /* tm2 slam start */
    TrackingManager tm;
    /* tm2 slam end*/
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
