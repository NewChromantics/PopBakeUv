using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;


[RequireComponent(typeof(MeshFilter))]
public class GenerateUv : MonoBehaviour {

	public Shader			DrawTriangleShader;
	public RenderTexture	BakedTextureMap;
	public Mesh				ExplodedMesh;

	void SetTriangle(Material DrawTriangleMat,Mesh mesh,Vector2[] uvs,int MeshTriangleIndex,int ShaderTriangleIndex)
	{
		for ( int v=0;	v<3;	v++ )
		{
			var uv = uvs[(MeshTriangleIndex*3) + v];
		
			string Uniform = "Triangle_Uv_" + ShaderTriangleIndex + "_" + v;
			DrawTriangleMat.SetVector(Uniform, new Vector4( uv.x, uv.y, v, 0) );
			Debug.Log ("SetTriangle( " + Uniform + " ) = (" + uv.x + " " + uv.y + " )");
		}

		DrawTriangleMat.SetInt ("TriangleCount", ShaderTriangleIndex + 1); 
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

	public void GenerateUvs() 
	{
		ExplodeMesh ();

		var Mesh = ExplodedMesh;
		

		var uvs = UnityEditor.Unwrapping.GeneratePerTriangleUV (Mesh);
		int TriangleCount = Mesh.GetTriangles (0).Length;
		Debug.Log ("Generated " + uvs.Length + " uvs for " + Mesh.vertexCount + " vertexes; mesh uv count: " + Mesh.uv.Length + " trianglecount: " + TriangleCount);


		//	bake new uv's
		var Indexes = Mesh.GetIndices(0);
		List<Vector2> SharedUvs = new List<Vector2> ();
		Mesh.GetUVs (0, SharedUvs);

		if (Indexes.Length != uvs.Length)
			throw new System.Exception ("Index/uv size mismatch");
		
		for (int i = 0;	i < Indexes.Length;	i++)
		{
			var VertexIndex = Indexes [i];
			SharedUvs [VertexIndex] = uvs [i];
		}
		Mesh.SetUVs (0, SharedUvs);




		//	bake shader
		if (DrawTriangleShader) 
		{
			var DrawTriangleMat = new Material (DrawTriangleShader);

			var LastTexture = RenderTexture.GetTemporary (BakedTextureMap.width, BakedTextureMap.height, 0, BakedTextureMap.format);
			var TempTexture = RenderTexture.GetTemporary (BakedTextureMap.width, BakedTextureMap.height, 0, BakedTextureMap.format);

			//	clear 
			Graphics.Blit( Texture2D.blackTexture, LastTexture );

			int TriCount = Indexes.Length / 3;

			for (int t = 0;	t < TriCount;	t++) {
				SetTriangle (DrawTriangleMat, Mesh, uvs, t, 0);
				Graphics.Blit (LastTexture, TempTexture, DrawTriangleMat);
				Graphics.Blit (TempTexture, LastTexture);
			}
			Graphics.Blit (LastTexture, BakedTextureMap);
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
