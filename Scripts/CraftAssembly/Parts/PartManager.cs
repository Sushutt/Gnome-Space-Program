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
        
        return new Part(); // Return an empty part for now because I DIDN'T FINISH THIS FUNCTION YET
    }
}