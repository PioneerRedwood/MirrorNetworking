// 2021.02.22 편집
// 학습용으로 만들어졌으므로 사용할 수 없음

using System;
using UnityEngine;

public class RedwoodNetworkBehaviour : MonoBehaviour
{
    Redwood.Client client = new Redwood.Client();
    Redwood.Server server = new Redwood.Server();

    private void Awake()
    {
        // 윈도우 창 위로 집중되지 않아도 수신하기 위해 업데이트
        Application.runInBackground = true;

        client.OnConnected = () => Debug.Log("Client Connected");
        client.OnData = (message) => Debug.Log($"Client Data: {BitConverter.ToString(message.Array, message.Offset, message.Count)}");
        client.OnDisconnected = () => Debug.Log("Client Disconnected");

        server.OnConnected = (connectionId) => Debug.Log(connectionId + " Connected");
        server.OnData = (connectionId, message) => Debug.Log($"{connectionId} Data: {BitConverter.ToString(message.Array, message.Offset, message.Count)}");
        server.OnDisconnected = (connectionId) => Debug.Log(connectionId + " Disconnected");
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (client.Connected)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                client.Send(new ArraySegment<byte>(new byte[] { 0x1 }));
            }
        }

        client.Tick(100);


        if (server.Active)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                server.Send(0, new ArraySegment<byte>(new byte[] { 0x2 }));
            }
        }

        server.Tick(100);
    }

    private void OnGUI()
    {
        GUI.enabled = !client.Connected;
        if (GUI.Button(new Rect(0, 0, 120, 20), "Connect client"))
        {
            client.Connect("localhost", 9000);
        }

        GUI.enabled = client.Connected;
        if (GUI.Button(new Rect(130, 0, 120, 20), "Disconnected client"))
        {
            client.Disconnect();
        }

        GUI.enabled = !server.Active;
        if (GUI.Button(new Rect(0, 25, 120, 20), "Start server"))
        {
            server.Start(9000);
        }

        GUI.enabled = server.Active;
        if (GUI.Button(new Rect(130, 25, 120, 20), "Stop server"))
        {
            server.Stop();
        }

        GUI.enabled = true;
    }

    private void OnApplicationQuit()
    {
        client.Disconnect();
        server.Stop();
    }
}
