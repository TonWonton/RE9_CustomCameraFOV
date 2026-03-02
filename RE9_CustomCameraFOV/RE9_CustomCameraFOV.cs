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
using via.render.layer;


namespace RE9_CustomCameraFOV
{
	public enum PlayerCameraMode
	{
		None = 0,
		TPS = 1,
		FPS = 2,
	}

	public class CustomCameraFOVPlugin
	{
		#region PLUGIN_INFO

		/*PLUGIN INFO*/
		public const string PLUGIN_NAME = "RE9_CustomCameraFOV";
		public const string COPYRIGHT = "";
		public const string COMPANY = "https://github.com/TonWonton/RE9_CustomCameraFOV";

		public const string GUID = "RE9_CustomCameraFOV";
		public const string VERSION = "1.1.0";

		public const string GUID_AND_V_VERSION = GUID + " v" + VERSION;

		#endregion



		/* VARIABLES */
		//Const
		public const float DEFAULT_TPS_FOV = 33f;
		public const float DEFAULT_TPS_ADS_FOV = 25f;
		public const float DEFAULT_FPS_FOV = 46f;
		public const float DEFAULT_FPS_ADS_FOV = 40f;

		public const float DEFAULT_EASE_STRENGTH = 0.5f;
		public const float INTERACT_MOVE_TOWARDS_PERCENT = 0.01f;
		public const float INTERACT_MOVE_TOWARDS_AMOUNT = 0.002f;

		public const float MIN_FOV = -180f;
		public const float MAX_FOV = 360f;
		public const float FOV_STEP = 0.1f;

		//Config
		private static Vector4 _colorRed = new Vector4(1f, 0.4f, 0.4f, 1f);
		private static Config _config = new Config(GUID);

		//General
		private static ConfigEntry<bool> _enabled = _config.Add("Enabled", true);
		private static ConfigEntry<bool> _interactUseOriginalFOV = _config.Add("Use original FOV for interact", true);
		private static ConfigEntry<bool> _inspectUseOriginalFOV = _config.Add("Use original FOV for inspect", false);

		//TPS
		private static ConfigEntry<float> _tpsFov = _config.Add("TPS FOV", DEFAULT_TPS_FOV);
		private static ConfigEntry<float> _tpsFovADS = _config.Add("TPS ADS FOV", DEFAULT_TPS_ADS_FOV);
		private static ConfigEntry<bool> _tpsFixedADSFOV = _config.Add("TPS Fixed ADS FOV", false);

		//FPS
		private static ConfigEntry<float> _fpsFOV = _config.Add("FPS FOV", DEFAULT_FPS_FOV);
		private static ConfigEntry<float> _fpsFOVADS = _config.Add("FPS ADS FOV", DEFAULT_FPS_ADS_FOV);
		private static ConfigEntry<bool> _fpsFixedADSFOV = _config.Add("FPS Fixed ADS FOV", false);

		//Modifiers
		private static ConfigEntry<float> _easeStrength = _config.Add("Ease strength", DEFAULT_EASE_STRENGTH);

		//Singletons
		private static CameraSystem? _cameraSystem;
		private static PauseManager? _pauseManager;
		private static InteractManager? _interactManager;

		//Variables
		private static Camera? _camera;
		private static float _previousNewFOV = 0f;
		private static PlayerCameraMode _playerCameraMode = PlayerCameraMode.None;
		private static PlayerCameraMode _previousPlayerCameraMode = PlayerCameraMode.None;

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
		private static void SetPlayerCameraMode(PlayerCameraMode mode)
		{
			if (_playerCameraMode != mode)
			{
				_previousPlayerCameraMode = _playerCameraMode;
				_playerCameraMode = mode;
			}
		}

		private static void SetTPSFOV()
		{
			if (_enabled.Value)
			{
				//Check paused and interact first
				InteractManager? interactManager = _interactManager;
				if (_interactUseOriginalFOV.Value && interactManager != null)
				{
					PauseManager? pauseManager = _pauseManager;
					if (pauseManager != null)
					{
						//If paused due to player interaction return and let game reset FOV
						if ((pauseManager._CurrentPauseTypeBits != 0) && (interactManager.LimitType & InteractLimitType.PLOneAction) != 0) return;
					}
				}

				//Set FOV
				Camera? camera = _camera;
				if (camera != null)
				{
					//Get info
					float gameFOV = camera.FOV;
					float tpsFOV = _tpsFov.Value;
					float tpsADSFOV = _tpsFovADS.Value;
					float previousNewFOV = _previousNewFOV;
					float newFOV;

					if (_tpsFixedADSFOV.Value)
					{
						float t = (gameFOV - DEFAULT_TPS_FOV) / (DEFAULT_TPS_ADS_FOV - DEFAULT_TPS_FOV); //Linear interpolation
						t = Mathf.EaseInOutSineLinearBlend(t, _easeStrength.Value); //Sine in out ease
						newFOV = tpsFOV + t * (tpsADSFOV - tpsFOV);
					}
					else
					{
						//Calculate FOV multiplier and multiply with configured FOV so the FOV zoom percentage is the same
						newFOV = (gameFOV / DEFAULT_TPS_FOV) * tpsFOV;
					}

					if (_inspectUseOriginalFOV.Value && interactManager != null && (interactManager.LimitType & InteractLimitType.SpecialMode) != 0)
					{
						//If unpaused player interaction move FOV towards clamped to preserve original inspect FOV
						newFOV = Mathf.MoveTowardsClamped(previousNewFOV, gameFOV, previousNewFOV * INTERACT_MOVE_TOWARDS_PERCENT + INTERACT_MOVE_TOWARDS_AMOUNT);
					}

					//Set FOV
					_previousNewFOV = newFOV;
					camera.FOV = newFOV;
				}
			}
		}

		private static void SetFPSFOV()
		{
			if (_enabled.Value)
			{
				//Check paused and interact first
				InteractManager? interactManager = _interactManager;
				if (_interactUseOriginalFOV.Value && interactManager != null)
				{
					PauseManager? pauseManager = _pauseManager;
					if (pauseManager != null)
					{
						//If paused due to player interaction return and let game reset FOV
						if ((pauseManager._CurrentPauseTypeBits != 0) && (interactManager.LimitType & InteractLimitType.PLOneAction) != 0) return;
					}
				}

				//Set FOV
				Camera? camera = _camera;
				if (camera != null)
				{
					//Get camera and info
					float gameFOV = camera.FOV;
					float fpsFOV = _fpsFOV.Value;
					float fpsADSFOV = _fpsFOVADS.Value;
					float previousNewFOV = _previousNewFOV;
					float newFOV;

					if (_fpsFixedADSFOV.Value)
					{
						float t = (gameFOV - DEFAULT_FPS_FOV) / (DEFAULT_FPS_ADS_FOV - DEFAULT_FPS_FOV); //Linear interpolation
						t = Mathf.EaseInOutSineLinearBlend(t, _easeStrength.Value); //Sine in out ease
						newFOV = fpsFOV + t * (fpsADSFOV - fpsFOV);
					}
					else
					{
						//Calculate FOV multiplier and multiply with configured FOV so the FOV zoom percentage is the same
						newFOV = (gameFOV / DEFAULT_FPS_FOV) * fpsFOV;
					}

					if (_inspectUseOriginalFOV.Value && interactManager != null && (interactManager.LimitType & InteractLimitType.SpecialMode) != 0)
					{
						//If unpaused player interaction move FOV towards clamped to preserve original inspect FOV
						newFOV = Mathf.MoveTowardsClamped(previousNewFOV, gameFOV, previousNewFOV * INTERACT_MOVE_TOWARDS_PERCENT + INTERACT_MOVE_TOWARDS_AMOUNT);
					}

					//Set FOV
					_previousNewFOV = newFOV;
					camera.FOV = newFOV;
				}
			}
		}

		private static void OnSettingsChanged()
		{
			_config.SaveToJson();
		}

		private static void RegisterConfigEvents()
		{
			_enabled.ValueChanged += OnSettingsChanged;
			_interactUseOriginalFOV.ValueChanged += OnSettingsChanged;
			_inspectUseOriginalFOV.ValueChanged += OnSettingsChanged;

			_tpsFov.ValueChanged += OnSettingsChanged;
			_tpsFovADS.ValueChanged += OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged += OnSettingsChanged;

			_fpsFOV.ValueChanged += OnSettingsChanged;
			_fpsFOVADS.ValueChanged += OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged += OnSettingsChanged;

			_easeStrength.ValueChanged += OnSettingsChanged;
		}

		private static void UnregisterConfigEvents()
		{
			_enabled.ValueChanged -= OnSettingsChanged;
			_interactUseOriginalFOV.ValueChanged -= OnSettingsChanged;
			_inspectUseOriginalFOV.ValueChanged -= OnSettingsChanged;

			_tpsFov.ValueChanged -= OnSettingsChanged;
			_tpsFovADS.ValueChanged -= OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged -= OnSettingsChanged;

			_fpsFOV.ValueChanged -= OnSettingsChanged;
			_fpsFOVADS.ValueChanged -= OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged -= OnSettingsChanged;

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
		[MethodHook(typeof(TPSCameraPositionCalc), nameof(TPSCameraPositionCalc.update), MethodHookType.Post)]
		public static void PostTPSCameraPositionCalcUpdate(ref ulong ptr)
		{
			//If TPS camera is updating set PlayerCameraMode to TPS
			SetPlayerCameraMode(PlayerCameraMode.TPS);
		}

		[MethodHook(typeof(FPSCameraPositionInterpolation), nameof(FPSCameraPositionInterpolation.update), MethodHookType.Post)]
		public static void PostFPSCameraPositionCalcUpdate(ref ulong ptr)
		{
			//If FPS camera is updating set PlayerCameraMode to FPS
			SetPlayerCameraMode(PlayerCameraMode.FPS);
		}

		[Callback(typeof(BeginRendering), CallbackType.Pre)]
		public static void PreBeginRendering()
		{
			//Get CameraSystem if null
			if (_cameraSystem == null) _cameraSystem = API.GetManagedSingletonT<CameraSystem>();
			if (_cameraSystem != null)
			{
				//Get Camera if null
				if (_camera == null)
				{
					var cameraGameObject = _cameraSystem.getCameraObject(CameraDefine.Role.Main);
					if (cameraGameObject != null) _camera = cameraGameObject.TryGetComponent<Camera>("via.Camera");
				}

				//Get InteractManager and PauseManager if null
				if (_interactManager == null) _interactManager = API.GetManagedSingletonT<InteractManager>();
				if (_pauseManager == null) _pauseManager = API.GetManagedSingletonT<PauseManager>();

				//Check current PlayerCameraMode and set FOV accordingly
				PlayerCameraMode currentCameraMode = _playerCameraMode;
				if (currentCameraMode != PlayerCameraMode.None)
				{
					if (currentCameraMode == PlayerCameraMode.TPS) SetTPSFOV();
					else SetFPSFOV();
				}
				else
				{
					if (_pauseManager != null)
					{
						if (_pauseManager._CurrentPauseTypeBits != 8) //8 == cutscene, let game reset FOV
						{
							//If the game is paused (currentCameraMode == PlayerCameraMode.None) and not in cutscene set FOV to the previous PlayerCameraMode
							if (_previousPlayerCameraMode == PlayerCameraMode.TPS) SetTPSFOV();
							else if (_previousPlayerCameraMode == PlayerCameraMode.FPS) SetFPSFOV();
						}
					}
				}
			}

			//Reset PlayerCameraMode at the end or the paused FOV change might be one frame late
			SetPlayerCameraMode(PlayerCameraMode.None);
		}



		/* PLUGIN GENERATED UI */
		[Callback(typeof(ImGuiDrawUI), CallbackType.Pre)]
		public static void PreImGuiDrawUI()
		{
			if (ImGui.TreeNode(GUID_AND_V_VERSION))
			{
				int labelNr = 0;

				ImGui.TextColored(_colorRed, "Note: FOV is vertical. 71 vertical ~= 103 horizontal @ 16:9.");
				ImGui.Text("Fixed ADS FOV ENABLED = use ADS FOV value below.");
				ImGui.Text("Fixed ADS FOV DISABLED = zoom in same percent value \n"
							+ "as the game starting from the configured (not ADS) FOV.");
				ImGui.NewLine();

				ImGui.Text("GENERAL");
				ImGui.Separator();
				_enabled.DrawCheckbox(); _enabled.DrawResetButtonSameLine(ref labelNr); ImGui.Spacing();
				_interactUseOriginalFOV.DrawCheckbox(); _interactUseOriginalFOV.DrawResetButtonSameLine(ref labelNr);
				_inspectUseOriginalFOV.DrawCheckbox(); _inspectUseOriginalFOV.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				ImGui.Text("TPS");
				ImGui.Separator();
				_tpsFixedADSFOV.DrawCheckbox(); _tpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				_tpsFov.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _tpsFov.DrawResetButtonSameLine(ref labelNr);
				_tpsFovADS.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _tpsFovADS.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				ImGui.Text("FPS");
				ImGui.Separator();
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
		public static float Divide0(float a, float b)
		{
			if (b != 0f) return a / b;
			else return 0f;
		}

		public static float DivideMinMax(float a, float b)
		{
			if (b != 0f) return a / b;
			else return a < 0f ? float.MinValue : float.MaxValue;
		}

		public static float Clamp(float value, float min, float max)
		{
			if (value < min) return min;
			else if (value > max) return max;
			else return value;
		}

		public static float MoveTowardsClamped(float value, float target, float amount)
		{
			amount = MathF.Abs(amount);

			if (value < target)
			{
				value += amount;
				if (value > target) { return target; }
			}
			else if (value > target)
			{
				value -= amount;
				if (value < target) { return target; }
			}

			return value;
		}

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