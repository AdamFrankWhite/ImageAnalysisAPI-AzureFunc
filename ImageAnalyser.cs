using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using ImageAnalysisAPI;
using ImageAnalysisAPI.models;
using ImageAnalysisAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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
        private static ImageModerationService _imageModerationService;
        private static IConfigurationRoot config = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables()
                   .Build();

        private static List<ExplicitCategory> explicitCategories;
        private static List<ExplicitCategory> explicitParentCategories;
        private static ImageAnalysisAPI.models.Image imageDataObject = new ImageAnalysisAPI.models.Image();
        [FunctionName("analyse")]
        public static async Task<IActionResult> RunAnalyse(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            RunRekognitionClient();




            
            try
            {
                var file = req.Form.Files[0];
                var imageBytes = await ReadImageFile(file);

                await AnalyseImage(imageBytes);
                SetImageDataObject(file);
                


                //TODO - if no explicit categories, return success message
                // Determine the response format based on the Accept header
                var acceptHeader = req.Headers["Accept"].ToString().ToLower();
                if (acceptHeader.Contains("application/xml"))
                {
                    // Return XML response
                    var xmlSerializer = new XmlSerializer(typeof(ImageAnalysisAPI.models.Image));
                    var xmlResult = new StringWriter();
                    xmlSerializer.Serialize(xmlResult, imageDataObject);
                    Console.WriteLine(xmlResult.ToString());
                    return new ContentResult
                    {
                        Content = xmlResult.ToString(),
                        ContentType = "application/xml",
                        StatusCode = StatusCodes.Status200OK
                    };
                }
                else
                {
                    Console.WriteLine(imageDataObject.Filename);
                    // Return JSON response
                    return new OkObjectResult(imageDataObject);
                }
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new { Error = ex.Message });
            }
        }


        [FunctionName("moderate")]
        public static async Task<IActionResult> RunModerate(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var file = req.Form.Files[0];
            RunRekognitionClient();
            var moderationResult = await UseModerationQueueService(file);
            return new OkObjectResult(moderationResult);
        }

        private static void SetImageDataObject(IFormFile file)
        {
            
            // add image to user imagemoderation queue db record
            imageDataObject.OwnerId = "owner";
            imageDataObject.Filename = file.FileName;
            imageDataObject.ImageUrl = "http://placeholder.com";
            imageDataObject.ExplicitCategories = explicitCategories;
            imageDataObject.ExplicitParentCategories = explicitParentCategories;
            
        }

        private static async Task<IActionResult> AnalyseImage(byte[] imageBytes)
        {
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
            return new OkObjectResult("success");
        }
        private static void RunRekognitionClient()
        {
            // singleton 
            if (rekognitionClient == null)
            {
                try
                {
                    // Retrieve settings from Azure Functions configuration
                    var accessKey = config["AWS_ACCESS_KEY"];
                    var secretKey = config["AWS_SECRET_KEY"];
                    var region = config["AWS_REGION"];

                    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(region))
                    {
                        throw new InvalidOperationException("AWS credentials or region not configured.");
                    }
                    var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
                    rekognitionClient = new AmazonRekognitionClient(credentials, RegionEndpoint.GetBySystemName(region));



                }
                catch (Exception ex)
                {
                    // Handle the exception or rethrow it
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
                
        }
            private static async Task<IActionResult> UseModerationQueueService(IFormFile file)
        {
            if (_imageModerationService == null)
            {
                var ConnectionString = config["ConnectionString"];
                var DatabaseName = config["DatabaseName"];
                var ImagesCollectionName = config["ImagesCollectionName"];
                Console.WriteLine($"{ConnectionString}, {DatabaseName}, {ImagesCollectionName}");
                // Instantiate the service only if it's not already created
                _imageModerationService = new ImageModerationService(ConnectionString, DatabaseName, ImagesCollectionName);
            }
            
            
            SetImageDataObject(file);
            //TODO - send image to moderation bucket, send file data, explicicit categories to mongodb
            // add image to user imagemoderation queue db record
            // send explicit image data to db
            try
            {
                await _imageModerationService.CreateAsync(imageDataObject);
                return new OkObjectResult("Explicit image added to moderation queue");
            } catch
            {
                return new OkObjectResult("There has been a problem");
            }
            // TODO - send image to moderation bucket
            
            
            

        }

       /* private static ImageAnalysisAPI.models.Image createImageModel(string filename)
        {
            // add image to user imagemoderation queue db record
            ImageAnalysisAPI.models.Image imageDataObject = new ImageAnalysisAPI.models.Image();
            imageDataObject.OwnerId = "owner";
            imageDataObject.Filename = filename;
            imageDataObject.ImageUrl = "http://placeholder.com";
            imageDataObject.ExplicitCategories = explicitCategories;
            imageDataObject.ExplicitParentCategories = explicitParentCategories;

            return imageDataObject;
        }*/
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
                Image = new Amazon.Rekognition.Model.Image
                {
                    Bytes = new MemoryStream(imageBytes)
                }
            };

            return await rekognitionClient.DetectModerationLabelsAsync(moderationRequest);
        }
    }
}
