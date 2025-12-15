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

public class CityGeometryGenerator : MonoBehaviour
{
	[SerializeField] private ArcGISMapComponent arcGISMapComponent;
	[SerializeField] private HPRoot arcGISHPRoot;
	[SerializeField] private GameObject cityPrefab;
	[SerializeField] private CityInfoBox cityInfoBox;
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
				int cityTemperature = liveWeatherData["temperature"].AsInt;

				// Create a new city object
				// This object is just a basic sphere
				CityIndicator cityIndicator = Instantiate(cityPrefab, transform).GetComponent<CityIndicator>();
				cityIndicator.Initialize(cityInfoBox, cityData.Name, cityData.Population, cityTemperature);

				// Set the mesh renderer material
				MeshRenderer cityRenderer = cityIndicator.GetComponent<MeshRenderer>();
				cityRenderer.material = new Material(materialTemplate);
				cityRenderer.material.color = temperatureGradient.Evaluate(Remap(cityTemperature, 0, 100, 0, 1));

				// Calculate the unity coordinate of the geographic coordinate
				ArcGISPoint geographicPoint = new ArcGISPoint(cityData.Coordinates.y, cityData.Coordinates.x, 0f, ArcGISSpatialReference.WGS84());
				double3 universeCoordinate = arcGISMapComponent.View.GeographicToWorld(geographicPoint);
				double3 unityCoordinate = arcGISHPRoot.TransformPoint(universeCoordinate);

				// Set the position of the city object
				cityIndicator.transform.localPosition = unityCoordinate.ToVector3();
				cityIndicator.transform.localScale = Remap(cityData.Population, 0, 5000000, minCityScale, maxCityScale) * Vector3.one;

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
			string name = dataValues[(int)CityDataIndex.CITY_ASCII];

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
				cities[stateID].Insert(cityIndex, new CityData(name, coords, population));
				totalCities++;

				if (cities[stateID].Count > citiesPerState)
					cities[stateID].RemoveAt(citiesPerState);
			}
		}
	}

	private float Remap(float value, float from1, float to1, float from2, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}
}