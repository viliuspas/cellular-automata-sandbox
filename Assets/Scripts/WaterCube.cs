using System;
using UnityEngine;

public class WaterCube : Cube
{
    public float waterLevel = 1f;

    protected override void Start() => InitMesh(waterLevel);

}
