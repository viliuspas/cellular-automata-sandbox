using System;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class World3 : MonoBehaviour
{
    private const byte maxFluidLevel = 255;
    private const byte minFluidLevel = 1;

    private Voxel3[,,] voxels;
    private Voxel3[,,] previousVoxels;
    private static GameObject[,,] voxelObjects;

    public Vector3Int worldSize = new Vector3Int(5, 10, 4);
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
        //for (int z = 0; z < worldSize.z; z++)
        //{
        //    for (int x = 0; x < worldSize.x; x++)
        //    {
        //        for (int y = 0; y < worldSize.y; y++)
        //        {
        //            Voxel3 voxel = previousVoxels[x, y, z];
        //            if (IsVoxelEmpty(voxel)) continue;
        //            if (voxel.isSolid)
        //            {
        //                continue;
        //            }

        //            Water_CheckBellow(voxel, x, y, z);
        //        }
        //    }
        //}

        for (int z = 0; z < worldSize.z; z++)
        {
            for (int x = 0; x < worldSize.x; x++)
            {
                for (int y = 0; y < worldSize.y; y++)
                {
                    if (voxels[x,y,z].isSolid)
                    {
                        continue;
                    }

                    bool hasFlownDown = FlowDown(x, y, z);
                    if (!hasFlownDown)
                        FlowSideways(x, y, z);
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

        // platform
        for (int x = 0; x < worldSize.x; x++)
        {
            for (int z = 0; z < worldSize.z; z++)
            {
                Vector3Int solidPos = new Vector3Int(x, 1, z);
                Voxel3 solidVoxel = new Voxel3
                {
                    isSolid = true,
                    fluidLevel = 0,
                    position = solidPos
                };

                voxels[solidPos.x, solidPos.y, solidPos.z] = solidVoxel;
                voxelObjects[solidPos.x, solidPos.y, solidPos.z] = Instantiate(solidPrefab, solidVoxel.position, Quaternion.identity);
            }
        }

        // water
        Vector3Int waterPos = new Vector3Int(1, 4, 1);
        voxels[waterPos.x, waterPos.y, waterPos.z] = CreateWaterVoxel(waterPos);
        voxels[waterPos.x, waterPos.y, waterPos.z].isSource = true;
        //voxels[waterPos.x, waterPos.y, waterPos.z].fluidLevel = 80;

        //Vector3Int waterPos2 = new Vector3Int(6, 2, 8);
        //voxels[waterPos2.x, waterPos2.y, waterPos2.z] = CreateWaterVoxel(waterPos2);
        //voxels[waterPos2.x, waterPos2.y, waterPos2.z].isSource = false;

        //waterPos = new Vector3Int(6, 5, 6);
        //voxels[waterPos.x, waterPos.y, waterPos.z] = CreateWaterVoxel(waterPos);
    }

    //private void Water_CheckBellow(Voxel3 voxel, int x, int y, int z)
    //{
    //    Vector3Int newPos = new Vector3Int(x, y - 1, z);
    //    if (newPos.y >= 0 && CanVoxelBeFilledBellow(previousVoxels[newPos.x, newPos.y, newPos.z]))
    //    {
    //        if (voxel.isSource)
    //        {
    //            voxels[newPos.x, newPos.y, newPos.z] = CreateWaterVoxel(newPos);
    //        }
    //        else
    //        {
    //            byte availableSpace = (byte)(maxFluidLevel - previousVoxels[newPos.x, newPos.y, newPos.z].fluidLevel);
    //            byte fluidToTransfer = Clamp(voxel.fluidLevel, 0, availableSpace);
    //            if (fluidToTransfer == 0)
    //            {
    //                Water_CheckSides(x, y, z);
    //                return;
    //            }

    //            Debug.Log($"down {x} {y} {z} to {newPos.x} {newPos.y} {newPos.z}; {previousVoxels[x, y, z].fluidLevel}; {previousVoxels[newPos.x, newPos.y, newPos.z].fluidLevel}; to transfer {fluidToTransfer}");

    //            voxels[x,y,z].fluidLevel -= fluidToTransfer;
    //            if (voxels[x, y, z].fluidLevel < minFluidLevel)
    //            {
    //                Destroy(voxelObjects[x, y, z]);
    //                voxels[x, y, z] = new Voxel3();
    //            }
    //            else
    //            {
    //                Water_CheckSides(x, y, z);
    //            }

    //            if (voxelObjects[newPos.x, newPos.y, newPos.z] == null)
    //            {
    //                voxels[newPos.x, newPos.y, newPos.z] = CreateWaterVoxel(newPos, fluidToTransfer);
    //                return;
    //            }

    //            voxels[newPos.x, newPos.y, newPos.z].fluidLevel += fluidToTransfer;
    //        }
    //    }
    //    else
    //    {
    //        Water_CheckSides(x, y, z);
    //    }
    //}

    private bool FlowDown(int x, int y, int z)
    {
        byte fluidIn = 0;
        byte fluidOut = 0;
        byte availableSpace = 0;

        if (y + 1 < worldSize.y && !previousVoxels[x, y + 1, z].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[x, y, z].fluidLevel);
            fluidIn = Clamp(previousVoxels[x, y + 1, z].fluidLevel, 0, availableSpace);
        }

        if (y - 1 >= 0 && !previousVoxels[x, y - 1, z].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[x, y - 1, z].fluidLevel);
            fluidOut = Clamp(previousVoxels[x, y, z].fluidLevel, 0, availableSpace);
        }

        byte fluidLevel = (byte)(previousVoxels[x, y, z].fluidLevel + fluidIn - fluidOut);
        
        if (previousVoxels[x, y, z].isSource)
        {
            fluidLevel = maxFluidLevel;
        }

        if (voxelObjects[x, y, z] == null && fluidLevel == 0)
        {
            return false;
        }

        if (voxelObjects[x, y, z] == null)
        {
            voxels[x, y, z] = CreateWaterVoxel(x, y, z, fluidLevel);
        }
        else
        {
            voxels[x, y, z].fluidLevel = fluidLevel;
        }

        if (voxels[x, y, z].fluidLevel < minFluidLevel)
        {
            Destroy(voxelObjects[x, y, z]);
            voxelObjects[x, y, z] = null;
            voxels[x, y, z] = new Voxel3();
        }

        if (voxels[x,y,z].fluidLevel == previousVoxels[x,y,z].fluidLevel)
            return false;
        return true;
    }

    //private void Water_CheckSides(int x, int y, int z)
    //{
    //    Voxel3 prevVoxel = previousVoxels[x, y, z];

    //    if (prevVoxel.fluidLevel <= minFluidLevel) return;

    //    (Voxel3 Voxel, bool CanBeFilled)[] prevNeighbours = new (Voxel3, bool)[4];

    //    if (x + 1 < worldSize.x && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x + 1, y, z]))
    //    {
    //        SetNeighbour(x + 1, y, z, 0);
    //    }

    //    if (x - 1 >= 0 && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x - 1, y, z]))
    //    {
    //        SetNeighbour(x - 1, y, z, 1);
    //    }

    //    if (z + 1 < worldSize.z && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x, y, z + 1]))
    //    {
    //        SetNeighbour(x, y, z + 1, 2);
    //    }

    //    if (z - 1 >= 0 && CanVoxelBeFilledToSides(prevVoxel, previousVoxels[x, y, z - 1]))
    //    {
    //        SetNeighbour(x, y, z - 1, 3);
    //    }

    //    void SetNeighbour(int x, int y, int z, int index)
    //    {
    //        prevNeighbours[index].Voxel = previousVoxels[x, y, z];
    //        prevNeighbours[index].Voxel.position = new Vector3(x, y, z);
    //        prevNeighbours[index].CanBeFilled = true;
    //    }

    //    int prevNeighboursLength = 0;
    //    foreach (var nb in prevNeighbours)
    //    {
    //        if (nb.CanBeFilled)
    //        {
    //            prevNeighboursLength++;
    //        }
    //    }

    //    byte maxFluidLevelToTransfer = (byte)(prevVoxel.fluidLevel / (prevNeighboursLength + 1));

    //    foreach (var nb in prevNeighbours)
    //    {
    //        Voxel3 currentVoxel = previousVoxels[x, y, z];

    //        if (!nb.CanBeFilled ||
    //            currentVoxel.fluidLevel <= minFluidLevel)
    //        {
    //            continue;
    //        }

    //        Vector3 nbPos = nb.Voxel.position;
    //        Voxel3 neighbour =  previousVoxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z];

    //        byte currentFluidToTransfer = CalculateFluidToTransfer(currentVoxel, neighbour, maxFluidLevelToTransfer);

    //        if (currentFluidToTransfer == 0)
    //            continue;

    //        Debug.Log($"sides {x} {y} {z} to {nbPos.x} {nbPos.y} {nbPos.z}; {voxels[x, y, z].fluidLevel}; {voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z].fluidLevel}; to transfer {currentFluidToTransfer}");

    //        if (voxelObjects[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z] == null)
    //        {
    //            voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z] = CreateWaterVoxel(nbPos, currentFluidToTransfer);
    //        }
    //        else
    //        {
    //            voxels[(int)nbPos.x, (int)nbPos.y, (int)nbPos.z].fluidLevel += currentFluidToTransfer;
    //        }

    //        if (!prevVoxel.isSource)
    //        {
    //            voxels[x, y, z].fluidLevel -= currentFluidToTransfer;
    //        }

    //        if (voxels[x, y, z].fluidLevel < minFluidLevel)
    //        {
    //            Destroy(voxelObjects[x, y, z]);
    //            voxels[x, y, z] = new Voxel3();
    //            return;
    //        }
    //    }
    //}

    private void FlowSideways(int x, int y, int z)
    {
        if (previousVoxels[x,y,z].isSource)
        {
            return;
        }

        (Voxel3 Voxel, bool CanBeFilled)[] prevNeighbours = new (Voxel3, bool)[4];

        if (x + 1 < worldSize.x && !previousVoxels[x + 1, y, z].isSolid)
        {
            SetNeighbour(x + 1, y, z, 0);
        }

        if (x - 1 >= 0 && !previousVoxels[x - 1, y, z].isSolid)
        {
            SetNeighbour(x - 1, y, z, 1);
        }

        if (z + 1 < worldSize.z && !previousVoxels[x, y, z + 1].isSolid)
        {
            SetNeighbour(x, y, z + 1, 2);
        }

        if (z - 1 >= 0 && !previousVoxels[x, y, z - 1].isSolid)
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
            if (nb.CanBeFilled && !CanVoxelFlowDown(nb.Voxel))
            {
                prevNeighboursLength++;
            }
        }

        byte fluidIn = 0;
        byte fluidOut = 0;

        foreach (var nb in prevNeighbours)
        {
            if (!nb.CanBeFilled || CanVoxelFlowDown(nb.Voxel)) 
                continue;

            Voxel3 currentVoxel = previousVoxels[x, y, z];

            if (currentVoxel.fluidLevel == 0 && nb.Voxel.fluidLevel == 0)
                continue;

            if (nb.Voxel.fluidLevel > currentVoxel.fluidLevel)
                fluidIn += Diff(nb.Voxel.fluidLevel, currentVoxel.fluidLevel, prevNeighboursLength);

            if (nb.Voxel.fluidLevel < currentVoxel.fluidLevel)
                fluidOut += Diff(currentVoxel.fluidLevel, nb.Voxel.fluidLevel, prevNeighboursLength);
        }

        byte fluidLevel = (byte)(previousVoxels[x, y, z].fluidLevel + fluidIn - fluidOut);

        if (voxelObjects[x, y, z] == null && fluidLevel == 0)
        {
            return;
        }

        if (fluidIn == 0 && fluidOut == 0)
        {
            return;
        }

        if (voxelObjects[x, y, z] == null)
        {
            voxels[x, y, z] = CreateWaterVoxel(x, y, z, fluidLevel);
        }
        else
        {
            voxels[x, y, z].fluidLevel = fluidLevel;
        }

        if (voxels[x, y, z].fluidLevel < minFluidLevel)
        {
            Destroy(voxelObjects[x, y, z]);
            voxels[x, y, z] = new Voxel3();
        }
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

    private bool CanVoxelFlowDown(Voxel3 voxel)
    {
        int x = (int)voxel.position.x;
        int y = (int)voxel.position.y;
        int z = (int)voxel.position.z;

        byte fluidOut = 0;
        byte availableSpace = 0;

        if (y - 1 >= 0 && !previousVoxels[x, y - 1, z].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[x, y - 1, z].fluidLevel);
            fluidOut = Clamp(previousVoxels[x, y, z].fluidLevel, 0, availableSpace);
        }

        return fluidOut > 0;
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

    private byte Diff(byte a, byte b, int nCount)
    {
        byte curr = (byte)((a - b) / (nCount + 1));
        byte max = (byte)(a / nCount + 1);
        return Clamp(curr, 0, max);
    }

    private byte Clamp(byte value, byte min, byte max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
