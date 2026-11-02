using Character;
using Game;
using Game.Events;
using Game.Messages;
using Game.State;
using HarmonyLib;
using Helpers;
using Map.Runtime;
using Model;
using Model.Definition;
using Model.Definition.Data;
using Model.Ops;
using Model.Ops.Definition;
using RollingStock;
using RollingStock.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Track;
using UI.CarInspector;
using UI.Console.Commands;
using UI.PreferencesWindow;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if HAS_NATURE_RENDERER
using VisualDesignCafe.Rendering.Nature;
#endif

namespace Utilities;

public class UtilitiesMod : MonoBehaviour
{
	public enum MapStates { MAINMENU, MAPLOADING, MAPLOADED, MAPUNLOADING }
	public static MapStates MapState { get; private set; } = MapStates.MAINMENU;

	public static UtilitiesMod? Instance { get; private set; }

	// GUI vars
	private bool showGui = false;
	private Rect buttonRect = new Rect(0, 30, 20, 20);
	private Rect windowRect = new Rect(20, 30, 0, 0);
	private Vector2 scrollPosition;
	private Rect scrollRect;

	private bool settingsShow = false;
	private Rect settingsRect;
	private Vector2 settingsScroll;

	// Switching to sandbox is permanent in Railroader 0.6, so it needs a second click
	private int pendingGameMode = -1;

	private Rect teleportRect;
	private int teleportLocation = -1;
	private bool teleportShow = false;
	private static string[] teleportLocations
	{
		get
		{
			return SpawnPoint.All.Where(sp => sp.name.ToLower() != "ds").Select(sp => sp.name).OrderBy(sp => sp).ToArray();
		}
	}

	private Rect loadRect;
	private int loadIndex = -1;
	private bool loadShow = false;
	private static string[] loadNames
	{
		get
		{
			return CarPrototypeLibrary.instance.opsLoads.Select(load => load.id).ToArray();
		}
	}

	internal static UtilitiesSettings Settings => ModEntryPoint.Settings;
	internal static RenderPipelineAsset? originalPipelineAsset;

	public static int Scale(float value) => Mathf.RoundToInt(value * Settings.UiScale);

	void Awake()
	{
		Instance = this;
	}

	void OnDestroy()
	{
		OnWorldUnload();
		MapState = MapStates.MAINMENU;
		if (Instance == this) Instance = null;
	}

	internal void OnWorldLoad()
	{
		if (MapState == MapStates.MAPLOADED) return;
		// Wait in Update() until the world's objects and camera exist before applying settings
		MapState = MapStates.MAPLOADING;
		readySince = -1f;
	}

	internal void OnWorldUnload()
	{
		if (MapState == MapStates.MAINMENU) return;
		MapState = MapStates.MAPUNLOADING;
		showGui = false;
		settingsShow = teleportShow = loadShow = false;
		pendingGameMode = -1;
		RestorePipelineAsset();
		MapState = MapStates.MAINMENU;
	}

	private float readySince = -1f;

	void Update()
	{
		if (MapState != MapStates.MAPLOADING) return;

		bool ready = StateManager.Shared != null && StateManager.Shared.Storage != null
			&& TrainController.Shared != null && Camera.main != null;
		if (!ready) { readySince = -1f; return; }

		// Give the scene a moment to finish spawning interactables
		if (readySince < 0f) { readySince = Time.unscaledTime; return; }
		if (Time.unscaledTime - readySince < 1f) return;

		MapState = MapStates.MAPLOADED;
		ModEntryPoint.Log("World ready, applying settings");
		OnDistanceSettingsChanged();
		OnGraphicsSettingsChanged();
	}

	internal static void RestorePipelineAsset()
	{
		if (originalPipelineAsset == null) return;
		var copy = QualitySettings.renderPipeline;
		QualitySettings.renderPipeline = originalPipelineAsset;
		if (copy != null && copy != originalPipelineAsset) Destroy(copy);
		originalPipelineAsset = null;
	}

	private static void SafeRun(string what, Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			ModEntryPoint.Warning($"{what} failed: {ex.GetBaseException().Message}");
		}
	}

	public void OnGraphicsSettingsChanged()
	{
		if (MapState != MapStates.MAPLOADED) return;
		SafeRun("Applying graphics settings", ApplyGraphicsSettings);
		SafeRun("Applying foliage LOD", ApplyFoliageLod);
	}

	private void ApplyGraphicsSettings()
	{
		if (originalPipelineAsset == null && QualitySettings.renderPipeline != null)
		{
			originalPipelineAsset = QualitySettings.renderPipeline;
			QualitySettings.renderPipeline = UnityEngine.Object.Instantiate(QualitySettings.renderPipeline);
		}

		var pipelineAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;

		if (pipelineAsset != null)
		{
			pipelineAsset.msaaSampleCount = (int)Settings.graphicsSettings.MSAA;
			pipelineAsset.renderScale = Settings.graphicsSettings.SSAA;
		}

		var camera = Camera.main;
		if (camera != null)
		{
			var cam = camera.GetUniversalAdditionalCameraData();
			if (cam != null)
			{
				cam.antialiasing = Settings.graphicsSettings.PostProcessingAntiAliasing;
				cam.antialiasingQuality = Settings.graphicsSettings.PostProcessingAntiAliasingQuality;
				var universalRenderer = cam.scriptableRenderer as UniversalRenderer;
				if (universalRenderer != null) universalRenderer.depthPrimingMode = Settings.graphicsSettings.DepthPrimingMode;
			}
		}

		QualitySettings.lodBias = Settings.graphicsSettings.lodBias;
	}

	private void ApplyFoliageLod()
	{
#if HAS_NATURE_RENDERER
		var camera = Camera.main;
		if (camera == null) return;
		var natureRendererCameraSettings = camera.GetComponent<NatureRendererCameraSettings>();
		if (natureRendererCameraSettings != null)
			natureRendererCameraSettings.LodBias = Settings.graphicsSettings.lodBiasFoliage;
#endif
	}

	public void OnDistanceSettingsChanged()
	{
		if (MapState != MapStates.MAPLOADED) return;

		SafeRun("Switch stand colliders", () =>
		{
			foreach (var obj in Resources.FindObjectsOfTypeAll<SwitchStand>())
			{
				var collider = obj.GetComponentInChildren<CapsuleCollider>(true);
				if (collider == null) continue;
				collider.radius = Settings.distanceSettings.SwitchStandRadius;
				collider.height = 1.94f + (Settings.distanceSettings.SwitchStandRadius - 0.17f) * 2f;
			}
		});

		SafeRun("Flare colliders", () =>
		{
			foreach (var obj in Resources.FindObjectsOfTypeAll<FlarePickable>())
			{
				var collider = obj.GetComponentInChildren<CapsuleCollider>(true);
				if (collider == null) continue;
				collider.radius = Settings.distanceSettings.FlareRadius;
				collider.height = 0.4f + (Settings.distanceSettings.FlareRadius - 0.09f) * 2f;
			}
		});

		SafeRun("Coupler colliders", () =>
		{
			foreach (var obj in Resources.FindObjectsOfTypeAll<CouplerPickable>())
			{
				var collider = obj.GetComponentInChildren<CapsuleCollider>(true);
				if (collider == null) continue;
				collider.radius = Settings.distanceSettings.CouplerRadius;
				collider.height = 0.53f + (Settings.distanceSettings.CouplerRadius - 0.21f) * 2f;
			}
		});

		SafeRun("Gladhand colliders", () =>
		{
			foreach (var obj in Resources.FindObjectsOfTypeAll<GladhandClickable>())
			{
				var collider = obj.GetComponentInChildren<CapsuleCollider>(true);
				if (collider == null) continue;
				collider.radius = Settings.distanceSettings.GladhandsRadius;
				collider.height = 0.53f + (Settings.distanceSettings.GladhandsRadius - 0.21f) * 2f;
			}
		});

		SafeRun("Station agent colliders", () =>
		{
			foreach (var obj in Resources.FindObjectsOfTypeAll<StationAgent>())
			{
				var collider = obj.GetComponentInChildren<BoxCollider>(true);
				if (collider == null) continue;
				var delta = Settings.distanceSettings.StationRadius - 2.81f;
				collider.size = new Vector3(1f + delta, collider.size.y, 2.81f + delta);
			}
		});

		SafeRun("Coal/water/diesel colliders", () =>
		{
			foreach (var obj in Resources.FindObjectsOfTypeAll<KeyValuePickableToggle>())
			{
				var collider = obj.GetComponentInChildren<CapsuleCollider>(true);
				if (collider == null) continue;

				if (obj.displayTitle == "Coal Chute")
				{
					collider.radius = Settings.distanceSettings.CoalRadius;
					collider.height = 4.13f + (Settings.distanceSettings.CoalRadius - 0.3f) * 2f;
				}
				else if (obj.displayTitle == "Water Spout" || obj.displayTitle == "Water Column")
				{
					collider.radius = Settings.distanceSettings.WaterRadius;
					collider.height = 4.13f + (Settings.distanceSettings.WaterRadius - 0.3f) * 2f;
				}
				else if (obj.displayTitle == "Diesel Fueling Stand")
				{
					collider.radius = Settings.distanceSettings.DieselRadius;
					collider.height = 3f + (Settings.distanceSettings.DieselRadius - 0.1f) * 2f;
				}
			}
		});
	}

	void OnGUI()
	{
		if (MapState != MapStates.MAPLOADED)
		{
			showGui = false;
			return;
		}

		selectedCar = TrainController.Shared.SelectedCar;
		buttonRect = new Rect(0, Scale(30), Scale(20), Scale(20));
		if (GUI.Button(buttonRect, "U", new GUIStyle(GUI.skin.button) { fontSize = Scale(10), clipping = TextClipping.Overflow })) showGui = !showGui;

		if (showGui)
		{
			windowRect.x = Scale(20);
			windowRect.y = Scale(30);

			windowRect = GUILayout.Window(555, windowRect, Window, "Utilities", GUIStyle.none, GUILayout.Height(Screen.height - windowRect.y), GUILayout.Width(Scale(270) + GUI.skin.verticalScrollbar.fixedWidth));
			if (teleportShow)
			{
				if (teleportRect.width > 0)
				{
					teleportRect.x = windowRect.x + windowRect.width;
					teleportRect.y = windowRect.y;
				}
				teleportRect = GUILayout.Window(556, teleportRect, TeleportWindow, "Select Teleport Location");
			}
			if (loadShow)
			{
				if (loadRect.width > 0)
				{
					loadRect.x = windowRect.x + windowRect.width;
					loadRect.y = windowRect.y + scrollRect.height - loadRect.height;
				}
				loadRect = GUILayout.Window(557, loadRect, LoadWindow, "Select Load Type");
			}
			if (settingsShow)
			{
				settingsRect.x = windowRect.x + windowRect.width;
				settingsRect.y = windowRect.y;
				settingsRect = GUILayout.Window(558, settingsRect, SettingsWindow, "Utilities Settings", GUILayout.Width(Scale(360)), GUILayout.Height(Screen.height - settingsRect.y - Scale(20)));
			}
		}
	}

	private void SettingsWindow(int windowId)
	{
		settingsScroll = GUILayout.BeginScrollView(settingsScroll);
		SettingsGui.Draw(Settings, OnGraphicsSettingsChanged, OnDistanceSettingsChanged);
		GUILayout.EndScrollView();
		if (GUILayout.Button("Close")) settingsShow = false;
	}

	private Car? selectedCar;
	public static GUIStyle? styleMiddleRight;
	public static GUIStyle? styleMiddleCenter;
	private readonly HashSet<string> failedSections = new HashSet<string>();

	void Window(int windowId)
	{
		styleMiddleRight ??= new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleRight };
		styleMiddleCenter ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

		scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUI.skin.window, GUILayout.ExpandHeight(false));
		using (new GUILayout.VerticalScope())
		{
			if (GUILayout.Button(settingsShow ? "Hide Settings" : "Settings (graphics / click distances)"))
			{
				settingsShow = !settingsShow;
				teleportShow = loadShow = false;
			}

			Section("Game Mode", DrawGameMode);
			Section("Time Controls", DrawTimeControls);
			Section("Teleport Locations", DrawTeleport);
			Section("Weather Presets", DrawWeather);

			if (IsSandboxHost())
			{
				GUILayout.Label("Sandbox shortcuts", styleMiddleCenter, GUILayout.ExpandWidth(true));
				Section("Money", DrawMoney);
				Section("Train info", DrawTrainInfo);
				if (selectedCar != null)
				{
					Section("Repair", DrawRepair);
					Section("Fill/Empty", DrawFillEmpty);
					Section("Load/Unload", DrawLoadUnload);
				}
			}
		}
		GUILayout.EndScrollView();

		if (Event.current.type == EventType.Repaint)
		{
			scrollRect = GUILayoutUtility.GetLastRect();
		}
	}

	/// <summary>
	/// Draws one boxed section. Each section is its own method so that a game API that changed
	/// in a newer Railroader build only breaks that section (logged once), not the whole window.
	/// </summary>
	private void Section(string title, Action draw)
	{
		if (failedSections.Contains(title)) return;
		using (new GUILayout.VerticalScope("box"))
		{
			GUILayout.Label(title, styleMiddleCenter, GUILayout.ExpandWidth(true));
			try
			{
				draw();
			}
			catch (Exception ex) when (ex is not ExitGUIException)
			{
				failedSections.Add(title);
				ModEntryPoint.Warning($"'{title}' section disabled: {ex.GetBaseException().GetType().Name}: {ex.GetBaseException().Message}");
			}
		}
	}

	private static bool IsSandboxHost() => StateManager.IsSandbox && StateManager.IsHost;

	private void DrawGameMode()
	{
		var stateManager = StateManager.Shared;
		string[] modes = Enum.GetNames(typeof(GameMode));
		int gameMode = (int)stateManager.GameMode;
		int sandbox = Array.IndexOf(modes, "Sandbox");

		GUILayout.Label("Current: " + modes[gameMode], styleMiddleCenter);

		// Railroader 0.6 made converting a game to sandbox permanent (it protects progression
		// state), so leaving sandbox is not offered and entering it needs a confirmation.
		if (gameMode == sandbox)
		{
			GUILayout.Label("Sandbox is permanent for this save.", styleMiddleCenter);
			return;
		}

		using (new GUILayout.HorizontalScope())
		{
			GUILayout.FlexibleSpace();
			int selected = GUILayout.Toolbar(gameMode, modes);
			GUILayout.FlexibleSpace();
			if (selected != gameMode)
			{
				if (selected == sandbox) pendingGameMode = selected;
				else ApplyGameMode(selected);
			}
		}

		if (pendingGameMode == sandbox)
		{
			GUILayout.Label("<b>Converting to sandbox is permanent and can't be undone.</b>", new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
			using (new GUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Convert to Sandbox")) { ApplyGameMode(sandbox); pendingGameMode = -1; }
				if (GUILayout.Button("Cancel")) pendingGameMode = -1;
			}
		}
	}

	private static void ApplyGameMode(int mode)
	{
		StateManager.ApplyLocal(new PropertyChange("_game", "mode", new IntPropertyValue(mode)));
	}

	private void DrawTimeControls()
	{
		var stateManager = StateManager.Shared;
		GUILayout.Label("Time: " + TimeWeather.TimeOfDayString);
		if (!StateManager.CheckAuthorizedToSendMessage(default(WaitTime))) return;

		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Wait 1 Hour"))
			{
				StateManager.ApplyLocal(new WaitTime { Hours = 1f });
			}
			if (GUILayout.Button("    Sleep    "))
			{
				float currentHours = TimeWeather.Now.Hours;
				int interchangeServeHour = stateManager.Storage.InterchangeServeHour;
				float hours = ((currentHours < (float)interchangeServeHour) ? ((float)interchangeServeHour - currentHours) : (24f - currentHours + (float)interchangeServeHour));
				StateManager.ApplyLocal(new WaitTime { Hours = hours });
			}
		}
	}

	private void DrawTeleport()
	{
		if (GUILayout.Button("Teleport to Dispatch Station"))
		{
			Teleport("ds");
		}
		if (GUILayout.Button("Choose Location"))
		{
			teleportShow = !teleportShow;
			loadShow = settingsShow = false;
			teleportLocation = -1;
		}
	}

	private static void Teleport(string location)
	{
		SafeRun($"Teleport to {location}", () => new TeleportCommand().Execute(new string[] { "/tp", location }));
	}

	private void DrawWeather()
	{
		string[] weather = TimeWeather.WeatherIdLookup.Keys.Select(key => char.ToUpper(key[0]) + key.Substring(1)).OrderBy(x => x).ToArray();
		int selected = GUILayout.SelectionGrid(-1, weather, 2);
		if (selected != -1)
		{
			StateManager.ApplyLocal(new PropertyChange("_game", "weatherId", new IntPropertyValue(TimeWeather.WeatherIdLookup[weather[selected].ToLower()])));
		}
	}

	private void DrawMoney()
	{
		using (new GUILayout.HorizontalScope())
		{
			GUILayout.Label("Money: ");
			GUILayout.Label(StateManager.Shared.GetBalance().ToString("C0"), styleMiddleRight);
		}

		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("> $1k")) AddMoney(1000);
			if (GUILayout.Button("> $10k")) AddMoney(10000);
			if (GUILayout.Button("> $100k")) AddMoney(100000);
			if (GUILayout.Button("> $1M")) AddMoney(1000000);
		}
		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("< $1k")) AddMoney(-1000);
			if (GUILayout.Button("< $10k")) AddMoney(-10000);
			if (GUILayout.Button("< $100k")) AddMoney(-100000);
			if (GUILayout.Button("< $1M")) AddMoney(-1000000);
		}
	}

	private void DrawTrainInfo()
	{
		GUILayout.Label("Selected Loco/Car: " + (selectedCar != null ? CarInspector.TitleForCar(selectedCar) : "None"));
		if (selectedCar == null) return;

		GUILayout.Label(CarInspector.SubtitleForCar(selectedCar));
		GUILayout.Label($"Speed: {(Mathf.Abs(selectedCar.velocity) * 2.23694f),4:N1} mph");

		int count = selectedCar.Definition.LoadSlots.Count;
		for (int i = 0; i < count; i++)
		{
			CarLoadInfo? loadInfo = selectedCar.GetLoadInfo(i);
			if (loadInfo != null)
			{
				CarLoadInfo value = loadInfo.Value;
				Load load = CarPrototypeLibrary.instance.LoadForId(value.LoadId);
				if (load != null)
					GUILayout.Label($"Load: {value.LoadString(load)}");
			}
		}
	}

	private static IEnumerable<Car> SelectedFreightCars()
	{
		return TrainController.Shared.SelectedTrain.Where(car => car.Definition.Archetype.IsFreight()).ToList();
	}

	private void DrawRepair()
	{
		var car = selectedCar!;
		if (GUILayout.Button("Repair Car"))
		{
			car.SetCondition(1f);
		}
		if (GUILayout.Button("Repair Train"))
		{
			foreach (Car c in TrainController.Shared.SelectedTrain)
				c.SetCondition(1f);
		}
	}

	private static void FillCar(Car car)
	{
		var slots = car.Definition.LoadSlots;
		for (int i = 0; i < slots.Count; i++)
		{
			LoadSlot loadSlot = slots[i];
			if (!string.IsNullOrEmpty(loadSlot.RequiredLoadIdentifier))
				car.SetLoadInfo(i, new CarLoadInfo(loadSlot.RequiredLoadIdentifier, loadSlot.MaximumCapacity));
		}
	}

	private static void EmptyCar(Car car)
	{
		// Use each car's own slot count (the old code reused the selected car's count for the whole train)
		int count = car.Definition.LoadSlots.Count;
		for (int i = 0; i < count; i++)
			car.SetLoadInfo(i, null);
	}

	private static void LoadCar(Car car, string loadId)
	{
		new UI.Console.SetLoadCommand().SetLoad(car, loadId, new string[] { "/setload", car.id, loadId, "100%" });
	}

	private void DrawFillEmpty()
	{
		var car = selectedCar!;
		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Fill Car")) FillCar(car);
			if (GUILayout.Button("Empty Car")) EmptyCar(car);
		}
		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Fill Train")) foreach (var c in SelectedFreightCars()) FillCar(c);
			if (GUILayout.Button("Empty Train")) foreach (var c in SelectedFreightCars()) EmptyCar(c);
		}
	}

	private void DrawLoadUnload()
	{
		var car = selectedCar!;
		string[] names = loadNames;
		if (loadIndex >= names.Length) loadIndex = -1;

		using (new GUILayout.HorizontalScope())
		{
			GUILayout.Label("Load:", GUILayout.ExpandWidth(false));
			if (GUILayout.Button(loadIndex == -1 ? "Select Load" : names[loadIndex]))
			{
				loadShow = !loadShow;
				teleportShow = settingsShow = false;
				loadIndex = -1;
			}
		}

		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Load Car") && loadIndex >= 0) LoadCar(car, names[loadIndex]);
			if (GUILayout.Button("Empty Car")) EmptyCar(car);
		}
		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Load Train") && loadIndex >= 0) foreach (var c in SelectedFreightCars()) LoadCar(c, names[loadIndex]);
			if (GUILayout.Button("Empty Train")) foreach (var c in SelectedFreightCars()) EmptyCar(c);
		}
	}

	private void TeleportWindow(int windowId)
	{
		string[] locations = teleportLocations;
		teleportLocation = GUILayout.SelectionGrid(teleportLocation, locations, 2);
		if (teleportLocation != -1)
		{
			teleportShow = false;
			Teleport(locations[teleportLocation]);
			teleportLocation = -1;
		}
	}

	private void LoadWindow(int windowId)
	{
		loadIndex = GUILayout.SelectionGrid(loadIndex, loadNames, 2);
		if (loadIndex != -1)
		{
			loadShow = false;
		}
	}

	private static void AddMoney(int amount)
	{
		var sm = StateManager.Shared;
		sm.Balance = sm.Balance + amount;
		StateManager.Shared.SendFireEvent<BalanceDidChange>(default(BalanceDidChange));
	}
}

[HarmonyPatch("SwitchStandClick", "MaxPickDistance", MethodType.Getter)]
public static class SwitchStandMaxDistancePatch
{
	public static bool Prefix(ref float __result)
	{
		__result = ModEntryPoint.Settings.distanceSettings.SwitchStandDistance;
		
		return false;
	}
}

[HarmonyPatch("FlarePickable", "MaxPickDistance", MethodType.Getter)]
public static class FlareMaxDistancePatch
{
	public static bool Prefix(ref float __result)
	{
		__result = ModEntryPoint.Settings.distanceSettings.FlareDistance;

		return false;
	}
}

[HarmonyPatch("CouplerPickable", "MaxPickDistance", MethodType.Getter)]
public static class CouplerMaxDistancePatch
{
	public static bool Prefix(ref float __result)
	{
		__result = ModEntryPoint.Settings.distanceSettings.CouplerDistance;

		return false;
	}
}

[HarmonyPatch("GladhandClickable", "MaxPickDistance", MethodType.Getter)]
public static class GladhandsMaxDistancePatch
{
	public static bool Prefix(ref float __result)
	{
		__result = ModEntryPoint.Settings.distanceSettings.GladhandsDistance;

		return false;
	}
}

[HarmonyPatch("StationAgent", "MaxPickDistance", MethodType.Getter)]
public static class StationAgentMaxDistancePatch
{
	public static bool Prefix(ref float __result)
	{
		__result = ModEntryPoint.Settings.distanceSettings.StationDistance;

		return false;
	}
}

[HarmonyPatch("IndustryContentHoverable", "MaxPickDistance", MethodType.Getter)]
public static class IndustryMaxDistancePatch
{
	public static bool Prefix(ref float __result)
	{
		__result = ModEntryPoint.Settings.distanceSettings.IndustryDistance;

		return false;
	}
}

[HarmonyPatch("KeyValuePickableToggle", "MaxPickDistance", MethodType.Getter)]
public static class CoalWaterDieselMaxDistancePatch
{
	public static bool Prefix(ref float __result, KeyValuePickableToggle __instance)
	{
		if (__instance.displayTitle == "Coal Chute")
		{
			__result = ModEntryPoint.Settings.distanceSettings.CoalDistance;
			return false;
		}
		else if (__instance.displayTitle == "Water Spout" || __instance.displayTitle == "Water Column")
		{
			__result = ModEntryPoint.Settings.distanceSettings.WaterDistance;
			return false;
		}
		else if (__instance.displayTitle == "Diesel Fueling Stand")
		{
			__result = ModEntryPoint.Settings.distanceSettings.DieselDistance;
			return false;
		}
		

		return true;
	}
}

[HarmonyPatch("ObjectPicker", "QueryTooltipInfo")]
public static class QueryToolDistancePatch
{
	public static bool Prefix(ref TooltipInfo __result, Ray ray)
	{
		RaycastHit raycastHit;
		if (!Physics.Raycast(ray, out raycastHit, ModEntryPoint.Settings.distanceSettings.QueryDistance, (1 << Layers.Terrain) | (1 << Layers.Track)))
		{
			__result = TooltipInfo.Empty;
			return false;
		}
		if (raycastHit.collider.gameObject.layer == Layers.Track)
		{
			Graph graph = TrainController.Shared.graph;
			if (graph.TryGetLocationFromGamePoint(raycastHit.point, 1f, out Track.Location location))
			{
				float num = graph.CurvatureAtLocation(location, Graph.CurveQueryResolution.Interpolate);
				float num2 = Mathf.Abs(graph.GradeAtLocation(location));
				__result = new TooltipInfo("Track", string.Format("{0:F1}%, {1:F0} deg", num2, num));
				return false;
			}
		}

		__result = TooltipInfo.Empty;
		return false;
	}
}

[HarmonyPatch]
public static class SetQualityLevelPatch
{
	static MethodBase TargetMethod()
	{
		var methodInfo = typeof(PreferencesBuilder).GetMethod(nameof(PreferencesBuilder.BuildTabGraphics), BindingFlags.NonPublic | BindingFlags.Static);
		var codes = PatchProcessor.ReadMethodBody(methodInfo);
		var instructions = codes.Select(pair => new CodeInstruction(pair.Key, pair.Value));
		var codeMatcher = new CodeMatcher(instructions)
			.MatchStartForward(new CodeMatch(OpCodes.Ldstr, "Quality"))
			.ThrowIfNotMatch("Could not find PreferencesBuilder Quality string")
			.MatchStartForward(new CodeMatch(OpCodes.Ldftn))
			.ThrowIfNotMatch("Could not find PreferencesBuilder AddDropdownIntPicker delegate")
			.Advance(1)
			.MatchStartForward(new CodeMatch(OpCodes.Ldftn))
			.ThrowIfNotMatch("Could not find PreferencesBuilder SetQualityLevel delegate");
		return (codeMatcher.Operand as MethodInfo)!;
	}

	public static bool Prefix(int value)
	{
		// Put the game's own pipeline asset back (and drop our copy) before switching quality
		// level, then let the mod make a fresh copy of the new level's asset and re-apply.
		UtilitiesMod.RestorePipelineAsset();
		QualitySettings.SetQualityLevel(value);
		UtilitiesMod.Instance?.OnGraphicsSettingsChanged();
		return false;
	}
}

