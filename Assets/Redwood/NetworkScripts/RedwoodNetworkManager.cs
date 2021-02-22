using UnityEngine;
using Mirror;

namespace Redwood
{
    // player 구분 태그
    public enum PlayerTag
    {
        White, Black, Red, Green, Blue
    }

    public class RedwoodNetworkManager : NetworkManager
    {
        public Transform whiteFlag;
        public Transform blackFlag;

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            Transform start = numPlayers == 0 ? blackFlag : whiteFlag;
            GameObject player = Instantiate(playerPrefab, start.position, start.rotation);

            if(start == whiteFlag)
            {
                player.GetComponent<Player>().SetTag(PlayerTag.White);
                player.GetComponentInChildren<SpriteRenderer>().sprite = Resources.Load<Sprite>("WhiteFlag");
            }
            else
            {
                player.GetComponent<Player>().SetTag(PlayerTag.Black);
                player.GetComponentInChildren<SpriteRenderer>().sprite = Resources.Load<Sprite>("BlackFlag");
            }
            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);
        }
    }
}

