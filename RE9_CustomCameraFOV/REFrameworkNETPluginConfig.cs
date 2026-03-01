#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Collections.Concurrent;

using Logging = REFrameworkNET.API; //Replace with logging class if needed


namespace REFrameworkNETPluginConfig
{
#if EMBEDDED_SOURCE //If referencing .cs files directly in plugin and shipping only one .dll file instead of using shared dependency .dll
	internal static class Info
#else
	public static class Info
#endif
	{
		/* VARIABLES */
		//Library info
		internal const string PLUGIN_NAME = "REFrameworkNETPluginConfig";
		internal const string COPYRIGHT = "";
		internal const string COMPANY = "https://github.com/TonWonton/REFrameworkNETPluginConfig";
		internal const string VERSION = "1.0.0";

		//File path
		public static readonly string rootFolder = Path.GetDirectoryName(Environment.ProcessPath) ?? Directory.GetCurrentDirectory(); //Replace with correct directory lookup and folder names if needed
		public const string FOLDER1 = "reframework";
		public const string FOLDER2 = "data";
		public const string FILE_EXTENSION = ".json";

		//Directory
		private static readonly string _configDirectory = Path.Combine(rootFolder, FOLDER1, FOLDER2);
		/// <summary>Returns the predetermined directory + hardcoded directories.</summary>
		public static string ConfigDirectory { get { return _configDirectory; } }

		//File name
		public const string FALLBACK_FILE_NAME = "config";
		private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

		//Json options
		private static readonly JsonSerializerOptions _jsonOptions = GetJsonSerializerOptions();
		/// <summary>Returns the field initialized <c>_jsonOptions</c> with DefaultJsonTypeInfoResolver() and MakeReadOnly(populateMissingResolver: true).</summary>
		public static JsonSerializerOptions JsonOptions { get { return _jsonOptions; } }



		/* METHODS */
		//Test directories
		//internal static void TestDirectories()
		//{
		//	Log.Info("Directory.GetCurrentDirectory() = " + Directory.GetCurrentDirectory());
		//	Log.Info("AppDomain.CurrentDomain.BaseDirectory = " + AppDomain.CurrentDomain.BaseDirectory);
		//	Log.Info("Environment.CurrentDirectory = " + Environment.CurrentDirectory);
		//	Log.Info("Assembly.GetExecutingAssembly().Location = " + System.Reflection.Assembly.GetExecutingAssembly().Location);
		//	Log.Info("Path.GetDirectoryName(typeof(Info).Assembly.Location) = " + Path.GetDirectoryName(typeof(Info).Assembly.Location));
		//	Log.Info("Path.GetDirectoryName(Environment.ProcessPath) = " + Path.GetDirectoryName(Environment.ProcessPath));
		//	Log.Info("Process.GetCurrentProcess().MainModule.FileName = " + System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);
		//}

		//JsonSerializerOptions
		private static JsonSerializerOptions GetJsonSerializerOptions()
		{
			JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
			{
				WriteIndented = true,
				TypeInfoResolver = new DefaultJsonTypeInfoResolver()
			};
			jsonSerializerOptions.MakeReadOnly(populateMissingResolver: true);
			return jsonSerializerOptions;
		}

		/// <summary>
		/// Returns the predetermined directory + hardcoded directories + parameter + FILE_EXTENSION.
		/// </summary>
		public static string GetConfigFilePath(string fileNameWithoutExtension)
		{
			//TestDirectories(); //Debug test directories

			//Get info
			char[] invalidFileNameChars = _invalidFileNameChars;
			int invalidFileNameCharsLength = invalidFileNameChars.Length;

			if (string.IsNullOrEmpty(fileNameWithoutExtension))
			{
				//Use fallback if null or empty
				fileNameWithoutExtension = FALLBACK_FILE_NAME;
				Log.Warning("Provided file name is null or empty, falling back to = " + fileNameWithoutExtension);
			}

			if (fileNameWithoutExtension.IndexOfAny(invalidFileNameChars) >= 0)
			{
				//Sanitize if invalid characters
				for (int i = 0; i < invalidFileNameCharsLength; i++)
				{
					fileNameWithoutExtension = fileNameWithoutExtension.Replace(invalidFileNameChars[i], '_');
				}

				Log.Warning("Provided file name contains invalid characters, sanitized file name = " + fileNameWithoutExtension);
			}

			return Path.Combine(_configDirectory, fileNameWithoutExtension + FILE_EXTENSION);
		}
	}



	internal static class Log
	{
		internal const string PREFIX = "[REFrameworkNETPluginConfig] ";

		internal static void Info(string message) { Logging.LogInfo(PREFIX + message); }
		internal static void Warning(string message) { Logging.LogWarning(PREFIX + message); }
		internal static void Error(string message) { Logging.LogError(PREFIX + message); }
	}



#if EMBEDDED_SOURCE
	internal class Config
#else
	public class Config
#endif
	{
		/* VARIABLES */
		private readonly ConcurrentDictionary<string, ConfigEntryBase> _configEntries = new ConcurrentDictionary<string, ConfigEntryBase>();
		private readonly string _fileNameWithoutFileExtension;

		/// <summary>Returns the <c>_configEntries</c> collection.</summary>
		public ConcurrentDictionary<string, ConfigEntryBase> ConfigEntries { get { return _configEntries; } }
		/// <summary>Returns the <c>ICollection</c> interface of the <c>_configEntries</c> keys.</summary>
		public ICollection<string> Keys { get { return _configEntries.Keys; } }
		/// <summary>Returns the <c>ICollection</c> interface of the <c>_configEntries</c> values.</summary>
		public ICollection<ConfigEntryBase> Values { get { return _configEntries.Values; } }
		/// <summary>Returns <c>_configEntries.Count</c>.</summary>
		public int Count { get { return _configEntries.Count; } }



		/* METHODS */
		public bool ContainsKey(string key) { return _configEntries.ContainsKey(key); }
		/// <summary>
		/// Is <c>return _configEntries.TryGetValue(key, out configEntryBase);</c>.
		/// </summary>
		public bool TryGetEntry(string key, [MaybeNullWhen(false)] out ConfigEntryBase configEntryBase) { return _configEntries.TryGetValue(key, out configEntryBase); }
		/// <summary>
		/// Is <c>_configEntries.TryGetValue(key, out configEntryBase)</c> and <c>is ConfigEntry&lt;T&gt;</c>.
		/// </summary>
		public bool TryGetEntryAs<T>(string key, [MaybeNullWhen(false)] out ConfigEntry<T> configEntry)
		{
			if (_configEntries.TryGetValue(key, out ConfigEntryBase? configEntryBase) && configEntryBase is ConfigEntry<T> configEntryAsT)
			{
				configEntry = configEntryAsT;
				return true;
			}

			configEntry = null;
			return false;
		}

		/// <summary>
		/// Adds a new ConfigEntry&lt;T&gt; which will be serialized if possible. Overwrites existing entry if it exists, and does not unregister events.
		/// </summary>
		public ConfigEntry<T> Add<T>(string key, T defaultValue)
		{
			ConfigEntry<T> entry = new ConfigEntry<T>(key, defaultValue);
			_configEntries.AddOrUpdate(key, entry, (_, _) => entry);
			return entry;
		}

		 /// <summary>
		 /// Adds an existing ConfigEntryBase which will be serialized if possible. Overwrites existing entry if it exists, and does not unregister events.
		 /// </summary>
		public void Add(string key, ConfigEntryBase configEntry)
		{
			_configEntries.AddOrUpdate(key, configEntry, (_, _) => configEntry);
		}

		/// <summary>Is <c>return _configEntries.TryRemove(key, out configEntryBase);</c></summary>
		public bool TryRemove(string key, [MaybeNullWhen(false)] out ConfigEntryBase configEntryBase) { return _configEntries.TryRemove(key, out configEntryBase); }

		/// <summary>Tries to save config entries to disk as .json to a predetermined directory + constructor string parameter + .json.</summary>
		public void SaveToJson() { SaveToJson(_fileNameWithoutFileExtension); }

		/// <summary>
		/// Tries to save config entries to disk as .json to a predetermined directory + string parameter + .json.
		/// </summary>
		public void SaveToJson(string fileNameWithoutFileExtension)
		{
			//Get info
			string? configFilePath = null;
			JsonObject jsonObject = new JsonObject();

			try
			{
				configFilePath = Info.GetConfigFilePath(fileNameWithoutFileExtension);
			}
			catch (Exception exception) { Log.Error("Exception while trying to get config file path: " + exception.ToString()); return; }

			if (string.IsNullOrEmpty(configFilePath)) { Log.Error("Unable to save config file to disk: config file path is null or empty"); return; }

			try
			{
				//Try to add JsonNode from each ConfigEntry
				foreach ((string key, ConfigEntryBase entry) in _configEntries)
				{
					try
					{
						if (entry.TryGetJsonTypeInfo(out JsonTypeInfo? jsonTypeInfo) == false) continue; //Skip unserializable entries

						JsonNode? jsonNode = entry.ConfigValue;
						jsonObject[key] = jsonNode;
					}
					catch (Exception exception) { Log.Error("Exception while trying to get JSON node " + key + " from config entry, skipping entry: " + exception.ToString()); continue; }
				}
			}
			catch (Exception exception) { Log.Error("Exception while trying to get JSON nodes from config entries: " + exception.ToString()); return; }

			try
			{
				//Try to write file to disk
				string tempPath = configFilePath + Path.GetRandomFileName();
				Directory.CreateDirectory(Info.ConfigDirectory);
				File.WriteAllText(tempPath, jsonObject.ToJsonString(Info.JsonOptions));
				File.Move(tempPath, configFilePath, overwrite: true);
			}
			catch (Exception exception) { Log.Error("Exception while trying to save config to JSON: " + exception.ToString()); }
		}

		/// <summary>Tries to load config entries from disk as .json from a predetermined directory + constructor string parameter + .json.</summary>
		public void LoadFromJson() { LoadFromJson(_fileNameWithoutFileExtension); }

		/// <summary>
		/// Tries to load config entries from disk as .json from a predetermined directory + string parameter + .json.
		/// </summary>
		public void LoadFromJson(string fileNameWithoutFileExtension)
		{
			//Get info
			string? configFilePath = null;
			JsonObject? jsonObject = null;

			try
			{
				configFilePath = Info.GetConfigFilePath(fileNameWithoutFileExtension);
			}
			catch (Exception exception) { Log.Error("Exception while trying to get config file path: " + exception.ToString()); return; }

			if (string.IsNullOrEmpty(configFilePath)) { Log.Error("Unable to read config file from disk: config file path is null or empty"); return; }

			try
			{
				//Try get JsonObject
				jsonObject = JsonSerializer.Deserialize<JsonObject>(File.ReadAllText(configFilePath), Info.JsonOptions);
			}
			catch (DirectoryNotFoundException) { return; }
			catch (FileNotFoundException) { return; }
			catch (Exception exception) { Log.Error("Exception while trying to read config file from disk: " + exception.ToString()); return; }

			if (jsonObject == null) return;

			try
			{
				//Try set ConfigEntry values from JsonObject
				foreach ((string key, JsonNode? jsonNode) in jsonObject)
				{
					try
					{
						if (_configEntries.TryGetValue(key, out ConfigEntryBase? entry)) entry.ConfigValue = jsonNode;
					}
					catch (Exception exception) { Log.Error("Exception while trying to set config entry " + key + " from JSON, skipping entry: " + exception.ToString()); continue; }
				}
			}
			catch (Exception exception) { Log.Error("Exception while trying to get config entries from JSON: " + exception.ToString()); }
		}



		/* INITIALIZATION */
		public Config(string fileNameWithoutFileExtension) { _fileNameWithoutFileExtension = fileNameWithoutFileExtension; }
	}



#if EMBEDDED_SOURCE
	internal class ConfigEntry<T> : ConfigEntryBase
#else
	public class ConfigEntry<T> : ConfigEntryBase
#endif
	{
		/* VARIABLES */
		private T _value;
		private T _defaultValue;

		public T Value { get { return _value; } }
		public T DefaultValue { get { return _defaultValue; } }
		public ref T RefValue { get { return ref _value; } }

		/// <summary>
		/// Gets the <c>JsonNode</c> from <c>_value</c> or sets <c>_value</c> from the <c>JsonNode</c> if <c>T</c> has <c>JsonTypeInfo</c> and can be serialized. Setter does not invoke the events. 
		/// </summary>
		public override JsonNode? ConfigValue
		{
			get
			{
				try
				{
					if (TryGetJsonTypeInfo(out JsonTypeInfo? jsonTypeInfo))
					{
						return JsonSerializer.SerializeToNode(_value, jsonTypeInfo);
					}

					return null;
				}
				catch (Exception exception) { Log.Error("Exception while trying to get JSON node from config entry " + Key + ": " + exception.ToString()); return null; }
			}
			set
			{
				try
				{
					if (TryGetJsonTypeInfo(out JsonTypeInfo? jsonTypeInfo))
					{
						if (value != null)
						{
							object? deserializedValue = value.Deserialize(jsonTypeInfo);

							if (deserializedValue is T tValue) _value = tValue;
							else if (deserializedValue is null && default(T) is null) _value = default!;
							else Log.Warning("Deserialized value for config entry " + Key + " is not of the correct type, leaving config entry value unchanged");
						}
						else if (default(T) is null)
						{
							_value = default!;
						}
					}
				}
				catch (Exception exception) { Log.Error("Exception while trying to set config entry " + Key + " from JsonNode: " + exception.ToString()); }
			}
		}



		/* EVENT */
		/// <summary>Invoked on <c>Set(T newValue)</c>, <c>Reset()</c>, and <c>NotifyValueChanged()</c>, but not on the silent methods or the <c>ConfigValue</c> setter.</summary>
		public event Action? ValueChanged;
		/// <summary>Invoked on <c>Set(T newValue)</c>, <c>Reset()</c>, and <c>NotifyValueChanged()</c>, but not on the silent methods or the <c>ConfigValue</c> setter.</summary>
		public event Action<T>? ValueChangedT;



		/* METHODS */
		/// <summary>Sets <c>_value</c> and invokes the <c>ValueChanged</c> events.</summary>
		public void Set(T newValue) { _value = newValue; NotifyValueChanged(); }
		/// <summary>Sets <c>_value</c> without invoking the <c>ValueChanged</c> events.</summary>
		public void SetSilent(T newValue) { _value = newValue; }

		/// <summary>Sets <c>_value</c> to <c>_defaultValue</c> and invokes the <c>ValueChanged</c> events.</summary>
		public void Reset() { _value = _defaultValue; NotifyValueChanged(); }
		/// <summary>Sets <c>_value</c> to <c>_defaultValue</c> without invoking the <c>ValueChanged</c> events.</summary>
		public void ResetSilent() { _value = _defaultValue; }

		/// <summary>Invokes the <c>ValueChanged</c> events.</summary>
		public void NotifyValueChanged()
		{
			var valueChanged = ValueChanged;
			var valueChangedT = ValueChangedT;

			valueChanged?.Invoke();
			valueChangedT?.Invoke(_value);
		}



		/* INITIALIZATION */
		public ConfigEntry(string key, T defaultValue) : base(key, typeof(T))
		{
			_value = defaultValue;
			_defaultValue = defaultValue;
		}
	}



#if EMBEDDED_SOURCE
	internal abstract class ConfigEntryBase
#else
	public abstract class ConfigEntryBase
#endif
	{
		/* VARIABLES */
		private readonly string _key;
		private readonly Type _type;
		private readonly JsonTypeInfo? _jsonTypeInfo;

		public string Key { get { return _key; } }
		public Type Type { get { return _type; } }
		public JsonTypeInfo? JsonTypeInfo { get { return _jsonTypeInfo; } }
		public abstract JsonNode? ConfigValue { get; set; }



		/* METHODS */
		/// <summary>
		/// Tries to get the <c>JsonTypeInfo</c> which was cached in the constructor if any. Returns true if it exists and false if it doesn't.
		/// </summary>
		public bool TryGetJsonTypeInfo([MaybeNullWhen(false)] out JsonTypeInfo jsonTypeInfo)
		{
			jsonTypeInfo = _jsonTypeInfo;
			return _jsonTypeInfo != null;
		}



		/* INITIALIZATION */
		protected ConfigEntryBase(string key, Type type)
		{
			_key = key;
			_type = type;

			try
			{
				if (Info.JsonOptions.TryGetTypeInfo(_type, out JsonTypeInfo? jsonTypeInfo))
				{
					_jsonTypeInfo = jsonTypeInfo;
				}
				else
				{
					Log.Warning("Type " + _type.FullName + " with key " + _key + " does not have JSON type info and will not be serialized");
				}
			}
			catch (Exception exception) { Log.Error("Exception while trying to get JSON type info for config entry value type " + _type.FullName + " with key " + _key + ": " + exception.ToString()); }
		}
	}
}