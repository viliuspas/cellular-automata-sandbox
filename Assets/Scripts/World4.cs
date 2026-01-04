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

    private Voxel3[,,] voxels;
    private Voxel3[,,] previousVoxels;
    private static GameObject[,,] voxelObjects;
    private Voxel3 defaultVoxel = new Voxel3();

    private Vector3Int worldSize = new Vector3Int(30, 30, 30);
    public GameObject solidPrefab;
    public GameObject waterPrefab;


    void Start()
    {
        voxelObjects = new GameObject[worldSize.x, worldSize.y, worldSize.z];

        InitVoxels();
        previousVoxels = voxels.Clone() as Voxel3[,,];
    }

    void Update()
    {
        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    Voxel3 voxel = previousVoxels[x, y, z];
                    if (IsVoxelEmpty(voxel)) continue;
                    if (voxel.isSolid)
                    {
                        continue;
                    }

                    Water_CheckBellow(voxel, x, y, z);
                }
            }
        }

        previousVoxels = voxels.Clone() as Voxel3[,,];

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
        voxels = new Voxel3[worldSize.x, worldSize.y, worldSize.z];

        // fill with cubes
        for (int x = 0; x < worldSize.x; x++)
        {
            for (int z = 0; z < worldSize.z; z++)
            {
                for (int y = 0; y < worldSize.y - 1; y++)
                {
                    CreateSolidVoxel(x, y, z);
                }
            }
        }

        // first tunnel x
        for (int x = 1; x < 4; x++)
        {
            DeleteVoxel(x, worldSize.y - 2, worldSize.z - 2);
        }

        //// go down
        //for (int y = worldSize.y - 2; y > 1; y--)
        //{
        //    DeleteVoxel(worldSize.x - 2, y, worldSize.z - 2);
        //}

        // first tunnel z
        for (int z = worldSize.z - 2; z > worldSize.z - 8; z--)
        {
            DeleteVoxel(4, worldSize.y - 2, z);
        }

        DeleteVoxel(4, 27, 23);

        for (int x = 1; x < worldSize.x - 1; x++)
        {
            for (int z = 1; z < worldSize.z - 1; z++)
            {
                for (int y = worldSize.y - 4; y > worldSize.y - 20; y--)
                {
                    DeleteVoxel(x, y, z);
                }
            }
        }

        // water
        Vector3Int waterPos = new Vector3Int(1, worldSize.y - 2, worldSize.z - 2);
        voxels[waterPos.x, waterPos.y, waterPos.z] = CreateWaterVoxel(waterPos);
        voxels[waterPos.x, waterPos.y, waterPos.z].isSource = true;
    }

    private void Water_CheckBellow(Voxel3 voxel, int x, int y, int z)
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

                Debug.Log($"down {x} {y} {z} to {newPos.x} {newPos.y} {newPos.z}; {previousVoxels[x, y, z].fluidLevel}; {previousVoxels[newPos.x, newPos.y, newPos.z].fluidLevel}; to transfer {fluidToTransfer}");

                voxels[x, y, z].fluidLevel -= fluidToTransfer;
                if (voxels[x, y, z].fluidLevel < minFluidLevel)
                {
                    Destroy(voxelObjects[x, y, z]);
                    voxels[x, y, z] = new Voxel3();
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
        Voxel3 prevVoxel = previousVoxels[x, y, z];

        if (prevVoxel.fluidLevel <= minFluidLevel) return;

        (Voxel3 Voxel, bool CanBeFilled)[] prevNeighbours = new (Voxel3, bool)[4];

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
            Voxel3 currentVoxel = voxels[x, y, z];

            if (!nb.CanBeFilled ||
                currentVoxel.fluidLevel <= minFluidLevel)
            {
                continue;
            }

            Vector3 nbPos = nb.Voxel.position;
            Voxel3 neighbour = voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z];

            byte currentFluidToTransfer = CalculateFluidToTransfer(currentVoxel, neighbour, maxFluidLevelToTransfer);

            if (currentFluidToTransfer == 0)
                continue;

            Debug.Log($"sides {x} {y} {z} to {nbPos.x} {nbPos.y} {nbPos.z}; {voxels[x, y, z].fluidLevel}; {voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z].fluidLevel}; to transfer {currentFluidToTransfer}");

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
                voxels[x, y, z] = new Voxel3();
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

    private Voxel3 CreateWaterVoxel(int x, int y, int z, byte? fluidLevel = null)
    {
        return CreateWaterVoxel(new Vector3Int(x, y, z), fluidLevel);
    }

    private Voxel3 CreateWaterVoxel(Vector3 pos, byte? fluidLevel = null)
    {
        if (fluidLevel == null)
            fluidLevel = maxFluidLevel;

        Voxel3 voxel = new Voxel3
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
        Voxel3 solidVoxel = new Voxel3
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

    private static void ReRenderVoxel(Voxel3 voxel, Vector3 pos)
    {
        GameObject voxelObject = voxelObjects[(int)pos.x, (int)pos.y, (int)pos.z];

        float scaleY = voxel.fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxelObject.transform.position = new Vector3(pos.x, (float)Math.Ceiling(pos.y) - newY, pos.z);
    }

    private static bool CanVoxelBeFilledToSides(Voxel3 voxel, Voxel3 newVoxel)
    {
        return IsVoxelEmpty(newVoxel) ||
            (!newVoxel.isSolid && newVoxel.fluidLevel < maxFluidLevel && voxel.fluidLevel > newVoxel.fluidLevel);
    }

    private static bool CanVoxelBeFilledBellow(Voxel3 newVoxel)
    {
        return IsVoxelEmpty(newVoxel) ||
            (!newVoxel.isSolid && newVoxel.fluidLevel < maxFluidLevel);
    }

    private static bool IsVoxelEmpty(Voxel3 voxel)
    {
        return voxel.fluidLevel == 0 && !voxel.isSolid;
    }

    private byte CalculateFluidToTransfer(Voxel3 voxel, Voxel3 neighbour, byte maxFluidToTransfer)
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
