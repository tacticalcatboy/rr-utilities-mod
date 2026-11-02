using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Utilities;

/// <summary>
/// Persisted as settings.json in the mod folder (the 0.6 loader has no settings store of its own).
/// </summary>
public class UtilitiesSettings
{
	public const string FileName = "settings.json";

	/// <summary>Scale of the mod's own IMGUI windows.</summary>
	public float UiScale = 1f;

	public bool showGraphics = true;
	public bool showDistance = true;

	public GraphicsSettings graphicsSettings = new GraphicsSettings();
	public DistanceSettings distanceSettings = new DistanceSettings();

	public class GraphicsSettings
	{
		// Anti-aliasing
		public float SSAA = 1f;
		[JsonConverter(typeof(StringEnumConverter))]
		public MsaaQuality MSAA = (MsaaQuality)((QualitySettings.renderPipeline as UniversalRenderPipelineAsset)?.msaaSampleCount ?? 2);
		[JsonConverter(typeof(StringEnumConverter))]
		public AntialiasingMode PostProcessingAntiAliasing = AntialiasingMode.FastApproximateAntialiasing;
		[JsonConverter(typeof(StringEnumConverter))]
		public AntialiasingQuality PostProcessingAntiAliasingQuality = AntialiasingQuality.High;

		// LOD
		public float lodBias = QualitySettings.lodBias;
		public float lodBiasFoliage = QualitySettings.lodBias;

		// Advanced
		[JsonConverter(typeof(StringEnumConverter))]
		public DepthPrimingMode DepthPrimingMode;
	}

	public class DistanceSettings
	{
		public float SwitchStandDistance = 300f;
		public float SwitchStandRadius = 0.34f;

		public float FlareDistance = 300f;
		public float FlareRadius = 0.27f;

		public float CouplerDistance = 250f;
		public float CouplerRadius = 0.21f;

		public float GladhandsDistance = 30f;
		public float GladhandsRadius = 0.12f;

		public float StationDistance = 250f;
		public float StationRadius = 4.81f;

		public float IndustryDistance = 250f;

		public float CoalDistance = 150f;
		public float CoalRadius = 0.6f;

		public float WaterDistance = 150f;
		public float WaterRadius = 0.6f;

		public float DieselDistance = 150f;
		public float DieselRadius = 0.6f;

		public float QueryDistance = 1500f;
	}

	[JsonIgnore]
	private string? _path;

	public static UtilitiesSettings Load(string? modDirectory)
	{
		string path = Path.Combine(modDirectory ?? ".", FileName);
		UtilitiesSettings? settings = null;
		try
		{
			if (File.Exists(path))
				settings = JsonConvert.DeserializeObject<UtilitiesSettings>(File.ReadAllText(path));
		}
		catch (Exception ex)
		{
			ModEntryPoint.Warning($"Could not read {FileName}, using defaults: {ex.Message}");
		}

		settings ??= new UtilitiesSettings();
		settings.graphicsSettings ??= new GraphicsSettings();
		settings.distanceSettings ??= new DistanceSettings();
		settings._path = path;
		settings.Sanitise();
		return settings;
	}

	public void Save()
	{
		Sanitise();
		if (string.IsNullOrEmpty(_path)) return;

		try
		{
			File.WriteAllText(_path, JsonConvert.SerializeObject(this, Formatting.Indented));
		}
		catch (Exception ex)
		{
			ModEntryPoint.Warning($"Could not save {FileName}: {ex.Message}");
		}
	}

	private void Sanitise()
	{
		if (float.IsNaN(UiScale) || UiScale < 0.5f || UiScale > 3f) UiScale = 1f;
		var g = graphicsSettings;
		g.SSAA = Mathf.Clamp((float)(Math.Round(g.SSAA * 2, MidpointRounding.AwayFromZero) / 2), 1f, 2f);
		g.lodBias = Mathf.Clamp(g.lodBias, 1f, 10f);
		g.lodBiasFoliage = Mathf.Clamp(g.lodBiasFoliage, 1f, 10f);
	}
}
