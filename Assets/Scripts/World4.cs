using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class World4 : MonoBehaviour
{
    private const byte maxFluidLevel = 127;
    private const byte minFluidLevel = 1;

    private Voxel4[,,] voxels;
    private Voxel4[,,] previousVoxels;
    private static GameObject[,,] voxelObjects;
    private Voxel4 defaultVoxel = new Voxel4();

    private Vector3Int worldSize = new Vector3Int(30, 30, 30);
    public GameObject solidPrefab;
    public GameObject waterPrefab;


    void Start()
    {
        voxelObjects = new GameObject[worldSize.x, worldSize.y, worldSize.z];

        InitVoxels();
        previousVoxels = voxels.Clone() as Voxel4[,,];
    }

    void Update()
    {
        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    Voxel4 voxel = previousVoxels[x, y, z];
                    if (IsVoxelEmpty(voxel)) continue;
                    if (voxel.isSolid)
                    {
                        continue;
                    }

                    Water_CheckBellow(voxel, x, y, z);
                }
            }
        }

        previousVoxels = voxels.Clone() as Voxel4[,,];

        ReRenderVoxels();
    }

    private void ReRenderVoxels()
    {
        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    if (voxelObjects[x, y, z] == null || voxels[x, y, z].isSolid) continue;
                    ReRenderVoxel(voxels[x, y, z], new Vector3(x, y, z));
                }
            }
        }
    }

    private void InitVoxels()
    {
        voxels = new Voxel4[worldSize.x, worldSize.y, worldSize.z];

        // water
        Vector3Int waterPos = new Vector3Int(15, 29, 15);
        voxels[waterPos.x, waterPos.y, waterPos.z] = CreateWaterVoxel(waterPos);
        voxels[waterPos.x, waterPos.y, waterPos.z].isSource = true;
    }

    private void Water_CheckBellow(Voxel4 voxel, int x, int y, int z)
    {
        Vector3Int newPos = new Vector3Int(x, y - 1, z);
        if (newPos.y >= 0 && CanVoxelBeFilledBellow(previousVoxels[newPos.x, newPos.y, newPos.z]))
        {
            if (voxel.isSource)
            {
                voxels[newPos.x, newPos.y, newPos.z] = CreateWaterVoxel(newPos);
            }
            else
            {
                byte availableSpace = (byte)(maxFluidLevel - previousVoxels[newPos.x, newPos.y, newPos.z].fluidLevel);
                byte fluidToTransfer = Clamp(voxel.fluidLevel, 0, availableSpace);
                if (fluidToTransfer == 0)
                {
                    Water_CheckSides(x, y, z);
                    return;
                }

                voxels[x, y, z].fluidLevel -= fluidToTransfer;
                if (voxels[x, y, z].fluidLevel < minFluidLevel)
                {
                    Destroy(voxelObjects[x, y, z]);
                    voxels[x, y, z] = new Voxel4();
                }
                else
                {
                    Water_CheckSides(x, y, z);
                }

                if (voxelObjects[newPos.x, newPos.y, newPos.z] == null)
                {
                    voxels[newPos.x, newPos.y, newPos.z] = CreateWaterVoxel(newPos, fluidToTransfer);
                    return;
                }

                voxels[newPos.x, newPos.y, newPos.z].fluidLevel += fluidToTransfer;
            }
        }
        else
        {
            Water_CheckSides(x, y, z);
        }
    }

    private void Water_CheckSides(int x, int y, int z)
    {
        Voxel4 prevVoxel = previousVoxels[x, y, z];

        if (prevVoxel.fluidLevel <= minFluidLevel) return;

        (Voxel4 Voxel, bool CanBeFilled)[] prevNeighbours = new (Voxel4, bool)[4];

        if (x + 1 < worldSize.x && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x + 1, y, z]))
        {
            SetNeighbour(x + 1, y, z, 0);
        }

        if (x - 1 >= 0 && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x - 1, y, z]))
        {
            SetNeighbour(x - 1, y, z, 1);
        }

        if (z + 1 < worldSize.z && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x, y, z + 1]))
        {
            SetNeighbour(x, y, z + 1, 2);
        }

        if (z - 1 >= 0 && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x, y, z - 1]))
        {
            SetNeighbour(x, y, z - 1, 3);
        }

        void SetNeighbour(int x, int y, int z, int index)
        {
            prevNeighbours[index].Voxel = previousVoxels[x, y, z];
            prevNeighbours[index].Voxel.position = new Vector3(x, y, z);
            prevNeighbours[index].CanBeFilled = true;
        }

        int prevNeighboursLength = 0;
        foreach (var nb in prevNeighbours)
        {
            if (nb.CanBeFilled)
            {
                prevNeighboursLength++;
            }
        }

        byte maxFluidLevelToTransfer = (byte)(prevVoxel.fluidLevel / (prevNeighboursLength + 1));

        foreach (var nb in prevNeighbours)
        {
            Voxel4 currentVoxel = voxels[x, y, z];

            if (!nb.CanBeFilled ||
                currentVoxel.fluidLevel <= minFluidLevel)
            {
                continue;
            }

            Vector3 nbPos = nb.Voxel.position;
            Voxel4 neighbour = voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z];

            byte currentFluidToTransfer = CalculateFluidToTransfer(currentVoxel, neighbour, maxFluidLevelToTransfer);

            if (currentFluidToTransfer == 0)
                continue;

            if (voxelObjects[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z] == null)
            {
                voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z] = CreateWaterVoxel(nbPos, currentFluidToTransfer);
            }
            else
            {
                voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z].fluidLevel += currentFluidToTransfer;
            }

            if (!prevVoxel.isSource)
            {
                voxels[x, y, z].fluidLevel -= currentFluidToTransfer;
            }

            if (voxels[x, y, z].fluidLevel < minFluidLevel)
            {
                Destroy(voxelObjects[x, y, z]);
                voxels[x, y, z] = new Voxel4();
                return;
            }
        }
    }

    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < worldSize.x &&
               y >= 0 && y < worldSize.y &&
               z >= 0 && z < worldSize.z;
    }

    private Voxel4 CreateWaterVoxel(int x, int y, int z, byte? fluidLevel = null)
    {
        return CreateWaterVoxel(new Vector3Int(x, y, z), fluidLevel);
    }

    private Voxel4 CreateWaterVoxel(Vector3 pos, byte? fluidLevel = null)
    {
        if (fluidLevel == null)
            fluidLevel = maxFluidLevel;

        Voxel4 voxel = new Voxel4
        {
            isSolid = false,
            fluidLevel = (byte)fluidLevel,
            isSource = false,
            position = pos
        };

        voxelObjects[(int)pos.x, (int)pos.y, (int)pos.z] = Instantiate(waterPrefab, pos, Quaternion.identity);
        return voxel;
    }

    private void CreateSolidVoxel(int x, int y, int z)
    {
        CreateSolidVoxel(new Vector3Int(x, y, z));
    }

    private void CreateSolidVoxel(Vector3 pos)
    {
        Voxel4 solidVoxel = new Voxel4
        {
            isSolid = true,
            fluidLevel = 0,
            position = pos
        };

        voxels[(int)pos.x, (int)pos.y, (int)pos.z] = solidVoxel;
        voxelObjects[(int)pos.x, (int)pos.y, (int)pos.z] = Instantiate(solidPrefab, solidVoxel.position, Quaternion.identity);
    }

    private void DeleteVoxel(int x, int y, int z)
    {
        DeleteVoxel(new Vector3Int(x, y, z));
    }

    private void DeleteVoxel(Vector3 pos)
    {
        voxels[(int)pos.x, (int)pos.y, (int)pos.z] = default;
        Destroy(voxelObjects[(int)pos.x, (int)pos.y, (int)pos.z]);
    }

    private static void ReRenderVoxel(Voxel4 voxel, Vector3 pos)
    {
        GameObject voxelObject = voxelObjects[(int)pos.x, (int)pos.y, (int)pos.z];

        float scaleY = voxel.fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxelObject.transform.position = new Vector3(pos.x, (float)Math.Ceiling(pos.y) - newY, pos.z);
    }

    private static bool CanVoxelBeFilledToSides(Voxel4 voxel, Voxel4 newVoxel)
    {
        return IsVoxelEmpty(newVoxel) ||
            (!newVoxel.isSolid && newVoxel.fluidLevel < maxFluidLevel && voxel.fluidLevel > newVoxel.fluidLevel);
    }

    private static bool CanVoxelBeFilledBellow(Voxel4 newVoxel)
    {
        return IsVoxelEmpty(newVoxel) ||
            (!newVoxel.isSolid && newVoxel.fluidLevel < maxFluidLevel);
    }

    private static bool IsVoxelEmpty(Voxel4 voxel)
    {
        return voxel.fluidLevel == 0 && !voxel.isSolid;
    }

    private byte CalculateFluidToTransfer(Voxel4 voxel, Voxel4 neighbour, byte maxFluidToTransfer)
    {
        if (maxFluidToTransfer > (voxel.fluidLevel - neighbour.fluidLevel) / 2)
        {
            if (voxel.fluidLevel - neighbour.fluidLevel == 1)
            {
                return 1;
            }

            return (byte)((voxel.fluidLevel - neighbour.fluidLevel) / 2);
        }

        return maxFluidToTransfer;
    }

    private static byte Diff(byte a, byte b, int nCount)
    {
        byte curr = (byte)((a - b) / (nCount + 1));
        byte max = (byte)(a / nCount + 1);
        return Clamp(curr, 0, max);
    }

    private static byte Clamp(byte value, byte min, byte max)
    {
        if (value <= min) return min;
        if (value >= max) return max;
        return value;
    }
}
