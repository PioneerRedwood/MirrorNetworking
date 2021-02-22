using UnityEngine;
using Mirror;

namespace Redwood
{
    public class Player : NetworkBehaviour
    {
        // player 구분 태그
        private PlayerTag _tag;

        public void SetTag(PlayerTag tag)
        {
            _tag = tag;
        }

        void FixedUpdate()
        {
            if(Input.GetMouseButtonDown(0))
            {
                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Physics.Raycast(ray, out hit);

                if(hit.collider != null)
                {
                    if(hit.collider.GetComponent<GamePlay.Tile>())
                    {
                        hit.collider.GetComponent<GamePlay.Tile>().OnTileClicked(_tag);
                    }
                }
            }
        }
    }
}
