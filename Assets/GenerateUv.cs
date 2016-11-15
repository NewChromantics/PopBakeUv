using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;


[RequireComponent(typeof(MeshFilter))]
public class GenerateUv : MonoBehaviour {

	public Shader			DrawTriangleShader;
	public RenderTexture	BakedTextureMap;
	public bool				GenerateNewUvs = true;
	public Mesh				ExplodedMesh;
	[Header("0 = all")]
	[Range(0,200)]
	public int 				MaxTriangleBakes = 0;

	float GetTrianlgeArea(Vector2 t0,Vector2 t1,Vector2 t2)
	{
		var a = Vector2.Distance(t0,t1);
		var b = Vector2.Distance(t1,t2);
		var c = Vector2.Distance(t2,t0);
		var s = (a + b + c) / 2;
		return Mathf.Sqrt(s * (s-a) * (s-b) * (s-c));
		/*
		var v0 = b - a;
		var v1 = c - a;
		float d00 = Vector2.Dot(v0, v0);
		float d01 = Vector2.Dot(v0, v1);
		float d11 = Vector2.Dot(v1, v1);
		float denom = d00 * d11 - d01 * d01;
		return denom /2.0f;
		*/
	}

	bool SetTriangle(Material DrawTriangleMat,Mesh mesh,Vector2[] uvs,int MeshTriangleIndex,int ShaderTriangleIndex)
	{
		bool IndexedUvs = uvs.Length == mesh.vertices.Length;
		Vector2[] TriangleUvs;

		if ( IndexedUvs )
		{
			var Indexes = mesh.GetIndices (0);
			TriangleUvs = new Vector2[] {
				uvs [Indexes[(MeshTriangleIndex * 3) + 0]],
				uvs [Indexes[(MeshTriangleIndex * 3) + 1]],
				uvs [Indexes[(MeshTriangleIndex * 3) + 2]]
			};
		}
		else
		{
			TriangleUvs = new Vector2[] {
				uvs [(MeshTriangleIndex * 3) + 0],
				uvs [(MeshTriangleIndex * 3) + 1],
				uvs [(MeshTriangleIndex * 3) + 2]
			};
		}

		//	evaluate size of triangle
		var TriangleArea = GetTrianlgeArea( TriangleUvs[0], TriangleUvs[1], TriangleUvs[2] );
		TriangleArea *= BakedTextureMap.width * BakedTextureMap.height;
	
		if (TriangleArea < 1) {
			Debug.Log ("Triangle skipped; area in pixels: " + TriangleArea);
			return false;
		}
	

		for ( int v=0;	v<3;	v++ )
		{
			var uv = TriangleUvs[v];
		
			string Uniform = "Triangle_Uv_" + ShaderTriangleIndex + "_" + v;
			DrawTriangleMat.SetVector(Uniform, new Vector4( uv.x, uv.y, v, 0) );
			//Debug.Log ("SetTriangle( " + Uniform + " ) = (" + uv.x + " " + uv.y + " )");
		}

		DrawTriangleMat.SetInt ("TriangleCount", ShaderTriangleIndex + 1); 

		return true;
	}

	public void ExplodeMesh()
	{
		if ( ExplodedMesh != null )
			return;
		/*
		 * var mf = GetComponent<MeshFilter> ();
		var Mesh = mf ? mf.sharedMesh : null;
		if (!Mesh)
			return;
			*/
		var mf = GetComponent<MeshFilter> ();
		ExplodedMesh = mf.mesh;
	}

	void SetTriangleMeta(Material DrawTriangleMat,Mesh mesh,int MeshTriangleIndex,int ShaderTriangleIndex)
	{
		var Verts = mesh.vertices;
		var Indexes = mesh.GetIndices (0);
		for ( int v=0;	v<3;	v++ )
		{
			//	get world pos
			var LocalPos = Verts[Indexes[(MeshTriangleIndex*3) + v]];
			var WorldPos = this.transform.TransformPoint (LocalPos);

			string Uniform = "Triangle_Pos_" + ShaderTriangleIndex + "_" + v;
			DrawTriangleMat.SetVector(Uniform, new Vector4( WorldPos.x, WorldPos.y, WorldPos.z, 0) );
		}

	}


	public void GenerateUvs() 
	{
		ExplodeMesh ();

		var Mesh = ExplodedMesh;
		
		var ProgressTitle = "Blitting Triangles";

		if (GenerateNewUvs) {
			EditorUtility.DisplayCancelableProgressBar (ProgressTitle, "Generating UV's...", 0);

			var uvs = UnityEditor.Unwrapping.GeneratePerTriangleUV (Mesh);
			int TriangleCount = Mesh.GetTriangles (0).Length;
			Debug.Log ("Generated " + uvs.Length + " uvs for " + Mesh.vertexCount + " vertexes; mesh uv count: " + Mesh.uv.Length + " trianglecount: " + TriangleCount);

			//	bake new uv's
			List<Vector2> SharedUvs = new List<Vector2> ();
			Mesh.GetUVs (0, SharedUvs);

			var Indexes = Mesh.GetIndices(0);
			if (Indexes.Length != uvs.Length)
				throw new System.Exception ("Index/uv size mismatch");
			
			for (int i = 0;	i < Indexes.Length;	i++)
			{
				var VertexIndex = Indexes [i];
				SharedUvs [VertexIndex] = uvs [i];
			}
			Mesh.SetUVs (0, SharedUvs);
		}



		//	bake shader
		if (DrawTriangleShader) 
		{
			var DrawTriangleMat = new Material (DrawTriangleShader);

			var LastTexture = RenderTexture.GetTemporary (BakedTextureMap.width, BakedTextureMap.height, 0, BakedTextureMap.format);
			var TempTexture = RenderTexture.GetTemporary (BakedTextureMap.width, BakedTextureMap.height, 0, BakedTextureMap.format);

			//	clear 
			Graphics.Blit( Texture2D.blackTexture, LastTexture );

			var Indexes = Mesh.GetIndices(0);
			int TriCount = Indexes.Length / 3;
			int BlitTriCount = (MaxTriangleBakes > 0) ? Mathf.Min (TriCount, MaxTriangleBakes) : TriCount;

			Debug.Log ("Blitting " + BlitTriCount + "/" + TriCount + " triangles"); 

			List<Vector2> uvs_List = new List<Vector2> ();
			Mesh.GetUVs(0,uvs_List);
			var uvs = uvs_List.ToArray ();

			for (int t = 0;	t <BlitTriCount;	t++) 
			{
				if (EditorUtility.DisplayCancelableProgressBar (ProgressTitle, "Blitting " + t + " of " + BlitTriCount + "/" + TriCount + " triangles", t / (float)BlitTriCount))
					break;

				try
				{
					if (!SetTriangle (DrawTriangleMat, Mesh, uvs, t, 0))
						continue;
					SetTriangleMeta (DrawTriangleMat, Mesh, t, 0);
					Graphics.Blit (LastTexture, TempTexture, DrawTriangleMat);
					Graphics.Blit (TempTexture, LastTexture);
				}
				catch {
					EditorUtility.ClearProgressBar ();
					Graphics.Blit (LastTexture, BakedTextureMap);
					throw;
				}
			}
			Graphics.Blit (LastTexture, BakedTextureMap);
			EditorUtility.ClearProgressBar ();
		}
	}
}



#if UNITY_EDITOR
[CustomEditor(typeof(GenerateUv))]
public class GenerateUvInspector : Editor
{
	

	public override void OnInspectorGUI()
	{
		var Object = target as GenerateUv;
		//Editor ThisEditor = this;

		if (GUILayout.Button ("Bake uv map")) 
		{
			try
			{
				Object.GenerateUvs();
			}
			catch(System.Exception e) 
			{
				Debug.LogException (e);
			}
		}

		DrawDefaultInspector();
	}
}
#endif
