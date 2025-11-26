using Microsoft.Xrm.Sdk;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.DataverseRespository
{
    public interface IDataverseRepository
    {
        Task<Entity?> GetSurveyJsonAsync(string surveyidentifier);
        Task<IEnumerable<FontStyleDto>> GetSurveyFontStyesAsync(string surveyidentifier);
        Task<bool> IsLicenseBlocksConfiguredInDataverse(string newOriginUrl);
    }
}
