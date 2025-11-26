using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Xrm.Sdk;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Models.DTOs;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.BlobRepository;
using QUALIFY_SURVEY_CREATOR_FAPP_UAT.Repository.DataverseRespository;
using System.Drawing;

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

}