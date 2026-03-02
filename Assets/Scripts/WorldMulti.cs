using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;

public class WorldMulti : MonoBehaviour
{
    public Vector3Int worldSize = new Vector3Int(20, 20, 20);
    public GameObject solidPrefab;
    public GameObject waterPrefab;

    private const byte maxFluidLevel = 127;
    private const byte minFluidLevel = 1;

    private static int worldSizeX;
    private static int worldSizeY;
    private static int worldSizeZ;
    private int totalVoxels;

    private NativeArray<Voxel> writeVoxels;
    private NativeArray<Voxel> readVoxels;
    private NativeArray<bool> dirty;
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
        totalVoxels = worldSizeX * worldSizeY * worldSizeZ;

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

            NativeArray<Voxel>.Copy(readVoxels, writeVoxels);

            var job = new FluidSimulationJob
            {
                readVoxels = readVoxels,
                writeVoxels = writeVoxels,
                dirty = dirty,
                sizeX = worldSizeX,
                sizeY = worldSizeY,
                sizeZ = worldSizeZ,
                maxFluid = maxFluidLevel,
                minFluid = minFluidLevel
            };

            JobHandle handle = job.Schedule(totalVoxels, 64);
            handle.Complete();

            (readVoxels, writeVoxels) = (writeVoxels, readVoxels);
        }
        
        SyncGameObjects();
    }

    private void SyncGameObjects()
    {
        for (int i = 0; i < totalVoxels; i++)
        {
            //if (!dirty[i]) continue;
            //dirty[i] = false;

            Voxel v = readVoxels[i];

            if (v.fluidLevel < minFluidLevel && !v.isSolid)
            {
                if (voxelObjects[i] != null)
                {
                    Destroy(voxelObjects[i]);
                    voxelObjects[i] = null;
                }
            }
            else if (!v.isSolid && v.fluidLevel >= minFluidLevel)
            {
                IndexToXYZ(i, out int x, out int y, out int z);

                if (voxelObjects[i] == null)
                {
                    SpawnWaterVoxel(x, y, z);
                    HandleVoxelNeigbours(x,y,z, MarkAsDirtyIfValid);
                }

                ReRenderVoxel(i);
            }
        }
    }

    [BurstCompile(Debug=true)]
    private struct FluidSimulationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Voxel> readVoxels;
        [WriteOnly] public NativeArray<Voxel> writeVoxels;
        [NativeDisableParallelForRestriction] public NativeArray<bool> dirty;

        public int sizeX, sizeY, sizeZ;

        public byte maxFluid, minFluid;

        public void Execute(int index)
        {
            Voxel cur = readVoxels[index];

            if (!dirty[index]) return;
            if (cur.isSolid) return;
            if (cur.isSettled && !cur.isSource) return;

            IndexToXYZ(index, out int x, out int y, out int z);
            Debug.Log($"index: {index}, Processing voxel at ({x}, {y}, {z}) with fluid level {cur.fluidLevel} and isSource={cur.isSource}");

            if (cur.isSource)
            {
                cur.fluidLevel = maxFluid;
                cur.isSettled = false;
            }

            bool flowedDown = TryFlowDown(ref cur, x, y, z);
            bool flowedSideways = false;
            if (!flowedDown)
            {
                flowedSideways = TryFlowSideways(ref cur, x, y, z);
            }
            dirty[index] = flowedDown || flowedSideways;
            writeVoxels[index] = cur;
        }

        private bool TryFlowDown(ref Voxel cur, int x, int y, int z)
        {
            byte fluidIn = 0;
            byte fluidOut = 0;
            byte availableSpace = 0;

            int voxelIndexBellow = XYZToIndex(x, y - 1, z);
            int voxelIndexAbove = XYZToIndex(x, y + 1, z);

            if (y + 1 < sizeY && !readVoxels[voxelIndexAbove].isSolid)
            {
                availableSpace = (byte)(maxFluidLevel - cur.fluidLevel);
                fluidIn = Clamp(readVoxels[voxelIndexAbove].fluidLevel, 0, availableSpace);
            }

            if (y - 1 >= 0 && !readVoxels[voxelIndexBellow].isSolid)
            {
                availableSpace = (byte)(maxFluidLevel - readVoxels[voxelIndexBellow].fluidLevel);
                fluidOut = Clamp(cur.fluidLevel, 0, availableSpace);
            }

            byte fluidLevel = (byte)(cur.fluidLevel + fluidIn - fluidOut);

            if (cur.isSource)
            {
                fluidLevel = maxFluidLevel;
            }

            if (fluidOut == 0)
            {
                return false;
            }

            if (cur.Equals(new Voxel()) && fluidLevel == 0)
            {
                return false;
            }

            if (fluidLevel == cur.fluidLevel)
            {
                cur.isSettled = true;
                return cur.isSource;
            }

            cur.fluidLevel = fluidLevel;
            return true;
        }

        private bool TryFlowSideways(ref Voxel cur, int x, int y, int z)
        {
            if (cur.isSource)
                return false;

            NeighboursXZ prevNeighbours = new NeighboursXZ();
            int neighbourCount = GetValidNeighbours(x, y, z, ref prevNeighbours);

            if (neighbourCount == 0)
                return false;

            CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount, out byte fluidIn, out byte fluidOut);

            if (fluidIn == 0 && fluidOut == 0 && cur.fluidLevel != 0)
            {
                cur.isSettled = true;
                return false;
            }

            byte newFluidLevel = (byte)(cur.fluidLevel + fluidIn - fluidOut);

            if (newFluidLevel == 0 && cur.Equals(new Voxel()))
                return false;

            cur.fluidLevel = newFluidLevel;
            return true;

        }

        private int GetValidNeighbours(int x, int y, int z, ref NeighboursXZ neighbours)
        {
            int count = 0;

            TryAdd(x + 1, y, z, ref neighbours, ref count);
            TryAdd(x - 1, y, z, ref neighbours, ref count);
            TryAdd(x, y, z + 1, ref neighbours, ref count);
            TryAdd(x, y, z - 1, ref neighbours, ref count);

            return count;
        }

        private void TryAdd(int nx, int ny, int nz, ref NeighboursXZ neighbours, ref int count)
        {
            if (!IsValidPosition(nx, ny, nz)) return;

            int i = XYZToIndex(nx, ny, nz);
            if (readVoxels[i].isSolid) return;
            if (CanVoxelFlowDown(i)) return;

            neighbours[count++] = new Neighbour { index = i, isValid = true };
        }

        private void CalculateFluidFlow(
        int x, int y, int z,
        NeighboursXZ neighbours,
        int neighbourCount,
        out byte fluidIn, out byte fluidOut)
        {
            fluidIn = 0;
            fluidOut = 0;
            int voxelIndex = XYZToIndex(x, y, z);
            Voxel currentVoxel = readVoxels[voxelIndex];

            for (int nb = 0; nb < 4; nb++)
            {
                if (!neighbours[nb].isValid) continue;

                int nbIdx = neighbours[nb].index;

                if (CanVoxelFlowDown(nbIdx))
                    continue;

                Voxel neighbourVoxel = readVoxels[nbIdx];
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

        private bool CanVoxelFlowDown(int voxelIndex)
        {
            IndexToXYZ(voxelIndex, out int x, out int y, out int z);

            if (y == 0) return false;

            int belowIndex = XYZToIndex(x, y - 1, z);

            if (readVoxels[belowIndex].isSolid) return false;

            int available = maxFluidLevel - readVoxels[belowIndex].fluidLevel;
            return available > 0 && readVoxels[voxelIndex].fluidLevel > 0;
        }

        private bool IsValidPosition(int x, int y, int z)
        {
            return x >= 0 && x < sizeX &&
                   y >= 0 && y < sizeY &&
                   z >= 0 && z < sizeZ;
        }

        private void IndexToXYZ(int i, out int x, out int y, out int z)
        {
            x = i % sizeX;
            int yz = i / sizeX;
            y = yz % sizeY;
            z = yz / sizeY;
        }

        private int XYZToIndex(int x, int y, int z) => x + sizeX * (y + sizeY * z);

        private struct NeighboursXZ
        {
            private Neighbour _0, _1, _2, _3;
            public Neighbour this[int i]
            {
                readonly get => i switch { 0 => _0, 1 => _1, 2 => _2, _ => _3 };
                set
                {
                    switch (i)
                    {
                        case 0: _0 = value; break;
                        case 1: _1 = value; break;
                        case 2: _2 = value; break;
                        default: _3 = value; break;
                    }
                }
            }
        }

        private struct Neighbour
        {
            public bool isValid;
            public int index;
        }
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

    private void MarkAsDirtyIfValid(int x, int y, int z)
    {
        if (!IsValidPosition(x, y, z)) return;
        dirty[XYZToIndex(x, y, z)] = true;
    }

    private void ReRenderVoxels()
    {
        foreach (int voxelIndex in waterVoxelsIndexes)
        {
            if (writeVoxels[voxelIndex].isSettled) continue;
            if (voxelObjects[voxelIndex] == null || writeVoxels[voxelIndex].isSolid) continue;
            ReRenderVoxel(voxelIndex);
        }
    }

    private void InitVoxels()
    {
        readVoxels = new NativeArray<Voxel>(totalVoxels, Allocator.Persistent);
        writeVoxels = new NativeArray<Voxel>(totalVoxels, Allocator.Persistent);
        dirty = new NativeArray<bool>(totalVoxels, Allocator.Persistent);
        voxelObjects = new GameObject[totalVoxels];

        //int voxelIndex = GetIndex(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
        int voxelIndex = XYZToIndex(worldSizeX / 2, 0, worldSizeZ / 2);

        Voxel voxel = new Voxel { fluidLevel = maxFluidLevel, isSource = true };

        readVoxels[voxelIndex] = voxel;
        dirty[voxelIndex] = true;
        HandleVoxelNeigbours(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2, MarkAsDirtyIfValid);

        SpawnWaterVoxel(worldSizeX / 2, worldSizeY - 1, worldSizeZ / 2);
    }

    private void SpawnWaterVoxel(int x, int y, int z)
    {
        int voxelIndex = XYZToIndex(x, y, z);
        voxelObjects[voxelIndex] = Instantiate(waterPrefab, new Vector3(x, y, z), Quaternion.identity);
        UpdateFluidLevelText(voxelIndex, maxFluidLevel);

        waterVoxelsIndexes.Add(voxelIndex);
    }

    private void UpdateFluidLevelText(int voxelIndex, byte fluidLevel)
    {
        string value = fluidLevel.ToString();
        float offset = -0.5f;

        Vector3[] directions = {
        Vector3.forward, Vector3.back,
        Vector3.left, Vector3.right,
        Vector3.up, Vector3.down
    };

        Transform parent = voxelObjects[voxelIndex].transform;

        int existingCount = 0;
        foreach (Transform child in parent)
        {
            if (child.name.Contains("FluidLevelText"))
            {
                var tmp = child.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null)
                    tmp.text = value;

                existingCount++;
            }
        }

        if (existingCount == 6)
            return;

        foreach (var dir in directions)
        {
            GameObject textObj = new GameObject("FluidLevelText");
            textObj.transform.SetParent(parent);
            textObj.transform.localPosition = dir * offset;
            textObj.transform.localRotation = Quaternion.LookRotation(dir);

            var tmp = textObj.AddComponent<TMPro.TextMeshPro>();
            tmp.text = value;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.fontSize = 2;
        }
    }

    private void CreateSolidVoxel(int x, int y, int z)
    {
        Voxel solidVoxel = new Voxel
        {
            isSolid = true,
            fluidLevel = 0,
        };

        int VoxelIndex = x + worldSizeX * (y + worldSizeY * z);
        writeVoxels[VoxelIndex] = solidVoxel;
        voxelObjects[VoxelIndex] = Instantiate(solidPrefab, new Vector3(x, y, z), Quaternion.identity);
    }

    private void DeleteVoxel(int voxelIndex)
    {
        writeVoxels[voxelIndex] = default;
        Destroy(voxelObjects[voxelIndex]);
        waterVoxelsIndexes.Remove(voxelIndex);
    }

    private void ReRenderVoxel(int voxelIndex)
    {
        IndexToXYZ(voxelIndex, out int x, out int y, out int z);
        GameObject voxelObject = voxelObjects[voxelIndex];

        byte fluidLevel = readVoxels[voxelIndex].fluidLevel;

        float scaleY = fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxelObject.transform.position = new Vector3(x, y - newY, z);

        UpdateFluidLevelText(voxelIndex, fluidLevel);
    }

    private static bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < worldSizeX &&
               y >= 0 && y < worldSizeY &&
               z >= 0 && z < worldSizeZ;
    }

    private static void IndexToXYZ(int i, out int x, out int y, out int z)
    {
        x = i % worldSizeX;
        int yz = i / worldSizeX;
        y = yz % worldSizeY;
        z = yz / worldSizeY;
    }

    private static int XYZToIndex(int x, int y, int z) => x + worldSizeX * (y + worldSizeY * z);

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
