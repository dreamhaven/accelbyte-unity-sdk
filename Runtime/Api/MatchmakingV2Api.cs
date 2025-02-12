﻿// Copyright (c) 2022 - 2023 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using AccelByte.Core;
using AccelByte.Models;
using AccelByte.Utils;
using JetBrains.Annotations;
using System.Collections;

namespace AccelByte.Api
{
    public class MatchmakingV2Api : ApiBase
    {
        /// <summary>
        /// Api class for MatchmakingV2 service
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="config">baseUrl==MatchmakingV2ServerUrl</param>
        /// <param name="session"></param>
        [UnityEngine.Scripting.Preserve]
        public MatchmakingV2Api(IHttpClient httpClient
            , Config config
            , ISession session)
            : base(httpClient, config, config.MatchmakingV2ServerUrl, session)
        {
        }

        public IEnumerator CreateMatchmakingTicket(string matchPoolName
            , [CanBeNull] MatchmakingV2CreateTicketRequestOptionalParams optionalParams
            , ResultCallback<MatchmakingV2CreateTicketResponse> callback)
        {
            var error = ApiHelperUtils.CheckForNullOrEmpty(
                Namespace_
                , AuthToken
                , matchPoolName
            );
            if (error != null)
            {
                callback.TryError(error);
                yield break;
            }

            var requestBody = new MatchmakingV2CreateTicketRequest()
            {
                matchPool = matchPoolName
            };

            if (optionalParams != null)
            {
                if (optionalParams.attributes != null)
                {
                    requestBody.attributes = optionalParams.attributes;
                }
                if (optionalParams.latencies != null)
                {
                    requestBody.latencies = optionalParams.latencies;
                }
                if (!string.IsNullOrEmpty(optionalParams.sessionId))
                {
                    requestBody.sessionId = optionalParams.sessionId;
                }
                if (optionalParams.ExcludedGameSessionIds != null)
                {
                    requestBody.ExcludedSessions = optionalParams.ExcludedGameSessionIds;
                }
            }

            var request = HttpRequestBuilder
                .CreatePost(BaseUrl + "/v1/namespaces/{namespace}/match-tickets")
                .WithPathParam("namespace", Namespace_)
                .WithBearerAuth(AuthToken)
                .WithBody(requestBody.ToUtf8Json())
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

            HttpOperator.SendRequest(request, response =>
            {
                var result = response.TryParseJson<MatchmakingV2CreateTicketResponse>();
                callback?.Try(result);
            });

            yield break;
        }

        public IEnumerator GetMatchmakingTicket(string ticketId
            , ResultCallback<MatchmakingV2MatchTicketStatus> callback)
        {
            var error = ApiHelperUtils.CheckForNullOrEmpty(
                Namespace_
                , AuthToken
                , ticketId
            );
            if (error != null)
            {
                callback.TryError(error);
                yield break;
            }

            var request = HttpRequestBuilder
                .CreateGet(BaseUrl + "/v1/namespaces/{namespace}/match-tickets/{ticketId}")
                .WithPathParam("namespace", Namespace_)
                .WithPathParam("ticketId", ticketId)
                .WithBearerAuth(AuthToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

            IHttpResponse response = null;

            yield return HttpClient.SendRequest(request,
                rsp => response = rsp);

            var result = response.TryParseJson<MatchmakingV2MatchTicketStatus>();

            callback.Try(result);
        }

        public IEnumerator DeleteMatchmakingTicket(string ticketId, ResultCallback callback)
        {
            var error = ApiHelperUtils.CheckForNullOrEmpty(
                Namespace_
                , AuthToken
                , ticketId
            );
            if (error != null)
            {
                callback.TryError(error);
                yield break;
            }

            var request = HttpRequestBuilder
                .CreateDelete(BaseUrl + "/v1/namespaces/{namespace}/match-tickets/{ticketId}")
                .WithPathParam("namespace", Namespace_)
                .WithPathParam("ticketId", ticketId)
                .WithBearerAuth(AuthToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

            IHttpResponse response = null;

            yield return HttpClient.SendRequest(request,
                rsp => response = rsp);

            var result = response.TryParse();

            callback.Try(result);
        }

        public IEnumerator GetMatchmakingMetrics(string matchPool, ResultCallback<MatchmakingV2Metrics> callback)
        {
            var error = ApiHelperUtils.CheckForNullOrEmpty(
                Namespace_
                , AuthToken
                , matchPool
            );
            if (error != null)
            {
                callback.TryError(error);
                yield break;
            }

            var request = HttpRequestBuilder
                .CreateGet(BaseUrl + "/v1/namespaces/{namespace}/match-pools/{matchPool}/metrics")
                .WithPathParam("namespace", Namespace_)
                .WithPathParam("matchPool", matchPool)
                .WithBearerAuth(AuthToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

            IHttpResponse response = null;

            yield return HttpClient.SendRequest(request,
                rsp => response = rsp);

            var result = response.TryParseJson<MatchmakingV2Metrics>();

            callback.Try(result);
        }

        public IEnumerator GetUserMatchmakingTickets(ResultCallback<MatchmakingV2ActiveTickets> callback
            , string matchPool, int offset, int limit)
        {
            if (string.IsNullOrEmpty(Namespace_))
            {
                callback.TryError(ErrorCode.NamespaceNotFound);
                yield break;
            }

            if (string.IsNullOrEmpty(AuthToken))
            {
                callback.TryError(ErrorCode.UnauthorizedAccess);
                yield break;
            }

            var request = HttpRequestBuilder
                .CreateGet(BaseUrl + "/v1/namespaces/{namespace}/match-tickets/me")
                .WithPathParam("namespace", Namespace_)
                .WithQueryParam("matchPool", matchPool)
                .WithQueryParam("offset", offset.ToString())
                .WithQueryParam("limit", limit.ToString())
                .WithBearerAuth(AuthToken)
                .WithContentType(MediaType.ApplicationJson)
                .Accepts(MediaType.ApplicationJson)
                .GetResult();

            IHttpResponse response = null;

            yield return HttpClient.SendRequest(request, rsp =>
            {
                response = rsp;
            });

            var result = response.TryParseJson<MatchmakingV2ActiveTickets>();

            callback.Try(result);
        }
    }
}