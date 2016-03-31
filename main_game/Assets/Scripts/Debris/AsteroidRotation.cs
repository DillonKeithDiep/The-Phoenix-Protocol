﻿/*
    Causes client-side rotation of asteroid based on speed sent from server
*/

using UnityEngine;
using System.Collections;

public class AsteroidRotation : MonoBehaviour
{
	private GameSettings settings;

	// Configuration parameters loaded through GameSettings
	private float maxRenderDistance;

	private float speed, distance;
	private GameObject player;

	#pragma warning disable 0649 // Disable warnings about unset private SerializeFields	
	[SerializeField] private Mesh highPoly;
	[SerializeField] private Mesh medPoly;
	[SerializeField] private Mesh lowPoly;
	#pragma warning restore 0649

	private MeshFilter myFilter;
    private ObjectPoolManager poolManager;
	private new Renderer renderer;
	private AsteroidSpawner spawner;
    private bool rotateEnabled = true;
    private bool currentStatus, oldStatus;
    private float waitTime;

	private bool coroutineRunning;

  // Only one packet needs to be sent to the client to control the asteroid's rotation
	void Start ()
	{
		player   = GameObject.Find("PlayerShip(Clone)");
        myFilter = GetComponent<MeshFilter>();
		renderer = GetComponent<Renderer>();
        waitTime = Random.Range(0.75f, 1.5f);

		GameObject spawnerObj = GameObject.Find("Spawner");
		if (spawnerObj != null)
			spawner  = spawnerObj.GetComponent<AsteroidSpawner>();

		settings = GameObject.Find("GameSettings").GetComponent<GameSettings>();
		LoadSettings();

		/*StartCoroutine(AsteroidLOD());
		coroutineRunning = true;*/
	}

	private void LoadSettings()
	{
		maxRenderDistance = settings.AsteroidMaxDistance;
	}

    public void SetSpeed(float tempSpeed)
    {
        if(poolManager == null)
            poolManager = GameObject.Find("AsteroidManager").GetComponent<ObjectPoolManager>();
        poolManager.SetAsteroidSpeed(gameObject.name, tempSpeed);
        speed = tempSpeed;

		coroutineRunning = false;
    }

    public void SetClientSpeed(float tempSpeed)
    {
        speed = tempSpeed;

		coroutineRunning = false;
    }
	
	void Update()
	{
        if(rotateEnabled)
		    transform.Rotate(transform.forward * speed * Time.deltaTime);

		// Because coroutines are stopped when the object becomes inactive, we need a way to restart them once it becomes active again
		// We know it will have its speed set, so we can start the coroutines on the next frame
		if (!coroutineRunning)
		{
			StartCoroutine(AsteroidLOD());
			coroutineRunning = true;
		}
	}

	IEnumerator AsteroidLOD()
	{
		distance = Vector3.Distance(transform.position, player.transform.position);
		if(distance < 300)
        {
		    myFilter.mesh = highPoly;
            rotateEnabled = true;
            renderer.enabled = true;
        }
		else if(distance < 600)
        {
		    myFilter.mesh = medPoly;
            rotateEnabled = true;
            renderer.enabled = true;
        }
		else if(distance < maxRenderDistance)
        {
            myFilter.mesh = lowPoly;
            rotateEnabled = false;
            renderer.enabled = true;
        }
        else
        {
			// When the asteroid goes out of range, destroy it
			AsteroidLogic logic = this.gameObject.GetComponentInChildren<AsteroidLogic>();
			if (logic != null)
			{
				if (spawner == null)
					Debug.LogWarning("Spawner is null when logic is not. This shouldn't happen.");
				
				coroutineRunning = false;
				spawner.OnAsteroidOutOfView();
				logic.Despawn();
			}
        }

		yield return new WaitForSeconds(waitTime);
		StartCoroutine(AsteroidLOD());
	}
}
