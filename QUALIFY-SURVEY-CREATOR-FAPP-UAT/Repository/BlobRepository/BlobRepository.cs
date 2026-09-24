
using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;
using System.ComponentModel;
using System.Net.Http;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository
{
    public class BlobRepository(BlobServiceClient blobServiceClient, IConfiguration configuration, ServiceClient serviceClient) : IBlobRepository
    {
        //private readonly BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobContainer"));
        private readonly BlobContainerClient _surveyContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobSurveyContainer"));
        private readonly BlobContainerClient _surveyTemplateContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobSurveyTemplateContainer"));

        private readonly ServiceClient _serviceClient = serviceClient;
        private readonly HttpClient httpClient = new HttpClient();

        public async Task GenerateBlobFolderFromTemplate(BlobFolderGenerateRequest request)
        {
            try
            {
                var templateFolderName = request.TemplatePath.Replace("https://gyde365qualifysurveyimg.blob.core.windows.net/partner-templates/", "") + "/";
                var blobs = _surveyTemplateContainerClient.GetBlobs(prefix: templateFolderName);
                foreach (var blobItem in blobs)
                {
                    var item = _surveyTemplateContainerClient.GetBlobClient(blobItem.Name);
                    var targetName = blobItem.Name.Replace(templateFolderName, request.FolderName + "/");
                    var targetBlob = _surveyContainerClient.GetBlobClient(targetName);
                    targetBlob.StartCopyFromUri(item.Uri);
                }
                string surveyNewOriginUrl = "https://survey-portal-uat-gxchbpcrc4fkbze3.uksouth-01.azurewebsites.net/" + request.FolderName;
                string surveyId = "";
                string originurl = "";
                JObject surveyData = null;
                var getSurveyDetailsFetch = String.Format(@"<fetch top='1'>
                      <entity name='seer_surveys'>
                        <attribute name='seer_surveysid' />
                        <attribute name='seer_originurl' />
                        <filter>
                          <condition attribute='seer_neworiginurl' operator='eq' value='{0}'/>
                        </filter>
                      </entity>
                    </fetch>", surveyNewOriginUrl);

                var getSurveyDetails = _serviceClient.RetrieveMultiple(new FetchExpression(getSurveyDetailsFetch));


                foreach (var getSurveyDetail in getSurveyDetails.Entities)
                {
                    if (getSurveyDetail.Attributes.ContainsKey("seer_surveysid"))
                    {
                        surveyId = getSurveyDetail.Attributes["seer_surveysid"].ToString();

                    }
                    if (getSurveyDetail.Attributes.ContainsKey("seer_originurl"))
                    {
                        originurl = getSurveyDetail.Attributes["seer_originurl"].ToString();

                    }
                }

                if (!string.IsNullOrEmpty(originurl))
                {
                    var SurveyFileUrl = originurl + "/Survey.json"; // Adjust the path accordingly

                    try
                    {
                        // FIX: Added 'await' here
                        var SurveyFileContent = await httpClient.GetStringAsync(SurveyFileUrl);
                        // 1. Send the HTTP GET request
                        HttpResponseMessage SurveyFileContentResponse = await httpClient.GetAsync(SurveyFileUrl);

                        // 2. Check if the response status code is successful (200-299 OK)
                        if (SurveyFileContentResponse.IsSuccessStatusCode)
                        {
                            // 3. Read the JSON string content
                            string surveyFileContent = await SurveyFileContentResponse.Content.ReadAsStringAsync();
                            // 4. Validate that the content is not empty or whitespace
                            if (!string.IsNullOrWhiteSpace(surveyFileContent))
                            {
                                var jsonDataDorSurvey = JsonConvert.DeserializeObject<dynamic>(surveyFileContent);
                                // Extract surveyId and assign it to a string variable
                                string SurveyJSSurveyId = jsonDataDorSurvey["surveyId"].ToString();
                                // Build the API URL with the extracted surveyId
                                string apiUrl = $"https://api.surveyjs.io/public/Survey/getSurvey?surveyId={SurveyJSSurveyId}";

                                try
                                {
                                    // Make an asynchronous GET request
                                    var response = await httpClient.GetAsync(apiUrl);

                                    if (response.IsSuccessStatusCode)
                                    {
                                        // Read the response content
                                        string surveyContent = await response.Content.ReadAsStringAsync();

                                        // Deserialize and process survey content
                                        surveyData = JObject.Parse(surveyContent);
                                    }
                                }
                                catch (Exception e)
                                {

                                    throw new Exception(e.Message);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {

                        throw new Exception(e.Message);
                    }
                }

                //return Task.CompletedTask;

                // Update entity in Dataverse
                if (!string.IsNullOrEmpty(surveyId))
                {
                    Entity surveyEntity = new Entity("seer_surveys", new Guid(surveyId));

                    surveyEntity["seer_bloburl"] = "https://gyde365qualifysurveyimg.blob.core.windows.net/userfunctions/" + request.FolderName + "/";
                    if (surveyData != null)
                    {
                        surveyEntity["seer_json"] = surveyData.ToString();
                    }
                    //Upodate Entity
                    _serviceClient.Update(surveyEntity);

                }

            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public async Task<string> UploadBlobFile(BlobImageUploadRequest request)
        {
            try
            {
                var path = $"{request.SurveyIdentifier}/Images/{request.FileName}";
                BlobClient blobClient;
                if (request.IsTemplate)
                {
                    blobClient = _surveyTemplateContainerClient.GetBlobClient(path);
                }
                else
                {
                    blobClient = _surveyContainerClient.GetBlobClient(path);
                }
                var status = await blobClient.UploadAsync(request.FileStream, true);
                return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}
