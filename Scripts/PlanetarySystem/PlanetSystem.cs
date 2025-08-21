using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

public partial class PlanetSystem : Node3D
{
	[Export] public PackedScene orbitRendererPrefab;

	public static PlanetSystem Instance;
	// Default config path
	public static readonly string ConfigPath = "res://GameData";

	public static readonly string classTag = "([color=green]PlanetSystem[color=white])";

	public Node3D localSpace;
	public Node3D localSpacePlanets;
	public Node3D scaledSpace;
	public Control orbitRenderers;

	// Since patched conics require an SOI, then have a "root SOI" in the form of a "root body"
	public CelestialBody rootBody;

	public List<CelestialBody> celestialBodies = [];

	// Called when the node enters the scene tree for the first time.
	/*public override void _Ready()
	{
		Instance = this;
		localSpace = (Node3D)GetTree().GetFirstNodeInGroup("LocalSpace");
		localSpacePlanets = (Node3D)localSpace.FindChild("Planets");
		orbitRenderers = (Control)GetTree().GetFirstNodeInGroup("OrbitRenderers");
		CreateSystem(GetPlanetConfigs(ConfigPath));
	}*/

	// Start the Planet System for this particular save
	public void InitSystem(List<string> chosenPacks)
	{
        // Get all relevant celestial body configs loaded in this save
        List<string> planetConfigs = [];
        foreach (string pack in chosenPacks)
		{
            string fullPath = $"{ConfigPath}/{pack}";
            planetConfigs.AddRange(GetPlanetConfigs(ConfigPath));
			GD.PrintRich($"{classTag} Successfully indexed celestial pack '{ConfigPath}'");
        }
		// Might aswell do this while we're at it
		localSpace = (Node3D)GetTree().GetFirstNodeInGroup("LocalSpace");
		localSpacePlanets = (Node3D)localSpace.FindChild("Planets");
		orbitRenderers = (Control)GetTree().GetFirstNodeInGroup("OrbitRenderers");
		CreateSystem(planetConfigs);
		GD.PrintRich($"{classTag} System created successfully!");
	}

	public void CreateSystem(List<string> configs)
	{
		foreach (string str in configs)
		{
			//GD.Print(str);
			CreateCBody(str);
		}
		PostProcessPlanets();
	}

	public CelestialBody FindCBodyByName(string name)
	{
		foreach (CelestialBody cBody in celestialBodies)
		{
			if (cBody.name == name) return cBody;
		}
		GD.PrintRich($"{classTag} Couldn't find CelestialBody with name '{name}'");
		return null;
	}

	// Recursive function to get all planet configs in a path
	public static List<string> GetPlanetConfigs(string path)
	{
		List<string> files = [];
		DirAccess dir = DirAccess.Open(path);
		if (dir != null)
		{
			dir.ListDirBegin();
			string fileName = dir.GetNext();
			while (fileName != "")
			{
				string filePath = path + "/" + fileName;

				// Check if directory is a valid JSON and if it has the "cBody" configType
				FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
				if (file != null)
				{
					string content = file.GetAsText();

					Json jsonFile = new();
					Error err = jsonFile.Parse(content);
					Dictionary data = (Dictionary)jsonFile.Data;
					
					if (err == Error.Ok && (string)data["configType"] == "cBody")
					{
						GD.PrintRich($"{classTag} JSON found: {filePath}");
						files.Add(filePath);
					}else if (err == Error.ParseError)
					{
						GD.PrintErr($"Cannot parse JSON file: {filePath}");
					}
				}
				
				List<string> subFiles = GetPlanetConfigs(filePath);
				if (subFiles.Count > 0) files.AddRange(subFiles);

				fileName = dir.GetNext();
			}
		}
		return files;
	}

	// Function to set the parent and child planets after all planets are created (because some might be in the wrong order)
	public void PostProcessPlanets()
	{
		foreach (CelestialBody cBody in celestialBodies)
		{
			CelestialBody parent = FindCBodyByName(cBody.parentName);
			if (parent != null)
			{
				cBody.orbit.parent = parent;
				if (cBody.orbit.sphereOfInfluence <= 0) // 14959800320 * ((5.289772250524424*10^22) / (1.7565459*10^28))^(2/5)
					cBody.orbit.sphereOfInfluence = cBody.orbit.semiMajorAxis * Math.Pow(5.289772250524424e22 / 1.7565459e28, 2f/5f);
				cBody.cartesianData.parent = parent;
				parent.childPlanets.Add(cBody);
			}
			localSpacePlanets.AddChild(cBody);
			cBody.Name = cBody.name;
		}
	}

	public void CreateCBody(string configPath)
	{
		CelestialBody cBody = ParseConfig(configPath);
		// Add cBody to the great planetary list (and set as root body if it is)
		celestialBodies.Add(cBody);
		if (cBody.orbit != null)
		{
			//OrbitRenderer renderer = (OrbitRenderer)orbitRendererPrefab.Instantiate();
			//renderer.cBody = cBody;
			//renderer.camera = ((RemoteCam)GetTree().GetFirstNodeInGroup("Camera")).localCamera;
			//orbitRenderers.AddChild(renderer);
		}
		if (cBody.isRoot) rootBody = cBody;
        cBody.pqsSphere = new TerrainGen
        {
			cBody = cBody,
			runInSeparateThread = false,
            radius = (float)cBody.radius
        };
        cBody.AddChild(cBody.pqsSphere);
	}

	public static CelestialBody ParseConfig(string path)
	{
		FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		string content = file.GetAsText();

		Json jsonFile = new();
		Error err = jsonFile.Parse(content);
		Dictionary data = (Dictionary)jsonFile.Data;

		if (err != Error.Ok)
		{
			GD.PushWarning($"Config file {path} could not be loaded.");
			return null;
		}

		CelestialBody cBody = new();

		// Handle "properties" dictionary
		if (ConfigUtility.TryGetDictionary("properties", data, out Dictionary properties))
		{
			cBody.name = properties.TryGetValue("name", out var name) ? (string)name : MissingString(path, "name"); //GetValue("Properties", "name");
			GD.PrintRich($"{classTag} Parsing config for: {name}");
			// only mass or geeASL is required, the unassigned one will be calculated based off one of the values.
			cBody.mass = properties.TryGetValue("mass", out var mass) ? (double)mass : -1;
			cBody.geeASL = properties.TryGetValue("geeASL", out var geeASL) ? (double)geeASL : -1;
			cBody.radius = properties.TryGetValue("radius", out var radius) ? (double)radius : MissingNum(path, "radius");

			if (cBody.mass < 0)
			{
				if (cBody.geeASL < 0) MissingNum(path, "geeASL");
				cBody.mass = cBody.geeASL * PatchedConics.EarthGravity * Mathf.Pow(cBody.radius, 2f) / PatchedConics.GravConstant;
			}
			if (cBody.geeASL < 0)
			{
				if (cBody.mass < 0) MissingNum(path, "mass");
				cBody.geeASL = cBody.mass * PatchedConics.GravConstant / Mathf.Pow(cBody.radius, 2f) / PatchedConics.EarthGravity;
			}
		}else{
			GD.PrintErr($"Properties dictionary does not exist in planet config {path}");
			return null;
		}

		// The "parent" parameter is set in a different function because the parent might be parsed after this one
		if (ConfigUtility.TryGetDictionary("orbit", data, out Dictionary orbit))
		{
			cBody.parentName = orbit.TryGetValue("parent", out var pnm) ? (string)pnm : null;
			cBody.orbit = new Orbit{
				semiMajorAxis = orbit.TryGetValue("semiMajorAxis", out var sma) ? (double)sma : MissingNum(path, "orbit/semiMajorAxis"),
				inclination = orbit.TryGetValue("inclination", out var inc) ? (double)inc + Math.PI : MissingNum(path, "orbit/inclination"),
				eccentricity = orbit.TryGetValue("eccentricity", out var ecc) ? (double)ecc : MissingNum(path, "orbit/eccentricity"),
				argumentOfPeriapsis = orbit.TryGetValue("argumentOfPeriapsis", out var arp) ? (double)arp : MissingNum(path, "orbit/argumentOfPeriapsis"),
				longitudeOfAscendingNode = orbit.TryGetValue("longitudeOfAscendingNode", out var lon) ? (double)lon : MissingNum(path, "orbit/longitudeOfAscendingNode"),
				trueAnomaly = orbit.TryGetValue("trueAnomaly", out var tra) ? (double)tra : 0,
				trueAnomalyAtEpoch = orbit.TryGetValue("trueAnomalyAtEpoch", out var tre) ? (double)tre : MissingNum(path, "orbit/trueAnomalyAtEpoch"),
				sphereOfInfluence = orbit.TryGetValue("sphereOfInfluence", out var soi) ? (double)soi : -1,
			};
		}else{
			GD.PrintRich($"{classTag} CBody {cBody.name} is missing its orbit! If this is intended, then disregard this message.");
		}
		// Same here too
		cBody.cartesianData = new()
		{
			position = Double3.Zero,
			velocity = Double3.Zero
		};

		// Oceans
		if (ConfigUtility.TryGetDictionary("ocean", data, out Dictionary ocean))
		{
			
		}

		// PQS
		if (ConfigUtility.TryGetDictionary("pqs", data, out Dictionary pqs))
		{
			if (ConfigUtility.TryGetArray("pqsMods", pqs, out Godot.Collections.Array pqsMods))
			{
				// Initialize pqs mods for the cBody
				cBody.pqsMods = [];
				foreach (var pqMod in pqsMods)
				{
					Dictionary pqsMod = (Dictionary)pqMod;
					string pqsModType = pqsMod.TryGetValue("mod", out var mtp) ? (string)mtp : MissingString(path, "pqs/mods/mod");

					switch (pqsModType)
					{
						case "fastNoise3D":
							FastNoise3D mod = new();
							mod.Initialize(pqsMod, path);
							cBody.pqsMods.Add(mod);
							break;
						default:
							GD.PrintErr($"Unknown PQS mod type in {path}");
							break;
					}
				}
			}
		}

		cBody.isRoot = data.TryGetValue("rootBody", out var rbd) && (bool)rbd;

		return cBody;
	}

	// Throw missing key errors when parsing configs
	public static string MissingString(string path, string key)
	{
		GD.PrintErr($"Parsing error in planet config {path}");
		GD.PrintErr($"Key {key} is missing!");

		return "Epic Fail :sob:";
	}
	public static double MissingNum(string path, string key)
	{
		GD.PrintErr($"Parsing error in planet config {path}");
		GD.PrintErr($"Key {key} is missing!");

		return double.NaN;
	}
}
