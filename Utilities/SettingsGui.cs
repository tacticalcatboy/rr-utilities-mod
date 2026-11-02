using System;
using UnityEngine;

namespace Utilities;

/// <summary>
/// IMGUI drawing for the mod's settings. Replaces Unity Mod Manager's DrawFields, which
/// isn't available under the Railroader 0.6 loader.
/// </summary>
internal static class SettingsGui
{
	/// <summary>Draws all settings; applies and saves them when something changes.</summary>
	public static void Draw(UtilitiesSettings s, Action onGraphicsChanged, Action onDistanceChanged)
	{
		bool changed = false;

		changed |= Slider("Utilities window scale", ref s.UiScale, 0.5f, 3f, 1);

		if (Section("Graphics Settings", ref s.showGraphics))
		{
			var g = s.graphicsSettings;
			bool gChanged = false;
			using (new GUILayout.VerticalScope("box"))
			{
				Header("Anti-aliasing");
				float ssaa = g.SSAA;
				if (Slider("Super-Sampling AA (SSAA) <i><b>high impact</b></i>", ref ssaa, 1f, 2f, 1))
				{
					ssaa = (float)(Math.Round(ssaa * 2, MidpointRounding.AwayFromZero) / 2);
					if (ssaa != g.SSAA) { g.SSAA = ssaa; gChanged = true; }
				}
				gChanged |= EnumField("Multi-Sampling AA (MSAA)", ref g.MSAA);
				gChanged |= EnumField("Post Processing AA Mode (FXAA/SMAA)", ref g.PostProcessingAntiAliasing);
				gChanged |= EnumField("Post Processing AA Quality", ref g.PostProcessingAntiAliasingQuality);

				Header("LOD");
				gChanged |= Slider("LOD Bias", ref g.lodBias, 1f, 10f, 0);
				gChanged |= Slider("Foliage LOD Bias", ref g.lodBiasFoliage, 1f, 10f, 0);

				Header("Advanced Settings");
				gChanged |= EnumField("Depth Priming Mode", ref g.DepthPrimingMode);
			}
			if (gChanged) onGraphicsChanged();
			changed |= gChanged;
		}

		if (Section("Distance Interaction Settings", ref s.showDistance))
		{
			var d = s.distanceSettings;
			bool dChanged = false;
			using (new GUILayout.VerticalScope("box"))
			{
				Header("Switch Stand");
				dChanged |= Slider("Click Distance (Default 100)", ref d.SwitchStandDistance, 100f, 500f, 0);
				dChanged |= Slider("Click Radius (Default 0.17)", ref d.SwitchStandRadius, 0.17f, 0.5f, 2);

				Header("Flare / Fusee");
				dChanged |= Slider("Click Distance (Default 100)", ref d.FlareDistance, 100f, 500f, 0);
				dChanged |= Slider("Click Radius (Default 0.09)", ref d.FlareRadius, 0.09f, 0.5f, 2);

				Header("Coupler");
				dChanged |= Slider("Click Distance (Default 50)", ref d.CouplerDistance, 50f, 250f, 0);
				dChanged |= Slider("Click Radius (Default 0.21)", ref d.CouplerRadius, 0.21f, 0.5f, 2);

				Header("Gladhands / Hose");
				dChanged |= Slider("Click Distance (Default 30)", ref d.GladhandsDistance, 30f, 150f, 0);
				dChanged |= Slider("Click Radius (Default 0.06)", ref d.GladhandsRadius, 0.06f, 0.3f, 2);

				Header("Station Info");
				dChanged |= Slider("Click Distance (Default 50)", ref d.StationDistance, 50f, 500f, 0);
				dChanged |= Slider("Click Radius (Default 2.81)", ref d.StationRadius, 2.81f, 6f, 2);

				Header("Industry / Loader Info");
				dChanged |= Slider("Hover Distance (Default 50)", ref d.IndustryDistance, 50f, 500f, 0);

				Header("Coal Loader");
				dChanged |= Slider("Click Distance (Default 50)", ref d.CoalDistance, 50f, 250f, 0);
				dChanged |= Slider("Click Radius (Default 0.3)", ref d.CoalRadius, 0.3f, 1.2f, 2);

				Header("Water Spout");
				dChanged |= Slider("Click Distance (Default 50)", ref d.WaterDistance, 50f, 250f, 0);
				dChanged |= Slider("Click Radius (Default 0.3)", ref d.WaterRadius, 0.3f, 1.2f, 2);

				Header("Diesel Stand");
				dChanged |= Slider("Click Distance (Default 50)", ref d.DieselDistance, 50f, 250f, 0);
				dChanged |= Slider("Click Radius (Default 0.1)", ref d.DieselRadius, 0.1f, 1.2f, 2);

				Header("Query Tool");
				dChanged |= Slider("Distance (Default 100)", ref d.QueryDistance, 100f, 1500f, 0);
			}
			if (dChanged) onDistanceChanged();
			changed |= dChanged;
		}

		if (changed) s.Save();
	}

	private static bool Section(string title, ref bool shown)
	{
		using (new GUILayout.HorizontalScope())
		{
			GUILayout.Label(title, GUILayout.ExpandWidth(false));
			if (GUILayout.Button(shown ? "Hide" : "Show", GUILayout.ExpandWidth(false)))
				shown = !shown;
		}
		return shown;
	}

	private static void Header(string text)
	{
		GUILayout.Space(4);
		GUILayout.Label($"<b>{text}</b>", new GUIStyle(GUI.skin.label) { richText = true });
	}

	private static bool Slider(string label, ref float value, float min, float max, int decimals)
	{
		GUILayout.Label(label, new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });
		using (new GUILayout.HorizontalScope())
		{
			float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
			GUILayout.Label(v.ToString("F" + decimals), GUILayout.Width(UtilitiesMod.Scale(45)));
			v = (float)Math.Round(v, decimals);
			if (Mathf.Approximately(v, value)) return false;
			value = v;
			return true;
		}
	}

	private static bool EnumField<T>(string label, ref T value) where T : struct, Enum
	{
		GUILayout.Label(label);
		string[] names = Enum.GetNames(typeof(T));
		int current = Array.IndexOf(names, value.ToString());
		int selected = GUILayout.SelectionGrid(current, names, 2);
		if (selected == current || selected < 0) return false;
		value = (T)Enum.Parse(typeof(T), names[selected]);
		return true;
	}
}
