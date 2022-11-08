using UnityEngine;

using PersistentTypeID = System.Int32;
using LocalIdentifierInFileType = System.Int64;

// ReSharper disable BuiltInTypeReferenceStyle
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable UnusedMember.Global

#pragma warning disable IDE0044
#pragma warning disable CS0169
#pragma warning disable CS0649
#pragma warning disable IDE0051

namespace OkTools.Unity.AssetDb;

public struct CurrentRevision
{
    public BlobArtifactKey ArtifactKey;
    public ArtifactId ArtifactId;
}

public struct BlobArtifactKey
{
    public UnityGuid Guid;
    public BlobImporterId ImporterId;

    public const string CsvHeader = "UnityGuid," + BlobImporterId.CsvHeader;
    public string ToCsv() => $"{Guid},{ImporterId.ToCsv()}";
}

public struct BlobImporterId
{
    public Int32 NativeImporterType;
    public Hash128 ScriptedImporterType;

    public const string CsvHeader = "NativeImporterType,ScriptedImporterType";
    public string ToCsv() => $"{NativeImporterType},{ScriptedImporterType}";
}

public struct ArtifactId
{
    public Hash128 Hash;
}

struct ArtifactIdsBlob
{
    public BlobArtifactKey ArtifactKey;
    public BlobArray<ArtifactId> Ids;
}

public struct ArtifactIds
{
    public BlobArtifactKey ArtifactKey;
    public ArtifactId[] Ids;
}

struct ArtifactImportStatsBlob
{
    // Editor
    public UInt64           ImportTimeMicroseconds;
    public BlobString       ArtifactPath;
    public Int64            ImportedTimestamp;
    public BlobString       EditorRevision;
    public BlobString       UserName;

    // Cache Server
    public UInt16           ReliabilityIndex;
    public Int64            UploadedTimestamp;
    public BlobString       UploadIpAddress;
}

public struct ArtifactImportStats
{
    // Editor
    public UInt64           ImportTimeMicroseconds;
    public string           ArtifactPath;
    public Int64            ImportedTimestamp;
    public string           EditorRevision;
    public string           UserName;

    // Cache Server
    public UInt16           ReliabilityIndex;
    public Int64            UploadedTimestamp;
    public string           UploadIpAddress;

    public const string CsvHeader = "ImportTimeMicroseconds,ArtifactPath,ImportedTimestamp,EditorRevision,UserName,ReliabilityIndex,UploadedTimestamp,UploadIpAddress";
    public string ToCsv() => $"{ImportTimeMicroseconds},{ArtifactPath},{ImportedTimestamp},{EditorRevision},{UserName},{ReliabilityIndex},{UploadedTimestamp},{UploadIpAddress}";

    internal static unsafe ArtifactImportStats Create(ArtifactImportStatsBlob* blob) => new()
    {
        ImportTimeMicroseconds = blob->ImportTimeMicroseconds,
        ArtifactPath = blob->ArtifactPath.GetStringFromBlob(),
        ImportedTimestamp = blob->ImportedTimestamp,
        EditorRevision = blob->EditorRevision.GetStringFromBlob(),
        UserName = blob->UserName.GetStringFromBlob(),
        ReliabilityIndex = blob->ReliabilityIndex,
        UploadedTimestamp = blob->UploadedTimestamp,
        UploadIpAddress = blob->UploadIpAddress.GetStringFromBlob(),
    };
}

enum AssetType
{
    // Used to indicate that the asset is unknown to an asset importer
    kUnknownAsset = 0,
    // An importer imports the data from the asset path (e.g. texture) and converts it into a serialized representation.
    // Nothing can be modified in the inspector and it will only be saved after a reimport.
    // Imported assets, e.g fbx file, as well as generated default assets.
    kCopyAsset = 1 << 0,
    // A serialized asset, that is modified in the editor and stored in the assets path.
    // Automatically saved with the project when saving.
    // Unity native assets, e.g material.
    kSerializedAsset = 1 << 1,
    // This has folder semantics (No extra data, may contain children)
    kFolderAsset = 1 << 2,

    kUnused1 = 1 << 30,
    kUnused2 = 1 << 31,
}

struct ArtifactMetaInfoBlob
{
    ArtifactMetaInfoHash                    artifactMetaInfoHash;
    BlobArtifactKey                         artifactKey;
    AssetType                               type;
    bool                                    isImportedAssetCacheable;
    BlobArray<ArtifactFileMetaInfo>         producedFiles;
    BlobArray<BlobProperty>                 properties;
    BlobArray<ImportedAssetMetaInfo>        importedAssetMetaInfos;
}

struct ArtifactMetaInfoHash
{
    Hash128 value;
}

struct ArtifactFileMetaInfo
{
    ArtifactFileStorage storage;
    BlobString  extension;
    Hash128     contentHash;
    BlobArray<Byte> inlineStorage;
}


enum ArtifactFileStorage
{
    Library,  // uses extension for filename in Library folder
    Inline    // uses the inlineStorage
}

struct BlobProperty
{
    BlobString       id;
    BlobArray<Byte> data;
}

struct ImportedAssetMetaInfo
{
    bool                                    postProcessedAsset;
    ImportedObjectMetaInfo                  mainObjectInfo;
    BlobArray<ImportedObjectMetaInfo>       objectInfo;
}

struct ImportedObjectMetaInfo
{
    BlobString                  name;
    BlobImage                   thumbnail;
    PersistentTypeID            typeID;
    UInt32                      flags;
    ScriptClassNameBlob         scriptClassName;
    LocalIdentifierInFileType   localIdentifier;
}

struct BlobImage
{
    GraphicsFormat format;
    Int32  width;
    Int32  height;
    Int32  rowBytes;
    BlobArray<Byte> image;
}

enum GraphicsFormat
{
    kFormatUnknown = -1,
    kFormatNone = 0, kFormatFirst = kFormatNone,

    // sRGB formats
    kFormatR8_SRGB,
    kFormatR8G8_SRGB,
    kFormatR8G8B8_SRGB,
    kFormatR8G8B8A8_SRGB,

    // 8 bit integer formats
    kFormatR8_UNorm,
    kFormatR8G8_UNorm,
    kFormatR8G8B8_UNorm,
    kFormatR8G8B8A8_UNorm,
    kFormatR8_SNorm,
    kFormatR8G8_SNorm,
    kFormatR8G8B8_SNorm,
    kFormatR8G8B8A8_SNorm,
    kFormatR8_UInt,
    kFormatR8G8_UInt,
    kFormatR8G8B8_UInt,
    kFormatR8G8B8A8_UInt,
    kFormatR8_SInt,
    kFormatR8G8_SInt,
    kFormatR8G8B8_SInt,
    kFormatR8G8B8A8_SInt,

    // 16 bit integer formats
    kFormatR16_UNorm,
    kFormatR16G16_UNorm,
    kFormatR16G16B16_UNorm,
    kFormatR16G16B16A16_UNorm,
    kFormatR16_SNorm,
    kFormatR16G16_SNorm,
    kFormatR16G16B16_SNorm,
    kFormatR16G16B16A16_SNorm,
    kFormatR16_UInt,
    kFormatR16G16_UInt,
    kFormatR16G16B16_UInt,
    kFormatR16G16B16A16_UInt,
    kFormatR16_SInt,
    kFormatR16G16_SInt,
    kFormatR16G16B16_SInt,
    kFormatR16G16B16A16_SInt,

    // 32 bit integer formats
    kFormatR32_UInt,
    kFormatR32G32_UInt,
    kFormatR32G32B32_UInt,
    kFormatR32G32B32A32_UInt,
    kFormatR32_SInt,
    kFormatR32G32_SInt,
    kFormatR32G32B32_SInt,
    kFormatR32G32B32A32_SInt,

    // HDR formats
    kFormatR16_SFloat,
    kFormatR16G16_SFloat,
    kFormatR16G16B16_SFloat,
    kFormatR16G16B16A16_SFloat,
    kFormatR32_SFloat,
    kFormatR32G32_SFloat,
    kFormatR32G32B32_SFloat,
    kFormatR32G32B32A32_SFloat,

    // Luminance and Alpha format
    kFormatL8_UNorm,
    kFormatA8_UNorm,
    kFormatA16_UNorm,

    // BGR formats
    kFormatB8G8R8_SRGB,
    kFormatB8G8R8A8_SRGB,
    kFormatB8G8R8_UNorm,
    kFormatB8G8R8A8_UNorm,
    kFormatB8G8R8_SNorm,
    kFormatB8G8R8A8_SNorm,
    kFormatB8G8R8_UInt,
    kFormatB8G8R8A8_UInt,
    kFormatB8G8R8_SInt,
    kFormatB8G8R8A8_SInt,

    // 16 bit packed formats
    kFormatR4G4B4A4_UNormPack16,
    kFormatB4G4R4A4_UNormPack16,
    kFormatR5G6B5_UNormPack16,
    kFormatB5G6R5_UNormPack16,
    kFormatR5G5B5A1_UNormPack16,
    kFormatB5G5R5A1_UNormPack16,
    kFormatA1R5G5B5_UNormPack16,

    // Packed formats
    kFormatE5B9G9R9_UFloatPack32,
    kFormatB10G11R11_UFloatPack32,

    kFormatA2B10G10R10_UNormPack32,
    kFormatA2B10G10R10_UIntPack32,
    kFormatA2B10G10R10_SIntPack32,
    kFormatA2R10G10B10_UNormPack32,
    kFormatA2R10G10B10_UIntPack32,
    kFormatA2R10G10B10_SIntPack32,
    kFormatA2R10G10B10_XRSRGBPack32,
    kFormatA2R10G10B10_XRUNormPack32,
    kFormatR10G10B10_XRSRGBPack32,
    kFormatR10G10B10_XRUNormPack32,
    kFormatA10R10G10B10_XRSRGBPack32,
    kFormatA10R10G10B10_XRUNormPack32,

    // ARGB formats... TextureFormat legacy
    kFormatA8R8G8B8_SRGB,
    kFormatA8R8G8B8_UNorm,
    kFormatA32R32G32B32_SFloat,

    // Depth Stencil for formats
    kFormatD16_UNorm,
    kFormatD24_UNorm,
    kFormatD24_UNorm_S8_UInt,
    kFormatD32_SFloat,
    kFormatD32_SFloat_S8_UInt,
    kFormatS8_UInt,

    // Compression formats
    kFormatRGBA_DXT1_SRGB, kFormatDXTCFirst = kFormatRGBA_DXT1_SRGB,
    kFormatRGBA_DXT1_UNorm,
    kFormatRGBA_DXT3_SRGB,
    kFormatRGBA_DXT3_UNorm,
    kFormatRGBA_DXT5_SRGB,
    kFormatRGBA_DXT5_UNorm, kFormatDXTCLast = kFormatRGBA_DXT5_UNorm,
    kFormatR_BC4_UNorm, kFormatRGTCFirst = kFormatR_BC4_UNorm,
    kFormatR_BC4_SNorm,
    kFormatRG_BC5_UNorm,
    kFormatRG_BC5_SNorm, kFormatRGTCLast = kFormatRG_BC5_SNorm,
    kFormatRGB_BC6H_UFloat, kFormatBPTCFirst = kFormatRGB_BC6H_UFloat,
    kFormatRGB_BC6H_SFloat,
    kFormatRGBA_BC7_SRGB,
    kFormatRGBA_BC7_UNorm, kFormatBPTCLast = kFormatRGBA_BC7_UNorm,

    kFormatRGB_PVRTC_2Bpp_SRGB, kFormatPVRTCFirst = kFormatRGB_PVRTC_2Bpp_SRGB,
    kFormatRGB_PVRTC_2Bpp_UNorm,
    kFormatRGB_PVRTC_4Bpp_SRGB,
    kFormatRGB_PVRTC_4Bpp_UNorm,
    kFormatRGBA_PVRTC_2Bpp_SRGB,
    kFormatRGBA_PVRTC_2Bpp_UNorm,
    kFormatRGBA_PVRTC_4Bpp_SRGB,
    kFormatRGBA_PVRTC_4Bpp_UNorm, kFormatPVRTCLast = kFormatRGBA_PVRTC_4Bpp_UNorm,

    kFormatRGB_ETC_UNorm, kFormatETCFirst = kFormatRGB_ETC_UNorm, kFormatETC1First = kFormatRGB_ETC_UNorm, kFormatETC1Last = kFormatRGB_ETC_UNorm,
    kFormatRGB_ETC2_SRGB, kFormatETC2First = kFormatRGB_ETC2_SRGB,
    kFormatRGB_ETC2_UNorm,
    kFormatRGB_A1_ETC2_SRGB,
    kFormatRGB_A1_ETC2_UNorm,
    kFormatRGBA_ETC2_SRGB,
    kFormatRGBA_ETC2_UNorm, kFormatETCLast = kFormatRGBA_ETC2_UNorm, kFormatETC2Last = kFormatRGBA_ETC2_UNorm,

    kFormatR_EAC_UNorm, kFormatEACFirst = kFormatR_EAC_UNorm,
    kFormatR_EAC_SNorm,
    kFormatRG_EAC_UNorm,
    kFormatRG_EAC_SNorm, kFormatEACLast = kFormatRG_EAC_SNorm,

    kFormatRGBA_ASTC4X4_SRGB, kFormatASTCFirst = kFormatRGBA_ASTC4X4_SRGB,
    kFormatRGBA_ASTC4X4_UNorm,
    kFormatRGBA_ASTC5X5_SRGB,
    kFormatRGBA_ASTC5X5_UNorm,
    kFormatRGBA_ASTC6X6_SRGB,
    kFormatRGBA_ASTC6X6_UNorm,
    kFormatRGBA_ASTC8X8_SRGB,
    kFormatRGBA_ASTC8X8_UNorm,
    kFormatRGBA_ASTC10X10_SRGB,
    kFormatRGBA_ASTC10X10_UNorm,
    kFormatRGBA_ASTC12X12_SRGB,
    kFormatRGBA_ASTC12X12_UNorm, kFormatASTCLast = kFormatRGBA_ASTC12X12_UNorm,

    // Video formats
    kFormatYUV2,

    // Automatic formats, back-end decides
    kFormatDepthAuto_removed_donotuse,
    kFormatShadowAuto_removed_donotuse,
    kFormatVideoAuto_removed_donotuse,

    kFormatRGBA_ASTC4X4_UFloat, kFormatASTCHDRFirst = kFormatRGBA_ASTC4X4_UFloat,
    kFormatRGBA_ASTC5X5_UFloat,
    kFormatRGBA_ASTC6X6_UFloat,
    kFormatRGBA_ASTC8X8_UFloat,
    kFormatRGBA_ASTC10X10_UFloat,
    kFormatRGBA_ASTC12X12_UFloat, kFormatASTCHDRLast = kFormatRGBA_ASTC12X12_UFloat,

    kFormatD16_UNorm_S8_UInt,

    kFormatLast = kFormatD16_UNorm_S8_UInt, // Remove?
};

struct ScriptClassNameBlob
{
    // If guid in monoScript is invalid, then use name
    BlobString                  name;
    BlobPPtr                    monoScript;
};

struct BlobPPtr
{
    UnityGuid                               guid;
    LocalIdentifierInFileType               localIdentifier;
    Int32                                   type;
};
