using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using CommandLine.Text;

namespace P3diTools
{
	[Verb("compile", HelpText = "Compile a p3di intermediary into a p3d model and/or p3dr rig.")]
	public class CompileOptions
	{
		[Option('p', "snap-resolution", Required = false, HelpText = "UV vertex snapping resolution, in pixels. Requires -s/--snap.", Default = 128)]
		public int SnapResolution { get; set; }

		[Option('e', "snap-epsilon", Required = false, HelpText = "Maximum distance, in pixels, that UV vertices may be moved to snap to a pixel edge or corner. Requires -s/--snap.", Default = 0.1f)]
		public float SnapEpsilon { get; set; }

		[Option('s', "snap", Required = false, HelpText = "Enable UV vertex snapping epsilon.", Default = false)]
		public bool Snap { get; set; }

		[Option('m', "model", Required = false, HelpText = "Generate a model p3d file.", Default = false)]
		public bool GenerateModel { get; set; }

		[Option('r', "rig", Required = false, HelpText = "Generate a rig p3dr file.", Default = false)]
		public bool GenerateRig { get; set; }

		[Option("model-dest", Required = false, HelpText = "(Default: Working directory) Destination directory for the p3d model.", Default = null)]
		public string ModelDest { get; set; }

		[Option("rig-dest", Required = false, HelpText = "(Default: Working directory) Destination directory for the p3dr rig.", Default = null)]
		public string RigDest { get; set; }

		[Value(0, MetaName = "input", HelpText = "Input p3di file.")]
		public string Input { get; set; }
	}

	[Verb("inspect", HelpText = "Display information about a p3di file.")]
	public class InspectOptions
	{
		[Value(0, MetaName = "input", HelpText = "Input p3di file")]
		public string Input { get; set; }
	}

	class Program
	{
		private const int ErrorCodeParse = -1;
		private const int ErrorCodeInputFileMissing = -2;
		private const int ErrorCodeP3diJsonParseFailed = -3;
		private const int ErrorCodeCompileDestModelDirMissing = -4;
		private const int ErrorCodeCompileDestRigDirMissing = -5;
		private const int ErrorCodeCompileSkippedBoth = -6;
		private const int ErrorCodeCompileFailed = -7;
		private const int ErrorCodeInspectFailed = -8;

		private static int Main(string[] args)
		{
			var parser = new Parser(with => with.HelpWriter = null);
			var parseResult = parser.ParseArguments<CompileOptions, InspectOptions>(args);
			return parseResult.MapResult((CompileOptions o) => CompileMain(o), (InspectOptions o) => InspectMain(o), errors => MentionError(parseResult));
		}

		private static int MentionError<T>(ParserResult<T> result)
		{
			HelpText helpText;
			if (result.Errors.IsVersion())
				helpText = HelpText.AutoBuild(result);
			else
				helpText = HelpText.AutoBuild(result, h =>
				{
					h.Copyright = "MIT - https://github.com/Parzivail-Modding-Team/P3diTools";
					return h;
				}, e => e);

			Console.Error.WriteLine(helpText);

			return ErrorCodeParse;
		}

		private static int InspectMain(InspectOptions options)
		{
			if (!File.Exists(options.Input))
			{
				LogError("Could not locate input file");
				return ErrorCodeInputFileMissing;
			}

			try
			{
				var model = JsonSerializer.Deserialize<P3di>(File.ReadAllText(options.Input));

				Console.WriteLine($"Version: {model.Version}");
				Console.WriteLine($"Root meshes: {model.Meshes.Length}");

				PrintMeshes(model.Meshes);

				Console.WriteLine($"Sockets: {model.Sockets.Length}");

				foreach (var socket in model.Sockets)
					Console.WriteLine($"\t{socket.Name} {(socket.Parent != null ? $"(child of {socket.Parent})" : "")}");
			}
			catch (JsonException e)
			{
				LogError(e.Message);
				return ErrorCodeP3diJsonParseFailed;
			}
			catch (Exception e)
			{
				LogError(e.Message);
				return ErrorCodeInspectFailed;
			}

			return 0;
		}

		private static void PrintMeshes(Mesh[] modelMeshes, int tabLevel = 1)
		{
			var tab = new string('\t', tabLevel);

			foreach (var mesh in modelMeshes)
			{
				Console.WriteLine($"{tab}{mesh.Name} ({mesh.Material})");
				PrintMeshes(mesh.Children, tabLevel + 1);
			}
		}

		private static int CompileMain(CompileOptions options)
		{
			var outModel = Path.Combine(options.ModelDest ?? "", Path.GetFileNameWithoutExtension(options.Input) + ".p3d");
			var outRig = Path.Combine(options.RigDest ?? "", Path.GetFileNameWithoutExtension(options.Input) + ".p3dr");

			var optionVerifyResult = VerifyCompileOptions(options);
			if (optionVerifyResult != null)
				return optionVerifyResult.Value;

			try
			{
				CompileModel(options.Input, outModel, outRig, options);
			}
			catch (JsonException e)
			{
				LogError(e.Message);
				return ErrorCodeP3diJsonParseFailed;
			}
			catch (Exception e)
			{
				LogError(e.Message);
				return ErrorCodeCompileFailed;
			}

			return 0;
		}

		private static void LogError(object message)
		{
			Console.Error.WriteLine($"ERROR: {message}");
		}

		private static void LogWarn(object message)
		{
			Console.Error.WriteLine($"WARN: {message}");
		}

		private static int? VerifyCompileOptions(CompileOptions options)
		{
			if (!File.Exists(options.Input))
			{
				LogError("Could not locate input file");
				return ErrorCodeInputFileMissing;
			}

			if (options.ModelDest != null && !Directory.Exists(options.ModelDest))
			{
				LogError("Could not locate model output directory");
				return ErrorCodeCompileDestModelDirMissing;
			}

			if (options.RigDest != null && !Directory.Exists(options.RigDest))
			{
				LogError("Could not locate rig output directory");
				return ErrorCodeCompileDestRigDirMissing;
			}

			if (!options.GenerateModel && !options.GenerateRig)
			{
				LogWarn("Skipped both model and rig");
				return ErrorCodeCompileSkippedBoth;
			}

			return null;
		}

		private static FileStream OpenWrite(string filename)
		{
			var fileInfo = new FileInfo(filename);
			var fileMode = fileInfo.Exists ? FileMode.Truncate : FileMode.CreateNew;
			return File.Open(filename, fileMode, FileAccess.Write, FileShare.None);
		}

		private static void CompileModel(string input, string outModel, string outRig, CompileOptions options)
		{
			var model = JsonSerializer.Deserialize<P3di>(File.ReadAllText(input));

			if (options.GenerateModel)
			{
				using var bw = new BinaryWriter(OpenWrite(outModel));
				WriteModel(bw, model, true, options);
			}

			if (options.GenerateRig)
			{
				using var bw = new BinaryWriter(OpenWrite(outRig));
				WriteModel(bw, model, false, options);
			}
		}

		private static void WriteModel(BinaryWriter bw, P3di model, bool writeVertexData, CompileOptions options)
		{
			bw.Write(Encoding.ASCII.GetBytes(writeVertexData ? "P3D" : "P3DR"));

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
				WriteMesh(bw, mesh, writeVertexData, options);
		}

		private static void WriteMesh(BinaryWriter bw, Mesh mesh, bool writeVertexData, CompileOptions options)
		{
			bw.Write(Encoding.ASCII.GetBytes(mesh.Name));
			bw.Write((byte)0);

			WriteMeshTransform(bw, mesh.Transform);

			if (writeVertexData)
			{
				bw.Write(GetMaterial(mesh.Name, mesh.Material));

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
								bw.Write(SnapTexCoord(vertices[vertIdx].Texture[i], options));
						}

						// Repeat the last triangle vertex to make a quad
						WriteVec3(bw, vertices[2].Position);

						for (var i = 0; i < 2; i++)
							bw.Write(SnapTexCoord(vertices[2].Texture[i], options));
					}
					else if (vertices.Length == 4)
					{
						for (var vertIdx = 0; vertIdx < 4; vertIdx++)
						{
							WriteVec3(bw, vertices[vertIdx].Position);

							for (var i = 0; i < 2; i++)
								bw.Write(SnapTexCoord(vertices[vertIdx].Texture[i], options));
						}
					}
					else
						throw new NotSupportedException($"Only triangles and quads supported, found {vertices.Length}-gon in object {mesh.Name}");
				}
			}

			bw.Write(mesh.Children.Length);

			foreach (var child in mesh.Children)
				WriteMesh(bw, child, writeVertexData, options);
		}

		private static void WriteSocketTransform(BinaryWriter bw, float[][] t)
		{
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

		private static float SnapTexCoord(float f, CompileOptions options)
		{
			if (!options.Snap)
				return f;

			var rounded = (float)Math.Round(f * options.SnapResolution);
			return (Math.Abs(rounded - f * options.SnapResolution) < options.SnapEpsilon ? rounded / options.SnapResolution : f);
		}

		private static byte GetMaterial(string objectName, string materialName)
		{
			switch (materialName)
			{
				case "MAT_DIFFUSE_OPAQUE":
					return (byte)FaceMaterial.DiffuseOpaque;
				case "MAT_DIFFUSE_CUTOUT":
					return (byte)FaceMaterial.DiffuseCutout;
				case "MAT_DIFFUSE_TRANSLUCENT":
					return (byte)FaceMaterial.DiffuseTranslucent;
				case "MAT_EMISSIVE":
					return (byte)FaceMaterial.Emissive;
				default:
					LogWarn($"Unsupported material \"{materialName}\" for object \"{objectName}\", defaulting to diffuse opaque");
					return (byte)FaceMaterial.DiffuseOpaque;
			}
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
		[JsonPropertyName("name")] public string Name { get; set; }

		[JsonPropertyName("parent")] public string Parent { get; set; }

		[JsonPropertyName("transform")] public float[][] Transform { get; set; }
	}

	public class Vertex
	{
		[JsonPropertyName("v")] public float[] Position { get; set; }

		[JsonPropertyName("t")] public float[] Texture { get; set; }
	}

	public class Face
	{
		[JsonPropertyName("normal")] public float[] Normal { get; set; }

		[JsonPropertyName("vertices")] public Vertex[] Vertices { get; set; }
	}

	public class Mesh
	{
		[JsonPropertyName("name")] public string Name { get; set; }

		[JsonPropertyName("transform")] public float[][] Transform { get; set; }

		[JsonPropertyName("material")] public string Material { get; set; }

		[JsonPropertyName("faces")] public Face[] Faces { get; set; }

		[JsonPropertyName("children")] public Mesh[] Children { get; set; }
	}

	public class P3di
	{
		[JsonPropertyName("version")] public int Version { get; set; }

		[JsonPropertyName("sockets")] public Socket[] Sockets { get; set; }

		[JsonPropertyName("meshes")] public Mesh[] Meshes { get; set; }
	}
}