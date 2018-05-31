using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ToggleEvent : MonoBehaviour {

	[System.Serializable]
	public class BooleanEvent : UnityEvent<bool> {}

	public bool isOn;

	public BooleanEvent onValueChanged;

	public void Toggle () {
		onValueChanged.Invoke (isOn ^= true);
	}
}
