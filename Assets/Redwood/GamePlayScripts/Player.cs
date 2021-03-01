using UnityEngine;
using Mirror;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Redwood
{
    // player 구분 태그
    public enum Flag
    {
        None, Red, Green, Blue
    }

    public class Player : NetworkBehaviour
    {
        [SyncVar]
        public Flag _flag = Flag.None;
        [SyncVar]
        bool _isReady = false;
        
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
                    // 동일한 netId가 아니고 색이 바뀐 상태가 아니라면 바꿔주기
                    if ((obj.GetComponent<NetworkIdentity>().netId == netId) && (_flag != Flag.None))
                    {
                        ChangeFlagColor(obj, obj.GetComponent<Player>()._flag);
                        break;
                    }
                }
            }
        }
        private void Start()
        {
            if(isLocalPlayer)
            {
                _isReady = false;
                GameObject.Find("ReadyButton").GetComponentInChildren<Text>().text = "Not ready";
                GameObject.Find("ReadyButton").GetComponent<Button>().onClick.AddListener(gameObject.GetComponent<Player>().Ready);
            }
        }

        // ready 상태 설정 안됨
        public void Ready()
        {
            if (isLocalPlayer)
            {
                if (_isReady)
                {
                    SetReady(out _isReady, false);
                    GameObject.Find("ReadyButton").GetComponentInChildren<Text>().text = "Not ready";
                }
                else
                {
                    SetReady(out _isReady, true);
                    GameObject.Find("ReadyButton").GetComponentInChildren<Text>().text = "Ready";
                }
            }
        }

        void SetReady(out bool oldValue, bool newValue)
        {
            oldValue = newValue;
        }

        private void ChangeFlagColor(GameObject player, Flag flag)
        {
            switch (flag)
            {
                case Flag.None:
                    break;
                case Flag.Red:
                    if (player.GetComponentInChildren<SpriteRenderer>().color != Color.red)
                    {
                        player.GetComponentInChildren<SpriteRenderer>().color = Color.red;
                    }
                    break;
                case Flag.Green:
                    break;
                case Flag.Blue:
                    if (player.GetComponentInChildren<SpriteRenderer>().color != Color.blue)
                    {
                        player.GetComponentInChildren<SpriteRenderer>().color = Color.blue;
                    }
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
