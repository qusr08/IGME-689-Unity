using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class CityInfoBox : MonoBehaviour
{
	[SerializeField] private RectTransform rectTransform;
	[SerializeField] private GameObject uiContainer;
	[SerializeField] private Canvas canvas;
	[SerializeField] private TextMeshProUGUI cityNameText;
	[SerializeField] private TextMeshProUGUI populationText;
	[SerializeField] private TextMeshProUGUI temperatureText;

	private void Update()
	{
		rectTransform.anchoredPosition = Mouse.current.position.ReadValue() / canvas.scaleFactor;
	}

	public void SetEnabled(bool isEnabled, string cityName, int population, int temperature)
	{
		uiContainer.SetActive(isEnabled);
		cityNameText.text = cityName;
		populationText.text = $"Population: {population:#,##0}";
		temperatureText.text = $"Temperature: {temperature}°F";
	}
}
