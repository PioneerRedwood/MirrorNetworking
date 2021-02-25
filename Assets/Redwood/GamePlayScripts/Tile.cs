using System.Collections;
using UnityEngine;
using Mirror;

namespace Redwood.GamePlay
{
    public class Tile : NetworkBehaviour
    {
        private Flag _prevFlag = Flag.None;

        [SyncVar]
        private bool _isRollin = false;

        [SyncVar]
        private Flag _currFlag = Flag.None;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if(isClientOnly && !IsInvoking(nameof(UpdateTileOnClient)))
            {
                InvokeRepeating(nameof(UpdateTileOnClient), 0.0f, 0.1f);
            }
        }

        [Client]
        void UpdateTileOnClient()
        {
            switch (_currFlag)
            {
                case Flag.None:
                    break;
                case Flag.White:
                    GetComponentInChildren<SpriteRenderer>().color = Color.red;
                    break;
                case Flag.Black:
                    GetComponentInChildren<SpriteRenderer>().color = Color.blue;
                    break;
                default:
                    break;
            }
        }

        public void OnTileClicked(Flag newFlag)
        {
            if (!_isRollin)
            {
                _isRollin = true;
                _prevFlag = _currFlag;
                _currFlag = newFlag;
                StartCoroutine(nameof(RollAndChange));
            }
        }

        IEnumerator RollAndChange()
        {
            _isRollin = false;

            switch (_currFlag)
            {
                case Flag.None:
                    break;
                case Flag.White:
                    GetComponentInChildren<SpriteRenderer>().color = Color.red;
                    break;
                case Flag.Black:
                    GetComponentInChildren<SpriteRenderer>().color = Color.blue;
                    break;
                default:
                    break;
            }

            yield return new WaitForSeconds(0.1f);

            RPCOnRollin();
        }

        [ClientRpc]
        void RPCOnRollin()
        {
            GetComponent<Animator>().SetTrigger("Roll");
        }
    }
}
