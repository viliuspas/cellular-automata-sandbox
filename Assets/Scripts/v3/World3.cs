using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class World3 : MonoBehaviour
{
    public Vector3Int worldSize = new Vector3Int(20, 20, 20);
    public GameObject solidPrefab;
    public GameObject waterPrefab;

    private const byte maxFluidLevel = 127;
    private const byte minFluidLevel = 1;

    private int worldSizeX;
    private int worldSizeY;
    private int worldSizeZ;

    private Voxel3[] voxels;
    private Voxel3[] previousVoxels;
    private static GameObject[] voxelObjects;

    private Voxel3 defaultVoxel = new Voxel3();

    private HashSet<int> waterVoxelsIndexes = new HashSet<int>();
    private HashSet<int> prevWaterVoxelsIndexes = new HashSet<int>();
    private HashSet<int> checkedIndexes = new HashSet<int>();

    void Start()
    {
        worldSizeX = worldSize.x;
        worldSizeY = worldSize.y;
        worldSizeZ = worldSize.z;
        InitVoxels();
    }

    void Update()
    {
        checkedIndexes.Clear();
        prevWaterVoxelsIndexes = new HashSet<int>(waterVoxelsIndexes);

        foreach (int voxelIndex in prevWaterVoxelsIndexes)
        {
            if (voxels[voxelIndex].isSettled)
                continue;

            (int x, int y, int z) = GetVoxelCoordinates(voxelIndex);

            HandleVoxelStep(x, y, z);
            VoxelNeigbours(x, y, z, HandleVoxelStep);
        }

        previousVoxels = voxels.Clone() as Voxel3[];

        ReRenderVoxels();
    }

    private static void VoxelNeigbours(int x, int y, int z, Action<int, int, int> func)
    {
        func(x + 1, y, z);
        func(x - 1, y, z);
        func(x, y + 1, z);
        func(x, y - 1, z);
        func(x, y, z + 1);
        func(x, y, z - 1);
    }

    private (int x, int y, int z) GetVoxelCoordinates(int voxelIndex)
    {
        int x = voxelIndex % worldSizeX;
        int yz = voxelIndex / worldSizeX;
        int y = yz % worldSizeY;
        int z = yz / worldSizeY;
        return (x, y, z);
    }

    private void HandleVoxelStep(int x, int y, int z)
    {
        int voxelIndex = GetIndex(x, y, z);
        if (checkedIndexes.Contains(voxelIndex))
            return;

        checkedIndexes.Add(voxelIndex);
        if (!IsValidPosition(x, y, z) || voxels[voxelIndex].isSolid)
        {
            return;
        }

        bool hasFlownDown = FlowDown(x, y, z);
        if (!hasFlownDown)
            FlowSideways(x, y, z);
    }

    private void ReRenderVoxels()
    {
        foreach (int voxelIndex in waterVoxelsIndexes)
        {
            if (voxels[voxelIndex].isSettled) continue;
            if (voxelObjects[voxelIndex] == null || voxels[voxelIndex].isSolid) continue;
            (int x, int y, int z) = GetVoxelCoordinates(voxelIndex);
            ReRenderVoxel(x, y, z);
        }
    }

    private void InitVoxels()
    {
        //CaveGenerator caveGen = new CaveGenerator(worldSizeX, worldSizeY, worldSizeZ, noiseScale, threshold);
        //(voxels, voxelObjects) = caveGen.Generate(solidPrefab);

        //FillFirstEmptyVoxelWithWater(true);

        voxels = new Voxel3[worldSizeX * worldSizeY * worldSizeZ];
        voxelObjects = new GameObject[worldSizeX * worldSizeY * worldSizeZ];
        int voxelIndex = GetIndex(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        voxels[voxelIndex] = CreateWaterVoxel(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        voxels[voxelIndex].isSource = true;

        previousVoxels = voxels.Clone() as Voxel3[];
    }

    private void FillFirstEmptyVoxelWithWater(bool isSource)
    {
        for (int y = worldSizeY - 1; y > 0; y--)
        {
            for (int x = worldSizeX - 1; x > 0; x--)
            {
                for (int z = worldSizeZ - 1; z > 0; z--)
                {
                    if (!voxels[x + worldSizeX * (y + worldSizeY * z)].isSolid)
                    {
                        Voxel3 waterVoxel = CreateWaterVoxel(x, y - 1, z);
                        waterVoxel.isSource = isSource;
                        voxels[x + worldSizeX * (y + worldSizeY * z)] = waterVoxel;
                        return;
                    }
                }
            }
        }
    }

    private bool FlowDown(int x, int y, int z)
    {
        byte fluidIn = 0;
        byte fluidOut = 0;
        byte availableSpace = 0;

        int voxelIndex = GetIndex(x, y, z);
        int voxelIndexBellow = GetIndex(x, y - 1, z);
        int voxelIndexAbove = GetIndex(x, y + 1, z);

        if (y + 1 < worldSizeY && !previousVoxels[voxelIndexAbove].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[voxelIndex].fluidLevel);
            fluidIn = Clamp(previousVoxels[voxelIndexAbove].fluidLevel, 0, availableSpace);
        }

        if (y - 1 >= 0 && !previousVoxels[voxelIndexBellow].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[voxelIndexBellow].fluidLevel);
            fluidOut = Clamp(previousVoxels[voxelIndex].fluidLevel, 0, availableSpace);
        }

        byte fluidLevel = (byte)(previousVoxels[voxelIndex].fluidLevel + fluidIn - fluidOut);
        
        if (previousVoxels[voxelIndex].isSource)
        {
            fluidLevel = maxFluidLevel;
        }

        if (voxels[voxelIndex].Equals(defaultVoxel) && fluidLevel == 0)
        {
            return false;
        }

        UpdateVoxelFluid(x, y, z, fluidLevel);

        if (voxels[voxelIndex].fluidLevel == previousVoxels[voxelIndex].fluidLevel)
        {
            voxels[voxelIndex].isSettled = true;
            return previousVoxels[voxelIndex].isSource;
        }
        return true;
    }

    private void FlowSideways(int x, int y, int z)
    {
        int voxelIndex = GetIndex(x, y, z);

        if (previousVoxels[voxelIndex].isSource)
            return;

        var prevNeighbours = GetValidNeighbours(x, y, z);
        int neighbourCount = CountFlowableNeighbours(prevNeighbours);
    
        if (neighbourCount == 0)
            return;

        var (fluidIn, fluidOut) = CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount);
    
        if (fluidIn == 0 && fluidOut == 0)
        {
            voxels[voxelIndex].isSettled = true;
            return;
        }
            
        byte newFluidLevel = (byte)(previousVoxels[voxelIndex].fluidLevel + fluidIn - fluidOut);
    
        if (newFluidLevel == 0 && voxels[voxelIndex].Equals(defaultVoxel))
            return;

        UpdateVoxelFluid(x, y, z, newFluidLevel);
    }

    private (Voxel3 Voxel, int voxelIndex, bool CanBeFilled)[] GetValidNeighbours(int x, int y, int z)
    {
        var neighbours = new (Voxel3 Voxel, int voxelIndex, bool CanBeFilled)[4];

        CheckAndSetNeighbour(x + 1, y, z, 0);
        CheckAndSetNeighbour(x - 1, y, z, 1);
        CheckAndSetNeighbour(x, y, z + 1, 2);
        CheckAndSetNeighbour(x, y, z - 1, 3);

        return neighbours;

        void CheckAndSetNeighbour(int nx, int ny, int nz, int index)
        {
            int neighbourIndex = GetIndex(nx, ny, nz);
            if (IsValidPosition(nx, ny, nz) && !previousVoxels[neighbourIndex].isSolid)
            {
                neighbours[index].Voxel = previousVoxels[neighbourIndex];
                neighbours[index].voxelIndex = neighbourIndex;
                neighbours[index].CanBeFilled = true;
            }
        }
    }

    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < worldSizeX &&
               y >= 0 && y < worldSizeY &&
               z >= 0 && z < worldSizeZ;
    }

    private int CountFlowableNeighbours((Voxel3 Voxel, int voxelIndex, bool CanBeFilled)[] neighbours)
    {
        int count = 0;
        foreach (var nb in neighbours)
        {
            if (nb.CanBeFilled && !CanVoxelFlowDown(nb.voxelIndex))
                count++;
        }
        return count;
    }

    private (byte fluidIn, byte fluidOut) CalculateFluidFlow(
        int x, int y, int z, 
        (Voxel3 Voxel, int neighbourIndex, bool CanBeFilled)[] neighbours, 
        int neighbourCount)
    {
        byte fluidIn = 0;
        byte fluidOut = 0;
        int voxelIndex = GetIndex(x, y, z);
        Voxel3 currentVoxel = previousVoxels[voxelIndex];

        foreach (var nb in neighbours)
        {
            if (!nb.CanBeFilled || CanVoxelFlowDown(nb.neighbourIndex))
                continue;

            if (currentVoxel.fluidLevel == 0 && nb.Voxel.fluidLevel == 0)
                continue;

            if (nb.Voxel.fluidLevel > currentVoxel.fluidLevel)
            {
                if (nb.Voxel.fluidLevel - currentVoxel.fluidLevel == 1)
                    fluidIn = 1;
                else
                    fluidIn += Diff(nb.Voxel.fluidLevel, currentVoxel.fluidLevel, neighbourCount);
            }

            if (nb.Voxel.fluidLevel < currentVoxel.fluidLevel)
                fluidOut += Diff(currentVoxel.fluidLevel, nb.Voxel.fluidLevel, neighbourCount);
        }

        return (fluidIn, fluidOut);
    }

    private void UpdateVoxelFluid(int x, int y, int z, byte fluidLevel)
    {
        int voxelIndex = GetIndex(x, y, z);

        VoxelNeigbours(x, y, z, (nx, ny, nz) =>
        {
            if (!IsValidPosition(nx, ny, nz)) return;
            if (voxels[voxelIndex].isSettled)
            {
                voxels[voxelIndex].isSettled = false;
            }
        });

        if (voxels[voxelIndex].Equals(defaultVoxel))
        {
            voxels[voxelIndex] = CreateWaterVoxel(x, y, z, fluidLevel);
        }
        else
        {
            voxels[voxelIndex].fluidLevel = fluidLevel;
        }

        if (voxels[voxelIndex].fluidLevel < minFluidLevel)
        {
            DeleteVoxel(voxelIndex);
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
        };

        int voxelIndex = GetIndex((int)pos.x, (int)pos.y, (int)pos.z);
        voxelObjects[voxelIndex] = Instantiate(waterPrefab, pos, Quaternion.identity);
        waterVoxelsIndexes.Add(voxelIndex);
        return voxel;
    }

    private void CreateSolidVoxel(int x, int y, int z)
    {
        Voxel3 solidVoxel = new Voxel3
        {
            isSolid = true,
            fluidLevel = 0,
            //position = pos
        };

        int voxelIndex = x + worldSizeX * (y + worldSizeY * z);
        voxels[voxelIndex] = solidVoxel;
        voxelObjects[voxelIndex] = Instantiate(solidPrefab, new Vector3(x, y, z), Quaternion.identity);
    }

    private void DeleteVoxel(int voxelIndex)
    {
        voxels[voxelIndex] = default;
        Destroy(voxelObjects[voxelIndex]);
        waterVoxelsIndexes.Remove(voxelIndex);
    }

    private void ReRenderVoxel(int x, int y, int z)
    {
        int voxelIndex = GetIndex(x, y, z);
        GameObject voxelObject = voxelObjects[voxelIndex];

        float scaleY = voxels[voxelIndex].fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxelObject.transform.position = new Vector3(x, y - newY, z);
    }

    private bool CanVoxelFlowDown(int voxelIndex)
    {
        (int x, int y, int z) = GetVoxelCoordinates(voxelIndex);

        byte fluidOut = 0;
        byte availableSpace = 0;

        int bellowVoxelIndex = GetIndex(x, y - 1, z);
        if (y - 1 >= 0 && !previousVoxels[bellowVoxelIndex].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[bellowVoxelIndex].fluidLevel);
            fluidOut = Clamp(previousVoxels[voxelIndex].fluidLevel, 0, availableSpace);
        }

        return fluidOut > 0;
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

    private int GetIndex(int x, int y, int z)
    {
        return x + worldSizeX * (y + worldSizeY * z);
    }
}
