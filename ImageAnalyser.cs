using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using ImageAnalysisAPI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ImageAnalyserFunction
{
    [Serializable]
    public class DetectionResult
    {
        public DetectionResult() { } // Parameterless constructor

        public List<Label> Labels { get; set; }
        public List<ModerationLabel> ExplicitContent { get; set; }
    }

    public static class ImageContentFunction
    {
        private static AmazonRekognitionClient rekognitionClient;

        [FunctionName("ImageContentFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                // Retrieve settings from Azure Functions configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();



                var accessKey = config["AWS_ACCESS_KEY"];
                var secretKey = config["AWS_SECRET_KEY"];
                var region = config["AWS_REGION"];

                if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(region))
                {
                    throw new InvalidOperationException("AWS credentials or region not configured.");
                }


                var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
                rekognitionClient = new AmazonRekognitionClient(credentials, RegionEndpoint.GetBySystemName(region));

                var file = req.Form.Files[0];
                var imageBytes = await ReadImageFile(file);

                //var labelResponse = await DetectLabels(imageBytes);
                var moderationResponse = await DetectModerationLabels(imageBytes);

                var result = new DetectionResult
                {
                    //Labels = labelResponse.Labels,
                    ExplicitContent = moderationResponse.ModerationLabels
                };

                List<ExplicitCategory> explicitCategories = new List<ExplicitCategory>();
                List<ExplicitCategory> explicitParentCategories = new List<ExplicitCategory>();
                foreach (var moderationLabel in result.ExplicitContent)
                {
                    // extract secondary moderation labels to list
                    if (!explicitCategories.Any(item => item.Name == moderationLabel.Name) && !moderationLabel.ParentName.Equals(""))
                    {
                        ExplicitCategory explicitCategory = new ExplicitCategory();
                        explicitCategory.Name = moderationLabel.Name;
                        explicitCategory.Confidence = moderationLabel.Confidence;
                        explicitCategories.Add(explicitCategory);
                    }

                    // extract parent moderation labels to list
                    if (!explicitParentCategories.Any(item => item.Name == moderationLabel.ParentName) && moderationLabel.ParentName.Equals(""))
                    {
                        ExplicitCategory explicitParentCategory = new ExplicitCategory();
                        explicitParentCategory.Name = moderationLabel.Name;
                        explicitParentCategory.Confidence = moderationLabel.Confidence;
                        explicitParentCategories.Add(explicitParentCategory);
                    }
                    
                }
                // Create a dictionary to represent the result
                var resultMap = new Dictionary<string, List<ExplicitCategory>>
                {
                    { "explicitCategories", explicitCategories }, 
                    { "explicitParentCategories", explicitParentCategories }
                };

                return new OkObjectResult(resultMap);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { Error = ex.Message });
            }
        }

        private static async Task<byte[]> ReadImageFile(IFormFile file)
        {
            using (var imageStream = file.OpenReadStream())
            {
                var imageBytes = new byte[imageStream.Length];
                await imageStream.ReadAsync(imageBytes, 0, (int)imageStream.Length);
                return imageBytes;
            }
        }

       /* private static async Task<DetectLabelsResponse> DetectLabels(byte[] imageBytes)
        {
            var labelRequest = new DetectLabelsRequest
            {
                Image = new Image
                {
                    Bytes = new MemoryStream(imageBytes)
                }
            };

            return await rekognitionClient.DetectLabelsAsync(labelRequest);
        }*/

        private static async Task<DetectModerationLabelsResponse> DetectModerationLabels(byte[] imageBytes)
        {
            var moderationRequest = new DetectModerationLabelsRequest
            {
                Image = new Image
                {
                    Bytes = new MemoryStream(imageBytes)
                }
            };

            return await rekognitionClient.DetectModerationLabelsAsync(moderationRequest);
        }
    }
}
