using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using UnityEngine;

public class Terrain : MonoBehaviour
{
    public Vector3Int platform = new Vector3Int(15, 5, 15);
    public Vector3Int worldSize = new Vector3Int(256, 256, 256);
    public Material waterMat;
    public Material groundMat;

    private Cube[,,] cubes;

    void Start()
    {
        InitWorldArray();
        InitPlatform();
        InitGroundCubes();
        InitWaterCubes();
    }

    void Update()
    {
        for (int x = 0; x < worldSize.x - 1; x++)
        {
            for (int y = 0; y < worldSize.y - 1; y++)
            {
                for (int z = 0; z < worldSize.z - 1; z++)
                {
                    Water_CheckBellow(cubes[x, y, z]);
                }
            }
        }
    }

    private void InitWorldArray() => cubes = new Cube[worldSize.x, worldSize.y, worldSize.z];

    private void InitPlatform()
    {
        for (int x = 0; x < platform.x; x++)
        {
            for (int z = 0; z < platform.z; z++)
            {
                cubes[x, platform.y, z] = CreateGroundCube(x, platform.y, z);
            }
        }
    }

    private void InitWaterCubes()
    {
        cubes[3, 5, 3] = CreateWaterCube(3, platform.y + 5, 3);
    }

    private void InitGroundCubes()
    {
        int y = platform.y + 1;
        for (int x = 0; x < 10; x++)
        {
            cubes[3, 5, 3] = CreateGroundCube(x, y, 0);
            cubes[3, 5, 3] = CreateGroundCube(x, y, 10);
        }

        for (int z = 0; z < 10; z++)
        {
            cubes[3, 5, 3] = CreateGroundCube(0, y, z);
            cubes[3, 5, 3] = CreateGroundCube(10, y, z);
        }
    }

    private void Water_CheckBellow(Cube cube)
    {
        if (cube is not WaterCube) return;

        int x = cube.pos.x;
        int y = cube.pos.y;
        int z = cube.pos.z;

        if (y - 1 >= 0 && cubes[x, y - 1, z] == null)
        {
            cubes[x, y, z].DestroyCube();
            cubes[x, y, z] = null;

            cubes[x, y - 1, z] = CreateWaterCube(x, y - 1, z);
        }
        else
        {
            Water_CheckSides(cubes[x, y, z]);
        }
    }

    private void Water_CheckFlowDirection(Cube cube)
    {
        if (cube is not WaterCube waterCube || waterCube.waterLevel <= 0) return;

        if (waterCube.flowDirection != FlowDirection.None)
        {

        }

    }

    private void Water_CheckSides(Cube cube)
    {
        if (cube is not WaterCube waterCube || waterCube.waterLevel <= 0) return;

        int x = cube.pos.x;
        int y = cube.pos.y;
        int z = cube.pos.z;
        Debug.Log($"center xyz: {x} {y} {z}");

        if (cubes[x + 1, y, z] == null)
        {
            DistributeFlow(FlowDirection.East, x + 1, z);
            if (waterCube.waterLevel <= 0) return;
        }
        
        if (x - 1 >= 0 && cubes[x - 1, y, z] == null)
        {
            DistributeFlow(FlowDirection.West, x - 1, z);
            if (waterCube.waterLevel <= 0) return;
        }
        
        if (cubes[x, y, z + 1] == null)
        {
            DistributeFlow(FlowDirection.North, x, z + 1);
            if (waterCube.waterLevel <= 0) return;
        }
        
        if (z - 1 >= 0 && cubes[x, y, z - 1] == null)
        {
            DistributeFlow(FlowDirection.South, x, z - 1);
            if (waterCube.waterLevel <= 0) return;
        }

        void DistributeFlow(FlowDirection dir, int nX, int nZ)
        {
            waterCube.waterLevel -= 0.1f;

            WaterCube dirCube = CreateWaterCube(nX, y, nZ, 0.1f);
            dirCube.flowDirection = dir;
            cubes[nX, y, nZ] = dirCube;
            Debug.Log($"distributeFlow cube pos: {dirCube.pos}, {dir}, xyz: {nX} {y} {nZ}");

            if (waterCube.waterLevel <= 0)
            {
                cubes[x, y, z].DestroyCube();
                cubes[x, y, z] = null;
            }
        }
    }

    private GroundCube CreateGroundCube(int x, int y, int z)
    {
        GroundCube cube = new GameObject("GroundCube").AddComponent<GroundCube>();
        cube.pos = new Vector3Int(x, y ,z);
        cube.GetComponent<Renderer>().material = groundMat;
        return cube;
    }
    private WaterCube CreateWaterCube(int x, int y, int z, float? waterLevel = 1f)
    {
        WaterCube cube = new GameObject("WaterCube").AddComponent<WaterCube>();
        cube.pos = new Vector3Int(x, y ,z);
        cube.waterLevel = waterLevel ?? 1f;
        cube.GetComponent<Renderer>().material = waterMat;
        return cube;
    }
}
