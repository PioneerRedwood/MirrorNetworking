using UnityEngine;
using UnityEditor;

using Redwood.GamePlay;

[CustomEditor(typeof(TileManager))]
[CanEditMultipleObjects]
public class TileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        TileManager manager = target as TileManager;

        if (GUILayout.Button("타일 생성"))
        {
            manager.CreateTiles();
        }

        if(GUILayout.Button("타일 제거"))
        {
            manager.RemoveTiles();
        }

    }
}
