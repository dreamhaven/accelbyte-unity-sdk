﻿// Copyright (c) 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections;
using AccelByte.Core;
using AccelByte.Models;
using UnityEngine;
using UnityEngine.Assertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AccelByte.Server
{
    public class ServerWatchdog
    {
        #region Events

        /// <summary>
        /// Raised when DS is connected to watchdog
        /// </summary>
        public event Action OnOpen;

        /// <summary>
        /// Raised when DS got message from server that it will disconnect
        /// </summary>
        public event ResultCallback<DisconnectNotif> Disconnecting;

        /// <summary>
        /// Raised when DS is disconnected from watchdog
        /// </summary>
        public event Action<WsCloseCode> Disconnected;

        // <summary>
        /// Raised when DS receive drain command
        /// </summary>
        public event Action OnDrainReceived;

        #endregion

        private ServerWatchdogWebsocketApi websocketApi;
        private readonly CoroutineRunner coroutineRunner;
        private Coroutine heartBeatCoroutine;
        private TimeSpan heartBeatInterval = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Event triggered each time a websocket reconnection attempt failed
        /// </summary>
        public event EventHandler OnRetryAttemptFailed
        {
            add => websocketApi.WebSocket.OnRetryAttemptFailed += value;
            remove => websocketApi.WebSocket.OnRetryAttemptFailed -= value;
        }

        /// <summary>
        /// Watchdog connection status
        /// </summary>
        public bool IsConnected => websocketApi.IsConnected;

        internal ServerWatchdog(ServerConfig config, CoroutineRunner inCoroutineRunner)
        {
            Assert.IsNotNull(inCoroutineRunner);

            coroutineRunner = inCoroutineRunner;
            websocketApi = new ServerWatchdogWebsocketApi(inCoroutineRunner, config.WatchdogServerUrl);
            heartBeatInterval = TimeSpan.FromSeconds(config.WatchdogHeartbeatInterval);
            websocketApi.OnOpen += HandleOnOpen;
            websocketApi.OnMessage += HandleOnMessage;
            websocketApi.OnClose += HandleOnClose;
        }

        /// <summary>
        /// Connect to Watchdog with provided ds id.
        /// The token generator need to be set for connection with entitlement verification.
        /// </summary>
        public virtual void Connect(string dsId)
        {
            Report.GetFunctionLog(GetType().Name);

            if (dsId == null || dsId.Length == 0)
            {
                AccelByteDebug.LogWarning("dsid not provided, not connecting to watchdog");
            }
            else
            {
                websocketApi.Connect(dsId);
            }
        }

        /// <summary>
        /// Change the delay parameters to maintain connection to Watchdog.
        /// </summary>
        /// <param name="inTotalTimeout">Time limit until stop to re-attempt</param>
        /// <param name="inBackoffDelay">Initial delay time</param>
        /// <param name="inMaxDelay">Maximum delay time</param>
        public void SetRetryParameters(int inTotalTimeout = 60000
            , int inBackoffDelay = 1000
            , int inMaxDelay = 30000)
        {
            websocketApi.SetRetryParameters(inTotalTimeout, inBackoffDelay, inMaxDelay);
        }

        /// <summary>
        /// Disconnect from Watchdog.
        /// </summary>
        public virtual void Disconnect()
        {
            Report.GetFunctionLog(GetType().Name);

            StopHeartBeatScheduler();
            websocketApi.Disconnect();
        }

        /// <summary>
        /// Send ready message to Watchdog.
        /// </summary>
        public void SendReadyMessage()
        {
            websocketApi.SendReadyMessage();
            StartHeartBeatScheduler();
        }

        #region private methods
        private void HandleOnOpen()
        {
            // debug ws connection
#if DEBUG
            AccelByteDebug.LogVerbose("[WS] Connection open");
#endif
            Action handler = OnOpen;

            if (handler != null)
            {
                handler();
            }
        }

        private void HandleOnClose(ushort closecode)
        {
            // debug ws connection
#if DEBUG
            AccelByteDebug.LogVerbose("[WS] Connection close: " + closecode);
#endif
            var code = (WsCloseCode)closecode;
            Disconnected?.Invoke(code);
        }

        private void HandleOnMessage(string message)
        {
            Report.GetWebSocketResponse(message);

            try
            {
                var command = JsonConvert.DeserializeObject<JObject>(message);
                if (command.ContainsKey("drain"))
                {
                    // enter drain mode
                    AccelByteDebug.LogWarning("Enter drain mode");
                    OnDrainReceived?.Invoke();
                }
            }
            catch (Exception ex)
            {
                AccelByteDebug.LogWarning(ex.Message);
            }
        }

        private IEnumerator RunPeriodicHeartBeat()
        {
            while (true)
            {
                yield return new WaitForSeconds((float)heartBeatInterval.TotalSeconds);
                websocketApi.SendHeartBeat();
            }
        }

        private void StartHeartBeatScheduler()
        {
            if (heartBeatCoroutine != null)
            {
                StopHeartBeatScheduler();
            }

            this.heartBeatCoroutine = this.coroutineRunner.Run(RunPeriodicHeartBeat());
        }

        private void StopHeartBeatScheduler()
        {
            if (heartBeatCoroutine == null)
            {
                return;
            }

            coroutineRunner.Stop(this.heartBeatCoroutine);
            this.heartBeatCoroutine = null;
        }

        #endregion
    }
}
