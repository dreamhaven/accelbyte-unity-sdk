﻿// Copyright (c) 2018 - 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System.Runtime.Serialization;
using UnityEngine.Scripting;

namespace AccelByte.Models
{
    [DataContract, Preserve]
    public class OAuthConfig
    {
        [DataMember] public string ClientId;
        [DataMember] public string ClientSecret;

        /// <summary>
        ///  Copy member values
        /// </summary>
        public OAuthConfig ShallowCopy()
        {
            return (OAuthConfig)MemberwiseClone();
        }

        public bool Compare(OAuthConfig anotherConfig)
        {
            return this.ClientId == anotherConfig.ClientId &&
                   this.ClientSecret == anotherConfig.ClientSecret;
        }

        /// <summary>
        ///  Assign missing config values.
        /// </summary>
        public void Expand()
        {
            if (this.ClientId == null)
            {
                this.ClientId = "";
            }
            if (this.ClientSecret == null)
            {
                this.ClientSecret = "";
            }
        }

        /// <summary>
        /// Check required config field.
        /// </summary>
        public void CheckRequiredField()
        {
            if (string.IsNullOrEmpty(this.ClientId))
            {
                throw new System.Exception("Init AccelByte SDK failed, Client ID must not null or empty.");
            }
        }

        public bool IsRequiredFieldEmpty()
        {
            System.Collections.Generic.List<string> checkedStringFields = new System.Collections.Generic.List<string>()
            {
                ClientId
            };
            var retval = checkedStringFields.Exists((field) => string.IsNullOrEmpty(field));
            return retval;
        }
    }

    [DataContract, Preserve]
    public class MultiOAuthConfigs
    {
        [DataMember] public OAuthConfig Development;
        [DataMember] public OAuthConfig Certification;
        [DataMember] public OAuthConfig Production;
        [DataMember] public OAuthConfig Default;

        public void Expand()
        {
            if (Development == null)
            {
                Development = new OAuthConfig();
            }
            Development.Expand();
            if (Certification == null)
            {
                Certification = new OAuthConfig();
            }
            Certification.Expand();
            if (Production == null)
            {
                Production = new OAuthConfig();
            }
            Production.Expand();
            if (Default == null)
            {
                Default = new OAuthConfig();
            }
            Default.Expand();
        }

        internal OAuthConfig GetConfigFromEnvironment(SettingsEnvironment environment)
        {
            switch (environment)
            {
                case SettingsEnvironment.Development:
                    return Development;
                case SettingsEnvironment.Certification:
                    return Certification;
                case SettingsEnvironment.Production:
                    return Production;
                case SettingsEnvironment.Default:
                default:
                    return Default;
            }
        }
    }
}
