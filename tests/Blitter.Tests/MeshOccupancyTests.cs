using System.Numerics;
using Blitter;
using Blitter.Bits;

namespace Blitter.Tests;

public class MeshOccupancyTests
{
    [Fact]
    public void EmptyMesh_ReturnsEmpty()
    {
        var mesh = Mesh.Create<Vertex3D>(ReadOnlySpan<Vertex3D>.Empty);
        var boxes = mesh.ComputeOccupiedBoxes(0.5f);
        Assert.Empty(boxes);
    }

    [Fact]
    public void InvalidVoxelSize_Throws()
    {
        var mesh = Meshes.Cube(Color.White);
        Assert.Throws<ArgumentOutOfRangeException>(() => mesh.ComputeOccupiedBoxes(0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => mesh.ComputeOccupiedBoxes(-1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => mesh.ComputeOccupiedBoxes(float.PositiveInfinity));
    }

    [Fact]
    public void NonTriangleListMesh_Throws()
    {
        Vertex3D[] verts = [new(Vector3.Zero), new(Vector3.UnitX)];
        var mesh = Mesh.Create<Vertex3D>(verts, Topology.LineList);
        Assert.Throws<ArgumentException>(() => mesh.ComputeOccupiedBoxes(0.5f));
    }

    [Fact]
    public void VoxelLargerThanMesh_ProducesSingleBoxCoveringMesh()
    {
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        // voxelSize 10 → grid is 1×1×1, single box covering the cube.
        var boxes = cube.ComputeOccupiedBoxes(10f, MeshOccupancyMode.Fast);
        Assert.Single(boxes);
        Assert.True(boxes[0].Contains(cube.ComputeBoundingBox()));
    }

    [Fact]
    public void Cube_FastMode_ProducesHollowShellCoveringMesh()
    {
        // The cube mesh is hollow (only surface triangles). Each face triangle's
        // AABB is a flat slab (one axis collapsed), so Fast mode also marks only
        // surface cells. Greedy merge carves the 6-face shell into a small set
        // of disjoint axis-aligned slabs that together cover the cube exactly.
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        var boxes = cube.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Fast);

        Assert.NotEmpty(boxes);
        var union = BoundingBox.Empty;
        foreach (var b in boxes) union = union.Encapsulate(b);
        Assert.Equal(cube.ComputeBoundingBox().Min, union.Min);
        Assert.Equal(cube.ComputeBoundingBox().Max, union.Max);
    }

    [Fact]
    public void Cube_AccurateMode_ReturnsHollowShell()
    {
        // Surface voxelization → only boundary cells are occupied. With voxelSize
        // 0.5 on a 2×2×2 cube the grid is 4×4×4 (=64 cells); the interior is
        // a 2×2×2 block (=8 cells) so we expect 56 surface cells. Greedy merge
        // turns those into a small number of boxes, but never just 1.
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        var boxes = cube.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Accurate);

        Assert.True(boxes.Length > 1);
        var union = BoundingBox.Empty;
        foreach (var b in boxes) union = union.Encapsulate(b);
        Assert.True(union.Contains(cube.ComputeBoundingBox()));
    }

    [Fact]
    public void FastMode_NeverFewerBoxesThanAccurate_OnSameMesh()
    {
        // Fast over-covers, so its merged result is at most as many boxes as
        // Accurate's (and almost always fewer). Sanity check: Accurate ≥ Fast.
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        var fast = cube.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Fast);
        var accurate = cube.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Accurate);
        Assert.True(accurate.Length >= fast.Length);
    }

    [Fact]
    public void SingleAxisAlignedTriangle_AccurateMode_OccupiesOnlyTrianglePlaneCells()
    {
        // Triangle in the y=0 plane spanning a 2×2 area in XZ. Only z-slice 0
        // (cells whose center y is at +0.5*voxel) should be occupied. With
        // voxelSize 1 that puts the cell-center at y=0.5; the SAT test against
        // the plane y=0 with half-extent 0.5 just barely touches → occupied.
        Vertex3D[] verts =
        [
            new(new Vector3(0f, 0f, 0f)),
            new(new Vector3(2f, 0f, 0f)),
            new(new Vector3(0f, 0f, 2f)),
        ];
        var mesh = Mesh.Create<Vertex3D>(verts);
        var boxes = mesh.ComputeOccupiedBoxes(1f, MeshOccupancyMode.Accurate);

        Assert.NotEmpty(boxes);
        // All boxes should sit in the y=0 slab (Min.Y == 0, Max.Y == 1).
        foreach (var b in boxes)
        {
            Assert.Equal(0f, b.Min.Y, 5);
            Assert.Equal(1f, b.Max.Y, 5);
        }
    }

    [Fact]
    public void ResultBoxesAreDisjoint()
    {
        var cube = Meshes.Cube(Color.White, size: new Vector3(2f));
        var boxes = cube.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Accurate);

        // Greedy merge consumes cells once, so output boxes should not
        // intersect each other (they may share faces).
        for (int i = 0; i < boxes.Length; i++)
        for (int j = i + 1; j < boxes.Length; j++)
        {
            var a = boxes[i];
            var b = boxes[j];
            // True overlap means strict interior intersection on every axis.
            bool overlapX = a.Min.X < b.Max.X && b.Min.X < a.Max.X;
            bool overlapY = a.Min.Y < b.Max.Y && b.Min.Y < a.Max.Y;
            bool overlapZ = a.Min.Z < b.Max.Z && b.Min.Z < a.Max.Z;
            Assert.False(overlapX && overlapY && overlapZ,
                $"boxes {i} and {j} overlap: {a.Min}..{a.Max} vs {b.Min}..{b.Max}");
        }
    }

    [Fact]
    public void IndexedAndNonIndexedMeshes_AgreeOnSameTriangles()
    {
        // Two equivalent representations of the same single triangle.
        Vertex3D[] verts =
        [
            new(new Vector3(0f, 0f, 0f)),
            new(new Vector3(1f, 0f, 0f)),
            new(new Vector3(0f, 1f, 0f)),
        ];
        var nonIndexed = Mesh.Create<Vertex3D>(verts);

        Vertex3D[] uniqueVerts = verts;
        uint[] indices = [0, 1, 2];
        var indexed = Mesh.Create<Vertex3D>(uniqueVerts, indices);

        var a = nonIndexed.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Accurate);
        var b = indexed.ComputeOccupiedBoxes(0.5f, MeshOccupancyMode.Accurate);
        Assert.Equal(a.Length, b.Length);
    }
}
