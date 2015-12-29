﻿/*
    2015-2016 Team Pyrolite
    Project "Sky Base"
    Authors: Marc Steene
    Description: Smooth camera movement
*/

using UnityEngine;
using System.Collections;

public class SmoothCamera : MonoBehaviour 
{

GameObject parent;
[SerializeField] float damping;

	// Use this for initialization
	void Start () 
	{
		parent = GameObject.Find("PlayerShip(Clone)");
		//transform.parent = null; // unlink from parent
	}
	
	// Update is called once per frame
	void LateUpdate () 
	{
		transform.position = parent.transform.position;
		Quaternion rotation = parent.transform.rotation;
		transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * damping);
	}
}
