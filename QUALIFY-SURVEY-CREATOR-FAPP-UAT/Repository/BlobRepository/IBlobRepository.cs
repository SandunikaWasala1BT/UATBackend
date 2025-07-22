namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository
{
    public interface IBlobRepository
    {
        Task<string> UploadBlobFile(string fileName, Stream content);
    }
}
