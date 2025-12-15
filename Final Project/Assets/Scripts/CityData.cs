using UnityEngine;

public readonly struct CityData
{
	public readonly string Name;
	public readonly Vector2 Coordinates;
	public readonly int Population;

	public CityData(string name, Vector2 coordinates, int population)
	{
		Name = name;
		Coordinates = coordinates;
		Population = population;
	}
}