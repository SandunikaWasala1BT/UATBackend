using Microsoft.Xrm.Sdk;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.DataverseRespository
{
    public interface IDataverseRepository
    {
        Task<Entity> GetSurveyJson(string surveyId);
        Task<Entity> GetSurveyFont(Guid surveyId);
        Task<IEnumerable<FontStyleDto>> GetSurveyFontStyes(string surveyId);
    }
}
