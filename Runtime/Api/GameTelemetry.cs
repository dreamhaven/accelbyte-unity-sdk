// Copyright (c) 2020 - 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AccelByte.Core;
using AccelByte.Models;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

namespace AccelByte.Api
{
    /// <summary>
    /// Send telemetry data securely and the game user should be logged in.
    /// </summary>
    public class GameTelemetry : WrapperBase
    {
        private readonly GameTelemetryApi api;
        private readonly UserSession session;

        private TimeSpan telemetryInterval = TimeSpan.FromMinutes(1);
        private HashSet<string> immediateEvents = new HashSet<string>();
        private ConcurrentQueue<Tuple<TelemetryBody, ResultCallback>> jobQueue =
            new ConcurrentQueue<Tuple<TelemetryBody, ResultCallback>>();

        private List<TelemetryBody> eventList = new List<TelemetryBody>();
        
        private bool isSchedulerRunning = false;
        private static readonly TimeSpan minimumTelemetryInterval = 
            new TimeSpan(0, 0, 5);

        IAccelByteDataStorage cacheStorage;
        string cacheTableName;

        internal TimeSpan TelemetryInterval
        {
            get
            {
                return telemetryInterval;
            }
        }

        [UnityEngine.Scripting.Preserve]
        internal GameTelemetry( GameTelemetryApi inApi
            , UserSession inSession
            , CoroutineRunner inCoroutineRunner )
        {
            Assert.IsNotNull(inApi, "inApi parameter can not be null.");

            api = inApi;
            session = inSession;

            cacheStorage = session.DataStorage;
            cacheTableName = $"GameTelemetryCache/{AccelByteSDK.Environment.Current}.cache";

            if (session.IsValid())
            {
                SendCachedTelemetry();
            }
            else
            {
                Action<string> onGetRefreshToken = null;
                onGetRefreshToken = (newAccessToken) =>
                {
                    session.RefreshTokenCallback -= onGetRefreshToken;
                    SendCachedTelemetry();
                };
                session.RefreshTokenCallback += onGetRefreshToken;
            }
        }

        ~GameTelemetry()
        {
            isSchedulerRunning = false;
        }

        /// <summary>
        /// Set the interval of sending telemetry event to the backend.
        /// By default it sends the queued events once a minute.
        /// Should not be less than 5 seconds.
        /// </summary>
        /// <param name="interval">The interval between telemetry event.</param>
        public void SetBatchFrequency( TimeSpan interval )
        {
            if (interval >= minimumTelemetryInterval)
            {
                telemetryInterval = interval;
            }
            else
            {
                AccelByteDebug.LogWarning($"Telemetry schedule interval is too small! " +
                    $"Set to {minimumTelemetryInterval.TotalSeconds} seconds.");
                telemetryInterval = minimumTelemetryInterval;
            }
        }

        /// <summary>
        /// Set the interval of sending telemetry event to the backend.
        /// By default it sends the queued events once a minute.
        /// Should not be less than 5 seconds.
        /// </summary>
        /// <param name="intervalSeconds">The seconds interval between telemetry event.</param>
        public void SetBatchFrequency(int intervalSeconds)
        {
            SetBatchFrequency(TimeSpan.FromSeconds(intervalSeconds));
        }

        /// <summary>
        /// Set list of event that need to be sent immediately without the needs to jobQueue it.
        /// </summary>
        /// <param name="eventList">String list of payload EventName.</param>
        public void SetImmediateEventList( List<string> eventList )
        {
            immediateEvents = new HashSet<string>(eventList);
        }

        /// <summary>
        /// Send/enqueue a single authorized telemetry data. 
        /// Server should be logged in. See DedicatedServer.LoginWithClientCredentials()
        /// </summary>
        /// <param name="telemetryBody">Telemetry request with arbitrary payload. </param>
        /// <param name="callback">Returns boolean status via callback when completed. </param>
        public void Send( TelemetryBody telemetryBody
            , ResultCallback callback )
        {
            Report.GetFunctionLog(GetType().Name);

            if (immediateEvents.Contains(telemetryBody.EventName))
            {
                SendTelemetryRequest(
                    new List<TelemetryBody> { telemetryBody }
                    , callback
                );
            }
            else
            {
                jobQueue.Enqueue(new Tuple<TelemetryBody, ResultCallback>(telemetryBody, callback));
                AppendEventToCache(telemetryBody);

                if (!isSchedulerRunning)
                {
                    RunPeriodicTelemetry();
                }
            }
        }

        /// <summary>
        /// Send telemetry data in the btach
        /// </summary>
        /// <param name="callback">Callback after sending telemetry data is complete</param>
        public void SendTelemetryBatch(ResultCallback callback)
        {
            ConcurrentQueue<Tuple<TelemetryBody, ResultCallback>> queue = null;
            lock (jobQueue)
            {
                if (jobQueue == null || jobQueue.IsEmpty)
                {
                    callback?.Invoke(Result.CreateOk());
                    return;
                }

                queue = new ConcurrentQueue<Tuple<TelemetryBody, ResultCallback>>(jobQueue);
                jobQueue.Clear();
            }

            var queueBackup = new ConcurrentQueue<Tuple<TelemetryBody, ResultCallback>>(queue);

            List<TelemetryBody> telemetryBodies = new List<TelemetryBody>();
            List<ResultCallback> telemetryCallbacks = new List<ResultCallback>();
            while (!queue.IsEmpty)
            {
                if (queue.TryDequeue(out var dequeueResult))
                {
                    telemetryBodies.Add(dequeueResult.Item1);
                    telemetryCallbacks.Add(dequeueResult.Item2);
                }
            }

            eventList.Clear();
            SendTelemetryRequest(telemetryBodies, result =>
            {
                if (result.IsError)
                {
                    lock (jobQueue)
                    {
                        foreach (var unsentQueue in queueBackup)
                        {
                            jobQueue.Enqueue(unsentQueue);
                        }
                    }
                }
                else
                {
                    RemoveEventsFromCache();
                }

                foreach (var telemetryCallback in telemetryCallbacks)
                {
                    telemetryCallback?.Invoke(result);
                }
                callback?.Invoke(result);
            });
        }

        private void SendCachedTelemetry()
        {
            LoadEventsFromCache(telemetryBodies =>
            {
                SendTelemetryRequest(telemetryBodies, cb =>
                {
                    if (!cb.IsError)
                    {
                        RemoveEventsFromCache();
                    }
                });
            });
        }

        internal void ClearQueuedTelemetry()
        {
            lock(jobQueue)
            {
                jobQueue.Clear();
            }
        }

        private async void RunPeriodicTelemetry()
        {
            if(isSchedulerRunning)
            {
                return;
            }

            isSchedulerRunning = true;
            while (isSchedulerRunning && session != null && session.IsValid())
            {
                SendTelemetryBatch(callback: null);

                await System.Threading.Tasks.Task.Delay(telemetryInterval);
            }
            isSchedulerRunning = false;
        }

        internal void LoadEventsFromCache(Action<List<TelemetryBody>> onLoadCacheDone)
        {
            if (!session.IsValid())
            {
                onLoadCacheDone?.Invoke(null);
                return;
            }

            LoadEventsFromCache(session.UserId, onLoadCacheDone);
        }

        internal void LoadEventsFromCache(string key, Action<List<TelemetryBody>> onLoadCacheDone)
        {
            cacheStorage.GetItem(
                key
                , tableName: cacheTableName
                , onDone: (bool isSuccess, string loadedTelemetry) =>
                {
                    if (isSuccess && !string.IsNullOrEmpty(loadedTelemetry))
                    {
                        List<TelemetryBody> telemetryBodies = new List<TelemetryBody>();
                        var eventListDict = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(loadedTelemetry);
                        foreach (var item in eventListDict)
                        {
                            TelemetryBody telemetryBody = new TelemetryBody();
                            telemetryBody.EventName = item["EventName"].ToString();
                            telemetryBody.EventNamespace = item["EventNamespace"].ToString();
                            telemetryBody.Payload = item["Payload"];
                            telemetryBodies.Add(telemetryBody);
                        }
                        onLoadCacheDone?.Invoke(telemetryBodies);
                    }
                    else
                    {
                        onLoadCacheDone?.Invoke(null);
                    }
                }
            );
        }

        internal void AppendEventToCache(TelemetryBody telemetryBody, Action<bool> onSaveCacheDone = null)
        {
            if (!session.IsValid())
            {
                onSaveCacheDone?.Invoke(false);
                return;
            }

            AppendEventToCache(session.UserId, telemetryBody, onSaveCacheDone);
        }

        internal void AppendEventToCache(string key, TelemetryBody telemetryBody, Action<bool> onSaveCacheDone = null)
        {
            eventList.Add(telemetryBody);
            var telemetryEventsJson = System.Text.Encoding.UTF8.GetString(eventList.ToUtf8Json());
            cacheStorage.SaveItem(key, telemetryEventsJson, onSaveCacheDone, tableName: cacheTableName);
        }

        internal void DeleteCache(Action<bool> onDone = null)
        {
            cacheStorage.Reset(result: onDone, tableName: cacheTableName);
        }

        private void RemoveEventsFromCache()
        {
            if (session != null && !session.IsValid())
            {
                return;
            }

            cacheStorage.DeleteItem(session.UserId, onDone: null, tableName:cacheTableName);
        }

        private void SendTelemetryRequest(List<TelemetryBody> telemetryBodies, ResultCallback callback)
        {
            if (telemetryBodies != null && telemetryBodies.Count > 0)
            {
                api.SendProtectedEventsV1(telemetryBodies, callback);
            }
            else
            {
                callback?.Invoke(Result.CreateOk());
            }
        }
    }
}