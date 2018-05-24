using UnityEngine;
using Intel.RealSense.Tracking;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Security;

namespace Intel.RealSense.Tracking
{
	public class WindowsTrackingManager : ITrackingManager, IDisposable
	{
		/// <summary>
		/// 
		/// </summary>
		internal sealed class TrackingDevice : ITrackingDevice
		{
			Pose m_pose;

			public Pose GetPose ()
			{
				return m_pose;
			}

			public bool TryGetPose (ref Pose pose)
			{
				pose = m_pose;
				return true;
			}

			public Vector3 GetLocalPosition ()
			{
				return m_pose.translation;
			}

			public bool TryGetLocalPosition (out Vector3 pos)
			{
				pos = m_pose.translation;
				return true;
			}

			public Quaternion GetLocalRotation ()
			{
				return m_pose.rotation;
			}

			public long GetTimestamp ()
			{
				return m_pose.timestamp;
			}

			public bool TryGetLocalRotation (out Quaternion rot)
			{
				rot = m_pose.rotation;
				return true;
			}

			public bool IsControllerConnected (int index)
			{
				throw new NotImplementedException ();
			}

			public readonly HandleRef instance;
			readonly Listener m_listener;

			//!TODO: lock?
			public readonly ControllerDevice[] controllers = new ControllerDevice[]{ null, null };

			public TrackingDevice (IntPtr pNativeData)
			{
				instance = new HandleRef (this, pNativeData);

				m_listener = new Listener {
					onPose = p => {
						if (p.sourceIndex == DeviceIndex.HMD) {
							m_pose = p;
							var h = onPose;
							if (h != null)
								h (p);
						} else {
							var ctrl = controllers [(int)p.sourceIndex - 1];
							if (ctrl != null)
								ctrl.RaisePoseEvent (p);
						}
					},

					onGyroFrame = g => {
						var h = onGyro;
						if (h != null)
							h.Invoke (g.angularVelocity);
					},

					//onVideoFrame = fptr =>
					//{
					//    var f = (VideoFrame)Marshal.PtrToStructure(fptr, typeof(VideoFrame));
					//    onVideoFrame.Invoke(f);
					//},

					onVideoFrame = f => {
						var h = onVideoFrame;
						if (h != null)
							h.Invoke (f);
					},

					onControllerDiscoveryEventFrame = d => {
						Debug.Log (d);
						var ctrl = new ControllerDevice (this, d);
						onControllerDiscovery (ctrl);
					},

					onControllerConnectedEventFrame = e => {
						
					},

					onControllerDisconnectedEventFrame = e => {
						Debug.Log (e);
						controllers [e.controllerId - 1].RaiseDisconnect ();
					},

					onControllerFrame = e => {
						onControllerEvent (e);
					}
				};
			}

			#if DEBUG
			~TrackingDevice ()
			{
				Debug.Log ("~TrackingDevice " + instance.Handle);
			}
			#endif

			/// <summary>
			/// Retrieve information on the TM2 device
			/// </summary>
			public DeviceInfo Info {
				get {
					DeviceInfo info = default(DeviceInfo);
					var st = NativeMethods.TrackingDevice_GetDeviceInfo (instance.Handle, ref info);
					if (st != Status.SUCCESS) {
						throw new InvalidOperationException (st.ToString ());
					}
					return info;
				}
			}

			/// <summary>
			/// Start streaming of all stream that were previously configured.
			/// </summary>
			public void Start ()
			{
				Status s = NativeMethods.TrackingDevice_Start (instance.Handle, m_listener);
				if (s != Status.SUCCESS) {
					//Debug.LogError(s);
					throw new Exception (s.ToString ());
				}
			}

			/// <summary>
			/// Stop streaming of all stream that were previously configured, all stream configuration parameters will be cleared.
			/// </summary>
			public void Stop ()
			{
				NativeMethods.TrackingDevice_Stop (instance.Handle);
			}

			/// <summary>
			/// Enables 6DoF tracking.
			/// </summary>
			public void EnableTracking ()
			{
				NativeMethods.TrackingDevice_EnableTracking (instance.Handle);
			}

			public void EnableRawStreams (StreamProfile[] profiles)
			{
				NativeMethods.TrackingDevice_EnableRawStreams (instance.Handle, profiles, profiles.Length);
			}

			/// <summary>
			/// Enables the controllers. The device will keep the scanning active until the requested number of controllers are connected.
			/// To disable controllers, EnableControllers with numberOfControllers equals 0.
			/// </summary>
			/// <param name="numControllers"></param>
			public void EnableControllers (int numControllers)
			{
				NativeMethods.TrackingDevice_EnableControllers (instance.Handle, numControllers);
			}

			public void ConnectController (byte[] macAddress, byte controllerId)
			{
				var st = NativeMethods.TrackingDevice_ControllerConnect (instance.Handle, macAddress, 2000, out controllerId);
				if (st != Status.SUCCESS) {
					//Debug.LogError (st);
					throw new Exception (st.ToString ());
				}
			}

			public void ControllerDisconnect (int controllerId)
			{
				var st = NativeMethods.TrackingDevice_ControllerDisconnect (instance.Handle, controllerId);
				if (st != Status.SUCCESS) {
					Debug.LogError (st);
				}
			}

			public List<byte[]> readAssociatedDevices ()
			{
				ControllerAssociatedDevices devices;
				var st = NativeMethods.TrackingDevice_GetAssociatedDevices (instance.Handle, out devices);
				if (st != Status.SUCCESS) {
					Debug.LogError (st);
				}
				return new List<byte[]> { 
					devices.macAddress1, devices.macAddress2
				};
			}

			public void writeAssociatedDevices (byte[] mac1, byte[] mac2)
			{
				var st = NativeMethods.TrackingDevice_SetAssociatedDevices (instance.Handle, new ControllerAssociatedDevices {
					macAddress1 = mac1,
					macAddress2 = mac2
				});
				if (st != Status.SUCCESS) {
					Debug.LogError (st);
				}
			}

			public event Action<Pose> onPose = delegate { };
			public event Action<Vector3> onGyro = null;

			public event Action<ControllerEvent> onControllerEvent = delegate { };

			public event Action<IControllerDevice> onControllerDiscovery = delegate { };
			public event Action<VideoFrame> onVideoFrame = null;

			//public event Action<IControllerDevice> onControllerDisconnect = delegate { };

			public void Close ()
			{
				Debug.Log ("Close");

//				Reset ();

				m_listener.Clear ();
				onPose = null;
				onControllerEvent = null;
				onControllerDiscovery = null;

				NativeMethods.TrackingDevice_Close (instance.Handle);

//				GC.Collect ();
//				GC.WaitForPendingFinalizers ();
			}

			public void Reset ()
			{
				Status st = NativeMethods.TrackingDevice_Reset (instance.Handle);
				if (st != Status.SUCCESS) {
					Debug.LogError ("Reset: " + st);
					throw new Exception (st.ToString ());
				}
			}

			public StreamProfile[] GetSupportedRawStreams ()
			{
				StreamProfile[] profiles = null;
				NativeMethods.TrackingDevice_GetSupportedRawStreams (instance.Handle, (ptr, size) => {
					profiles = new StreamProfile[size];

					int s = Marshal.SizeOf (typeof(StreamProfile));
					for (int i = 0; i < size; i++) {
						profiles [i] = (StreamProfile)Marshal.PtrToStructure (new IntPtr (ptr.ToInt64 () + s * i), typeof(StreamProfile));
					}

				});

				return profiles;
			}

			public string FirmwareVersion {
				get {
					try {
						var i = Info;
						return i.version.fw.ToString ();
					} catch (Exception e) {
						Debug.LogException (e);
						return string.Empty;
					}
				}
			}

			public SensorTemperature[] Temperature {
				get {
					var t = default(Temperature);
					var st = NativeMethods.TrackingDevice_GetTemperature (instance.Handle, ref t);
					if (st != Status.SUCCESS)
						throw new Exception (st.ToString ());
					return t.sensor;
				}
			}

			// placeholder...
			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void FrameDelegate (IntPtr data);

			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void onPoseFrameDelegate (Pose pose);

			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void onGyroFrameDelegate (GyroFrame frame);

			[SuppressUnmanagedCodeSecurity]
            [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void VideoFrameDelegate (VideoFrame frame);

			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void onControllerDiscoveryEventFrameDelegate (ControllerDiscoveryEventFrame frame);

			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
			public delegate void onControllerConnectedEventFrameDelegate (ControllerConnectedEventFrame frame);

			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void onControllerDisconnectedEventFrameDelegate (ControllerDisconnectedEventFrame frame);

			[SuppressUnmanagedCodeSecurity]
			[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
            public delegate void onControllerEventFrameDelegate (ControllerEvent frame);

			[StructLayout (LayoutKind.Sequential)]
			internal sealed class Listener
			{
				public onPoseFrameDelegate onPose;
				public VideoFrameDelegate onVideoFrame;
				public FrameDelegate onAccelerometerFrame;
				public onGyroFrameDelegate onGyroFrame;
				public onControllerDiscoveryEventFrameDelegate onControllerDiscoveryEventFrame;
				public onControllerEventFrameDelegate onControllerFrame;
				public onControllerConnectedEventFrameDelegate onControllerConnectedEventFrame;
				public onControllerDisconnectedEventFrameDelegate onControllerDisconnectedEventFrame;

				public void Clear ()
				{
					onPose = null;
					onVideoFrame = null;
					onAccelerometerFrame = null;
					onGyroFrame = null;
					onControllerDiscoveryEventFrame = null;
					onControllerFrame = null;
					onControllerConnectedEventFrame = null;
					onControllerDisconnectedEventFrame = null;
				}
			}
		}

		internal sealed class ControllerDevice : IControllerDevice
		{
			TrackingDevice device { get { return m_device.Target as TrackingDevice; } }

			readonly WeakReference m_device;

			struct ControllerInfo: IControllerInfo
			{
				public ControllerInfo (ControllerDiscoveryEventFrame e)
				{
					VendorData = e.vendorData;
				}

				public int VendorData {
					get;
					private set;
				}
			}

			readonly ControllerInfo m_info;

			public ControllerDevice (TrackingDevice device, ControllerDiscoveryEventFrame d)
			{
				this.MacAddress = d.macAddress;
				this.m_device = new WeakReference (device);
				this.m_info = new ControllerInfo (d);
			}

			#if DEBUG
			~ControllerDevice ()
			{
				Debug.Log ("~ControllerDevice");
			}
			#endif

			static public bool MacsEqual (byte[] m0, byte[] m)
			{
				for (int i = 0; i < m.Length; i++)
					if (m0 [i] != m [i])
						return false;
				return true;
			}

			#region IPoseListener implementation

			Pose m_pose;

			public event Action<Pose> onPose = delegate { };

			public Pose GetPose ()
			{
				return m_pose;
			}

			public bool TryGetPose (ref Pose pose)
			{
				pose = m_pose;
				return true;
			}

			#endregion

			public IControllerInfo Info {
				get {
					return m_info;
				}
			}

			#region IControllerDevice implementation

			public event Action<ControllerEvent> onControllerEvent = delegate { };
			public event Action<int> onConnect = delegate { };
			public event Action<int> onDisconnect = delegate { };

			public int Connect ()
			{
				var dev = this.device;
				if (dev == null)
					throw new NullReferenceException ("device is null");

				byte id;
				var st = NativeMethods.TrackingDevice_ControllerConnect (dev.instance.Handle, MacAddress, 2000, out id);
				this.Id = id;
				Debug.LogFormat ("ControllerConnect: status: {0}, id: {1}", st, id);

				if (st != Status.SUCCESS) {
					throw new Exception (st.ToString ());
				}
				dev.controllers [Id - 1] = this;

				dev.onControllerEvent += e => onControllerEvent (e);
				dev.onPose += RaisePoseEvent;
				onConnect.Invoke (Id);

				return Id;	
			}

			internal void RaisePoseEvent (Pose pose)
			{
				if ((byte)pose.sourceIndex == Id) {
					m_pose = pose;
					onPose.Invoke (m_pose);
				}
			}

			public void Disconnect ()
			{
				var dev = device;
				if (dev == null)
					throw new NullReferenceException ("device is null");
				dev.onPose -= RaisePoseEvent;
				dev.ControllerDisconnect (Id);
				dev.controllers [Id] = null;
				onDisconnect.Invoke (Id);
			}

			public byte[] MacAddress { get; private set; }

			public byte Id { get; private set; }

			#endregion

			public override string ToString ()
			{
				return string.Format ("{0}: [ Id:{1}, MacAddress:{2} ]", this.GetType ().Name, Id,
					string.Join (":", Array.ConvertAll (MacAddress, b => b.ToString ("X")))
				);
			}

			internal void RaiseDisconnect ()
			{
				onDisconnect.Invoke (Id);
			}
		}

		readonly HandleRef instance;
		readonly Listener m_listener;
		//		WeakReference m_dev;

		#region ITrackingManager implementation

		public event Action<ITrackingDevice> onTrackingDeviceAvailable = delegate { };
		public event Action onTrackingDeviceUnavailable = delegate { };

		public bool IsDeviceConnected { get; private set; }

		string m_version = null;

		public string Version {
			get {
				if (m_version != null)
					return m_version;
				var v = NativeMethods.TrackingManager_Version (instance.Handle);
				m_version = string.Format ("{0}.{1}.{2}.{3}", 
					(v >> 56) & 0xff, 
					(v >> 48) & 0xff, 
					(v >> 32) & 0xffff, 
					(v >> 00) & 0xffffffff);
				return m_version;
			}
		}

		#endregion

		internal sealed class Listener
		{
			internal enum EventType
			{
				ATTACH = 0,
				DETACH
			}

			[StructLayout (LayoutKind.Sequential)]
			public sealed class Callbacks
			{
				[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
				public delegate void onStateChangedDelegate (EventType state,IntPtr device);

				[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
				public delegate void onErrorDelegate (Error error,IntPtr device);

				public onStateChangedDelegate onStateChanged;
				public onErrorDelegate onError;
			}

			public readonly Callbacks callbacks;
			readonly WindowsTrackingManager tm;
			public WeakReference m_dev;

			public Listener (WindowsTrackingManager tm)
			{
				this.tm = tm;
				callbacks = new Listener.Callbacks {
					onStateChanged = (EventType state, IntPtr device) => {
						Debug.LogFormat ("{0} {1}", state, device);

						switch (state) {
						case EventType.ATTACH:
							onTrackingDeviceAavailable (device);
							break;
						case EventType.DETACH:
							onTrackingDeviceUnavailable ();
							break;
						}
					},

					onError = (Error error, IntPtr device) => {
						Debug.LogError (error);
//						IsDeviceConnected = false;
						//throw new Exception(error.ToString());
					},
				};				
			}

			void onTrackingDeviceAavailable (IntPtr device)
			{
				var dev = new TrackingDevice (device);
				m_dev = new WeakReference (dev);
				tm.onTrackingDeviceAvailable.Invoke (dev);
				tm.IsDeviceConnected = true;
			}

			public void onTrackingDeviceUnavailable ()
			{
				var dev = m_dev.Target as TrackingDevice;
				tm.onTrackingDeviceUnavailable.Invoke ();
				tm.IsDeviceConnected = false;
				if (dev != null)
					dev.Close ();
			}

		}


		public WindowsTrackingManager ()
		{
			m_listener = new Listener (this);
			instance = new HandleRef (this, NativeMethods.TrackingManager_CreateInstance (m_listener.callbacks));
		}

		public void Dispose ()
		{
			m_listener.callbacks.onError = null;
			m_listener.callbacks.onStateChanged = null;
			NativeMethods.TrackingManager_Destroy (instance.Handle);
		}

		#if DEBUG
		~WindowsTrackingManager ()
		{
			Debug.Log ("~WindowsTrackingManager");
		}
		#endif
	}

	#region Enums
	public enum Error
	{
		NONE = 0,
		INIT_FAILED = 1,
		DFU_FAILED = 2,
		NO_CALIBRATION_DATA = 1000
	}

	public enum Status
	{
		SUCCESS = 0,
		COMMON_ERROR = 1,
		FEATURE_UNSUPPORTED = 2,
		ERROR_PARAMETER_INVALID = 3,
		INIT_FAILED = 4,
		ALLOC_FAILED = 5,
		ERROR_USB_TRANSFER = 6,
		ERROR_EEPROM_VERIFY_FAIL = 7,
		ERROR_FW_INTERNAL = 8,
		BUFFER_TOO_SMALL = 9,
		NOT_SUPPORTED_BY_FW = 10,
		DEVICE_BUSY = 11,
		TIMEOUT = 12,
		TABLE_NOT_EXIST = 13,
		TABLE_LOCKED = 14,
		TEMPERATURE_WARNING = 15,
		TEMPERATURE_STOP = 16,
		SLAM_NO_DICTIONARY = 17,
	}

	public enum SensorType : byte
	{
		FISHEYE = 3,
		GYRO = 4,
		ACCELEROMETER = 5,
		CONTROLLER = 6
	}

	public enum PixelFormat : int
	{
		/// <summary>
		///  Any pixel format
		/// </summary>
		ANY,
		/// <summary>
		///  16-bit per pixel - linear depth values. The depth is meters is equal to depth scale * pixel value
		/// </summary>
		Z16,
		/// <summary>
		///  16-bit per pixel - linear disparity values. The depth in meters is equal to depth scale / pixel value
		/// </summary>
		DISPARITY16,
		/// <summary>
		///  96-bit per pixel - 32 bit floating point 3D coordinates.
		/// </summary>
		XYZ32F,
		/// <summary>
		///  16-bit per pixel - Standard YUV pixel format as described in https://en.wikipedia.org/wiki/YUV
		/// </summary>
		YUYV,
		/// <summary>
		///  24-bit per pixel - 8-bit Red, Green and Blue channels
		/// </summary>
		RGB8,
		/// <summary>
		///  24-bit per pixel - 8-bit Blue, Green and Red channels, suitable for OpenCV
		/// </summary>
		BGR8,
		/// <summary>
		///  32-bit per pixel - 8-bit Red, Green, Blue channels + constant alpha channel equal to FF
		/// </summary>
		RGBA8,
		/// <summary>
		///  32-bit per pixel - 8-bit Blue, Green, Red channels + constant alpha channel equal to FF
		/// </summary>
		BGRA8,
		/// <summary>
		///  8-bit per pixel - grayscale image
		/// </summary>
		Y8,
		/// <summary>
		///  16-bit per-pixel - grayscale image
		/// </summary>
		Y16,
		/// <summary>
		///  8-bit per pixel - raw image
		/// </summary>
		RAW8,
		/// <summary>
		///  10-bit per pixel - Four 10-bit luminance values encoded into a 5-byte macropixel
		/// </summary>
		RAW10,
		/// <summary>
		///  16-bit per pixel - raw image
		/// </summary>
		RAW16,
	}
	#endregion

	#region Structs

	[StructLayout (LayoutKind.Sequential)]
	struct Temperature
	{
		public uint numOfSensors;

		[MarshalAs (UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 3)]
		public SensorTemperature[] sensor;
	}

	[StructLayout (LayoutKind.Sequential)]                                                                                                                                        
	public struct Version
	{
		/// <summary>                                                                                                                                                        
		///  Major part of version                                                                                                                                           
		/// </summary>                                                                                                                                                       
		//[FieldOffset(0)]                                                                                                                                                   
		public uint major;
		/// <summary>                                                                                                                                                        
		///  Minor part of version                                                                                                                                           
		/// </summary>                                                                                                                                                       
		//[FieldOffset(4)]                                                                                                                                                   
		public uint minor;
		/// <summary>                                                                                                                                                        
		///  Patch part of version                                                                                                                                           
		/// </summary>                                                                                                                                                       
		//[FieldOffset(8)]                                                                                                                                                   
		public uint patch;
		/// <summary>                                                                                                                                                        
		///  Build part of version                                                                                                                                           
		/// </summary>                                                                                                                                                       
		//[FieldOffset(12)]                                                                                                                                                  
		public uint build;

		//public static readonly int Size = 16;

		public override string ToString ()
		{                                                                                                                                                                    
			return string.Format ("{0}.{1}.{2}.{3}", major, minor, patch, build);                                                 
		}
	}



	[System.Serializable]
	[StructLayout (LayoutKind.Sequential)]
	public struct DeviceInfo
	{

		[StructLayout (LayoutKind.Sequential)]                                                                                                                                             
		public struct UsbConnectionDescriptor
		{
			/// <summary>                                                                                                                                                             
			///  USB Vendor ID: DFU Device = 0x03E7, Device = 0x8087                                                                                                                  
			/// </summary>                                                                                                                                                            
			//[FieldOffset(0)]                                                                                                                                                        
			public ushort idVendor;
			/// <summary>                                                                                                                                                             
			///  USB Product ID: DFU Device = 0x2150, Device = 0x0AF3                                                                                                                 
			/// </summary>                                                                                                                                                            
			//[FieldOffset(2)]                                                                                                                                                        
			public ushort idProduct;
			/// <summary>                                                                                                                                                             
			///  USB specification release number: 0x100 = USB 1.0, 0x110 = USB 1.1, 0x200 = USB 2.0, 0x300 = USB 3.0                                                                 
			/// </summary>                                                                                                                                                            
			//[FieldOffset(4)]                                                                                                                                                        
			public ushort bcdUSB;
			/// <summary>                                                                                                                                                             
			///  Number of the port that the device is connected to                                                                                                                   
			/// </summary>                                                                                                                                                            
			//[FieldOffset(6)]                                                                                                                                                        
			public byte port;
			/// <summary>                                                                                                                                                             
			///  Number of the bus that the device is connected to                                                                                                                    
			/// </summary>                                                                                                                                                            
			//[FieldOffset(7)]                                                                                                                                                        
			public byte bus;
			/// <summary>                                                                                                                                                             
			///  Number of ports in the port tree of this device                                                                                                                      
			/// </summary>                                                                                                                                                            
			//[FieldOffset(8)]                                                                                                                                                        
			public byte portChainDepth;
			/// <summary>                                                                                                                                                             
			///  List of all port numbers from root for this device                                                                                                                   
			/// </summary>                                                                                                                                                            
			//[FieldOffset(9)]                                                                                                                                                        
			[MarshalAs (UnmanagedType.ByValArray, SizeConst = 64)]
			public byte[] portChain;

			//public static readonly int Size = 74;

			public override string ToString ()
			{                                                                                                                                                                         
				return string.Format ("UsbConnectionDescriptor:[ idVendor:{0}, idProduct:{1}, bcdUSB:{2}, port:{3}, bus:{4}, portChainDepth:{5}, portChain:{6} ]", idVendor, idProduct, bcdUSB, port, bus, portChainDepth, portChain);                                                                                                                              
			}
		}


		[StructLayout (LayoutKind.Sequential)]
		public struct DeviceStatus
		{
			/// <summary>
			///  Host status
			/// </summary>
			//[FieldOffset(0)]
			public Status host;
			/// <summary>
			///  HW status
			/// </summary>
			//[FieldOffset(4)]
			public Status hw;

			//public static readonly int Size = 8;

			public override string ToString ()
			{
				return string.Format ("DeviceStatus:[ host:{0}, hw:{1} ]", host, hw);
			}
		}


		[StructLayout (LayoutKind.Sequential)]                                                                                                                                             
		public struct DeviceVersion
		{
			/// <summary>                                                                                                                                                             
			///  Device supported interface API version                                                                                                                               
			/// </summary>                                                                                                                                                            
			//[FieldOffset(0)]                                                                                                                                                        
			public Version deviceInterface;
			/// <summary>                                                                                                                                                             
			///  libtm Host version                                                                                                                                                   
			/// </summary>                                                                                                                                                            
			//[FieldOffset(16)]                                                                                                                                                       
			public Version host;
			/// <summary>                                                                                                                                                             
			///  Myriad firmware version                                                                                                                                              
			/// </summary>                                                                                                                                                            
			//[FieldOffset(32)]                                                                                                                                                       
			public Version fw;
			/// <summary>                                                                                                                                                             
			///  ASIC Board version: 0x00 = ES0, 0x01 = ES1, 0x02 = ES2, 0x03 = ES3, 0xFF = Unknown - only major part is active                                                       
			/// </summary>                                                                                                                                                            
			//[FieldOffset(48)]                                                                                                                                                       
			public Version hw;
			/// <summary>                                                                                                                                                             
			///  Central firmware version - only major, minor, patch parts are active                                                                                                 
			/// </summary>                                                                                                                                                            
			//[FieldOffset(64)]                                                                                                                                                       
			public Version centralApp;
			/// <summary>                                                                                                                                                             
			///  Central BLE protocol version - only major part is active                                                                                                             
			/// </summary>                                                                                                                                                            
			//[FieldOffset(80)]                                                                                                                                                       
			public Version centralProtocol;
			/// <summary>                                                                                                                                                             
			///  Central BLE protocol version - only major, minor, patch parts are active                                                                                             
			/// </summary>                                                                                                                                                            
			//[FieldOffset(96)]                                                                                                                                                       
			public Version centralBootLoader;
			/// <summary>                                                                                                                                                             
			///  Central BLE protocol version - only major part is active                                                                                                             
			/// </summary>                                                                                                                                                            
			//[FieldOffset(112)]                                                                                                                                                      
			public Version centralSoftDevice;
			/// <summary>                                                                                                                                                             
			///  EEPROM data version - only major and minor parts are active                                                                                                          
			/// </summary>                                                                                                                                                            
			//[FieldOffset(128)]                                                                                                                                                      
			public Version eeprom;
			/// <summary>                                                                                                                                                             
			///  Myriad ROM version - only major part is active                                                                                                                       
			/// </summary>                                                                                                                                                            
			//[FieldOffset(144)]                                                                                                                                                      
			public Version rom;

			//public static readonly int Size = 160;

			public override string ToString ()
			{                                                                                                                                                                         
				return string.Format ("DeviceVersion:[ deviceInterface:{0}, host:{1}, fw:{2}, hw:{3}, centralApp:{4}, centralProtocol:{5}, centralBootLoader:{6}, centralSoftDevice:{7}, eeprom:{8}, rom:{9} ]", deviceInterface, host, fw, hw, centralApp, centralProtocol, centralBootLoader, centralSoftDevice, eeprom, rom);
			}
		}

		public enum EepromLockState
		{
			/// <summary>
			///  The EEPROM is fully writeable
			/// </summary>
			LOCKSTATEWRITEABLE,
			/// <summary>
			///  The upper quarter of the EEPROM memory is write-protected
			/// </summary>
			LOCKSTATELOCKED,
			/// <summary>
			///  The upper quarter of the EEPROM memory is permanently write-protected
			/// </summary>
			LOCKSTATEPERMANENTLOCKED,
			LOCKSTATEMAX,
		}

		/// <summary>
		///  USB Connection Descriptor includes USB info and physical location
		/// </summary>
		//[FieldOffset(0)]
		public UsbConnectionDescriptor usbDescriptor;
		/// <summary>
		///  HW and Host status
		/// </summary>
		//[FieldOffset(76)]
		public DeviceStatus status;
		/// <summary>
		///  HW, FW, Host, Central, EEPROM, ROM, interface versions
		/// </summary>
		//[FieldOffset(84)]
		public DeviceVersion version;
		/// <summary>
		///  EEPROM Lock state
		/// </summary>
		//[FieldOffset(244)]
		public EepromLockState eepromLockState;
		/// <summary>
		///  Device identifier: 0x1 = T250
		/// </summary>
		//[FieldOffset(248)]
		public byte deviceType;
		/// <summary>
		///  Device serial number
		/// </summary>
		//[FieldOffset(256)]
		public ulong serialNumber;
		/// <summary>
		///  Number of Gyro Supported Profiles returned by Supported RAW Streams
		/// </summary>
		//[FieldOffset(264)]
		public byte numGyroProfile;
		/// <summary>
		///  Number of Accelerometer Supported Profiles returned by Supported RAW Streams
		/// </summary>
		//[FieldOffset(265)]
		public byte numAccelerometerProfiles;
		/// <summary>
		///  Number of Video Supported Profiles returned by Supported RAW Streams
		/// </summary>
		//[FieldOffset(266)]
		public byte numVideoProfiles;

		//public static readonly int Size = 272;

		public override string ToString ()
		{
			return string.Format ("DeviceInfo:[ usbDescriptor:{0}, status:{1}, version:{2}, eepromLockState:{3}, deviceType:{4}, serialNumber:{5}, numGyroProfile:{6}, numAccelerometerProfiles:{7}, numVideoProfiles:{8} ]", usbDescriptor, status, version, eepromLockState, deviceType, serialNumber, numGyroProfile, numAccelerometerProfiles, numVideoProfiles);
		}
	}


	[System.Serializable]
	[StructLayout (LayoutKind.Sequential)]
	public struct GyroFrame
	{
		/// <summary>
		///  Frame integration timestamp, as measured in nanoseconds since device initialization
		/// </summary>
		public long timestamp;
		/// <summary>
		///  Frame arrival timestamp, as measured in nanoseconds since device initialization
		/// </summary>
		public long arrivalTimeStamp;
		/// <summary>
		///  Host correlated time stamp in nanoseconds
		/// </summary>
		public long systemTimestamp;
		/// <summary>
		///  Zero based index of sensor with the same type within device
		/// </summary>
		public byte sensorIndex;
		/// <summary>
		///  A running index of frames from every unique sensor, starting from 0
		/// </summary>
		public uint frameId;
		/// <summary>
		///  X, Y, Z values of gyro, in radians/sec
		/// </summary>
		public Vector3 angularVelocity;

		public override string ToString ()
		{
			return string.Format ("GyroFrame:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, sensorIndex:{3}, frameId:{4}, angularVelocity:{5} ]", timestamp, arrivalTimeStamp, systemTimestamp, sensorIndex, frameId, angularVelocity);
		}
	}

	[System.Serializable]
    //[StructLayout(LayoutKind.Explicit)]
	[StructLayout (LayoutKind.Sequential)]
	public struct RawProfile
	{
		/// <summary>
		///  Length in bytes of each line in the image (including padding). 0 for non-camera streams. 
		/// </summary>
		//[FieldOffset(0)]
		public ushort stride;
		/// <summary>
		///  Supported width (in pixels) of first stream, 0 for non-camera streams 
		/// </summary>
		//[FieldOffset(2)]
		public ushort width;
		/// <summary>
		///  Supported height (in pixels) or first stream, 0 for non-camera streams 
		/// </summary>
		//[FieldOffset(4)]
		public ushort height;
		/// <summary>
		///  Pixel format of the stream, according to enum PixelFormat 
		/// </summary>
		//[FieldOffset(8)]
		public PixelFormat pixelFormat;

		public override string ToString ()
		{
			return string.Format ("RawProfile:[ stride:{0}, width:{1}, height:{2}, pixelFormat:{3} ]", stride, width, height, pixelFormat);
		}
	}


	[System.Serializable]
    //[StructLayout(LayoutKind.Explicit)]
	[StructLayout (LayoutKind.Sequential)]
	public struct VideoProfile
	{
		/// <summary>
		///  true if this profile is enabled 
		/// </summary>
		//[FieldOffset(0)]
		[MarshalAs (UnmanagedType.I1)]
		public bool enabled;
		/// <summary>
		///  0x0 - Send sensor outputs to the internal middlewares only, 0x1 - Send this sensor outputs also to the host over the USB interface. 
		/// </summary>
		//[FieldOffset(8)]
		[MarshalAs (UnmanagedType.I1)]
		public bool outputEnabled;
		/// <summary>
		///  Supported frame per second for this profile 
		/// </summary>
		//[FieldOffset(16)]
		public byte fps;
		/// <summary>
		///  Zero based index of sensor with the same type within device 
		/// </summary>
		//[FieldOffset(24)]
		public byte sensorIndex;
		//[FieldOffset(32)]
		public RawProfile profile;

		public override string ToString ()
		{
			return string.Format ("VideoProfile:[ enabled:{0}, outputEnabled:{1}, fps:{2}, sensorIndex:{3}, profile:{4} ]", enabled, outputEnabled, fps, sensorIndex, profile);
		}
	}


	[StructLayout (LayoutKind.Sequential)]
	public struct ControllerAssociatedDevices
	{
		/// <summary>
		///  MAC address of controller 1, set to all zeros if controller is not setup
		/// </summary>
		//[FieldOffset(0)]
		[MarshalAs (UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] macAddress1;
		/// <summary>
		///  MAC address of controller 2, set to all zeros if controller is not setup
		/// </summary>
		//[FieldOffset(6)]
		[MarshalAs (UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] macAddress2;

		//public static readonly int Size = 12;

		public override string ToString ()
		{
			return string.Format ("ControllerAssociatedDevices:[ macAddress1:{0}, macAddress2:{1} ]", macAddress1, macAddress2);
		}
	}


	[System.Serializable]
	[StructLayout (LayoutKind.Sequential)]
	struct ControllerDiscoveryEventFrame
	{
		/// <summary>
		///  Frame integration timestamp, as measured in nanoseconds since device initialization
		/// </summary>
		public long timestamp;
		/// <summary>
		///  Frame arrival timestamp, as measured in nanoseconds since device initialization
		/// </summary>
		public long arrivalTimeStamp;
		/// <summary>
		///  Host correlated time stamp in nanoseconds
		/// </summary>
		public long systemTimestamp;

		/// <summary>
		///  Byte array of MAC address of discovered device
		/// </summary>
		[MarshalAs (UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] macAddress;
		/// <summary>
		///  Identifier of the controller manufacturer
		/// </summary>
		//[FieldOffset(30)]
		public ushort manufacturerId;
		/// <summary>
		///  vendor specific data copied from the controller advertisement
		/// </summary>
		//[FieldOffset(32)]
		public ushort vendorData;
		/// <summary>
		///  Controller BLE protocol version - only major part is active
		/// </summary>
		//[FieldOffset(36)]
		public Version protocol;
		/// <summary>
		///  Controller App version - only major, minor, build parts are active
		/// </summary>
		//[FieldOffset(52)]
		public Version app;
		/// <summary>
		///  Controller Soft Device version - only major part is active
		/// </summary>
		//[FieldOffset(68)]
		public Version softDevice;
		/// <summary>
		///  Controller Boot Loader version - only major, minor, build parts are active
		/// </summary>
		//[FieldOffset(84)]
		public Version bootLoader;

		//public static readonly int Size = 104;

		public override string ToString ()
		{
			return string.Format ("ControllerDiscoveryEventFrame:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, macAddress:{3}, manufacturerId:{4}, vendorData:{5}, protocol:{6}, app:{7}, softDevice:{8}, bootLoader:{9} ]", timestamp, arrivalTimeStamp, systemTimestamp, macAddress, manufacturerId, vendorData, protocol, app, softDevice, bootLoader);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct ControllerConnectedEventFrame
	{
		/// <summary>
		///  Frame integration timestamp, as measured in nanoseconds since device initialization
		/// </summary>
		//[FieldOffset(0)]
		public long timestamp;
		/// <summary>
		///  Frame arrival timestamp, as measured in nanoseconds since device initialization
		/// </summary>
		//[FieldOffset(8)]
		public long arrivalTimeStamp;
		/// <summary>
		///  Host correlated time stamp in nanoseconds
		/// </summary>
		//[FieldOffset(16)]
		public long systemTimestamp;
		/// <summary>
		///  Connection status: SUCCESS ▒ connection succeeded
		/// </summary>
		//[FieldOffset(24)]
		public Status status;
		/// <summary>
		///  Disconnected controller identifier (1 or 2)
		/// </summary>
		//[FieldOffset(28)]
		public byte controllerId;
		/// <summary>
		///  Identifier of the controller manufacturer
		/// </summary>
		//[FieldOffset(30)]
		public ushort manufacturerId;
		/// <summary>
		///  Controller BLE protocol version - only major part is active
		/// </summary>
		//[FieldOffset(32)]
		public Version protocol;
		/// <summary>
		///  Controller App version - only major, minor, build parts are active
		/// </summary>
		//[FieldOffset(48)]
		public Version app;
		/// <summary>
		///  Controller Soft Device version - only major part is active
		/// </summary>
		//[FieldOffset(64)]
		public Version softDevice;
		/// <summary>
		///  Controller Boot Loader version - only major, minor, build parts are active
		/// </summary>
		//[FieldOffset(80)]
		public Version bootLoader;

		//public static readonly int Size = 96;

		public override string ToString ()
		{
			return string.Format ("ControllerConnectedEventFrame:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, status:{3}, controllerId:{4}, manufacturerId:{5}, protocol:{6}, app:{7}, softDevice:{8}, bootLoader:{9} ]", timestamp, arrivalTimeStamp, systemTimestamp, status, controllerId, manufacturerId, protocol, app, softDevice, bootLoader);
		}
	}

	[System.Serializable]
	[StructLayout (LayoutKind.Sequential)]
	struct ControllerDisconnectedEventFrame
	{
		/// <summary>                                                                                                                    
		///  Frame integration timestamp, as measured in nanoseconds since device initialization                                         
		/// </summary>                                                                                                                   
		public long timestamp;
		/// <summary>                                                                                                                    
		///  Frame arrival timestamp, as measured in nanoseconds since device initialization                                             
		/// </summary>                                                                                                                   
		public long arrivalTimeStamp;
		/// <summary>                                                                                                                    
		///  Host correlated time stamp in nanoseconds                                                                                   
		/// </summary>                                                                                                                   
		public long systemTimestamp;
		/// <summary>                                                                                                                    
		///  Disconnected controller identifier (1 or 2)                                                                                 
		/// </summary>                                                                                                                   
		public byte controllerId;

		public override string ToString ()
		{
			return string.Format ("ControllerDisconnectedEventFrame:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, controllerId:{3} ]", timestamp, arrivalTimeStamp, systemTimestamp, controllerId);
		}
	}
	#endregion

	[SuppressUnmanagedCodeSecurity]
	static class NativeMethods
	{
		#if UNITY_STANDALONE || UNITY_EDITOR
		const string TM_WRAPPER = "libtm_unity_wrapper";
		#else
        const string TM_WRAPPER = "libtm_unity_wrapper.dll";
		#endif
		//const string TM_WRAPPER = @"D:\libtm_unity\native\build\Debug\libtm_unity_wrapper.dll";

		[DllImport (TM_WRAPPER)]
		internal static extern IntPtr TrackingManager_CreateInstance (WindowsTrackingManager.Listener.Callbacks l);

		[DllImport (TM_WRAPPER)]
		internal static extern void TrackingManager_Destroy (IntPtr tm);

		[DllImport (TM_WRAPPER)]
		internal static extern ulong TrackingManager_Version (IntPtr tm);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_Start (IntPtr device, WindowsTrackingManager.TrackingDevice.Listener listener);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_Stop (IntPtr device);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_Reset (IntPtr device);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_Close (IntPtr device);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_ControllerConnect (IntPtr device, byte[] macAddress, ushort timeout, out byte controllerId);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_ControllerDisconnect (IntPtr device, int controllerId);

		[DllImport (TM_WRAPPER)]
		internal static extern void TrackingDevice_EnableTracking (IntPtr device);

		[UnmanagedFunctionPointer (CallingConvention.Cdecl)]
		public delegate void GetRawStreamsCallback (IntPtr profiles,int size);

		[DllImport (TM_WRAPPER)]
		internal static extern void TrackingDevice_GetSupportedRawStreams (IntPtr device, GetRawStreamsCallback cb);
		//        out IntPtr profiles, out int size);
		//[Out, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 2)] out StreamProfile[] profiles, out int size);

		[DllImport (TM_WRAPPER)]
		internal static extern void TrackingDevice_EnableRawStreams (IntPtr device,
		                                                             [In][MarshalAs (UnmanagedType.LPArray, SizeParamIndex = 2)] StreamProfile[] profiles, int size);

		[DllImport (TM_WRAPPER)]
		internal static extern void TrackingDevice_EnableControllers (IntPtr device, int numControllers);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_GetDeviceInfo (IntPtr device, [In, Out] ref DeviceInfo info);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_GetTemperature (IntPtr device, [In, Out] ref Temperature temperature);

		[DllImport (TM_WRAPPER)]
		internal static extern Status TrackingDevice_GetAssociatedDevices (IntPtr device, out ControllerAssociatedDevices devices);

		[DllImport (TM_WRAPPER)]
		internal static extern  Status TrackingDevice_SetAssociatedDevices (IntPtr device, ControllerAssociatedDevices devices);

	}
}