﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Runtime.Serialization.Json;

using ElencyConfig.Encryption;

namespace ElencyConfig
{
    public class ElencyConfigClient : IElencyConfigClient
    {
        private bool _initialized = false;
        private ElencyConfiguration _config;
        private string _configurationPath;
        private Dictionary<string, string> _currentConfiguration;
        private string _currentVersionHash;
        private string _currentAppVersion;
        private string _currentEnvironment;
        private string _currentConfigurationId;
        private Timer _timer;
        private TimerCallback _timerCallback;
        private bool _refreshing = false;
        private const int Timeout = 30000;

        public async Task Init(ElencyConfiguration config)
        {
            if (config == null)
            {
                throw new Exception("You must define a configuration");
            }

            if (_initialized == true)
            {
                throw new Exception("The client is already initialised");
            }

            try
            {
                config.Validate();
                _config = config;
                _configurationPath = string.Format("/config/{0}/{1}/{2}",
                    _config.AppId,
                    _config.Environment,
                    _config.AppVersion);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            if (_config.LocalConfiguration != null)
            {
                await GetLocalConfiguration();
                return;
            }

            await GetConfiguration();
            _initialized = true;

            if (_config.RefreshInterval > 0)
            {
                _timerCallback = new TimerCallback(RefreshConfigurationOnInteval);
                _timer = new Timer(_timerCallback, null, 0, _config.RefreshInterval);
            }
        }

        private async void RefreshConfigurationOnInteval(object stateInfo)
        {
            if (_refreshing)
            {
                return;
            }

            try
            {
                await GetConfiguration();
            }
            catch (Exception ex)
            {
                _config.RefreshFailure?.Invoke(ex);
            }
        }

        public string AppVersion
        {
            get
            {
                CheckInitialisation();
                return _currentAppVersion;
            }
        }

        public string Environment
        {
            get
            {
                CheckInitialisation();
                return _currentEnvironment;
            }
        }

        public string ConfigurationId
        {
            get
            {
                CheckInitialisation();
                return _currentConfigurationId;
            }
        }

        public string Get(string key)
        {
            CheckInitialisation();
            if (_currentConfiguration.ContainsKey(key))
                return _currentConfiguration[key];

            return null;
        }

        public List<string> GetAllKeys()
        {
            CheckInitialisation();
            return _currentConfiguration.Keys.ToList();
        }

        public async Task Refresh()
        {
            CheckInitialisation();
            await GetConfiguration();
        }

        public void Reset()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            _initialized = false;
            _configurationPath = null;
            _currentConfiguration = null;
            _currentVersionHash = null;
            _currentAppVersion = null;
            _currentEnvironment = null;
            _currentConfigurationId = null;
        }

        public bool? GetBoolean(string key)
        {
            return ValueRetrieval.GetBoolean(Get(key));
        }

        public bool? GetBoolean(string key, bool? fallback)
        {
            return ValueRetrieval.GetBoolean(Get(key), fallback);
        }

        public DateTime? GetDateTime(string key)
        {
            return ValueRetrieval.GetDateTime(Get(key));
        }

        public DateTime? GetDateTime(string key, DateTime? fallback)
        {
            return ValueRetrieval.GetDateTime(Get(key), fallback);
        }

        public int? GetInteger(string key)
        {
            return ValueRetrieval.GetInteger(Get(key));
        }

        public int? GetInteger(string key, int? fallback)
        {
            return ValueRetrieval.GetInteger(Get(key), fallback);
        }

        public float? GetFloat(string key)
        {
            return ValueRetrieval.GetFloat(Get(key));
        }

        public float? GetFloat(string key, float? fallback)
        {
            return ValueRetrieval.GetFloat(Get(key), fallback);

        }

        public decimal? GetDecimal(string key)
        {
            return ValueRetrieval.GetDecimal(Get(key));
        }

        public decimal? GetDecimal(string key, decimal? fallback)
        {
            return ValueRetrieval.GetDecimal(Get(key), fallback);
        }

        public double? GetDouble(string key)
        {
            return ValueRetrieval.GetDouble(Get(key));
        }

        public double? GetDouble(string key, double? fallback)
        {
            return ValueRetrieval.GetDouble(Get(key), fallback);
        }

        public T GetObject<T>(string key) where T : class
        {
            return ValueRetrieval.GetObject<T>(Get(key));
        }

        public T GetObject<T>(string key, T fallback) where T : class
        {
            return ValueRetrieval.GetObject<T>(Get(key), fallback);
        }

        private void CheckInitialisation()
        {
            if (!_initialized)
            {
                throw new Exception("The client has not been successfully initialized");
            }
        }

        private async Task GetLocalConfiguration()
        {
            _currentAppVersion = _config.LocalConfiguration.AppVersion;
            _currentEnvironment = _config.LocalConfiguration.Environment;
            _currentConfigurationId = _config.LocalConfiguration.ConfigurationId;
            _currentConfiguration = _config.LocalConfiguration.ConfigurationData;
            _initialized = true;

            if (_config.Retrieved != null)
            {
                await Task.Run(_config.Retrieved);
            }
        }

        private async Task GetConfiguration()
        {
            if (_currentVersionHash != null)
            {
                try
                {
                    _refreshing = true;
                    await RefreshConfiguration();
                    _refreshing = false;
                }
                catch (Exception ex)
                {
                    _refreshing = false;
                    throw ex;
                }
            }
            else
            {
                await RetrieveConfiguration();
            }
        }

        private async Task RefreshConfiguration()
        {
            var authorizationHeader = Authorization.GenerateAuthorizationHeader(_config, _configurationPath, "head");
            var uri = string.Format("{0}{1}", _config.Uri, _configurationPath);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "HEAD";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", authorizationHeader);
            request.Headers.Add("x_version_hash", _currentVersionHash);
            request.Timeout = _config.RequestTimeout.HasValue ? _config.RequestTimeout.Value : Timeout;

            await Task.Run(async () =>
            {
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            await RetrieveConfiguration();
                            return;
                        }

                        if (response.StatusCode == HttpStatusCode.NoContent)
                        {
                            return;
                        }

                        throw new Exception(string.Format("HttpStatusCode: {0} was returned", response.StatusCode));
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("An error occurred while trying to refresh the configuration", ex);
                }
            });
        }

        private async Task RetrieveConfiguration()
        {
            var accessToken = await GetAccessToken();
            var authorizationHeader = Authorization.GenerateAuthorizationHeader(_config, _configurationPath, "get");

            var uri = string.Format("{0}{1}", _config.Uri, _configurationPath);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", authorizationHeader);
            request.Headers.Add("x-access-token", accessToken);
            request.Timeout = _config.RequestTimeout.HasValue ? _config.RequestTimeout.Value : Timeout;

            await Task.Run(async () =>
            {
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            Stream responseStream = response.GetResponseStream();
                            var body = new StreamReader(responseStream).ReadToEnd();

                            var searchFor = "\"configuration\":";
                            var startIndex = body.IndexOf(searchFor);
                            var endIndex = body.IndexOf("],", startIndex + 1);
                            var originalEncryptedConfigurationBody = body.Substring(startIndex, (endIndex - startIndex) + 1);
                            var encryptedConfigurationBody = originalEncryptedConfigurationBody.Substring(searchFor.Length).Trim();
                            var encryptedConfigurationBodyAsArray = DeserializeObject<string[]>(encryptedConfigurationBody);
                            var decryptedConfigurationBody = AES_256_CBC.Decrypt(encryptedConfigurationBodyAsArray, _config.ConfigEncryptionKey);
                            body = body.Replace(originalEncryptedConfigurationBody, searchFor + decryptedConfigurationBody);

                            var configurationResponse = DeserializeObject<ConfigurationResponse>(body);
                            var currentConfiguration = new Dictionary<string, string>();

                            for (var i = 0; i < configurationResponse.configuration.Length; i++)
                            {
                                var item = configurationResponse.configuration[i];

                                if (!item.encrypted)
                                {
                                    currentConfiguration.Add(item.key, item.value[0]);
                                    continue;
                                }

                                item.value = new string[] { AES_256_CBC.Decrypt(item.value, _config.ConfigEncryptionKey) };
                                currentConfiguration.Add(item.key, item.value[0]);
                            }

                            _currentAppVersion = configurationResponse.appVersion;
                            _currentEnvironment = configurationResponse.environment;
                            _currentConfigurationId = configurationResponse.configurationId;
                            _currentVersionHash = configurationResponse.configurationHash;
                            _currentConfiguration = currentConfiguration;
                            _initialized = true;

                            if (_config.Retrieved != null)
                            {
                                await Task.Run(_config.Retrieved);
                            }
                            return;
                        }

                        throw new Exception(string.Format("HttpStatusCode: {0} was returned", response.StatusCode));
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("An error occurred while trying to refresh the configuration", ex);
                }
            });

        }

        private T DeserializeObject<T>(string value) where T : class
        {
            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(value)))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                return serializer.ReadObject(memoryStream) as T;
            }
        }


        private async Task<string> GetAccessToken()
        {
            var authorizationHeader = Authorization.GenerateAuthorizationHeader(_config, "/config", "head", true);
            var uri = string.Format("{0}/config", _config.Uri);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "HEAD";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", authorizationHeader);
            request.Timeout = _config.RequestTimeout.HasValue ? _config.RequestTimeout.Value : Timeout;

            string responseText = await Task.Run(() =>
            {
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    {

                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            return response.Headers["x-access-token"];
                        }
                    }

                    throw new Exception("An x-access-token could not be retrieved");
                }
                catch (Exception ex)
                {
                    throw new Exception("An error occurred while trying to retrieve an access token", ex);
                }
            });

            return responseText;
        }
    }
}
