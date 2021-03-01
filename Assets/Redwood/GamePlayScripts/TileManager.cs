using UnityEngine;
using Mirror;

namespace Redwood.GamePlay
{
    public class TileManager : NetworkBehaviour
    {
        [SerializeField] private Tile[,] _tiles;
        [SerializeField] private Vector2Int _tileSize;
        [SerializeField] private GameObject _tilePrefab;
        [Tooltip("타일 간격 설정값")]
        [Range(0, 1)]
        [SerializeField] private float _tileIntervalOffset;

        #region Tile Editing

        public void CreateTiles()
        {
            RemoveTiles();

            _tiles = new Tile[_tileSize.x, _tileSize.y];

            for (int x = 0; x < _tileSize.x; ++x)
            {
                for (int y = 0; y < _tileSize.y; ++y)
                {
                    GameObject tile = Instantiate(_tilePrefab, transform.position, transform.rotation);
                    tile.transform.SetParent(transform);
                    tile.name = $"Tile {x}, {y}";
                    _tiles[x, y] = tile.GetComponent<Tile>();
                    tile.transform.localPosition = (new Vector3(x, y, 0) * _tileIntervalOffset);
                }
            }

            // 화면 중앙에 타일들이 위치하도록 
            transform.position = Vector3.zero;
            transform.position -= new Vector3(
                (transform.GetChild(transform.childCount - 1).transform.position.x - transform.GetChild(0).transform.position.x) / 2,
                (transform.GetChild(transform.childCount - 1).transform.position.y - transform.GetChild(0).transform.position.y) / 2, 0);
        }

        public void RemoveTiles()
        {
            for (int i = transform.childCount - 1; i >= 0; --i)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            _tiles = null;
        }

        #endregion

        public void InitTiles()
        {
            foreach(Tile tile in GetComponentsInChildren<Tile>())
            {
                tile?.InitTile();
            }
        }
    }
}


