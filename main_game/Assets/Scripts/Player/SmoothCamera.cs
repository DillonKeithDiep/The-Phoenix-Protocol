﻿using UnityEngine;
using System.Collections;

public class SmoothCamera : MonoBehaviour {

GameObject parent;
[SerializeField] float damping;

	// Use this for initialization
	void Start () 
	{
		parent = transform.parent.gameObject; // cache parent game object
		transform.parent = null; // unlink from parent
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		transform.position = parent.transform.position;
		Quaternion rotation = parent.transform.rotation;
		transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * damping);
	}
}