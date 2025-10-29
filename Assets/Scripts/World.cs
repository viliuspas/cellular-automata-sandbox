using System;
using System.Linq;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class World : MonoBehaviour
{
    private const byte maxFluidLevel = 127;

    private Voxel[,,] voxels;
    private Voxel emptyVoxel = new Voxel
    {
        isSolid = false,
        fluidLevel = 0,
        voxelObject = null,
        isSource = false,
        wasUpdated = false
    };

    public Vector3Int worldSize = new Vector3Int(5, 10, 4);
    public GameObject solidPrefab;
    public GameObject waterPrefab;

    void Start()
    {
        InitVoxels();
        RenderVoxels();
    }

    void Update()
    {
        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    Voxel voxel = voxels[x, y, z];
                    Debug.Log($"checking0: {x}, {y}, {z}");
                    if (IsVoxelEmpty(voxel)) continue;
                    if (voxel.isSolid || voxel.wasUpdated)
                    {
                        Debug.Log($"checking1: {x}, {y}, {z}");
                        voxels[x, y, z].wasUpdated = false;
                        continue;
                    }

                    Debug.Log($"checking2: {x}, {y}, {z}");
                    Water_CheckBellow(voxel, x, y, z);
                    voxels[x, y, z].wasUpdated = false;
                }
            }
        }

        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    voxels[x, y, z].wasUpdated = false;
                }
            }
        }
    }

    private void InitVoxels()
    {
        voxels = new Voxel[worldSize.x, worldSize.y, worldSize.z];

        // platform
        for (int x = 0; x < worldSize.x; x++)
        {
            for (int z = 0; z < worldSize.z; z++)
            {
                voxels[x, 2, z] = new Voxel
                {
                    isSolid = true,
                    fluidLevel = 0,
                    voxelObject = solidPrefab
                };
            }
        }

        //// water
        //voxels[6, 5, 6] = new Voxel
        //{
        //    isSolid = false,
        //    fluidLevel = maxFluidLevel,
        //    isSource = false,
        //    voxelObject = waterPrefab
        //};

        voxels[8, 6, 8] = new Voxel
        {
            isSolid = false,
            fluidLevel = maxFluidLevel,
            isSource = false,
            voxelObject = waterPrefab
        };
    }

    private void RenderVoxels()
    {
        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    Voxel voxel = voxels[x, y, z];
                    if (IsVoxelEmpty(voxel))
                        continue;

                    Vector3 position = new Vector3(x, y, z);
                    GameObject voxelObj = Instantiate(voxel.voxelObject, position, Quaternion.identity);
                    voxel.voxelObject = voxelObj;
                    voxels[x, y, z] = voxel;
                }
            }
        }
    }

    private void Water_CheckBellow(Voxel voxel, int x, int y, int z)
    {
        Vector3Int newPos = new Vector3Int(x, y - 1, z);
        if (newPos.y >= 0 && CanVoxelBeFilled(voxel, voxels[newPos.x, newPos.y, newPos.z]))
        {
            if (voxel.isSource)
            {
                voxels[newPos.x, newPos.y, newPos.z] = CreateWaterVoxel(newPos);
            }
            else
            {
                voxel.voxelObject.transform.position = newPos;
                voxels[x, y, z] = emptyVoxel;
                voxels[newPos.x, newPos.y, newPos.z] = voxel;
            }
        }
        else
        {
            Water_CheckSides(voxels[x, y, z], x, y, z);
        }
    }

    private void Water_CheckSides(Voxel voxel, int x, int y, int z)
    {
        if (voxel.fluidLevel <= 1) return;
     
        Voxel[] neighbours = new Voxel[4];

        if (x + 1 < worldSize.x && CanVoxelBeFilled(voxel, voxels[x + 1, y, z]))
        {
            neighbours[0] = voxels[x + 1, y, z];
            neighbours[0].position = new Vector3(x + 1, y, z);
        }

        if (x - 1 >= 0 && CanVoxelBeFilled(voxel, voxels[x - 1, y, z]))
        {
            neighbours[1] = voxels[x - 1, y, z];
            neighbours[1].position = new Vector3(x - 1, y, z);
        }

        if (z + 1 < worldSize.z && CanVoxelBeFilled(voxel, voxels[x, y, z + 1]))
        {
            neighbours[2] = voxels[x, y, z + 1];
            neighbours[2].position = new Vector3(x, y, z + 1);
        }

        if (z - 1 >= 0 && CanVoxelBeFilled(voxel, voxels[x, y, z - 1]))
        {
            neighbours[3] = voxels[x, y, z - 1];
            neighbours[3].position = new Vector3(x, y, z - 1);
        }

        neighbours = neighbours
            .Where(v => v.position != Vector3.zero)
            .OrderBy(v => v.fluidLevel).ToArray();

        byte fluidLevelToTransfer = (byte)(voxel.fluidLevel / (neighbours.Length + 1));

        foreach (var neighbour in neighbours)
        {
            DistributeFlow((int)neighbour.position.x, y, (int)neighbour.position.z);
            if (voxel.fluidLevel <= 1) return;
        }
        DebugLogVoxelAt(x, y, z);
        void DistributeFlow(int newX, int newY, int newZ)
        {
            if (!voxel.isSource)
            {
                voxel.fluidLevel -= fluidLevelToTransfer;
                voxels[x, y, z] = RenderedVoxel(voxel);
            }

            if (IsVoxelEmpty(voxels[newX, newY, newZ]))
            {
                Voxel newVoxel = CreateWaterVoxel(newX, newY, newZ, fluidLevelToTransfer);
                voxels[newX, newY, newZ] = newVoxel;
            }
            else
            {
                voxels[newX, newY, newZ].fluidLevel += fluidLevelToTransfer;
                voxels[newX, newY, newZ] = RenderedVoxel(voxels[newX, newY, newZ]);
                //DebugLogVoxelAt(newX, newY, newZ);
            }

            if (voxel.fluidLevel <= 0)
            {
                Destroy(voxel.voxelObject);
                voxels[x, y, z] = emptyVoxel;
            }
        }
    }

    private void DebugLogVoxelAt(int x, int y, int z)
    {
        //if (x != 8 || z != 7) return;

        Voxel voxel = voxels[x, y, z];
        if (IsVoxelEmpty(voxel))
        {
            Debug.Log($"Voxel at {x},{y},{z} is empty");
        }
        else
        {
            Debug.Log($"Voxel at {x},{y},{z} - fluidLevel: {voxel.fluidLevel}, wasUpdated: {voxel.wasUpdated}");
        }
    }

    private static bool CanVoxelBeFilled(Voxel voxel, Voxel newVoxel)
    {
        return IsVoxelEmpty(newVoxel) || 
            (!newVoxel.isSolid && newVoxel.fluidLevel < maxFluidLevel && voxel.fluidLevel > newVoxel.fluidLevel);
    }

    private static bool IsVoxelEmpty(Voxel voxel)
    {
        return voxel.voxelObject == null;
    }

    private Voxel CreateWaterVoxel(int x, int y, int z, byte? fluidLevel = null)
    {
        return CreateWaterVoxel(new Vector3Int(x, y, z), fluidLevel);
    }

    private Voxel CreateWaterVoxel(Vector3Int pos, byte? fluidLevel = null)
    {
        if (fluidLevel == null)
            fluidLevel = maxFluidLevel;

        Voxel voxel = new Voxel
        {
            isSolid = false,
            fluidLevel = (byte)fluidLevel,
            voxelObject = Instantiate(waterPrefab, pos, Quaternion.identity),
            isSource = false,
            wasUpdated = true,
            position = pos
        };

        return RenderedVoxel(voxel);
    }

    private static Voxel RenderedVoxel(Voxel voxel)
    {
        float scaleY = voxel.fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        Vector3 pos = voxel.voxelObject.transform.position;
        voxel.voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxel.voxelObject.transform.position = new Vector3(pos.x, (float)Math.Ceiling(pos.y) - newY, pos.z);
        return voxel;
    }

    void OnDestroy()
    {
        if (voxels != null)
        {
            for (int z = 0; z < worldSize.z; z++)
            {
                for (int x = 0; x < worldSize.x; x++)
                {
                    for (int y = 0; y < worldSize.y; y++)
                    {
                        if (voxels[x, y, z].voxelObject != null)
                        {
                            Destroy(voxels[x, y, z].voxelObject);
                        }
                    }
                }
            }
        }
    }
}
