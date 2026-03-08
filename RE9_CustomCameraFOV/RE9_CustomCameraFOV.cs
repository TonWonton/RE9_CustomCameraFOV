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
		public const string VERSION = "1.4.1";

		public const string GUID_AND_V_VERSION = GUID + " v" + VERSION;

		#endregion



		/* VARIABLES */
		//Const
		public const float DEFAULT_TPS_FOV = 40f;
		public const float DEFAULT_TPS_ADS_FOV = 25f;
		public const float DEFAULT_FPS_FOV = 46f;
		public const float DEFAULT_FPS_ADS_FOV = 40f;
		public const float DEFAULT_SIGHT_FOV = 43f;

		public const float MIN_FOV = 0f;
		public const float MAX_FOV = 180f;
		public const float FOV_STEP = 0.1f;

		//Config
		private static Vector4 _colorRed = new Vector4(1f, 0.4f, 0.4f, 1f);
		private static Config _config = new Config(GUID);

		//General
		private static ConfigEntry<bool> _enabled = _config.Add("Enabled", true);

		//TPS
		private static ConfigEntry<float> _tpsFOV = _config.Add("TPS FOV", DEFAULT_TPS_FOV);
		private static ConfigEntry<float> _tpsADSFOV = _config.Add("TPS ADS FOV", DEFAULT_TPS_ADS_FOV);
		private static ConfigEntry<bool> _tpsFixedADSFOV = _config.Add("TPS Fixed ADS FOV", false);
		private static ConfigEntry<bool> _tpsForceExactFOV = _config.Add("TPS Force exact FOV", false);
		private static ConfigEntry<bool> _tpsForceExactADSFOV = _config.Add("TPS Force exact ADS FOV", false);
		private static ConfigEntry<bool> _tpsDisableADSFOVChange = _config.Add("TPS Disable ADS zoom / FOV change", false);

		//FPS
		private static ConfigEntry<float> _fpsFOV = _config.Add("FPS FOV", DEFAULT_FPS_FOV);
		private static ConfigEntry<float> _fpsADSFOV = _config.Add("FPS ADS FOV", DEFAULT_FPS_ADS_FOV);
		private static ConfigEntry<bool> _fpsFixedADSFOV = _config.Add("FPS Fixed ADS FOV", false);
		private static ConfigEntry<bool> _fpsForceExactFOV = _config.Add("FPS Force exact FOV", false);
		private static ConfigEntry<bool> _fpsForceExactADSFOV = _config.Add("FPS Force exact ADS FOV", false);
		private static ConfigEntry<bool> _fpsDisableADSFOVChange = _config.Add("FPS Disable ADS zoom / FOV change", false);

		//References
		private static InteractManager? _interactManager;
		private static CharacterManager? _characterManager;
		private static PlayerCameraFOVCalc? _playerCameraFOVCalc;
		private static ScopeCameraControllerV3? _scopeCameraControllerV3;
		private static ADSCameraController? _adsCameraController;

		//Variables
		private static Dictionary<ADSCameraZoomLogic_Base, float> _zoomLogicOriginalFOV = new Dictionary<ADSCameraZoomLogic_Base, float>();
		private static float _previousTPSFOV = 0f;
		public static float PreviousTPSFOV
		{
			get
			{
				if (_previousTPSFOV != 0f) return _previousTPSFOV;
				else return _tpsFOV.Value;
			}
		}

		private static float _previousFPSFOV = 0f;
		public static float PreviousFPSFOV
		{
			get
			{
				if (_previousFPSFOV != 0f) return _previousFPSFOV;
				else return _fpsFOV.Value;
			}
		}

		private static float _previousNewFOV = 0f;
		public static float PreviousNewFOV { get { return _previousNewFOV; } }

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
		private static float GetOriginalFOVFromZoomLogic(ADSCameraZoomLogic_Base zoomLogic)
		{
			if (_zoomLogicOriginalFOV.TryGetValue(zoomLogic, out float originalFOV) == false)
			{
				originalFOV = zoomLogic.CameraFOV;
				_zoomLogicOriginalFOV[zoomLogic] = originalFOV;
			}

			return originalFOV;
		}

		private static float GetTPSFOV(float desiredFOV)
		{
			//Calculate FOV
			float newFOV = desiredFOV;
			float tpsFOV = _tpsFOV.Value;
			float tpsADSFOV = _tpsADSFOV.Value;

			InteractManager? interactManager = _interactManager;
			bool isADS = interactManager != null && (interactManager.LimitType == InteractLimitType.Stance || interactManager.LimitType == InteractLimitType.ScopeStance);

			if (isADS)
			{
				//ADS
				if (_tpsDisableADSFOVChange.Value)
				{
					//Set forced exact or previous FOV
					if (_tpsForceExactFOV.Value) newFOV = tpsFOV; //Force exact value
					else newFOV = PreviousTPSFOV; //Set previous FOV
				}
				else
				{
					//Calculate ADS FOV
					if (_tpsForceExactADSFOV.Value) newFOV = tpsADSFOV; //Force exact ADS FOV
					else if (_tpsFixedADSFOV.Value) newFOV = desiredFOV / DEFAULT_TPS_ADS_FOV * tpsADSFOV; //Target specific FOV and scale with game
					else newFOV = desiredFOV / DEFAULT_TPS_FOV * tpsFOV; //Zoom in same percent value as the game from the configured FOV
				}
			}
			else
			{
				//Look
				if (_tpsForceExactFOV.Value) newFOV = tpsFOV; //Force exact value
				else newFOV = desiredFOV / DEFAULT_TPS_FOV * tpsFOV; //Zoom in same percent value as the game from the configured FOV
				_previousTPSFOV = newFOV;
			}

			return newFOV;
		}

		private static float GetFPSFOV(float desiredFOV)
		{
			//Calculate FOV
			float newFOV = desiredFOV;
			float fpsFOV = _fpsFOV.Value;
			float fpsADSFOV = _fpsADSFOV.Value;

			InteractManager? interactManager = _interactManager;
			bool isADS = interactManager != null && (interactManager.LimitType == InteractLimitType.Stance || interactManager.LimitType == InteractLimitType.ScopeStance);

			if (isADS)
			{
				//ADS
				if (_fpsDisableADSFOVChange.Value)
				{
					//Set forced exact or previous FOV
					if (_fpsForceExactFOV.Value) newFOV = fpsFOV; //Force exact normal FOV
					else newFOV = PreviousFPSFOV; //Set previous FOV
				}
				else
				{
					//Calculate ADS FOV
					if (_fpsForceExactADSFOV.Value) newFOV = fpsADSFOV; //Force exact ADS FOV
					else if (_fpsFixedADSFOV.Value) newFOV = desiredFOV / DEFAULT_FPS_ADS_FOV * fpsADSFOV; //Target specific FOV and scale with game
					else newFOV = desiredFOV / DEFAULT_FPS_FOV * fpsFOV; //Zoom in same percent value as the game from the configured FOV
				}
			}
			else
			{
				//Look
				if (_fpsForceExactFOV.Value) newFOV = fpsFOV; //Force exact normal FOV
				else newFOV = desiredFOV / DEFAULT_FPS_FOV * fpsFOV; //Zoom in same percent value as the game from the configured FOV
				_previousFPSFOV = newFOV;
			}

			return newFOV;
		}

		private static float GetSightFOV(float desiredFOV)
		{
			//Log.Info("Original Sight FOV: " + desiredFOV);
			float newFOV = desiredFOV;
			float fpsFOV = _fpsFOV.Value;
			float fpsADSFOV = _fpsADSFOV.Value;

			if (_fpsDisableADSFOVChange.Value)
			{
				if (_fpsForceExactFOV.Value) newFOV = fpsFOV; //Force exact normal FOV
				else newFOV = PreviousFPSFOV; //Set previous FOV
			}
			else
			{
				if (_fpsForceExactADSFOV.Value) newFOV = fpsADSFOV; //Force exact ADS FOV
				else if (_fpsFixedADSFOV.Value) newFOV = desiredFOV / DEFAULT_SIGHT_FOV * fpsADSFOV; //Target specific FOV and scale with game
				else newFOV = desiredFOV / DEFAULT_FPS_FOV * fpsFOV; //Zoom in same percent value as the game from the configured FOV
			}

			//Log.Info("New Sight FOV: " + newFOV);
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

		/* HOOKS */
		//[MethodHook(typeof(ScopeCameraControllerV3), nameof(ScopeCameraControllerV3.setDisplayScope), MethodHookType.Pre)]
		//public static PreHookResult PreScopeCameraSetupScopeInfo(Span<ulong> args)
		//{
		//	if (_scopeCameraControllerV3 == null) _scopeCameraControllerV3 = ManagedObject.ToManagedObject(args[1]).TryAs<ScopeCameraControllerV3>();
		//	if (_scopeCameraControllerV3 != null)
		//	{
		//		ScopeCameraV3ParamUserData? paramUserData = _scopeCameraControllerV3._ParamUserData;
		//		if (paramUserData != null)
		//		{
		//			paramUserData._LensImageDefaultScale = 1f;
		//			paramUserData._LensImageZoomRate = 0f;
		//		}
		//	}

		//	//_isScope = true;
		//	return PreHookResult.Continue;
		//}

		[MethodHook(typeof(ADSCameraController), nameof(ADSCameraController.updateFOV), MethodHookType.Pre)]
		public static PreHookResult PreADSCameraControllerUpdateFOV(Span<ulong> args)
		{
			if (_enabled.Value)
			{
				if (_adsCameraController == null) _adsCameraController = ManagedObject.ToManagedObject(args[1]).TryAs<ADSCameraController>();
				if (_adsCameraController != null)
				{
					//Scope zoom
					//ADSCameraController.ADSZoomData? adsZoomData = _adsCameraController.CurrentZoomData;
					//if (adsZoomData != null)
					//{
					//	int currentZoomIndex = adsZoomData._CurrentZoomIndex;
					//	int zoomStepsCount = adsZoomData._ZoomSteps.Count;

					//	for (int i = 0; i < zoomStepsCount; i++)
					//	{
					//		ADSCameraZoomData_Base.ZoomStepData zoomStepData = adsZoomData._ZoomSteps[i];
					//		if (zoomStepData != null)
					//		{
					//			//zoomStepData._ZoomFov = 11f;
					//			//Log.Info("ZoomStep " + i + ": new FOV: " + zoomStepData._ZoomFov);
					//		}
					//	}
					//}

					//Sight zoom
					var zoomLogics = _adsCameraController._ADSCameraZoomLogics;
					if (zoomLogics != null)
					{
						int zoomLogicsLength = zoomLogics.Length;

						for (int i = 0; i < zoomLogicsLength; i++)
						{
							ADSCameraZoomLogic_Base? zoomLogic = zoomLogics[i];
							if (zoomLogic != null)
							{
								float desiredFOV = GetOriginalFOVFromZoomLogic(zoomLogic);
								//Log.Info("Original ZoomLogicCameraFOV: " + i + " " + desiredFOV);
								zoomLogic.CameraFOV = GetSightFOV(desiredFOV);
								//Log.Info("New ZoomLogicCameraFOV: " + i + " " + zoomLogic.CameraFOV);
							}
							//Log.Info("New CameraFOV: " + zoomLogic.CameraFOV);

							//int currentZoomIndex = zoomLogic.CurrentZoomIndex;
							//int zoomStepsLength = zoomLogic.ZoomSteps.Length;

							//Log.Info("ZoomLogic " + i + " CurrentZoomIndex: " + currentZoomIndex);
							//Log.Info("ZoomLogic " + i + " ZoomStepsLength: " + zoomStepsLength);

							//for (int x = 0; x < zoomStepsLength; x++)
							//{
							//	ADSCameraZoomData_Base.ZoomStepData? zoomStepData = zoomLogic.ZoomSteps[x];
							//	if (zoomStepData == null) continue;

							//	Log.Info("ZoomLogic " + i + " ZoomStep " + x + ": previous FOV: " + zoomStepData._ZoomFov);
							//	zoomStepData._ZoomFov = _fpsFOV.Value;
							//	//Log.Info("ZoomLogic " + i + " ZoomStep " + x + ": new FOV: " + zoomStepData._ZoomFov);
							//}
						}
					}
				}
			}

			return PreHookResult.Continue;
		}

		[MethodHook(typeof(PlayerCameraFOVCalc), nameof(PlayerCameraFOVCalc.getFOV), MethodHookType.Pre)]
		public static PreHookResult PrePlayerCameraFOVCalcGetFOV(Span<ulong> args)
		{
			//Always update the reference since it goes stale and crashes the game when trying to reapply params to update FOV after changing config
			try { _playerCameraFOVCalc = ManagedObject.ToManagedObject(args[1]).TryAs<PlayerCameraFOVCalc>(); }
			catch { _playerCameraFOVCalc = null; }
			return PreHookResult.Continue;
		}

		[MethodHook(typeof(PlayerCameraFOVCalc), nameof(PlayerCameraFOVCalc.getFOV), MethodHookType.Post)]
		public static void PostPlayerCameraFOVCalcGetFOV(ref ulong retVal)
		{
			if (_enabled.Value)
			{
				//Get InteractManager and CharacterManager if null
				if (_interactManager == null) _interactManager = API.GetManagedSingletonT<InteractManager>();
				if (_characterManager == null) _characterManager = API.GetManagedSingletonT<CharacterManager>();

				//Get return value
				float desiredFOV = BitConverter.Int32BitsToSingle((int)(retVal & 0xFFFFFFFF));
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
				newFOV = Mathf.Clamp(newFOV, MIN_FOV, MAX_FOV);
				_previousNewFOV = newFOV;
				retVal = (retVal & 0xFFFFFFFF00000000) | (uint)BitConverter.SingleToInt32Bits(newFOV);

				//Log.Info("New FOV: " + newFOV);
			}
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

		private static void OnSettingsChanged()
		{
			_config.SaveToJson();
			ReApplyParamsToFOVCalc();
		}

		private static void RegisterConfigEvents()
		{
			_enabled.ValueChanged += OnSettingsChanged;

			_tpsFOV.ValueChanged += OnSettingsChanged;
			_tpsADSFOV.ValueChanged += OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged += OnSettingsChanged;
			_tpsForceExactFOV.ValueChanged += OnSettingsChanged;
			_tpsForceExactADSFOV.ValueChanged += OnSettingsChanged;
			_tpsDisableADSFOVChange.ValueChanged += OnSettingsChanged;

			_fpsFOV.ValueChanged += OnSettingsChanged;
			_fpsADSFOV.ValueChanged += OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged += OnSettingsChanged;
			_fpsForceExactFOV.ValueChanged += OnSettingsChanged;
			_fpsForceExactADSFOV.ValueChanged += OnSettingsChanged;
			_fpsDisableADSFOVChange.ValueChanged += OnSettingsChanged;
		}

		private static void UnregisterConfigEvents()
		{
			_enabled.ValueChanged -= OnSettingsChanged;

			_tpsFOV.ValueChanged -= OnSettingsChanged;
			_tpsADSFOV.ValueChanged -= OnSettingsChanged;
			_tpsFixedADSFOV.ValueChanged -= OnSettingsChanged;
			_tpsForceExactFOV.ValueChanged -= OnSettingsChanged;
			_tpsForceExactADSFOV.ValueChanged -= OnSettingsChanged;
			_tpsDisableADSFOVChange.ValueChanged -= OnSettingsChanged;

			_fpsFOV.ValueChanged -= OnSettingsChanged;
			_fpsADSFOV.ValueChanged -= OnSettingsChanged;
			_fpsFixedADSFOV.ValueChanged -= OnSettingsChanged;
			_fpsForceExactFOV.ValueChanged -= OnSettingsChanged;
			_fpsForceExactADSFOV.ValueChanged -= OnSettingsChanged;
			_fpsDisableADSFOVChange.ValueChanged -= OnSettingsChanged;
		}



		//[Callback(typeof(LockScene), CallbackType.Pre)]
		//public static void PreLockScene()
		//{
		//	InteractManager? interactManager = API.GetManagedSingletonT<InteractManager>();
		//	if (interactManager != null && (interactManager.LimitType == InteractLimitType.PLOneAction))
		//	{
		//		CameraSystem? cameraSystem = API.GetManagedSingletonT<CameraSystem>();
		//		if (cameraSystem != null)
		//		{
		//			Camera? camera = cameraSystem.getCameraObject(CameraDefine.Role.Main).TryGetComponent<Camera>("via.Camera");
		//			if (camera != null)
		//			{
		//				camera.FOV = 71f;
		//			}
		//		}
		//	}

		//	PauseManager? pauseManager = API.GetManagedSingletonT<PauseManager>();
		//	if (pauseManager != null && pauseManager._CurrentPauseTypes.Contains(PauseType.Cutscene))
		//	{
		//		CameraSystem? cameraSystem = API.GetManagedSingletonT<CameraSystem>();
		//		if (cameraSystem != null)
		//		{
		//			Camera? camera = cameraSystem.getCameraObject(CameraDefine.Role.Main).TryGetComponent<Camera>("via.Camera");
		//			if (camera != null)
		//			{
		//				camera.FOV = 71f;
		//			}
		//		}
		//	}
		//}



		/* PLUGIN GENERATED UI */

		[Callback(typeof(ImGuiDrawUI), CallbackType.Pre)]
		public static void PreImGuiDrawUI()
		{
			if (API.IsDrawingUI() && ImGui.TreeNode(GUID_AND_V_VERSION))
			{
				const float INDENT = 12f;
				int labelNr = 0;

				//Description
				ImGui.Text("DESCRIPTION");
				ImGui.Separator();
				ImGui.TextColored(_colorRed, "Note: FOV is vertical. 71 vertical ~= 103 horizontal @ 16:9.");
				ImGui.Spacing();

				ImGui.Text("- Force exact FOV");
				ImGui.Indent(INDENT);
				ImGui.Text("- Enabled: set FOV to the exact configured value");
				ImGui.Text("- Disabled: allow FOV to scale together with the game FOV");
				ImGui.Unindent(INDENT);
				ImGui.Spacing();

				ImGui.Text("- Fixed ADS FOV");
				ImGui.Indent(INDENT);
				ImGui.Text("- Enabled: set a target ADS FOV and allow it to scale together with the game FOV");
				ImGui.Text("- Disabled: scale ADS FOV the same percent value as the game from the configured look FOV");
				ImGui.Indent(INDENT);
				ImGui.Text("- If game decreases FOV by 20 percent then FOV will decrease by 20 percent from the configured look FOV");
				ImGui.Unindent(INDENT);
				ImGui.Unindent(INDENT);
				ImGui.Separator();
				ImGui.NewLine();

				//General
				ImGui.Text("GENERAL");
				ImGui.Separator();
				_enabled.DrawCheckbox(); _enabled.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				//TPS
				ImGui.Text("TPS");
				ImGui.Separator();
				ImGui.Spacing();

				//TPS look
				ImGui.Text("LOOK");
				_tpsForceExactFOV.DrawCheckbox(); _tpsForceExactFOV.DrawResetButtonSameLine(ref labelNr);
				_tpsFOV.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _tpsFOV.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				//TPS ADS
				ImGui.Text("ADS");
				_tpsDisableADSFOVChange.DrawCheckbox(); _tpsDisableADSFOVChange.DrawResetButtonSameLine(ref labelNr);
				bool isTPSDisableADSFOVChange = _tpsDisableADSFOVChange.Value;

				ImGui.BeginDisabled(isTPSDisableADSFOVChange); _tpsForceExactADSFOV.DrawCheckbox(); ImGui.EndDisabled();
				_tpsForceExactADSFOV.DrawResetButtonSameLine(ref labelNr);

				ImGui.BeginDisabled(isTPSDisableADSFOVChange || _tpsForceExactADSFOV.Value); _tpsFixedADSFOV.DrawCheckbox(); ImGui.EndDisabled();
				_tpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				ImGui.Spacing();

				ImGui.BeginDisabled(isTPSDisableADSFOVChange || (_tpsForceExactADSFOV.Value == false && _tpsFixedADSFOV.Value == false));
				_tpsADSFOV.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV);
				ImGui.EndDisabled();
				_tpsADSFOV.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				//FPS
				ImGui.Text("FPS");
				ImGui.Separator();
				ImGui.Spacing();

				//FPS look
				ImGui.Text("LOOK");
				_fpsForceExactFOV.DrawCheckbox(); _fpsForceExactFOV.DrawResetButtonSameLine(ref labelNr);
				_fpsFOV.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV); _fpsFOV.DrawResetButtonSameLine(ref labelNr);
				ImGui.NewLine();

				//FPS ADS
				ImGui.Text("ADS");
				_fpsDisableADSFOVChange.DrawCheckbox(); _fpsDisableADSFOVChange.DrawResetButtonSameLine(ref labelNr);
				bool isFPSDisableADSFOVChange = _fpsDisableADSFOVChange.Value;

				ImGui.BeginDisabled(isFPSDisableADSFOVChange); _fpsForceExactADSFOV.DrawCheckbox(); ImGui.EndDisabled();
				_fpsForceExactADSFOV.DrawResetButtonSameLine(ref labelNr);

				ImGui.BeginDisabled(isFPSDisableADSFOVChange || _fpsForceExactADSFOV.Value); _fpsFixedADSFOV.DrawCheckbox(); ImGui.EndDisabled();
				_fpsFixedADSFOV.DrawResetButtonSameLine(ref labelNr);
				ImGui.Spacing();

				ImGui.BeginDisabled(isFPSDisableADSFOVChange || (_fpsForceExactADSFOV.Value == false && _fpsFixedADSFOV.Value == false));
				_fpsADSFOV.DrawDragFloat(FOV_STEP, MIN_FOV, MAX_FOV);
				ImGui.EndDisabled();
				_fpsADSFOV.DrawResetButtonSameLine(ref labelNr);

				//End
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
		public static float Clamp(float value, float min, float max)
		{
			if (value < min) return min;
			else if (value > max) return max;
			else return value;
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

		public static bool DrawResetButtonSameLine(this ConfigEntryBase configEntry, ref int labelNr)
		{
			float buttonWidth = CustomCameraFOVPlugin.ResetTextSize + ImGui.GetStyle().FramePadding.X * 2;

			ImGui.SameLine(ImGui.GetContentRegionAvail().X - buttonWidth);
			bool reset = ImGui.Button(TryGetResetButtonLabel(ref labelNr));
			if (reset) configEntry.Reset();

			return reset;
		}
	}
}