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


namespace RE9_CustomCameraFOV
{
	public enum PlayerCameraMode
	{
		None = 0,
		TPS = 1,
		FPS = 2
	}

	public class CustomCameraFOVPlugin
	{
		#region PLUGIN_INFO

		/*PLUGIN INFO*/
		public const string PLUGIN_NAME = "RE9_CustomCameraFOV";
		public const string COPYRIGHT = "";
		public const string COMPANY = "https://github.com/TonWonton/RE9_CustomCameraFOV";

		public const string GUID = "RE9_CustomCameraFOV";
		public const string VERSION = "1.0.0";

		public const string GUID_AND_V_VERSION = GUID + " v" + VERSION;

		#endregion



		/* VARIABLES */
		//Const
		public const float DEFAULT_TPS_FOV = 33f;
		public const float DEFAULT_TPS_ADS_FOV = 25f;
		public const float DEFAULT_FPS_FOV = 46f;
		public const float DEFAULT_FPS_ADS_FOV = 40f;

		//Config
		private static Vector4 _colorRed = new Vector4(1f, 0.4f, 0.4f, 1f);
		private static Config _config = new Config(GUID);

		//TPS
		private static ConfigEntry<float> _tpsFov = _config.Add("TPS FOV", DEFAULT_TPS_FOV);
		private static ConfigEntry<float> _tpsFovADS = _config.Add("TPS ADS FOV", DEFAULT_TPS_ADS_FOV);
		private static ConfigEntry<bool> _tpsFixedADSFOV = _config.Add("TPS Fixed ADS FOV", false);

		//FPS
		private static ConfigEntry<float> _fpsFOV = _config.Add("FPS FOV", DEFAULT_FPS_FOV);
		private static ConfigEntry<float> _fpsFOVADS = _config.Add("FPS ADS FOV", DEFAULT_FPS_ADS_FOV);
		private static ConfigEntry<bool> _fpsFixedADSFOV = _config.Add("FPS Fixed ADS FOV", false);


		//Singletons
		private static CameraSystem? _cameraSystem;
		private static PauseManager? _pauseManager;

		//Variables
		private static Camera? _camera;
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
			if (_camera != null)
			{
				//Get camera and info
				Camera camera = _camera;
				float gameFOV = camera.FOV;

				float tpsFOV = _tpsFov.Value;
				float tpsADSFOV = _tpsFovADS.Value;

				if (_tpsFixedADSFOV.Value)
				{
					float t = (gameFOV - DEFAULT_TPS_FOV) / (DEFAULT_TPS_ADS_FOV - DEFAULT_TPS_FOV);
					camera.FOV = tpsFOV + t * (tpsADSFOV - tpsFOV);
				}
				else
				{
					camera.FOV = gameFOV / DEFAULT_TPS_FOV * tpsFOV;
				}
			}
		}

		private static void SetFPSFOV()
		{
			if (_camera != null)
			{
				//Get camera and info
				Camera camera = _camera;
				float gameFOV = camera.FOV;

				float fpsFOV = _fpsFOV.Value;
				float fpsADSFOV = _fpsFOVADS.Value;

				if (_fpsFixedADSFOV.Value)
				{
					float t = (gameFOV - DEFAULT_FPS_FOV) / (DEFAULT_FPS_ADS_FOV - DEFAULT_FPS_FOV);
					camera.FOV = fpsFOV + t * (fpsADSFOV - fpsFOV);
				}
				else
				{
					camera.FOV = gameFOV / DEFAULT_FPS_FOV * fpsFOV;
				}
			}
		}

		private static void OnSettingsChanged()
		{
			_config.SaveToJson();
		}

		private static void InitializeConfig()
		{
			_tpsFov.ValueChanged += OnSettingsChanged;
			_tpsFovADS.ValueChanged += OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged += OnSettingsChanged;

			_fpsFOV.ValueChanged += OnSettingsChanged;
			_fpsFOVADS.ValueChanged += OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged += OnSettingsChanged;
		}

		private static void Initialize()
		{
			InitializeConfig();
			_config.LoadFromJson();
		}



		/* PLUGIN LOAD */
		[PluginEntryPoint]
		protected static void Load()
		{
			Initialize();
			Log.Info("Loaded " + VERSION);
		}



		/* HOOKS */
		[MethodHook(typeof(TPSCameraPositionCalc), nameof(TPSCameraPositionCalc.update), MethodHookType.Pre)]
		public static PreHookResult PreTPSCameraPositionCalcUpdate(Span<ulong> args)
		{
			//If TPS camera is updating set PlayerCameraMode to TPS
			SetPlayerCameraMode(PlayerCameraMode.TPS);
			return PreHookResult.Continue;
		}

		[MethodHook(typeof(FPSCameraPositionInterpolation), nameof(FPSCameraPositionInterpolation.update), MethodHookType.Pre)]
		public static PreHookResult PreFPSCameraPositionCalcUpdate(Span<ulong> args)
		{
			//If FPS camera is updating set PlayerCameraMode to FPS
			SetPlayerCameraMode(PlayerCameraMode.FPS);
			return PreHookResult.Continue;
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

				//Check current PlayerCameraMode and set FOV accordingly
				PlayerCameraMode currentCameraMode = _playerCameraMode;
				if (currentCameraMode != PlayerCameraMode.None)
				{
					if (currentCameraMode == PlayerCameraMode.TPS) SetTPSFOV();
					else SetFPSFOV();
				}
				else
				{
					//Get PauseManager if null
					if (_pauseManager == null) _pauseManager = API.GetManagedSingletonT<PauseManager>();

					if (_pauseManager != null)
					{
						if (_pauseManager._CurrentPauseTypeBits != 8) //8 == cutscene, let game reset FOV
						{
							//If the game is paused and not in cutscene set FOV to the previous PlayerCameraMode
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
				ImGui.TextColored(_colorRed, "Note: FOV is vertical. 71 vertical ~= 103 horizontal @ 16:9.");
				ImGui.Text("Fixed ADS FOV ENABLED = use ADS FOV value below.");
				ImGui.Text("Fixed ADS FOV DISABLED = zoom in same percent value \n"
							+ "as the game starting from the configured (not ADS) FOV.");
				ImGui.Separator();
				ImGui.NewLine();

				int labelNr = 0;
				_tpsFixedADSFOV.DrawCheckbox(); _tpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				_tpsFov.DrawDragFloat(0.1f, 0f, 180f); _tpsFov.DrawResetButtonSameLine(ref labelNr);
				_tpsFovADS.DrawDragFloat(0.1f, 0f, 180f); _tpsFovADS.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				_fpsFixedADSFOV.DrawCheckbox(); _fpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				_fpsFOV.DrawDragFloat(0.1f, 0f, 180f); _fpsFOV.DrawResetButtonSameLine(ref labelNr);
				_fpsFOVADS.DrawDragFloat(0.1f, 0f, 180f); _fpsFOVADS.DrawResetButtonSameLine(ref labelNr);

				ImGui.TreePop();
			}
		}



		/* LOGGING */
		public static class Log
		{
			private const string PREFIX = "[" + GUID + "] ";
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