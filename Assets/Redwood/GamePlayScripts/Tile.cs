using System.Collections;
using UnityEngine;

namespace Redwood.GamePlay
{
    public enum TileFlag
    {
        // 나중에 melee를 위해 enum을 다양하게 넣어둠
        None, White, Black, Red, Green, Blue
    }

    public class Tile : Mirror.NetworkBehaviour
    {
        TileFlag flag = TileFlag.None;
        bool _isRollin = false;

        public void OnTileClicked(PlayerTag tag)
        {
            if(!_isRollin)
            {
                _isRollin = true;
                StartCoroutine(nameof(Rollin), tag);
            }
        }

        IEnumerator Rollin(PlayerTag tag)
        {
            yield return new WaitForSeconds(0.1f);

            GetComponent<Animator>().SetBool("Set", true);

            if (tag == PlayerTag.White)
            {
                GetComponentInChildren<SpriteRenderer>().color = Color.white;
            }
            else if(tag == PlayerTag.Black)
            {
                GetComponentInChildren<SpriteRenderer>().color = Color.black;
            }
            _isRollin = false;
        }
    }
}
