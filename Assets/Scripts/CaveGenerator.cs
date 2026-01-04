using UnityEngine;
using System.Collections.Generic;

public class CaveGenerator : MonoBehaviour
{
    private Vector3Int worldSize;
    private float noiseScale;
    private float threshold;

    public CaveGenerator(int width, int height, int depth, float noiseScale, float threshold)
    {
        worldSize = new Vector3Int(width, height, depth);
        this.noiseScale = noiseScale;
        this.threshold = threshold;
    }

    public (Voxel3[,,], GameObject[,,]) Generate(GameObject wallPrefab)
    {
        Vector3 offset = new Vector3(
            Random.Range(0f, 10000f),
            Random.Range(0f, 10000f),
            Random.Range(0f, 10000f)
        );

        Voxel3[,,] voxels = new Voxel3[worldSize.x, worldSize.y, worldSize.z];
        GameObject[,,] wallObjects = new GameObject[worldSize.x, worldSize.y, worldSize.z];

        for (int x = 0; x < worldSize.x; x++)
        {
            for (int y = 0; y < worldSize.y; y++)
            {
                for (int z = 0; z < worldSize.z; z++)
                {
                    float nx = (x + offset.x) / noiseScale;
                    float ny = (y + offset.y) / noiseScale;
                    float nz = (z + offset.z) / noiseScale;

                    float noise =
                        Mathf.PerlinNoise(nx, ny) +
                        Mathf.PerlinNoise(nx, nz) +
                        Mathf.PerlinNoise(ny, nz);

                    noise /= 3f;
                    if (noise > threshold)
                    {
                        voxels[x, y, z] = new Voxel3
                        {
                            isSolid = true,
                            position = new Vector3(x, y, z)
                        };
                        wallObjects[x, y, z] = GameObject.Instantiate(wallPrefab, new Vector3(x, y, z), Quaternion.identity);
                    }
                }
            }
        }

        return (voxels, wallObjects);
    }
}