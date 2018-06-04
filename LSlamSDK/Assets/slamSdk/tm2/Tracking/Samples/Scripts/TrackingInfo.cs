using System.Collections;
using UnityEngine;
using Intel.RealSense.Tracking;
using System;
using UnityEngine.UI;

public class TrackingInfo : MonoBehaviour
{
	readonly FPSCounter unityFPS = new FPSCounter ();
	readonly FPSCounter hmdFPS = new FPSCounter ();
	readonly FPSCounter ctrl1FPS = new FPSCounter ();
	readonly FPSCounter ctrl2FPS = new FPSCounter ();
	readonly Intel.RealSense.Tracking.Pose[] cachedPose = {
		default(Intel.RealSense.Tracking.Pose), 
		default(Intel.RealSense.Tracking.Pose), 
		default(Intel.RealSense.Tracking.Pose)
	};
	bool[] connections = { false, false, false };

	ITrackingManager manager;
	ITrackingDevice device;

	public Gradient fpsGradient;
	public Text text;

	bool showGUI = true;

	IEnumerator Start ()
	{
		StartCoroutine (unityFPS.UpdateFPS ());
		StartCoroutine (hmdFPS.UpdateFPS ());
		StartCoroutine (ctrl1FPS.UpdateFPS ());

		var tm = FindObjectOfType<TrackingManager> ();
		manager = tm.manager;

		var counters = new []{ hmdFPS, ctrl1FPS, ctrl2FPS };

		Action<Intel.RealSense.Tracking.Pose> updatePose = pose => {
			cachedPose [(int)pose.sourceIndex] = pose;
			counters [(byte)pose.sourceIndex].Increment ();
		};

		Action<IControllerDevice> onControllerDiscovery = (IControllerDevice controller) => {

			controller.onConnect += (int index) => {

				connections [index] = true;

				controller.onPose += pose => {
					cachedPose [index] = pose;
					counters [index].Increment ();
				};

			};
		};

		while (true) {
			var co = StartCoroutine (UpdateText ());

			yield return new WaitUntil (tm.IsDeviceConnected);
			device = tm.device;

			connections [0] = true;
			var co2 = StartCoroutine (UpdateTemperature ());

			device.onPose += updatePose;		
			device.onControllerDiscovery += onControllerDiscovery;

			yield return new WaitWhile (tm.IsDeviceConnected);

			StopCoroutine (co);
			StopCoroutine (co2);

			Array.Clear (connections, 0, connections.Length);

			if (device != null) {
				device.onPose -= updatePose;		
				device.onControllerDiscovery -= onControllerDiscovery;
				device = null;
			}
		}

	}

	void OnDestroy ()
	{
		manager = null;
		device = null;
		GC.Collect ();
		GC.WaitForPendingFinalizers ();
	}

	void Update ()
	{
		if (Input.GetKeyDown (KeyCode.D))
			showGUI ^= true;
		unityFPS.Increment ();
	}


	string fw = null;
	string temperature = "";

	IEnumerator UpdateTemperature ()
	{	
		var wait = new WaitForSeconds (1f);
		var d = new WaitWhile (() => device == null);

		while (true) {
			yield return d;
			temperature = String.Join (", ", Array.ConvertAll (device.Temperature,
				t => string.Format ("{0}: {1:F2}°C", t.index, t.temperature)));

			yield return wait;
		}
	}

	readonly DateTime startTime = DateTime.Now;

	String GetText ()
	{
		var sb = new System.Text.StringBuilder ();

		fw = fw ?? (device != null ? device.FirmwareVersion : null);

		var timeSpan = DateTime.Now - startTime;

		sb.Append ("Version: ");
		sb.AppendLine (manager.Version);
		sb.Append ("FW: ");
		sb.AppendLine (fw ?? "N\\A");

		var c = fpsGradient.Evaluate (unityFPS.FPS / 60.0f);
		sb.Append ("Update: <color=#");
		sb.Append (ColorUtility.ToHtmlStringRGBA (c));
		sb.Append ('>');
		sb.Append (unityFPS.FPS);
		sb.AppendLine ("FPS</color>");
		sb.AppendLine (timeSpan.ToString ());
		sb.AppendLine ("----------------------------------------------------------------");

		if (manager.IsDeviceConnected) {
			
			sb.AppendLine (temperature);
				
			sb.AppendLine ("HMD: ");
			var pose = cachedPose [(int)DeviceIndex.HMD];
			const string fmt = "+0.0000;-0.0000;0";
			sb.Append (pose.translation.ToString (fmt));
			sb.AppendLine ("[m]");
			//			sb.AppendLine (pose.velocity.ToString (fmt) + "[m/s]");
			//			sb.AppendLine (pose.acceleration.ToString (fmt) + "[m/s^2]");
			sb.AppendLine (pose.rotation.ToString (fmt));
			sb.Append (pose.rotation.eulerAngles.ToString ("F2"));
			sb.AppendLine ("[euler]");
			//			sb.AppendLine (pose.angularVelocity.ToString (fmt) + "[rad/s]");
			//			sb.AppendLine (pose.angularAcceleration.ToString (fmt) + "[rad/s^2]");
			sb.AppendLine (TimeSpan.FromMilliseconds (pose.timestamp * 1e-6f).ToString ());

			c = fpsGradient.Evaluate (hmdFPS.FPS / 260.0f);
			sb.Append ("<color=#");
			sb.Append (ColorUtility.ToHtmlStringRGBA (c));
			sb.Append ('>');
			sb.Append (hmdFPS.FPS);
			sb.AppendLine ("FPS</color>");

			c = fpsGradient.Evaluate ((float)pose.trackerConfidence / 3.0f);
			sb.Append ("Tracker Confidence: <color=#");
			sb.Append (ColorUtility.ToHtmlStringRGBA (c));
			sb.Append ('>');
			sb.Append (pose.trackerConfidence);
			sb.AppendLine ("</color>");

			sb.AppendLine ("----------------------------------------------------------------");
			sb.AppendLine ("CONTROLLER1: ");
			if (connections [1]) {
				pose = cachedPose [(int)DeviceIndex.CONTROLLER1];
				sb.Append (pose.translation.ToString (fmt));
				sb.AppendLine ("[m]");
				sb.AppendLine (pose.rotation.ToString (fmt));
				sb.Append (pose.rotation.eulerAngles.ToString ("F2"));
				sb.AppendLine ("[euler]");

				c = fpsGradient.Evaluate (ctrl1FPS.FPS / 80.0f);
				sb.Append ("<color=#");
				sb.Append (ColorUtility.ToHtmlStringRGBA (c));
				sb.Append ('>');
				sb.Append (ctrl1FPS.FPS);
				sb.AppendLine ("FPS</color>");

				c = fpsGradient.Evaluate ((float)pose.trackerConfidence / 3.0f);
				sb.Append ("Tracker Confidence: <color=#");
				sb.Append (ColorUtility.ToHtmlStringRGBA (c));
				sb.Append ('>');
				sb.Append (pose.trackerConfidence);
				sb.AppendLine ("</color>");
			} else {
				sb.AppendLine ("Disconnected");
			}

		} else {
			sb.AppendLine ("\n\n<size=48>       Tracking device\n         disconnected</size>");
		}
			
		return sb.ToString ();
	}

	// UI.Text updates are expensive.. limit framerate
	IEnumerator UpdateText ()
	{
		var canvas = GetComponentInChildren<Canvas> ();
		var wait = new WaitForSeconds (1f / 25f);

		while (true) {
			if (canvas.enabled) {
				text.text = GetText ();
			}

			yield return wait;
		}
	}

	class FPSCounter
	{
		public float FPS { get; private set; }

		int counter;
		const float interval = 0.5f;

		public void Increment ()
		{
			System.Threading.Interlocked.Increment (ref counter);
		}

		public IEnumerator UpdateFPS ()
		{
			var wait = new WaitForSeconds (interval);
			while (true) {
				FPS = counter / interval;
				counter = 0;
				yield return wait;
			}
		}
	}

	#if DEBUG
	DateTime t0 = DateTime.Now;
	string txt = "";

	void OnGUI ()
	{
		if (showGUI && Event.current.type == EventType.Repaint) {
			if ((DateTime.Now - t0).TotalMilliseconds > 33) {
				t0 = DateTime.Now;
				txt = GetText ();
			}

			GUI.Label (new Rect (0, 0, Screen.width, Screen.height), txt, new GUIStyle { richText = true });
		}
	}
	#endif
}
