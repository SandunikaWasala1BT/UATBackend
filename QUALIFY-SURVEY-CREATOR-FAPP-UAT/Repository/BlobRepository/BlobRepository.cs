
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository
{
    public class BlobRepository(BlobServiceClient blobServiceClient, IConfiguration configuration) : IBlobRepository
    {
        private readonly BlobContainerClient _containerClient = blobServiceClient.GetBlobContainerClient(configuration.GetValue<string>("AzureBlobContainer"));

        public async Task<string> UploadBlobFile(string fileName, Stream content)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                var status = await blobClient.UploadAsync(content, true); 
                return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception e) 
            {
                throw new Exception(e.Message);
            }
        }
    }
}
