﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CutsceneManager : MonoBehaviour 
{

	public bool playCutscene = true; // Used to disable the cutscene when testing
[	SerializeField] MusicManager music;

	// Use this for initialization
	void Awake () 
	{
		if(playCutscene) StartCoroutine ("Cutscene");
	}
	
	IEnumerator Cutscene()
	{
		yield return new WaitForSeconds(9f);
		music.PlayMusic (0);
	}
}