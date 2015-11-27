﻿using UnityEngine;
using System.Collections;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

public class UDPServer : MonoBehaviour
{
    [SerializeField] int listenPort;
    [SerializeField] int maxReceivedMessagesPerInterval;

    private GameState state;

    private UdpClient socket;
    private IPEndPoint phoneServer;
    private byte[] receive_byte_array;

    void Start()
    {
        //Debug.Log("Starting UDP server.");
        socket = new UdpClient(listenPort);
        phoneServer = new IPEndPoint(IPAddress.Any, 0);
        state = this.gameObject.GetComponent<GameState>();
        StartCoroutine("PhoneServerConnectionHandler");
        StartCoroutine("SendUpdatedOjects");
    }

    // Update is called once per frame
    void Update()
    {
    }

    // Handles incomming messages from the phone server
    IEnumerator PhoneServerConnectionHandler()
    {
        while (true)
        {
            int receivedMessages = 0;
            while (socket.Available != 0 && receivedMessages < maxReceivedMessagesPerInterval)
            {
                receive_byte_array = socket.Receive(ref phoneServer);
                Debug.Log("Received a broadcast from " + phoneServer.ToString());
                string received_data = Encoding.ASCII.GetString(receive_byte_array, 0, receive_byte_array.Length);
                Debug.Log("Data: " + received_data);
                receivedMessages++;
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator SendUpdatedOjects()
    {
        while (true)
        {
            if(!phoneServer.Address.Equals(IPAddress.Any))
            {
                SendNewAsteroids();
                SendRemovedAsteroids();
            }
            yield return new WaitForSeconds(0.07f);
        }
    }

    private void SendNewAsteroids()
    {
        List<GameObject> newAsteroids = state.GetNewAsteroids();
        if(newAsteroids != null && newAsteroids.Count > 0)
        {
            string jsonMsg = "{\"type\":\"NEW_AST\",\"data\":[";
            foreach (GameObject ast in newAsteroids)
            {
                jsonMsg += "{\"id\": " + (uint)ast.GetInstanceID() +
                            ",\"x\":" + ast.transform.position.x +
                            ",\"y\":" + ast.transform.position.z +
                            "},";
            }
            jsonMsg = jsonMsg.Remove(jsonMsg.Length - 1);
            jsonMsg += "]}";
            SendMsg(jsonMsg);
            state.ClearNewAsteroids();
        }
    }

    private void SendRemovedAsteroids()
    {
        List<uint> removedAsteroids = state.GetRemovedAsteroids();
        if (removedAsteroids != null && removedAsteroids.Count > 0)
        {
            string jsonMsg = "{\"type\":\"RMV_AST\",\"data\":[";
            foreach (uint id in removedAsteroids)
            {
                jsonMsg += id + ",";
            }
            jsonMsg = jsonMsg.Remove(jsonMsg.Length - 1);
            jsonMsg += "]}";
            SendMsg(jsonMsg);
            state.ClearRemovedAsteroids();
        }
    }

    // Send a JSON encoded message to the phone server
    private void SendMsg(String jsonMsg)
    {
        Byte[] data = Encoding.ASCII.GetBytes(jsonMsg);
        socket.Send(data, data.Length, phoneServer);
    }
}