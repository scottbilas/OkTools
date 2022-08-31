namespace OkTools.ProcMonUtils.FileSystem;

public enum FileSystemOperation
{
    // ReSharper disable InconsistentNaming CommentTypo IdentifierTypo
    #pragma warning disable CA1707

    VolumeDismount                             = 0,  // IRP_MJ_VOLUME_DISMOUNT
    VolumeMount                                = 1,  // IRP_MJ_VOLUME_MOUNT
    FASTIO_MDL_WRITE_COMPLETE                  = 2,  // FASTIO_MDL_WRITE_COMPLETE
    WriteFile2                                 = 3,  // FASTIO_PREPARE_MDL_WRITE
    FASTIO_MDL_READ_COMPLETE                   = 4,  // FASTIO_MDL_READ_COMPLETE
    ReadFile2                                  = 5,  // FASTIO_MDL_READ
    QueryOpen                                  = 6,  // FASTIO_NETWORK_QUERY_OPEN
    FASTIO_CHECK_IF_POSSIBLE                   = 7,  // FASTIO_CHECK_IF_POSSIBLE
    IRP_MJ_12                                  = 8,  // IRP_MJ_12
    IRP_MJ_11                                  = 9,  // IRP_MJ_11
    IRP_MJ_10                                  = 10, // IRP_MJ_10
    IRP_MJ_9                                   = 11, // IRP_MJ_9
    IRP_MJ_8                                   = 12, // IRP_MJ_8
    FASTIO_NOTIFY_STREAM_FO_CREATION           = 13, // FASTIO_NOTIFY_STREAM_FO_CREATION
    FASTIO_RELEASE_FOR_CC_FLUSH                = 14, // FASTIO_RELEASE_FOR_CC_FLUSH
    FASTIO_ACQUIRE_FOR_CC_FLUSH                = 15, // FASTIO_ACQUIRE_FOR_CC_FLUSH
    FASTIO_RELEASE_FOR_MOD_WRITE               = 16, // FASTIO_RELEASE_FOR_MOD_WRITE
    FASTIO_ACQUIRE_FOR_MOD_WRITE               = 17, // FASTIO_ACQUIRE_FOR_MOD_WRITE
    FASTIO_RELEASE_FOR_SECTION_SYNCHRONIZATION = 18, // FASTIO_RELEASE_FOR_SECTION_SYNCHRONIZATION
    CreateFileMapping                          = 19, // FASTIO_ACQUIRE_FOR_SECTION_SYNCHRONIZATION
    CreateFile                                 = 20, // IRP_MJ_CREATE
    CreatePipe                                 = 21, // IRP_MJ_CREATE_NAMED_PIPE
    IRP_MJ_CLOSE                               = 22, // IRP_MJ_CLOSE
    ReadFile                                   = 23, // IRP_MJ_READ
    WriteFile                                  = 24, // IRP_MJ_WRITE
    QueryInformationFile                       = 25, // IRP_MJ_QUERY_INFORMATION
    SetInformationFile                         = 26, // IRP_MJ_SET_INFORMATION
    QueryEAFile                                = 27, // IRP_MJ_QUERY_EA
    SetEAFile                                  = 28, // IRP_MJ_SET_EA
    FlushBuffersFile                           = 29, // IRP_MJ_FLUSH_BUFFERS
    QueryVolumeInformation                     = 30, // IRP_MJ_QUERY_VOLUME_INFORMATION
    SetVolumeInformation                       = 31, // IRP_MJ_SET_VOLUME_INFORMATION
    DirectoryControl                           = 32, // IRP_MJ_DIRECTORY_CONTROL
    FileSystemControl                          = 33, // IRP_MJ_FILE_SYSTEM_CONTROL
    DeviceIoControl                            = 34, // IRP_MJ_DEVICE_CONTROL
    InternalDeviceIoControl                    = 35, // IRP_MJ_INTERNAL_DEVICE_CONTROL
    Shutdown                                   = 36, // IRP_MJ_SHUTDOWN
    LockUnlockFile                             = 37, // IRP_MJ_LOCK_CONTROL
    CloseFile                                  = 38, // IRP_MJ_CLEANUP
    CreateMailSlot                             = 39, // IRP_MJ_CREATE_MAILSLOT
    QuerySecurityFile                          = 40, // IRP_MJ_QUERY_SECURITY
    SetSecurityFile                            = 41, // IRP_MJ_SET_SECURITY
    Power                                      = 42, // IRP_MJ_POWER
    SystemControl                              = 43, // IRP_MJ_SYSTEM_CONTROL
    DeviceChange                               = 44, // IRP_MJ_DEVICE_CHANGE
    QueryFileQuota                             = 45, // IRP_MJ_QUERY_QUOTA
    SetFileQuota                               = 46, // IRP_MJ_SET_QUOTA
    PlugAndPlay                                = 47, // IRP_MJ_PNP

    // ReSharper restore InconsistentNaming CommentTypo IdentifierTypo
    #pragma warning restore CA1707
}

public enum QueryVolumeInformationOperation
{
    QueryInformationVolume          = 0x1,
    QueryLabelInformationVolume     = 0x2,
    QuerySizeInformationVolume      = 0x3,
    QueryDeviceInformationVolume    = 0x4,
    QueryAttributeInformationVolume = 0x5,
    QueryControlInformationVolume   = 0x6,
    QueryFullSizeInformationVolume  = 0x7,
    QueryObjectIdInformationVolume  = 0x8,
}

public enum SetVolumeInformationOperation
{
    SetControlInformationVolume  = 0x1,
    SetLabelInformationVolume    = 0x2,
    SetObjectIdInformationVolume = 0x8,
}

public enum QueryInformationFileOperation
{
    #pragma warning disable CA1711

    QueryBasicInformationFile                     = 0x4,
    QueryStandardInformationFile                  = 0x5,
    QueryFileInternalInformationFile              = 0x6,
    QueryEaInformationFile                        = 0x7,
    QueryNameInformationFile                      = 0x9,
    QueryPositionInformationFile                  = 0xe,
    QueryAllInformationFile                       = 0x12,
    QueryEndOfFile                                = 0x14,
    QueryStreamInformationFile                    = 0x16,
    QueryCompressionInformationFile               = 0x1c,
    QueryId                                       = 0x1d,
    QueryMoveClusterInformationFile               = 0x1f,
    QueryNetworkOpenInformationFile               = 0x22,
    // QueryAttributeTag                          = 0x23, // consts.py has this commented out
    QueryAttributeTagFile                         = 0x23,
    QueryIdBothDirectory                          = 0x25,
    QueryValidDataLength                          = 0x27,
    QueryShortNameInformationFile                 = 0x28,
    QueryIoPriorityHint                           = 0x2b,
    QueryLinks                                    = 0x2e,
    QueryNormalizedNameInformationFile            = 0x30,
    QueryNetworkPhysicalNameInformationFile       = 0x31,
    QueryIdGlobalTxDirectoryInformation           = 0x32,
    QueryIsRemoteDeviceInformation                = 0x33,
    QueryAttributeCacheInformation                = 0x34,
    QueryNumaNodeInformation                      = 0x35,
    QueryStandardLinkInformation                  = 0x36,
    QueryRemoteProtocolInformation                = 0x37,
    QueryRenameInformationBypassAccessCheck       = 0x38,
    QueryLinkInformationBypassAccessCheck         = 0x39,
    QueryVolumeNameInformation                    = 0x3a,
    QueryIdInformation                            = 0x3b,
    QueryIdExtendedDirectoryInformation           = 0x3c,
    QueryHardLinkFullIdInformation                = 0x3e,
    QueryIdExtendedBothDirectoryInformation       = 0x3f,
    QueryDesiredStorageClassInformation           = 0x43,
    QueryStatInformation                          = 0x44,
    QueryMemoryPartitionInformation               = 0x45,
    QuerySatLxInformation                         = 0x46,
    QueryCaseSensitiveInformation                 = 0x47,
    QueryLinkInformationEx                        = 0x48,
    QueryLinkInformationBypassAccessCheck2        = 0x49, // consts.py has bad spelling QueryLinkInfomraitonBypassAccessCheck
    QueryStorageReservedIdInformation             = 0x4a,
    QueryCaseSensitiveInformationForceAccessCheck = 0x4b,

    #pragma warning restore CA1711
}

public enum SetInformationFileOperation
{
    #pragma warning disable CA1711

    SetBasicInformationFile                 = 0x4,
    SetRenameInformationFile                = 0xa,
    SetLinkInformationFile                  = 0xb,
    SetDispositionInformationFile           = 0xd,
    SetPositionInformationFile              = 0xe,
    SetAllocationInformationFile            = 0x13,
    SetEndOfFileInformationFile             = 0x14,
    SetFileStreamInformation                = 0x16,
    SetPipeInformation                      = 0x17,
    SetValidDataLengthInformationFile       = 0x27,
    SetShortNameInformation                 = 0x28,
    SetReplaceCompletionInformation         = 0x3d,
    SetDispositionInformationEx             = 0x40,
    SetRenameInformationEx                  = 0x41,
    SetRenameInformationExBypassAccessCheck = 0x42,
    SetStorageReservedIdInformation         = 0x4a,

    #pragma warning restore CA1711
}

public enum DirectoryControlOperation
{
    QueryDirectory        = 0x1,
    NotifyChangeDirectory = 0x2,
}

public enum PlugAndPlayOperation
{
    StartDevice                = 0x0,
    QueryRemoveDevice          = 0x1,
    RemoveDevice               = 0x2,
    CancelRemoveDevice         = 0x3,
    StopDevice                 = 0x4,
    QueryStopDevice            = 0x5,
    CancelStopDevice           = 0x6,
    QueryDeviceRelations       = 0x7,
    QueryInterface             = 0x8,
    QueryCapabilities          = 0x9,
    QueryResources             = 0xa,
    QueryResourceRequirements  = 0xb,
    QueryDeviceText            = 0xc,
    FilterResourceRequirements = 0xd,
    ReadConfig                 = 0xf,
    WriteConfig                = 0x10,
    Eject                      = 0x11,
    SetLock                    = 0x12,
    QueryId2                   = 0x13,
    QueryPnpDeviceState        = 0x14,
    QueryBusInformation        = 0x15,
    DeviceUsageNotification    = 0x16,
    SurpriseRemoval            = 0x17,
    QueryLegacyBusInformation  = 0x18,
}

public enum LockUnlockFileOperation
{
    // ReSharper disable CommentTypo

    LockFile         = 0x1,  // IRP_MJ_LOCK_CONTROL, FASTIO_LOCK
    UnlockFileSingle = 0x2,  // IRP_MJ_LOCK_CONTROL, FASTIO_UNLOCK_SINGLE
    UnlockFileAll    = 0x3,  // IRP_MJ_LOCK_CONTROL, FASTIO_UNLOCK_ALL
    UnlockFileByKey  = 0x4,  // IRP_MJ_LOCK_CONTROL, FASTIO_UNLOCK_ALL_BY_KEY

    // ReSharper restore CommentTypo
}

public enum FileOperationPriority
{
    VeryLow  = 1,
    Low      = 2,
    Normal   = 3,
    High     = 4,
    Critical = 5,
}

[Flags]
#pragma warning disable CA1711
public enum FileOperationIoFlags
#pragma warning restore CA1711
{
    Buffered            = 0x10,
    NonCached           = 0x1,
    PagingIo            = 0x2,
    Synchronous         = 0x4,
    SynchronousPagingIo = 0x40,
    WriteThrough        = 0x400000,
}
