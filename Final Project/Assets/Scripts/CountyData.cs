using System.Collections.Generic;
using UnityEngine;

public class CountyData
{
	public List<List<Vector2>> GeoData { get; private set; }
	public List<MeshRenderer> MeshRenderers { get; private set; }

	public CountyData ()
	{
		GeoData = new List<List<Vector2>>();
		MeshRenderers = new List<MeshRenderer>();
	}
}