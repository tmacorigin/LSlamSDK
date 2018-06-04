using UnityEngine;
using Intel.RealSense.Tracking;
using System.Collections;
using System;

namespace Intel.RealSense.Tracking
{
	public class TrackingTransformer : MonoBehaviour
	{
		public DeviceIndex source;

		public bool UsePosition = true;
		public bool UseRotation = true;
        public float predictionTime = 0;
		IPoseListener m_poseListener;
		Pose pose;

		Transform m_transform;
		TrackingManager tm;

		IEnumerator Start ()
		{
			tm = FindObjectOfType<TrackingManager> ();
			if (tm == null) {
				Debug.LogError ("Couldn't find TrackingManager component in scene.");
				yield break;
			}

			m_transform = transform;

			var waitConnect = new WaitUntil (tm.IsDeviceConnected);
			var waitConnectCtrl = new WaitUntil (() => m_poseListener != null || !tm.IsDeviceConnected ());
			var waitDisconnect = new WaitUntil (() => m_poseListener == null || !tm.IsDeviceConnected ());

			// handle disconnect
			while (true) {
				yield return waitConnect;
				Debug.LogFormat (this, "Device {0} ready, Waiting for {1} poses", tm.device, source);

				if (source == DeviceIndex.HMD) {
					m_poseListener = tm.device;

					#if UNITY_2017_1_OR_NEWER
					Application.onBeforeRender += Update;
					#else
					Camera.onPreCull += onPreCull;
					#endif

				} else {
//					yield return new WaitUntil (tm.device.IsControllerConnected((int)source));

					tm.device.onControllerDiscovery += (IControllerDevice ctrl) => {
						Debug.Log ("onControllerDiscovery: " + ctrl);

						ctrl.onConnect += (int index) => {
							Debug.Log ("onConnect: " + ctrl);
							Debug.LogFormat ("id={0}, source={1}", index, source);

							if (index != (int)source)
								return;

							m_poseListener = ctrl;

							ctrl.onDisconnect += (int index2) => {
								if (index2 != (int)source)
									return;
								Debug.LogFormat ("onDisconnect: index={0}", index2);
								m_poseListener = null;
							};
						};
					};

					yield return waitConnectCtrl;

					if (m_poseListener == null) {
						Debug.LogFormat ("device disconnected while waiting for controller {0}", source);
						continue;
					}

					Debug.LogFormat ("WaitForPose: {0}, source={1}", m_poseListener, source);
				}
			
				yield return waitDisconnect;

				Debug.LogFormat ("{0} ({1}) disconnected", m_poseListener, source);

				#if UNITY_2017_1_OR_NEWER
				Application.onBeforeRender -= Update;
				#else
				Camera.onPreCull -= onPreCull;
				#endif

				m_poseListener = null;
			}

		}

		void OnDestroy ()
		{
			m_poseListener = null;
		}

		void Update ()
		{
			if (m_poseListener == null)
				return;			
			UpdatePose ();
		}

		void onPreCull (Camera cam)
		{
			Update ();
		}

		static readonly Quaternion q = Quaternion.Euler (-90, 180, 0);

		void UpdatePose ()
		{
			if (!m_poseListener.TryGetPose (ref pose))
				return;
				
			if (predictionTime > 0)
        PredictPose(ref pose, predictionTime);
        
			var pos = pose.translation;
			var rot = pose.rotation;

			rot.x = -rot.x;
			rot.w = -rot.w;
			rot = q * rot;

			pos.Set (pos.x, pos.z, pos.y);

			if (UsePosition)
				m_transform.localPosition = pos;
			if (UseRotation)
				m_transform.localRotation = rot;	
		}

        private void PredictPose(ref Pose pose, float dt)
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

            pose.rotation = (quatW*pose.rotation);
        }

        Quaternion quaternionExp(Vector3 v)
        {
            Vector3 w = new Vector3( v.x / 2.0f, v.y / 2.0f, v.z / 2.0f );
            float th2 = w.x * w.x + w.y * w.y + w.z * w.z;
            float th = Mathf.Sqrt(th2);
            float c = Mathf.Cos(th);
            float s = th2 < Mathf.Sqrt(120 * Mathf.Epsilon) ? 1.0f - 1.0f / 6.0f * th2 : Mathf.Sin(th) / th;
            Quaternion Q = new Quaternion( s * w.x, s * w.y, s * w.z, c );
            return Q;
        }

    }
}
