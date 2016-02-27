﻿/*
    2015-2016 Team Pyrolite
    Project "Sky Base"
    Authors: Dillon Keith Diep, Andrei Poenaru, Marc Steene
    Description: Server-side logic for enemy spawner

	Relevant Documentation:
	  * Enemy AI:    https://bitbucket.org/pyrolite/game/wiki/Enemy%20AI
      * Enemy Types: https://bitbucket.org/pyrolite/game/wiki/Enemy%20Types
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
	#pragma warning disable 0649 // Disable warnings about unset private SerializeFields
	[SerializeField] private GameObject enemy;
	[SerializeField] private float minDistance;
	[SerializeField] private float maxDistance;
	[SerializeField] private GameObject gameManager;
	#pragma warning restore 0649

	public static int numEnemies = 0; // Number of currently active enemies
	public int maxEnemies;            // Maximum number of enemies at a time
    
	private GameState state;
	private GameObject player, spawnLocation;

	// TODO: the AI constants probably need to be tweaked
	private const int AI_WAYPOINTS_PER_ENEMY      = 10;
	private const int AI_GEN_WAYPOINTS_FACTOR     = 8;
	private const int AI_WAYPOINT_RADIUS          = 100;
	private const float AI_WAYPOINT_WIDTH_SCALE   = 1.5f;
	private const float AI_WAYPOINT_HEIGHT_SCALE  = 0.5f;
	private readonly Vector3 AI_WAYPOINT_SHIFT    = new Vector3 (0, 15, 30);
	private List<GameObject> aiWaypoints;

	private List<GameObject> playerShipTargets = null;

	private static List<EnemyProperties> enemyTypeList = null;

    private ObjectPoolManager logicManager;
    private ObjectPoolManager enemyManager;
	private Queue<OutpostSpawnRequest> outpostSpawnRequests = null;
	private const int OUTPOST_SPAWN_RADIUS                  = 150; // The radius of the sphere around an outpost in which to spawn protecting enemies

    void Start()
    {
        if (gameManager != null)
        {
            state = gameManager.GetComponent<GameState>();
        }

        player = null;
        enemyManager = GameObject.Find("EnemyManager").GetComponent<ObjectPoolManager>();
        logicManager = GameObject.Find("EnemyLogicManager").GetComponent<ObjectPoolManager>();
        spawnLocation = new GameObject(); // Create temporary object to spawn enemies at
        spawnLocation.name = "EnemySpawnLocation";

        StartCoroutine("Cleanup");

		if (enemyTypeList == null)
			InitialiseEnemyTypes ();

		outpostSpawnRequests = new Queue<OutpostSpawnRequest>();
    }

	// Create an EnemyProperties object for each type of enemy that will be used
	private static void InitialiseEnemyTypes()
	{
		enemyTypeList = new List<EnemyProperties>();
		enemyTypeList.Add(new EnemyProperties(EnemyType.Fighter, 100, 0, 15, 15)); // This is the "default" enemy we had before introducing types
		enemyTypeList.Add(new EnemyProperties(EnemyType.Tank, 200, 50, 50, 10));
		enemyTypeList.Add(new EnemyProperties(EnemyType.Assassin, 50, 20, 5, 30));
	}

    // Spawn a new enemy in a random position if less than specified by maxEnemies
    void Update ()
	{
        if (state.Status == GameState.GameStatus.Started)
        {
            if(player == null)
            {
                player = state.PlayerShip;

				Transform playerSpaceshipModel = player.transform.Find ("Model").Find ("Spaceship");
				CreateAIWaypoints(playerSpaceshipModel);
				GetPlayerShipTargets(playerSpaceshipModel);
            }

            // First, spawn regular enemies. Then, spawn enemies around outposts, if needed
			if (numEnemies < maxEnemies)
            {
				SpawnEnemy();
            }
			else if (outpostSpawnRequests.Count > 0)
			{
				OutpostSpawnRequest req = outpostSpawnRequests.Dequeue();
				for (int i = 0; i < req.NumEnemies; i++)
					SpawnWaitingEnemy(req.Location);
			}
        }
	}
		
	private void InstantiateEnemy(out GameObject enemyObject, out EnemyLogic enemyLogic)
	{
		// Spawn enemy and server logic
		enemyObject = enemyManager.RequestObject();
        GameObject enemyLogicObject = logicManager.RequestObject();
        enemyLogicObject.transform.parent = enemyObject.transform;
        enemyLogicObject.transform.localPosition = Vector3.zero;
		enemyObject.transform.position = spawnLocation.transform.position;

		// Set up enemy with components, spawn on network      
		enemyLogic = enemyLogicObject.GetComponent<EnemyLogic> ();
		ApplyEnemyType (enemyLogic, Random.Range(0, enemyTypeList.Count)); // random enemy type
		enemyLogic.SetControlObject(enemyObject);
		enemyLogic.SetPlayer(state.PlayerShip);
		enemyLogic.SetPlayerShipTargets(playerShipTargets);
		enemyLogic.SetAIWaypoints(GetAIWaypointsForEnemy ());

		enemyObject.transform.eulerAngles = new Vector3(-90, 0, 0); // Set to correct rotation
		enemyManager.EnableClientObject(enemyObject.name, enemyObject.transform.position, enemyObject.transform.rotation, enemyObject.transform.localScale);
		numEnemies += 1;
		state.AddToEnemyList(enemyObject);
	}

	// Spawn an enemy with the default settings
	private void SpawnEnemy()
	{
		// Set spawn position based on input attributes
		spawnLocation.transform.position = player.transform.position;
		spawnLocation.transform.rotation = Random.rotation;
		spawnLocation.transform.Translate(transform.forward * Random.Range(minDistance,maxDistance));

		GameObject enemy; 
		EnemyLogic logic;
		InstantiateEnemy(out enemy, out logic);
	}

	// Spawn an enemy waiting at a location
	private void SpawnWaitingEnemy(Vector3 location)
	{
		spawnLocation.transform.position = location + Random.insideUnitSphere * OUTPOST_SPAWN_RADIUS;
		spawnLocation.transform.rotation = Random.rotation;

		GameObject enemy; 
		EnemyLogic logic;
		InstantiateEnemy(out enemy, out logic);

		logic.SetGuarding(location);
	}

	// Generate a list of waypoints around the player to guide the enemy ships
	// Each enemy spawned will get some waypoints from this list
	private void CreateAIWaypoints(Transform spaceshipModel)
	{
		int n_waypoints = maxEnemies * AI_GEN_WAYPOINTS_FACTOR;
		aiWaypoints = new List<GameObject> (n_waypoints);

		// Get the bounds of all (important) ship parts
		List<Bounds> bounds = new List<Bounds> ();
		foreach (Transform child in spaceshipModel)
		{
			GameObject gameObject = child.gameObject;
			if (gameObject.name.Contains ("Engine") || gameObject.name.Contains ("Hull") || gameObject.name.Equals ("CaptainBridge"))
				bounds.Add (gameObject.GetComponent<Renderer> ().bounds);
		}

		GameObject waypoint;
		bool intersects;

		for (int i = 0; i < n_waypoints; i++)
		{
			// Randomly generate a waypoint around the player, but discard it if it's inside the ship
			do
			{
				// Uncomment this to see waypoints as spheres
				/*if (Debug.isDebugBuild)
			    {
					waypoint = GameObject.CreatePrimitive (PrimitiveType.Sphere);
					//waypoint.transform.localScale = waypoint.transform.localScale / 2;
					waypoint.GetComponent<Renderer>().material.color = Color.red;
			    }
				else*/
				waypoint = new GameObject ("AIWaypoint");
				Vector3 pos = Random.insideUnitSphere * AI_WAYPOINT_RADIUS;
				pos.x *= AI_WAYPOINT_WIDTH_SCALE; // Widen the sphere on the horizontal axis
				pos.y *= AI_WAYPOINT_HEIGHT_SCALE; // Squash the sphere on the vertical axis

				waypoint.transform.position = pos;
				waypoint.transform.Translate (AI_WAYPOINT_SHIFT); // Shift the waypoints a upwards and forwards a little, to keep enemies on sight more
				waypoint.transform.parent = player.transform;

				// Check if the waypoint intersects any of the the player ship parts
				intersects = false;
				for (int j = 0; j < bounds.Count && !intersects; j++)
					if (bounds[j].Contains(waypoint.transform.position))
						intersects = true;
			} while(intersects);

			aiWaypoints.Add (waypoint);
		}
	}

	// Get a subset of waypoints to hand out to a particular enemy
	private List<GameObject> GetAIWaypointsForEnemy()
	{
		List<GameObject> waypoints = new List<GameObject> (AI_WAYPOINTS_PER_ENEMY);
		int n_waypoints = aiWaypoints.Count, r;

		for (int i = 0; i < AI_WAYPOINTS_PER_ENEMY; i++)
		{
			// Make sure we don't hand out the same waypoint more than once
			do
			{
				r = Random.Range (0, n_waypoints);
			} while(waypoints.Contains(aiWaypoints[r]));

			waypoints.Add (aiWaypoints [r]);
		}

		return waypoints;
	}

	// Build a list of targets on the player's ship
	// The enemies will use these to shoot at the player
	private void GetPlayerShipTargets(Transform spaceshipModel)
	{
		playerShipTargets = new List<GameObject>();

		foreach (Transform child in spaceshipModel)
		{
			GameObject gameObject = child.gameObject;
			if (gameObject.name.Contains ("Engine") || gameObject.name.Contains ("Hull") || gameObject.name.Equals ("CaptainBridge"))
				playerShipTargets.Add(gameObject);
		}
	}

	// Remove destroyed enemies from Game State
    IEnumerator Cleanup()
    {
        yield return new WaitForSeconds(1f);

        if (state.Status == GameState.GameStatus.Started)
			state.CleanupEnemies();

        StartCoroutine("Cleanup");
    }

	// This class holds the various atributes of an enemy. Each enemy type will be be represented by a separate instance
	// TODO: the enemies should look differently based on their type
	private class EnemyProperties
	{
		public int maxHealth, maxShield, collisionDamage, speed;
		public EnemyType Type { get; private set; }

		public EnemyProperties(EnemyType type, int maxHealth, int maxShield, int collisionDamage, int speed)
		{
			this.Type            = type;
			this.maxHealth       = maxHealth;
			this.maxShield       = maxShield;
			this.collisionDamage = collisionDamage;
			this.speed           = speed;
		}
	}

	// Apply properties to an enemy object, i.e. make it be of certain type
	private static void ApplyEnemyType (EnemyLogic enemy, EnemyType type)
	{
		EnemyProperties props = GetPropertiesOfType (type);
		ApplyEnemyType (enemy, props);
	}

	private static void ApplyEnemyType (EnemyLogic enemy, int index)
	{
		ApplyEnemyType (enemy, enemyTypeList [index]);
	}

	private static void ApplyEnemyType (EnemyLogic enemy, EnemyProperties props)
	{
		enemy.maxHealth       = props.maxHealth;
		enemy.health          = props.maxHealth;
		enemy.maxShield       = props.maxShield;
		enemy.speed           = props.speed;
		enemy.collisionDamage = props.collisionDamage;
		enemy.type            = props.Type;
	}

	private static EnemyProperties GetPropertiesOfType(EnemyType type)
	{
		foreach (EnemyProperties props in enemyTypeList)
		{
			if (props.Type == type)
				return props;
		}

		// If our code is correct, this should never happen
		Debug.LogError("Tried to spawn invalid enemy type: " + type.ToString());
		return null;
	}

	public class OutpostSpawnRequest
	{
		public int NumEnemies { get; private set; }
		public Vector3 Location { get; private set; }

		public OutpostSpawnRequest(int numEnemies, Vector3 location)
		{
			this.NumEnemies = numEnemies;
			this.Location   = location;
		}
	}

	// Request spawning of count enemies around outpostLocation
	public void RequestSpawnForOutpost (int count, Vector3 outpostLocation)
	{
		// Only register the request here. It will be spawned on the next frame after all regular enemies are spawned.
		outpostSpawnRequests.Enqueue(new OutpostSpawnRequest(count, outpostLocation));
	}
}

// The types of enemies available. Each type should have its properties initialised before it's used
public enum EnemyType
{
	Fighter,
	Tank,
	Assassin
}
