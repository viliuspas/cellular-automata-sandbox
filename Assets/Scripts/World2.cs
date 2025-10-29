using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;

public class World2 : MonoBehaviour
{
    public Vector3Int worldSize = new Vector3Int(15, 15, 15);
    public GameObject solidPrefab;
    public GameObject waterPrefab;

    private const int maxFluidLevel = 127;

    private Voxel[] voxels;
    private int numOfVoxels = 0;
    private int numOfWaterVoxels = 0;

    void Start()
    {
        InitVoxels();
        RenderVoxels();
    }

    void Update()
    {
        voxels = voxels
            .OrderByDescending(v => v.fluidLevel)
            .ToArray();

        for (int i = 0; i < numOfVoxels; i++)
        {
            Voxel voxel = voxels[i];
            if (voxel.isSolid || voxel.wasUpdated)
            {
                voxels[i].wasUpdated = false;
                continue;
            }

            Water_CheckBellow(i);
            voxels[i].wasUpdated = false;
        }
    }

    private void InitVoxels()
    {
        voxels = new Voxel[worldSize.x * worldSize.y * worldSize.z];

        // platform
        for (int x = 0; x < worldSize.x; x++)
        {
            for (int z = 0; z < worldSize.z; z++)
            {
                voxels[numOfVoxels] = new Voxel
                {
                    isSolid = true,
                    voxelObject = null,
                    position = new Vector3(x, 1, z)
                };

                numOfVoxels++;
            }
        }

        // water
        voxels[numOfVoxels] = new Voxel
        {
            isSolid = false,
            fluidLevel = maxFluidLevel,
            isSource = false,
            voxelObject = null,
            position = new Vector3(5, 2, 5),
            wasUpdated = true
        };

        numOfWaterVoxels++;
        numOfVoxels++;
    }

    private void RenderVoxels()
    {
        for (int i = 0; i < numOfVoxels; i++)
        {
            GameObject prefab = voxels[i].isSolid ? solidPrefab : waterPrefab;
            voxels[i].voxelObject = Instantiate(prefab, voxels[i].position, Quaternion.identity);
        }
    }

    private void Water_CheckBellow(int idx)
    {
        Voxel voxel = voxels[idx];
        Vector3 pos = voxel.position;
        Vector3 newPos = new Vector3(pos.x, pos.y - 1, pos.z);

        Voxel newVoxel = GetVoxel(newPos);

        if (newPos.y >= 0 && CanVoxelBeFilled(voxel, newVoxel))
        {
            if (voxel.isSource)
            {
                voxels[numOfVoxels - 1] = CreateWaterVoxel(newPos);
            }
            else
            {
                voxels[idx].position = newPos;
            }
        }
        else
        {
            Water_CheckSides(idx);
        }
    }

    private void Water_CheckSides(int idx)
    {
        Voxel voxel = voxels[idx];

        if (voxel.fluidLevel <= 1) return;

        int x = (int)voxel.position.x;
        int y = (int)voxel.position.y;
        int z = (int)voxel.position.z;

        Voxel[] neighbours = new Voxel[4];

        if (x + 1 < worldSize.x && CanVoxelBeFilled(voxel, GetVoxel(x + 1, y, z)))
        {
            neighbours[0] = GetVoxel(x + 1, y, z);
            neighbours[0].position = new Vector3(x + 1, y, z);
        }

        if (x - 1 >= 0 && CanVoxelBeFilled(voxel, GetVoxel(x - 1, y, z)))
        {
            neighbours[1] = GetVoxel(x - 1, y, z);
            neighbours[1].position = new Vector3(x - 1, y, z);
        }

        if (z + 1 < worldSize.z && CanVoxelBeFilled(voxel, GetVoxel(x, y, z + 1)))
        {
            neighbours[2] = GetVoxel(x, y, z + 1);
            neighbours[2].position = new Vector3(x, y, z + 1);
        }

        if (z - 1 >= 0 && CanVoxelBeFilled(voxel, GetVoxel(x, y, z - 1)))
        {
            neighbours[3] = GetVoxel(x, y, z - 1);
            neighbours[3].position = new Vector3(x, y, z - 1);
        }

        neighbours = neighbours
            .Where(v => v.position != Vector3.zero)
            .OrderBy(v => v.fluidLevel).ToArray();

        byte fluidLevelToTransfer = (byte)(voxel.fluidLevel / (neighbours.Length + 1));
        if (fluidLevelToTransfer < 1) return;

        foreach (var neighbour in neighbours)
        {
            DistributeFlow((int)neighbour.position.x, y, (int)neighbour.position.z);
            if (voxel.fluidLevel <= 1) return;
        }

        void DistributeFlow(int newX, int newY, int newZ)
        {
            int newIdx = GetVoxelIdx(newX, newY, newZ);

            if (!voxel.isSource)
            {
                voxels[idx].fluidLevel -= fluidLevelToTransfer;
            }

            if (IsVoxelEmpty(GetVoxel(newX, newY, newZ)))
            {
                voxels[numOfVoxels - 1] = CreateWaterVoxel(newX, newY, newZ, fluidLevelToTransfer);
            }
            else
            {
                voxels[newIdx].fluidLevel += fluidLevelToTransfer;
            }

            if (voxel.fluidLevel <= 0)
            {
                Destroy(voxel.voxelObject);
                voxels[idx] = default;
            }
        }
    }

    private int GetVoxelIdx(int x, int y, int z)
    {
        Vector3 pos = new Vector3(x, y, z);
        return Array.FindIndex(voxels, v => v.position == pos);
    }

    private Voxel GetVoxel(int x, int y, int z)
    {
        Vector3 pos = new Vector3(x, y, z);
        return GetVoxel(pos);
    }

    private Voxel GetVoxel(Vector3 pos)
    {
        pos = new Vector3(pos.x, pos.y, pos.z);

        return voxels.FirstOrDefault(v => v.position == pos);
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

    private Voxel CreateWaterVoxel(Vector3 pos, byte? fluidLevel = null)
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

        voxel.fluidLevel = (byte)(fluidLevel == null ? maxFluidLevel : (byte)fluidLevel);

        numOfWaterVoxels++;
        numOfVoxels++;
        return voxel;
    }
}
