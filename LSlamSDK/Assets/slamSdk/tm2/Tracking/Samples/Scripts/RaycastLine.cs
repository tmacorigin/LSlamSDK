using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class RaycastLine : MonoBehaviour
{
	public Transform endpoint;

	LineRenderer line;
	bool lastHit = false;
	Transform m_transform;

	void Start ()
	{
		line = GetComponent<LineRenderer> ();
		m_transform = GetComponent<Transform> ();
	}

	void Update ()
	{
		RaycastHit hit;
		bool r;
		if (r = Physics.Raycast (m_transform.position, m_transform.up, out hit)) {
			line.SetPosition (1, m_transform.InverseTransformPoint (hit.point));
			endpoint.gameObject.SetActive (true);

			endpoint.position = hit.point;
			endpoint.rotation = Quaternion.LookRotation (hit.normal);

		} else {
			if (lastHit) {
				line.SetPosition (1, Vector3.up * 100f);
				endpoint.gameObject.SetActive (false);
			}
		}

		lastHit = r;
	}
}
