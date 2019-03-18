// This file was @generated with LibOVRPlatform/codegen/main. Do not modify it!

namespace Oculus.Platform.Models
{
  using System;
  using System.Collections;
  using Models;
  using System.Collections.Generic;
  using UnityEngine;

  public class AssetDetails
  {
    public readonly UInt64 AssetId;
    public readonly string DownloadStatus;
    public readonly string Filepath;
    public readonly string IapStatus;


    public AssetDetails(IntPtr o)
    {
      this.AssetId = CAPI.ovr_AssetDetails_GetAssetId(o);
      this.DownloadStatus = CAPI.ovr_AssetDetails_GetDownloadStatus(o);
      this.Filepath = CAPI.ovr_AssetDetails_GetFilepath(o);
      this.IapStatus = CAPI.ovr_AssetDetails_GetIapStatus(o);
    }
  }

  public class AssetDetailsList : DeserializableList<AssetDetails> {
    public AssetDetailsList(IntPtr a) {
      var count = (int)CAPI.ovr_AssetDetailsArray_GetSize(a);
      this._Data = new List<AssetDetails>(count);
      for (int i = 0; i < count; i++) {
        this._Data.Add(new AssetDetails(CAPI.ovr_AssetDetailsArray_GetElement(a, (UIntPtr)i)));
      }

    }

  }
}
