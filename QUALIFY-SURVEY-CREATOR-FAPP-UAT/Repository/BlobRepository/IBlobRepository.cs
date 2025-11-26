using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository
{
    public interface IBlobRepository
    {
        Task<string> UploadBlobFile(BlobImageUploadRequest request);
        Task GenerateBlobFolderFromTemplate(BlobFolderGenerateRequest request);
    }
}
