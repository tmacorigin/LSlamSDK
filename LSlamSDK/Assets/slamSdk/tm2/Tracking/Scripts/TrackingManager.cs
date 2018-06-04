using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Intel.RealSense.Tracking;
using UnityEngine.Events;
using System.Text.RegularExpressions;
using System.IO;

namespace Intel.RealSense.Tracking
{
	public class TrackingManager : MonoBehaviour
	{
		public static TrackingManager Instance {
			get;
			private set;
		}

		[Serializable]
		public class Settings
		{
			public bool enableTracking = true;
			public int numControllers = 2;
			public bool connectControllers = false;
			public List<string> macFilter = new List<string> ();
			public bool DisableAutoVRCameraTracking = false;
		}

		public Settings settings = new Settings ();

//		public StreamProfile[] rawStreams;

		[Space]
		[Header ("Events")]
		public UnityEvent onDeviceAttached;
		public UnityEvent onDeviceDetached;

		public ITrackingManager manager { get; private set; }

		public ITrackingDevice device { get; private set; }

		#region Settings

		[ContextMenu ("Save Settings")]
		public void SaveSettings ()
		{			
			File.WriteAllText ("settings.json", JsonUtility.ToJson (settings, true));
		}

		[ContextMenu ("Load Settings")]
		public void LoadSettings ()
		{
			try {
				JsonUtility.FromJsonOverwrite (File.ReadAllText ("settings.json"), settings);
			} catch (Exception e) {
				Debug.LogWarning (e.Message);
			}
		}

		void ApplySettings ()
		{
			#if UNITY_2017_1
			if (UnityEngine.VR.VRDevice.isPresent)
				UnityEngine.VR.VRDevice.DisableAutoVRCameraTracking (Camera.main, settings.DisableAutoVRCameraTracking);
			#elif UNITY_2017_2_OR_NEWER
			if (UnityEngine.XR.XRDevice.isPresent)
				UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking (Camera.main, settings.DisableAutoVRCameraTracking);
			#endif

			foreach (var f in new []{ 
				"TM2Ctrl.txt", 
				Path.Combine (Application.persistentDataPath, "TM2Ctrl.txt"),
				Path.Combine ("/storage/emulated/0/Android/data/com.intel.TM2ControllersPairing/files", "TM2Ctrl.txt")
			}) {		
				if (!File.Exists (f)) {
					Debug.LogWarningFormat ("{0} does not exist", f);
					continue;
				}
				Debug.Log (f);
				var lines = File.ReadAllLines (f);
				lines = Array.ConvertAll (lines, l => {
					l = l.Trim ();
					if (Regex.IsMatch (l, @"[a-zA-Z]")) {
						string[] hex2dec = Array.ConvertAll (l.Split (':'), h => Convert.ToByte (h, 16).ToString ());
						return string.Join (":", hex2dec);
					}
					return l;
				});
				settings.macFilter.AddRange (lines);
			}
		}

		#endregion Settings

		public bool IsDeviceConnected ()
		{
			return manager.IsDeviceConnected;
		}

		static ITrackingManager CreateManager ()
		{
			#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			Type T = typeof(WindowsTrackingManager);
			#elif UNITY_ANDROID
			Type T = typeof(AndroidTrackingManager);
			#else
			Type T;
			throw new InvalidOperationException("Unsupported platform");
			#endif
			Debug.Log (T);
			return Activator.CreateInstance (T) as ITrackingManager;
		}

		void Awake ()
		{
			Instance = this;
		
			manager = manager ?? CreateManager ();

			LoadSettings ();
			ApplySettings ();

			Debug.Log ("Tracking Lib Version: " + manager.Version);

			manager.onTrackingDeviceAvailable += (ITrackingDevice dev) => {

				Debug.Log ("onTrackingDeviceAvailable: " + dev);
				device = dev;
			};

			manager.onTrackingDeviceUnavailable += () => {

				Debug.Log ("onTrackingDeviceUnavailable: " + device);
				device = null;

				RunOnMainThread (onDeviceDetached.Invoke);
			};
		}

		IEnumerator Start ()
		{
			yield return new WaitUntil (() => device != null);

			Debug.Log ("Firmware Version: " + device.FirmwareVersion);

			onDeviceAttached.Invoke ();

//            rawStreams = device.GetSupportedRawStreams ();

			device.EnableControllers (settings.numControllers);

			if (settings.enableTracking)
				device.EnableTracking ();

			try {
				device.Start ();
			} catch (Exception e) {
				//TODO: device already started?
				Debug.LogException (e);
				device.Reset ();
			}

			device.onControllerDiscovery += (IControllerDevice controller) => {
				
				var mac = controller.MacAddress;
				Debug.Log ("onControllerDiscovery: " + string.Join (":", Array.ConvertAll (mac, m => m.ToString ())) + " [0x]" + string.Join (":", Array.ConvertAll (mac, m => m.ToString ("X"))));
				
				if (settings.connectControllers) {
					try {
						var macStr = string.Join (":", Array.ConvertAll (mac, m => m.ToString ()));
						if (settings.macFilter.Count == 0 || settings.macFilter.Contains (macStr)) {
							RunOnMainThread (() => controller.Connect ());
						}

					} catch (Exception e) {
						Debug.LogException (e);
					}
				}
			};
		}

		public static readonly Queue<Action> q = new Queue<Action> ();

		public static void RunOnMainThread (Action a)
		{
			lock (q) {
				q.Enqueue (a);
			}
		}

		void Update ()
		{

			lock (q) {
				while (q.Count > 0) {
					q.Dequeue ().Invoke ();
				}
			}

			#if DEBUG
			if (Input.GetKeyDown (KeyCode.L)) {
				UnityEngine.SceneManagement.SceneManager.LoadScene (0);
			}
			if (Input.GetKeyDown (KeyCode.R)) {
				if (device != null)
					device.Reset ();
			}
			#endif
		}

		void OnDestroy ()
		{
			OnApplicationQuit ();
		}

		void OnApplicationQuit ()
		{
			if (device != null) {
				device.Close ();
				device = null;
			}
			if (manager != null) {
				manager.Dispose ();
				manager = null;
			}
//			GC.Collect ();
//			GC.WaitForPendingFinalizers ();
		}

		void OnApplicationPause (bool pauseStatus)
		{
			Debug.LogFormat ("pauseStatus: {0}, device: {1}", pauseStatus, device);

			try {
				if (pauseStatus) {
					if (device != null) {
//						device.Stop ();
//						device.Close ();
//						device.Reset ();
//						device = null;
					}
				} else {
					if (device != null) {		
//						device.Reset ();
//						device.Start();
					}
				}
			} catch (Exception e) {
				Debug.Log (e);
			}
		}
	}
}
