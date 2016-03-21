﻿/*
    2015-2016 Team Pyrolite
    Project "Sky Base"
    Authors: Marc Steene
    Description: Displays visual warnings when health is low and direction from which the player was hit
*/

using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class DamageEffects : NetworkBehaviour
{
	private VideoGlitches.VideoGlitchSpectrumOffset lowHealth;
	private float health, alpha;
	private int direction;

	#pragma warning disable 0649 // Disable warnings about unset private SerializeFields
	// Directional damage textures
	[SerializeField] private Texture2D left;
	[SerializeField] private Texture2D right;
	[SerializeField] private Texture2D up;
	[SerializeField] private Texture2D down;
	[SerializeField] private Texture2D topLeft;
	[SerializeField] private Texture2D topRight;
	[SerializeField] private Texture2D bottomLeft;
	[SerializeField] private Texture2D bottomRight;
	#pragma warning restore 0649
    
	void Start () 
	{
		lowHealth = GetComponent<VideoGlitches.VideoGlitchSpectrumOffset>();
    }

    // Fades out damage textures
	void Update () 
	{
		if(alpha > 0f)
			alpha -= 2f * Time.deltaTime;
	}

	void OnGUI()
	{
		GUI.color = new Color(1f,1f,1f,alpha); // Set texture alpha value

        // Texture to show depends on direction
		if(direction == 0) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), left, ScaleMode.StretchToFill);
		if(direction == 1) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), up, ScaleMode.StretchToFill);
		if(direction == 2) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), right, ScaleMode.StretchToFill);
		if(direction == 3) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), down, ScaleMode.StretchToFill);
		if(direction == 4) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), topLeft, ScaleMode.StretchToFill);
		if(direction == 5) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), topRight, ScaleMode.StretchToFill);
		if(direction == 6) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), bottomLeft, ScaleMode.StretchToFill);
		if(direction == 7) GUI.DrawTexture (new Rect (0, 0, Screen.width, Screen.height), bottomRight, ScaleMode.StretchToFill);
	}

	public void Damage(int dir, float damage, float hp)
	{
        health           = hp;
        lowHealth.amount = Mathf.Clamp(0.35f - ((float)health/100f),0f,0.35f);
		direction        = dir;
		alpha            = Mathf.Clamp(0.5f + (damage/20f),0f,1f);
	}

    public void DistortionEffect()
    {
        lowHealth.amount = 1.0f;
        StartCoroutine("ReduceEffect");
    }

    private IEnumerator ReduceEffect()
    {
        lowHealth.amount -= 0.01f;
        yield return new WaitForSeconds(0.1f);
        if(lowHealth.amount > 0)
            StartCoroutine("ReduceEffect"); 
    }
}


