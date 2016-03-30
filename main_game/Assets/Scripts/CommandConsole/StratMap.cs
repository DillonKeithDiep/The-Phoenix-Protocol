﻿using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class StratMap : MonoBehaviour {

    private GameObject playerIcon;
    private GameObject objectiveIcon;
    private RectTransform objectiveIconRectTransform;
    private Transform shipTransform;
    private RectTransform playerIconTransform;
    private float panelHeight;
    private float panelWidth;
    Dictionary<int,GameObject> outpostIconDict;
    public GameObject Portal { get; set; }

    private Vector3 offset = new Vector3(0, 200, 0);
	// Use this for initialization
	void Start () {
        var panel = this;
        shipTransform = GameObject.Find("PlayerShip(Clone)").transform;
        playerIcon = Instantiate(Resources.Load("Prefabs/IndicatorArrow", typeof(GameObject))) as GameObject;
        playerIcon.transform.SetParent(panel.transform, false);
        playerIcon.transform.SetParent(panel.transform, false);
        playerIconTransform = (RectTransform)playerIcon.transform;
        objectiveIcon  = Instantiate(Resources.Load("Prefabs/ObjectiveIcon", typeof(GameObject))) as GameObject;
        objectiveIcon.SetActive(false);
        objectiveIcon.transform.SetParent(panel.transform, false);
        objectiveIconRectTransform = (RectTransform)objectiveIcon.transform;
        RectTransform panelRectTransform = (RectTransform)panel.transform;
        panelHeight = panelRectTransform.sizeDelta.y;
        panelWidth = panelRectTransform.sizeDelta.x;
        if (Portal == null) print("portal undefined at stratmap start (Luke's fault)");
        else PortalInit();
        outpostIconDict = new Dictionary<int,GameObject>();
    }

    public void NewOutpost(GameObject outpost, int id, int difficulty)
    {
        var panel = this;
        if (panel != null)  // make sure you actually found it!
        {
            GameObject outpostIcon = Instantiate(Resources.Load("Prefabs/OutpostIcon", typeof(GameObject))) as GameObject;
            outpostIcon.transform.SetParent(panel.transform, false);
            RectTransform outpostRectTransform = (RectTransform)outpostIcon.transform;
            outpostRectTransform.sizeDelta *= (float)(0.5 + difficulty * 0.5);
            Vector3 screenPos = new Vector3(outpost.transform.position.x/20, outpost.transform.position.z/20,0);
            outpostRectTransform.anchoredPosition = screenPos;
            if (WithinBounds(screenPos))
                outpostIcon.SetActive(true);
            else outpostIcon.SetActive(false);
            Image outpostImage = outpostIcon.GetComponent<Image>();
            outpostImage.color = Color.yellow;
            outpostIconDict.Add(id, outpostIcon);
        }
    }

    public void PortalInit()
    {
        var panel = this;
        if (panel != null)  // make sure you actually found it!
        {
            GameObject portalSymbol = Instantiate(Resources.Load("Prefabs/PortalIcon", typeof(GameObject))) as GameObject;
            portalSymbol.transform.SetParent(panel.transform, false);
            RectTransform portalRectTransform = (RectTransform)portalSymbol.transform;
            Vector3 screenPos = new Vector3(Portal.transform.position.x / 20, Portal.transform.position.z / 20, 0);
            portalRectTransform.anchoredPosition = screenPos;
            if (WithinBounds(screenPos))
                portalSymbol.SetActive(true);
            else portalSymbol.SetActive(false);
        }
    }

    private bool WithinBounds(Vector3 screenPos)
    {
        if (screenPos.x > -(panelWidth/2) && screenPos.x < (panelWidth/2) && 
            screenPos.y > -(panelHeight/2) && screenPos.y < (panelHeight/2)) 
            return true;
        return false;
    }

    public void outpostVisitNotify(int id)
    {
        //outpostIconDict[id].GetComponent<Image>().color = Color.grey;
    }

    public void startMission(int id)
    {
        if (outpostIconDict[id] == null) print("outpostIconDict[id] == null");
        else
        {
            RectTransform outpostRectTransform = (RectTransform)outpostIconDict[id].transform;
            objectiveIconRectTransform.anchoredPosition = outpostRectTransform.anchoredPosition;
            objectiveIconRectTransform.sizeDelta = outpostRectTransform.sizeDelta;
            objectiveIcon.SetActive(true);
        }
    }

    public void endMission(int id)
    {
        //outpostIconDict[id].GetComponent<Image>().color = Color.green;
        objectiveIcon.SetActive(false);
    }

	// Update is called once per frame
	void Update () {
        Vector3 screenPos = new Vector3(shipTransform.position.x / 20, shipTransform.position.z / 20, 0);
        playerIconTransform.anchoredPosition = screenPos;
        Quaternion shipRotation = shipTransform.rotation;
        Vector3 eulerRotation = shipRotation.eulerAngles;
        Quaternion newRotation = Quaternion.identity;
        newRotation.eulerAngles = new Vector3(0, 0, -eulerRotation.y);
        playerIconTransform.localRotation = newRotation;
        if (WithinBounds(screenPos))
            playerIcon.SetActive(true);
        else playerIcon.SetActive(false);
    }
}
