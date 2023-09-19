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
    public int newAlphamapRes = 1024;

    public string newName;

    [MenuItem("Tools/Split Terrain")]
    public static void ShowWindow()
    {
        GetWindow<SplitTerrain>("Split Terrain");
    }

    void OnGUI()
    {
        GUILayout.Label("Split Terrain", EditorStyles.boldLabel);
        terrain = (GameObject)EditorGUILayout.ObjectField("Terrain GameObject", terrain, typeof(GameObject), true);
        chunkX = EditorGUILayout.IntField("Chunk Count X", chunkX);
        chunkZ = EditorGUILayout.IntField("Chunk Count Z", chunkZ);
        newRes = EditorGUILayout.IntField("New Heightmap Resolution", newRes);
        newAlphamapRes = EditorGUILayout.IntField("New Alphamap Resolution", newAlphamapRes);
        newName = EditorGUILayout.TextField("New Folder Name", newName);

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
        int ares = terrainData.alphamapResolution;
        float[,,] alphamaps = terrainData.GetAlphamaps(0, 0, ares, ares);

        float[,] newHeights = new float[newRes, newRes];
        float[,,] newAlphamaps = new float[newAlphamapRes, newAlphamapRes, 4];

        int chunkHeightmapSizeX = (res - 1) / chunkX;
        int chunkHeightmapSizeZ = (res - 1) / chunkZ;

        int chunkAlphamapSizeX = (ares - 1) / chunkX;
        int chunkAlphamapSizeZ = (ares - 1) / chunkZ;

        Debug.Log("Total trees = " + terrainData.treeInstanceCount);
        Debug.Log("Resolution = " + res);

        TerrainData[,] newTerrainData = new TerrainData[chunkX, chunkZ];

        AssetDatabase.CreateFolder("Assets/Terrain", newName);

        for (int x = 0; x < chunkX; x++)
        {
            for (int z = 0; z < chunkZ; z++)
            {
                var newTerrain = Instantiate(terrain, new Vector3(terrain.transform.position.x + x * terrainData.size.x / chunkX, 0, terrain.transform.position.z + z * terrainData.size.z / chunkZ), Quaternion.identity);
                newTerrain.name = "Terrain " + x + " " + z;
                var newTerrainCom = newTerrain.GetComponent<Terrain>();
                newTerrainCom.terrainData = new TerrainData();
                newTerrainCom.terrainData.treePrototypes = terrainData.treePrototypes;
                newTerrainCom.terrainData.RefreshPrototypes();
                newTerrainCom.terrainData.heightmapResolution = newRes;
                newTerrainCom.terrainData.alphamapResolution = newAlphamapRes;
                newTerrainCom.terrainData.SetTerrainLayersRegisterUndo(terrainData.terrainLayers, "asdf");

                newTerrainData[x, z] = newTerrain.GetComponent<Terrain>().terrainData;

                newTerrain.GetComponent<TerrainCollider>().terrainData = newTerrainCom.terrainData;

                AssetDatabase.CreateAsset(newTerrain.GetComponent<Terrain>().terrainData, $"Assets/Terrain/{newName}/TerrainData_{x}_{z}.asset");
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

                // heightmap slice
                float chunkXorigin = chunkHeightmapSizeX * x;
                float chunkZorigin = chunkHeightmapSizeZ * z;

                for (int i = 0; i < newRes; i++)
                {
                    for (int j = 0; j < newRes; j++)
                    {
                        float sx = chunkXorigin + ((float)i / (float)(newRes - 1)) * (float)chunkHeightmapSizeX;
                        float sz = chunkZorigin + ((float)j / (float)(newRes - 1)) * (float)chunkHeightmapSizeZ;

                        var wx = sx - (float)Math.Truncate(sx);
                        var wz = sz - (float)Math.Truncate(sz);

                        if (sz < res - 1 && sx < res - 1)
                        {
                            // a--b
                            // |  |
                            // c--d
                            var a = heights[(int)sz, (int)sx];
                            var b = heights[(int)sz, (int)sx + 1];
                            var c = heights[(int)sz + 1, (int)sx];
                            var d = heights[(int)sz + 1, (int)sx + 1];

                            newHeights[j, i] = Mathf.Lerp(Mathf.Lerp(a, b, wx), Mathf.Lerp(c, d, wx), wz);
                        }
                        else
                        {
                            newHeights[j, i] = heights[(int)sz, (int)sx];
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

                // alphamap slice
                float chunkXAorigin = chunkAlphamapSizeX * x;
                float chunkZAorigin = chunkAlphamapSizeZ * z;

                for (int i = 0; i < newAlphamapRes; i++)
                {
                    for (int j = 0; j < newAlphamapRes; j++)
                    {
                        float sx = chunkXAorigin + ((float)i / (float)(newAlphamapRes - 1)) * (float)chunkAlphamapSizeX;
                        float sz = chunkZAorigin + ((float)j / (float)(newAlphamapRes - 1)) * (float)chunkAlphamapSizeZ;

                        var wx = sx - (float)Math.Truncate(sx);
                        var wz = sz - (float)Math.Truncate(sz);

                        for (int k = 0; k < 4; k++)
                        {
                            if (sz < ares - 1 && sx < ares - 1)
                            {
                                // a--b
                                // |  |
                                // c--d
                                var a = alphamaps[(int)sz, (int)sx, k];
                                var b = alphamaps[(int)sz, (int)sx + 1, k];
                                var c = alphamaps[(int)sz + 1, (int)sx, k];
                                var d = alphamaps[(int)sz + 1, (int)sx + 1, k];

                                newAlphamaps[j, i, k] = Mathf.Lerp(Mathf.Lerp(a, b, wx), Mathf.Lerp(c, d, wx), wz);
                            }
                            else
                            {
                                newAlphamaps[j, i, k] = alphamaps[(int)sz, (int)sx, k];
                            }
                        }
                    }
                }

                newTerrainData[x, z].size = new Vector3(terrainData.size.x / chunkX, terrainData.size.y, terrainData.size.z / chunkZ);
                newTerrainData[x, z].SetHeights(0, 0, newHeights);
                newTerrainData[x, z].SetAlphamaps(0, 0, newAlphamaps);
                newTerrainData[x, z].SetTreeInstances(newTrees[x, z].ToArray(), true);
            }
        }

        AssetDatabase.SaveAssets();
    }
}
