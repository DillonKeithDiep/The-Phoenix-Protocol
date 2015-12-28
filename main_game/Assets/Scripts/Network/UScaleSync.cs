﻿/*
    2015-2016 Team Pyrolite
    Project "Sky Base"
    Authors: Marc Steene
    Description: Synchronise object scale when spawned on the server
*/

using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class UScaleSync : NetworkBehaviour
{

  [SyncVar] Vector3 scale;

  void Start ()
  {
      if(isServer)
       {
            scale = gameObject.transform.localScale;
       }
       else if (isClient)
       {
            gameObject.transform.localScale = scale;
       }
  }
}
