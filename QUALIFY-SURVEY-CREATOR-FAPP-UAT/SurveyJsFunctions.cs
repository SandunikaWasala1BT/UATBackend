using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Xrm.Sdk;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.DataverseRespository;
using System.Drawing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

namespace QUALIFY_SURVEY_CREATOR_FAPP_UAT;

public class SurveyJsFunctions
{
    private readonly IBlobRepository _blobRepository;
    private readonly IDataverseRepository _dataverseRepository;
    public SurveyJsFunctions(IBlobRepository blobRepository, IDataverseRepository dataverseRepository)
    {
        _blobRepository = blobRepository;
        _dataverseRepository = dataverseRepository;
    }

    [Function("UploadsSurveyCreatorImageUploadsToBlob")]
    public async Task<IActionResult> RunUploadsSurveyCreatorImageUploadsToBlob([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        try
        {

            var form = await req.ReadFormAsync();

            if (form is null)
            {
                throw new Exception("Invalid arguments");
            }

            var surveyIdentifierRes = form.TryGetValue("surveyIdentifier", out var surveyIdentifier);
            var isTemplateRes = form.TryGetValue("isTemplate", out var isTemplate);
            if (!surveyIdentifierRes) {
                surveyIdentifier = "/default";
            }
            if (!isTemplateRes)
            {
                return new BadRequestObjectResult("The property isTemplate must be properly specified");
            }
            var result = bool.TryParse(isTemplate, out bool isTemplateBool);
            if (!result)
            {
                return new BadRequestObjectResult("The property isTemplate must be properly specified");
            }
            List<string> savedFileUrls = [];
            foreach (var file in form.Files)
            {
                var fileStream = file.OpenReadStream();
                var imageUploadRequest = new BlobImageUploadRequest(file.FileName, fileStream, surveyIdentifier, isTemplateBool);
                var savedUrl = await _blobRepository.UploadBlobFile(imageUploadRequest);
                savedFileUrls.Add(savedUrl);
            }
            return new OkObjectResult(new { urls = savedFileUrls });
        }
        catch (Exception ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
    }

    [Function("RetriveSurveyJsonFromDataverse")]
    public async Task<IActionResult> RunRetriveSurveyJsonFromDataverse([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        try
        {
            var surveySlogan = req.Query["slogan"];
            if (string.IsNullOrEmpty(surveySlogan))
            {
                return new BadRequestObjectResult(new { Error = "Invalid aurguments" });
            }

            var entity = await _dataverseRepository.GetSurveyJsonAsync(surveySlogan!);
            if (entity is null)
            {
                return new NotFoundResult();
            }
            if (!entity.Contains("seer_json"))
            {
                return new BadRequestObjectResult(new {Error="No scheme found"});
            }
            bool isLicenseBlocksSetUp = entity.Contains("seer_licensingblock");
            return new OkObjectResult(new { name = entity["seer_name"], content = entity["seer_json"], isLicenseBlocksSetUp});
        }
        catch(Exception ex)
        {
            return new BadRequestObjectResult(new { Error = ex.Message });
        }
    }

    [Function("GetFontStyles")]
    public async Task<IActionResult> RunGetFontStyles([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        try
        {
            var surveySlogan = req.Query["slogan"];
            if (string.IsNullOrEmpty(surveySlogan))
            {
                return new BadRequestObjectResult(new { Error = "Invalid aurgments" });
            }
            var res = await _dataverseRepository.GetSurveyFontStyesAsync(surveySlogan!);
            return new OkObjectResult(res);
        }
        catch (Exception ex)
        {
            return new BadRequestObjectResult(new { Error = ex.Message });
        }
    }

    [Function("GenerateBlobFolder")]
    public async Task<IActionResult> RunGenerateBlobFolder([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req, [Microsoft.Azure.Functions.Worker.Http.FromBody] BlobFolderGenerateRequest blobFolderGenerateRequest)
    {
        try
        {
            await _blobRepository.GenerateBlobFolderFromTemplate(blobFolderGenerateRequest);
            return new OkResult();
        }
        catch (Exception ex)
        {
            return new BadRequestObjectResult(new { Error = ex.Message });
        }
    }

    [Function("SyncChangesFromSurveyJS")]
    public async Task<IActionResult> RunSyncChangesFromSurveyJS([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var bodyJson = JsonNode.Parse(requestBody) ?? throw new Exception("Invalid request body");
            var originUrl = bodyJson["originUrl"]?.ToString() ?? throw new ArgumentException("Please provide the OriginUrl");

            if (!Uri.TryCreate(originUrl, UriKind.Absolute, out Uri uriResult)
                || (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("Please provide a valid OriginUrl");
            }

            string tenantId = "14519221-4d30-4936-bbc4-14e4abd033d2";
            string clientId = "9052a0c3-8da1-4aec-bfa2-52bef5def0f9";
            string clientSecret = "L4-8Q~rcrgMNH6BY4tU18-uijKlGDEETzIjXQc3D";
            string subscriptionId = "32d0adcd-7367-44fb-a08f-b53948f8367b";
            TokenCredential credentials = new ClientSecretCredential(tenantId, clientId, clientSecret);
            ArmClient client = new(credentials, subscriptionId);
            var resourceGroupName = "GYDE365-Qualify";
            var webAppName = originUrl.Replace("https://", "").Replace(".azurewebsites.net", "");
            SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();
            ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
            ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(resourceGroupName);
            var webapp = await resourceGroup.GetWebSiteAsync(webAppName);
            var publishProfile = webapp.Value.GetPublishingProfileXmlWithSecrets(new CsmPublishingProfile
            {
                Format = PublishingProfileFormat.WebDeploy
            });

            string xml;
            publishProfile.Value.Position = 0; // Reset stream to start
            using (var reader = new StreamReader(publishProfile.Value))
            {
                xml = await reader.ReadToEndAsync();
            }
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var node = doc.SelectSingleNode("//publishProfile[@publishMethod='MSDeploy']") ?? throw new Exception("Publish profile not found");
            var userName = node.Attributes?["userName"]?.Value ?? throw new Exception("Username not found in publish profile");
            var userPWD = node.Attributes?["userPWD"]?.Value ?? throw new Exception("Password not found in publish profile");
            var base64credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{userPWD}"));

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {base64credentials}");
            var surveyIdResponse = await httpClient.GetAsync($"https://{webAppName}.scm.azurewebsites.net/api/vfs/site/wwwroot/Survey.json");
            if (!surveyIdResponse.IsSuccessStatusCode)
            {
                throw new Exception("Failed to retrieve Survey.json from the web app");
            }
            var surveyIdContent = await surveyIdResponse.Content.ReadAsStringAsync();
            var surveyIdJson = JsonNode.Parse(surveyIdContent) ?? throw new Exception("Invalid Survey.json content");
            var surveyId = surveyIdJson["surveyId"]?.ToString() ?? throw new Exception("surveyIdentifier not found in Survey.json");

            var surveyJsonResponse = await new HttpClient().GetAsync($"https://api.surveyjs.io/public/Survey/getSurvey?surveyId={surveyId}");
            if (!surveyJsonResponse.IsSuccessStatusCode)
                throw new Exception("Failed to retrieve JSON scheme from SurveyJS");
            var surveyJsonContent = await surveyJsonResponse.Content.ReadAsStringAsync();
            var surveyJson = JsonNode.Parse(surveyJsonContent) ?? throw new Exception("Invalid survey JSON scheme content");

            return new OkObjectResult(surveyJson);
        }
        catch (Exception ex)
        {
            return new BadRequestObjectResult(new { Error = ex.Message });
        }
    }

}