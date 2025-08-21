using System.Collections.Generic;
using Godot;
using Godot.Collections;

// Helper class for parsing JSON configs
public partial class ConfigUtility
{
	public static readonly string GameData = "res://GameData";

	public static Dictionary ParseConfig(string path)
	{
		FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
			
		string content = file.GetAsText();

		Json jsonFile = new();
		Error err = jsonFile.Parse(content);
		Dictionary data = (Dictionary)jsonFile.Data;

		if (err == Error.Ok)
			return data;

		GD.Print($"Failed to parse config {path}");
		return null;
	}

	public static List<string> GetConfigs(string path, string type = null)
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

				// Check if directory is a valid JSON
				FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
				if (file != null)
				{
					string content = file.GetAsText();

					Json jsonFile = new();
					Error err = jsonFile.Parse(content);
					Dictionary data = (Dictionary)jsonFile.Data;

					string configType = (string)data["configType"];

					// Get the config if it matches the type or if the type is null
					// Also check for errors but that's obviouuss
					if (err == Error.Ok && (configType == type || type == null))
					{
						files.Add(filePath);
					}
				}
				
				List<string> subFiles = GetConfigs(filePath, type);
				if (subFiles.Count > 0) files.AddRange(subFiles);

				fileName = dir.GetNext();
			}
		}
		return files;
	}
	public static bool TryGetDictionary(string name, Dictionary parent, out Dictionary dict)
	{
		if (parent.TryGetValue(name, out var dih))
		{
			dict = (Dictionary)dih;
			return true;
		}
		dict = null;
		return false;
	}
	public static bool TryGetArray(string name, Dictionary parent, out Array array)
	{
		if (parent.TryGetValue(name, out var arr))
		{
			array = (Array)arr;
			return true;
		}
		array = null;
		return false;
	}
}
