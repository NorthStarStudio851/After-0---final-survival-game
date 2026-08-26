using UnityEngine;

public class TerrainGridSystem : MonoBehaviour
{
    [Header("Terrain References")]
    [SerializeField] private Terrain terrain;
    
    [Header("GridDimensions (Target Size 255)")]
    [SerializeField] private int gridWidth = 51; //51cells * 5m = 255m
    [SerializeField] private int gridHeight = 51; //51cells * 5 = 255m
    [SerializeField] private float cellSize = 5f; //5m per cell

    [Header("GizmosVisuals")]
    [SerializeField] private Color gridColor = Color.green;

    private Vector3 terrainOrigin;

    private void Start()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if(terrain != null)
        {
            terrainOrigin = terrain.transform.position;
        }
    }

    //Calculate the 3D World Position of a call, clamped to terrain height 

    public Vector3 GetCellWorldPosition(int x, int z)
    {
        if (terrain == null) return Vector3.zero;

        float xPos = terrainOrigin.x + (x * cellSize) +(cellSize / 2f);
        float zPos = terrainOrigin.z + (z * cellSize) +(cellSize / 2f);

        //SampleHeight retrieves the exact Y elevation from Terrain at X, Z 
        float yPos = terrain.SampleHeight(new Vector3(xPos, 0, zPos)) +terrainOrigin.y;

        return new Vector3(xPos, yPos, zPos);
    }

        //Converts a World Position into grid coordinates

    public Vector2Int GetGridCoords(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt((worldPosition.x - terrainOrigin.x) /cellSize);
             int z = Mathf.FloorToInt((worldPosition.z - terrainOrigin.z) /cellSize); 
         
            x = Mathf.Clamp(x, 0, gridWidth - 1);
            z = Mathf.Clamp(z, 0, gridHeight- 1);

            return new Vector2Int(x,z);
        }
        //Draws the grid inside the SceneView for editor debugging

        private void OnDrawGizmos()
        {
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            terrainOrigin =terrain.transform.position;
            Gizmos.color = gridColor;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int z =0; z < gridHeight; z++)
            {
                Vector3 cellCenter =GetCellWorldPosition(x, z);
                Gizmos.DrawWireCube(cellCenter, new Vector3(cellSize, 0.05f, cellSize));
            }

        }
    }
}
