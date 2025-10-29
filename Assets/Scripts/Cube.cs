using UnityEngine;
using System.Collections.Generic;

public enum FlowDirection
{
    None,
    North,
    East,
    South,
    West
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public abstract class Cube : MonoBehaviour
{
    public Vector3Int pos;
    public FlowDirection flowDirection = FlowDirection.None;

    private Mesh mesh;
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();
    private readonly List<Vector2> uvs = new List<Vector2>();

    private int lastVertex;

    protected virtual void Start() => InitMesh();


    protected void InitMesh(float height = 1f)
    {
        // init mesh
        mesh = new Mesh();

        // create mesh data
        DrawCube(height);

        // set mesh data
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.SetUVs(0, uvs);

        // recalculate lighting
        mesh.RecalculateNormals();

        // set the mesh
        GetComponent<MeshFilter>().mesh = mesh;
    }

    public void DestroyCube()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            Destroy(meshFilter.mesh);
            meshFilter.mesh = null;
        }

        Destroy(gameObject);
    }

    private void DrawCube(float? height = null)
    {
        Front_GenerateFace(height);
        Back_GenerateFace(height);
        Right_GenerateFace(height);
        Left_GenerateFace(height);
        Top_GenerateFace(height);
        Bottom_GenerateFace();
    }

    private void Front_GenerateFace(float? height)
    {
        lastVertex = vertices.Count;

        // declare vertices
        vertices.Add(pos + new Vector3(0, 0, 0));   // 0
        vertices.Add(pos + new Vector3(0, GetHeight(height), 0));   // 1
        vertices.Add(pos + new Vector3(1, GetHeight(height), 0));   // 2
        vertices.Add(pos + new Vector3(1, 0, 0));   // 3

        AddTriangles(lastVertex);
    }

    private void Back_GenerateFace(float? height)
    {
        lastVertex = vertices.Count;

        // declare vertices
        vertices.Add(pos + new Vector3(0, 0, 1));   // 0
        vertices.Add(pos + new Vector3(1, 0, 1));   // 1
        vertices.Add(pos + new Vector3(1, GetHeight(height), 1));   // 2
        vertices.Add(pos + new Vector3(0, GetHeight(height), 1));   // 3

        AddTriangles(lastVertex);
    }

    private void Right_GenerateFace(float? height)
    {
        lastVertex = vertices.Count;

        // declare vertices
        vertices.Add(pos + new Vector3(1, 0, 0));   // 0
        vertices.Add(pos + new Vector3(1, GetHeight(height), 0));   // 1
        vertices.Add(pos + new Vector3(1, GetHeight(height), 1));   // 2
        vertices.Add(pos + new Vector3(1, 0, 1));   // 3

        AddTriangles(lastVertex);
    }

    private void Left_GenerateFace(float? height)
    {
        lastVertex = vertices.Count;

        // declare vertices
        vertices.Add(pos + new Vector3(0, 0, 0));   // 0
        vertices.Add(pos + new Vector3(0, 0, 1));   // 1
        vertices.Add(pos + new Vector3(0, GetHeight(height), 1));   // 2
        vertices.Add(pos + new Vector3(0, GetHeight(height), 0));   // 3

        AddTriangles(lastVertex);
    }

    private void Top_GenerateFace(float? height)
    {
        lastVertex = vertices.Count;

        // declare vertices
        vertices.Add(pos + new Vector3(0, GetHeight(height), 0));   // 0
        vertices.Add(pos + new Vector3(0, GetHeight(height), 1));   // 1
        vertices.Add(pos + new Vector3(1, GetHeight(height), 1));   // 2
        vertices.Add(pos + new Vector3(1, GetHeight(height), 0));   // 3

        AddTriangles(lastVertex);
    }

    private void Bottom_GenerateFace()
    {
        lastVertex = vertices.Count;

        // declare vertices
        vertices.Add(pos + new Vector3(0, 0, 0));   // 0
        vertices.Add(pos + new Vector3(1, 0, 0));   // 1
        vertices.Add(pos + new Vector3(1, 0, 1));   // 2
        vertices.Add(pos + new Vector3(0, 0, 1));   // 3

        AddTriangles(lastVertex);
    }

    private void AddTriangles(int lastVertex)
    {
        // first triangle
        triangles.Add(lastVertex + 0);
        triangles.Add(lastVertex + 1);
        triangles.Add(lastVertex + 3);

        // second triangle
        triangles.Add(lastVertex + 1);
        triangles.Add(lastVertex + 2);
        triangles.Add(lastVertex + 3);
    }

    private static float GetHeight(float? height)
    {
        return height.HasValue ? Mathf.Clamp((float)height, 0f, 1f) : 1f;
    }
}
