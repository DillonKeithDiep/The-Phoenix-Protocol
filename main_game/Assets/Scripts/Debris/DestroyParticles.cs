﻿/*
    2015-2016 Team Pyrolite
    Project "Sky Base"
    Authors: Marc Steene
    Description: Plays a sound then destroys particle effects upon completion
*/

using UnityEngine;
using System.Collections;
 
public class DestroyParticles : MonoBehaviour
{
    [SerializeField] AudioClip snd;
    AudioSource mySrc;

    private void Start()
    {
        if(snd != null)
        {
            mySrc = GetComponent<AudioSource>();
            mySrc.clip = snd;
            mySrc.Play();
        }

        Destroy(gameObject, 6f); // Destroys the object after 6 seconds
    }
}