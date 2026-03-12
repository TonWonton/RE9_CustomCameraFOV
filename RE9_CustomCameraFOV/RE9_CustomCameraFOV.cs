#nullable enable
using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using REFrameworkNET.Callbacks;
using REFrameworkNET.Attributes;
using REFrameworkNET;
using REFrameworkNETPluginConfig;
using REFrameworkNETPluginConfig.Utility;
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
		public const string VERSION = "1.6.0";

		public const string GUID_AND_V_VERSION = GUID + " v" + VERSION;

		#endregion



		/* VARIABLES */
		//Const
		public const float DEFAULT_TPS_FOV = 40f;
		public const float DEFAULT_TPS_ADS_FOV = 25f;
		public const float DEFAULT_FPS_FOV = 46f;
		public const float DEFAULT_FPS_ADS_FOV = 40f;
		public const float LEON_FPS_FOV = 51f;

		public const float MIN_FOV = 0f;
		public const float MAX_FOV = 180f;
		public const float FOV_STEP = 0.1f;

		//Config
		private static Config _config = new Config(GUID);

		//General
		private static ConfigEntry<bool> _enabled = _config.Add("Enabled", true);

		//TPS
		private static ConfigEntry<float> _tpsFOV = _config.Add("TPS FOV", DEFAULT_TPS_FOV);
		private static ConfigEntry<float> _tpsADSFOV = _config.Add("TPS ADS FOV", DEFAULT_TPS_ADS_FOV);
		private static ConfigEntry<bool> _tpsForceExactADSFOV = _config.Add("TPS Force exact ADS FOV", false);
		private static ConfigEntry<bool> _tpsDisableADSFOVChange = _config.Add("TPS Disable ADS zoom / FOV change", false);

		//FPS
		private static ConfigEntry<float> _fpsFOV = _config.Add("FPS FOV", DEFAULT_FPS_FOV);
		private static ConfigEntry<float> _fpsADSFOV = _config.Add("FPS ADS FOV", DEFAULT_FPS_ADS_FOV);
		private static ConfigEntry<bool> _fpsForceExactADSFOV = _config.Add("FPS Force exact ADS FOV", false);
		private static ConfigEntry<bool> _fpsDisableADSFOVChange = _config.Add("FPS Disable ADS zoom / FOV change", false);

		//Scope
		private static ConfigEntry<float> _scopeBaseZoomMultiplier = _config.Add("Scope Base Zoom Multiplier", 2f);

		//References
		private static InteractManager? _interactManager;
		private static CharacterManager? _characterManager;
		private static PlayerCameraFOVCalc? _playerCameraFOVCalc;
		private static ScopeCameraControllerV3? _scopeCameraControllerV3;
		private static ADSCameraController? _adsCameraController;

		//Variables
		private static Dictionary<ADSCameraZoomData_Base.ZoomStepData, float> _zoomStepDataOriginalFOV = new Dictionary<ADSCameraZoomData_Base.ZoomStepData, float>();
		private static Dictionary<ADSCameraZoomLogic_Base, float> _zoomLogicOriginalFOV = new Dictionary<ADSCameraZoomLogic_Base, float>();

		private static float _previousDesiredTPSFOV = 0f;
		public static float PreviousDesiredTPSFOV
		{
			get { return _previousDesiredTPSFOV > 0f ? _previousDesiredTPSFOV : DEFAULT_TPS_FOV; }
			set { if (value > 0f) _previousDesiredTPSFOV = value; }
		}

		private static float _previousDesiredFPSFOV = 0f;
		public static float PreviousDesiredFPSFOV
		{
			get { return _previousDesiredFPSFOV > 0f ? _previousDesiredFPSFOV : DEFAULT_FPS_FOV; }
			set { if (value > 0f) _previousDesiredFPSFOV = value; }
		}



		/* METHODS */
		private static float GetTPSFOV(float desiredFOV)
		{
			//Calculate FOV
			float newFOV = desiredFOV;
			float tpsFOV = _tpsFOV.Value;
			float tpsADSFOV = _tpsADSFOV.Value;

			if (_interactManager == null) _interactManager = API.GetManagedSingletonT<InteractManager>();

			InteractManager? interactManager = _interactManager;
			if (interactManager != null)
			{
				InteractLimitType limitType = interactManager.LimitType;
				bool isADS = limitType == InteractLimitType.Stance || limitType == InteractLimitType.ScopeStance;

				if (isADS)
				{
					//ADS
					if (_tpsDisableADSFOVChange.Value)
					{
						newFOV = tpsFOV; //Set exact value
					}
					else
					{
						//Calculate ADS FOV
						if (_tpsForceExactADSFOV.Value) newFOV = tpsADSFOV; //Force exact ADS FOV
						else newFOV = desiredFOV / PreviousDesiredTPSFOV * tpsFOV; //Zoom in same percent value as the game from the configured FOV
					}
				}
				else
				{
					//Look
					newFOV = tpsFOV; //Set exact value
					PreviousDesiredTPSFOV = desiredFOV;
				}
			}

			return newFOV;
		}

		private static float GetFPSFOV(float desiredFOV)
		{
			//Calculate FOV
			float newFOV = desiredFOV;
			float fpsFOV = _fpsFOV.Value;
			float fpsADSFOV = _fpsADSFOV.Value;

			if (_interactManager == null) _interactManager = API.GetManagedSingletonT<InteractManager>();

			InteractManager? interactManager = _interactManager;
			if (interactManager != null)
			{
				InteractLimitType limitType = interactManager.LimitType;
				bool isADS = limitType == InteractLimitType.Stance || limitType == InteractLimitType.ScopeStance;

				if (isADS)
				{
					//ADS
					if (_fpsDisableADSFOVChange.Value)
					{
						newFOV = fpsFOV; //Set exact value
					}
					else
					{
						//Calculate ADS FOV
						if (_fpsForceExactADSFOV.Value) newFOV = fpsADSFOV; //Force exact ADS FOV
						else newFOV = desiredFOV / PreviousDesiredFPSFOV * fpsFOV; //Zoom in same percent value as the game from the configured FOV
					}
				}
				else
				{
					//Look
					newFOV = fpsFOV; //Set exact value
					PreviousDesiredFPSFOV = desiredFOV;
				}
			}

			return newFOV;
		}

		private static bool TryGetOriginalFOVFromZoomStepData(ADSCameraZoomData_Base.ZoomStepData zoomStepData, out float originalFOV)
		{
			if (_zoomStepDataOriginalFOV.TryGetValue(zoomStepData, out float existingOriginalFOV))
			{
				originalFOV = existingOriginalFOV;
				return true;
			}
			else
			{
				originalFOV = zoomStepData._ZoomFov;
				if (originalFOV != 0f)
				{
					_zoomStepDataOriginalFOV[zoomStepData] = originalFOV;
					return true;
				}
			}

			return false;
		}

		private static bool TryGetOriginalFOVFromZoomLogic(ADSCameraZoomLogic_Base zoomLogic, out float originalFOV)
		{
			if (_zoomLogicOriginalFOV.TryGetValue(zoomLogic, out float existingOriginalFOV))
			{
				originalFOV = existingOriginalFOV;
				return true;
			}
			else
			{
				originalFOV = zoomLogic.CameraFOV;
				if (originalFOV != 0f)
				{
					_zoomLogicOriginalFOV[zoomLogic] = originalFOV;
					return true;
				}
			}

			return false;
		}

		private static float GetScopeFOV(float desiredFOV, int zoomStep)
		{
			float zoomDivisor = _scopeBaseZoomMultiplier.Value + 0.1f * zoomStep;
			if (zoomDivisor <= 0f) zoomDivisor = 0.1f;
			return desiredFOV / zoomDivisor;
		}

		private static float GetSightFOV(float desiredFOV)
		{
			float newFOV = desiredFOV;
			float fpsFOV = _fpsFOV.Value;
			float fpsADSFOV = _fpsADSFOV.Value;

			if (_fpsDisableADSFOVChange.Value)
			{
				newFOV = fpsFOV; //Set exact value
			}
			else
			{
				if (_fpsForceExactADSFOV.Value) newFOV = fpsADSFOV; //Force exact ADS FOV
				else newFOV = desiredFOV / LEON_FPS_FOV * fpsFOV; //Zoom in same percent value as the game from the configured FOV
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

		/* HOOKS */
		[MethodHook(typeof(ScopeCameraControllerV3), nameof(ScopeCameraControllerV3.setDisplayScope), MethodHookType.Pre)]
		public static PreHookResult PreScopeCameraSetupScopeInfo(Span<ulong> args)
		{
			if (_scopeCameraControllerV3 == null) _scopeCameraControllerV3 = ManagedObject.ToManagedObject(args[1]).TryAs<ScopeCameraControllerV3>();
			if (_scopeCameraControllerV3 != null)
			{
				ScopeCameraV3ParamUserData? paramUserData = _scopeCameraControllerV3._ParamUserData;
				if (paramUserData != null)
				{
					if (_enabled.Value)
					{
						//Set the default image scale to 1x and set zoom rate to 0
						paramUserData._LensImageDefaultScale = 1f;
						paramUserData._LensImageZoomRate = 0f;
					}
					else
					{
						//Reset to default values
						paramUserData._LensImageDefaultScale = 2f;
						paramUserData._LensImageZoomRate = 0.1f;
					}
				}
			}

			//_isScope = true;
			return PreHookResult.Continue;
		}

		[MethodHook(typeof(ADSCameraController), nameof(ADSCameraController.updateFOV), MethodHookType.Pre)]
		public static PreHookResult PreADSCameraControllerUpdateFOV(Span<ulong> args)
		{
			bool isEnabled = _enabled.Value;

			if (_adsCameraController == null) _adsCameraController = ManagedObject.ToManagedObject(args[1]).TryAs<ADSCameraController>();
			if (_adsCameraController != null)
			{
				//Scope zoom
				ADSCameraController.ADSZoomData? adsZoomData = _adsCameraController.CurrentZoomData;
				if (adsZoomData != null)
				{
					int zoomStepsCount = adsZoomData._ZoomSteps.Count;
					for (int i = 0; i < zoomStepsCount; i++)
					{
						ADSCameraZoomData_Base.ZoomStepData zoomStepData = adsZoomData._ZoomSteps[i];
						if (zoomStepData != null)
						{
							if (TryGetOriginalFOVFromZoomStepData(zoomStepData, out float originalFOV))
							{
								zoomStepData._ZoomFov = isEnabled ? GetScopeFOV(originalFOV, i) : originalFOV;
							}
						}
					}
				}

				//Sight zoom
				ADSCameraZoomLogic_Base_Array1D? zoomLogics = _adsCameraController._ADSCameraZoomLogics;
				if (zoomLogics != null)
				{
					int zoomLogicsLength = zoomLogics.Length;
					for (int i = 0; i < zoomLogicsLength; i++)
					{
						ADSCameraZoomLogic_Base? zoomLogic = zoomLogics[i];
						if (zoomLogic != null)
						{
							if (TryGetOriginalFOVFromZoomLogic(zoomLogic, out float originalFOV))
							{
								zoomLogic.CameraFOV = isEnabled ? GetSightFOV(originalFOV) : originalFOV;
							}
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
				//Get CharacterManager if null

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
				retVal = (retVal & 0xFFFFFFFF00000000) | (uint)BitConverter.SingleToInt32Bits(newFOV);

				//Log.Info("New FOV: " + newFOV);
			}
		}



		/* EVENT HANDLING */
		private static void OnSettingsChanged()
		{
			_config.SaveToJson();
			ReApplyParamsToFOVCalc();
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

		private static void RegisterConfigEvents()
		{
			foreach (ConfigEntryBase configEntry in _config.Values)
			{
				configEntry.ValueChanged += OnSettingsChanged;
			}
		}

		private static void UnregisterConfigEvents()
		{
			foreach (ConfigEntryBase configEntry in _config.Values)
			{
				configEntry.ValueChanged -= OnSettingsChanged;
			}
		}



		/* PLUGIN GENERATED UI */

		[Callback(typeof(ImGuiDrawUI), CallbackType.Pre)]
		public static void PreImGuiDrawUI()
		{
			if (API.IsDrawingUI() && ImGui.TreeNode(GUID_AND_V_VERSION))
			{
				int labelNr = 0;

				//General
				ImGuiF.Category("GENERAL");
				_enabled.Checkbox().ResetButton(ref labelNr);

				//TPS
				ImGuiF.Category("TPS");
				_tpsFOV.DragFloat(FOV_STEP, MIN_FOV, MAX_FOV).ResetButton(ref labelNr);

				//TPS ADS
				ImGui.Spacing();
				_tpsDisableADSFOVChange.Checkbox().ResetButton(ref labelNr).GetValue(out bool isTPSDisableADSFOVChange);
				_tpsForceExactADSFOV.BeginDisabled(isTPSDisableADSFOVChange).Checkbox().EndDisabled().ResetButton(ref labelNr).GetValue(out bool isTPSForceExactADSFOV);
				_tpsADSFOV.BeginDisabled(isTPSDisableADSFOVChange || isTPSForceExactADSFOV == false).DragFloat(FOV_STEP, MIN_FOV, MAX_FOV).EndDisabled().ResetButton(ref labelNr);

				//FPS
				ImGuiF.Category("FPS");
				_fpsFOV.DragFloat(FOV_STEP, MIN_FOV, MAX_FOV).ResetButton(ref labelNr);

				//FPS ADS
				ImGui.Spacing();
				_fpsDisableADSFOVChange.Checkbox().ResetButton(ref labelNr).GetValue(out bool isFPSDisableADSFOVChange);
				_fpsForceExactADSFOV.BeginDisabled(isFPSDisableADSFOVChange).Checkbox().EndDisabled().ResetButton(ref labelNr).GetValue(out bool isFPSForceExactADSFOV);
				_fpsADSFOV.BeginDisabled(isFPSDisableADSFOVChange || isFPSForceExactADSFOV == false).DragFloat(FOV_STEP, MIN_FOV, MAX_FOV).EndDisabled().ResetButton(ref labelNr);

				//Scope
				ImGuiF.Category("SCOPE");
				_scopeBaseZoomMultiplier.DragFloat(0.01f, 0f, 10f).ResetButton(ref labelNr);

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
}