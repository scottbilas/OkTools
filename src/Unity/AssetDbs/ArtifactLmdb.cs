namespace OkTools.Unity;

public class ArtifactLmdb : AssetLmdb
{
    const uint k_expectedDbVersion = 0x8EC31D8B;

    public ArtifactLmdb(NPath projectRoot)
        : base(projectRoot.Combine(UnityProjectConstants.ArtifactDbNPath), k_expectedDbVersion) {}
}

// TODO: ArtifactIDPropertyIDToProperty
// TODO: ArtifactIDToArtifactDependencies
// TODO: ArtifactIDToArtifactMetaInfo
// TODO: ArtifactIDToImportStats
// TODO: ArtifactKeyToArtifactIDs
// TODO: CurrentRevisions
