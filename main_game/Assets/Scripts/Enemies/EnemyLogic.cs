﻿/*
    2015-2016 Team Pyrolite
    Project "Sky Base"
    Authors: Marc Steene, Andrei Poenaru
    Description: Control enemy ship attributes and AI
		
    Relevant Documentation:
	  * Enemy AI:    https://bitbucket.org/pyrolite/game/wiki/Enemy%20AI
      * Enemy Types: https://bitbucket.org/pyrolite/game/wiki/Enemy%20Types
*/

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyLogic : MonoBehaviour
{

	/* These fields are set when assigning a type to the enemy. They should not be initilaised manually.
	 * Please use the internal modifier for all of them to make it clear that they are set somewhere else in code,
	 * and to prevent them from showing up in the Unity Editor, which would cause confusion. */
	internal float speed;
	internal float maxHealth;
	internal float maxShield; // Max recharging shield level. Set to 0 to disable shields
	internal float collisionDamage;
	internal EnemyType type;

	[SerializeField] float shotsPerSec = 1f;
	[SerializeField] float shootPeriod; // How long in seconds the enemy should shoot for when it fires
	[SerializeField] int percentageVariation; // Percentage variation +/- in the length of the shooting period
	[SerializeField] float shieldDelay; // Delay in seconds to wait before recharging shield
	[SerializeField] float shieldRechargeRate; // Units of shield to increase per second
	[SerializeField] bool isSuicidal; // Attempt to crash into player?
	[SerializeField] GameObject bullet;
	[SerializeField] GameObject bulletLogic;
    [SerializeField] GameObject destroyEffect;
    [SerializeField] AudioClip fireSnd;
    [SerializeField] bool randomPitch;

    AudioSource mySrc;
	private ServerManager serverManager;
	private GameState gameState;
	public GameObject player;
	bool shoot = false, angleGoodForShooting = false;
	bool rechargeShield;
    public bool draw = false;
	internal float health;
	private float shield;
    public float distance;
	float lastShieldCheck; // Temp variable allows us to see whether I've taken damage since last checking
    float randomZ; // A random Z angle to give the enemies some uniqueness

	GameObject shootAnchor;
	Vector3 prevPos, currentPos;
	Renderer myRender;
	private GameObject controlObject;

	// This is the amount of resources dropped by the enemy when killed. It is calculated based on the enemy's max health and shield
	private int droppedResources;
	private const int DROP_RESOURCE_RANGE = 100;

	// The probability than the enemy goes suicidal, assumming a check per frame @ 60 fps for 10 minutes
	private const double MADNESS_PROB = 0.999999720824043; // 1% probability
	private System.Random sysRand;

	// The current state of the ship's AI
	internal EnemyAIState state;
	private const int ENGAGE_DISTANCE      = 400;

	// Waypoints are used to move around the player when close enough
	private List<GameObject> aiWaypoints;
	private GameObject currentWaypoint               = null;
	private const float AI_WAYPOINT_ROTATION_SPEED   = 1.3f;   // Turning speed when following waypoints
	private const float AI_WAYPOINT_REACHED_DISTANCE = 20f;   // Distance when a waypoint is considered reached
	private const float AI_SHOOT_MAX_ANGLE           = 50f;  // Maximum angle with the player when shooting is possible
	private float lastYRot;

	private List<GameObject> playerShipTargets;
	private GameObject currentTarget;

	// Parameters for raycasting obstacle detection
	// Two rays are shot forwards, on the left and right side of the ship to detect incoming obstacles
	private const int AI_OBSTACLE_RAY_FRONT_OFFSET = 15;
	private const int AI_OBSTACLE_RAY_FRONT_LENGTH = 200;
	private const string AI_OBSTACLE_TAG_DEBRIS    = "Debris";
	private const string AI_OBSTACLE_TAG_ENEMY     = "EnemyShip";
	private const int AI_OBSTACLE_AVOID_ROTATION   = 45;
	private int previousAvoidDirection             = 0;
	private int avoidDirection;


    private ObjectPoolManager bulletManager;
    private ObjectPoolManager logicManager;
    private ObjectPoolManager impactManager;
    private ObjectPoolManager explosionManager;
    private ObjectPoolManager enemyLogicManager;
    private ObjectPoolManager enemyManager;
    
	private Vector3 guardLocation = Vector3.zero; // If this enemy is an outpost guard, this will be set to a non-zero value
	private const int AI_GUARD_TURN_BACK_DISTANCE = 500; // The distance at which guards stop engaging the player and turn back to the outpost
	private const int AI_GUARD_PROTECT_DISTANCE   = 100; // The distance from the outpost at which to stop and wait when returning to guard

	void Start ()
	{
		GameObject server = GameObject.Find("GameManager");
		serverManager = server.GetComponent<ServerManager>();
		gameState = server.GetComponent<GameState>();

        bulletManager = GameObject.Find("EnemyBulletManager").GetComponent<ObjectPoolManager>();
        logicManager = GameObject.Find("EnemyBulletLogicManager").GetComponent<ObjectPoolManager>();
        impactManager = GameObject.Find("BulletImpactManager").GetComponent<ObjectPoolManager>();
        explosionManager = GameObject.Find("EnemyExplosionManager").GetComponent<ObjectPoolManager>();
        enemyLogicManager = GameObject.Find("EnemyLogicManager").GetComponent<ObjectPoolManager>();
        enemyManager = GameObject.Find("EnemyManager").GetComponent<ObjectPoolManager>();
	}

    void OnEnable()
    {
        // Decide the resource drop for this ship to be within DROP_RESOURCE_RANGE range of its max health + shield
        droppedResources = System.Convert.ToInt32(maxHealth + maxShield + Random.Range (0, DROP_RESOURCE_RANGE)); 
    }

    public void SetControlObject(GameObject newControlObject)
    {
        controlObject = newControlObject;
        transform.parent.gameObject.GetComponent<EnemyCollision>().collisionDamage = collisionDamage;
    }

    // This function is run when the object is spawned
    public void SetPlayer(GameObject temp)
	{
        mySrc = GetComponent<AudioSource>();
        mySrc.clip = fireSnd;
		player = temp;
		state = EnemyAIState.SeekPlayer;
		myRender = transform.parent.gameObject.GetComponent<Renderer>();
        controlObject.transform.eulerAngles = new Vector3(controlObject.transform.eulerAngles.x, controlObject.transform.eulerAngles.y, randomZ);
        randomZ = Random.Range(0f,359f);

        if(maxShield > 0)
		{
			shield = maxShield;
			lastShieldCheck = shield;
			StartCoroutine ("RechargeShields");
		}

        // Find location to spawn bullets at
        foreach(Transform child in this.transform.parent)
		{
			if(child.gameObject.name.Equals("ShootAnchor")) shootAnchor = child.gameObject;
		}

		StartCoroutine ("ShootManager");
		StartCoroutine("DrawDelay");

		// Get a System.Random object that will be used for madness checks
		sysRand = new System.Random();
	}

	// Set the waypoints to follow when engaging the player
	public void SetAIWaypoints(List<GameObject> waypoints)
	{
		aiWaypoints = waypoints;
	}

	// Set the parts of the player's ship that can be targeted
	public void SetPlayerShipTargets(List<GameObject> targets)
	{
		this.playerShipTargets = targets;
		currentTarget = targets[Random.Range(0, targets.Count)]; // For now, choose the target ship part at random
	}

	// Make this enemy guard location
	public void SetGuarding(Vector3 location)
	{
		state         = EnemyAIState.Wait;
		guardLocation = location;
	}

    // Avoids null reference during initial spawning
	IEnumerator DrawDelay()
	{
		yield return new WaitForSeconds(1f);
		draw = true;
	}

	void Update ()
	{
		// If this enemy has gone mad, its sole purpose is to crash into the player
		if (isSuicidal)
		{
			MoveTowardsPlayer ();
			return;
		}

		// Check to see if this enemy is going mad this frame
		/* Assuming a 10 minute game, there is a 1% chance that an enemy will go mad.
		 * It's pointless to add a parameter and do the calculations in engine, since it involves finding the 36000th root of the probability value.
		 * We use C#'s random instead of Unity's because we need double precision. */
		double madnessCheck = sysRand.NextDouble ();
		if (madnessCheck > MADNESS_PROB)
			isSuicidal = true;

		prevPos    = currentPos;
		currentPos = player.transform.position;
		distance   = Vector3.Distance(transform.position, player.transform.position);

		// Check if about to collide with something
		// Ignore the outpost if returning towards its location, because the guard distance might be smaller than the avoid distance.
		// We will not hit the outpost as long as the guard distance accounts for the the outpost's size
		AvoidInfo obstacleInfo = CheckObstacleAhead();
		if (!obstacleInfo.IsNone() && (state != EnemyAIState.ReturnToGuardLocation || !obstacleInfo.ObstacleTag.Equals("Outpost")))
		{
			// If already avoiding an obsctale or returning to an outpost, clear the previous waypoint before creating another one
			if (state == EnemyAIState.AvoidObstacle || state == EnemyAIState.ReturnToGuardLocation)
				Destroy(currentWaypoint);

			// If about to collide with an enemy, go towards a different waypoint - it's very likely the other guy will not go the same way
			// Otherwise, temporarily change direction
			if (obstacleInfo.ObstacleTag.Equals(AI_OBSTACLE_TAG_ENEMY))
			{
				state                  = EnemyAIState.EngagePlayer;
				previousAvoidDirection = 0;
				currentWaypoint        = GetNextWaypoint();
			}
			else
			{
				state = EnemyAIState.AvoidObstacle;

				// If already avoiding an obstacle, keep the same direction
				// Otherwise, decided based on the side the obstacle is closest to
				if (previousAvoidDirection != 0)
					avoidDirection = previousAvoidDirection;
				else
					avoidDirection = obstacleInfo.Side == AvoidInfo.AvoidSide.Left ? -1 : 1;

				// Create a temp waypoint to follow in order to avoid the asteroid
				// TODO: when we get a proper ship, rotation axes need to be changed
				GameObject avoidWaypoint         = new GameObject ();
				avoidWaypoint.name               = "AvoidWaypoint";
				avoidWaypoint.transform.position = controlObject.transform.position;
				avoidWaypoint.transform.Rotate (0, avoidDirection * AI_OBSTACLE_AVOID_ROTATION, 0);
				avoidWaypoint.transform.Translate (Vector3.forward * AI_OBSTACLE_RAY_FRONT_LENGTH);
				currentWaypoint = avoidWaypoint;

				// Uncomment this to visualise the waypoint
				// If there is more than one enemy, this probably doesn't help much
				/*GameObject visibleWaypoint = GameObject.CreatePrimitive (PrimitiveType.Sphere);
				visibleWaypoint.transform.position   = currentWaypoint.transform.position;
				visibleWaypoint.transform.localScale = visibleWaypoint.transform.localScale * 5;*/
			}
		}

		// Check if the angle is good for shooting and the enemy is in front of the player
		Vector3 direction             = player.transform.position - controlObject.transform.position;
		float angle                   = Vector3.Angle(-controlObject.transform.up, direction);
		Vector3 enemyRelativeToPlayer = player.transform.InverseTransformPoint(controlObject.transform.position);
		angleGoodForShooting          = (distance < ENGAGE_DISTANCE) && (angle < AI_SHOOT_MAX_ANGLE) && (enemyRelativeToPlayer.z > 0);
		
		// Avoid obsctales if needed
		if (state == EnemyAIState.AvoidObstacle)
		{
			bool finishedAvoiding = MoveTowardsCurrentWaypoint();

			// When the temporary avoid waypoint is reached, return to seeking the player
			if (finishedAvoiding)
			{
				state                  = EnemyAIState.SeekPlayer;
				previousAvoidDirection = 0;
			}
		}
		else
		{
			// If this enemy is a guard and is too far from its guarding location, turn back towards it
			if (guardLocation != Vector3.zero && state != EnemyAIState.ReturnToGuardLocation &&
				Vector3.Distance(controlObject.transform.position, guardLocation) >= AI_GUARD_TURN_BACK_DISTANCE)
			{
				state                             = EnemyAIState.ReturnToGuardLocation;
				GameObject returnWaypoint         = new GameObject ();
				returnWaypoint.name               = "GuardReturnWaypoint";
				returnWaypoint.transform.position = guardLocation;
				currentWaypoint                   = returnWaypoint;
			}
			// Engage player when close enough, otherwise catch up to them
			else if ((state == EnemyAIState.SeekPlayer || state == EnemyAIState.Wait) && distance <= ENGAGE_DISTANCE)
			{
				state = EnemyAIState.EngagePlayer;
				currentWaypoint = GetNextWaypoint();
			}
			else if (state == EnemyAIState.EngagePlayer && distance > ENGAGE_DISTANCE)
			{
				state = EnemyAIState.SeekPlayer;
			}

			if (state == EnemyAIState.EngagePlayer)
			{
				MoveTowardsCurrentWaypoint();
			}
			else if (state == EnemyAIState.SeekPlayer)
			{
				angleGoodForShooting = false;
				MoveTowardsPlayer();
			}
			else if (state == EnemyAIState.ReturnToGuardLocation)
			{
				MoveTowardsCurrentWaypoint();
				if (Vector3.Distance(controlObject.transform.position, guardLocation) < AI_GUARD_PROTECT_DISTANCE)
				{
					state = EnemyAIState.Wait;
					Destroy(currentWaypoint);
				}
			}
			// if (state == EnemyAIState.Wait) do nothing
		}

        enemyManager.UpdateTransform(controlObject.transform.position, controlObject.transform.rotation, controlObject.name);

	}

	// When not engaged, try and get closer to the player
	void MoveTowardsPlayer()
	{
		controlObject.transform.LookAt(player.transform.position);
		controlObject.transform.Translate (controlObject.transform.right*Time.deltaTime * speed);
		controlObject.transform.eulerAngles = new Vector3(controlObject.transform.eulerAngles.x - 90, controlObject.transform.eulerAngles.y,
			controlObject.transform.eulerAngles.z);
	}

	// Get the next engagement waypoint to follow, which should be different from the previous one
	GameObject GetNextWaypoint()
	{
		GameObject nextWaypoint;

		do
		{
			int r = Random.Range (0, aiWaypoints.Count);
			nextWaypoint = aiWaypoints [r];
		} while (currentWaypoint != null && nextWaypoint.Equals (currentWaypoint));

		return nextWaypoint;
	}

	// When engaged with the player ship, move between waypoints, returning true when the waypoint is reached
	bool MoveTowardsCurrentWaypoint()
	{
		Vector3 relativePos = currentWaypoint.transform.position - controlObject.transform.position;

		// TODO: when we get a proper ship, remove the upwards parameter and the extra rotation
		Quaternion rotation = Quaternion.LookRotation (relativePos, Vector3.forward);
		rotation *= Quaternion.Euler(-Vector3.right * 90);

		// Turn and bank
		controlObject.transform.rotation = Quaternion.Lerp (controlObject.transform.rotation, rotation,
			Time.deltaTime * AI_WAYPOINT_ROTATION_SPEED);
		float yRot = Mathf.Clamp (lastYRot - controlObject.transform.localEulerAngles.y, 0, 3);
		controlObject.transform.Rotate (0, 0, yRot, Space.Self);
		lastYRot = controlObject.transform.localEulerAngles.y;

		controlObject.transform.Translate (0, Time.deltaTime * speed * (-1), 0); // TODO: when we get a proper ship, move it forwards

		float distanceToWaypoint = Vector3.Distance(controlObject.transform.position, currentWaypoint.transform.position);
		if (distanceToWaypoint < AI_WAYPOINT_REACHED_DISTANCE)
		{
			// If the reached waypoint is an avoid waypoint, it is not needed any more
			if (state == EnemyAIState.AvoidObstacle)
				Destroy(currentWaypoint);
			
			currentWaypoint = GetNextWaypoint();
			return true;
		}

		return false;
	}

	// Check if the enemy is about to collide with an object
	// Returns the tag of the object, or null if there is no collision about to happen
	private AvoidInfo CheckObstacleAhead()
	{
		Transform objectTransform = controlObject.transform;
		bool hitLeft, hitRight;
		RaycastHit hitInfoLeft, hitInfoRight;

		// Cast two rays forward, one on each side of the object, to check for obstaclse
		// TODO: when we get a proper ship, vector directions need to be changed
		hitLeft = Physics.Raycast (objectTransform.position - AI_OBSTACLE_RAY_FRONT_OFFSET * objectTransform.right, -objectTransform.up,
			out hitInfoLeft, AI_OBSTACLE_RAY_FRONT_LENGTH);
		hitRight = Physics.Raycast (objectTransform.position + AI_OBSTACLE_RAY_FRONT_OFFSET * objectTransform.right, -objectTransform.up,
			out hitInfoRight, AI_OBSTACLE_RAY_FRONT_LENGTH);

		// If an obstacle is found, return its tag
		// Uncomment to show a ray when a collision is detected
		if (hitLeft)
		{
			/*Debug.DrawRay (objectTransform.position - AI_OBSTACLE_RAY_FRONT_OFFSET * objectTransform.right,
				-AI_OBSTACLE_RAY_FRONT_LENGTH * objectTransform.up, Color.magenta, 3, false);*/
			return new AvoidInfo(hitInfoLeft.collider.gameObject.tag, AvoidInfo.AvoidSide.Left);
		}
		else if (hitRight)
		{
			/*Debug.DrawRay(objectTransform.position + AI_OBSTACLE_RAY_FRONT_OFFSET*objectTransform.right,
				-AI_OBSTACLE_RAY_FRONT_LENGTH*objectTransform.up, Color.magenta, 3, false);*/
			return new AvoidInfo(hitInfoRight.collider.gameObject.tag, AvoidInfo.AvoidSide.Right);
		}
		else
			return AvoidInfo.None();

		// Uncomment to debug raycasting parameters
		/*Debug.DrawRay(objectTransform.position - AI_OBSTACLE_RAY_FRONT_OFFSET*objectTransform.right,
			-AI_OBSTACLE_RAY_FRONT_LENGTH*objectTransform.up, Color.green, 0, false);
		Debug.DrawRay(objectTransform.position + AI_OBSTACLE_RAY_FRONT_OFFSET*objectTransform.right,
			-AI_OBSTACLE_RAY_FRONT_LENGTH*objectTransform.up, Color.green, 0, false);*/
		/*Debug.DrawRay (objectTransform.position + AI_OBSTACLE_RAY_BACK_OFFSET*objectTransform.up,
			-AI_OBSTACLE_RAY_BACK_LENGTH*objectTransform.right, Color.yellow, 0, false);
		Debug.DrawRay (objectTransform.position + AI_OBSTACLE_RAY_BACK_OFFSET*objectTransform.up,
			+AI_OBSTACLE_RAY_BACK_LENGTH*objectTransform.right, Color.yellow, 0, false);*/
	}

	// Control shooting based on attributes
	IEnumerator ShootManager()
	{
		if(!shoot && angleGoodForShooting)
		{
			yield return new WaitForSeconds(0.1f);
			shoot = true;
			StartCoroutine ("Shoot");
		}
		else
		{
			yield return new WaitForSeconds(shootPeriod * (Random.Range (100-percentageVariation, 100+percentageVariation) / 100f));
			shoot = false;
		}

		StartCoroutine ("ShootManager");
	}

    // Spawn bullet, use predication, and player sound
	IEnumerator Shoot()
	{
		yield return new WaitForSeconds((1f/ shotsPerSec) + Random.Range (0.01f, 0.1f/shotsPerSec));

        GameObject obj = bulletManager.RequestObject();
        obj.transform.position = shootAnchor.transform.position;
        GameObject logic = logicManager.RequestObject();

		logic.transform.parent = obj.transform;
		logic.transform.localPosition = Vector3.zero;

		Vector3 destination = currentTarget.transform.position + ((currentPos - prevPos) * (distance / 10f));

		logic.GetComponent<BulletLogic>().SetDestination (destination, false, player, bulletManager, logicManager, impactManager);

        bulletManager.EnableClientObject(obj.name, obj.transform.position, obj.transform.rotation, obj.transform.localScale);

        if(randomPitch) mySrc.pitch = Random.Range(0.7f, 1.3f);
        if(distance < 300f) mySrc.PlayOneShot(fireSnd);
		if(shoot) StartCoroutine ("Shoot");
	}

	IEnumerator RechargeShields()
	{
		if(lastShieldCheck == shield)
		{
			shield += shieldRechargeRate / 10f;
			lastShieldCheck = shield;
			yield return new WaitForSeconds(0.1f);
			StartCoroutine ("RechargeShields");
		}
		else
		{
			lastShieldCheck = shield;
			yield return new WaitForSeconds(shieldDelay);
			StartCoroutine ("RechargeShields");
		}
	}

    // Detect collisions with other game objects
	public void collision(float damage, int playerId)
	{
		if (shield > damage)
		{
			shield -= damage;
		}
		else if (shield > 0)
		{
			float remDamage = damage - shield;
			shield = 0;

			health -= remDamage;
		}
		else if(health > damage)
		{
			health -= damage;
		}
		else if (transform.parent != null) // The null check prevents trying to destroy an object again while it's already being destroyed
		{
			if(playerId != -1)
			{
				// Update player score
				gameState.AddPlayerScore(playerId, 10);
			}

            string removeName = gameObject.name;

			// Automatically collect resources from enemy ship
			gameState.AddShipResources(droppedResources);

			// Destroy Object
            GameObject temp = explosionManager.RequestObject();
            temp.transform.position = transform.position;

            explosionManager.EnableClientObject(temp.name, temp.transform.position, temp.transform.rotation, temp.transform.localScale);

            //GameObject temp = Instantiate(destroyEffect, transform.position, transform.rotation) as GameObject;

			gameState.RemoveEnemy (controlObject.gameObject);
            //GetComponent<bl_MiniMapItem>().DestroyItem();
           
            transform.parent = null;
            enemyManager.DisableClientObject(removeName);
            enemyManager.RemoveObject(removeName);
            enemyLogicManager.RemoveObject(gameObject.name);
		}
	}

	// Class that shows obstacle detection info to be used for avoiding moves
	private class AvoidInfo
	{
		public enum AvoidSide { None, Left, Right }

		public string ObstacleTag { get; private set; } // The tag of the detected obstacle
		public AvoidSide Side { get; private set; }     // The side of the ship that the obstacle is coming up on

		private static AvoidInfo noneInfo = null;

		// Create an object showing no obstacle info
		private AvoidInfo()
		{
			this.ObstacleTag = null;
			this.Side        = AvoidSide.None;
		}

		// Create an object containing info about a collision
		public AvoidInfo(string obstacleTag, AvoidSide side)
		{
			this.ObstacleTag = obstacleTag;
			this.Side        = side;
		}
			
		// Get an object containing no obstacle info
		public static AvoidInfo None()
		{
			if (noneInfo == null)
				noneInfo = new AvoidInfo();

			return noneInfo;
		}

		// Check if this object contains no collision info
		public bool IsNone()
		{
			return this == noneInfo;
		}
	}
}

public enum EnemyAIState
{
	SeekPlayer,
	AvoidObstacle,
	EngagePlayer,
	Wait,
	ReturnToGuardLocation
}
