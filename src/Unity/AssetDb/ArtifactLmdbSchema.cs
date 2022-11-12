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

struct ArtifactDependencies
{
    public ArtifactDependenciesHash                   dependenciesHash;
    public ArtifactStaticDependenciesHash             staticDependencyHash;
    public BlobOffsetPtr<StaticArtifactDependencies>  staticDependencies;
    public BlobOffsetPtr<DynamicArtifactDependencies> dynamicDependencies;
    public ArtifactID                                 artifactID;
}

struct ArtifactDependenciesHash
{
    public Hash128 value;
}

struct ArtifactFileMetaInfo
{
    public ArtifactFileStorage storage;
    public BlobString          extension;
    public Hash128             contentHash;
    public BlobArray<Byte>     inlineStorage;
}

struct ArtifactID
{
    public Hash128 value;
}

struct ArtifactIDs
{
    public BlobArtifactKey       artifactKey;
    public BlobArray<ArtifactID> ids;
}

struct ArtifactImportStats
{
    // Editor
    public UInt64     importTimeMicroseconds;
    public BlobString artifactPath;
    public Int64      importedTimestamp;
    public BlobString editorRevision;
    public BlobString userName;

    // Cache Server
    public UInt16     reliabilityIndex;
    public Int64      uploadedTimestamp;
    public BlobString uploadIpAddress;

    public DateTime   ImportedTimestampAsDateTime => new(importedTimestamp);
    public DateTime   UploadedTimestampAsDateTime => new(uploadedTimestamp);
}

struct BlobArtifactKey // an ArtifactKey that goes in a Blob (does not have all the fields of an ArtifactKey)
{
    public UnityGUID  guid;
    public ImporterId importerId;
}

struct ArtifactMetaInfo
{
    public ArtifactMetaInfoHash             artifactMetaInfoHash;
    public BlobArtifactKey                  artifactKey;
    public AssetType                        type;
    public bool                             isImportedAssetCacheable;
    public BlobArray<ArtifactFileMetaInfo>  producedFiles;
    public BlobArray<BlobProperty>          properties;
    public BlobArray<ImportedAssetMetaInfo> importedAssetMetaInfos;
}

struct ArtifactMetaInfoHash
{
    public Hash128 value;
}

struct ArtifactStaticDependenciesHash
{
    public Hash128 value;
}

struct BlobImage
{
    public GraphicsFormat  format;
    public Int32           width;
    public Int32           height;
    public Int32           rowBytes;
    public BlobArray<Byte> image;
}

struct BlobPPtr
{
    public UnityGUID                 guid;
    public LocalIdentifierInFileType localIdentifier;
    public Int32                     type;
}

struct BlobProperty
{
    public BlobString      id;
    public BlobArray<Byte> data;
}

struct BuildTargetSelection
{
    public BuildTargetPlatform platform;
    public int                 subTarget;
    public int                 extendedPlatform;
    public int                 isEditor;
}

struct CurrentRevision
{
    public BlobArtifactKey artifactKey;
    public ArtifactID      artifactID;
}

struct CustomDependency
{
    public BlobString name;
    public Hash128    valueHash;
}

struct DynamicArtifactDependencies
{
    public BlobArray<HashOfSourceAsset>           hashOfSourceAsset;
    public BlobArray<GUIDOfPathLocationBlob>      guidOfPathLocation;
    public BlobArray<HashOfGUIDsOfChildren>       hashOfGUIDsOfChildren;
    public BlobArray<HashOfArtifact>              hashOfArtifact;
    public BlobArray<PropertyOfArtifact>          propertyOfArtifact;

    public BlobOptional<BuildTargetSelection>     buildTarget;
    public BlobOptional<BuildTargetPlatformGroup> buildTargetPlatformGroup;
    public BlobOptional<TextureImportCompression> textureImportCompression;
    public BlobOptional<ColorSpace>               colorSpace;
    public BlobOptional<UInt32>                   graphicsApiMask;
    public BlobOptional<ScriptingRuntimeVersion>  scriptingRuntimeVersion;
    public BlobArray<CustomDependency>            customDependencies;
}

struct GUIDOfPathLocationBlob
{
    public BlobString path;
    public UnityGUID  guid;
}

struct HashOfArtifact
{
    public BlobArtifactKey artifactKey;
    public ArtifactID artifactID;
}

struct HashOfGUIDsOfChildren
{
    public UnityGUID guid;
    public Hash128   hash;
}

struct HashOfSourceAsset
{
    public UnityGUID guid;
    public Hash128   assetHash;
    public Hash128   metaFileHash;
}

struct ImportedAssetMetaInfo
{
    public bool                              postProcessedAsset;
    public ImportedObjectMetaInfo            mainObjectInfo;
    public BlobArray<ImportedObjectMetaInfo> objectInfo;
}

struct ImportedObjectMetaInfo
{
    public BlobString                name;
    public BlobImage                 thumbnail;
    public PersistentTypeID          typeID;
    public UInt32                    flags;
    public ScriptClassNameBlob       scriptClassName;
    public LocalIdentifierInFileType localIdentifier;
}

struct ImporterId
{
    public PersistentTypeID NativeImporterType;
    public Hash128          ScriptedImporterType;
}

struct PropertyOfArtifact
{
    public BlobArtifactKey artifactKey;
    public BlobProperty    prop;
}

struct ScriptClassNameBlob
{
    public BlobString name;
    public BlobPPtr   monoScript;
}

struct StaticArtifactDependencies
{
    public UInt32                       artifactFormatVersion;
    public UInt32                       allImporterVersion;
    public ImporterId                   importerID;
    public UInt32                       importerVersion;
    public PostprocessorType            postprocessorType;
    public Hash128                      postprocessorVersionHash;
    public BlobString                   nameOfAsset;
    public BlobArray<HashOfSourceAsset> hashOfSourceAsset;
}

// ENUMS

enum ArtifactFileStorage
{
    Library,  // uses extension for filename in Library folder
    Inline    // uses the inlineStorage
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

enum BuildTargetPlatform
{
    kBuildNoTargetPlatform = -2,
    kBuildAnyPlayerData = -1,
    kBuildValidPlayer = 1,

    kFirstValidStandaloneTarget = 2,
    // We don't support building for these any more, but we still need the constants for asset bundle
    // backwards compatibility.
    kBuildDeprecatedStandaloneOSXPPC = 3,

    kBuildDeprecatedStandaloneOSXIntel = 4,

    kBuildDeprecatedWebPlayerLZMA = 6,
    kBuildDeprecatedWebPlayerLZMAStreamed = 7,
    kBuildDeprecatedStandaloneOSXIntel64 = 27,
#pragma warning disable CA1069
    kBuildStandaloneOSX = 2,
#pragma warning restore CA1069
    kBuildStandaloneWinPlayer = 5,
    kBuild_iPhone = 9,
    kBuildDeprecatedPS3 = 10,
    // was kBuildXBOX360 = 11,
    // was kBuild_Broadcom = 12,
    kBuild_Android = 13,
    // was kBuildWinGLESEmu = 14,
    // was kBuildWinGLES20Emu = 15,
    // was kBuildNaCl = 16,
    kBuildDeprecatedStandaloneLinux = 17,
    // was kBuildFlash = 18,
    kBuildStandaloneWin64Player = 19,
    kBuildWebGL = 20,
    kBuildMetroPlayer = 21,
    kBuildStandaloneLinux64 = 24,
    kBuildDeprecatedStandaloneLinuxUniversal = 25,
    kBuildDeprecatedWP8Player = 26,
    kBuildDeprecatedBB10 = 28,
    kBuildDeprecatedTizen = 29,
    kBuildDeprecatedPSP2 = 30,
    kBuildPS4 = 31,
    kBuildDeprecatedPSM = 32,
    kBuildXboxOne = 33,
    kBuildDeprecatedSamsungTV = 34,
    kBuildDeprecatedN3DS = 35,
    kBuildDeprecatedWiiU = 36,
    kBuildtvOS = 37,
    kBuildSwitch = 38,
    kBuildDeprecatedLumin = 39,
    kBuildStadia = 40,
    kBuildCloudRendering = 41,
    kBuildGameCoreScarlett = 42,
    kBuildGameCoreXboxOne = 43,
    kBuildPS5 = 44,
    kBuildEmbeddedLinux = 45,
    kBuildQNX = 46,
    kBuildPlayerTypeCount
}

enum BuildTargetPlatformGroup
{
    kPlatformUnknown = 0,
    kPlatformStandalone = 1,
    // was kPlatformWebPlayer = 2,
    kPlatform_iPhone = 4,
    // was kPlatformPS3 = 5,
    // was kPlatformXBOX360 = 6,
    kPlatformAndroid = 7,
    // was kPlatformBroadcom = 8,
    // was kPlatformGLESEmu = 9,
    // was kPlatformNaCl = 11,
    // was kPlatformFlash = 12,
    kPlatformWebGL = 13,
    kPlatformMetro = 14,
    // was kPlatformWP8 = 15,
    // was kPlatformBB10 = 16,
    // was kPlatformTizen = 17,
    // was kPlatformPSP2  = 18,
    kPlatformPS4 = 19,
    // was kPlatformPSM  = 20,
    kPlatformXboxOne = 21,
    // was kPlatformSTV = 22,
    // was kPlatformN3DS = 23,
    // was kPlatformWiiU = 24,
    kPlatformtvOS = 25,
    // was kPlatformFacebook = 26,
    kPlatformSwitch = 27,
    // was kPlatformLumin = 28,
    kPlatformStadia = 29,
    kPlatformCloudRendering = 30,
    kPlatformGameCoreScarlett = 31,
    kPlatformGameCoreXboxOne = 32,
    kPlatformPS5 = 33,
    kPlatformEmbeddedLinux = 34,
    kPlatformQNX = 35,
    kPlatformCount
}

enum ColorSpace
{
    kUninitializedColorSpace = -1,
    kGammaColorSpace = 0,
    kLinearColorSpace,
    kColorSpaceCount,
    kCurrentColorSpace
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
}

enum PostprocessorType
{
    kPostprocessorNone = 0,
    kPostprocessorModel,
    kPostprocessorTexture,
    kPostprocessorAudio,
    kPostprocessorSpeedTree,
    kPostprocessorPrefab,
    kPostprocessorTypeCount
}

enum ScriptingRuntimeVersion
{
    kScriptingRuntimeVersionLegacy = 0,
    kScriptingRuntimeVersionLatest = 1
}

enum TextureImportCompression
{
    Uncompressed,
    Compressed
}
