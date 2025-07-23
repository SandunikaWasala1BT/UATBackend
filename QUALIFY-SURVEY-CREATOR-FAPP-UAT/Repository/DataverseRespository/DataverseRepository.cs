using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.DataverseRespository
{
    public class DataverseRepository(ServiceClient serviceClient, ILogger<DataverseRepository> logger) : IDataverseRepository
    {
        private readonly ServiceClient _serviceClient = serviceClient;
        private readonly ILogger<DataverseRepository> _logger = logger;

        public async Task<IEnumerable<FontStyleDto>> GetSurveyFontStyes(string surveyId)
        {
            try
            {
                string fetchQuery = @$"<fetch returntotalrecordcount=""true"">
                                  <entity name=""seer_qualifysurveyfontstyles"">
                                    <filter>
                                      <condition attribute=""seer_survey"" operator=""eq"" value=""{surveyId}"">
                                        <value>7a1b8338-da24-f011-8c4e-00224842ca4d</value>
                                      </condition>
                                    </filter>
                                    <link-entity name=""seer_qualifyfont"" from=""seer_qualifyfontid"" to=""seer_font"" alias=""qf"">
                                      <attribute name=""seer_name"" />
                                      <attribute name=""seer_type"" />
                                    </link-entity>
                                    <link-entity name=""seer_qualifyfontmaster"" from=""seer_qualifyfontmasterid"" to=""seer_qualifyfontmaster"" alias=""qfm"">
                                      <attribute name=""seer_cssselector"" />
                                    </link-entity>
                                  </entity>
                                </fetch>";
                FetchExpression fetchExpression = new(fetchQuery);
                var res = await _serviceClient.RetrieveMultipleAsync(fetchExpression);
                if (res is null || res.TotalRecordCount == 0)
                {
                    return [];
                }
                return res.Entities.Select((entity) =>
                {
                    var cssPropertyVal = entity["qfm.seer_cssselector"] as AliasedValue;
                    var fontNameVal = entity["qf.seer_name"] as AliasedValue;
                    var fontTypeVal = entity["qf.seer_type"] as AliasedValue;
                    if (cssPropertyVal is null || fontNameVal is null || fontTypeVal is null)
                    {
                        throw new Exception("Some properties are missing");
                    }

                    var fontTypeSet = fontTypeVal.Value as OptionSetValue;
                    if (fontTypeSet is null)
                    {
                        throw new Exception("Font type is missing");
                    }
                    var fontType = fontTypeSet.Value switch
                    {
                        311830000 => "default",
                        311830001 => "google"
                    };
                    return new FontStyleDto(cssPropertyVal.Value.ToString()!, fontNameVal.Value.ToString()!, fontType);
                });
            }
            catch (Exception) 
            {
                throw;
            }
        }

        public async Task<Entity> GetSurveyFont(Guid surveyId)
        {
            try
            {
                var entity = await _serviceClient.RetrieveAsync("seer_qualifysurveyfont", surveyId, new ColumnSet("seer_name"));
                return entity;
            }catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<Entity> GetSurveyJson(string surveyId)
        {
            try
            {
                if (_serviceClient.IsReady)
                {
                    var entity = await _serviceClient.RetrieveAsync("seer_surveys", Guid.Parse(surveyId), new ColumnSet("seer_json", "seer_originurl", "seer_name"));
                    return entity;
                }
                else
                {
                    throw new Exception("Unable to connect with Dataverse");
                }
            }
            catch (Exception ex) 
            {
                throw new Exception(ex.Message);
            }
        }


    }
}
