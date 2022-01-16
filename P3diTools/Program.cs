using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;

namespace P3diTools
{
	class Program
	{
		private static readonly int SnapResolution = 128;
		private static readonly float SnapEpsilon = 0.1f;

		static void Main(string[] args)
		{
			var path = @"E:\Forge\Mods\PSWG\PSWG15\resources\models\blasters\dlt19";

			var input = Path.Combine(path, "dlt19.p3di");
			var outModel = Path.Combine(path, "dlt19.p3d");
			var outRig = Path.Combine(path, "dlt19.p3dr");

			var model = JsonConvert.DeserializeObject<P3di>(File.ReadAllText(input));

			using var bw = new BinaryWriter(File.OpenWrite(outModel));
			WriteModel(bw, model, false);

			using var bw2 = new BinaryWriter(File.OpenWrite(outRig));
			WriteModel(bw2, model, true);

			Console.WriteLine("Done.");
		}

		private static void WriteModel(BinaryWriter bw, P3di model, bool rigOnly)
		{
			if (rigOnly)
				bw.Write(Encoding.ASCII.GetBytes("P3DR"));
			else
				bw.Write(Encoding.ASCII.GetBytes("P3D"));

			bw.Write(model.Version);

			bw.Write(model.Sockets.Length);
			foreach (var socket in model.Sockets)
			{
				bw.Write(Encoding.ASCII.GetBytes(socket.Name));
				bw.Write((byte)0);

				bw.Write(socket.Parent != null);

				if (socket.Parent != null)
				{
					bw.Write(Encoding.ASCII.GetBytes(socket.Parent));
					bw.Write((byte)0);
				}

				WriteSocketTransform(bw, socket.Transform);
			}

			bw.Write(model.Meshes.Length);
			foreach (var mesh in model.Meshes)
				WriteMesh(bw, mesh, !rigOnly);
		}

		private static void WriteMesh(BinaryWriter bw, Mesh mesh, bool writeVertexData)
		{
			bw.Write(Encoding.ASCII.GetBytes(mesh.Name));
			bw.Write((byte)0);

			WriteMeshTransform(bw, mesh.Transform);

			if (writeVertexData)
			{
				bw.Write(GetMaterial(mesh.Material));

				bw.Write(mesh.Faces.Length);
				foreach (var face in mesh.Faces)
				{
					WriteVec3(bw, face.Normal);

					var vertices = face.Vertices;
					if (vertices.Length == 3)
					{
						for (var vertIdx = 0; vertIdx < 3; vertIdx++)
						{
							WriteVec3(bw, vertices[vertIdx].Position);

							for (var i = 0; i < 2; i++)
								bw.Write(SnapTexCoord(vertices[vertIdx].Texture[i]));
						}

						// Repeat the last triangle vertex to make a quad
						WriteVec3(bw, vertices[2].Position);

						for (var i = 0; i < 2; i++)
							bw.Write(SnapTexCoord(vertices[2].Texture[i]));
					}
					else if (vertices.Length == 4)
					{
						for (var vertIdx = 0; vertIdx < 4; vertIdx++)
						{
							WriteVec3(bw, vertices[vertIdx].Position);

							for (var i = 0; i < 2; i++)
								bw.Write(SnapTexCoord(vertices[vertIdx].Texture[i]));
						}
					}
					else
						throw new NotSupportedException($"Only triangles and quads supported, found {vertices.Length}-gon");
				}
			}

			bw.Write(mesh.Children.Length);

			foreach (var child in mesh.Children)
				WriteMesh(bw, child, writeVertexData);
		}

		private static void WriteSocketTransform(BinaryWriter bw, float[][] t)
		{
			Console.WriteLine(string.Join(",\n", t.Select(floats => string.Join(", ", floats))));

			var mat = new Matrix4x4(
				t[0][0], t[0][1], t[0][2], t[0][3],
				t[1][0], t[1][1], t[1][2], t[1][3],
				t[2][0], t[2][1], t[2][2], t[2][3],
				t[3][0], t[3][1], t[3][2], t[3][3]
			);

			var rot = new Matrix4x4(
				1, 0, 0, 0,
				0, 0, 1, 0,
				0, -1, 0, 0,
				0, 0, 0, 1
			);
			mat = rot * mat;

			mat *= Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(-Math.PI / 2)));
			mat *= Matrix4x4.CreateScale(1, 1, -1);

			bw.Write(mat.M11);
			bw.Write(mat.M12);
			bw.Write(mat.M13);
			bw.Write(mat.M14);
			bw.Write(mat.M21);
			bw.Write(mat.M22);
			bw.Write(mat.M23);
			bw.Write(mat.M24);
			bw.Write(mat.M31);
			bw.Write(mat.M32);
			bw.Write(mat.M33);
			bw.Write(mat.M34);
			bw.Write(mat.M41);
			bw.Write(mat.M42);
			bw.Write(mat.M43);
			bw.Write(mat.M44);
		}

		private static void WriteMeshTransform(BinaryWriter bw, float[][] t)
		{
			var mat = new Matrix4x4(
				t[0][0], t[0][1], t[0][2], t[0][3],
				t[1][0], t[1][1], t[1][2], t[1][3],
				t[2][0], t[2][1], t[2][2], t[2][3],
				t[3][0], t[3][1], t[3][2], t[3][3]
			);

			// Convert from Z-up to Y-up
			var tX = mat.M14;
			var tY = mat.M24;
			var tZ = mat.M34;

			bw.Write(mat.M11);
			bw.Write(mat.M12);
			bw.Write(mat.M13);
			bw.Write(tX);
			bw.Write(mat.M21);
			bw.Write(mat.M22);
			bw.Write(mat.M23);
			bw.Write(tZ);
			bw.Write(mat.M31);
			bw.Write(mat.M32);
			bw.Write(mat.M33);
			bw.Write(-tY);
			bw.Write(mat.M41);
			bw.Write(mat.M42);
			bw.Write(mat.M43);
			bw.Write(mat.M44);
		}

		private static void WriteVec3(BinaryWriter bw, float[] v)
		{
			bw.Write(v[0]); // X

			// Convert from Z-up to Y-up
			bw.Write(v[2]); // Z
			bw.Write(-v[1]); // Y
		}

		private static float SnapTexCoord(float f)
		{
			var rounded = (float)Math.Round(f * SnapResolution);
			return (Math.Abs(rounded - f * SnapResolution) < SnapEpsilon ? rounded / SnapResolution : f);
		}

		private static byte GetMaterial(string materialName)
		{
			return materialName switch
			{
				"MAT_DIFFUSE_OPAQUE" => (byte)FaceMaterial.DiffuseOpaque,
				"MAT_DIFFUSE_CUTOUT" => (byte)FaceMaterial.DiffuseCutout,
				"MAT_DIFFUSE_TRANSLUCENT" => (byte)FaceMaterial.DiffuseTranslucent,
				"MAT_EMISSIVE" => (byte)FaceMaterial.Emissive,
				_ => (byte)FaceMaterial
					.DiffuseOpaque //throw new InvalidDataException("Expected material name to be one of: MAT_DIFFUSE_OPAQUE, MAT_DIFFUSE_CUTOUT, MAT_DIFFUSE_TRANSLUCENT, MAT_EMISSIVE")
			};
		}
	}

	internal enum FaceMaterial : byte
	{
		DiffuseOpaque = 0,
		DiffuseCutout = 1,
		DiffuseTranslucent = 2,
		Emissive = 3
	}

	public class Socket
	{
		[JsonProperty("name")] public string Name { get; set; }

		[JsonProperty("parent")] public string Parent { get; set; }

		[JsonProperty("transform")] public float[][] Transform { get; set; }
	}

	public class Vertex
	{
		[JsonProperty("v")] public float[] Position { get; set; }

		[JsonProperty("t")] public float[] Texture { get; set; }
	}

	public class Face
	{
		[JsonProperty("normal")] public float[] Normal { get; set; }

		[JsonProperty("vertices")] public Vertex[] Vertices { get; set; }
	}

	public class Mesh
	{
		[JsonProperty("name")] public string Name { get; set; }

		[JsonProperty("transform")] public float[][] Transform { get; set; }

		[JsonProperty("material")] public string Material { get; set; }

		[JsonProperty("faces")] public Face[] Faces { get; set; }

		[JsonProperty("children")] public Mesh[] Children { get; set; }
	}

	public class P3di
	{
		[JsonProperty("version")] public int Version { get; set; }

		[JsonProperty("sockets")] public Socket[] Sockets { get; set; }

		[JsonProperty("meshes")] public Mesh[] Meshes { get; set; }
	}
}