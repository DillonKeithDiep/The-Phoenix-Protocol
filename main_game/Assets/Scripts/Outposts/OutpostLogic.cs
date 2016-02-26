﻿using UnityEngine;
using System.Collections;

public class OutpostLogic : MonoBehaviour {

	private GameState gameState;

	private const int MIN_OUTPOST_RESOURCES = 100;
	private const int MAX_OUTPOST_RESOURCES = 500;

	private bool resourcesCollected = false;
	//private bool civiliansCollected = false;

	private int numberOfResources;
	//private int numberOfCivilians; 

	// Use this for initialization
	void Start () {
		// Set the number of resources for this outpost to be between the min and max value.
		numberOfResources = Random.Range(MIN_OUTPOST_RESOURCES, MAX_OUTPOST_RESOURCES);
	}

	// Update is called once per frame
	void Update () {

	}
		
	public void collision ()
	{
		if(!resourcesCollected) 
		{
			CollectResources();
			resourcesCollected = true;
		}
	}

	public void SetStateReference(GameState state)
	{
		gameState = state;
	}


	public void CollectResources() {
		// Add ship resources to the game state
		gameState.AddShipResources(numberOfResources);
		Debug.Log("Collected Resources from outpost");
	}



}


