using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

namespace Intel.RealSense.Tracking
{
	public enum DeviceIndex : byte
	{
		HMD = 0x0,
		CONTROLLER1 = 0x1,
		CONTROLLER2 = 0x2,
	}

	public enum Confidence : uint
	{
		FAILED,
		LOW,
		MEDIUM,
		HIGH
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct Pose
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
		///  Index of HMD or controller - 0x0 = HMD, 0x1 - controller 1, 0x2 - controller 2
		/// </summary>
		public DeviceIndex sourceIndex;
		/// <summary>
		///  X, Y, Z values of translation, in meters (relative to initial position)
		/// </summary>
		public Vector3 translation;
		/// <summary>
		///  X, Y, Z values of velocity, in meter/sec
		/// </summary>
		public Vector3 velocity;
		/// <summary>
		///  X, Y, Z values of acceleration, in meter/sec^2
		/// </summary>
		public Vector3 acceleration;
		/// <summary>
		///  Qi, Qj, Qk, Qr components of rotation as represented in quaternion rotation (relative to initial position)
		/// </summary>
		public Quaternion rotation;
		/// <summary>
		///  Yaw, Pitch, Roll values of velocity, in radians/sec
		/// </summary>
		public Vector3 angularVelocity;
		/// <summary>
		///  Yaw, Pitch, Roll values of acceleration, in radians/sec^2
		/// </summary>
		public Vector3 angularAcceleration;
		/// <summary>
		///  pose data confidence 0x0 - Failed, 0x1 - Low, 0x2 - Medium, 0x3 - High
		/// </summary>
		public Confidence trackerConfidence;
		/// <summary>
		///  pose data confidence 0x0 - Failed, 0x1 - Low, 0x2 - Medium, 0x3 - High
		/// </summary>
		public Confidence mapperConfidence;

		public override string ToString ()
		{
			return string.Format ("Pose:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, sourceIndex:{3}, translation:{4}, velocity:{5}, acceleration:{6}, rotation:{7}, angularVelocity:{8}, angularAcceleration:{9}, confidence:{10} ]", timestamp, arrivalTimeStamp, systemTimestamp, sourceIndex, translation, velocity, acceleration, rotation, angularVelocity, angularAcceleration, trackerConfidence);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct SlamFeature
	{
		public int id;
		public Vector3 worldCoordinate;
		public Vector3 imageCoordinate;

		public override string ToString ()
		{
			return string.Format ("SlamFeature:[ id:{0}, worldCoordinate:{1}, imageCoordinate:{2} ]", id, worldCoordinate, imageCoordinate);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct ControllerEvent
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
		/// Zero based index of sensor with the same type within device
		/// </summary>
		public byte sensorIndex;

		/// <summary>
		/// A running index of frames from every unique sensor, starting from 0
		/// </summary>
		public uint frameId;

		/// <summary>
		/// Event ID – button, trackpad or battery (vendor specific), supported values 0-63
		/// </summary>
		public byte eventId;

		/// <summary>
		/// Instance of the sensor in case of multiple sensors
		/// </summary>
		public byte instanceId;

		/// <summary>
		/// Sensor data that is pass-through from the controller firmware
		/// </summary>
		[MarshalAs (UnmanagedType.ByValArray, SizeConst = 6)]
		public byte[] sensorData;

		public override string ToString ()
		{
			return string.Format ("ControllerEvent:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, sensorIndex:{3}, frameId:{4}, eventId:{5}, instanceId:{6}, sensorData:[{7}] ]", timestamp, arrivalTimeStamp, systemTimestamp, sensorIndex, frameId, eventId, instanceId, string.Join (",", System.Array.ConvertAll (sensorData, s => s.ToString ())));
		}
	}

	public enum TemperatureSensor
	{
		/// <summary>
		///  Temperature Sensor located in the Vision Processing Unit
		/// </summary>
		VPU,
		/// <summary>
		///  Temperature Sensor located in the Inertial Measurement Unit
		/// </summary>
		IMU,
		/// <summary>
		///  Temperature Sensor located in the Bluetooth Low Energy Unit
		/// </summary>
		BLE,
	}

	[System.Serializable]
	[StructLayout (LayoutKind.Sequential)]                                                                                                    
	public struct SensorTemperature
	{
		/// <summary>                                                                                                                    
		///  Temperature sensor index: 0x0 - VPU, 0x1 - IMU, 0x2 - BLE                                                                   
		/// </summary>                                                                                                                   
		public TemperatureSensor index;
		/// <summary>                                                                                                                    
		///  Sensor temperature (Celsius)                                                                                                
		/// </summary>                                                                                                                   
		public float temperature;
		/// <summary>                                                                                                                    
		///  Sensor temperature threshold (Celsius)                                                                                      
		/// </summary>                                                                                                                   
		public float threshold;

		public override string ToString ()
		{                                                                                                                                
			return string.Format ("SensorTemperature:[ index:{0}, temperature:{1}, threshold:{2} ]", index, temperature, threshold); 
		}
	}


	public enum TrackingError
	{
		NO_ERROR = 0,

		UNKNOWN = 0x1,
		INVALID_PARAMETER = 0x3,
		INTERNAL_ERROR = 0x4,
		UNSUPPORTED_OPERATION = 0x5,
		LIST_TOO_BIG = 0x6,
		MORE_DATA_AVAILABLE = 0x7,
		DEVICE_BUSY = 0x8,
		TIMEOUT = 0x9,
		TABLE_NOT_EXIST = 0xA,
		TABLE_LOCKED = 0xB,
		DEVICE_STOPPED = 0xC,
		TEMPERATURE_WARNING = 0x10,
		//The device temperature reached 10% from its threshold.
		TEMPERATURE_STOP = 0x11,
		//The device temperature reached its threshold, and the device stopped tracking.
		CRC_ERROR = 0x12,
		CONTROLLER_ERROR = 0xA000,
		SLAM_NO_DICTIONARY = 0x9001,

		//HOST specific at 1000 offset
		NO_USB_PERMISSION = 0x1001,
		LOAD_FIRMWARE_FAILED = 0x1002,
		DEVICE_IN_ERROR_STATE = 0x1003,
		USB_TRANSFER_FAILED = 0x1004,
		USB_DEVICE_DETACHED = 0x1005,
		NO_CALIBRATION_DATA = 0x1006
	}

	public interface ITrackingManager
	{
		string Version { get; }

		bool IsDeviceConnected { get; }

		void Dispose ();

		event System.Action<ITrackingDevice> onTrackingDeviceAvailable;
		event System.Action onTrackingDeviceUnavailable;
	}

	public interface IPoseListener
	{
		event System.Action<Pose> onPose;

		Pose GetPose ();

		bool TryGetPose (ref Pose pose);
	}

	public interface IControllerInfo {
		int VendorData { get; }
	}

	public interface IControllerDevice : IPoseListener
	{
		int Connect ();

		void Disconnect ();

		byte[] MacAddress { get; }

		byte Id { get; }

		IControllerInfo Info { get; }

		event System.Action<ControllerEvent> onControllerEvent;

		event System.Action<int> onConnect;

		event System.Action<int> onDisconnect;
	}

	[System.Serializable]
	[StructLayout (LayoutKind.Sequential)]
	public struct StreamProfile
	{
		public SensorType sensorType;
		public byte sensorIndex;
		public ushort fps;
		public ushort width;
		public ushort height;
		public byte pixelFormat;
		public ushort stride;

		public override string ToString ()
		{
			return string.Format ("StreamProfile:[ sensorType:{0}, sensorIndex:{1}, fps:{2}, width:{3}, height:{4}, pixelFormat:{5}, stride:{6} ]", sensorType, sensorIndex, fps, width, height, pixelFormat, stride);
		}
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct VideoFrame
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
		///  Zero based index of sensor with the same type within device
		/// </summary>
		//[FieldOffset(24)]
		public byte sensorIndex;
		/// <summary>
		///  Running index of frames from every unique sensor. Starting from 0.
		/// </summary>
		//[FieldOffset(28)]
		public uint frameId;
		/// <summary>
		///  Frame format profile - includes width, height, stride, pixelFormat
		/// </summary>
		//[FieldOffset(32)]
		public RawProfile profile;
		/// <summary>
		///  Exposure time of this frame in microseconds
		/// </summary>
		//[FieldOffset(44)]
		public uint exposuretime;
		/// <summary>
		///  Length of frame below, in bytes, shall be equal to Stride X Height X BPP
		/// </summary>
		//[FieldOffset(48)]
		public uint frameLength;
		/// <summary>
		///  Frame data pointer
		/// </summary>
		//[FieldOffset(52)]
		public System.IntPtr data;

		public override string ToString ()
		{
			return string.Format ("VideoFrame:[ timestamp:{0}, arrivalTimeStamp:{1}, systemTimestamp:{2}, sensorIndex:{3}, frameId:{4}, profile:{5}, exposuretime:{6}, frameLength:{7}, data:{8} ]", timestamp, arrivalTimeStamp, systemTimestamp, sensorIndex, frameId, profile, exposuretime, frameLength, data);
		}
	}

	public interface ITrackingDevice : IPoseListener
	{
		void EnableControllers (int numberOfControllers);

		void EnableTracking ();

		StreamProfile[] GetSupportedRawStreams ();

		void EnableRawStreams (StreamProfile[] profiles);

		void Start ();

		void Stop ();

		void Close ();

		void Reset ();

		List<byte[]> readAssociatedDevices ();

		void writeAssociatedDevices (byte[] mac1, byte[] mac2);

		//        void ConnectController(byte[] macAddress, int controllerId);

		event System.Action<IControllerDevice> onControllerDiscovery;
		//event System.Action<IControllerDevice> onControllerDisconnect;

		event System.Action<Vector3> onGyro;
		//		event System.Action<Vector3> onAccelerometer;
		event System.Action<VideoFrame> onVideoFrame;

		string FirmwareVersion { get; }

		SensorTemperature[] Temperature { get; }
	}

}
