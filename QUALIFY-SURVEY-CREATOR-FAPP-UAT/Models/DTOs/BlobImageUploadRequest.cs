namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs
{
    public record BlobImageUploadRequest(string FileName, Stream FileStream, string SurveyIdentifier, 
        bool IsTemplate);
}
