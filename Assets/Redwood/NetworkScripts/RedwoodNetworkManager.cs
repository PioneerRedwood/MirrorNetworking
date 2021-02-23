using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Redwood
{
    // player 구분 태그
    public enum PlayerTag
    {
        White, Black, Red, Blue
    }

    public class RedwoodNetworkManager : NetworkManager
    {
        public Transform[] _flagTransforms = new Transform[2];

        public Transform whiteFlag;
        public Transform blackFlag;

        public GameObject[] Players = new GameObject[2];

        public override void OnClientConnect(NetworkConnection conn)
        {
            // 새로운 서버에 접속 시 클라이언트에서 호출
            // 서버에 접속 시 서버와 연결된 클라이언트들의 정보를 받아서 씬에 전시해야 함
            // 서버와 연결돼있는 클라이언트의 정보들을 어디서 가져와야 할까
            base.OnClientConnect(conn);
            Debug.Log($"OnClientConnect() Total connetions: {NetworkServer.connections.Count}");


            for (int i = 0; i < NetworkServer.connections.Count; ++i)
            {
                if((NetworkServer.connections[i].identity != conn.identity) && (Players[i] == null))
                {
                    GameObject player = Instantiate(playerPrefab, _flagTransforms[i].position, _flagTransforms[i].rotation);

                    if (_flagTransforms[i] == whiteFlag)
                    {
                        player.GetComponent<Player>().SetTag(PlayerTag.White);
                        player.GetComponentInChildren<SpriteRenderer>().sprite = Resources.Load<Sprite>("WhiteFlag");
                        Players[i] = player;
                    }
                    else
                    {
                        player.GetComponent<Player>().SetTag(PlayerTag.Black);
                        player.GetComponentInChildren<SpriteRenderer>().sprite = Resources.Load<Sprite>("BlackFlag");
                        Players[i] = player;
                    }
                }
            }
        }

        public override void OnServerConnect(NetworkConnection conn)
        {
            // 새로운 클라이언트가 접속 되면 서버에서 호출
            // Host(Server + Client)일 경우에도 호출됨
            base.OnServerConnect(conn);
            Debug.Log($"OnServerConnect: {conn}");


        }

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            if(numPlayers == maxConnections ) // maxConnections: 5
            {
                return;
            }

            // 순서에 맞게 색 배정 및 서버에 추가
            Transform start;
            if (numPlayers == 0)
            {
                start = whiteFlag;
            }
            else
            {
                start = blackFlag;
            }
            GameObject player = Instantiate(playerPrefab, start.position, start.rotation);

            if(start == whiteFlag)
            {
                player.GetComponent<Player>().SetTag(PlayerTag.White);
                player.GetComponentInChildren<SpriteRenderer>().sprite = Resources.Load<Sprite>("WhiteFlag");
                Players[0] = player;
            }
            else
            {
                player.GetComponent<Player>().SetTag(PlayerTag.Black);
                player.GetComponentInChildren<SpriteRenderer>().sprite = Resources.Load<Sprite>("BlackFlag");
                Players[1] = player;
            }
            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);


        }
    }
}

