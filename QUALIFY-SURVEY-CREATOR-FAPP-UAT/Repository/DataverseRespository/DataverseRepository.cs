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

        public async Task<IEnumerable<FontStyleDto>> GetSurveyFontStyesAsync(string surveyidentifier)
        {
            try
            {
                string fetchQuery = @$"<fetch returntotalrecordcount=""true"">
                                      <entity name=""seer_qualifysurveyfontstyles"">
                                        <link-entity name=""seer_surveys"" from=""seer_surveysid"" to=""seer_survey"" link-type=""inner"" alias=""qs"">
                                          <filter>
                                            <condition attribute=""seer_surveyidentifier"" operator=""eq"" value=""{surveyidentifier}"" />
                                          </filter>
                                        </link-entity>
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
                        311830001 => "google",
                        311830002 => "cdn"
                    };
                    return new FontStyleDto(cssPropertyVal.Value.ToString()!, fontNameVal.Value.ToString()!, fontType);
                });
            }
            catch (Exception) 
            {
                throw;
            }
        }

        public async Task<Entity?> GetSurveyJsonAsync(string surveyidentifier)
        {
            try
            {
                if (_serviceClient.IsReady)
                {
                    string fetchQuery = @$"<fetch returntotalrecordcount=""true"">
                                  <entity name=""seer_surveys"">
                                    <attribute name=""seer_name"" />                                    
                                    <attribute name=""seer_json"" />
                                    <attribute name=""seer_licensingblock"" />
                                    <filter>
                                      <condition attribute=""seer_surveyidentifier"" operator=""eq"" value=""{surveyidentifier}"" />
                                      <condition attribute=""seer_surveystatus"" operator=""eq"" value=""311830000"" />
                                    </filter>
                                  </entity>
                                </fetch>";
                    
                    FetchExpression fetchExpression = new(fetchQuery);

                    var res = await _serviceClient.RetrieveMultipleAsync(fetchExpression);

                    if (res is null || res.TotalRecordCount == 0)
                    {
                        return null;
                    }

                    return res.Entities.FirstOrDefault() ?? throw new Exception("No survey found with the given slogan");
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

        public async Task<bool> IsLicenseBlocksConfiguredInDataverse(string newOriginUrl)
        {
            try
            {
                string query = $@"<fetch returntotalrecordcount=""true"">
                              <entity name=""seer_licensingblock"">
                                <link-entity name=""seer_surveys"" from=""seer_licensingblock"" to=""seer_licensingblockid"">
                                  <attribute name=""seer_licensingblock"" />
                                  <filter>
                                    <condition attribute=""seer_neworiginurl"" operator=""eq"" value=""{newOriginUrl}"" />
                                  </filter>
                                </link-entity>
                              </entity>
                            </fetch>";
                FetchExpression fetchQuery = new(query);
                var blocks = await _serviceClient.RetrieveMultipleAsync(fetchQuery);
                if(blocks.Entities.Count > 0)
                {
                    return true;
                }
                return false;
            } catch(Exception)
            {
                throw;
            }
        }
    }
}
