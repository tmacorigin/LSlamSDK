using UnityEngine;
using System;
using Intel.RealSense.Tracking;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

namespace Intel.RealSense.Tracking
{
	public class AndroidTrackingManager : ITrackingManager, IDisposable
	{
		#region ITrackingManager implementation

		public event Action<ITrackingDevice> onTrackingDeviceAvailable = null;

		public event Action onTrackingDeviceUnavailable = null;

		public bool IsDeviceConnected { get; private set; }

		public string Version { get; private set; }

		#endregion

		readonly Enumerator m_enumerator;
		readonly Enumerator.Listener m_managerListener;

		internal class Enumerator
		{
			readonly AndroidJavaObject instance;

			/// <summary>
			/// In case a device is already available, onTrackingDeviceAvailable callback will be called.
			/// close must be called at the of the usage.
			/// </summary>
			/// <param name="context">Application Context.</param>
			/// <param name="listener">Listener.</param>
			public Enumerator (AndroidJavaObject context, Listener listener)
			{
				instance = new AndroidJavaObject ("com.intel.realsense.trackingdevicedetector.Enumerator", context, listener);
				Debug.Log ("Enumerator: " + instance);
			}

			public void Close ()
			{
				instance.Call ("close");
				instance.Dispose ();
			}

			public sealed class Listener : AndroidJavaProxy
			{
				readonly AndroidTrackingManager tm;
				TrackingDevice m_dev;

				public Listener (AndroidTrackingManager tm)
					: base ("com.intel.realsense.trackingdevicedetector.Enumerator$Listener")
				{
					this.tm = tm;
				}

				public void onTrackingDeviceAvailable ()
				{
					Debug.Log ("onTrackingDeviceAvailable");

					try {
						m_dev = getTrackingDevice ();

						var h = tm.onTrackingDeviceAvailable;
						if (h != null)
							h.Invoke (m_dev);

						tm.IsDeviceConnected = true;

						m_dev.onClosed += () => {
							tm.IsDeviceConnected = false;
							m_dev = null;
//							var h2 = tm.onTrackingDeviceUnavailable;
//							if (h2 != null)
//								h2.Invoke ();
						};

					} catch (AndroidJavaException e) {
						Debug.LogException (e);
						tm.IsDeviceConnected = false;
//							tm.onTrackingDeviceUnavailable.Invoke (null);
					}

					GC.Collect ();
					GC.WaitForPendingFinalizers ();
				}

				public void onTrackingDeviceUnavailable ()
				{
					tm.IsDeviceConnected = false;
					Debug.LogFormat ("onTrackingDeviceUnavailable");

					if (m_dev != null) {

						var h = tm.onTrackingDeviceUnavailable;
						if (h != null)
							h.Invoke ();
						
						m_dev.Close ();
						m_dev = null;
					}
				}
			}
		}

		/// <summary>
		/// Pose listener with manual marshalling
		/// Notice that for AOT (iOS, IL2CPP) you can't have native callbacks to instance methods, just static.
		/// So we marshal the instance method and pass it as an argument to the static callback.
		/// </summary>
		internal class PoseListener : IDisposable
		{
			[StructLayout (LayoutKind.Explicit)]
			public class PoseData
			{
				[FieldOffset (6)]
				public byte index;

				[FieldOffset (8)]
				public Vector3 translation;

				[FieldOffset (20)]
				public Quaternion rotation;

				[FieldOffset (36)]
				public Vector3 velocity;

				[FieldOffset (48)]
				public Vector3 angularVelocity;

				[FieldOffset (60)]
				public Vector3 acceleration;

				[FieldOffset (72)]
				public Vector3 angularAcceleration;

				[FieldOffset (84)]
				public ulong timestamp;

				[FieldOffset (92)]
				public int trackerConfidence;

				[FieldOffset (96)]
				public int mapperConfidence;
			}

			[StructLayout (LayoutKind.Sequential)]
			public struct Callbacks
			{
				// Points to unmanaged PoseData
				public IntPtr pose;

				// Points to instance method
				public IntPtr onPose;

				// Points to static callback
				public Action<IntPtr> Callback;
			}

			IntPtr posePtr;

			IntPtr onPosePtr;

			public IntPtr Ptr { get; private set; }

			readonly Callbacks cb;

			readonly PoseData poseData = new PoseData {
//				rotation = Quaternion.identity,
				rotation = Quaternion.LookRotation (Vector3.up, Vector3.forward),
			};

			public bool TryGetPose (ref Pose pose)
			{
				if (posePtr == IntPtr.Zero)
					return false;
				
				Marshal.PtrToStructure (posePtr, poseData);

				pose.sourceIndex = (DeviceIndex)poseData.index;
				pose.translation = poseData.translation;
				pose.velocity = poseData.velocity;
				pose.acceleration = poseData.velocity;
				pose.rotation = poseData.rotation;
				pose.angularVelocity = poseData.angularVelocity;
				pose.angularAcceleration = poseData.angularAcceleration;
				pose.timestamp = (long)poseData.timestamp;
				pose.trackerConfidence = (Confidence)poseData.trackerConfidence;

				return true;
			}

			public Pose Pose { 
				get {
					if (posePtr != IntPtr.Zero)
						Marshal.PtrToStructure (posePtr, poseData);
					return new Pose {
						sourceIndex = (DeviceIndex)poseData.index,
						translation = poseData.translation,
						velocity = poseData.velocity,
						acceleration = poseData.velocity,
						rotation = poseData.rotation,
						angularVelocity = poseData.angularVelocity,
						angularAcceleration = poseData.angularAcceleration,
						timestamp = (long)poseData.timestamp,
						trackerConfidence = (Confidence)poseData.trackerConfidence,
						mapperConfidence = 0,
					};
				}
			}

			[AOT.MonoPInvokeCallback (typeof(Action<IntPtr>))]
			static void Invoke (IntPtr action)
			{
				if (action == IntPtr.Zero)
					return;
				(GCHandle.FromIntPtr (action).Target as Action) ();
			}

			public PoseListener (Action onPose)
			{
				posePtr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof(PoseData)));
				Marshal.StructureToPtr (poseData, posePtr, false);

				onPosePtr = (IntPtr)GCHandle.Alloc (onPose);

				cb = new Callbacks { 
					pose = posePtr,
					onPose = onPosePtr,
					Callback = Invoke
				};

				Ptr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof(Callbacks)));
				Marshal.StructureToPtr (cb, Ptr, false);
			}

			#region IDisposable implementation

			public void Dispose ()
			{
				if (onPosePtr != IntPtr.Zero) {
					try {
						GCHandle.FromIntPtr (onPosePtr).Free ();
					} catch (Exception e) {
						Debug.LogException (e);
					} finally {
						onPosePtr = IntPtr.Zero;
					}
				}
				if (posePtr != IntPtr.Zero) {
					Marshal.FreeHGlobal (posePtr);
					posePtr = IntPtr.Zero;
				}
				
			}

			#endregion

		}

		internal class ControllerDevice : IControllerDevice, IDisposable
		{
			const string ControllerListenerClass = "com.intel.realsense.tracking.unity.ControllerListener";

			readonly AndroidJavaObject instance;
			readonly AndroidJavaObject m_poseListener;
			readonly ControllerListener listener;
			readonly PoseListener poseListener;

			const int FRAME_ID_OFFSET = 24;
			const int SENSOR_ID_OFFSET = 36;
			const int INSTANCE_ID_OFFSET = 37;
			const int SENSOR_DATA_OFFSET = 38;
			const int DATA_SIZE = 6;

			void RaiseControllerEvent (IntPtr p)
			{
				var b = new byte[44];
				Marshal.Copy (p, b, 0, 44);
//				Debug.LogFormat ("onControllerEvent: {0}", String.Join(",", Array.ConvertAll(b, x=>x.ToString())));

				var e = new ControllerEvent {
//					timestamp = BitConverter.ToUInt64(b, 8),
//					arrivalTimeStamp = BitConverter.ToUInt64(b, 16),
					sensorIndex = (byte)(b [6] >> 5),
					frameId = b [FRAME_ID_OFFSET],
					eventId = b [SENSOR_ID_OFFSET],
					instanceId = b [INSTANCE_ID_OFFSET],
					sensorData = new byte[DATA_SIZE],
				};
				Array.Copy (b, SENSOR_DATA_OFFSET, e.sensorData, 0, 6);

				Debug.LogFormat ("onControllerEvent: {0}", e);
				var h = onControllerEvent;
				if (h != null)
					h (e);
			}

			void RaiseConnectionEvent (IntPtr controllerDevice, int index, bool isConnected)
			{
				Debug.LogFormat ("RaiseConnectionEvent {0}, {1}, {2}", controllerDevice, index, isConnected);

				if (!isConnected) {

//					Dispose ();

					var h = onDisconnect;
					if (h != null)
						h.Invoke (index);					
				}
			}

			class ControllerListener : IDisposable
			{
				readonly Callbacks callbacks;

				public IntPtr Ptr { get; private set; }

				public ControllerListener (Action<IntPtr> onControllerEvent, Action<IntPtr, int, bool> onConnectionEvent)
				{
					callbacks = new Callbacks {
						onControllerEvent = (IntPtr)GCHandle.Alloc (onControllerEvent),
						onControllerEventCallback = InvokeControllerEvent,

						onConnectionEvent = (IntPtr)GCHandle.Alloc (onConnectionEvent),
						onConnectionEventCallback = InvokeConnectionEvent,
					};

					Ptr = Marshal.AllocHGlobal (Marshal.SizeOf (typeof(Callbacks)));
					Marshal.StructureToPtr (callbacks, Ptr, false);
				}

				[AOT.MonoPInvokeCallback (typeof(Action<IntPtr>))]
				static void InvokeControllerEvent (IntPtr action, IntPtr @event)
				{
					(GCHandle.FromIntPtr (action).Target as Action<IntPtr>) (@event);
				}

				[AOT.MonoPInvokeCallback (typeof(Action<IntPtr>))]
				static void InvokeConnectionEvent (IntPtr action, IntPtr ctrl, int index, bool isConnected)
				{
//					Debug.LogFormat ("InvokeConnectionEvent: {0}, {1}, {2}", ctrl, index, isConnected);
					(GCHandle.FromIntPtr (action).Target as Action<IntPtr, int, bool>) (ctrl, index, isConnected);
				}

				#region IDisposable implementation

				public void Dispose ()
				{
					((GCHandle)(callbacks.onControllerEvent)).Free ();
					((GCHandle)(callbacks.onConnectionEvent)).Free ();

					if (Ptr != IntPtr.Zero) {
						Marshal.FreeHGlobal (Ptr);
						Ptr = IntPtr.Zero;
					}
				}

				#endregion

				[StructLayout (LayoutKind.Sequential)]
				public struct Callbacks
				{
					public IntPtr onControllerEvent;

					public Action<IntPtr, IntPtr> onControllerEventCallback;

					public IntPtr onConnectionEvent;

					public Action<IntPtr, IntPtr, int, bool> onConnectionEventCallback;
				}
			}

			public ControllerDevice (AndroidJavaObject instance)
			{
				this.instance = instance;

				listener = new ControllerListener (RaiseControllerEvent, RaiseConnectionEvent);
//				GCHandle.Alloc (listener, GCHandleType.Pinned);

				poseListener = new PoseListener (RaisePoseEvent);

				m_poseListener = new AndroidJavaObject (ControllerListenerClass, listener.Ptr.ToInt64 (), poseListener.Ptr.ToInt64 ());
			}

			void RaisePoseEvent ()
			{
				var h = onPose;
				if (h != null)
					h.Invoke (GetPose ());
			}

			public void Dispose ()
			{
				Debug.Log ("Dispose");

				onPose = null;
				onConnect = null;
				onDisconnect = null;
				onControllerEvent = null;

				listener.Dispose ();
				poseListener.Dispose ();

				m_poseListener.Dispose ();

				instance.Dispose ();
			}

			//			#if DEBUG
			~ControllerDevice ()
			{
				Debug.Log ("~ControllerDevice");
			}
			//			#endif

			public Pose GetPose ()
			{
				return poseListener.Pose;
			}

			public bool TryGetPose (ref Pose pose)
			{
				return poseListener.TryGetPose (ref pose);
			}

			#region IControllerDevice implementation

			public event Action<Pose> onPose = null;

			public event Action<ControllerEvent> onControllerEvent = null;

			public event Action<int> onConnect = null;

			public event Action<int> onDisconnect = null;

			public int Id { get; private set; }

			public int Connect ()
			{
				try {
					Id = instance.Call<int> ("connect", m_poseListener);
//					Index = instance.Call<int> ("connect", m_listener);
					Debug.LogFormat ("Connect: index: {0}", Id);

					m_isConnected = true;
					onConnect.Invoke (Id);

					return Id;
				} catch (AndroidJavaException e) {
					Debug.LogException (e);
					m_isConnected = false;
					throw e;
				}
			}

			public void Disconnect ()
			{
				try {
					instance.Call ("disconnect");
					m_isConnected = false;
				} catch (AndroidJavaException e) {
					Debug.LogException (e);
				}
			}

			public byte[] MacAddress {
				get {
					using (var a = instance.Call<AndroidJavaObject> ("getMacAddress")) {
						return AndroidJNIHelper.ConvertFromJNIArray<byte[]> (a.GetRawObject ());
					}
				}
			}

			#endregion

			bool m_isConnected;

			public bool IsConnected ()
			{
				return m_isConnected;
			}

			public override string ToString ()
			{
				return string.Format ("{0}: {1}", GetType ().Name,
					string.Join (":", Array.ConvertAll (MacAddress, b => b.ToString ()))
				);
			}
		}

		internal class TrackingDevice : ITrackingDevice
		{
			const string TrackingDeviceListener = "com.intel.realsense.tracking.unity.TrackingDeviceListener";

			public event Action<Pose> onPose = null;
			public event Action<IControllerDevice> onControllerDiscovery = null;
			public event Action onClosed;
//			public event Action<Vector3> onGyro = null;

			readonly AndroidJavaObject m_dev;

			readonly TrackingDevice.ErrorListener m_errorListener;
			readonly ControllerDiscoveryListener m_controllerDiscoveryListener;
			readonly AndroidJavaObject m_poseListener;

			readonly PoseListener poseListener;

			public TrackingDevice (AndroidJavaObject dev)
			{
				m_dev = dev;

				m_errorListener = new TrackingDevice.ErrorListener ();
				m_controllerDiscoveryListener = new TrackingDevice.ControllerDiscoveryListener (this);

				poseListener = new PoseListener (RaisePoseEvent);

				m_poseListener = new AndroidJavaObject (TrackingDeviceListener, poseListener.Ptr.ToInt64 ());
			}

			~TrackingDevice ()
			{
				Debug.Log ("~TrackingDevice");
			}

			void RaisePoseEvent ()
			{
				var h = onPose;
				if (h != null)
					h.Invoke (GetPose ());
			}

			public void EnableControllers (int numberOfControllers)
			{
				m_dev.Call ("enableControllers", numberOfControllers, m_controllerDiscoveryListener);
			}

			public void EnableTracking ()
			{
				m_dev.Call ("enableTracking", m_poseListener);
			}

			public Pose GetPose ()
			{
				return poseListener.Pose;
			}

			public bool TryGetPose (ref Pose pose)
			{
				return poseListener.TryGetPose (ref pose);
			}

			#region ITrackingDevice implementation

			public void Start ()
			{
				m_dev.Call ("start", m_errorListener);
			}

			public void Stop ()
			{
				m_dev.Call ("stop");
			}

			public void Close ()
			{
				Debug.Log ("Close");

				onPose = null;
				onControllerDiscovery = null;

				poseListener.Dispose ();

				if (m_poseListener != null) {
					m_poseListener.Set<long> ("mListener", 0L);
					m_poseListener.Dispose ();
				}
					
				var h = onClosed;
				if (h != null)
					h ();
				onClosed = null;

				using (m_dev) {
					m_dev.Call ("close");
				}
			}

			public void Reset ()
			{
				using (var javaUnityPlayer = new AndroidJavaClass ("com.unity3d.player.UnityPlayer")) {
					using (var currentActivity = javaUnityPlayer.GetStatic<AndroidJavaObject> ("currentActivity")) {
						using (var tm_class = new AndroidJavaClass ("com.intel.realsense.tracking.TrackingManager")) {
							tm_class.CallStatic ("resetTrackingDevice", currentActivity);
						}
					}
				}
			}

			public StreamProfile[] GetSupportedRawStreams ()
			{
				throw new NotImplementedException ();
			}

			public void EnableRawStreams (StreamProfile[] profiles)
			{
				throw new NotImplementedException ();
			}

			//			public bool IsControllerConnected (int index)
			//			{
			//				foreach (var c in m_controllerDiscoveryListener.controllers) {
			//					if (c.Index == index && c.IsConnected ())
			//						return true;
			//				}
			//				return false;
			//			}

			public string FirmwareVersion {
				get {
					if (m_dev == null)
						return null;
					using (var info = m_dev.Call<AndroidJavaObject> ("getInfo")) {
						return info == null ? null : info.Call<string> ("getFirmwareVersion");
					}
				}
			}

			public SensorTemperature[] Temperature {
				get {
					using (var list = m_dev.Call<AndroidJavaObject> ("getTemperature")) {
						using (var arr = list.Call<AndroidJavaObject> ("toArray")) {
							var temps = AndroidJNIHelper.ConvertFromJNIArray<AndroidJavaObject[]> (arr.GetRawObject ());
							return Array.ConvertAll (temps, a => new SensorTemperature {
								index = (TemperatureSensor)a.Get<AndroidJavaObject> ("sensor").Call<int> ("ordinal"),
								temperature = a.Get<float> ("temperature"),
								threshold = a.Get<float> ("threshold"),
							});
						}
					}
				}
			}

			#endregion

			public sealed class ErrorListener : AndroidJavaProxy
			{

				public ErrorListener ()
					: base ("com.intel.realsense.tracking.TrackingDevice$ErrorListener")
				{
//					Debug.Log ("ErrorListener");
				}

				public void onError (AndroidJavaObject error)
				{
					using (error) {
						var e = (TrackingError)error.Call<int> ("ordinal");
						Debug.LogError (e);
//						throw new Exception (e.ToString ());
					}
				}
			}

			public sealed class ControllerDiscoveryListener : AndroidJavaProxy
			{
				readonly WeakReference m_dev;

				public ControllerDiscoveryListener (TrackingDevice device)
					: base ("com.intel.realsense.tracking.TrackingDevice$ControllerDiscoveryListener")
				{
					m_dev = new WeakReference (device);
				}

				public void onControllerDiscoveryEvent (AndroidJavaObject controllerDevice)
				{				
					var ctrl = new ControllerDevice (controllerDevice);
//					
					var dev = m_dev.Target as TrackingDevice;
					if (dev != null) {
						if (dev.onControllerDiscovery != null)
							dev.onControllerDiscovery (ctrl);
					}
				}

			}
		}

		internal static TrackingDevice getTrackingDevice ()
		{
			using (var javaUnityPlayer = new AndroidJavaClass ("com.unity3d.player.UnityPlayer")) {
				using (var currentActivity = javaUnityPlayer.GetStatic<AndroidJavaObject> ("currentActivity")) {
					using (var tm_class = new AndroidJavaClass ("com.intel.realsense.tracking.TrackingManager")) {
						AndroidJavaObject dev = tm_class.CallStatic<AndroidJavaObject> ("getTrackingDevice", currentActivity);
						if (dev == null) {
							throw new Exception ("Got a null TrackingDevice");
						}
						return new TrackingDevice (dev);
					}
				}
			}
		}

		public AndroidTrackingManager ()
		{
//			AndroidJNIHelper.debug = true;

			Screen.sleepTimeout = SleepTimeout.NeverSleep;

			IsDeviceConnected = false;
			m_managerListener = new Enumerator.Listener (this);

			using (var javaUnityPlayer = new AndroidJavaClass ("com.unity3d.player.UnityPlayer")) {
				using (var currentActivity = javaUnityPlayer.GetStatic<AndroidJavaObject> ("currentActivity")) {
					using (var tm_class = new AndroidJavaClass ("com.intel.realsense.tracking.TrackingManager")) {
						Version = tm_class.CallStatic<String> ("getVersion", currentActivity);
						m_enumerator = new Enumerator (currentActivity, m_managerListener);
					}
				}
			}
		}

		public void Dispose ()
		{
			try {

				if (m_enumerator != null) {
					m_enumerator.Close ();
				}
				//				if (m_dev != null) {
				//					m_dev.Stop ();
				//					m_dev.Close ();
				//				}
			} catch (Exception e) {
				Debug.Log (e);
			}			
		}
	}
}