﻿using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

public class EngineerController : NetworkBehaviour
{
	private GameSettings settings;

	// Configuration parameters loaded through GameSettings
	private float walkSpeed;

	private float runSpeed;
	private float jumpSpeed;
	private float upMultiplier;
	private bool isWalking;
	private float stepInterval;

    private Text upgradeText;
    private Text dockText;
    private Text popupText;
    private PlayerController playerController;
    private new Camera camera;
    private MouseLook mouseLook;
    private Vector2 input;
    private float m_StepCycle = 0f;
    private float m_NextStep;
    private float engineerMaxDistance;

    private string upgradeString;
    private string repairString;
    private string dockString;
    private string popupString;

    private bool canUpgrade;
    private bool canRepair;
    private bool pressedUpgrade;
    private bool pressedRepair;
    private bool isDocked = false;
    private bool jump;
    private bool showPopup = false;

	private GameState gameState = null;

    private List<GameObject> engines;
    private List<GameObject> turrets;
    private List<GameObject> bridge;
	private List<GameObject> shieldGen;
	private List<GameObject> resourceStorage;

    private GameObject playerShip;
    private GameObject dockCanvas;
    private GameObject engineerCanvas;

    private Texture emptyProgressBar;
    private Texture filledProgressBar;

    private Vector2 progressBarLocation;

    private EngineerInteraction interactiveObject;
    private NetworkStartPosition startPosition;

    private Dictionary<InteractionKey, float> keyPressTime;

	private float workTime; // The repair and upgrade time in seconds

	#pragma warning disable 0649 // Disable warnings about unset private SerializeFields
	[SerializeField] Material defaultMat;
    [SerializeField] Material repairMat;
    [SerializeField] Material upgradeMat;
	#pragma warning restore 0649

    private enum InteractionKey
    {
        Repair,
        Upgrade,
        Popup
    }


	// Use this for initialization
    void Start()
    {
        //Initialize with default values
        if (isServer)
            gameObject.transform.rotation = Quaternion.identity;

		settings = GameObject.Find("GameSettings").GetComponent<GameSettings>();
		LoadSettings();

        runSpeed     = walkSpeed * 2;
        jumpSpeed    = walkSpeed;
        upMultiplier = jumpSpeed / 2;

		int enumElements = Enum.GetNames(typeof(InteractionKey)).Length;
		keyPressTime     = new Dictionary<InteractionKey, float>(enumElements);

        // Initialize with keys
        keyPressTime[InteractionKey.Upgrade] = 0f;
        keyPressTime[InteractionKey.Repair] = 0f;
        keyPressTime[InteractionKey.Popup] = 0f;

        // Remove crosshair from this scene. 
        GameObject.Find("CrosshairCanvas(Clone)").SetActive(false);
    }

	private void LoadSettings()
	{
        engineerMaxDistance = settings.EngineerMaxDistance;
        emptyProgressBar = settings.EmptyProgressBar;
        filledProgressBar = settings.FilledProgressBar;
	}

    /// <summary>
    /// Use this to initialize the Engineer once the main_game
    /// scene has been loaded. This acts as a replacement for Unity's
    /// Start() method. The Start() method can still be used to perform
    /// any initialization that does not require the main_game scene to be loaded
    /// </summary>
    /// <param name="cam">The camera object of the engineer</param>
    /// <param name="controller">The player controller of the engineer</param>
    public void Initialize(GameObject cam, PlayerController controller)
    {
        // Initialize the camera and the MouseLook script
        camera = cam.GetComponent<Camera>();
        mouseLook = gameObject.GetComponent<MouseLook>();
        mouseLook.Init(transform, camera.transform);
        playerController = controller;

        // Set the upgrade and repair strings depending on wheter
        // a controller is used or the keyboard is used
        if (Input.GetJoystickNames().Length > 0)
        {
            upgradeString = "Press LT to upgrade";
            repairString = "Press RT to repair";
            dockString = "Press B to dock";
            popupString = "Job finished. Press B to dock, or continue doing jobs";
        }
        else
        {
            upgradeString = "Press Mouse1 to upgrade";
            repairString = "Press Mouse2 to repair";
            dockString = "Press L Shift to dock";
            popupString = "Job finished. Press L Shift to dock, or continue doing jobs";
        }

        // Set the progress bar location
        progressBarLocation = new Vector2((Screen.width / 2) - 50, (Screen.height / 2) + 130);

        // Get a reference to the player ship
        playerShip = GameObject.Find("PlayerShip(Clone)");

        // Create the upgrade text object to use
        engineerCanvas = Instantiate(Resources.Load("Prefabs/UpgradeText")) as GameObject;
        Text[] tmp = engineerCanvas.GetComponentsInChildren<Text>();

        foreach (Text t in tmp)
        {
            if (t.name.Equals("Upgrade Text"))
                upgradeText = t;
            else if (t.name.Equals("Dock Text"))
                dockText = t;
            else if (t.name.Equals("Popup Text"))
                popupText = t;
        }
        dockText.text = dockString;

        // Create the docked canvas, and start the engineer in the docked state
        dockCanvas = Instantiate(Resources.Load("Prefabs/DockingCanvas")) as GameObject;

        // We need a reference to the engineer start position as this is where
        // we anchor the engineer
        startPosition = playerShip.GetComponentInChildren<NetworkStartPosition>();
        gameObject.transform.parent = startPosition.transform;

        Dock();

        // Get the components of the main ship that can be upgraded and/or repaired
        EngineerInteraction[] interactionObjects = playerShip.GetComponentsInChildren<EngineerInteraction>();

        engines 		= new List<GameObject>();
        turrets 		= new List<GameObject>();
        bridge 			= new List<GameObject>();
		shieldGen 		= new List<GameObject>();
		resourceStorage = new List<GameObject>();
		foreach (EngineerInteraction interaction in interactionObjects)
        {
            // Ensure that the properties of the Interaction
            // script are initialized as normally they are only
            // initialized on the server side
            interaction.Initialize();

			switch (interaction.Type)
			{
			case ComponentType.Engine:
				engines.Add (interaction.gameObject);
				break;
			case ComponentType.Bridge:
                bridge.Add (interaction.gameObject);
				break;
			case ComponentType.Turret:
				turrets.Add (interaction.gameObject);
				break;
			case ComponentType.ShieldGenerator:
				shieldGen.Add(interaction.gameObject);
				break;
			case ComponentType.ResourceStorage:
				resourceStorage.Add(interaction.gameObject);
				break;
			}
        }
    }

    /// <summary>
    /// Sets the upgradeable and repairable property
    /// for each game object in the list to value
    /// </summary>
    /// <param name="isUpgrade">Wether the job is an upgrade or a repair</param>
    /// <param name="value">The value to set Upgradeable/Repairable property to</param>
    /// <param name="parts">The list of parts this job applies to</param>
    private void ProcessJob(bool isUpgrade, bool value, List<GameObject> parts)
    {
        foreach (GameObject obj in parts)
        {
            EngineerInteraction interaction = obj.GetComponent<EngineerInteraction>();

            if (isUpgrade)
                interaction.Upgradeable = value;
            else
                interaction.Repairable = value;
        }
    }

	/// <summary>
	/// Gets list of parts of a specified type.
	/// </summary>
	/// <returns>The part list.</returns>
	/// <param name="type">The component type.</param>
	private List<GameObject> GetPartListByType(ComponentType type)
	{
		List<GameObject> partList = null;

		switch (type)
		{
		case ComponentType.Turret:
			partList = turrets;
			break;
		case ComponentType.Engine:
			partList = engines;
			break;
		case ComponentType.Bridge:
			partList = bridge;
			break;
		case ComponentType.ShieldGenerator:
			partList = shieldGen;
			break;
		case ComponentType.ResourceStorage:
			partList = resourceStorage;
			break;
		}

		return partList;
	}

    /// <summary>
    /// Adds the uprade/repair job to the engineer's list
    /// </summary>
    /// <param name="isUpgrade">Wether the job is an upgrade or a repair</param>
    /// <param name="part">The part of the ship this job applies to</param>
	public void AddJob(bool isUpgrade, ComponentType part)
    {
		List<GameObject> partList = GetPartListByType(part);

		this.ProcessJob(isUpgrade, true, partList);

        // Highlight the appropriate components
        Highlight(part);
    }

    /// <summary>
    /// Resets the Upgradeable/Repairable attribute of the object
    /// that has been upgraded/repaired thus taking the job off the queue
    /// </summary>
    /// <param name="isUpgrade"></param>
    /// <param name="part"></param>
    private void FinishJob(bool isUpgrade, ComponentType part)
    {
		List<GameObject> partList = GetPartListByType(part);

		this.ProcessJob(isUpgrade, false, partList);

        // Un-highlight the appropriate components
        Highlight(part);
    }

    private void Update()
    {

        // Make sure this only runs on the client
        if (playerController == null || !playerController.isLocalPlayer)
            return;

        RotateView();
        if (Input.GetButton("Dock"))
        {
            Dock();
            return;
        }

        jump = Input.GetButton("Jump");
        pressedUpgrade = Input.GetButton("Upgrade");
        pressedRepair = Input.GetButton("Repair");

        // Deal with how long Upgrade and Repair have been pressed
        if (pressedUpgrade)
            keyPressTime[InteractionKey.Upgrade] += Time.deltaTime;
        else
            keyPressTime[InteractionKey.Upgrade] = 0;

        if (pressedRepair)
            keyPressTime[InteractionKey.Repair] += Time.deltaTime;
        else
            keyPressTime[InteractionKey.Repair] = 0;

        // Artificial way of having the popup show for 2 seconds
        if (showPopup)
        {
            keyPressTime[InteractionKey.Popup] += Time.deltaTime;
            popupText.text = popupString;
        }
        else
        {
            keyPressTime[InteractionKey.Popup] = 0;
            popupText.text = "";
        }

        // Do forward raycast from camera to the center of the screen to see if an upgradeable object is in front of the player
        int x = Screen.width / 2;
        int y = Screen.height / 2;
        Ray ray = camera.ScreenPointToRay(new Vector3(x, y, 0));
        RaycastHit hitInfo;
        canUpgrade = false;
        canRepair = false;

        if (Physics.Raycast(ray, out hitInfo, 10.0f))
        {
            if (hitInfo.collider.CompareTag("Player"))
            {
                // Get the game object the engineer is currently looking at
                // and get the EngineerInteraction script attached if it has one
                GameObject objectLookedAt = hitInfo.collider.gameObject;
                interactiveObject = objectLookedAt.GetComponent<EngineerInteraction>();

                // If the object being looked at has an EngineerInteraction
                // script we use it
                if (interactiveObject != null)
                {
                    canUpgrade = interactiveObject.Upgradeable;
                    canRepair = interactiveObject.Repairable;
                }
            }

            if (canRepair)
                upgradeText.text = repairString;
            else if (canUpgrade)
                upgradeText.text = upgradeString;
            else
                ResetUpgradeText();
        }
        else
        {
            ResetUpgradeText();
        }
    }

    private void FixedUpdate()
    {
        // Make sure this only runs on the client
        if (playerController == null || !playerController.isLocalPlayer)
            return;

        float speed;
        GetInput(out speed);

        // Move the player if they have moved
        if (input.x != 0 || input.y != 0 || jump == true)
        {
            // If the engineer is docked we undock first
            UnDock();

            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 desiredMove = transform.forward * input.y + transform.right * input.x;

            Vector3 actualMove;
            actualMove.x = desiredMove.x * speed;
            actualMove.z = desiredMove.z * speed;
            actualMove.y = desiredMove.y * speed;

            if (jump)
            {
                actualMove.y += jumpSpeed;
            }

            // Only move the engineer if it is within the bounds of the player ship
            Vector3 newPosition = transform.position + actualMove;
            if (Vector3.Distance(newPosition, playerShip.transform.position) < engineerMaxDistance)
                transform.position = newPosition;
        }

        // If the popup has been show for the required amount of time then
        // we make it disappear
		if (showPopup && keyPressTime[InteractionKey.Popup] >= workTime)
            showPopup = false;

        // Do upgrades/repairs
        // Force engineer to repair before upgrading if
        // both are possible
		if (canRepair && keyPressTime[InteractionKey.Repair] >= workTime)
        {
            FinishJob(false, interactiveObject.Type);
            playerController.CmdDoRepair(interactiveObject.Type);
            showPopup = true;
        }
		else if (canUpgrade && keyPressTime[InteractionKey.Upgrade] >= workTime)
        {
            FinishJob(true, interactiveObject.Type);
            playerController.CmdDoUpgrade(interactiveObject.Type);
            showPopup = true;
        }

        ProgressStepCycle(speed);
    }

    /// <summary>
    /// Docks the engineer if it is not docked
    /// </summary>
    private void Dock()
    {
		if (isDocked)
			return; 
        
        isDocked = true;
        dockCanvas.SetActive(isDocked);
        engineerCanvas.SetActive(!isDocked);
        gameObject.transform.parent = startPosition.transform;
        gameObject.transform.localPosition = new Vector3(0,0,0);
        gameObject.transform.rotation = startPosition.transform.rotation;

		// Update the drone stats
		if (gameState == null) // This is needed because the engineer can't always get the game state from the start
			gameState = GameObject.Find("GameManager").GetComponent<GameState>();
		if (gameState != null)
			gameState.GetDroneStats(out walkSpeed, out workTime);
    }

    /// <summary>
    /// Un-docks the engineer if it is docked
    /// </summary>
    private void UnDock()
    {
		if (!isDocked)
			return;
	
        isDocked = false;
        dockCanvas.SetActive(isDocked);
        engineerCanvas.SetActive(!isDocked);
        gameObject.transform.parent = playerShip.transform;
    }

    private void ProgressStepCycle(float speed)
    {
        if (!(m_StepCycle > m_NextStep))
        {
            return;
        }

        m_NextStep = m_StepCycle + stepInterval;
    }

    private void GetInput(out float speed)
    {
        // Read input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

#if !MOBILE_INPUT
        // On standalone builds, walk/run speed is modified by a key press.
        // keep track of whether or not the character is walking or running
        isWalking = !Input.GetKey(KeyCode.LeftShift);
#endif
        // set the desired speed to be walking or running
        speed = isWalking ? walkSpeed : runSpeed;
        input = new Vector2(horizontal, vertical);

        // normalize input if it exceeds 1 in combined length:
        if (input.sqrMagnitude > 1)
        {
            input.Normalize();
        }
    }

    private void RotateView()
    {
        mouseLook.LookRotation(transform, camera.transform);
    }

    private void ResetUpgradeText()
    {
        upgradeText.text = "";
    }

    /// <summary>
    /// Highlights all components of type component
    /// </summary>
    /// <param name="component">The components to highlight</param>
    private void Highlight(ComponentType component)
    {
        // The list of game objects that need to be highlighted
		List<GameObject> toHighlight = GetPartListByType(component);

        for(int i = 0; i < toHighlight.Count; i++)
        {
            GameObject part = toHighlight[i];
            EngineerInteraction interaction = part.GetComponent<EngineerInteraction>();

            if (interaction == null)
            {
                Debug.Log("EngineerInteraction component could not be found");
            }
            else
            {
                Renderer renderer = part.GetComponent<Renderer>();
                Material[] mats = renderer.materials;

                if (interaction.Repairable)
                {
                    for(int j = 0; j < mats.Length; ++j)
                        mats[j] = repairMat;
                }
                else if(interaction.Upgradeable)
                {
                    for(int j = 0; j < mats.Length; ++j)
                        mats[j] = upgradeMat;
                }
                else
                {
                    // Default
                    for(int j = 0; j < mats.Length; ++j)
                        mats[j] = defaultMat;
                }

                renderer.materials = mats;
            }
        }
    }

    private void OnGUI()
    {
        if (canRepair && keyPressTime[InteractionKey.Repair] > 0)
        {
			float progress = keyPressTime[InteractionKey.Repair] / workTime;
            GUI.DrawTexture(new Rect(progressBarLocation.x, progressBarLocation.y, 100, 50), emptyProgressBar);
            GUI.DrawTexture(new Rect(progressBarLocation.x, progressBarLocation.y, 100 * progress, 50), filledProgressBar);
        }
        else if (canUpgrade && keyPressTime[InteractionKey.Upgrade] > 0)
        {
			float progress = keyPressTime[InteractionKey.Upgrade] / workTime;
            GUI.DrawTexture(new Rect(progressBarLocation.x, progressBarLocation.y, 100, 50), emptyProgressBar);
            GUI.DrawTexture(new Rect(progressBarLocation.x, progressBarLocation.y, 100 * progress, 50), filledProgressBar);
        }
    }
}
