using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Intel.RealSense.Tracking;
using UnityEngine.Events;
using System;

namespace Intel.RealSense.Tracking
{
	public class ControllerButtonEvent : MonoBehaviour
	{
		public enum ControllerId : byte
		{
			CONTROLLER1 = 1,
			CONTROLLER2 = 2,
		}

		public enum ControllerButton : byte
		{
			TRIGGER = 10,
			BUTTON1 = 1,
			BUTTON2 = 3,
			TOUCHPAD = 4,
		}

		public ControllerId controller;
		public ControllerButton button = ControllerButton.TRIGGER;

		public UnityEvent onPressed;
		public UnityEvent onReleased;

        private readonly Queue<Action> q = new Queue<Action>();

        IEnumerator Start ()
		{
			bool update = false;

			var tm = FindObjectOfType<TrackingManager> ();
			tm.manager.onTrackingDeviceAvailable += dev => {
//			Debug.Log ("onTrackingDeviceAvailable: " + dev);

				dev.onControllerDiscovery += (IControllerDevice ctrl) => {
//					Debug.Log ("onControllerDiscovery: " + ctrl);

					ctrl.onConnect += (int index) => {

						if (index != (byte)controller)
							return;

//						Debug.LogFormat ("onConnect: {0}, index={1}, controller={2}", ctrl, index, controller);

						ctrl.onControllerEvent += (ControllerEvent e) => {

							if (e.sensorIndex != (byte)controller)
								return;
					
							if (e.eventId == 0) {

								if ((byte)button == e.instanceId) {

//									Debug.LogFormat ("onControllerEvent: {0}, {1}", ctrl, e);

									if (e.sensorData [0] == 1) {
										q.Enqueue (onPressed.Invoke);
									} else {
										q.Enqueue (onReleased.Invoke);
									}

									update = true;
								}
							}
						};
					};
				};
			};

			while (true) {
				while (!update)
					yield return null;
				while (q.Count > 0) {
					q.Dequeue ().Invoke ();
				}
				update = false;
			}
		}

	}
}
