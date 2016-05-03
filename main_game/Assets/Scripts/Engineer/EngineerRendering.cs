﻿using UnityEngine;
using System.Collections;

public class EngineerRendering : MonoBehaviour 
{
	// Use this for initialization
	void Start () 
    {
        GameObject.Find("TargetCamera").SetActive(false);
        Camera.main.gameObject.name = "CommanderCamera";
        Camera.main.fov = 60;
        GameObject.Find("Shield(Clone)").GetComponent<MeshRenderer>().enabled = false;
	}
}
