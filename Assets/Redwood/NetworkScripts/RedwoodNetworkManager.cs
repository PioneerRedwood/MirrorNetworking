using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Text;
using System;

namespace Redwood
{
    public class RedwoodNetworkManager : NetworkManager
    {
        public Transform whiteFlag;
        public Transform blackFlag;

        // -- Host일 경우 Callbacks 호출 순서 --
        // https://docs.unity3d.com/2019.3/Documentation/Manual/NetworkManagerCallbacks.html
        // OnStartHost() -> OnStartServer() -> OnServerConnect() -> OnStartClient() -> OnClientConnect() 
        // -> OnServerSceneChanged() -> OnServerReady() -> OnServerAddPlayer() -> OnClientSceneChanged()

        public override void OnStartHost()
        {
            base.OnStartHost();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
        }

        public override void OnServerConnect(NetworkConnection conn)
        {
            
        }

        public override void OnStartClient()
        {

        }

        public override void OnClientConnect(NetworkConnection conn)
        {

        }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            if (numPlayers == maxConnections) // maxConnections: 5
            {
                return;
            }

            Transform start;
            if (numPlayers == 0)
            {
                start = whiteFlag;
                if (playerPrefab.GetComponent<Player>() && playerPrefab.GetComponentInChildren<SpriteRenderer>().sprite)
                {
                    playerPrefab.GetComponent<Player>().SetFlag(Flag.White);
                }
            }
            else
            {
                start = blackFlag;
                if (playerPrefab.GetComponent<Player>() && playerPrefab.GetComponentInChildren<SpriteRenderer>().sprite)
                {
                    playerPrefab.GetComponent<Player>().SetFlag(Flag.Black);
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

