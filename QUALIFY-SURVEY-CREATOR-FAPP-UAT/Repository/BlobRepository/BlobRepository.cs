
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
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

        public Task GenerateBlobFolderFromTemplate(BlobFolderGenerateRequest request)
        {
            try
            {
                var templateFolderName = request.TemplatePath.Replace("https://gyde365qualifysurveyimg.blob.core.windows.net/partner-templates/", "") + "/";
                var blobs = _surveyTemplateContainerClient.GetBlobs(prefix: templateFolderName);
                foreach(var blobItem in blobs)
                {
                    var item = _surveyTemplateContainerClient.GetBlobClient(blobItem.Name);
                    var targetName = blobItem.Name.Replace(templateFolderName, request.FolderName+"/");
                    var targetBlob = _surveyContainerClient.GetBlobClient(targetName);
                    targetBlob.StartCopyFromUri(item.Uri);
                }
                string surveyNewOriginUrl = "https://survey-portal-uat-gxchbpcrc4fkbze3.uksouth-01.azurewebsites.net/"+ request.FolderName;
                string surveyId = "";
                var getSurveyDetailsFetch = String.Format(@"<fetch top='1'>
                      <entity name='seer_surveys'>
                        <attribute name='seer_surveysid' />
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
                }

                // Create an instance of the entity you want to update
                Entity surveyEntity = new Entity("seer_surveys", new Guid(surveyId));

                surveyEntity["seer_bloburl"] = "https://gyde365qualifysurveyimg.blob.core.windows.net/userfunctions/" + request.FolderName +"/";
                //Upodate Entity
                _serviceClient.Update(surveyEntity);

                return Task.CompletedTask; 
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
                } else
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
