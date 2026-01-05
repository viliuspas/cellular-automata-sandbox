using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class World3 : MonoBehaviour
{
    private const byte maxFluidLevel = 127;
    private const byte minFluidLevel = 1;

    private Voxel3[] voxels;
    private Voxel3[] previousVoxels;
    private static GameObject[] voxelObjects;
    private Voxel3 defaultVoxel = new Voxel3();
    private HashSet<int> waterVoxelsIndexes = new HashSet<int>();
    private HashSet<int> prevWaterVoxelsIndexes = new HashSet<int>();

    //private Vector3Int worldSize = new Vector3Int(30, 30, 30);
    private readonly int worldSizeX = 30;
    private readonly int worldSizeY = 30;
    private readonly int worldSizeZ = 30;

    public GameObject solidPrefab;
    public GameObject waterPrefab;

    [SerializeField] float noiseScale = 1f;
    [SerializeField] float threshold = 0.5f;

    float lastNoiseScale, lastThreshold;

    void Start()
    {
        lastNoiseScale = noiseScale;
        lastThreshold = threshold;
        InitVoxels();
    }

    void Update()
    {
        //for (int z = 0; z < worldSizeZ; z++)
        //{
        //    for (int x = 0; x < worldSizeX; x++)
        //    {
        //        for (int y = 0; y < worldSizeY; y++)
        //        {
        //            if (voxels[x + worldSizeX * (y + worldSizeY * z)].isSolid)
        //            {
        //                continue;
        //            }

        //            bool hasFlownDown = FlowDown(x, y, z);
        //            if (!hasFlownDown)
        //                FlowSideways(x, y, z);
        //        }
        //    }
        //}
        prevWaterVoxelsIndexes = new HashSet<int>(waterVoxelsIndexes);

        foreach (int voxelIndex in prevWaterVoxelsIndexes)
        {
            if (voxels[voxelIndex].isSettled)
                continue;

            int x = voxelIndex % worldSizeX;
            int yz = voxelIndex / worldSizeX;
            int y = yz % worldSizeY;
            int z = yz / worldSizeY;

            VoxelNeigbours(x, y, z, CheckNeighbourVoxel); // todo: remove duplicate checks
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

    private void CheckNeighbourVoxel(int x, int y, int z)
    {
        if (!IsValidPosition(x, y, z) || voxels[x + worldSizeX * (y + worldSizeY * z)].isSolid)
        {
            return;
        }

        bool hasFlownDown = FlowDown(x, y, z);
        if (!hasFlownDown)
            FlowSideways(x, y, z);
    }

    //private void OnValidate()
    //{
    //    if (noiseScale != lastNoiseScale || threshold != lastThreshold)
    //    {
    //        for (int z = 0; z < worldSizeZ; z++)
    //        {
    //            for (int x = 0; x < worldSizeX; x++)
    //            {
    //                for (int y = 0; y < worldSizeY; y++)
    //                {
    //                    DeleteVoxel(x, y, z);
    //                }
    //            }
    //        }

    //        InitVoxels();
    //        lastNoiseScale = noiseScale;
    //        lastThreshold = threshold;
    //    }
    //}

    private void ReRenderVoxels()
    {
        for (int z = 0; z < worldSizeZ; z++)
        {
            for (int x = 0; x < worldSizeX; x++)
            {
                for (int y = 0; y < worldSizeY; y++)
                {
                    if (voxelObjects[x + worldSizeX * (y + worldSizeY * z)] == null || voxels[x + worldSizeX * (y + worldSizeY * z)].isSolid) continue;
                    ReRenderVoxel(voxels[x + worldSizeX * (y + worldSizeY * z)], new Vector3(x, y, z));
                }
            }
        }
    }

    private void InitVoxels()
    {
        //CaveGenerator caveGen = new CaveGenerator(worldSizeX, worldSizeY, worldSizeZ, noiseScale, threshold);
        //(voxels, voxelObjects) = caveGen.Generate(solidPrefab);

        //FillFirstEmptyVoxelWithWater(true);

        voxels = new Voxel3[worldSizeX * worldSizeY * worldSizeZ];
        voxelObjects = new GameObject[worldSizeX * worldSizeY * worldSizeZ];
        voxels[GetIndex(15, 29, 15)] = CreateWaterVoxel(15, 29, 15);
        voxels[GetIndex(15, 29, 15)].isSource = true;

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

        int voxelIndex = x + worldSizeX * (y + worldSizeY * z);

        if (y + 1 < worldSizeY && !previousVoxels[x + worldSizeX * (y + 1 + worldSizeY * z)].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[voxelIndex].fluidLevel);
            fluidIn = Clamp(previousVoxels[x + worldSizeX * (y + 1 + worldSizeY * z)].fluidLevel, 0, availableSpace);
        }

        if (y - 1 >= 0 && !previousVoxels[x + worldSizeX * (y - 1 + worldSizeY * z)].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[x + worldSizeX * (y - 1 + worldSizeY * z)].fluidLevel);
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
            //waterVoxelsIndexes.Remove(voxelIndex);
            return false;
        }
        return true;
    }

    private void FlowSideways(int x, int y, int z)
    {
        int voxelIndex = x + worldSizeX * (y + worldSizeY * z);

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
            //waterVoxelsIndexes.Remove(voxelIndex);
            return;
        }
            
        byte newFluidLevel = (byte)(previousVoxels[voxelIndex].fluidLevel + fluidIn - fluidOut);
    
        if (newFluidLevel == 0 && voxels[voxelIndex].Equals(defaultVoxel))
            return;

        UpdateVoxelFluid(x, y, z, newFluidLevel);
    }

    private (Voxel3 Voxel, bool CanBeFilled)[] GetValidNeighbours(int x, int y, int z)
    {
        var neighbours = new (Voxel3 Voxel, bool CanBeFilled)[4];

        CheckAndSetNeighbour(x + 1, y, z, 0);
        CheckAndSetNeighbour(x - 1, y, z, 1);
        CheckAndSetNeighbour(x, y, z + 1, 2);
        CheckAndSetNeighbour(x, y, z - 1, 3);

        return neighbours;

        void CheckAndSetNeighbour(int nx, int ny, int nz, int index)
        {
            int neighbourIndex = nx + worldSizeX * (ny + worldSizeY * nz);
            if (IsValidPosition(nx, ny, nz) && !previousVoxels[neighbourIndex].isSolid)
            {
                neighbours[index].Voxel = previousVoxels[neighbourIndex];
                neighbours[index].Voxel.position = new Vector3(nx, ny, nz);
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

    private int CountFlowableNeighbours((Voxel3 Voxel, bool CanBeFilled)[] neighbours)
    {
        int count = 0;
        foreach (var nb in neighbours)
        {
            if (nb.CanBeFilled && !CanVoxelFlowDown(nb.Voxel))
                count++;
        }
        return count;
    }

    private (byte fluidIn, byte fluidOut) CalculateFluidFlow(
        int x, int y, int z, 
        (Voxel3 Voxel, bool CanBeFilled)[] neighbours, 
        int neighbourCount)
    {
        byte fluidIn = 0;
        byte fluidOut = 0;
        Voxel3 currentVoxel = previousVoxels[x + worldSizeX * (y + worldSizeY * z)];

        foreach (var nb in neighbours)
        {
            if (!nb.CanBeFilled || CanVoxelFlowDown(nb.Voxel))
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
        int voxelIndex = x + worldSizeX * (y + worldSizeY * z);

        VoxelNeigbours(x, y, z, (nx, ny, nz) =>
        {
            if (!IsValidPosition(nx, ny, nz)) return;
            int neighbourIndex = x + worldSizeX * (y + worldSizeY * z);
            if (voxels[neighbourIndex].isSettled)
            {
                voxels[neighbourIndex].isSettled = false;
                //waterVoxelsIndexes.Add(neighbourIndex);
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
            DeleteVoxel(x, y, z);
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

        int voxelIndex = (int)pos.x + worldSizeX * ((int)pos.y + worldSizeY * (int)pos.z);
        voxelObjects[voxelIndex] = Instantiate(waterPrefab, pos, Quaternion.identity);
        waterVoxelsIndexes.Add(voxelIndex);
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

        int voxelIndex = (int)pos.x + worldSizeX * ((int)pos.y + worldSizeY * (int)pos.z);
        voxels[voxelIndex] = solidVoxel;
        voxelObjects[voxelIndex] = Instantiate(solidPrefab, solidVoxel.position, Quaternion.identity);
    }

    private void DeleteVoxel(int x, int y, int z)
    {
        DeleteVoxel(new Vector3Int(x, y, z));
    }

    private void DeleteVoxel(Vector3 pos)
    {
        int voxelIndex = (int)pos.x + worldSizeX * ((int)pos.y + worldSizeY * (int)pos.z);
        voxels[voxelIndex] = default;
        Destroy(voxelObjects[voxelIndex]);
        waterVoxelsIndexes.Remove(voxelIndex);
    }

    private void ReRenderVoxel(Voxel3 voxel, Vector3 pos)
    {
        GameObject voxelObject = voxelObjects[(int)pos.x + worldSizeX * ((int)pos.y + worldSizeY * (int)pos.z)];

        float scaleY = voxel.fluidLevel / (float)maxFluidLevel;
        float newY = (1f - scaleY) / 2f;

        voxelObject.transform.localScale = new Vector3(1f, scaleY, 1f);
        voxelObject.transform.position = new Vector3(pos.x, (float)Math.Ceiling(pos.y) - newY, pos.z);
    }

    private bool CanVoxelFlowDown(Voxel3 voxel)
    {
        int x = (int)voxel.position.x;
        int y = (int)voxel.position.y;
        int z = (int)voxel.position.z;

        byte fluidOut = 0;
        byte availableSpace = 0;

        int bellowVoxelIndex = x + worldSizeX * (y - 1 + worldSizeY * z);
        if (y - 1 >= 0 && !previousVoxels[bellowVoxelIndex].isSolid)
        {
            availableSpace = (byte)(maxFluidLevel - previousVoxels[bellowVoxelIndex].fluidLevel);
            fluidOut = Clamp(previousVoxels[x + worldSizeX * (y + worldSizeY * z)].fluidLevel, 0, availableSpace);
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
