using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SplitTerrain : EditorWindow
{
    public GameObject terrain;
    public int chunkX = 1;
    public int chunkZ = 1;

    public int newRes = 1024;

    public string newName;

    [MenuItem("Tools/Split Terrain")]
    public static void ShowWindow()
    {
        GetWindow<SplitTerrain>("Split Terrain");
    }

    void OnGUI()
    {
        GUILayout.Label("Split Terrain", EditorStyles.boldLabel);
        terrain = (GameObject)EditorGUILayout.ObjectField("Terrain", terrain, typeof(GameObject), true);
        chunkX = EditorGUILayout.IntField("Chunk X", chunkX);
        chunkZ = EditorGUILayout.IntField("Chunk Z", chunkZ);
        newRes = EditorGUILayout.IntField("New Resolution", newRes);
        newName = EditorGUILayout.TextField("New Name", newName);

        if (GUILayout.Button("Split"))
        {
            Split();
        }
    }

    private void Split()
    {
        if (terrain == null)
        {
            Debug.LogError("Terrain is null");
            return;
        }

        TerrainData terrainData = terrain.GetComponent<Terrain>().terrainData;

        int res = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, res, res);

        float[,] newHeights = new float[newRes, newRes];

        int chunkSizeX = res / chunkX;
        int chunkSizeZ = res / chunkZ;

        Debug.Log("Total trees = " + terrainData.treeInstanceCount);
        Debug.Log("Resolution = " + res);

        TerrainData[,] newTerrainData = new TerrainData[chunkX, chunkZ];

        for (int x = 0; x < chunkX; x++)
        {
            for (int z = 0; z < chunkZ; z++)
            {
                var newTerrain = Instantiate(terrain, new Vector3(terrain.transform.position.x + x * terrainData.size.x / chunkX, 0, terrain.transform.position.z + z * terrainData.size.z / chunkZ), Quaternion.identity);
                newTerrain.name = "Terrain " + x + " " + z;
                newTerrain.GetComponent<Terrain>().terrainData = new TerrainData();
                newTerrain.GetComponent<Terrain>().terrainData.treePrototypes = terrainData.treePrototypes;
                newTerrain.GetComponent<Terrain>().terrainData.RefreshPrototypes();
                newTerrain.GetComponent<Terrain>().terrainData.heightmapResolution = newRes;

                newTerrainData[x, z] = newTerrain.GetComponent<Terrain>().terrainData;

                AssetDatabase.CreateAsset(newTerrain.GetComponent<Terrain>().terrainData, $"Assets/Terrain/TerrainData_{newName}_{x}_{z}.asset");
            }
        }

        List<TreeInstance>[,] newTrees = new List<TreeInstance>[chunkX, chunkZ];

        for (int x = 0; x < chunkX; x++)
        {
            for (int z = 0; z < chunkZ; z++)
            {
                newTrees[x, z] = new List<TreeInstance>(terrainData.treeInstanceCount);
            }
        }

        for (int i = 0; i < terrainData.treeInstanceCount; i++)
        {
            TreeInstance tree = terrainData.treeInstances[i];
            Vector3 pos = tree.position;

            int cx = Mathf.Clamp((int)(pos.x * chunkX), 0, chunkX - 1);
            int cz = Mathf.Clamp((int)(pos.z * chunkZ), 0, chunkZ - 1);

            tree.position.x = (pos.x - cx * (1f / chunkX)) * chunkX;
            tree.position.z = (pos.z - cz * (1f / chunkZ)) * chunkZ;

            newTrees[cx, cz].Add(tree);
        }

        for (int x = 0; x < chunkX; x++)
        {
            for (int z = 0; z < chunkZ; z++)
            {
                float min = 999;
                float max = 0;
                for (int i = 0; i < newRes; i++)
                {
                    for (int j = 0; j < newRes; j++)
                    {
                        if (newRes > chunkX || newRes > chunkZ)
                        {
                            // a--b
                            // |  |
                            // c--d
                            var a = heights[Mathf.Clamp(z * chunkSizeZ + j * chunkSizeZ / newRes, 0, heights.GetLength(0)-1), Mathf.Clamp(x * chunkSizeX + i * chunkSizeX / newRes, 0, heights.GetLength(1)-1)];
                            var b = heights[Mathf.Clamp(z * chunkSizeZ + j * chunkSizeZ / newRes, 0, heights.GetLength(0)-1), Mathf.Clamp(x * chunkSizeX + i * chunkSizeX / newRes + 1, 0, heights.GetLength(1)-1)];
                            var c = heights[Mathf.Clamp(z * chunkSizeZ + j * chunkSizeZ / newRes + 1, 0, heights.GetLength(0)-1), Mathf.Clamp(x * chunkSizeX + i * chunkSizeX / newRes, 0, heights.GetLength(1)-1)];
                            var d = heights[Mathf.Clamp(z * chunkSizeZ + j * chunkSizeZ / newRes + 1, 0, heights.GetLength(0)-1), Mathf.Clamp(x * chunkSizeX + i * chunkSizeX / newRes + 1, 0, heights.GetLength(1)-1)];

                            float W(float w) => (float)(w - (int)(w));

                            var w_x = W(i * chunkSizeX / (float)newRes);
                            var w_y = W(j * chunkSizeZ / (float)newRes);

                            newHeights[j, i] = Mathf.Lerp(Mathf.Lerp(a, b, w_x), Mathf.Lerp(c, d, w_x), w_y);
                        }
                        else
                        {
                            newHeights[j, i] = heights[z * chunkSizeZ + j * chunkSizeZ / newRes, x * chunkSizeX + i * chunkSizeX / newRes];
                        }

                        if (newHeights[j, i] < min) {
                                min = newHeights[j, i];
                            }
                            if (newHeights[j, i] > max) {
                                max = newHeights[j, i];
                            }
                    }
                }
                Debug.Log($"Min ({x},{z}) = {min}");
                Debug.Log($"Max ({x},{z}) = {max}");

                newTerrainData[x, z].size = new Vector3(terrainData.size.x / chunkX, terrainData.size.y, terrainData.size.z / chunkZ);
                newTerrainData[x, z].SetHeights(0, 0, newHeights);
                newTerrainData[x, z].SetTreeInstances(newTrees[x, z].ToArray(), true);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
