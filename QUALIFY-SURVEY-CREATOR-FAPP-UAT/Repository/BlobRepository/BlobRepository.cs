
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;
using System.ComponentModel;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository
{
    public class BlobRepository(BlobServiceClient blobServiceClient, IConfiguration configuration) : IBlobRepository
    {
        //private readonly BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobContainer"));
        private readonly BlobContainerClient _surveyContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobSurveyContainer"));
        private readonly BlobContainerClient _surveyTemplateContainerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobSurveyTemplateContainer"));

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
