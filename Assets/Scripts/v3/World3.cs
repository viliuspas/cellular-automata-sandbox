using System;
using UnityEngine;

public class World3 : MonoBehaviour
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

    [SerializeField] float noiseScale = 1f;
    [SerializeField] float threshold = 0.5f;

    float lastNoiseScale, lastThreshold;


    void Start()
    {
        InitVoxels();
    }

    void Update()
    {
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

    private void OnValidate()
    {
        if (noiseScale != lastNoiseScale || threshold != lastThreshold)
        {
            for (int z = 0; z < worldSize.z; z++)
            {
                for (int x = 0; x < worldSize.x; x++)
                {
                    for (int y = 0; y < worldSize.y; y++)
                    {
                        DeleteVoxel(x, y, z);
                    }
                }
            }

            InitVoxels();
            lastNoiseScale = noiseScale;
            lastThreshold = threshold;
        }
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
        CaveGenerator caveGen = new CaveGenerator(worldSize.x, worldSize.y, worldSize.z, noiseScale, threshold);
        (voxels, voxelObjects) = caveGen.Generate(solidPrefab);

        FillFirstEmptyVoxelWithWater(true);

        previousVoxels = voxels.Clone() as Voxel3[,,];
    }

    private void FillFirstEmptyVoxelWithWater(bool isSource)
    {
        for (int y = worldSize.y - 1; y > 0; y--)
        {
            for (int x = worldSize.x - 1; x > 0; x--)
            {
                for (int z = worldSize.z - 1; z > 0; z--)
                {
                    if (!voxels[x, y, z].isSolid)
                    {
                        Voxel3 waterVoxel = CreateWaterVoxel(x, y - 1, z);
                        waterVoxel.isSource = isSource;
                        voxels[x, y, z] = waterVoxel;
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

        if (voxels[x, y, z].Equals(defaultVoxel) && fluidLevel == 0)
        {
            return false;
        }

        UpdateVoxelFluid(x, y, z, fluidLevel);

        if (voxels[x,y,z].fluidLevel == previousVoxels[x,y,z].fluidLevel)
            return false;
        return true;
    }

    private void FlowSideways(int x, int y, int z)
    {
        if (previousVoxels[x, y, z].isSource)
            return;

        var prevNeighbours = GetValidNeighbours(x, y, z);
        int neighbourCount = CountFlowableNeighbours(prevNeighbours);
    
        if (neighbourCount == 0)
            return;

        var (fluidIn, fluidOut) = CalculateFluidFlow(x, y, z, prevNeighbours, neighbourCount);
    
        if (fluidIn == 0 && fluidOut == 0)
            return;

        byte newFluidLevel = (byte)(previousVoxels[x, y, z].fluidLevel + fluidIn - fluidOut);
    
        if (newFluidLevel == 0 && voxels[x, y, z].Equals(defaultVoxel))
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
            if (IsValidPosition(nx, ny, nz) && !previousVoxels[nx, ny, nz].isSolid)
            {
                neighbours[index].Voxel = previousVoxels[nx, ny, nz];
                neighbours[index].Voxel.position = new Vector3(nx, ny, nz);
                neighbours[index].CanBeFilled = true;
            }
        }
    }

    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < worldSize.x && 
               y >= 0 && y < worldSize.y && 
               z >= 0 && z < worldSize.z;
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
        Voxel3 currentVoxel = previousVoxels[x, y, z];

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
        if (voxels[x, y, z].Equals(defaultVoxel))
        {
            voxels[x, y, z] = CreateWaterVoxel(x, y, z, fluidLevel);
        }
        else
        {
            voxels[x, y, z].fluidLevel = fluidLevel;
        }

        if (voxels[x, y, z].fluidLevel < minFluidLevel)
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
