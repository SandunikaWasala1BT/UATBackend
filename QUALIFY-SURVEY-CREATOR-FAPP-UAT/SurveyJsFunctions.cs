using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Xrm.Sdk;
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

            List<string> savedFileUrls = [];
            foreach (var file in form.Files)
            {
                var fileStream = file.OpenReadStream();
                var savedUrl = await _blobRepository.UploadBlobFile(file.FileName, fileStream);
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
            var surveyId = req.Query["id"];
            if (string.IsNullOrEmpty(surveyId))
            {
                return new BadRequestObjectResult(new { Error = "Invalid aurguments" });
            }

            var entity = await _dataverseRepository.GetSurveyJson(surveyId!);
            if (entity is null)
            {
                return new NotFoundResult();
            }
            if (!entity.Contains("seer_json"))
            {
                return new BadRequestObjectResult(new {Error="No scheme found"});
            }
            
            return new OkObjectResult(new { content = entity["seer_json"], originUrl = entity["seer_originurl"]});
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
            var surveyId = req.Query["id"];
            if (string.IsNullOrEmpty(surveyId))
            {
                return new BadRequestObjectResult(new { Error = "Invalid aurgments" });
            }
            var res = await _dataverseRepository.GetSurveyFontStyes(surveyId!);
            return new OkObjectResult(res);
        }
        catch (Exception ex)
        {
            return new BadRequestObjectResult(new { Error = ex.Message });
        }
    }
}