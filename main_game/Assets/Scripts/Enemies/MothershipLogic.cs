﻿using UnityEngine;
using System.Collections;

public class MothershipLogic : MonoBehaviour {

    private float health;
    private float maxHealth;
    private int spawnedEnemies = 0;
    private EnemySpawner spawner;
    private GameSettings settings;
    private GameState gameState;
    private bool destroyEffects = false;
    [SerializeField] GameObject[] particleSpawnLocations;
    private int numExplosions = 0;
    private int maxExplosions = 50;
    private ObjectPoolManager effectsManager;
    private Transform mesh;
    private GameObject bulletSpawnLocation;
    [SerializeField] GameObject beamObject;
    [SerializeField] GameObject beamLogicObject;
    GameObject player;

	// Use this for initialization
	void Start () {

        settings = GameObject.Find("GameSettings").GetComponent<GameSettings>();
        if( GameObject.Find("MothershipDamageManager") != null)
            effectsManager = GameObject.Find("MothershipDamageManager").GetComponent<ObjectPoolManager>();
        else
            Destroy(this);
        LoadSettings();

        bulletSpawnLocation = GameObject.Find("BulletSpawn").gameObject;

        GameObject server = settings.GameManager;
        gameState         = server.GetComponent<GameState>();
        player = gameState.PlayerShip;

        mesh = transform.parent.Find("Mesh");

        StartCoroutine(SpawnExplosions());
        StartCoroutine(ReduceExplosions());
        StartCoroutine(ShootBeam());
	}

    IEnumerator ShootBeam()
    {
        yield return new WaitForSeconds(Random.Range(10,20));

        Vector3 targetPos = player.transform.position + (player.transform.forward * (Vector3.Distance(transform.position, player.transform.position) / Random.Range(10f,14f)));
        int numberOfBeams = Random.Range(7,14);

        for(int i = 0; i < numberOfBeams; i++)
        {
            GameObject beam = Instantiate(beamObject, bulletSpawnLocation.transform.position, Quaternion.identity) as GameObject;
            beam.transform.LookAt(targetPos);
            beam.GetComponent<BulletMove>().Speed = 250f;
            ServerManager.NetworkSpawn(beam);
            beam.GetComponent<Collider>().enabled = true;
            GameObject beamLogic = Instantiate(beamLogicObject, bulletSpawnLocation.transform.position, Quaternion.identity) as GameObject;
            beamLogic.transform.parent = beam.transform;
            MothershipBeamLogic logicComp = beamLogic.GetComponent<MothershipBeamLogic>();
            logicComp.player = player;
            logicComp.Setup();
            yield return new WaitForSeconds(0.1f);
        }



        StartCoroutine(ShootBeam());
    }

    IEnumerator ReduceExplosions()
    {
        yield return new WaitForSeconds(3f);
        numExplosions--;
        StartCoroutine(ReduceExplosions());
    }

    IEnumerator SpawnExplosions()
    {
        if(destroyEffects && numExplosions < maxExplosions)
        {
            GameObject obj = effectsManager.RequestObject();
            obj.transform.position = particleSpawnLocations[Random.Range(0,particleSpawnLocations.Length)].transform.position;
            numExplosions++;
        }
        yield return new WaitForSeconds(Random.Range(0.1f,0.4f));
        StartCoroutine(SpawnExplosions());
    }

    public void SetSpawner(EnemySpawner temp)
    {
        spawner = temp;
        spawner.mothershipEnemySpawner = transform.parent.Find("MothershipEnemySpawner").gameObject;
        StartCoroutine(SpawnEnemies());
    }

    IEnumerator SpawnEnemies()
    {
		// Do not spawn enemies after the player has died
		if (gameState == null)
			yield return new WaitForSeconds(3f);
		else if (gameState.Status != GameState.GameStatus.Started)
			yield break;
		
        if(spawnedEnemies < 30)
        {
            spawner.SpawnEnemyFromMothership();
            spawnedEnemies++;
			yield return new WaitForSeconds(3f);
            StartCoroutine(SpawnEnemies());
        }
        else
        {
            // Take a little break
            yield return new WaitForSeconds(15f);
            spawnedEnemies = 0;
            StartCoroutine(SpawnEnemies());
        }
    }
	
    private void LoadSettings()
    {
        health = settings.GlomMothershipHealth;
        maxHealth = health;
    }

    // Detect collisions with other game objects
    public void collision(float damage, int playerId)
    {
            if(health > damage)
            {
                health -= damage;
            }
            else if (transform.parent != null) // The null check prevents trying to destroy an object again while it's already being destroyed
            {
                Debug.Log("Glom mothership destroyed");
                GameObject.Find("MusicManager(Clone)").GetComponent<MusicManager>().PlayMusic(1);

                GameObject explosion = Instantiate(Resources.Load("Prefabs/OutpostExplode", typeof(GameObject))) as GameObject;
                explosion.transform.position = transform.position;
                explosion.SetActive(true);
                ServerManager.NetworkSpawn(explosion);
                Destroy(transform.parent.gameObject);
            }

            if(health < maxHealth * 0.4f && !destroyEffects)
            {
                Debug.Log("Damage effects enabled");
                destroyEffects = true;
            }
    }
}
