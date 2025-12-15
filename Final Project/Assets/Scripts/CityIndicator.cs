using UnityEngine;
using UnityEngine.EventSystems;

public class CityIndicator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	private CityInfoBox _cityInfoBox;
	private string _cityName;
	private int _population;
	private int _temperature;

	public void Initialize(CityInfoBox cityInfoBox, string cityName, int population, int temperature)
	{
		_cityInfoBox = cityInfoBox;
		_cityName = cityName;
		_population = population;
		_temperature = temperature;
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		_cityInfoBox.SetEnabled(true, _cityName, _population, _temperature);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		_cityInfoBox.SetEnabled(false, _cityName, _population, _temperature);
	}
}
