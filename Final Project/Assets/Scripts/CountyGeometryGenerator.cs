using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Geometry;
using Esri.HPFramework;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public enum CityDataIndex
{
	CITY, CITY_ASCII, STATE_ID, STATE_NAME, COUNTY_FIPS, COUNTY_NAME, LAT, LNG, POPULATION, DENSITY, SOURCE, MILITARY, INCORPORATED, TIMEZONE, RANKING, ZIPS, ID
}

public class CountyGeometryGenerator : MonoBehaviour
{
	[SerializeField] private ArcGISMapComponent arcGISMapComponent;
	[SerializeField] private HPRoot arcGISHPRoot;
	[SerializeField] private Material materialTemplate;
	[Space]
	[SerializeField] private TextMeshProUGUI liveDataText;
	[SerializeField] private Slider liveDataProgressBar;
	[Space]
	[SerializeField] private Gradient temperatureGradient;
	[SerializeField] private int minCityScale;
	[SerializeField] private int maxCityScale;
	[SerializeField, Min(0)] private int minPopulation;
	[SerializeField, Min(1)] private int citiesPerState;

	private List<string> unusedStates = new List<string>() { "AK", "HI", "PR" };
	private Dictionary<string, List<CityData>> cities;
	private int totalCities;

	private void Awake()
	{
		cities = new Dictionary<string, List<CityData>>();
	}

	private void Start()
	{
		StartCoroutine(RequestWeatherData());
	}

	private IEnumerator RequestWeatherData()
	{
		LoadCityData();

		float citiesLoaded = 0f;
		liveDataText.text = $"Fetching Live Data: 0 / {totalCities} (0.0%)";
		liveDataProgressBar.value = 0;

		foreach (KeyValuePair<string, List<CityData>> kvp in cities)
		{
			foreach (CityData cityData in kvp.Value)
			{
				string baseURL = $"https://api.weather.gov/points/{cityData.Coordinates.x},{cityData.Coordinates.y}";

				// Need to send this request to get the forecast URL for the current city
				UnityWebRequest baseRequest = UnityWebRequest.Get(baseURL);
				yield return baseRequest.SendWebRequest();

				// If the request failed, continue to the next city
				if (baseRequest.result != UnityWebRequest.Result.Success)
					continue;

				// Get the hourly forecast URL
				JSONNode forecastURL = JSONNode.Parse(baseRequest.downloadHandler.text)["properties"]["forecastHourly"];

				UnityWebRequest forecastRequest = UnityWebRequest.Get(forecastURL);
				yield return forecastRequest.SendWebRequest();

				// If the request failed, continue to the next city
				if (forecastRequest.result != UnityWebRequest.Result.Success)
					continue;

				JSONNode liveWeatherData = JSONNode.Parse(forecastRequest.downloadHandler.text)["properties"]["periods"][0];

				// Create a new city object
				// This object is just a basic sphere
				GameObject cityObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				cityObject.transform.SetParent(transform);

				// Set the mesh renderer material
				cityData.MeshRenderer = cityObject.GetComponent<MeshRenderer>();
				cityData.MeshRenderer.material = new Material(materialTemplate);
				cityData.MeshRenderer.material.color = temperatureGradient.Evaluate(Remap(liveWeatherData["temperature"].AsInt, 0, 100, 0, 1));

				// Calculate the unity coordinate of the geographic coordinate
				ArcGISPoint geographicPoint = new ArcGISPoint(cityData.Coordinates.y, cityData.Coordinates.x, 0f, ArcGISSpatialReference.WGS84());
				double3 universeCoordinate = arcGISMapComponent.View.GeographicToWorld(geographicPoint);
				double3 unityCoordinate = arcGISHPRoot.TransformPoint(universeCoordinate);

				// Set the position of the city object
				cityObject.transform.localPosition = unityCoordinate.ToVector3();
				cityObject.transform.localScale = Remap(cityData.Population, 0, 5000000, minCityScale, maxCityScale) * Vector3.one;

				// Update the progress bar display
				citiesLoaded++;
				liveDataText.text = $"Fetching Live Data: {citiesLoaded} / {totalCities} ({(citiesLoaded / totalCities * 100f):0.0}%)";
				liveDataProgressBar.value = citiesLoaded / totalCities;
			}
		}
	}

	private void LoadCityData()
	{
		string[] csvData = File.ReadAllLines("Data/us_cities.csv");

		totalCities = 0;

		// Skip the first index because it is a template
		for (int i = 1; i < csvData.Length; i++)
		{
			// Split the data entry into its individual pieces
			string[] dataValues = csvData[i].Split("\",\"");

			// Check to make sure that the current county is not in an unused state
			string stateID = dataValues[(int)CityDataIndex.STATE_ID];
			if (unusedStates.Contains(stateID))
				continue;

			// Check to make sure the population of the city is above the threshold
			int population = int.Parse(dataValues[(int)CityDataIndex.POPULATION]);
			if (population < minPopulation)
				continue;

			// Create an new city data list if one has not been created for the current state
			if (!cities.ContainsKey(stateID))
				cities.Add(stateID, new List<CityData>());

			// Get the city's geographic coordinates
			Vector2 coords = new Vector2(float.Parse(dataValues[(int)CityDataIndex.LAT]), float.Parse(dataValues[(int)CityDataIndex.LNG]));

			// Sort the current city into the list to get the most populated cities for each state
			int cityIndex = cities[stateID].Count - 1;
			for (; cityIndex >= 0; cityIndex--)
			{
				if (cities[stateID][cityIndex].Population >= population)
					break;
			}
			cityIndex++;

			if (cityIndex < citiesPerState)
			{
				cities[stateID].Insert(cityIndex, new CityData(coords, population));
				totalCities++;

				if (cities[stateID].Count > citiesPerState)
					cities[stateID].RemoveAt(citiesPerState);
			}
		}
	}

	//private void LoadBoundaryData()
	//{
	//	string[] csvBoundaryData = File.ReadAllLines("Data/us-county-boundaries.csv");

	//	for (int i = 1; i < csvBoundaryData.Length; i++)
	//	{
	//		// Split the data entry into its individual pieces
	//		string[] dataValues = csvBoundaryData[i].Split(";");

	//		// Create a new county data object
	//		CountyData countyData = new CountyData();

	//		// Check to make sure that the current county is not in an unused state
	//		int stateFP = int.Parse(dataValues[(int)CountyDataIndex.STATEFP]);
	//		if (unusedStates.Contains(stateFP))
	//			continue;

	//		// Load all geography data using JSON nodes
	//		JSONNode coordinateNode = JSONNode.Parse(dataValues[(int)CountyDataIndex.GEO_SHAPE].Replace("\"\"", "\"")[1..^1])["coordinates"];
	//		foreach (JSONNode coordinateGroupNode in coordinateNode)
	//		{
	//			countyData.GeoData.Add(new List<Vector2>());

	//			// There may be sub-lists of points if the county spans multiple shapes
	//			JSONNode groupNode = coordinateGroupNode.Count == 1 ? coordinateGroupNode[0] : coordinateGroupNode;
	//			for (float j = 0; (int)j < groupNode.Count; j += meshCoordinateIterationSkip)
	//			{
	//				// Calculate the unity coordinate of the geographic coordinate
	//				Vector2 geographicCoordinate = new Vector2(groupNode[(int)j][0], groupNode[(int)j][1]);
	//				ArcGISPoint geographicPoint = new ArcGISPoint(geographicCoordinate.x, geographicCoordinate.y, meshMinAltitude, ArcGISSpatialReference.WGS84());
	//				double3 universeCoordinate = arcGISMapComponent.View.GeographicToWorld(geographicPoint);
	//				double3 unityCoordinate = arcGISHPRoot.TransformPoint(universeCoordinate);

	//				// Scale the mesh points down so the polygon collider component can generate a new mesh
	//				countyData.GeoData[^1].Add(new Vector2((float)unityCoordinate[0] / meshScale, (float)unityCoordinate[2] / meshScale));
	//			}
	//		}

	//		// Add the county data to the county list
	//		int geoID = int.Parse(dataValues[(int)CountyDataIndex.GEOID]);
	//		CountyDataList.Add(geoID, countyData);
	//	}
	//}

	//private void GenerateMeshGroups(int geoID)
	//{
	//	CountyData countyData = CountyDataList[geoID];

	//	// Create a new mesh group container object
	//	GameObject meshGroup = new GameObject($"{geoID} Mesh Group");
	//	meshGroup.transform.SetParent(transform);

	//	for (int i = 0; i < countyData.GeoData.Count; i++)
	//	{
	//		try
	//		{
	//			// Create a gameobject to display the mesh
	//			GameObject meshObject = new GameObject($"Mesh {i}");

	//			// Create polygon collider component to generate a triangulated mesh
	//			PolygonCollider2D collider = meshObject.AddComponent<PolygonCollider2D>();
	//			collider.SetPath(0, countyData.GeoData[i]);
	//			Mesh mesh = collider.CreateMesh(true, true);
	//			meshObject.AddComponent<MeshFilter>().mesh = mesh;
	//			Destroy(collider);

	//			// Set the material of the mesh renderer
	//			MeshRenderer meshRenderer = meshObject.AddComponent<MeshRenderer>();
	//			meshRenderer.material = new Material(meshMaterial);
	//			countyData.MeshRenderers.Add(meshRenderer);

	//			// Set the position of the mesh so it aligns with the map
	//			meshObject.transform.SetPositionAndRotation(new Vector3(0f, meshMinAltitude, 0f), Quaternion.Euler(90f, 0f, 0f));
	//			meshObject.transform.localScale = new Vector3(meshScale, meshScale, 1f);
	//			meshObject.transform.SetParent(meshGroup.transform);
	//		}
	//		catch
	//		{
	//			Debug.Log($"Error With GeoID: {geoID}");
	//		}
	//	}
	//}

	//private void UpdateStateMeshGroups ( )
	//{
	//	foreach (int stateFIPS in CountyDataList.Keys)
	//	{
	//		UpdateStateMeshGroup(stateFIPS);
	//	}

	//	yearText.text = $"Year: {(int) yearSlider.value}";
	//	medianTemperatureText.text = $"Median Temperature: {medianTemperatureSlider.value:0.00}°F";
	//	temperatureRangeText.text = $"Temperature Range: ±{temperatureRangeSlider.value:0.00}°F";
	//}

	//public void UpdateStateMeshGroup (int stateFIPS)
	//{
	//	StateData stateData = CountyDataList[stateFIPS];
	//	float temperature = stateData.SafeGetTemperature((int) yearSlider.value) - medianTemperatureSlider.value;
	//	Color temperatureColor = temperatureGradient.Evaluate(Remap(temperature, -temperatureRangeSlider.value, temperatureRangeSlider.value, 0f, 1f));

	//	for (int i = 0; i < stateData.MeshRenderers.Count; i++)
	//	{
	//		stateData.MeshRenderers[i].material.color = temperatureColor;
	//	}
	//}

	private float Remap(float value, float from1, float to1, float from2, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}
}