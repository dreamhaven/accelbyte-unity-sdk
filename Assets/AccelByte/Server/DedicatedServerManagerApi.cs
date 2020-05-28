﻿// Copyright (c) 2020 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections;
using AccelByte.Models;
using AccelByte.Core;
using UnityEngine.Assertions;
using UnityEngine;
using System.Collections.Generic;

namespace AccelByte.Server
{
    internal enum ServerType
    {
        NONE,
        LOCALSERVER,
        CLOUDSERVER
    }

    internal class DedicatedServerManagerApi
    {
        private readonly string baseUrl;
        private readonly IHttpWorker httpWorker;
        private readonly string namespace_;
        private string dsmServerUrl = "";
        private RegisterServerRequest serverSetup;
        private ServerType serverType = ServerType.NONE;

        internal DedicatedServerManagerApi(string baseUrl, string namespace_, IHttpWorker httpWorker)
        {
            Debug.Log("ServerApi init serverapi start");
            Assert.IsNotNull(baseUrl, "Creating " + GetType().Name + " failed. Parameter baseUrl is null");
            Assert.IsFalse(
                string.IsNullOrEmpty(namespace_),
                "Creating " + GetType().Name + " failed. Parameter namespace is null.");
            Assert.IsNotNull(httpWorker, "Creating " + GetType().Name + " failed. Parameter httpWorker is null");

            this.baseUrl = baseUrl;
            this.namespace_ = namespace_;
            this.httpWorker = httpWorker;
            this.serverSetup = new RegisterServerRequest() {
                game_version = "",
                ip = "",
                pod_name = "",
                provider = ""
            };
        }

        public IEnumerator RegisterServer(RegisterServerRequest registerRequest, string accessToken,
            ResultCallback callback)
        {
            Assert.IsNotNull(registerRequest, "Register failed. registerserverRequest is null!");
            Assert.IsNotNull(accessToken, "Can't update a slot! accessToken parameter is null!");
            if (this.serverType != ServerType.NONE)
            {
                callback.TryError(ErrorCode.Conflict, "Server is already registered.");

                yield break;
            }
            if (dsmServerUrl.Length == 0)
            {
                ServerQos qos = AccelByteServerPlugin.GetQos();
                Result<Dictionary<string, int>> latenciesResult = null;
                qos.GetServerLatencies(reqResult => latenciesResult = reqResult);
                yield return new WaitUntil(() => latenciesResult != null);

                KeyValuePair<string, int> minLatency = new KeyValuePair<string, int>("", 10000);
                foreach (KeyValuePair<string, int> latency in latenciesResult.Value)
                {
                    if(latency.Value < minLatency.Value)
                    {
                        minLatency = latency;
                    }
                }

                var getUrlRequest = HttpRequestBuilder.CreateGet(this.baseUrl + "/public/dsm?region=" + minLatency.Key)
                .WithBearerAuth(accessToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

                IHttpResponse getUrlResponse = null;

                yield return this.httpWorker.SendRequest(getUrlRequest, rsp => getUrlResponse = rsp);

                var getUrlResult = getUrlResponse.TryParseJson<DSMClient>();
                dsmServerUrl = getUrlResult.Value.host_address;
            }
            if(serverSetup.ip.Length == 0)
            {
                var getPubIpRequest = HttpRequestBuilder.CreateGet("https://api.ipify.org?format=json")
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

                IHttpResponse getPubIpResponse = null;

                yield return this.httpWorker.SendRequest(getPubIpRequest, rsp => getPubIpResponse = rsp);

                var getPubIpResult = getPubIpResponse.TryParseJson<PubIp>();
                serverSetup.ip = getPubIpResult.Value.ip;

                string[] args = System.Environment.GetCommandLineArgs();
                bool isProviderFound = false;
                bool isGameVersionFound = false;
                foreach(string arg in args)
                {
                    Debug.Log("arg: " + arg);
                    if (arg.Contains("provider"))
                    {
                        string[] split = arg.Split('=');
                        serverSetup.provider = split[1];
                        isProviderFound = true;
                    }
                    if (arg.Contains("game_version"))
                    {
                        string[] split = arg.Split('=');
                        serverSetup.game_version = split[1];
                        isGameVersionFound = true;
                    }
                    if(isProviderFound && isGameVersionFound)
                    {
                        break;
                    }
                }
            }

            registerRequest.ip = serverSetup.ip;
            registerRequest.provider = serverSetup.provider;
            registerRequest.game_version = serverSetup.game_version;

            var request = HttpRequestBuilder.CreatePost(this.dsmServerUrl + "/dsm/namespaces/{namespace}/servers/register")
                .WithPathParam("namespace", this.namespace_)
                .WithBearerAuth(accessToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .WithBody(registerRequest.ToUtf8Json())
                .GetResult();

            IHttpResponse response = null;

            yield return this.httpWorker.SendRequest(request, rsp => response = rsp);

            var result = response.TryParseJson<ServerInfo>();
            if (!result.IsError)
            {
                serverSetup.pod_name = result.Value.pod_name;
                serverType = ServerType.CLOUDSERVER;
            }   
            callback.Try(response.TryParse());
        }

        public IEnumerator ShutdownServer(ShutdownServerRequest shutdownServerRequest, string accessToken,
            ResultCallback callback)
        {
            Assert.IsNotNull(shutdownServerRequest, "Register failed. shutdownServerNotif is null!");
            Assert.IsNotNull(accessToken, "Can't update a slot! accessToken parameter is null!");
            if (this.serverType != ServerType.CLOUDSERVER)
            {
                callback.TryError(ErrorCode.Conflict, "Server not registered as Cloud Server.");

                yield break;
            }

            shutdownServerRequest.pod_name = serverSetup.pod_name;
            var request = HttpRequestBuilder.CreatePost(this.dsmServerUrl + "/dsm/namespaces/{namespace}/servers/shutdown")
                .WithPathParam("namespace", this.namespace_)
                .WithBearerAuth(accessToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .WithBody(shutdownServerRequest.ToUtf8Json())
                .GetResult();

            IHttpResponse response = null;

            yield return this.httpWorker.SendRequest(request, rsp => response = rsp);

            var result = response.TryParse();
            serverType = ServerType.NONE;
            callback.Try(result);
        }

        public IEnumerator SendHeartBeat(string name, string accessToken, ResultCallback<MatchRequest> callback)
        {
            if (serverType == ServerType.CLOUDSERVER)
            {
                name = serverSetup.pod_name;
            }
            Assert.IsNotNull(accessToken, "Can't update a slot! accessToken parameter is null!");
            string reqUrl;
            switch (serverType)
            {
                case ServerType.CLOUDSERVER: reqUrl = dsmServerUrl + "/dsm"; break;
                case ServerType.LOCALSERVER:
                default: reqUrl = this.baseUrl; break; 
            }
            var request = HttpRequestBuilder.CreatePost(reqUrl + "/namespaces/{namespace}/servers/heartbeat")
                .WithPathParam("namespace", this.namespace_)
                .WithBearerAuth(accessToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .WithBody(string.Format("{{\"name\": \"{0}\"}}", name))
                .GetResult();

            IHttpResponse response = null;

            yield return this.httpWorker.SendRequest(request, rsp => response = rsp);

            if (response.BodyBytes == null || response.BodyBytes.Length == 0)
            {
                callback.Try(null);
            }
            else
            {
                var result = response.TryParseJson<MatchRequest>();
                callback.Try(result);
            }
        }

        public IEnumerator RegisterLocalServer(RegisterLocalServerRequest registerRequest, string accessToken,
            ResultCallback callback)
        {
            Assert.IsNotNull(registerRequest, "Register failed. registerRequest is null!");
            Assert.IsNotNull(accessToken, "Can't update a slot! accessToken parameter is null!");

            if (this.serverType != ServerType.NONE)
            {
                callback.TryError(ErrorCode.Conflict, "Server is already registered.");

                yield break;
            }

            var request = HttpRequestBuilder.CreatePost(this.baseUrl + "/namespaces/{namespace}/servers/local/register")
                .WithPathParam("namespace", this.namespace_)
                .WithBearerAuth(accessToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .WithBody(registerRequest.ToUtf8Json())
                .GetResult();

            IHttpResponse response = null;

            yield return this.httpWorker.SendRequest(request, rsp => response = rsp);

            var result = response.TryParse();
            serverType = ServerType.LOCALSERVER;

            callback.Try(result);
        }
        
        public IEnumerator DeregisterLocalServer(string name, string accessToken, ResultCallback callback)
        {
            Assert.IsNotNull(name, "Deregister failed. name is null!");
            Assert.IsNotNull(accessToken, "Can't update a slot! accessToken parameter is null!");

            if (this.serverType != ServerType.LOCALSERVER)
            {
                callback.TryError(ErrorCode.Conflict, "Server not registered as Local Server.");

                yield break;
            }

            var request = HttpRequestBuilder.CreatePost(this.baseUrl + "/namespaces/{namespace}/servers/local/deregister")
                .WithPathParam("namespace", this.namespace_)
                .WithBearerAuth(accessToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .WithBody(string.Format("{{\"name\": \"{0}\"}}", name))
                .GetResult();

            IHttpResponse response = null;

            yield return this.httpWorker.SendRequest(request, rsp => response = rsp);

            var result = response.TryParse();
            serverType = ServerType.NONE;

            callback.Try(result);
        }
    }
}