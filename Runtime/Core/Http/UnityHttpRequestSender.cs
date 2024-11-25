﻿// Copyright (c) 2021 - 2024 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace AccelByte.Core
{
    internal class UnityHttpRequestSender : IHttpRequestSender
    {
        WebRequestScheduler httpTaskScheduler;

        private IDebugger logger;
        private CoreHeartBeat heartBeat;

        private static HashSet<string> clearedCookiesUrl;
        
        public UnityHttpRequestSender(WebRequestScheduler httpTaskScheduler)
        {
            this.httpTaskScheduler = httpTaskScheduler;
            if (clearedCookiesUrl == null)
            {
                clearedCookiesUrl = new HashSet<string>();
            }
        }

        public void SetLogger(IDebugger logger)
        {
            this.logger = logger;
            httpTaskScheduler?.SetLogger(logger);
        }

        internal void SetHeartBeat(CoreHeartBeat coreHeartBeat)
        {
            heartBeat = coreHeartBeat;
            httpTaskScheduler?.SetHeartBeat(coreHeartBeat);
        }

        public void AddTask(IHttpRequest request, Action<HttpSendResult> callback, int timeoutMs, uint delayTimeMs = 0)
        {
            WebRequestTask newTask = new WebRequestTask(request, timeoutMs, delayTimeMs)
            {
                OnComplete = (sentWebRequest) =>
                {
                    HttpSendResult responseResult = ParseWebRequestResult(sentWebRequest);
                    callback?.Invoke(responseResult);
                }
            };
            heartBeat.Wait(new WaitAFrameCommand(cancellationToken: new System.Threading.CancellationTokenSource().Token, onDone:
                () =>
                {
                    httpTaskScheduler.ExecuteWebTask(newTask);
                }));
        }

        public void ClearTasks()
        {
            this.httpTaskScheduler.Stop();
        }

        public void ClearCookies(Uri uri)
        {
            ClearCookiesSafeThread(uri);
        }
        
        internal Models.AccelByteResult<Error> ClearCookiesSafeThread(Uri uri)
        {
            var retval = new Models.AccelByteResult<Error>();
            string uriString = uri.ToString();

            if (clearedCookiesUrl.Contains(uriString) || string.IsNullOrEmpty(uriString))
            {
                retval.Resolve();                
            }
            else
            {
                try
                {
                    if (heartBeat != null)
                    {
                        heartBeat.Wait(new WaitAFrameCommand(
                            cancellationToken: new System.Threading.CancellationTokenSource().Token, onDone: () =>
                            {
                                if (!clearedCookiesUrl.Contains(uriString))
                                {
                                    clearedCookiesUrl.Add(uriString);
                                    UnityWebRequest.ClearCookieCache(uri);
                                }

                                retval.Resolve();
                            }));
                    }
                    else
                    {
                        UnityWebRequest.ClearCookieCache(uri);
                        retval.Resolve();
                    }
                }
                catch (Exception ex)
                {
                    retval.Reject(new Error(ErrorCode.ErrorFromException, ex.Message));
                }
            }

            return retval;
        }

        private HttpSendResult ParseWebRequestResult(UnityWebRequest unityWebRequest)
        {
            IHttpResponse callBackResponse = null;
            Error callBackError = null;
#if UNITY_2020_3_OR_NEWER
            switch (unityWebRequest.result)
            {
                case UnityWebRequest.Result.Success:
                case UnityWebRequest.Result.ProtocolError:
                case UnityWebRequest.Result.DataProcessingError:
                    callBackResponse = unityWebRequest.GetHttpResponse();
                    callBackError = null;
                    break;
                case UnityWebRequest.Result.ConnectionError:
                    callBackResponse = null;
                    callBackError = new Error(ErrorCode.NetworkError);
                    break;
            }
#else
            if (unityWebRequest.isNetworkError)
            {
                callBackResponse = null;
                callBackError = new Error(ErrorCode.NetworkError);
            }
            else
            {
                callBackResponse = unityWebRequest.GetHttpResponse();
                callBackError = null;
            }
#endif
            return new HttpSendResult(callBackResponse, callBackError);
        }
    }
}