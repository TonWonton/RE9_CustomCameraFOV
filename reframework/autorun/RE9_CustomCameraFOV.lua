--SCRIPT INFO
local s_GUID = "RE9_CustomCameraFOV"
local s_version = "1.6.1"

local s_GUIDAndVVersion = s_GUID .. " v" .. s_version
local s_logPrefix = "[" .. s_GUID .. "] "
local s_configFileName = s_GUID .. ".lua.json"



--UTILITY
local function GenerateEnum(typename, double_ended)
    local double_ended = double_ended or false

    local t = sdk.find_type_definition(typename)
    if not t then return {} end

    local fields = t:get_fields()
    local enum = {}

    for i, field in ipairs(fields) do
        if field:is_static() then
            local name = field:get_name()
            local raw_value = field:get_data(nil)

            --log.info(name .. " = " .. tostring(raw_value))

            enum[name] = raw_value

            if double_ended then
                enum[raw_value] = name
            end
        end
    end

    return enum
end



--LOG
local function LogInfo(message)
	log.info(s_logPrefix .. message)
end



--CONST
local f_defaultTPSFOV = 40.0
local f_defaultTPSADSFOV = 25.0
local f_defaultFPSFOV = 46.0
local f_defaultFPSADSFOV = 40.0
local f_leonFPSFOV = 51.0



--CONFIG
local tbl_config =
{
	--General
	b_enabled = true,

	--TPS
	f_tpsFOV = f_defaultTPSFOV,
	f_tpsADSFOV = f_defaultTPSADSFOV,
	b_tpsForceExactADSFOV = false,
	b_tpsDisableADSFOVChange = false,

	--FPS
	f_fpsFOV = f_defaultFPSFOV,
	f_fpsADSFOV = f_defaultFPSADSFOV,
	b_fpsForceExactADSFOV = false,
	b_fpsDisableADSFOVChange = false,
	
	f_scopeBaseZoomMultiplier = 2.0
}

local function LoadFromJson()
	local tblLoadedConfig = json.load_file(s_configFileName)

	if tblLoadedConfig ~= nil then
        for key, val in pairs(tblLoadedConfig) do
            tbl_config[key] = val
        end
    end
end

local function SaveToJson()
	json.dump_file(s_configFileName, tbl_config)
end



--VARIABLES
local c_interactManager = nil
local c_characterManager = nil
local c_scopeCameraControllerV3 = nil
local c_adsCameraController = nil
local ul_playerCameraFOVCalcAddress = nil
local tbl_zoomStepDataOriginalFOV = {}
local tbl_zoomLogicOriginalFOV = {}

local f_previousDesiredTPSFOV = 0.0
local function GetPreviousDesiredTPSFOV()
	if f_previousDesiredTPSFOV > 0.0 then return f_previousDesiredTPSFOV
	else return f_defaultTPSFOV end
end
local function SetPreviousDesiredTPSFOV(fValue)
	if fValue > 0.0 then f_previousDesiredTPSFOV = fValue end
end

local f_previousDesiredFPSFOV = 0.0
local function GetPreviousDesiredFPSFOV()
	if f_previousDesiredFPSFOV > 0.0 then return f_previousDesiredFPSFOV
	else return f_defaultFPSFOV end
end
local function SetPreviousDesiredFPSFOV(fValue)
	if fValue > 0.0 then f_previousDesiredFPSFOV = fValue end
end

local tef_limitType = nil
local te_playerMode = nil
local b_initialized = false



--FUNCTIONS
local function TryGetInteractManager()
	local cInteractManager = c_interactManager
	if cInteractManager ~= nil then
		return cInteractManager
	else
		cInteractManager = sdk.get_managed_singleton("app.InteractManager")
		if cInteractManager ~= nil then
			c_interactManager = cInteractManager
			return cInteractManager
		end
	end

	return nil
end

local function TryGetCharacterManager()
	local cCharacterManager = c_characterManager
	if cCharacterManager ~= nil then
		return cCharacterManager
	else
		cCharacterManager = sdk.get_managed_singleton("app.CharacterManager")
		if cCharacterManager ~= nil then
			c_characterManager = cCharacterManager
			return cCharacterManager
		end
	end

	return nil
end

local function GetTPSFOV(fDesiredFOV)
	local fNewFOV = fDesiredFOV
	local tblConfig = tbl_config

	local cInteractManager = TryGetInteractManager()
	if cInteractManager ~= nil then
		local tefLimitType = tef_limitType
		local efLimitType = cInteractManager._LimitType
		local bIsADS = (efLimitType & tefLimitType.Stance) ~= 0 or (efLimitType & tefLimitType.ScopeStance) ~= 0

		if (bIsADS) then
			if tblConfig.b_tpsDisableADSFOVChange then fNewFOV = tblConfig.f_tpsFOV
			elseif tblConfig.b_tpsForceExactADSFOV then fNewFOV = tblConfig.f_tpsADSFOV
			else fNewFOV = fDesiredFOV / GetPreviousDesiredTPSFOV() * tblConfig.f_tpsFOV end
		else
			fNewFOV = tblConfig.f_tpsFOV
			SetPreviousDesiredTPSFOV(fDesiredFOV)
		end
	end

	return fNewFOV
end

local function GetFPSFOV(fDesiredFOV)
	local fNewFOV = fDesiredFOV
	local tblConfig = tbl_config

	local cInteractManager = TryGetInteractManager()
	if cInteractManager ~= nil then
		local tefLimitType = tef_limitType
		local efLimitType = cInteractManager._LimitType
		local bIsADS = (efLimitType & tefLimitType.Stance) ~= 0 or (efLimitType & tefLimitType.ScopeStance) ~= 0

		if (bIsADS) then
			if tblConfig.b_fpsDisableADSFOVChange then fNewFOV = tblConfig.f_fpsFOV
			elseif tblConfig.b_fpsForceExactADSFOV then fNewFOV = tblConfig.f_fpsADSFOV
			else fNewFOV = fDesiredFOV / GetPreviousDesiredFPSFOV() * tblConfig.f_fpsFOV end
		else
			fNewFOV = tblConfig.f_fpsFOV
			SetPreviousDesiredFPSFOV(fDesiredFOV)
		end
	end

	return fNewFOV
end

local function TryGetOriginalFOVFromZoomStepData(cZoomStepData)
	local tblZoomStepDataOriginalFOV = tbl_zoomStepDataOriginalFOV

	local fOriginalFOV = tblZoomStepDataOriginalFOV[cZoomStepData:get_address()]
	if fOriginalFOV ~= nil then
		return fOriginalFOV
	else
		fOriginalFOV = cZoomStepData._ZoomFov
		if fOriginalFOV ~= nil and fOriginalFOV ~= 0.0 then
			tblZoomStepDataOriginalFOV[cZoomStepData:get_address()] = fOriginalFOV
			return fOriginalFOV
		end
	end

	return nil
end

local function TryGetOriginalFOVFromZoomLogic(cZoomLogic)
	local tblZoomLogicOriginalFOV = tbl_zoomLogicOriginalFOV

	local fOriginalFOV = tblZoomLogicOriginalFOV[cZoomLogic:get_address()]
	if fOriginalFOV ~= nil then
		return fOriginalFOV
	else
		fOriginalFOV = cZoomLogic:call("get_CameraFOV")
		if fOriginalFOV ~= nil and fOriginalFOV ~= 0.0 then
			tblZoomLogicOriginalFOV[cZoomLogic:get_address()] = fOriginalFOV
			return fOriginalFOV
		end
	end

	return nil
end

local function GetScopeFOV(fDesiredFOV, iZoomStep)
	local fZoomDivisor = tbl_config.f_scopeBaseZoomMultiplier + 0.1 * iZoomStep
	if fZoomDivisor <= 0.0 then fZoomDivisor = 0.1 end
	return fDesiredFOV / fZoomDivisor
end

local function GetSightFOV(fDesiredFOV)
	local fNewFOV = fDesiredFOV
	local tblConfig = tbl_config

	if tblConfig.b_fpsDisableADSFOVChange then fNewFOV = tblConfig.f_fpsFOV
	elseif tblConfig.b_fpsForceExactADSFOV then fNewFOV = tblConfig.f_fpsADSFOV
	else fNewFOV = fDesiredFOV / f_leonFPSFOV * tblConfig.f_fpsFOV end

	return fNewFOV
end

local function ReApplyParamsToFOVCalc()
	if b_initialized then
		local _, _ = pcall(function()
			local cPlayerCameraFOVCalc = sdk.to_managed_object(ul_playerCameraFOVCalcAddress)
			if cPlayerCameraFOVCalc ~= nil then
				local cParam = cPlayerCameraFOVCalc._Param
				if cParam ~= nil then
					cPlayerCameraFOVCalc:call("setup", cParam)
				end
			end
		end)
	end
end



--HOOKS
local function PreSetDisplayScope(args)
	if c_scopeCameraControllerV3 == nil then c_scopeCameraControllerV3 = sdk.to_managed_object(args[2]) end
	if c_scopeCameraControllerV3 ~= nil then
		local cParamUserData = c_scopeCameraControllerV3._ParamUserData
		if cParamUserData ~= nil then
			if tbl_config.b_enabled then
				cParamUserData._LensImageDefaultScale = 1.0
				cParamUserData._LensImageZoomRate = 0.0
			else
				cParamUserData._LensImageDefaultScale = 2.0
				cParamUserData._LensImageZoomRate = 0.1
			end
		end
	end
end

local function PostSetDisplayScope(retVal)
end

local function PreADSCameraControllerUpdateFOV(args)
	if c_adsCameraController == nil then c_adsCameraController = sdk.to_managed_object(args[2]) end
	if c_adsCameraController ~= nil then
		local tblConfig = tbl_config

		local cADSZoomData = c_adsCameraController:call("get_CurrentZoomData")
		if cADSZoomData ~= nil then
			local cZoomSteps = cADSZoomData._ZoomSteps
			if cZoomSteps ~= nil then
				local iZoomStepsLength = cZoomSteps:get_size()
				for i = 0, iZoomStepsLength - 1 do
					local cZoomStepData = cZoomSteps:get_element(i)
					if cZoomStepData ~= nil then
						local fOriginalFOV = TryGetOriginalFOVFromZoomStepData(cZoomStepData)
						if fOriginalFOV ~= nil then
							if tblConfig.b_enabled then
								cZoomStepData._ZoomFov = GetScopeFOV(fOriginalFOV, i)
							else
								cZoomStepData._ZoomFov = fOriginalFOV
							end
						end
					end
				end
			end
		end

		local cZoomLogics = c_adsCameraController._ADSCameraZoomLogics
		if cZoomLogics ~= nil then
			local iZoomLogicsLength = cZoomLogics:get_size()
			for i = 0, iZoomLogicsLength - 1 do
				local cZoomLogic = cZoomLogics:get_element(i)
				if cZoomLogic ~= nil then
					local fOriginalFOV = TryGetOriginalFOVFromZoomLogic(cZoomLogic)
					if fOriginalFOV ~= nil then
						if tblConfig.b_enabled then
							cZoomLogic:call("set_CameraFOV", GetSightFOV(fOriginalFOV))
						else
							cZoomLogic:call("set_CameraFOV", fOriginalFOV)
						end
					end
				end
			end
		end
	end
end

local function PostADSCameraControllerUpdateFOV(retVal)
end

local function PreGetFOV(args)
	ul_playerCameraFOVCalcAddress = args[2]
end

local function PostGetFOV(retVal)
	if tbl_config.b_enabled and b_initialized then
		local cCharacterManager = TryGetCharacterManager()
		if cCharacterManager ~= nil then
			local fDesiredFOV = sdk.to_float(retVal)
			if fDesiredFOV ~= nil then
				local fNewFOV = fDesiredFOV
				local cPlayerContext = cCharacterManager:call("get_PlayerContextFast")
				if cPlayerContext ~= nil then
					local ePlayerMode = cPlayerContext:call("get_CurrentViewMode")
					if ePlayerMode ~= nil then
						local tePlayerMode = te_playerMode
						if ePlayerMode == tePlayerMode.TPS then fNewFOV = GetTPSFOV(fDesiredFOV)
						elseif ePlayerMode == tePlayerMode.FPS then fNewFOV = GetFPSFOV(fDesiredFOV) end
						retVal = sdk.float_to_ptr(fNewFOV)
					end
				end
			end
		end
	end

	return retVal
end



--CALLBACKS
local function PreUpdateBehavior()
	if b_initialized == false then
		local mSetDisplayScope = nil
		local mUpdateFOV = nil
		local mGetFOV = nil

		if tef_limitType == nil then
			tef_limitType = GenerateEnum("app.InteractManager.InteractLimitType", true)
		end

		if te_playerMode == nil then
			te_playerMode = GenerateEnum("app.PlayerMode", true)
		end

		local tdScopeCameraControllerV3 = sdk.find_type_definition("app.ScopeCameraControllerV3")
		if tdScopeCameraControllerV3 ~= nil then
			mSetDisplayScope = tdScopeCameraControllerV3:get_method("setDisplayScope")
		end

		local tdADSCameraController = sdk.find_type_definition("app.ADSCameraController")
		if tdADSCameraController ~= nil then
			mUpdateFOV = tdADSCameraController:get_method("updateFOV")
		end

		local tdPlayerCameraFOVCalc = sdk.find_type_definition("app.PlayerCameraFOVCalc")
		if tdPlayerCameraFOVCalc ~= nil then
			mGetFOV = tdPlayerCameraFOVCalc:get_method("getFOV")
		end

		if tef_limitType ~= nil and te_playerMode ~= nil and mSetDisplayScope ~= nil and mUpdateFOV ~= nil and mGetFOV ~= nil then
			sdk.hook(mSetDisplayScope, PreSetDisplayScope, PostSetDisplayScope)
			sdk.hook(mUpdateFOV, PreADSCameraControllerUpdateFOV, PostADSCameraControllerUpdateFOV)
			sdk.hook(mGetFOV, PreGetFOV, PostGetFOV)
			b_initialized = true
			LogInfo("Initialized")
		end
	end
end



--MAIN
LoadFromJson()
re.on_config_save(SaveToJson)
re.on_pre_application_entry("UpdateBehavior", PreUpdateBehavior)
LogInfo("Loaded " .. s_version)



--SCRIPT GENERATED UI
re.on_draw_ui(function()
	if imgui.tree_node(s_GUIDAndVVersion) then
		local bChanged = false
		local bAnyChanged = false
		local tblConfig = tbl_config

		local fFOVStep = 0.01
		local fFOVMin = 0.0
		local fFOVMax = 180.0

		imgui.text("General")
		imgui.separator()
		imgui.text("Note: FOV is vertical.")
		bChanged, tblConfig.b_enabled = imgui.checkbox("Enabled", tblConfig.b_enabled)
		bAnyChanged = bAnyChanged or bChanged
		imgui.new_line()

		imgui.text("TPS")
		imgui.separator()
		bChanged, tblConfig.f_tpsFOV = imgui.drag_float("TPS FOV", tblConfig.f_tpsFOV, fFOVStep, fFOVMin, fFOVMax)
		bAnyChanged = bAnyChanged or bChanged
		bChanged, tblConfig.b_tpsDisableADSFOVChange = imgui.checkbox("TPS Disable ADS FOV change", tblConfig.b_tpsDisableADSFOVChange)
		bAnyChanged = bAnyChanged or bChanged
		local bIsTPSDisableADSFOVChange = tblConfig.b_tpsDisableADSFOVChange
		imgui.begin_disabled(bIsTPSDisableADSFOVChange)
		bChanged, tblConfig.b_tpsForceExactADSFOV = imgui.checkbox("TPS Force exact ADS FOV", tblConfig.b_tpsForceExactADSFOV)
		bAnyChanged = bAnyChanged or bChanged
		imgui.end_disabled()
		local bIsTPSForceExactADSFOV = tblConfig.b_tpsForceExactADSFOV
		imgui.begin_disabled(bIsTPSDisableADSFOVChange or bIsTPSForceExactADSFOV == false)
		bChanged, tblConfig.f_tpsADSFOV = imgui.drag_float("TPS ADS FOV", tblConfig.f_tpsADSFOV, fFOVStep, fFOVMin, fFOVMax)
		bAnyChanged = bAnyChanged or bChanged
		imgui.end_disabled()
		imgui.new_line()

		imgui.text("FPS")
		imgui.separator()
        bChanged, tblConfig.f_fpsFOV = imgui.drag_float("FPS FOV", tblConfig.f_fpsFOV, fFOVStep, fFOVMin, fFOVMax)
        bAnyChanged = bAnyChanged or bChanged
        bChanged, tblConfig.b_fpsDisableADSFOVChange = imgui.checkbox("FPS Disable ADS FOV change", tblConfig.b_fpsDisableADSFOVChange)
        bAnyChanged = bAnyChanged or bChanged
        local bIsFPSDisableADSFOVChange = tblConfig.b_fpsDisableADSFOVChange
        imgui.begin_disabled(bIsFPSDisableADSFOVChange)
        bChanged, tblConfig.b_fpsForceExactADSFOV = imgui.checkbox("FPS Force exact ADS FOV", tblConfig.b_fpsForceExactADSFOV)
        bAnyChanged = bAnyChanged or bChanged
        imgui.end_disabled()
        local bIsFPSForceExactADSFOV = tblConfig.b_fpsForceExactADSFOV
        imgui.begin_disabled(bIsFPSDisableADSFOVChange or bIsFPSForceExactADSFOV == false)
        bChanged, tblConfig.f_fpsADSFOV = imgui.drag_float("FPS ADS FOV", tblConfig.f_fpsADSFOV, fFOVStep, fFOVMin, fFOVMax)
        bAnyChanged = bAnyChanged or bChanged
        imgui.end_disabled()
        imgui.new_line()


		imgui.text("Scope")
		imgui.separator()
		bChanged, tblConfig.f_scopeBaseZoomMultiplier = imgui.drag_float("Scope base zoom multiplier", tblConfig.f_scopeBaseZoomMultiplier, 0.001, 0.0, 10.0)
		bAnyChanged = bAnyChanged or bChanged
		imgui.new_line()

		imgui.text("Defaults")
		imgui.separator()
		imgui.text("TPS FOV: 40")
		imgui.text("TPS ADS FOV: 25")
		imgui.spacing()
		imgui.text("FPS FOV: 46")
		imgui.text("FPS ADS FOV: 40")
		imgui.spacing()
		imgui.text("Disable ADS FOV change: OFF")
		imgui.text("Force exact ADS FOV: OFF")
		imgui.spacing()
		imgui.text("Scope base zoom multiplier: 2.0")
		imgui.new_line()

		if bAnyChanged then ReApplyParamsToFOVCalc() end

		imgui.tree_pop()
	end
end)