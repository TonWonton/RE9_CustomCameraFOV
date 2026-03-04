#nullable enable
using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using REFrameworkNET.Callbacks;
using REFrameworkNET.Attributes;
using REFrameworkNET;
using REFrameworkNETPluginConfig;
using app;
using via;
using System.Numerics;
using InteractLimitType = app.InteractManager.InteractLimitType;
using via.audiorender;
using via.landscape.spline;


namespace RE9_CustomCameraFOV
{
	public class CustomCameraFOVPlugin
	{
		#region PLUGIN_INFO

		/*PLUGIN INFO*/
		public const string PLUGIN_NAME = "RE9_CustomCameraFOV";
		public const string COPYRIGHT = "";
		public const string COMPANY = "https://github.com/TonWonton/RE9_CustomCameraFOV";

		public const string GUID = "RE9_CustomCameraFOV";
		public const string VERSION = "1.3.0";

		public const string GUID_AND_V_VERSION = GUID + " v" + VERSION;

		#endregion



		/* VARIABLES */
		//Const
		public const float DEFAULT_TPS_FOV = 40f;
		public const float DEFAULT_TPS_ADS_FOV = 25f;
		public const float DEFAULT_FPS_FOV = 46f;
		public const float DEFAULT_FPS_ADS_FOV = 40f;
		public const float DEFAULT_EASE_STRENGTH = 0.5f;

		public const float MIN_FOV = -180f;
		public const float MAX_FOV = 360f;
		public const float FOV_STEP = 0.1f;

		//Config
		private static Vector4 _colorRed = new Vector4(1f, 0.4f, 0.4f, 1f);
		private static Config _config = new Config(GUID);

		//General
		private static ConfigEntry<bool> _enabled = _config.Add("Enabled", true);
		//private static ConfigEntry<bool> _interactUseOriginalFOV = _config.Add("Use original FOV for interact", true);
		//private static ConfigEntry<bool> _inspectUseOriginalFOV = _config.Add("Use original FOV for inspect", false);

		//TPS
		private static ConfigEntry<float> _tpsFov = _config.Add("TPS FOV", DEFAULT_TPS_FOV);
		private static ConfigEntry<float> _tpsFovADS = _config.Add("TPS ADS FOV", DEFAULT_TPS_ADS_FOV);
		private static ConfigEntry<bool> _tpsFixedADSFOV = _config.Add("TPS Fixed ADS FOV", false);
		private static ConfigEntry<bool> _tpsForceExactFOV = _config.Add("TPS Force exact FOV", false);

		//FPS
		private static ConfigEntry<float> _fpsFOV = _config.Add("FPS FOV", DEFAULT_FPS_FOV);
		private static ConfigEntry<float> _fpsFOVADS = _config.Add("FPS ADS FOV", DEFAULT_FPS_ADS_FOV);
		private static ConfigEntry<bool> _fpsFixedADSFOV = _config.Add("FPS Fixed ADS FOV", false);
		private static ConfigEntry<bool> _fpsForceExactFOV = _config.Add("FPS Force exact FOV", false);

		//Modifiers
		private static ConfigEntry<float> _easeStrength = _config.Add("Ease strength", DEFAULT_EASE_STRENGTH);

		//Singletons
		private static InteractManager? _interactManager;
		private static CharacterManager? _characterManager;

		//Variables
		private static PlayerCameraFOVCalc? _playerCameraFOVCalc;
		private static float _resetTextSize = 0f;
		public static float ResetTextSize
		{
			get
			{
				if (_resetTextSize != 0f) return _resetTextSize;
				else return _resetTextSize = ImGui.CalcTextSize("Reset").X;
			}
		}



		/* METHODS */
		private static float GetTPSFOV(float desiredFOV)
		{
			//Calculate FOV
			float newFOV = desiredFOV;
			float tpsFOV = _tpsFov.Value;
			float tpsADSFOV = _tpsFovADS.Value;
			bool isADS = _interactManager != null && (_interactManager.LimitType == InteractLimitType.Stance || _interactManager.LimitType == InteractLimitType.ScopeStance);

			if (_tpsForceExactFOV.Value)
			{
				if (isADS) newFOV = tpsADSFOV;
				else newFOV = tpsFOV;
			}
			else
			{
				if (_tpsFixedADSFOV.Value)
				{
					if (isADS)
					{
						//Scale with ADS FOV
						float t = (desiredFOV - DEFAULT_TPS_FOV) / (DEFAULT_TPS_ADS_FOV - DEFAULT_TPS_FOV);
						t = Mathf.EaseInOutSineLinearBlend(t, _easeStrength.Value);
						newFOV = tpsFOV + t * (tpsADSFOV - tpsFOV);
					}
					else
					{
						//Scale with normal FOV
						newFOV = desiredFOV / DEFAULT_TPS_FOV * tpsFOV;
					}
				}
				else
				{
					//Scale with normal FOV
					newFOV = desiredFOV / DEFAULT_TPS_FOV * tpsFOV;
				}
			}

			return newFOV;
		}

		private static float GetFPSFOV(float desiredFOV)
		{
			//Calculate FOV
			float newFOV = desiredFOV;
			float fpsFOV = _fpsFOV.Value;
			float fpsADSFOV = _fpsFOVADS.Value;
			bool isADS = _interactManager != null && (_interactManager.LimitType == InteractLimitType.Stance || _interactManager.LimitType == InteractLimitType.ScopeStance);

			if (_fpsForceExactFOV.Value)
			{
				if (isADS) newFOV = fpsADSFOV;
				else newFOV = fpsFOV;
			}
			else
			{
				if (_fpsFixedADSFOV.Value)
				{
					if (isADS)
					{
						//Scale with ADS FOV
						float t = (desiredFOV - DEFAULT_FPS_FOV) / (DEFAULT_FPS_ADS_FOV - DEFAULT_FPS_FOV); //Linear interpolation
						t = Mathf.EaseInOutSineLinearBlend(t, _easeStrength.Value); //Sine in out ease
						newFOV = fpsFOV + t * (fpsADSFOV - fpsFOV);
					}
					else
					{
						//Scale with normal FOV
						newFOV = desiredFOV / DEFAULT_FPS_FOV * fpsFOV;
					}
				}
				else
				{
					//Scale with normal FOV
					newFOV = desiredFOV / DEFAULT_FPS_FOV * fpsFOV;
				}
			}

			return newFOV;
		}

		private static void ReApplyParamsToFOVCalc()
		{
			try
			{
				if (_playerCameraFOVCalc != null)
				{
					var param = _playerCameraFOVCalc._Param;
					if (param != null)
					{
						_playerCameraFOVCalc.setup(param);
					}
				}
			}
			catch { }
		}

		private static void OnSettingsChanged()
		{
			_config.SaveToJson();
			ReApplyParamsToFOVCalc();
		}

		private static void RegisterConfigEvents()
		{
			_enabled.ValueChanged += OnSettingsChanged;

			_tpsFov.ValueChanged += OnSettingsChanged;
			_tpsFovADS.ValueChanged += OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged += OnSettingsChanged;
			_tpsForceExactFOV.ValueChanged += OnSettingsChanged;

			_fpsFOV.ValueChanged += OnSettingsChanged;
			_fpsFOVADS.ValueChanged += OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged += OnSettingsChanged;
			_fpsForceExactFOV.ValueChanged += OnSettingsChanged;

			_easeStrength.ValueChanged += OnSettingsChanged;
		}

		private static void UnregisterConfigEvents()
		{
			_enabled.ValueChanged -= OnSettingsChanged;

			_tpsFov.ValueChanged -= OnSettingsChanged;
			_tpsFovADS.ValueChanged -= OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged -= OnSettingsChanged;
			_tpsForceExactFOV.ValueChanged -= OnSettingsChanged;

			_fpsFOV.ValueChanged -= OnSettingsChanged;
			_fpsFOVADS.ValueChanged -= OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged -= OnSettingsChanged;
			_fpsForceExactFOV.ValueChanged -= OnSettingsChanged;

			_easeStrength.ValueChanged -= OnSettingsChanged;
		}



		/* PLUGIN LOAD */
		[PluginEntryPoint]
		private static void Load()
		{
			RegisterConfigEvents();
			_config.LoadFromJson();
			Log.Info("Loaded " + VERSION);
		}

		[PluginExitPoint]
		private static void Unload()
		{
			UnregisterConfigEvents();
			Log.Info("Unloaded " + VERSION);
		}



		/* HOOKS */
		[MethodHook(typeof(PlayerCameraFOVCalc), nameof(PlayerCameraFOVCalc.getFOV), MethodHookType.Pre)]
		public static PreHookResult PrePlayerCameraFOVCalcGetFOV(Span<ulong> args)
		{
			try { _playerCameraFOVCalc = ManagedObject.ToManagedObject(args[1]).TryAs<PlayerCameraFOVCalc>(); }
			catch { _playerCameraFOVCalc = null; }
			return PreHookResult.Continue;
		}

		[MethodHook(typeof(PlayerCameraFOVCalc), nameof(PlayerCameraFOVCalc.getFOV), MethodHookType.Post)]
		public static void PostPlayerCameraFOVCalcGetFOV(ref ulong ptr)
		{
			if (_enabled.Value)
			{
				//Get InteractManager and CharacterManager if null
				if (_interactManager == null) _interactManager = API.GetManagedSingletonT<InteractManager>();
				if (_characterManager == null) _characterManager = API.GetManagedSingletonT<CharacterManager>();

				//Get return value
				float desiredFOV = BitConverter.Int32BitsToSingle((int)(ptr & 0xFFFFFFFF));
				float newFOV = desiredFOV;
				//Log.Info("Original FOV: " + desiredFOV);

				//Check current PlayerCameraMode and set FOV accordingly
				if (_characterManager != null && _characterManager.PlayerContextFast != null)
				{
					PlayerMode currentViewMode = _characterManager.PlayerContextFast.CurrentViewMode;
					if (currentViewMode == PlayerMode.TPS) newFOV = GetTPSFOV(desiredFOV);
					else if (currentViewMode == PlayerMode.FPS) newFOV = GetFPSFOV(desiredFOV);
				}

				//Set new FOV
				ptr = (ptr & 0xFFFFFFFF00000000) | (uint)BitConverter.SingleToInt32Bits(newFOV);

				//Log.Info("New FOV: " + newFOV);
			}
		}



		/* PLUGIN GENERATED UI */
		[Callback(typeof(ImGuiDrawUI), CallbackType.Pre)]
		public static void PreImGuiDrawUI()
		{
			if (ImGui.TreeNode(GUID_AND_V_VERSION))
			{
				int labelNr = 0;

				ImGui.TextColored(_colorRed, "Note: FOV is vertical. 71 vertical ~= 103 horizontal @ 16:9.");
				ImGui.Separator();

				ImGui.Text("- Force exact FOV ENABLED = use exact FOV values and don't \n"
							+ "scale together with game FOV.");
				ImGui.Text("- Force exact FOV DISABLED = allow configured FOV to scale \n"
							+ "together with game FOV.");
				ImGui.Separator();

				ImGui.Text("- Fixed ADS FOV ENABLED = use ADS FOV value below.");
				ImGui.Text("- Fixed ADS FOV DISABLED = zoom in same percent value \n"
							+ "as the game starting from the configured (not ADS) FOV.");
				ImGui.Separator();
				ImGui.NewLine();

				ImGui.Text("GENERAL");
				ImGui.Separator();
				_enabled.DrawCheckbox(); _enabled.DrawResetButtonSameLine(ref labelNr); ImGui.Spacing();
				_easeStrength.DrawDragFloat(0.01f, 0f, 1f); _easeStrength.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				ImGui.Text("TPS");
				ImGui.Separator();
				_tpsForceExactFOV.DrawCheckbox(); _tpsForceExactFOV.DrawResetButtonSameLine(ref labelNr);
				_tpsFixedADSFOV.DrawCheckbox(); _tpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				_tpsFov.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _tpsFov.DrawResetButtonSameLine(ref labelNr);
				_tpsFovADS.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _tpsFovADS.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				ImGui.Text("FPS");
				ImGui.Separator();
				_fpsForceExactFOV.DrawCheckbox(); _fpsForceExactFOV.DrawResetButtonSameLine(ref labelNr);
				_fpsFixedADSFOV.DrawCheckbox(); _fpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				_fpsFOV.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _fpsFOV.DrawResetButtonSameLine(ref labelNr);
				_fpsFOVADS.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _fpsFOVADS.DrawResetButtonSameLine(ref labelNr);

				ImGui.TreePop();
			}
		}
	}

	public static class Extensions
	{
		public static T? TryGetComponent<T>(this GameObject gameObject, string typeName) where T : class
		{
			_System.Type? type = _System.Type.GetType(typeName);
			if (type != null)
			{
				Component? componentFromGameObject = gameObject.getComponent(type);
				if (componentFromGameObject != null)
				{
					if (componentFromGameObject is IObject componentIObject)
					{
						return componentIObject.TryAs<T>();
					}
				}
			}

			return null;
		}
	}

	public static class Mathf
	{
		public static float EaseInOutSineLinearBlend(float t, float k)
		{
			if (t <= 0f) return (1f - k) * t;
			if (t >= 1f) return 1f + (1f - k) * (t - 1f);
			return t + k * ((1f - MathF.Cos(MathF.PI * t)) * 0.5f - t);
		}
	}

	public static class Log
	{
		private const string PREFIX = "[" + CustomCameraFOVPlugin.GUID + "] ";
		public static void Info(string message)
		{
			API.LogInfo(PREFIX + message);
		}

		public static void Warning(string message)
		{
			API.LogWarning(PREFIX + message);
		}

		public static void Error(string message)
		{
			API.LogError(PREFIX + message);
		}
	}

	internal static class ImGuiExtensions
	{
		private static Dictionary<int, string> _resetButtonLabels = new Dictionary<int, string>();

		private static string TryGetResetButtonLabel(ref int labelNr)
		{
			if (_resetButtonLabels.TryGetValue(labelNr, out string? label) == false)
			{
				label = "Reset##" + labelNr;
				_resetButtonLabels[labelNr] = label;
			}

			labelNr++;
			return label;
		}

		public static bool DrawCheckbox(this ConfigEntry<bool> configEntry)
		{
			bool changed = ImGui.Checkbox(configEntry.Key, ref configEntry.RefValue);
			if (changed) configEntry.NotifyValueChanged();

			return changed;
		}

		public static bool DrawDragFloat(this ConfigEntry<float> configEntry, float vSpeed, float vMin, float vMax)
		{
			bool changed = ImGui.DragFloat(configEntry.Key, ref configEntry.RefValue, vSpeed, vMin, vMax);
			if (changed) configEntry.NotifyValueChanged();

			return changed;
		}

		public static bool DrawResetButtonSameLine(this ConfigEntry<bool> configEntry, ref int labelNr)
		{
			float buttonWidth = CustomCameraFOVPlugin.ResetTextSize + ImGui.GetStyle().FramePadding.X * 2;

			ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth);
			bool reset = ImGui.Button(TryGetResetButtonLabel(ref labelNr));
			if (reset) configEntry.Reset();

			return reset;
		}

		public static bool DrawResetButtonSameLine(this ConfigEntry<float> configEntry, ref int labelNr)
		{
			float buttonWidth = CustomCameraFOVPlugin.ResetTextSize + ImGui.GetStyle().FramePadding.X * 2;

			ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth);
			bool reset = ImGui.Button(TryGetResetButtonLabel(ref labelNr));
			if (reset) configEntry.Reset();

			return reset;
		}
	}
}