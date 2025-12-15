using UnityEngine;

public class CityData
{
	public readonly Vector2 Coordinates;
	public readonly int Population;

	public MeshRenderer MeshRenderer { get; set; }

	public CityData(Vector2 coordinates, int population)
	{
		Coordinates = coordinates;
		Population = population;
	}
}