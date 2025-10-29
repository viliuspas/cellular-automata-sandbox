using System;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.PlayerSettings;

public struct Voxel
{
    public bool isSolid;
    public GameObject voxelObject;
    public bool isSource;
    public bool wasUpdated;
    public Vector3 position;

    private byte _fluidLevel;
    public byte fluidLevel
    {
        get => _fluidLevel;
        set
        {
            _fluidLevel = value;
            if (voxelObject != null)
            {
                var scaleY = value / 127f;
                var newY = (1f - scaleY) / 2f;

                voxelObject.transform.localScale = new Vector3(1, scaleY, 1);

                var pos = voxelObject.transform.position;
                voxelObject.transform.position = new Vector3(pos.x, (float)Math.Ceiling(pos.y) - newY, pos.z);

            }
        }
    }
}
