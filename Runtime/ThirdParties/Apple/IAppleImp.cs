// Copyright (c) 2024 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager

namespace AccelByte.ThirdParties.Apple
{
    public interface IAppleImp
    {
        Models.AccelByteResult<GetAppleTokenResult, Core.Error> GetAppleIdToken();
    }
}