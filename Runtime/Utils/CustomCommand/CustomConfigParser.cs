// Copyright (c) 2023 - 2024 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Core;
using AccelByte.Models;
using System;
using System.Linq;
using System.Reflection;

namespace AccelByte.Utils
{
    internal class CustomConfigParser
    {
        private const string OverrideSDKConfigCommand = "-OverrideSDKConfig";
        private const string OverrideOAuthConfigCommand = "-OverrideOAuthConfig";
        private const string OverrideConfigV2Prefix = "-AB";

        private ConfigT ParseConfigFromArgs<ConfigT>(string command) where ConfigT : class
        {
            ConfigT retval = null;
            string[] args = GetGameArgs();

            string overrideConfig = CommandLineArgs.GetArg(args, command);
            if (!string.IsNullOrEmpty(overrideConfig))
            {
                try
                {
                    overrideConfig = overrideConfig.Replace("'", "\"");
                    retval = overrideConfig.ToObject<ConfigT>();
                }
                catch (System.Exception ex)
                {
                    AccelByteDebug.LogWarning($"Failed to parse {command} args.\nException:{ex.Message}");
                }
            }

            return retval;
        }

        private ConfigT ParseConfigV2FromArgs<ConfigT>(string command) where ConfigT : class, new()
        {
            ConfigT retval = new ConfigT();
            string[] args = GetGameArgs();
            
            string[] overrideConfigs = CommandLineArgs.GetArgs(args, command);
            for (int index = 0; index < overrideConfigs?.Length; index++)
            {
                if (!string.IsNullOrEmpty(overrideConfigs[index]) && overrideConfigs[index].Contains("="))
                {
                    try
                    {
                        string[] configArray = overrideConfigs[index].Split(new char[] {'='}, 2);
                        string configName = configArray[0];
                        string configValue = configArray[1];

                        var configField = typeof(ConfigT).GetField(configName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                        var configType = Nullable.GetUnderlyingType(configField.FieldType) ?? configField.FieldType;

                        if (string.IsNullOrEmpty(configValue) && configType == typeof(bool))
                        {
                            configValue = "True";
                        }

                        configField.SetValue(retval, Convert.ChangeType(configValue, configType));
                    }
                    catch (System.Exception ex)
                    {
                        AccelByteDebug.LogWarning($"Failed to parse {overrideConfigs[index]} arg for config injection.\nException:{ex.Message}");
                    }
                }
            }

            if (CheckNullField(retval))
            {
                retval = null;
            }
            return retval;
        }

        public MultiSDKConfigsArgs ParseSDKConfigFromArgs()
        {
            MultiSDKConfigsArgs retval = null;
            var configArgs = ParseConfigFromArgs<SDKConfigArgs>(OverrideSDKConfigCommand);
            var configArgsV2 = ParseConfigV2FromArgs<SDKConfigArgs>(OverrideConfigV2Prefix);

            configArgs = configArgsV2.CopyToArgConfig(configArgs);

            if (configArgs != null)
            {
                retval = new MultiSDKConfigsArgs();
                retval.SetConfigValueToAllEnv(configArgs);
            }
            return retval;
        }

        public MultiOAuthConfigs ParseOAuthConfigFromArgs()
        {
            MultiOAuthConfigs retval = null;
            var configArgs = ParseConfigFromArgs<SDKConfigArgs>(OverrideOAuthConfigCommand);
            var configArgsV2 = ParseConfigV2FromArgs<SDKConfigArgs>(OverrideConfigV2Prefix);

            configArgs = configArgsV2.CopyToArgConfig(configArgs);

            if (configArgs != null)
            {
                var oAuthArgs = configArgs.CopyToConfig(new OAuthConfig());
                
                retval = new MultiOAuthConfigs();
                retval.SetConfigValueToAllEnv(oAuthArgs);
            }
            return retval;
        }

        private bool CheckNullField<ConfigT>(ConfigT config) where ConfigT : class
        {
            return typeof(ConfigT)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .All(fieldInfo => fieldInfo.GetValue(config) == null);
        }

        protected virtual string[] GetGameArgs()
        {
            return System.Environment.GetCommandLineArgs();
        }
    }
}