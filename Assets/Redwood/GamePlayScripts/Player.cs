using UnityEngine;
using Mirror;
using System.Collections.Generic;

namespace Redwood
{
    // player 구분 태그
    public enum Flag
    {
        None, White, Black, Red, Blue
    }

    public class Player : NetworkBehaviour
    {
        [SyncVar]
        public Flag _flag = Flag.None;
        
        public override void OnStartClient()
        {
            // Server is active in here
            if (NetworkServer.localClientActive)
            {
                ChangeFlagColor(gameObject, _flag);
                return;
            }
            else
            {
                foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Player"))
                {
                    if ((obj.GetComponent<NetworkIdentity>().netId == netId) && (_flag != Flag.None))
                    {
                        ChangeFlagColor(obj, obj.GetComponent<Player>()._flag);
                        break;
                    }
                }
            }
        }

        private void ChangeFlagColor(GameObject player, Flag flag)
        {
            switch (flag)
            {
                case Flag.None:
                    break;
                case Flag.White:
                    // red for debug
                    if (player.GetComponentInChildren<SpriteRenderer>().color != Color.red)
                    {
                        player.GetComponentInChildren<SpriteRenderer>().color = Color.red;
                    }
                    break;
                case Flag.Black:
                    // blue for debug
                    if (player.GetComponentInChildren<SpriteRenderer>().color != Color.blue)
                    {
                        player.GetComponentInChildren<SpriteRenderer>().color = Color.blue;
                    }
                    break;
                case Flag.Red:
                    break;
                case Flag.Blue:
                    break;
                default:
                    break;
            }
        }

        public void SetFlag(Flag tag)
        {
            _flag = tag;
        }

        void FixedUpdate()
        {
            if (isLocalPlayer)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    RaycastHit hit;
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    Physics.Raycast(ray, out hit);

                    if (hit.collider != null)
                    {
                        if (hit.collider.GetComponent<GamePlay.Tile>())
                        {
                            OnTileClick(hit.collider.GetComponent<GamePlay.Tile>());
                        }
                    }
                }
            }
        }

        [Command]
        void OnTileClick(GamePlay.Tile tile)
        {
            tile.OnTileClicked(_flag);
        }
    }
}
