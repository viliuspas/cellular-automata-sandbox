using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class World : MonoBehaviour
{
    public Vector3Int worldSize = new Vector3Int(20, 20, 20);
    public GameObject solidPrefab;
    public GameObject waterPrefab;

    private const byte maxFluidLevel = 127;
    private const byte minFluidLevel = 1;

    private int worldSizeX;
    private int worldSizeY;
    private int worldSizeZ;

    private Voxel[] voxels;
    private Voxel[] previousVoxels;
    private static GameObject[] voxelObjects;

    private Voxel defaultVoxel = new Voxel();

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

    static readonly ProfilerMarker WorldUpdateMarker = new ProfilerMarker("World.Update.Custom");

    void Update()
    {
        using (WorldUpdateMarker.Auto())
        {
            checkedIndexes.Clear();

            prevWaterVoxelsIndexes.Clear();
            prevWaterVoxelsIndexes.UnionWith(waterVoxelsIndexes);

            foreach (int voxelIndex in prevWaterVoxelsIndexes)
            {
                if (voxels[voxelIndex].isSettled)
                    continue;

                //(int x, int y, int z) = GetVoxelCoordinates(voxelIndex);
                GetVoxelCoordinates(voxelIndex, out int x, out int y, out int z);

                HandleVoxelStep(x, y, z);
                HandleVoxelNeigbours(x, y, z, HandleVoxelStep);
            }

            previousVoxels = voxels.Clone() as Voxel[];
        }

        ReRenderVoxels();
    }

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
            ReRenderVoxel(voxelIndex);
        }
    }

    private void InitVoxels()
    {
        //CaveGenerator caveGen = new CaveGenerator(worldSizeX, worldSizeY, worldSizeZ, noiseScale, threshold);
        //(voxels, voxelObjects) = caveGen.Generate(solidPrefab);

        //FillFirstEmptyVoxelWithWater(true);

        voxels = new Voxel[worldSizeX * worldSizeY * worldSizeZ];
        voxelObjects = new GameObject[worldSizeX * worldSizeY * worldSizeZ];
        int voxelIndex = GetIndex(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        voxels[voxelIndex] = CreateWaterVoxel(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        voxels[voxelIndex].isSource = true;

        previousVoxels = voxels.Clone() as Voxel[];
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
                        Voxel waterVoxel = CreateWaterVoxel(x, y - 1, z);
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

        //var prevNeighbours = GetValidNeighbours(x, y, z);
        //int neighbourCount = CountFlowableNeighbours(prevNeighbours);
        int[] prevNeighbours = new int[4];
        int neighbourCount = GetValidNeighbours(x, y, z, prevNeighbours);

        if (neighbourCount == 0)
            return;

        //var (fluidIn, fluidOut) = CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount);

        CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount, out byte fluidIn, out byte fluidOut);

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

    //private (Voxel Voxel, int voxelIndex, bool CanBeFilled)[] GetValidNeighbours(int x, int y, int z)
    //{
    //    var neighbours = new (Voxel Voxel, int voxelIndex, bool CanBeFilled)[4];

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
    //            neighbour.voxelIndex = neighbourIndex;
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

    //private int CountFlowableNeighbours((Voxel Voxel, int voxelIndex, bool CanBeFilled)[] neighbours)
    //{
    //    int count = 0;
    //    foreach (var nb in neighbours)
    //    {
    //        if (nb.CanBeFilled && !CanVoxelFlowDown(nb.voxelIndex))
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
    //    //out byte fluidIn, out byte fluidOut)
    //{
    //    byte fluidIn = 0;
    //    byte fluidOut = 0;
    //    int voxelIndex = GetIndex(x, y, z);
    //    Voxel currentVoxel = previousVoxels[voxelIndex];

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
        int voxelIndex = GetIndex(x, y, z);
        Voxel currentVoxel = previousVoxels[voxelIndex];

        foreach (var nbIdx in neighbours)
        {
            if (nbIdx == 0 || CanVoxelFlowDown(nbIdx))
                continue;

            ref Voxel neighbourVoxel = ref previousVoxels[nbIdx];
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
        int voxelIndex = GetIndex(x, y, z);
        ref Voxel voxel = ref voxels[voxelIndex];

        HandleVoxelNeigbours(x, y, z, (nx, ny, nz) =>
        {
            if (!IsValidPosition(nx, ny, nz)) return;
            if (voxels[voxelIndex].isSettled)
            {
                voxels[voxelIndex].isSettled = false;
            }
        });

        if (voxel.Equals(defaultVoxel))
        {
            voxel = CreateWaterVoxel(x, y, z, fluidLevel);
        }
        else
        {
            voxel.fluidLevel = fluidLevel;
        }

        if (voxel.fluidLevel < minFluidLevel)
        {
            DeleteVoxel(voxelIndex);
        }
    }

    private Voxel CreateWaterVoxel(int x, int y, int z, byte? fluidLevel = null)
    {
        if (fluidLevel == null)
            fluidLevel = maxFluidLevel;

        Voxel voxel = new Voxel
        {
            isSolid = false,
            fluidLevel = (byte)fluidLevel,
            isSource = false,
        };

        int voxelIndex = GetIndex(x, y, z);
        voxelObjects[voxelIndex] = Instantiate(waterPrefab, new Vector3(x, y, z), Quaternion.identity);
        waterVoxelsIndexes.Add(voxelIndex);
        return voxel;
    }

    private void CreateSolidVoxel(int x, int y, int z)
    {
        Voxel solidVoxel = new Voxel
        {
            isSolid = true,
            fluidLevel = 0,
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

    private void ReRenderVoxel(int voxelIndex)
    {
        GetVoxelCoordinates(voxelIndex, out int x, out int y, out int z);
        GameObject voxelObject = voxelObjects[voxelIndex];

        float scaleY = voxels[voxelIndex].fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxelObject.transform.position = new Vector3(x, y - newY, z);
    }

    private bool CanVoxelFlowDown(int voxelIndex)
    {
        GetVoxelCoordinates(voxelIndex, out int x, out int y, out int z);

        if (y == 0) return false;

        int belowIndex = GetIndex(x, y - 1, z);

        ref Voxel belowVoxel = ref previousVoxels[belowIndex];
        if (belowVoxel.isSolid) return false;

        ref Voxel currentVoxel = ref previousVoxels[voxelIndex];

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

    //private (int x, int y, int z) GetVoxelCoordinates(int voxelIndex)
    void GetVoxelCoordinates(int index, out int x, out int y, out int z)
    {
        x = index % worldSizeX;
        int yz = index / worldSizeX;
        y = yz % worldSizeY;
        z = yz / worldSizeY;
    }
}
