using Godot;
using Godot.Collections;
using System;

public partial class PartManager : Node
{
	// Ahh the beautiful monolithic function! 
	// The truest form of programming, not giving a shit and living with your mistakes.
	public static Part ParsePartConfig(string path)
	{
		Dictionary config = ConfigUtility.ParseConfig(path);
		GD.Print(config);
		
		return new Part(); // Return an empty part for now because I DIDN'T FINISH THIS FUNCTION YET
	}
	
	public override void _Ready()
	{
		foreach (string gameData in ResourceLoader.ListDirectory("res://GameData")){
			if(gameData == "Stock/"){
				foreach (string partCategories in ResourceLoader.ListDirectory("res://GameData/"+gameData+"Parts")){
					foreach (string partsInCategory in ResourceLoader.ListDirectory("res://GameData/"+gameData+"Parts/"+partCategories)){
						ParsePartConfig("res://GameData/"+gameData+"Parts/"+partCategories+partsInCategory+"config.json");
					}
				}
			}else{
				GD.Print("OH MY GOD IT'S A MOD I CAN'T HANDLE IT YET");
				throw new System.OutOfMemoryException("Mod was detected however mods cannot be handeled by PartManager yet. Mod: "+gameData);
			}
		}
	}
}
