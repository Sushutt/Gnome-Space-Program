using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// The sphere has some really dense points near the corners, have a look at https://catlikecoding.com/unity/tutorials/cube-sphere/
// Assuming you can find the time to update terrain, out of all the already broken things in this game.

public partial class TerrainGen : Node3D
{
    [Export] public bool runInSeparateThread = true;

    [Export] public float radius = 600.0f;
    [Export] public int perQuadSubdivison = 8;
    [Export] public int minLevel = 4;
    [Export] public int maxLevel = 12;
    [Export] public int minRenderLevel = 0;
    [Export] public int minColliderLevel = 10;
    [Export] public Node3D player;
    //[Export] public Material material;
    [Export] public Node3D scaledSpace;
    //[Export] public UniverseManager universeManager;

    // extras
    [Export] public Mesh scaledBillboard;

    public CelestialBody cBody;

    private Vector3 planetCenter;

    private Vector3 playerPos;
    private readonly List<Quad> quadList = [];
    private readonly List<QuadDetailLevel> quadDetailLevels = [];

    private readonly List<Quad> quadsQueuedForDeletion = [];

    private Node3D scaledContainer;

    private ScaledObject scaledObject;

    public override void _Ready()
    {
        scaledSpace = (Node3D)GetTree().GetFirstNodeInGroup("ScaledSpace");

        scaledContainer = new();
        scaledSpace.AddChild(scaledContainer);
        if(scaledBillboard != null)
        {
            MeshInstance3D billboard = new(){
                Mesh = scaledBillboard
            };
            scaledContainer.AddChild(billboard);
        }

        scaledObject = new()
        {
            truePosition = GlobalPosition,
            associatedNode = scaledContainer,
            objectRadius = radius
        };

        for (int i = 1; i < maxLevel+1; i++)
        {
            float distToQuad = Mathf.RoundToInt(radius/Mathf.Pow(2,i-minLevel));
            if (i <= minLevel)
            {
                distToQuad = float.PositiveInfinity;
            }
            quadDetailLevels.Add(new QuadDetailLevel(){detailValue = i, distanceToQuad = distToQuad});
        }

        GD.Print("Planet generator starting..");
        
        createCube();

        if(runInSeparateThread)
        {
            Thread quadThread = new Thread(QuadProcess);
            quadThread.Start();
        }else{
            QuadProcess();
        }
    }

    public override void _Process(double delta)
    {
        //player = FlightManager.Instance.currentCraft;
        playerPos = Vector3.Zero;//player.GlobalPosition;
        planetCenter = GlobalPosition;
    }

    // giant function that makes a cube
    private void createCube()
    {
        Quad quadU = new();
		Quad quadD = new();

		Quad quadF = new();
		Quad quadB = new();

		Quad quadL = new();
		Quad quadR = new();

        quadU.position = new Vector3(0, radius, 0);
		quadD.position = new Vector3(0, -radius, 0);

		quadF.position = new Vector3(0, 0, radius);
		quadB.position = new Vector3(0, 0, -radius);

		quadL.position = new Vector3(radius, 0, 0);
		quadR.position = new Vector3(-radius, 0, 0);

        quadU.basis = new Basis(new Vector3(1,0,0),  new Vector3(0,0,-1), new Vector3(0,1,0)); //new Vector3(-90,0,0);
		quadD.basis = new Basis(new Vector3(1,0,0),  new Vector3(0,0,1),  new Vector3(0,-1,0)); //new Vector3(90,0,0);

  		quadF.basis = new Basis(new Vector3(1,0,0),  new Vector3(0,1,0),  new Vector3(0,0,1)); //new Vector3(0,0,0);
  		quadB.basis = new Basis(new Vector3(-1,0,0), new Vector3(0,1,0),  new Vector3(0,0,-1)); //new Vector3(0,180,0);
		
		quadL.basis = new Basis(new Vector3(0,0,-1), new Vector3(0,1,0),  new Vector3(1,0,0)); //new Vector3(0,90,0);
		quadR.basis = new Basis(new Vector3(0,0,1),  new Vector3(0,1,0),  new Vector3(-1,0,0)); //new Vector3(0,-90,0);

        // shut the hell up about simplifying you're making it more bloody complex

        // starting mesh (UP)
        QuadMesh quadUMesh = new();
		quadUMesh.Orientation = PlaneMesh.OrientationEnum.Y;
		quadU.mesh = quadUMesh;

        quadU.colliderRotation = new Vector3(0,0,0);

        // starting mesh (DOWN)
		QuadMesh quadDMesh = new();
		quadDMesh.Orientation = PlaneMesh.OrientationEnum.Y;
		quadDMesh.FlipFaces = true;
		quadD.mesh = quadDMesh;

        quadD.colliderRotation = new Vector3(180,0,0);

        // starting mesh (FRONT)
		QuadMesh quadFMesh = new();
		quadFMesh.Orientation = PlaneMesh.OrientationEnum.Z;
		quadF.mesh = quadFMesh;

        quadF.colliderRotation = new Vector3(90,0,0);

        // starting mesh (BACK)
		QuadMesh quadBMesh = new();
		quadBMesh.Orientation = PlaneMesh.OrientationEnum.Z;
		quadBMesh.FlipFaces = true;
		quadB.mesh = quadBMesh;

        quadB.colliderRotation = new Vector3(-90,0,0);

        // starting mesh (LEFT)
		QuadMesh quadLMesh = new();
		quadLMesh.Orientation = PlaneMesh.OrientationEnum.X;
		quadL.mesh = quadLMesh;

        quadL.colliderRotation = new Vector3(0,0,-90);

        // starting mesh (RIGHT)
		QuadMesh quadRMesh = new();
		quadRMesh.Orientation = PlaneMesh.OrientationEnum.X;
		quadRMesh.FlipFaces = true;
		quadR.mesh = quadRMesh;

        quadR.colliderRotation = new Vector3(0,0,90);

        List<Quad> initialQuads = [quadU,quadD,quadL,quadR,quadF,quadB];

        foreach (Quad quad in initialQuads)
        {
            // "Large mesh" is a slightly larger version of this dumbass mesh thing
            // It is designed to have be a 1-face-long brim around the quad to allow for the normals to process correctly
            QuadMesh quadMesh = (QuadMesh)quad.mesh;
            QuadMesh largeMesh = (QuadMesh)quadMesh.Duplicate();
            quad.largeMesh = largeMesh;

            quadMesh.SubdivideDepth = perQuadSubdivison;
            quadMesh.SubdivideWidth = perQuadSubdivison;

            largeMesh.SubdivideDepth = perQuadSubdivison + 4;
            largeMesh.SubdivideWidth = perQuadSubdivison + 4;

            quad.scale = new Vector3(radius*2,radius*2,radius*2);
            quad.detailLevel = 1;
            quadList.Add(quad);

            // manage vertex data
            (Godot.Collections.Array quadMeshData,
              List<Vector3> globalQuadVertices) = ProcessQuadMesh(quad, quadMesh);

            (Godot.Collections.Array largeMeshData,
              _) = ProcessQuadMesh(quad, largeMesh);

            quad.meshData = quadMeshData;

            // same thing but this variable NEVER changes
            quad.originalMeshData = quadMeshData;
            quad.largeMeshData = largeMeshData;

            quad.centerPosition = GetCenterOfMesh(globalQuadVertices)+GlobalPosition;

            // colliders
            HeightMapShape3D collisionShape = new(){
                MapDepth = perQuadSubdivison+2,
                MapWidth = perQuadSubdivison+2
            };
            quad.originalCollider = collisionShape;
            quad.collider = collisionShape;
        }
    }

    private async void QuadProcess()
    {
        while (true)
        {
            // make it run at 60 fps or sum sh
            await Task.Delay(1);
            for (int i = 0; i < quadList.Count; i++)
            {
                Quad planetQuad = quadList[i];
                float distanceFromPlr = (planetQuad.centerPosition - (playerPos-planetCenter)).Length();
                int quadDetail = planetQuad.detailLevel;

                planetQuad.readyToSubdivide = false;
                foreach (QuadDetailLevel dt in quadDetailLevels)
                {
                    if (distanceFromPlr < dt.distanceToQuad)
                    {
                        if (quadDetail < dt.detailValue)
                        {
                            planetQuad.readyToSubdivide = true;
                        }
                    }
                }

                // remder!
                if (!planetQuad.rendered && planetQuad.children == null)
                {
                    planetQuad.rendered = true;
                    CallDeferred(nameof(RenderQuad), planetQuad, planetQuad.mesh);
                }
            }

            List<Quad> newQuadList = [.. quadList];
            foreach (Quad quad in newQuadList)
            {
                PerformSubdivisonOrUnSubdivision(quad); // optimize this
            }
            newQuadList = null;

            // delete all unused quad objects after everything is done here
            for (int i = 0; i < quadsQueuedForDeletion.Count; i++)
            {
                Quad currentQuad = quadsQueuedForDeletion[i];
                quadsQueuedForDeletion.Remove(currentQuad);
                currentQuad.QueueFree();
            }
        }
    }

    private void PerformSubdivisonOrUnSubdivision(Quad quad)
    {
        if (quad.readyToSubdivide)
        {
            if (quad.children == null)
            {
                SubdivideQuad(quad);
                CallDeferred(nameof(UnRenderQuad), quad);
            }
        }else
        {
            if (quad.children != null)
            {
                UnSubdivideQuad(quad);
            }
        }
    }

    private void SubdivideQuad(Quad quad)
    {
        float quadRadius = quad.scale.X/4;

		Quad quad1 = new();
		Quad quad2 = new();
		Quad quad3 = new();
		Quad quad4 = new();

		// Turned on some neurons to think of this one
		Vector3 globalFacingX = quad.basis.X;
		Vector3 globalFacingY = quad.basis.Y;

		quad1.position = (-globalFacingX + globalFacingY)*quadRadius + quad.position;
		quad2.position = (globalFacingX + globalFacingY)*quadRadius + quad.position;
		quad3.position = (globalFacingX + -globalFacingY)*quadRadius + quad.position;
		quad4.position = (-globalFacingX + -globalFacingY)*quadRadius + quad.position;

		List<Quad> quadArray = [quad1, quad2, quad3, quad4];

		quad.children = quadArray;

        foreach (Quad newQuad in quadArray)
        {
            newQuad.detailLevel = quad.detailLevel + 1;
            newQuad.scale = quad.scale/2;
            newQuad.basis = quad.basis;
            quadList.Add(newQuad);

            newQuad.originalMeshData = quad.originalMeshData;
            newQuad.largeMeshData = quad.largeMeshData;
            newQuad.largeMesh = quad.largeMesh;

            // colliders
            HeightMapShape3D collisionShapeOG = quad.originalCollider;
            newQuad.originalCollider = collisionShapeOG;
            newQuad.collider = quad.originalCollider;
            newQuad.colliderRotation = quad.colliderRotation;

            (ArrayMesh newMesh,
            Godot.Collections.Array quadMeshData,
            List<Vector3> globalQuadVertices) = InheritQuadMesh(newQuad);
            
            newQuad.mesh = newMesh;
            newQuad.meshData = quadMeshData;
            newQuad.centerPosition = GetCenterOfMesh(globalQuadVertices);
        }
    }

    private void UnSubdivideQuad(Quad quad)
    {
        int quadChildCount = quad.children.Count;
        for (int i = 0; i < quadChildCount; i++)
        {
            Quad childQuad = quad.children[i];
            quadList.Remove(childQuad);
            CallDeferred(nameof(UnRenderQuad), childQuad);

            childQuad.meshData = null;
            childQuad.originalMeshData = null;
            if(childQuad.mesh is ArrayMesh arrayMesh)arrayMesh.Dispose();

            // game crashed due to a potential race condition so im moving this all into my own "queue" system
            quadsQueuedForDeletion.Add(childQuad);
        }
        quad.children = null;
    }

    private void RenderQuad(Quad quad, Mesh mesh)
    {
        StaticBody3D meshBody = new();
        MeshInstance3D meshObject = new();
        if (IsInstanceValid(mesh) && IsInstanceValid(meshObject))
        {
            meshObject.Mesh = mesh;
            meshObject.Position = quad.position;
            meshObject.Scale = quad.scale;

            quad.renderedMesh = meshBody;

            meshBody.AddChild(meshObject); 
            if (quad.detailLevel >= minRenderLevel)
            {
                AddChild(meshBody);
            }else{
                scaledContainer.AddChild(meshBody); // set to scaled space if its below a certain detail level
                meshObject.SetLayerMaskValue(1,false);
                meshObject.SetLayerMaskValue(2,true);
            }

            // add colliders if such act is permitted
            if (quad.detailLevel >= minColliderLevel)
            {
                meshObject.CreateTrimeshCollision();
            }
        }
    }

    // it hides quads a little bit after so that no flickering occurs
    private async static void UnRenderQuad(Quad quad)
    {
        await Task.Delay(100);
        if (quad.renderedMesh!=null && IsInstanceValid(quad.renderedMesh))
        {
            quad.renderedMesh.QueueFree(); 
            quad.renderedMesh = null;
            quad.rendered = false;
        }
    }
    
    // get center mmm yummy and important
	private static Vector3 GetCenterOfMesh(List<Vector3> vertices)
	{
		Vector3 totalVertexThing = Vector3.Zero;

		foreach (Vector3 vertex in vertices)
		{
			totalVertexThing += vertex;
		}

		return totalVertexThing/vertices.Count;
	}

    private static (Godot.Collections.Array, List<Vector3>) ProcessQuadMesh(Quad quad, QuadMesh quadMesh)
    {
        Godot.Collections.Array meshData = quadMesh.SurfaceGetArrays(0);

		Godot.Collections.Array verticesUnfiltered = (Godot.Collections.Array) meshData[0];

		//List<Vector3> vertices = [];
		List<Vector3> globalVertices = [];
		
		for (int v = 0; v < verticesUnfiltered.Count; v++)
		{
			Vector3 vertex = (Vector3) verticesUnfiltered[v];
			//vertices.Add(vertex);
			globalVertices.Add(vertex + quad.position);
		}

		return (meshData, globalVertices);
    }

    private (ArrayMesh, Godot.Collections.Array, List<Vector3>) InheritQuadMesh(Quad quad)
    {
        // original data
        Vector3 quadPosition = quad.position;
        Vector3 quadScale = quad.scale;

        Vector3[] originalVertices = (Vector3[])quad.originalMeshData[(int)Mesh.ArrayType.Vertex];

        Vector3[] largeVertices = (Vector3[])quad.largeMeshData[(int)Mesh.ArrayType.Vertex];
        int[] largeIndices = (int[])quad.largeMeshData[(int)Mesh.ArrayType.Index];

        Vector3 quadNodeGlobalPos = quadPosition / quadScale.X;

        // data used in the creation of the new mesh
        Godot.Collections.Array newMeshData = quad.originalMeshData.Duplicate();

        // processing of the mesh data
        (Vector3[] newVertices, List<Vector3> newGlobalVertices) = ProcessVertices(quadNodeGlobalPos, quadScale.X, originalVertices, 1f);
        (Vector3[] newLargeVertices, _) = ProcessVertices(quadNodeGlobalPos, quadScale.X, largeVertices, 1.445f);

        Vector3[] newTemporaryNormals = MeshManipulation.calculateSmoothNormals(newLargeVertices, largeIndices);
        Vector3[] newNormals = FilterCenterVector3s(newTemporaryNormals, 2, perQuadSubdivison + 4);

        // Creation of the new mesh
        newMeshData[(int)Mesh.ArrayType.Vertex] = newVertices;
        newMeshData[(int)Mesh.ArrayType.Normal] = newNormals;

        ArrayMesh newMesh = new();
        newMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, newMeshData);
        //throw new Exception();
        return (newMesh, newMeshData, newGlobalVertices);
    }

    private (Vector3[], List<Vector3>) ProcessVertices(Vector3 quadNodeGlobalPos, float quadScale, Vector3[] originalVertices, float offsetSize)
    {
        // THis has to be a var for some dumb fucking reason;
        var newVertices = new Vector3[originalVertices.Length];
        List<Vector3> newGlobalVertices = [];
        for (int v = 0; v < originalVertices.Length; v++)
        {
            // Vertex modification
            Vector3 vertex = (Vector3)originalVertices[v]*offsetSize;

            Vector3 globalVertex = vertex + quadNodeGlobalPos;

            float quadNodeSizeToRadius = radius / quadScale;
            Vector3 noiseSamplePoint = globalVertex / quadNodeSizeToRadius;
            float noiseOffset = 0;
            if (cBody.pqsMods != null)
            {
                foreach (Node mod in cBody.pqsMods)
                {
                    if (mod is FastNoise3D fastNoise3D)
                    {
                        noiseOffset += fastNoise3D.SamplePoint(noiseSamplePoint);
                    }
                }
            }
            float quadNodeSizeToRadius2 = (radius + noiseOffset) / quadScale;

            Vector3 normalizedVertPos = (globalVertex - Vector3.Zero).Normalized() * quadNodeSizeToRadius2;

            Vector3 newVertex = normalizedVertPos - quadNodeGlobalPos;
            Vector3 newGlobalVertex = normalizedVertPos / quadNodeSizeToRadius2 * radius;

            newVertices[v]=newVertex;
            newGlobalVertices.Add(newGlobalVertex);
        }
        return (newVertices, newGlobalVertices);
    }

    private static float SampleNoise(FastNoiseLite noise, Vector3 position, float amplitude)
    {
        float val = noise.GetNoise3D(position.X*10,position.Y*10,position.Z*10);
        return val*amplitude;
    }

    private static Vector3[] FilterCenterVector3s(Vector3[] original, int distFromEdge, int meshSubdivision)
	{
		List<Vector3> newList = [];
		int row = 0;
		int column = 0;
		for (int i = 0; i < original.Length; i++)
		{
			if (column > distFromEdge-1 && column < meshSubdivision+2-distFromEdge && row > distFromEdge-1 && row < meshSubdivision+2-distFromEdge)
			{
				newList.Add(original[i]);
			}
			row++;
			if (row>=meshSubdivision+2)
			{
				row = 0;
				column++;
			}
		}
		// transfer
		Vector3[] mewArray = new Vector3[newList.Count];
		for (int i = 0; i < newList.Count; i++)
		{
			mewArray[i] = newList[i];
		}
		newList.Clear();
		return mewArray;
	}
}
