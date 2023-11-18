using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class CurlEdges : EditorWindow
{
    public Terrain terrain;
    public float height;
    public float falloff = 1;
    public bool withHeightAdjust;

    [MenuItem("Tools/Curl Edges")]
    public static void ShowWindow()
    {
        GetWindow<CurlEdges>("Curl Edges");
    }

    void OnGUI()
    {
        GUILayout.Label("Curl Edges ", EditorStyles.boldLabel);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain Square", terrain, typeof(Terrain), true);
        height = EditorGUILayout.Slider("Height", height, 0, 1);
        falloff = EditorGUILayout.FloatField("Falloff", falloff);
        withHeightAdjust = EditorGUILayout.ToggleLeft("Adjust Height?", withHeightAdjust);

        if (GUILayout.Button("Go For It"))
        {
            Curl();
        }
    }

    private void Curl()
    {
        var width = terrain.terrainData.heightmapResolution; // just gonna assume squares
        float[,] newHeights = terrain.terrainData.GetHeights(0, 0, width, width);
        float adjustedHeight = height / ((2 * height) + 1);

        if (withHeightAdjust) //just tacks on more space on top and under existing terrain
        {
            for (int i = 0; i < width; i++) // honestly surprised there isn't some existing built in method for this
            {
                for (int j = 0; j < width; j++)
                {
                    newHeights[i, j] = (newHeights[i, j] + height) / ((2 * height) + 1);
                }
            }

            terrain.transform.position -= new Vector3(0, terrain.terrainData.size.y * height, 0);
            terrain.terrainData.size = new Vector3(terrain.terrainData.size.x, terrain.terrainData.size.y + (terrain.terrainData.size.y * height * 2), terrain.terrainData.size.z);
            
        }
        
        int row = 0;
        float currentOffset = adjustedHeight;

        while(currentOffset > .01)
        {
            for (int i = 0; i < width; i++)
            {
                newHeights[row, i] += currentOffset;
                newHeights[width - row - 1, i] += currentOffset;
            }
            row++;
            currentOffset -= currentOffset / 2 * falloff;
        }

        terrain.terrainData.SetHeights(0, 0, newHeights);
    }
}
