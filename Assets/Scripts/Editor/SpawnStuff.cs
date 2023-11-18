using System.IO;
using UnityEditor;
using UnityEngine;

public class SpawnStuff : EditorWindow
{
    public GameObject spawnItem;
    public Transform parent;
    public int amount = 100;
    public bool randomizeRotations = true;
    public bool onlyRotateSubtly = true;
    public bool randomizeDepth = true;
    public Terrain fromTerrain;
    public Terrain toTerrain;

    [MenuItem("Tools/Spawn Stuff In")]
    public static void ShowWindow()
    {
        GetWindow<SpawnStuff>("Spawn Stuff In");
    }

    void OnGUI()
    {
        GUILayout.Label("Spawn Stuff In", EditorStyles.boldLabel);
        spawnItem = (GameObject)EditorGUILayout.ObjectField("GameObject", spawnItem, typeof(GameObject), true);
        parent = (Transform)EditorGUILayout.ObjectField("Parent", parent, typeof(Transform), true);
        amount = EditorGUILayout.IntField("Amount", amount);
        fromTerrain = (Terrain)EditorGUILayout.ObjectField("From Terrain Tile (closer to origin)", fromTerrain, typeof(Terrain), true);
        toTerrain = (Terrain)EditorGUILayout.ObjectField("To Terrain Tile", toTerrain, typeof(Terrain), true);
        randomizeRotations = EditorGUILayout.ToggleLeft("Randomize Rotations", randomizeRotations);
        onlyRotateSubtly = EditorGUILayout.ToggleLeft("Only a bit though", onlyRotateSubtly);
        randomizeDepth = EditorGUILayout.ToggleLeft("Randomize Depth", randomizeDepth);

        if (GUILayout.Button("Spawn"))
        {
            SpawnEverything();
        }
    }

    private void SpawnEverything()
    {
        GameObject current;
        RaycastHit hit;
        float spawnItemSize = spawnItem.GetComponent<Renderer>().bounds.size.y;

        for(int i = 0; i < amount; i++)
        {
            current = Instantiate(spawnItem, 
                new Vector3(
                    Random.Range(fromTerrain.transform.position.x, toTerrain.transform.position.x + fromTerrain.terrainData.size.x),
                    10000,
                    Random.Range(fromTerrain.transform.position.y, toTerrain.transform.position.z + fromTerrain.terrainData.size.z)),
                randomizeRotations ? onlyRotateSubtly ? Quaternion.Euler(Random.Range(-30, 30), Random.Range(0, 360), Random.Range(-30, 30)) :
                    Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360)) :
                    Quaternion.Euler(0, 0, 0),
                parent);
            Physics.Raycast(current.transform.position, Vector3.down, out hit);
            current.transform.position = current.transform.position + new Vector3(0, -hit.distance + (randomizeDepth ? Random.Range(-.25f * spawnItemSize, .125f * spawnItemSize) : 0), 0);
        }
    }
}
