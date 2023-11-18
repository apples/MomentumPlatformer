using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class GenerateMountains : EditorWindow
{
    public Terrain terrain;
    public int number;
    public float height;
    public float falloff = .02f;
    public bool withHeightAdjust;

    [MenuItem("Tools/Generate Mountains")]
    public static void ShowWindow()
    {
        GetWindow<GenerateMountains>("Generate Mountains");
    }

    void OnGUI()
    {
        GUILayout.Label("Curl Edges ", EditorStyles.boldLabel);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain Square", terrain, typeof(Terrain), true);
        number = EditorGUILayout.IntField("Number", number);
        height = EditorGUILayout.Slider("Height", height, 0, 1);
        falloff = EditorGUILayout.FloatField("Falloff per step", falloff);
        withHeightAdjust = EditorGUILayout.ToggleLeft("Adjust Height?", withHeightAdjust);

        if (GUILayout.Button("Go For It"))
        {
            Generate();
        }
    }

    private void Generate()
    {
        int width = terrain.terrainData.heightmapResolution; // just gonna assume squares
        float[,] newHeights = terrain.terrainData.GetHeights(0, 0, width, width);

        if (withHeightAdjust) //just tacks on more space on top and under existing terrain
        {
            for (int i = 0; i < width; i++) // honestly surprised there isn't some existing built in method for this
            {
                for (int j = 0; j < width; j++)
                {
                    newHeights[i, j] = (newHeights[i, j] + height) / ((4 * height) + 1);
                }
            }

            terrain.transform.position -= new Vector3(0, terrain.terrainData.size.y * height, 0);
            terrain.terrainData.size = new Vector3(terrain.terrainData.size.x, terrain.terrainData.size.y + (terrain.terrainData.size.y * height * 4), terrain.terrainData.size.z);
        }

        //int row = 0;
        float currentOffset = height;
        float currentFalloff = falloff;
        Vector2 originCoord = new Vector2();

        for(int heightNum = 0; heightNum < number; heightNum++)
        {
            currentOffset = Random.Range(height / 3, height);
            currentFalloff = falloff * Random.Range(.90f, 1.10f);

            originCoord = new Vector2(Random.Range(0, width), Random.Range(0, width));
            var estimatedSteps = Mathf.Floor(currentOffset / currentFalloff);

            while (originCoord.x - estimatedSteps < 0 || originCoord.x + estimatedSteps > width || originCoord.y - estimatedSteps < 0 || originCoord.y + estimatedSteps > width)
            {
                originCoord = new Vector2(Random.Range(0, width), Random.Range(0, width));
                estimatedSteps = Mathf.Floor(currentOffset / currentFalloff);
            }

            newHeights[(int)originCoord.x, (int)originCoord.y] += currentOffset;
            currentOffset -= currentFalloff;

            int steps = 1;
            int interiorSteps = 0;
            while (currentOffset > 0)
            {
                while(interiorSteps > -1)
                {
                    newHeights[(int)originCoord.x + steps - interiorSteps, (int)originCoord.y - interiorSteps] += currentOffset;
                    newHeights[(int)originCoord.x - steps + interiorSteps, (int)originCoord.y + interiorSteps] += currentOffset;
                    newHeights[(int)originCoord.x + interiorSteps, (int)originCoord.y + steps - interiorSteps] += currentOffset;
                    newHeights[(int)originCoord.x - interiorSteps, (int)originCoord.y - steps + interiorSteps] += currentOffset;
                    interiorSteps--;
                }

                currentOffset -= currentFalloff;
                interiorSteps = steps;
                steps++;
            }
        }

        terrain.terrainData.SetHeights(0, 0, newHeights);
    }
}
