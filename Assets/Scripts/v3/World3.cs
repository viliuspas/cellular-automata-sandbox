using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;

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

    private Voxel[] Voxels;
    private Voxel[] previousVoxels;
    private static GameObject[] VoxelObjects;

    private Voxel defaultVoxel = new Voxel();

    //private HashSet<int> waterVoxelsIndexes = new HashSet<int>();
    //private HashSet<int> prevWaterVoxelsIndexes = new HashSet<int>();
    //private HashSet<int> checkedIndexes = new HashSet<int>();


    void Start()
    {
        worldSizeX = worldSize.x;
        worldSizeY = worldSize.y;
        worldSizeZ = worldSize.z;

        InitVoxels();
    }

    static readonly ProfilerMarker WorldUpdateMarker = new ProfilerMarker("World.Update.Custom");

    void Update()
    {
        using (WorldUpdateMarker.Auto())
        {
            //checkedIndexes.Clear();

            //prevWaterVoxelsIndexes.Clear();
            //prevWaterVoxelsIndexes.UnionWith(waterVoxelsIndexes);

            //foreach (int VoxelIndex in prevWaterVoxelsIndexes)
            //{
            //    if (Voxels[VoxelIndex].isSettled)
            //        continue;

            //    (int x, int y, int z) = GetVoxelCoordinates(VoxelIndex);
            //    //GetVoxelCoordinates(VoxelIndex, out int x, out int y, out int z);

            //    HandleVoxelStep(x, y, z);
            //    HandleVoxelNeigbours(x, y, z, HandleVoxelStep);
            //}

            for (int x = 0; x < worldSizeX; x++)
            {
                for (int z = 0; z < worldSizeZ; z++)
                {
                    for (int y = 0; y < worldSizeY; y++)
                    {
                        int VoxelIndex = GetIndex(x, y, z);
                        if (previousVoxels[VoxelIndex].isSolid)
                            continue;
                        if (previousVoxels[VoxelIndex].isSettled)
                            continue;
                        //GetVoxelCoordinates(VoxelIndex, out int xx, out int yy, out int zz);

                        HandleVoxelStep(x, y, z);
                        HandleVoxelNeigbours(x, y, z, HandleVoxelStep);
                    }
                }
            }

            previousVoxels = Voxels.Clone() as Voxel[];

            //Array.Clear(checkedIndexes, 0, lastCheckedIndex);
            //lastCheckedIndex = 0;
        }

        ReRenderVoxels();
    }

    //void OnDestroy()
    //{
    //    waterVoxelsIndexes.Dispose();
    //    prevWaterVoxelsIndexes.Dispose();
    //    checkedIndexes.Dispose();
    //}

    private static void HandleVoxelNeigbours(int x, int y, int z, Action<int, int, int> func)
    {
        func(x + 1, y, z);
        func(x - 1, y, z);
        func(x, y + 1, z);
        func(x, y - 1, z);
        func(x, y, z + 1);
        func(x, y, z - 1);
    }

    private void HandleVoxelStep(int x, int y, int z)
    {
        int VoxelIndex = GetIndex(x, y, z);
        if (!IsValidPosition(x, y, z) || previousVoxels[VoxelIndex].isSolid)
        {
            return;
        }

        bool hasFlownDown = FlowDown(x, y, z);
        if (!hasFlownDown)
            FlowSideways(x, y, z);
    }

    private void ReRenderVoxels()
    {
        for (int x = 0; x < worldSizeX; x++)
        {
            for (int z = 0; z < worldSizeZ; z++)
            {
                for (int y = 0; y < worldSizeY; y++)
                {
                    int VoxelIndex = GetIndex(x, y, z);
                    if (Voxels[VoxelIndex].isSettled) continue;
                    if (VoxelObjects[VoxelIndex] == null || Voxels[VoxelIndex].isSolid) continue;
                    ReRenderVoxel(VoxelIndex);
                }
            }
        }
        //foreach (int VoxelIndex in waterVoxelsIndexes)
        //{
        //    if (Voxels[VoxelIndex].isSettled) continue;
        //    if (VoxelObjects[VoxelIndex] == null || Voxels[VoxelIndex].isSolid) continue;
        //    ReRenderVoxel(VoxelIndex);
        //}
    }

    private void InitVoxels()
    {
        //CaveGenerator caveGen = new CaveGenerator(worldSizeX, worldSizeY, worldSizeZ, noiseScale, threshold);
        //(Voxels, VoxelObjects) = caveGen.Generate(solidPrefab);

        //FillFirstEmptyVoxelWithWater(true);

        Voxels = new Voxel[worldSizeX * worldSizeY * worldSizeZ];
        VoxelObjects = new GameObject[worldSizeX * worldSizeY * worldSizeZ];
        int VoxelIndex = GetIndex(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        Voxels[VoxelIndex] = CreateWaterVoxel(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        Voxels[VoxelIndex].isSource = true;

        previousVoxels = Voxels.Clone() as Voxel[];
    }

    private void FillFirstEmptyVoxelWithWater(bool isSource)
    {
        for (int y = worldSizeY - 1; y > 0; y--)
        {
            for (int x = worldSizeX - 1; x > 0; x--)
            {
                for (int z = worldSizeZ - 1; z > 0; z--)
                {
                    if (!Voxels[x + worldSizeX * (y + worldSizeY * z)].isSolid)
                    {
                        Voxel waterVoxel = CreateWaterVoxel(x, y - 1, z);
                        waterVoxel.isSource = isSource;
                        Voxels[x + worldSizeX * (y + worldSizeY * z)] = waterVoxel;
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

        int VoxelIndex = GetIndex(x, y, z);
        int VoxelIndexBellow = GetIndex(x, y - 1, z);
        int VoxelIndexAbove = GetIndex(x, y + 1, z);

        if (y + 1 < worldSizeY && !previousVoxels[VoxelIndexAbove].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[VoxelIndex].fluidLevel);
            fluidIn = Clamp(previousVoxels[VoxelIndexAbove].fluidLevel, 0, availableSpace);
        }

        if (y - 1 >= 0 && !previousVoxels[VoxelIndexBellow].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[VoxelIndexBellow].fluidLevel);
            fluidOut = Clamp(previousVoxels[VoxelIndex].fluidLevel, 0, availableSpace);
        }

        byte fluidLevel = (byte)(previousVoxels[VoxelIndex].fluidLevel + fluidIn - fluidOut);

        if (previousVoxels[VoxelIndex].isSource)
        {
            fluidLevel = maxFluidLevel;
        }

        if (Voxels[VoxelIndex].Equals(defaultVoxel) && fluidLevel == 0)
        {
            return false;
        }

        UpdateVoxelFluid(x, y, z, fluidLevel);

        if (Voxels[VoxelIndex].fluidLevel == previousVoxels[VoxelIndex].fluidLevel)
        {
            Voxels[VoxelIndex].isSettled = true;
            return previousVoxels[VoxelIndex].isSource;
        }
        return true;
    }

    private void FlowSideways(int x, int y, int z)
    {
        int VoxelIndex = GetIndex(x, y, z);

        if (previousVoxels[VoxelIndex].isSource)
            return;

        //var prevNeighbours = GetValidNeighbours(x, y, z);
        //int neighbourCount = CountFlowableNeighbours(prevNeighbours);
        int[] prevNeighbours = new int[4];
        int neighbourCount = GetValidNeighbours(x, y, z, prevNeighbours);

        if (neighbourCount == 0)
            return;

        //var (fluidIn, fluidOut) = CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount);

        CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount, out byte fluidIn, out byte fluidOut);

        if (fluidIn == 0 && fluidOut == 0 && previousVoxels[VoxelIndex].fluidLevel != 0)
        {
            Voxels[VoxelIndex].isSettled = true;
            return;
        }

        byte newFluidLevel = (byte)(previousVoxels[VoxelIndex].fluidLevel + fluidIn - fluidOut);

        if (newFluidLevel == 0 && Voxels[VoxelIndex].Equals(defaultVoxel))
            return;

        UpdateVoxelFluid(x, y, z, newFluidLevel);
    }

    //private (Voxel Voxel, int VoxelIndex, bool CanBeFilled)[] GetValidNeighbours(int x, int y, int z)
    //{
    //    var neighbours = new (Voxel Voxel, int VoxelIndex, bool CanBeFilled)[4];

    //    CheckAndSetNeighbour(x + 1, y, z, 0);
    //    CheckAndSetNeighbour(x - 1, y, z, 1);
    //    CheckAndSetNeighbour(x, y, z + 1, 2);
    //    CheckAndSetNeighbour(x, y, z - 1, 3);

    //    return neighbours;

    //    void CheckAndSetNeighbour(int nx, int ny, int nz, int index)
    //    {
    //        int neighbourIndex = GetIndex(nx, ny, nz);
    //        if (IsValidPosition(nx, ny, nz) && !previousVoxels[neighbourIndex].isSolid)
    //        {
    //            ref var neighbour = ref neighbours[index];
    //            neighbour.Voxel = previousVoxels[neighbourIndex];
    //            neighbour.VoxelIndex = neighbourIndex;
    //            neighbour.CanBeFilled = true;
    //        }
    //    }
    //}

    private int GetValidNeighbours(int x, int y, int z, int[] neighbourIndices)
    {
        int count = 0;

        TryAdd(x + 1, y, z);
        TryAdd(x - 1, y, z);
        TryAdd(x, y, z + 1);
        TryAdd(x, y, z - 1);

        return count;

        void TryAdd(int nx, int ny, int nz)
        {
            if (!IsValidPosition(nx, ny, nz)) return;

            int i = GetIndex(nx, ny, nz);
            if (previousVoxels[i].isSolid) return;
            if (CanVoxelFlowDown(i)) return;

            neighbourIndices[count++] = i;
        }
    }

    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < worldSizeX &&
               y >= 0 && y < worldSizeY &&
               z >= 0 && z < worldSizeZ;
    }

    //private int CountFlowableNeighbours((Voxel Voxel, int VoxelIndex, bool CanBeFilled)[] neighbours)
    //{
    //    int count = 0;
    //    foreach (var nb in neighbours)
    //    {
    //        if (nb.CanBeFilled && !CanVoxelFlowDown(nb.VoxelIndex))
    //            count++;
    //    }
    //    return count;
    //}

    private int CountFlowableNeighbours(int[] neighbours)
    {
        int count = 0;
        foreach (var nbIdx in neighbours)
        {
            if (nbIdx != 0 && !CanVoxelFlowDown(nbIdx))
                count++;
        }
        return count;
    }

    //private (byte, byte) CalculateFluidFlow(
    //    int x, int y, int z,
    //    (Voxel Voxel, int neighbourIndex, bool CanBeFilled)[] neighbours,
    //    int neighbourCount)
    //{
    //    byte fluidIn = 0;
    //    byte fluidOut = 0;
    //    int VoxelIndex = GetIndex(x, y, z);
    //    Voxel currentVoxel = previousVoxels[VoxelIndex];

    //    foreach (var nb in neighbours)
    //    {
    //        if (!nb.CanBeFilled || CanVoxelFlowDown(nb.neighbourIndex))
    //            continue;

    //        if (currentVoxel.fluidLevel == 0 && nb.Voxel.fluidLevel == 0)
    //            continue;

    //        if (nb.Voxel.fluidLevel > currentVoxel.fluidLevel)
    //        {
    //            if (nb.Voxel.fluidLevel - currentVoxel.fluidLevel == 1)
    //                fluidIn = 1;
    //            else
    //                fluidIn += Diff(nb.Voxel.fluidLevel, currentVoxel.fluidLevel, neighbourCount);
    //        }

    //        if (nb.Voxel.fluidLevel < currentVoxel.fluidLevel)
    //            fluidOut += Diff(currentVoxel.fluidLevel, nb.Voxel.fluidLevel, neighbourCount);
    //    }

    //    return (fluidIn, fluidOut);
    //}

    private void CalculateFluidFlow(
    int x, int y, int z,
    int[] neighbours,
    int neighbourCount,
    out byte fluidIn, out byte fluidOut)
    {
        fluidIn = 0;
        fluidOut = 0;
        int VoxelIndex = GetIndex(x, y, z);
        Voxel currentVoxel = previousVoxels[VoxelIndex];

        foreach (var nbIdx in neighbours)
        {
            if (nbIdx == 0 || CanVoxelFlowDown(nbIdx))
                continue;

            ref Voxel neighbourVoxel = ref previousVoxels[nbIdx];
            //Voxel neighbourVoxel = previousVoxels[nbIdx];
            if (currentVoxel.fluidLevel == 0 && neighbourVoxel.fluidLevel == 0)
                continue;

            if (neighbourVoxel.fluidLevel > currentVoxel.fluidLevel)
            {
                if (neighbourVoxel.fluidLevel - currentVoxel.fluidLevel == 1)
                    fluidIn = 1;
                else
                    fluidIn += Diff(neighbourVoxel.fluidLevel, currentVoxel.fluidLevel, neighbourCount);
            }

            if (neighbourVoxel.fluidLevel < currentVoxel.fluidLevel)
                fluidOut += Diff(currentVoxel.fluidLevel, neighbourVoxel.fluidLevel, neighbourCount);
        }
    }

    private void UpdateVoxelFluid(int x, int y, int z, byte fluidLevel)
    {
        int VoxelIndex = GetIndex(x, y, z);
        ref Voxel Voxel = ref Voxels[VoxelIndex];

        HandleVoxelNeigbours(x, y, z, (nx, ny, nz) =>
        {
            int nbVoxelIndex = GetIndex(x, y, z);
            if (!IsValidPosition(nx, ny, nz)) return;
            if (Voxels[nbVoxelIndex].isSettled)
            {
                Voxels[nbVoxelIndex].isSettled = false;
            }
        });

        if (Voxel.Equals(defaultVoxel))
        {
            Voxel = CreateWaterVoxel(x, y, z, fluidLevel);
        }
        else
        {
            Voxel.fluidLevel = fluidLevel;
        }

        if (Voxel.fluidLevel < minFluidLevel)
        {
            DeleteVoxel(VoxelIndex);
        }
    }

    private Voxel CreateWaterVoxel(int x, int y, int z, byte? fluidLevel = null)
    {
        if (fluidLevel == null)
            fluidLevel = maxFluidLevel;

        Voxel Voxel = new Voxel
        {
            isSolid = false,
            fluidLevel = (byte)fluidLevel,
            isSource = false,
        };

        int VoxelIndex = GetIndex(x, y, z);
        VoxelObjects[VoxelIndex] = Instantiate(waterPrefab, new Vector3(x, y, z), Quaternion.identity);
        return Voxel;
    }

    private void CreateSolidVoxel(int x, int y, int z)
    {
        Voxel solidVoxel = new Voxel
        {
            isSolid = true,
            fluidLevel = 0,
        };

        int VoxelIndex = x + worldSizeX * (y + worldSizeY * z);
        Voxels[VoxelIndex] = solidVoxel;
        VoxelObjects[VoxelIndex] = Instantiate(solidPrefab, new Vector3(x, y, z), Quaternion.identity);
    }

    private void DeleteVoxel(int VoxelIndex)
    {
        Voxels[VoxelIndex] = default;
        Destroy(VoxelObjects[VoxelIndex]);
    }

    private void ReRenderVoxel(int VoxelIndex)
    {
        GetVoxelCoordinates(VoxelIndex, out int x, out int y, out int z);
        //(int x, int y, int z) = GetVoxelCoordinates(VoxelIndex);
        GameObject VoxelObject = VoxelObjects[VoxelIndex];

        float scaleY = Voxels[VoxelIndex].fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        VoxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        VoxelObject.transform.position = new Vector3(x, y - newY, z);
    }

    private bool CanVoxelFlowDown(int VoxelIndex)
    {
        GetVoxelCoordinates(VoxelIndex, out int x, out int y, out int z);
        //(int x, int y, int z) = GetVoxelCoordinates(VoxelIndex);

        if (y == 0) return false;

        int belowIndex = GetIndex(x, y - 1, z);

        ref Voxel belowVoxel = ref previousVoxels[belowIndex];
        //Voxel belowVoxel = previousVoxels[belowIndex];
        if (belowVoxel.isSolid) return false;

        ref Voxel currentVoxel = ref previousVoxels[VoxelIndex];
        //Voxel currentVoxel = previousVoxels[VoxelIndex];

        int available = maxFluidLevel - belowVoxel.fluidLevel;
        return available > 0 && currentVoxel.fluidLevel > 0;
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

    //private (int x, int y, int z) GetVoxelCoordinates(int index)
    void GetVoxelCoordinates(int index, out int x, out int y, out int z)
    {
        //int x = index % worldSizeX;
        //int yz = index / worldSizeX;
        //int y = yz % worldSizeY;
        //int z = yz / worldSizeY;
        //return (x, y, z);
        x = index % worldSizeX;
        int yz = index / worldSizeX;
        y = yz % worldSizeY;
        z = yz / worldSizeY;
    }
}
