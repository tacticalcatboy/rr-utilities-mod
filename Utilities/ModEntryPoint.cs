using HarmonyLib;
using Railroader.Modding;
using System;
using UnityEngine;

namespace Utilities;

/// <summary>
/// Railroader 0.6 native mod entry point (replaces the old Unity Mod Manager loader).
/// The game constructs this class, calls OnModLoad at boot, and OnWorldLoad / OnWorldUnload
/// whenever a save is loaded or left. It may unload and reload the mod when a save with a
/// different mod set is loaded, so all static state is reset in OnModLoad.
/// </summary>
public sealed class ModEntryPoint : IModEntryPoint, IWorldLifecycle
{
	private static IModContext? _context;
	private static Harmony? _harmony;
	private static GameObject? _root;

	internal static UtilitiesSettings Settings { get; private set; } = new UtilitiesSettings();

	public void OnModLoad(IModContext context)
	{
		_context = context;
		Settings = UtilitiesSettings.Load(context.ModDirectory);

		Log($"Loaded (game {Application.version}, Unity {Application.unityVersion})");

		_harmony = new Harmony(context.ModIdentifier);
		ApplyPatches(_harmony);

		_root = new GameObject("[UtilitiesMod]");
		UnityEngine.Object.DontDestroyOnLoad(_root);
		_root.AddComponent<UtilitiesMod>();
		InputSystemBlocker.CreateInputSystemBlocker(_root.transform);
	}

	public void OnModUnload()
	{
		OnWorldUnload();

		if (_root != null)
		{
			UnityEngine.Object.Destroy(_root);
			_root = null;
		}

		if (_harmony != null && _context != null)
			_harmony.UnpatchAll(_context.ModIdentifier);
		_harmony = null;

		Settings.Save();
		Log("Unloaded");
		_context = null;
	}

	public void OnWorldLoad()
	{
		UtilitiesMod.Instance?.OnWorldLoad();
	}

	public void OnWorldUnload()
	{
		UtilitiesMod.Instance?.OnWorldUnload();
	}

	/// <summary>
	/// Each patch class is applied on its own so a target that moved in a future game
	/// build only disables that one feature instead of the whole mod.
	/// </summary>
	private static void ApplyPatches(Harmony harmony)
	{
		int ok = 0, failed = 0;
		Type?[] types;
		try
		{
			types = typeof(ModEntryPoint).Assembly.GetTypes();
		}
		catch (System.Reflection.ReflectionTypeLoadException ex)
		{
			// Some of our types reference game types that no longer exist; patch the rest
			types = ex.Types;
		}

		foreach (var type in types)
		{
			if (type == null) continue;
			if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0) continue;
			try
			{
				harmony.CreateClassProcessor(type).Patch();
				ok++;
			}
			catch (Exception ex)
			{
				failed++;
				Warning($"Patch {type.Name} could not be applied and is disabled: {ex.GetBaseException().Message}");
			}
		}
		Log($"Harmony: {ok} patches applied, {failed} skipped");
	}

	internal static void Log(string message) => _context?.Log.Info($"[Utilities] {message}");

	internal static void Warning(string message) => _context?.Log.Warning($"[Utilities] {message}");

	internal static void LogDebug(string message)
	{
#if DEBUG
		Log(message);
#endif
	}
}
