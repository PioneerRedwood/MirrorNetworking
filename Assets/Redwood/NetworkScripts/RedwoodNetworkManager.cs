using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Text;
using System;

namespace Redwood
{
    public class RedwoodNetworkManager : NetworkManager
    {
        public Transform _redFlag;
        public Transform _blueFlag;
        public GamePlay.TileManager _tileManager;
        public GameObject _canvasObject;

        // -- Host일 경우 Callbacks 호출 순서 --
        // https://docs.unity3d.com/2019.3/Documentation/Manual/NetworkManagerCallbacks.html
        // OnStartHost() -> OnStartServer() -> OnServerConnect() -> OnStartClient() -> OnClientConnect() 
        // -> OnServerSceneChanged() -> OnServerReady() -> OnServerAddPlayer() -> OnClientSceneChanged()

        public override void OnStartHost() { }

        public override void OnStopHost()
        {
            base.OnStopHost();
            _tileManager.InitTiles();
            _canvasObject.SetActive(false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _tileManager.InitTiles();

        }

        public override void OnServerConnect(NetworkConnection conn) { }

        public override void OnStartClient()
        {
            Debug.Log("Set canvas enabled");
            _canvasObject.SetActive(true);
        }

        public override void OnClientConnect(NetworkConnection conn) { }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            if (numPlayers == maxConnections) // maxConnections: 5
            {
                return;
            }

            Transform start;
            if (numPlayers == 0)
            {
                start = _redFlag;
                if (playerPrefab.GetComponent<Player>() && playerPrefab.GetComponentInChildren<SpriteRenderer>().sprite)
                {
                    playerPrefab.GetComponent<Player>().SetFlag(Flag.Red);
                }
            }
            else
            {
                start = _blueFlag;
                if (playerPrefab.GetComponent<Player>() && playerPrefab.GetComponentInChildren<SpriteRenderer>().sprite)
                {
                    playerPrefab.GetComponent<Player>().SetFlag(Flag.Blue);
                }
            }
            GameObject player = Instantiate(playerPrefab, start.position, start.rotation);

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);

        }
    }
}

