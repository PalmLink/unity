/* Based on: https://learn.microsoft.com/fr-fr/windows/uwp/devices-sensors/gatt-server and https://github.com/marianylund/BleWinrtDll */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using UnityEngine.UI;
using TMPro;
using Random = System.Random;
using static BLE.Impl;
using PimDeWitte.UnityMainThreadDispatcher;
using System.Globalization;

public class BLEBehaviour : MonoBehaviour
{
    public TMP_Text TextIsScanning, TextTargetDeviceConnection, TextTargetDeviceData, TextDiscoveredDevices;
    [SerializeField] public TMP_Text TextData;
    /*public TextMeshProUGUI TextData;*/
    public Button ButtonStartScan;

    // Change to match the device.
    string targetDeviceName = "Arduino";
    string serviceUuid = "{19b10000-e8f2-537e-4f6c-d104768a1214}";
    string[] characteristicUuids = {
        // "{43552B6B-F233-48DD-ACEB-7808231A6CB1}",
        "{19b10001-e8f2-537e-4f6c-d104768a1214}" // UUID
    };

    BLE ble;
    BLE.BLEScan scan;
    bool isScanning = false, isConnected = false;
    string deviceId = null;
    IDictionary<string, string> discoveredDevices = new Dictionary<string, string>();
    int devicesCount = 0;
    string valuesBLE = "null";
    /*public GameObject UI_text;*/

    // BLE Threads 
    Thread scanningThread, connectionThread, readingThread;

    float timePassed = 0f;

    void Start()
    {
        ble = new BLE();
        StartScanHandler();
        TextTargetDeviceConnection.text = targetDeviceName + " not found.";
        /*UI_text = GameObject.Find("UI_text");*/
    }

    void Update()
    {
        /*if(UI_text == null)
        {
            Debug.Log("error UI text NULL");
        }
         var camPos = Camera.main.transform.position + Camera.main.transform.forward;
        var difference = new Vector3(camPos.x + 0.1f, camPos.y - 0.6f, camPos.z + 0.1f);
        UI_text.transform.position = difference;
        UI_text.transform.localScale = Vector3.one * 0.025f;
       *//* UI_text.transform.LookAt(Camera.main.transform.position, Vector3.up);*//*
        UI_text.transform.LookAt(Camera.main.transform.position);
        *//* var vec = new Vector3(0, Camera.main.transform.rotation[1], 0);
         UI_text.transform.RotateAround(camPos, Vector3.up, Camera.main.transform.rotation[1]);
         Debug.Log(Camera.main.transform.rotation.ToString());
         Debug.Log(Camera.main.transform.rotation[1]);*/

        if (isScanning)
        {
            if (discoveredDevices.Count > devicesCount)
            {
                UpdateGuiText("scan");

                devicesCount = discoveredDevices.Count;
            }
        }
        else
        {
            if (TextIsScanning.text != "Not scanning.")
            {
                TextIsScanning.color = Color.white;
                TextIsScanning.text = "Not scanning.";
            }
        }

        // The target device was found.
        if (deviceId != null && deviceId != "-1")
        {
            // Target device is connected and GUI knows.
            if (ble.isConnected && isConnected)
            {
                if (readingThread == null || !readingThread.IsAlive)
                {
                    readingThread = new Thread(ReadBLEData);
                }
                timePassed += Time.deltaTime;
                if (timePassed > 1f)
                {
                    // Debug.Log("Wants to read ble data");
                    timePassed = 0f;
                    // UnityMainThreadDispatcher.Instance().Enqueue(ReadDataEvent());
                    readingThread.Start();
                    UpdateGuiText("readData");
                    // Debug.Log("BLE values = " + valuesBLE);
                }

            }
            // Target device is connected, but GUI hasn't updated yet.
            else if (ble.isConnected && !isConnected)
            {
                UpdateGuiText("Connected");
                isConnected = true;
                // Device was found, but not connected yet. 
            }
            else if (!isConnected)
            {
                TextTargetDeviceConnection.text = "Found target device:\n" + targetDeviceName;
            }
        }
    }

    public void StartScanHandler()
    {
        devicesCount = 0;
        isScanning = true;
        discoveredDevices.Clear();
        scanningThread = new Thread(ScanBleDevices);
        scanningThread.Start();
        TextIsScanning.color = new Color(244, 180, 26);
        TextIsScanning.text = "Scanning...";
        TextIsScanning.text +=
            $"Searching for {targetDeviceName} with \nservice {serviceUuid} and \ncharacteristic {characteristicUuids[0]}";
        TextDiscoveredDevices.text = "";
    }

    void ScanBleDevices()
    {
        scan = BLE.ScanDevices();
        Debug.Log("BLE.ScanDevices() started.");
        scan.Found = (_deviceId, deviceName) =>
        {
            if (!discoveredDevices.ContainsKey(_deviceId))
            {
                Debug.Log("Found device with name: " + deviceName);
                discoveredDevices.Add(_deviceId, deviceName);
            }

            if (deviceId == null && deviceName == targetDeviceName)
            {
                deviceId = _deviceId;
            }
        };

        scan.Finished = () =>
        {
            isScanning = false;
            Debug.Log("Scan finished");
            if (deviceId == null)
                deviceId = "-1";
        };
        while (deviceId == null)
            Thread.Sleep(500);
        scan.Cancel();
        scanningThread = null;
        isScanning = false;

        if (deviceId == "-1")
        {
            Debug.Log($"Scan is finished. {targetDeviceName} was not found.");
            return;
        }
        Debug.Log($"Found {targetDeviceName} device with id {deviceId}.");
        StartConHandler();
    }

    public void StartConHandler()
    {
        connectionThread = new Thread(ConnectBleDevice);
        connectionThread.Start();
    }

    void ConnectBleDevice()
    {
        if (deviceId != null)
        {
            try
            {
                Debug.Log($"Attempting to connect to {targetDeviceName} device with id {deviceId} ...");
                ble.Connect(deviceId,
                    serviceUuid,
                    characteristicUuids);
            }
            catch (Exception e)
            {
                Debug.Log("Could not establish connection to device with ID " + deviceId + "\n" + e);
            }
        }
        if (ble.isConnected)
            Debug.Log("Connected to: " + targetDeviceName);
    }

    void UpdateGuiText(string action)
    {
        switch (action)
        {
            case "scan":
                TextDiscoveredDevices.text = "";
                foreach (KeyValuePair<string, string> entry in discoveredDevices)
                {
                    TextDiscoveredDevices.text += "DeviceID: " + entry.Key + "\nDeviceName: " + entry.Value + "\n\n";
                    Debug.Log("Added device: " + entry.Key);
                }
                break;
            case "connected":
                TextTargetDeviceConnection.text = "Connected to target device:\n" + targetDeviceName;
                break;
            case "readData":
                TextTargetDeviceData.text = valuesBLE;
                Debug.Log(valuesBLE);
                TextData.text = "changing txt...";
                if(valuesBLE != null)
                {
                    String dat = valuesBLE.Substring(0, 1);
                    Debug.Log("dat = " + dat);
                    DisplayData(dat);
                }
                /*valuesBLE = null;*/
                break;
        }
    }

    private void OnDestroy()
    {
        CleanUp();
    }

    private void OnApplicationQuit()
    {
        CleanUp();
    }

    // Prevent threading issues and free BLE stack.
    // Can cause Unity to freeze and lead
    // to errors when omitted.
    private void CleanUp()
    {
        try
        {
            scan.Cancel();
            ble.Close();
            scanningThread?.Abort();
            connectionThread.Abort();
            readingThread.Abort();
        }
        catch (NullReferenceException e)
        {
            Debug.Log("Thread or object never initialized.\n" + e);
        }
    }

    public void ReadBLEData()
    {
        Debug.Log("Reading");
        BLE.Impl.BLEData packageReceived = new BLE.Impl.BLEData();
        bool result = BLE.Impl.PollData(out packageReceived, false);
        int n = packageReceived.buf.Length;
        int i = 0;
        string str = "";
        for (i = 0; i < n && i < 20; i++)
        {
            char c = (char)packageReceived.buf[i];
            str += c;
        }
        if (str.Length > 0)
        {
            valuesBLE = str;
        }
        Thread.Sleep(100);
    }


    public IEnumerator ReadDataEvent()
    {
        // Debug.Log("This is executed from the main thread");
        BLE.Impl.BLEData packageReceived = new BLE.Impl.BLEData();
        bool result = BLE.Impl.PollData(out packageReceived, false);
        int n = packageReceived.buf.Length;
        int i = 0;
        string str = "";
        for (i = 0; i < n && i < 10; i++)
        {
            char c = (char)packageReceived.buf[i];
            str += c;
        }
        if (str.Length > 0)
        {
            // Debug.Log(str);
            // TextTargetDeviceData.text = str;
            valuesBLE = str;

        }
        yield return null;
    }

    public void DisplayData(string data)
    {
        /*var nb = int.Parse(data);*/
        String str = "";
        String addr = "N/A";
        /*String type = "";*/
        String content = "N/A";
        String receiver = "N/A";
        String sender = "N/A";
        /*Debug.Log("NB = " + nb);*/
        bool defaut = false;

        Debug.Log("data = " + data);
        switch (data)
        {
            case "0":
                Debug.Log("Case 0 found!!");
                addr = "Steinmüllerallee 1, 51643 Gummersbach";
                content = "PalmLink";
                receiver = "Group 3";
                break;
            case "1":
                addr = "3 Parv. Louis Néel, 38000 Grenoble, France";
                content = "Exchange contract";
                sender = "Adrien Sonot";
                receiver = "Office for outgoing students";
                break;
            case "2":
                addr = "Area 51, Nevada, USA";
                content = "Classified";
                sender = "Classified";
                receiver = "N/A";
                break;
            case "3":
                addr = "Amazon DE CGN9 (warehouse)";
                content = "Hololens 2";
                sender = "N/A";
                receiver = "N/A";
                break;
            case "":
                str = "Waiting for new scan...";
                break;
            default:
                defaut = true;
                break;
        }
        if (defaut)
        {
            str = "Waiting for new scan...";
        }
        else
        {
        str += "Address: " + addr + Environment.NewLine;
        str += "Content: " + content + Environment.NewLine;
        str += "Receiver: " + receiver + Environment.NewLine;
        str += "Sender: " + sender + Environment.NewLine;
        }
        TextData.text = str;
    }
    public void exitApp()
    {
        Debug.Log("exit app!");
        Application.Quit();
    }
}
