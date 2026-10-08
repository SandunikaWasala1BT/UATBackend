
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
using System.Text.RegularExpressions;

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
                string surveyJson = "";
                string surveyTemplateJson = "";
                string finalJson = "";
                string blobPath = "https://gyde365qualifysurveyimg.blob.core.windows.net/userfunctions/"+ request.FolderName + "/Images";
                //geeting survey details
                var getSurveyDetailsFetch = String.Format(@"<fetch top='1'>
                      <entity name='seer_surveys'>
                        <attribute name='seer_surveysid' />
                        <attribute name='seer_originurl' />
                        <attribute name='seer_json' />
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
                    if (getSurveyDetail.Attributes.ContainsKey("seer_json"))
                    {
                        surveyJson = getSurveyDetail.Attributes["seer_json"].ToString();

                    }
                }

                //getting selected survey template details
                var getSurveyTemplateDetailsFetch = String.Format(@"<fetch top='1'>
                      <entity name='seer_surveytemplate'>
                        <attribute name='seer_json' />
                        <filter>
                          <condition attribute='seer_surveytemplateid' operator='eq' value='{0}'/>
                        </filter>
                      </entity>
                    </fetch>", new Guid (request.TemplateId));

                var getSurveyTemplateDetails = _serviceClient.RetrieveMultiple(new FetchExpression(getSurveyTemplateDetailsFetch));


                foreach (var getSurveyTemplateDetail in getSurveyTemplateDetails.Entities)
                {
                    if (getSurveyTemplateDetail.Attributes.ContainsKey("seer_json"))
                    {
                        surveyTemplateJson = getSurveyTemplateDetail.Attributes["seer_json"].ToString();

                    }
                }

                if (!string.IsNullOrEmpty(surveyJson))
                {
                    finalJson = surveyJson;
                }
                else if (!string.IsNullOrEmpty(originurl))
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
                                
                                try
                                {
                                    JObject surveyScheme = await GetSurveyJsScheme(SurveyJSSurveyId);

                                    // 3. Process Logos
                                    // Assume surveyScheme is a JObject from Newtonsoft.Json
                                    if (surveyScheme["logo"] != null)
                                    {
                                        var logo = surveyScheme["logo"];


                                        // 1. Handle String Type (Single URL)
                                        if (logo.Type == JTokenType.String)
                                        {
                                            string logoValue = logo.ToString();
                                            // Regex to check for Base64 SVG (matches the PowerShell -match)
                                            bool isBase64Img = Regex.IsMatch(logoValue, @"^data:image\/svg\+xml;base64,[A-Za-z0-9+/=]+$");

                                            if (!isBase64Img)
                                            {
                                                string fileName = $"{request.FolderName}-logo.png";
                                                await UploadToBlob(logoValue, request.FolderName, fileName);

                                                // Update the URL in the JSON object
                                                surveyScheme["logo"] = $"{blobPath}/{fileName}";
                                            }
                                        }
                                        // 2. Handle Object Type (Multiple logos like { "default": "...", "dark": "..." })
                                        else if (logo.Type == JTokenType.Object)
                                        {
                                            var logoObject = (JObject)logo;
                                            // Iterate through all properties (similar to PSObject.Members)
                                            foreach (var property in logoObject.Properties())
                                            {
                                                string logoUrl = property.Value.ToString();
                                                string fileName = $"{request.FolderName}-logo-{property.Name}.png";

                                                await UploadToBlob(logoUrl, request.FolderName, fileName);

                                                // Update the specific property value with the new Blob URL
                                                property.Value = $"{blobPath}/{fileName}";
                                            }
                                        }

                                    }
                                    
                                    if (surveyScheme["logoWidth"] == null)
                                    {
                                        surveyScheme.Add("logoWidth", "300px");
                                    }
                                    else
                                    {
                                        //surveyScheme["logoWidth"] = "300px";
                                        surveyScheme["logoWidth"] = surveyScheme["logoWidth"].ToString();
                                    }

                                    if (surveyScheme["logoHeight"] == null)
                                    {
                                        surveyScheme.Add("logoHeight", "auto");
                                    }
                                    else
                                    {
                                        //surveyScheme["logoWidth"] = "300px";
                                        surveyScheme["logoHeight"] = surveyScheme["logoHeight"].ToString();
                                    }

                                    string jsonString = surveyScheme.ToString(Newtonsoft.Json.Formatting.None);
                                    string pattern = @"src=\\?""/Content/Images/([^\\?""]+)\\?""";
                                    string replacement = $"src=\\\"{blobPath}/$1\\\"";
                                    jsonString = Regex.Replace(jsonString, pattern, replacement);
                                    finalJson = jsonString;

                                }
                                catch (Exception e)
                                {
                                    finalJson = surveyTemplateJson;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        finalJson = surveyTemplateJson;
                    }
                }
                else
                {
                    finalJson = surveyTemplateJson;
                }

                //return Task.CompletedTask;

                // Update entity in Dataverse
                if (!string.IsNullOrEmpty(surveyId))
                {
                    Entity surveyEntity = new Entity("seer_surveys", new Guid(surveyId));

                    surveyEntity["seer_bloburl"] = "https://gyde365qualifysurveyimg.blob.core.windows.net/userfunctions/" + request.FolderName + "/";
                    
                    surveyEntity["seer_json"] = finalJson;
                    
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

        public async Task<JObject> GetSurveyJsScheme(string surveyId)
        {
            string uri = $"https://api.surveyjs.io/public/Survey/getSurvey?surveyId={surveyId}";
            var response = await httpClient.GetAsync(uri);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }

        public async System.Threading.Tasks.Task UploadToBlob(string url, string folderName, string fileName)
        {
            byte[] imageBytes = await httpClient.GetByteArrayAsync(url);
            
            string blobName = $"{folderName}/Images/{fileName}";
            BlobClient blobClient = _surveyContainerClient.GetBlobClient(blobName);

            using (var stream = new MemoryStream(imageBytes))
            {
                await blobClient.UploadAsync(stream, new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
                });
            }
        }

    }
}
